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
                    // 고정점(풀은 뿌리, 가지는 붙은 곳)을 중심으로 흔들림. 아래쪽 풀은 유닛보다 앞(가까운 풀), 위쪽 가지도 앞에
                    var r = place(p[0], F(p[5]), F(p[6]), w, h, new Vector2(F(p[3]) / w, 1f - F(p[4]) / h), (bend ? 1500 : 1400) + index);
                    if (r == null) break;
                    var sway = r.gameObject.AddComponent<Sway>();
                    sway.phase = F(p[7]);
                    sway.amplitude = bend ? 4f : 3f;
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
