using System.Collections.Generic;
using UnityEngine;

// 타이틀 화면 배경 (석양의 성). Resources/Title 의 레이어를 겹쳐서 움직이게 그립니다.
//   background.jpg  제목 · 메뉴 버튼 5개 · 카드 3장까지 그려진 고정 배경
//   branch_*.png    양쪽 나무의 잎 (붙은 곳을 중심으로 바람에 흔들림)
//   cloud.png       천천히 흘러가는 구름
//   river.png       강물 반사광 (일렁임) + 반짝이는 점(glint)
//   button_*.png / card_*.png  마우스를 올렸을 때 밝게 빛나는 버튼 · 카드 그림
//   layout.txt      원본 그림(1280x720) 기준 위치
// 버튼과 카드는 그림에 그려져 있으므로, 그 위치에 보이지 않는 클릭 영역을 둡니다 (GameManager.DrawTitle).
public static class TitleScene
{
    class Branch { public Texture2D tex; public Rect rect; public bool flip; public float phase; }
    class Cloud { public Rect rect; public float phase; }
    class Glint { public Vector2 pos; public float phase; }
    class Firework { public float start; public Vector2 launch, burst; public Color color; }
    class Card { public Texture2D tex; public Rect rect; public Vector2[] quad; }

    static bool loaded;
    static Texture2D background, cloudTex, riverTex;
    static readonly List<Branch> branches = new List<Branch>();
    static readonly List<Cloud> clouds = new List<Cloud>();
    static readonly List<Glint> glints = new List<Glint>();
    static readonly List<Firework> fireworks = new List<Firework>();
    static readonly List<Rect> buttons = new List<Rect>();
    static readonly List<Texture2D> buttonTex = new List<Texture2D>();
    static readonly List<Card> cards = new List<Card>();
    static Rect river, sun;
    static Vector2 canvas = new Vector2(1280f, 720f);

    static Rect bgRect;
    static float scale = 1f;
    const float Loop = 12f; // 12초 반복

    static float Now => Time.unscaledTime;

    public static bool Available { get { Load(); return background != null; } }
    public static int ButtonCount { get { Load(); return buttons.Count; } }
    public static int CardCount { get { Load(); return cards.Count; } }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        background = Resources.Load<Texture2D>("Title/background");
        cloudTex = Resources.Load<Texture2D>("Title/cloud");
        riverTex = Resources.Load<Texture2D>("Title/river");
        var layout = Resources.Load<TextAsset>("Title/layout");
        if (layout == null) { background = null; return; }
        foreach (var raw in layout.text.Split('\n'))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string key = line.Substring(0, eq);
            var p = line.Substring(eq + 1).Split(',');
            switch (key)
            {
                case "canvas": canvas = new Vector2(F(p[0]), F(p[1])); break;
                case "branch":
                    branches.Add(new Branch { tex = Resources.Load<Texture2D>("Title/" + p[0]), rect = new Rect(F(p[1]), F(p[2]), F(p[3]), F(p[4])), flip = p[5].Trim() == "1", phase = F(p[6]) });
                    break;
                case "cloud": clouds.Add(new Cloud { rect = new Rect(F(p[0]), F(p[1]), F(p[2]), F(p[3])), phase = F(p[4]) }); break;
                case "river": river = new Rect(F(p[0]), F(p[1]), F(p[2]), F(p[3])); break;
                case "glint": glints.Add(new Glint { pos = new Vector2(F(p[0]), F(p[1])), phase = F(p[2]) }); break;
                case "sun": sun = new Rect(F(p[0]) - F(p[2]) / 2f, F(p[1]) - F(p[3]) / 2f, F(p[2]), F(p[3])); break;
                case "firework":
                    fireworks.Add(new Firework
                    {
                        start = F(p[0]), launch = new Vector2(F(p[1]), F(p[2])), burst = new Vector2(F(p[3]), F(p[4])),
                        color = new Color(F(p[5]) / 255f, F(p[6]) / 255f, F(p[7]) / 255f),
                    });
                    break;
                case "button":
                    buttons.Add(new Rect(F(p[0]), F(p[1]), F(p[2]), F(p[3])));
                    buttonTex.Add(Resources.Load<Texture2D>("Title/button_" + (buttons.Count - 1)));
                    break;
                case "card":
                    var quad = new Vector2[4];
                    for (int i = 0; i < 4; i++) quad[i] = new Vector2(F(p[4 + i * 2]), F(p[5 + i * 2]));
                    cards.Add(new Card { tex = Resources.Load<Texture2D>("Title/card_" + cards.Count), rect = new Rect(F(p[0]), F(p[1]), F(p[2]), F(p[3])), quad = quad });
                    break;
            }
        }
    }

    static float F(string s)
    {
        float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
        return v;
    }

    // 원본 그림 좌표 → 화면 좌표
    static Vector2 Map(Vector2 p) => new Vector2(bgRect.x + p.x * scale, bgRect.y + p.y * scale);
    static Rect Map(Rect r) => new Rect(bgRect.x + r.x * scale, bgRect.y + r.y * scale, r.width * scale, r.height * scale);
    static Vector2 ToCanvas(Vector2 screen) => new Vector2((screen.x - bgRect.x) / scale, (screen.y - bgRect.y) / scale);

    static void DrawTinted(Rect r, Texture2D tex, Color tint)
    {
        if (tex == null) return;
        var old = GUI.color;
        GUI.color = tint;
        GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
        GUI.color = old;
    }

    // ---------------- 배경 ----------------

    public static void DrawBackground()
    {
        Load();
        float w = UI.Width;
        scale = Mathf.Max(w / canvas.x, UI.Height / canvas.y);
        var size = canvas * scale;
        bgRect = new Rect((w - size.x) / 2f, (UI.Height - size.y) / 2f, size.x, size.y);
        GUI.DrawTexture(bgRect, background, ScaleMode.StretchToFill);

        float t = Mathf.Repeat(Now, Loop);
        float p = Mathf.PI * 2f * t / Loop;

        // 천천히 흘러가는 구름 (생겼다가 사라짐)
        foreach (var c in clouds)
        {
            float q = Mathf.Repeat(t / Loop + c.phase, 1f);
            float fade = Mathf.Pow(Mathf.Sin(Mathf.PI * q), 0.8f);
            var r = new Rect(c.rect.x + 62f * q, c.rect.y, c.rect.width, c.rect.height);
            DrawTinted(Map(r), cloudTex, new Color(1f, 1f, 1f, 0.25f * fade));
        }

        // 해 주변 따뜻한 빛이 숨 쉬듯
        DrawTinted(Map(sun), SpriteFactory.GlowTexture(), new Color(1f, 0.79f, 0.44f, 0.16f + 0.07f * Mathf.Sin(p + 0.2f)));

        // 강물 반사광 일렁임 + 반짝임
        DrawTinted(Map(river), riverTex, new Color(1f, 1f, 1f, 0.12f + 0.08f * Mathf.Sin(p * 2f)));
        foreach (var g in glints)
        {
            float a = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(4f * p + g.phase * Mathf.PI * 2f)), 7f);
            if (a < 0.02f) continue;
            UI.Glow(Map(g.pos), 3f * scale, new Color(1f, 0.95f, 0.74f, 0.7f * a));
        }

        // 성에서 올라가는 작은 폭죽
        foreach (var f in fireworks) DrawFirework(f, t);

        // 양쪽 나무의 잎: 붙은 곳(바깥쪽 가장자리)을 중심으로 흔들림
        foreach (var b in branches)
        {
            if (b.tex == null) continue;
            float bend = 4.8f * Mathf.Sin(2f * p + b.phase) + 0.9f * Mathf.Sin(3f * p + 0.7f * b.phase); // 끝부분이 움직이는 픽셀
            float angle = Mathf.Atan2(bend, b.rect.width) * Mathf.Rad2Deg * (b.flip ? -1f : 1f);
            var r = Map(b.rect);
            var pivot = new Vector2(b.flip ? r.xMax : r.x, r.y + r.height * 0.35f);
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, pivot * UI.Scale);
            GUI.DrawTexture(r, b.tex, ScaleMode.StretchToFill);
            GUI.matrix = old;
        }
    }

    static void DrawFirework(Firework f, float t)
    {
        float q = t - f.start;
        if (q < 0f) return;
        if (q < 0.85f)
        {
            // 올라가는 불꽃 꼬리
            float k = q / 0.85f;
            var pos = Vector2.Lerp(f.launch, f.burst, k);
            for (int i = 0; i < 4; i++)
                UI.Glow(Map(pos + new Vector2(0f, i * 3f)), (2.2f - i * 0.4f) * scale, new Color(f.color.r, f.color.g, f.color.b, 0.8f - i * 0.18f));
        }
        else if (q < 2.25f)
        {
            // 터지며 퍼지는 불티
            float k = (q - 0.85f) / 1.4f;
            float radius = 32f * (1f - Mathf.Exp(-k * 4f));
            float alpha = 0.85f * Mathf.Pow(1f - k, 1.35f);
            for (int j = 0; j < 22; j++)
            {
                float angle = Mathf.PI * 2f * j / 22f;
                float s = Mathf.Sin(j * 4f);
                float rr = radius * (0.8f + 0.2f * s * s);
                var pos = f.burst + new Vector2(Mathf.Cos(angle) * rr, Mathf.Sin(angle) * rr + k * k * 13f);
                UI.Glow(Map(pos), 2.6f * scale, new Color(f.color.r, f.color.g, f.color.b, alpha));
            }
            if (k < 0.3f) UI.Glow(Map(f.burst), 18f * scale * (1f - k), new Color(f.color.r, f.color.g, f.color.b, 0.35f * (1f - k / 0.3f)));
        }
    }

    // ---------------- 버튼 · 카드 ----------------

    public static Rect ButtonRect(int i) => Map(buttons[i]);

    // 마우스를 올린 버튼을 밝게 (hover 0~1)
    public static void DrawButtonHover(int i, float hover)
    {
        if (hover <= 0f) return;
        var r = Map(buttons[i]);
        UI.Glow(r.center, r.width * 0.6f, new Color(1f, 0.8f, 0.45f, 0.22f * hover));
        DrawTinted(r, buttonTex[i], new Color(1f, 1f, 1f, 0.75f * hover));
    }

    // 쓸 수 없는 버튼은 어둡게
    public static void DrawButtonDisabled(int i)
    {
        var r = Map(buttons[i]);
        UI.Round(new Rect(r.x + 8f * scale, r.y + 8f * scale, r.width - 16f * scale, r.height - 16f * scale), new Color(0f, 0f, 0f, 0.5f));
    }

    public static bool CardContains(int i, Vector2 screen)
    {
        var p = ToCanvas(screen);
        var q = cards[i].quad;
        bool inside = false;
        for (int a = 0, b = q.Length - 1; a < q.Length; b = a++)
        {
            if ((q[a].y > p.y) != (q[b].y > p.y) && p.x < (q[b].x - q[a].x) * (p.y - q[a].y) / (q[b].y - q[a].y) + q[a].x)
                inside = !inside;
        }
        return inside;
    }

    // 마우스를 올린 카드: 뒤에서 빛이 퍼지고 카드가 밝아지며, 위에 문구가 떠오름
    public static void DrawCardHover(int i, float hover, string caption)
    {
        if (hover <= 0f) return;
        var c = cards[i];
        var r = Map(c.rect);
        float pulse = 0.8f + 0.2f * Mathf.Sin(Now * 5f);
        UI.Glow(r.center, Mathf.Max(r.width, r.height) * 0.75f, new Color(1f, 0.82f, 0.45f, 0.35f * hover * pulse));
        DrawTinted(r, c.tex, new Color(1f, 1f, 1f, 0.8f * hover * pulse));
        // 문구 (카드 위쪽에서 살짝 떠오름)
        var label = new Rect(r.center.x - 90f, r.y - 46f - 10f * hover, 180f, 40f);
        UI.Glow(label.center, 70f, new Color(0f, 0f, 0f, 0.45f * hover));
        UI.Text(label, caption, 28, UI.WithAlpha(UI.Gold, hover), TextAnchor.MiddleCenter, true);
    }
}
