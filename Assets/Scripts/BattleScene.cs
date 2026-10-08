using System.Collections.Generic;
using UnityEngine;

// 레이어로 나뉜 전투 배경 (Resources/StageScenes/stage<번호>/).
//   background.jpg  움직이지 않는 배경
//   <조각>.png       바람에 흔들리는 풀 · 나뭇가지
//   layout.txt      원본 그림(1280x720) 기준 위치 (Art/import_scene.py가 만듦)
//     zoom=1.2            배경을 화면 아래쪽 기준으로 확대 (땅이 넓게 보이도록)
//     lanes=470,690       유닛이 다닐 땅의 위쪽 / 아래쪽 (원본 그림의 y)
//     sway=그림,폭,높이,고정점x,고정점y,장면x,장면y,위상,bend|rotate      바람에 흔들리는 풀 · 가지
//     drift=그림,가운데x,가운데y,폭,높이,이동x,이동y,주기,위상,투명도        흘러가며 생겼다 사라지는 구름
//     herd=그림이름앞부분,장면수,시작x,이동거리,주기,시작위상,초당장면수     달리는 무리 (아래 horse 줄들이 한 마리씩)
//     horse=무리안에서뒤처진거리,발y,폭,높이,장면위상
//     bob=그림,가운데x,가운데y,폭,높이,오르내림픽셀,흔들림각도,주기          물 위에서 출렁이는 배
//     foam=그림,가운데x,가운데y,폭,높이,각도,주기,위상,투명도              해안으로 밀려오는 물거품
//     glint=그림,주기,최소투명도,최대투명도                                   화면 전체에 반짝이는 물빛
//     (sway 줄 끝에 back|front, 기울기(도)를 더 붙일 수 있음. flag 줄 끝의 1 = 깃대가 오른쪽. flame 줄 끝 r,g,b = 불티 · 불빛 색)
//     beam=그림,왼쪽,위,폭,높이,빛나는곳x,y,땅x,y,위상                         쏟아지는 빛줄기 (살짝 기울며 밝기가 바뀜)
//     motes=개수                                                              빛줄기 안에서 떠오르는 먼지
//     sparkle=그림,x,y,크기,주기,위상,빛나는시간                               금화 위에서 반짝 빛나는 빛
//     blink=그림,왼쪽,위,폭,높이,반복주기,깜빡이는시간,시작초...               어둠 속에서 깜빡이는 눈
//     hand=그림,x,땅y,폭,높이,반복주기,솟기시작,다솟음,들어가기시작,사라짐     땅에서 솟아올랐다 들어가는 해골 손
//     banner=그림,위가운데x,y,폭,높이,위상                                    위가 고정되어 아래로 갈수록 펄럭이는 깃발
//     mist=그림,왼쪽,위,폭,높이,투명도                                        옆으로 흘러가는 바닥 안개
// 이 폴더가 없는 스테이지는 예전처럼 Backgrounds/battle_<번호>.jpg 한 장을 씁니다.
public static class BattleScene
{
    public struct Result
    {
        public float laneTop, laneBottom; // 유닛이 다닐 땅 (월드 좌표)
    }

    public static bool TryBuild(int stageNumber, Transform world, Camera cam, out Result result)
    {
        result = default;
        string folder = "StageScenes/stage" + stageNumber + "/";
        var bg = Resources.Load<Texture2D>(folder + "background");
        var layout = Resources.Load<TextAsset>(folder + "layout");
        if (bg == null || layout == null) return false;

        Vector2 canvas = new Vector2(1280f, 720f);
        float zoom = 1f;
        Vector2 lanes = new Vector2(470f, 690f);
        var entries = new List<KeyValuePair<string, string[]>>();
        foreach (var raw in layout.text.Split('\n'))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string key = line.Substring(0, eq);
            var parts = line.Substring(eq + 1).Split(',');
            switch (key)
            {
                case "canvas": canvas = new Vector2(F(parts[0]), F(parts[1])); break;
                case "zoom": zoom = F(parts[0]); break;
                case "lanes": lanes = new Vector2(F(parts[0]), F(parts[1])); break;
                default: entries.Add(new KeyValuePair<string, string[]>(key, parts)); break;
            }
        }

        // 원본 그림 1픽셀 = 월드 몇 칸인지. 화면 높이에 맞춘 뒤 zoom만큼 확대, 가로가 모자라면 더 확대
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        float unit = 2f * halfH * zoom / canvas.y;
        if (canvas.x * unit < 2f * halfW) unit = 2f * halfW / canvas.x;
        float camX = cam.transform.position.x, camY = cam.transform.position.y;
        // 그림 아래쪽을 화면 아래쪽에 맞춤 (위쪽 하늘이 조금 잘림)
        System.Func<float, float, Vector2> toWorld = (px, py) =>
            new Vector2(camX + (px - canvas.x / 2f) * unit, camY - halfH + (canvas.y - py) * unit);

        var bgGo = new GameObject("Backdrop");
        bgGo.transform.SetParent(world, false);
        var sr = bgGo.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(bg, new Rect(0, 0, bg.width, bg.height), new Vector2(0.5f, 0f), bg.height / (canvas.y * unit));
        sr.sortingOrder = -3000;
        bgGo.transform.position = toWorld(canvas.x / 2f, canvas.y);

        // 조각 그림 하나를 원본 그림 좌표(px, py)에 놓음. pivot = 그림 안의 기준점 (0~1)
        System.Func<string, float, float, float, float, Vector2, int, SpriteRenderer> place = (name, px, py, w, h, pivot, order) =>
        {
            var tex = Resources.Load<Texture2D>(folder + name.Trim());
            if (tex == null) return null;
            var go = new GameObject("Layer_" + name.Trim());
            go.transform.SetParent(world, false);
            go.transform.position = toWorld(px, py);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, tex.width / (w * unit));
            r.sortingOrder = order;
            return r;
        };

        int index = 0;
        Herd herd = null;
        var beams = new List<KeyValuePair<Vector2, Vector2>>(); // 빛줄기: (빛나는 곳, 땅) 월드 좌표
        foreach (var e in entries)
        {
            var p = e.Value;
            index++;
            switch (e.Key)
            {
                case "sway":
                {
                    if (p.Length < 9) break;
                    float w = F(p[1]), h = F(p[2]);
                    bool bend = p[8].Trim() == "bend";
                    bool back = p.Length > 9 && p[9].Trim() == "back"; // 먼 곳의 잎: 유닛 뒤에
                    // 고정점(풀은 뿌리, 가지는 붙은 곳)을 중심으로 흔들림. 아래쪽 풀은 유닛보다 앞(가까운 풀), 위쪽 가지도 앞에
                    var r = place(p[0], F(p[5]), F(p[6]), w, h, new Vector2(F(p[3]) / w, 1f - F(p[4]) / h), back ? -2800 + index : (bend ? 1500 : 1400) + index);
                    if (r == null) break;
                    var sway = r.gameObject.AddComponent<Sway>();
                    sway.phase = F(p[7]);
                    sway.amplitude = p.Length > 10 ? F(p[10]) : bend ? 4f : 3f;
                    sway.bend = bend;
                    break;
                }
                case "drift":
                {
                    if (p.Length < 10) break;
                    var r = place(p[0], F(p[1]), F(p[2]), F(p[3]), F(p[4]), new Vector2(0.5f, 0.5f), -2900 + index);
                    if (r == null) break;
                    var d = r.gameObject.AddComponent<Drift>();
                    d.start = r.transform.position;
                    d.travel = new Vector2(F(p[5]) * unit, -F(p[6]) * unit);
                    d.period = Mathf.Max(0.5f, F(p[7]));
                    d.phase = F(p[8]);
                    d.opacity = F(p[9]);
                    break;
                }
                case "herd":
                {
                    if (p.Length < 7) break;
                    var go = new GameObject("Herd");
                    go.transform.SetParent(world, false);
                    herd = go.AddComponent<Herd>();
                    int count = Mathf.RoundToInt(F(p[1]));
                    for (int i = 1; i <= count; i++)
                    {
                        var tex = Resources.Load<Texture2D>(folder + p[0].Trim() + i);
                        if (tex != null) herd.frames.Add(tex);
                    }
                    herd.startX = F(p[2]); herd.travel = F(p[3]); herd.period = Mathf.Max(1f, F(p[4]));
                    herd.startPhase = F(p[5]); herd.fps = F(p[6]);
                    herd.toWorld = toWorld; herd.unit = unit;
                    break;
                }
                case "horse":
                {
                    if (herd == null || p.Length < 5 || herd.frames.Count == 0) break;
                    herd.AddRunner(F(p[0]), F(p[1]), F(p[2]), F(p[3]), F(p[4]), -2950 + index);
                    break;
                }
                case "bob":
                {
                    if (p.Length < 8) break;
                    var r = place(p[0], F(p[1]), F(p[2]), F(p[3]), F(p[4]), new Vector2(0.5f, 0.5f), -2920 + index);
                    if (r == null) break;
                    var b = r.gameObject.AddComponent<Bob>();
                    b.basePos = r.transform.position;
                    b.lift = -F(p[5]) * unit; // 원본 그림은 아래로 갈수록 y가 커짐
                    b.roll = F(p[6]);
                    b.period = Mathf.Max(0.5f, F(p[7]));
                    break;
                }
                case "foam":
                {
                    if (p.Length < 9) break;
                    var r = place(p[0], F(p[1]), F(p[2]), F(p[3]), F(p[4]), new Vector2(0.5f, 0.5f), -2930 + index);
                    if (r == null) break;
                    float angle = F(p[5]);
                    r.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                    var f = r.gameObject.AddComponent<Foam>();
                    f.basePos = r.transform.position;
                    // 물거품 줄에 수직인 방향 중 해안(아래) 쪽으로 밀려옴
                    float rad = angle * Mathf.Deg2Rad;
                    f.push = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)) * (18f * unit);
                    f.period = Mathf.Max(0.5f, F(p[6]));
                    f.phase = F(p[7]);
                    f.opacity = F(p[8]);
                    break;
                }
                case "glint":
                {
                    if (p.Length < 4) break;
                    var r = place(p[0], canvas.x / 2f, canvas.y / 2f, canvas.x, canvas.y, new Vector2(0.5f, 0.5f), -2910 + index);
                    if (r == null) break;
                    var g = r.gameObject.AddComponent<Shimmer>();
                    g.period = Mathf.Max(0.5f, F(p[1]));
                    g.min = F(p[2]);
                    g.max = F(p[3]);
                    break;
                }
                case "flag":
                {
                    // 깃발: 그림을 세로 띠로 잘라, 깃대 쪽은 고정하고 바깥쪽으로 갈수록 크게 물결치게
                    if (p.Length < 6) break;
                    var tex = Resources.Load<Texture2D>(folder + p[0].Trim());
                    if (tex == null) break;
                    var go = new GameObject("Flag");
                    go.transform.SetParent(world, false);
                    var flag = go.AddComponent<Flag>();
                    flag.Build(tex, toWorld(F(p[1]), F(p[2])), F(p[3]) * unit, F(p[4]) * unit, F(p[5]), -2880 + index, p.Length > 6 && p[6].Trim() == "1");
                    break;
                }
                case "flame":
                {
                    // 화로 불꽃: 불꽃 그림 여러 장을 부드럽게 섞으며 바꾸고, 불티가 올라감
                    if (p.Length < 7) break;
                    var go = new GameObject("Brazier");
                    go.transform.SetParent(world, false);
                    go.transform.position = toWorld(F(p[2]), F(p[3]));
                    var fire = go.AddComponent<Brazier>();
                    int count = Mathf.RoundToInt(F(p[1]));
                    for (int i = 1; i <= count; i++)
                    {
                        var tex = Resources.Load<Texture2D>(folder + p[0].Trim() + i);
                        if (tex != null) fire.frames.Add(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), tex.width / (F(p[4]) * unit)));
                    }
                    fire.phase = F(p[6]);
                    fire.height = F(p[5]) * unit;
                    if (p.Length > 9) { fire.sparkColor = new Color(F(p[7]), F(p[8]), F(p[9])); fire.glow = true; }
                    fire.Build(-2870 + index);
                    break;
                }
                case "beam":
                {
                    if (p.Length < 10) break;
                    float bx = F(p[1]), by = F(p[2]), w = F(p[3]), h = F(p[4]), sx = F(p[5]), sy = F(p[6]);
                    var r = place(p[0], sx, sy, w, h, new Vector2((sx - bx) / w, 1f - (sy - by) / h), -2890 + index);
                    if (r == null) break;
                    var beam = r.gameObject.AddComponent<Beam>();
                    beam.phase = F(p[9]);
                    beams.Add(new KeyValuePair<Vector2, Vector2>(toWorld(sx, sy), toWorld(F(p[7]), F(p[8]))));
                    break;
                }
                case "motes":
                {
                    if (p.Length < 1 || beams.Count == 0) break;
                    var go = new GameObject("Motes");
                    go.transform.SetParent(world, false);
                    go.AddComponent<Motes>().Build(Mathf.RoundToInt(F(p[0])), beams, unit, -2885 + index);
                    break;
                }
                case "sparkle":
                {
                    if (p.Length < 7) break;
                    float size = F(p[3]);
                    var r = place(p[0], F(p[1]), F(p[2]), size, size, new Vector2(0.5f, 0.5f), -2900 + index);
                    if (r == null) break;
                    var sp = r.gameObject.AddComponent<Sparkle>();
                    sp.period = Mathf.Max(0.5f, F(p[4])); sp.phase = F(p[5]); sp.duration = Mathf.Max(0.05f, F(p[6]));
                    break;
                }
                case "blink":
                {
                    if (p.Length < 8) break;
                    float w = F(p[3]), h = F(p[4]);
                    var r = place(p[0], F(p[1]) + w / 2f, F(p[2]) + h / 2f, w, h, new Vector2(0.5f, 0.5f), -2895 + index);
                    if (r == null) break;
                    var blink = r.gameObject.AddComponent<Blink>();
                    blink.period = Mathf.Max(1f, F(p[5]));
                    blink.duration = Mathf.Max(0.05f, F(p[6]));
                    for (int i = 7; i < p.Length; i++) blink.starts.Add(F(p[i]));
                    break;
                }
                case "hand":
                {
                    if (p.Length < 10) break;
                    var tex = Resources.Load<Texture2D>(folder + p[0].Trim());
                    if (tex == null) break;
                    var go = new GameObject("Hand");
                    go.transform.SetParent(world, false);
                    var hand = go.AddComponent<RisingHand>();
                    hand.ground = toWorld(F(p[1]), F(p[2]));
                    hand.unit = unit;
                    hand.period = Mathf.Max(1f, F(p[5]));
                    hand.times = new[] { F(p[6]), F(p[7]), F(p[8]), F(p[9]) };
                    hand.Build(tex, F(p[3]) * unit, -2840 + index);
                    break;
                }
                case "banner":
                {
                    if (p.Length < 6) break;
                    var tex = Resources.Load<Texture2D>(folder + p[0].Trim());
                    if (tex == null) break;
                    var go = new GameObject("Banner");
                    go.transform.SetParent(world, false);
                    go.AddComponent<HangingBanner>().Build(tex, toWorld(F(p[1]), F(p[2])), F(p[3]) * unit, F(p[4]) * unit, unit, F(p[5]), -2880 + index);
                    break;
                }
                case "mist":
                {
                    if (p.Length < 6) break;
                    var tex = Resources.Load<Texture2D>(folder + p[0].Trim());
                    if (tex == null) break;
                    var go = new GameObject("Mist");
                    go.transform.SetParent(world, false);
                    go.AddComponent<Mist>().Build(tex, toWorld(F(p[1]), F(p[2])), F(p[3]) * unit, F(p[4]) * unit, F(p[5]), -2850 + index);
                    break;
                }
                case "dust":
                {
                    // 길 위로 바람에 날리는 먼지
                    if (p.Length < 1) break;
                    var go = new GameObject("Dust");
                    go.transform.SetParent(world, false);
                    var dust = go.AddComponent<Dust>();
                    dust.Build(Mathf.RoundToInt(F(p[0])), toWorld(0f, lanes.x), toWorld(canvas.x, canvas.y), -2860 + index);
                    break;
                }
            }
        }

        result.laneTop = toWorld(0f, lanes.x).y;
        result.laneBottom = toWorld(0f, lanes.y).y;
        return true;
    }

    static float F(string s)
    {
        float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
        return v;
    }
}

// 바람에 흔들리는 풀 · 가지. 고정점을 중심으로 천천히 기울었다 돌아옴 (전투 배속과 상관없이 같은 속도)
public class Sway : MonoBehaviour
{
    public float phase;
    public float amplitude = 3f; // 최대 기울기 (도)
    public bool bend;            // 풀: 기울면서 살짝 납작해짐

    void Update()
    {
        float t = Time.unscaledTime * Mathf.PI * 2f / 4f + phase; // 4초에 한 번 크게 흔들림
        float wind = Mathf.Sin(t) * 0.75f + Mathf.Sin(t * 2.3f + phase * 1.7f) * 0.25f;
        transform.localRotation = Quaternion.Euler(0f, 0f, -wind * amplitude);
        if (bend) transform.localScale = new Vector3(1f, 1f - Mathf.Abs(wind) * 0.03f, 1f);
    }
}

// 흘러가는 구름: 시작 위치에서 travel만큼 움직이며 서서히 생겼다가 사라짐
public class Drift : MonoBehaviour
{
    public Vector2 start, travel;
    public float period = 12f, phase, opacity = 1f;
    SpriteRenderer r;

    void Awake() { r = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        float f = Mathf.Repeat(Time.unscaledTime / period + phase, 1f);
        transform.position = start + travel * f;
        if (r != null) r.color = new Color(1f, 1f, 1f, opacity * Mathf.Pow(Mathf.Sin(f * Mathf.PI), 0.6f));
    }
}

// 물 위에서 천천히 오르내리며 살짝 기우는 배
public class Bob : MonoBehaviour
{
    public Vector2 basePos;
    public float lift, roll, period = 4f;

    void Update()
    {
        float t = Time.unscaledTime * Mathf.PI * 2f / period;
        transform.position = basePos + new Vector2(0f, Mathf.Sin(t) * lift);
        transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t + 1.1f) * roll);
    }
}

// 해안으로 밀려왔다 사라지는 물거품
public class Foam : MonoBehaviour
{
    public Vector2 basePos, push;
    public float period = 12f, phase, opacity = 0.4f;
    SpriteRenderer r;

    void Awake() { r = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        float f = Mathf.Repeat(Time.unscaledTime / period + phase, 1f);
        transform.position = basePos + push * (f - 0.5f);
        if (r != null) r.color = new Color(1f, 1f, 1f, opacity * Mathf.Sin(f * Mathf.PI));
    }
}

// 밝아졌다 어두워지며 반짝이는 물빛
public class Shimmer : MonoBehaviour
{
    public float period = 4f, min = 0.3f, max = 1f;
    SpriteRenderer r;

    void Awake() { r = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        float t = Time.unscaledTime * Mathf.PI * 2f / period;
        float k = 0.5f + 0.35f * Mathf.Sin(t) + 0.15f * Mathf.Sin(t * 2.7f + 0.8f);
        if (r != null) r.color = new Color(1f, 1f, 1f, Mathf.Lerp(min, max, k));
    }
}

// 멀리서 달리는 무리 (말). 무리 전체가 화면 밖으로 나가면 반대편에서 다시 나타남
public class Herd : MonoBehaviour
{
    public readonly List<Texture2D> frames = new List<Texture2D>();
    public float startX, travel, period = 16f, startPhase, fps = 12f, unit;
    public System.Func<float, float, Vector2> toWorld;

    class Runner { public SpriteRenderer r; public Sprite[] sprites; public float offset, groundY, phase; }
    readonly List<Runner> runners = new List<Runner>();

    public void AddRunner(float offset, float groundY, float w, float h, float phase, int order)
    {
        var go = new GameObject("Runner");
        go.transform.SetParent(transform, false);
        var run = new Runner { r = go.AddComponent<SpriteRenderer>(), offset = offset, groundY = groundY, phase = phase, sprites = new Sprite[frames.Count] };
        for (int i = 0; i < frames.Count; i++)
        {
            var tex = frames[i];
            run.sprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.05f), tex.width / (w * unit));
        }
        run.r.sortingOrder = order;
        runners.Add(run);
        Update();
    }

    void Update()
    {
        if (toWorld == null) return;
        float t = Time.unscaledTime;
        float f = Mathf.Repeat(t / period + startPhase, 1f);
        foreach (var run in runners)
        {
            run.r.transform.position = toWorld(startX + f * travel - run.offset, run.groundY);
            run.r.sprite = run.sprites[Mathf.FloorToInt(t * fps + run.phase) % run.sprites.Length];
        }
    }
}

// 펄럭이는 깃발: 세로 띠마다 위아래로 물결 (깃대 쪽 0 → 바깥쪽으로 갈수록 크게), 접힌 면은 살짝 어둡게
public class Flag : MonoBehaviour
{
    const int Strips = 14;
    readonly SpriteRenderer[] strips = new SpriteRenderer[Strips];
    readonly Vector3[] basePos = new Vector3[Strips];
    float height, phase, stripW;
    bool flip; // 깃대가 오른쪽 (오른쪽이 고정, 왼쪽으로 갈수록 크게 펄럭임)

    public void Build(Texture2D tex, Vector2 topLeft, float width, float height, float phase, int order, bool flip = false)
    {
        this.height = height;
        this.phase = phase;
        this.flip = flip;
        stripW = width / Strips;
        float texStrip = tex.width / (float)Strips;
        float ppu = tex.height / height;
        for (int i = 0; i < Strips; i++)
        {
            var go = new GameObject("Strip");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            // 띠 사이 틈이 보이지 않도록 조금씩 겹치게 자름
            float x0 = Mathf.Max(0f, i * texStrip - 1f), w = Mathf.Min(tex.width - x0, texStrip + 2f);
            r.sprite = Sprite.Create(tex, new Rect(x0, 0, w, tex.height), new Vector2(0f, 1f), ppu);
            r.sortingOrder = order;
            basePos[i] = new Vector3(topLeft.x + i * stripW, topLeft.y, 0f);
            go.transform.position = basePos[i];
            strips[i] = r;
        }
    }

    void Update()
    {
        float t = Time.unscaledTime * Mathf.PI * 2f / 3f + phase * Mathf.PI * 2f;
        for (int i = 0; i < Strips; i++)
        {
            float u = (i + 0.5f) / Strips;              // 0 깃대 쪽 ~ 1 바깥쪽
            if (flip) u = 1f - u;
            float wave = Mathf.Sin(t - u * 5.5f);
            strips[i].transform.position = basePos[i] + new Vector3(0f, wave * height * 0.09f * u, 0f);
            float shade = 0.86f + 0.14f * Mathf.Cos(t - u * 5.5f) * u + 0.14f * (1f - u);
            strips[i].color = new Color(shade, shade, shade, 1f);
        }
    }
}

// 화로 불꽃: 불꽃 그림을 번갈아 부드럽게 섞고, 위로 불티가 날아오름
public class Brazier : MonoBehaviour
{
    public readonly List<Sprite> frames = new List<Sprite>();
    public float phase, height = 1f;
    public Color sparkColor = new Color(1f, 0.75f, 0.35f);
    public bool glow;            // 불꽃 둘레에 은은한 불빛
    SpriteRenderer a, b, halo;
    readonly List<SpriteRenderer> sparks = new List<SpriteRenderer>();
    const float PoseSeconds = 0.14f;

    public void Build(int order)
    {
        if (frames.Count == 0) return;
        a = MakeLayer("FlameA", order);
        b = MakeLayer("FlameB", order + 1);
        if (glow)
        {
            halo = MakeLayer("Glow", order - 1);
            var g = SpriteFactory.GlowTexture();
            halo.sprite = Sprite.Create(g, new Rect(0, 0, g.width, g.height), new Vector2(0.5f, 0.5f), g.width / (height * 2.4f));
            halo.transform.localPosition = new Vector3(0f, height * 0.45f, 0f);
        }
        for (int i = 0; i < 5; i++)
        {
            var go = new GameObject("Spark");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = SpriteFactory.Circle();
            r.sortingOrder = order + 2;
            go.transform.localScale = Vector3.one * height * 0.06f;
            sparks.Add(r);
        }
    }

    SpriteRenderer MakeLayer(string n, int order)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sortingOrder = order;
        return r;
    }

    void Update()
    {
        if (a == null) return;
        float t = Time.unscaledTime / PoseSeconds + phase * frames.Count;
        int i = Mathf.FloorToInt(t) % frames.Count;
        float mix = Mathf.SmoothStep(0f, 1f, t - Mathf.Floor(t));
        a.sprite = frames[i];
        b.sprite = frames[(i + 1) % frames.Count];
        a.color = new Color(1f, 1f, 1f, 1f - mix);
        b.color = new Color(1f, 1f, 1f, mix);
        float sway = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5.3f + phase * 6f);
        a.transform.localScale = b.transform.localScale = new Vector3(1f, sway, 1f);
        for (int k = 0; k < sparks.Count; k++)
        {
            float life = Mathf.Repeat(Time.unscaledTime * 0.6f + k * 0.21f + phase, 1f);
            sparks[k].transform.localPosition = new Vector3(Mathf.Sin(k * 3.1f + life * 5f) * height * 0.18f, height * (0.7f + life * 1.3f), 0f);
            sparks[k].color = new Color(sparkColor.r, sparkColor.g, sparkColor.b, 0.9f * (1f - life));
        }
        if (halo != null)
        {
            float strength = 0.8f + 0.12f * Mathf.Sin(Time.unscaledTime * 5.8f + phase) + 0.08f * Mathf.Sin(Time.unscaledTime * 8.9f + phase);
            halo.color = new Color(sparkColor.r * 0.6f, sparkColor.g * 0.8f, sparkColor.b, 0.32f * strength);
        }
    }
}

// 길 위로 바람에 날려 가는 먼지 (왼쪽 → 오른쪽, 생겼다 사라짐)
public class Dust : MonoBehaviour
{
    readonly List<SpriteRenderer> motes = new List<SpriteRenderer>();
    readonly List<Vector2> seeds = new List<Vector2>();
    Vector2 min, max;

    public void Build(int count, Vector2 topLeft, Vector2 bottomRight, int order)
    {
        min = new Vector2(topLeft.x, bottomRight.y);
        max = new Vector2(bottomRight.x, topLeft.y);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Mote");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = SpriteFactory.Circle();
            r.sortingOrder = order;
            float size = 0.12f + 0.1f * ((i * 37) % 10) / 10f;
            go.transform.localScale = new Vector3(size * 2.2f, size, 1f);
            motes.Add(r);
            seeds.Add(new Vector2((i * 0.618f) % 1f, (i * 0.381f + 0.2f) % 1f));
        }
    }

    void Update()
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < motes.Count; i++)
        {
            float life = Mathf.Repeat(t / 7f + seeds[i].x, 1f);
            float x = Mathf.Lerp(min.x, max.x, Mathf.Repeat(seeds[i].x + life * 0.35f, 1f));
            float y = Mathf.Lerp(min.y, max.y, seeds[i].y) + Mathf.Sin(t * 0.9f + i) * 0.15f + life * 0.4f;
            motes[i].transform.position = new Vector3(x, y, 0f);
            motes[i].color = new Color(0.92f, 0.86f, 0.72f, 0.28f * Mathf.Sin(life * Mathf.PI));
        }
    }
}


// 쏟아지는 빛줄기: 빛나는 곳을 중심으로 아주 살짝 기울고, 밝기가 숨 쉬듯 바뀜
public class Beam : MonoBehaviour
{
    public float phase;
    SpriteRenderer r;

    void Awake() { r = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        float t = Time.unscaledTime * Mathf.PI * 2f / 12f + phase;
        transform.localRotation = Quaternion.Euler(0f, 0f, 1.2f * Mathf.Sin(t) + 0.4f * Mathf.Sin(t * 2f + 0.7f));
        if (r != null) r.color = new Color(1f, 1f, 1f, 0.82f + 0.18f * Mathf.Sin(t * 2f + phase * 0.6f));
    }
}

// 빛줄기 안에서 천천히 떠오르는 먼지 (빛 안에서만 보이고, 위로 갈수록 흐려짐)
public class Motes : MonoBehaviour
{
    class Mote { public SpriteRenderer r; public int beam; public float seed, side, speed; }
    readonly List<Mote> motes = new List<Mote>();
    List<KeyValuePair<Vector2, Vector2>> beams;
    float unit;

    public void Build(int count, List<KeyValuePair<Vector2, Vector2>> beams, float unit, int order)
    {
        this.beams = beams;
        this.unit = unit;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Mote");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = SpriteFactory.Circle();
            r.sortingOrder = order;
            go.transform.localScale = Vector3.one * unit * (2.2f + 1.6f * ((i * 37) % 10) / 10f);
            motes.Add(new Mote { r = r, beam = i % beams.Count, seed = (i * 0.618f) % 1f, side = ((i * 0.381f) % 1f) * 2f - 1f, speed = 0.8f + 0.4f * ((i * 53) % 10) / 10f });
        }
    }

    void Update()
    {
        float t = Time.unscaledTime;
        foreach (var m in motes)
        {
            var b = beams[m.beam];
            float life = Mathf.Repeat(t / 12f * m.speed + m.seed, 1f);
            float along = 0.05f + life * 0.7f;                         // 땅 쪽에서 빛나는 곳 쪽으로 올라감
            Vector2 pos = Vector2.Lerp(b.Value, b.Key, along);
            float width = (1f - along) * 45f * unit;                    // 빛줄기는 땅 쪽이 넓음
            pos.x += m.side * width + Mathf.Sin(t * 0.8f + m.seed * 20f) * 6f * unit;
            m.r.transform.position = pos;
            m.r.color = new Color(1f, 0.95f, 0.78f, 0.75f * Mathf.Pow(Mathf.Sin(life * Mathf.PI), 2f));
        }
    }
}

// 금화 위의 반짝임: 주기마다 잠깐 빛났다 사라짐
public class Sparkle : MonoBehaviour
{
    public float period = 6f, phase, duration = 0.7f;
    SpriteRenderer r;

    void Awake() { r = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        float life = Mathf.Repeat(Time.unscaledTime + phase * period, period) / duration;
        float k = life < 1f ? Mathf.Pow(Mathf.Sin(Mathf.PI * life), 2f) : 0f;
        if (r != null) r.color = new Color(1f, 1f, 1f, k);
    }
}

// 어둠 속의 눈: 정해진 때에 눈꺼풀이 감겼다 뜸 (위아래로 납작해짐)
public class Blink : MonoBehaviour
{
    public float period = 12f, duration = 0.32f;
    public readonly List<float> starts = new List<float>();
    SpriteRenderer r;

    void Awake() { r = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        float t = Mathf.Repeat(Time.unscaledTime, period), open = 1f, half = duration * 0.5f;
        foreach (var s in starts)
        {
            float dt = t - s;
            if (dt >= 0f && dt < duration) open = Mathf.Min(open, Mathf.Abs(dt - half) / half);
        }
        transform.localScale = new Vector3(1f, Mathf.Max(0.05f, open), 1f);
        if (r != null) r.enabled = open > 0.09f;
    }
}

// 땅에서 솟아올랐다 다시 들어가는 해골 손. 땅 아래 부분은 보이지 않게, 드러난 만큼만 그림
public class RisingHand : MonoBehaviour
{
    const int Steps = 32;
    public Vector2 ground;
    public float unit, period = 12f;
    public float[] times = { 3f, 3.5f, 6.8f, 8.7f }; // 솟기 시작, 다 솟음, 들어가기 시작, 사라짐
    readonly Sprite[] sprites = new Sprite[Steps + 1];
    readonly List<SpriteRenderer> puffs = new List<SpriteRenderer>();
    SpriteRenderer r;

    public void Build(Texture2D tex, float width, int order)
    {
        float ppu = tex.width / width;
        for (int i = 1; i <= Steps; i++)
        {
            float h = tex.height * i / (float)Steps; // 손끝부터 i/Steps 만큼 드러남
            sprites[i] = Sprite.Create(tex, new Rect(0, tex.height - h, tex.width, h), new Vector2(0.5f, 0f), ppu);
        }
        var go = new GameObject("HandSprite");
        go.transform.SetParent(transform, false);
        r = go.AddComponent<SpriteRenderer>();
        r.sortingOrder = order;
        for (int i = 0; i < 12; i++)
        {
            var pg = new GameObject("Soil");
            pg.transform.SetParent(transform, false);
            var pr = pg.AddComponent<SpriteRenderer>();
            pr.sprite = SpriteFactory.Circle();
            pr.sortingOrder = order + 1;
            pg.transform.localScale = Vector3.one * unit * (2.4f + (i % 3));
            puffs.Add(pr);
        }
    }

    float Rise(float t)
    {
        if (t < times[0] || t >= times[3]) return 0f;
        if (t < times[1]) { float q = 1f - (t - times[0]) / (times[1] - times[0]); return 1f - q * q * q; }
        if (t < times[2]) return 1f;
        return 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (t - times[2]) / (times[3] - times[2]));
    }

    void Update()
    {
        if (r == null) return;
        float t = Mathf.Repeat(Time.unscaledTime, period);
        float rise = Rise(t);
        int step = Mathf.RoundToInt(rise * Steps);
        r.enabled = step > 0;
        if (step > 0)
        {
            r.sprite = sprites[step];
            float sway = 1.8f * Mathf.Sin(4f * Mathf.PI * 2f * t / period) * rise;
            r.transform.position = ground + new Vector2(sway * unit, 0f);
        }
        // 솟아오르기 직전 · 직후에 흙이 튐
        float start = times[0] - 0.35f, end = times[0] + 1.1f;
        for (int k = 0; k < puffs.Count; k++)
        {
            bool on = t > start && t < end;
            puffs[k].enabled = on;
            if (!on) continue;
            float q = (t - start) / (end - start);
            float ang = k / (float)(puffs.Count - 1) * Mathf.PI;
            float vx = Mathf.Cos(ang) * (12f + 14f * (k % 3)), vy = Mathf.Sin(ang) * 25f;
            puffs[k].transform.position = ground + new Vector2(vx * q, 5f + vy * Mathf.Sin(Mathf.PI * q)) * unit;
            puffs[k].color = new Color(0.49f, 0.41f, 0.31f, 0.75f * (1f - q));
        }
    }
}

// 위가 고정된 채 매달린 깃발: 가로 띠로 잘라, 아래로 갈수록 옆으로 크게 흔들림
public class HangingBanner : MonoBehaviour
{
    const int Strips = 16;
    readonly SpriteRenderer[] strips = new SpriteRenderer[Strips];
    readonly Vector3[] basePos = new Vector3[Strips];
    float unit, phase;

    public void Build(Texture2D tex, Vector2 topCenter, float width, float height, float unit, float phase, int order)
    {
        this.unit = unit;
        this.phase = phase;
        float texStrip = tex.height / (float)Strips;
        float ppu = tex.width / width;
        for (int i = 0; i < Strips; i++)
        {
            var go = new GameObject("Strip");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            // 위에서 i번째 띠 (텍스처는 아래가 0이라 뒤집어 셈). 틈이 보이지 않게 살짝 겹침
            float y1 = tex.height - i * texStrip;
            float y0 = Mathf.Max(0f, y1 - texStrip - 1.5f);
            r.sprite = Sprite.Create(tex, new Rect(0, y0, tex.width, y1 - y0), new Vector2(0.5f, 1f), ppu);
            r.sortingOrder = order;
            basePos[i] = new Vector3(topCenter.x, topCenter.y - i * height / Strips, 0f);
            go.transform.position = basePos[i];
            strips[i] = r;
        }
    }

    void Update()
    {
        float p = Time.unscaledTime * Mathf.PI * 2f / 12f;
        for (int i = 0; i < Strips; i++)
        {
            float v = (i + 0.5f) / Strips; // 0 위(고정) ~ 1 아래
            float dx = Mathf.Pow(v, 1.5f) * (6f * Mathf.Sin(2f * p - v * 4f + phase) + 1.3f * Mathf.Sin(3f * p - v * 6f + phase));
            strips[i].transform.position = basePos[i] + new Vector3(dx * unit, 0f, 0f);
            float shade = 0.9f + 0.1f * Mathf.Cos(2f * p - v * 4f + phase) * v;
            strips[i].color = new Color(shade, shade, shade, 1f);
        }
    }
}

// 바닥 안개: 같은 그림 두 장을 이어 붙여 옆으로 천천히 흘려 보냄 (그림 끝과 처음이 이어지는 그림)
public class Mist : MonoBehaviour
{
    SpriteRenderer a, b;
    Vector2 topLeft;
    float width, opacity;

    public void Build(Texture2D tex, Vector2 topLeft, float width, float height, float opacity, int order)
    {
        this.topLeft = topLeft;
        this.width = width;
        this.opacity = opacity;
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0f, 1f), tex.width / width);
        a = Make(sprite, order);
        b = Make(sprite, order);
    }

    SpriteRenderer Make(Sprite sprite, int order)
    {
        var go = new GameObject("MistTile");
        go.transform.SetParent(transform, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order;
        return r;
    }

    void Update()
    {
        if (a == null) return;
        float t = Time.unscaledTime;
        float shift = Mathf.Repeat(t / 24f, 1f) * width; // 24초에 한 화면 폭만큼 흘러감
        a.transform.position = topLeft + new Vector2(-shift, 0f);
        b.transform.position = topLeft + new Vector2(width - shift, 0f);
        float k = opacity * (0.85f + 0.15f * Mathf.Sin(t * Mathf.PI * 2f / 12f));
        a.color = b.color = new Color(1f, 1f, 1f, k);
    }
}
