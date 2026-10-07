using System.Collections.Generic;
using UnityEngine;

// 야영지 배경 (밤 숲속 공터). Resources/Campsite 의 레이어를 겹쳐서 움직이게 그립니다.
//   background.jpg   움직이지 않는 배경 (모닥불 돌과 장작까지)
//   fire_glow.png    모닥불 주변 불빛 (깜빡임)
//   flame_1~4.png    불꽃 모양 4가지 (번갈아 부드럽게 바뀜)
//   stars_overlay.png 별 (별마다 따로 반짝임)
//   layout.txt       원본 그림(1280x720) 기준 위치: 불꽃 바닥 위치와 크기, 별 위치
// 레이어가 없으면 예전 배경(Backgrounds/camp)과 코드로 그린 모닥불을 씁니다.
public static class CampScene
{
    static bool loaded;
    static Texture2D background, glow, stars;
    static readonly List<Texture2D> flames = new List<Texture2D>();
    static Vector2 canvas = new Vector2(1280f, 720f);
    static Vector2 flameBase = new Vector2(644f, 522f);
    static Vector2 flameSize = new Vector2(104f, 108f);
    static readonly List<Vector2> starPoints = new List<Vector2>();

    const float FlamePoseSeconds = 0.16f; // 불꽃 모양 하나가 유지되는 시간

    static Rect bgRect;  // 이번 프레임에 배경이 그려진 화면 위치
    static float bgScale;

    static float Now => Time.unscaledTime;

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        background = Resources.Load<Texture2D>("Campsite/background");
        glow = Resources.Load<Texture2D>("Campsite/fire_glow");
        stars = Resources.Load<Texture2D>("Campsite/stars_overlay");
        for (int i = 1; i <= 4; i++)
        {
            var f = Resources.Load<Texture2D>("Campsite/flame_" + i);
            if (f != null) flames.Add(f);
        }
        var layout = Resources.Load<TextAsset>("Campsite/layout");
        if (layout == null) return;
        foreach (var raw in layout.text.Split('\n'))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            var v = ParseNumbers(line.Substring(eq + 1));
            switch (line.Substring(0, eq))
            {
                case "canvas": if (v.Length >= 2) canvas = new Vector2(v[0], v[1]); break;
                case "flame":
                    if (v.Length >= 4) { flameBase = new Vector2(v[0], v[1]); flameSize = new Vector2(v[2], v[3]); }
                    break;
                case "star": if (v.Length >= 2) starPoints.Add(new Vector2(v[0], v[1])); break;
            }
        }
    }

    static float[] ParseNumbers(string text)
    {
        var parts = text.Split(',');
        var result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result[i]);
        return result;
    }

    public static bool HasLayers
    {
        get { Load(); return background != null; }
    }

    // 원본 그림 좌표 → 화면 좌표
    static Vector2 Map(Vector2 p) => new Vector2(bgRect.x + p.x * bgScale, bgRect.y + p.y * bgScale);

    // 모닥불 중심 (동료들이 둘러앉는 기준점)
    public static Vector2 FirePosition => HasLayers ? Map(flameBase + new Vector2(0f, -12f)) : new Vector2(UI.Width / 2f, 470f);

    // 배경 + 별. 동료보다 먼저 그림
    public static void DrawBackground()
    {
        Load();
        float w = UI.Width;
        if (background == null)
        {
            UI.Backdrop("camp");
            for (int i = 0; i < 14; i++)
            {
                float x = Mathf.Repeat(i * 137.5f, w);
                float y = Mathf.Repeat(i * 47.3f, 220f) + 70f;
                float a = 0.15f + 0.6f * Mathf.Abs(Mathf.Sin(Now * (0.4f + i % 5 * 0.25f) + i));
                UI.Glow(new Vector2(x, y), 6f, new Color(1f, 0.97f, 0.85f, a));
            }
            return;
        }

        // 화면을 꽉 채우도록 (넘치는 부분은 잘림)
        bgScale = Mathf.Max(w / canvas.x, UI.Height / canvas.y);
        var size = canvas * bgScale;
        bgRect = new Rect((w - size.x) / 2f, (UI.Height - size.y) / 2f, size.x, size.y);
        GUI.DrawTexture(bgRect, background, ScaleMode.StretchToFill);

        // 별: 별 그림 전체는 은은하게, 별 하나하나는 따로 반짝임
        if (stars != null) DrawTinted(bgRect, stars, new Color(1f, 1f, 1f, 0.75f + 0.15f * Mathf.Sin(Now * 0.7f)));
        for (int i = 0; i < starPoints.Count; i++)
        {
            float phase = i * 2.39f;
            float tw = Mathf.Sin(Now * (0.9f + (i % 7) * 0.23f) + phase);
            if (tw < 0.2f) continue;
            float a = (tw - 0.2f) / 0.8f;
            UI.Glow(Map(starPoints[i]), (2.5f + 3f * a) * bgScale, new Color(1f, 0.97f, 0.88f, 0.55f * a));
        }
        // 모닥불 빛이 닿는 바닥 (불꽃보다 먼저 그려서 뒤에 앉은 동료도 불빛을 받는 느낌)
        DrawGroundLight();
    }

    static float Flicker => 0.85f + 0.1f * Mathf.Sin(Now * 7.3f) + 0.05f * Mathf.Sin(Now * 13.1f + 1.3f);

    static void DrawGroundLight()
    {
        var fire = FirePosition;
        UI.Glow(fire + new Vector2(0f, 10f * bgScale), 260f * bgScale * Flicker, new Color(1f, 0.55f, 0.2f, 0.22f));
        if (glow != null) DrawTinted(bgRect, glow, new Color(1f, 0.65f, 0.3f, 0.9f * Flicker));
    }

    // 불꽃 (뒤쪽 동료와 앞쪽 동료 사이에 그림)
    public static void DrawFire()
    {
        Load();
        if (background == null || flames.Count == 0)
        {
            DrawSimpleFire(FirePosition);
            return;
        }
        // 불꽃 모양 4가지를 차례로, 사이사이를 부드럽게 섞어서 그림
        float t = Now / FlamePoseSeconds;
        int a = Mathf.FloorToInt(t) % flames.Count, b = (a + 1) % flames.Count;
        float mix = Mathf.SmoothStep(0f, 1f, t - Mathf.Floor(t));
        float sway = 1f + 0.05f * Mathf.Sin(Now * 5.1f);
        var baseP = Map(flameBase);
        var size = flameSize * bgScale;
        var r = new Rect(baseP.x - size.x / 2f, baseP.y - size.y * sway, size.x, size.y * sway);

        UI.Glow(baseP + new Vector2(0f, -size.y * 0.4f), size.x * 1.3f * Flicker, new Color(1f, 0.6f, 0.2f, 0.35f));
        DrawTinted(r, flames[a], new Color(1f, 1f, 1f, 1f - mix));
        DrawTinted(r, flames[b], new Color(1f, 1f, 1f, mix));
        // 위로 날아오르는 불티
        for (int i = 0; i < 6; i++)
        {
            float life = Mathf.Repeat(Now * 0.55f + i * 0.37f, 1f);
            float x = baseP.x + Mathf.Sin(i * 3.7f + life * 4f) * 14f * bgScale;
            float y = baseP.y - size.y * 0.6f - life * 120f * bgScale;
            UI.Glow(new Vector2(x, y), 3f * bgScale, new Color(1f, 0.75f, 0.35f, 0.9f * (1f - life)));
        }
    }

    static void DrawTinted(Rect r, Texture2D tex, Color tint)
    {
        var old = GUI.color;
        GUI.color = tint;
        GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
        GUI.color = old;
    }

    // 레이어 그림이 없을 때 쓰는 예전 모닥불
    static void DrawSimpleFire(Vector2 fire)
    {
        UI.Glow(fire, 170f + 10f * Mathf.Sin(Now * 7f), new Color(1f, 0.5f, 0.15f, 0.35f));
        UI.Fill(new Rect(fire.x - 34, fire.y + 6, 68, 10), new Color(0.35f, 0.22f, 0.12f));
        UI.Fill(new Rect(fire.x - 26, fire.y + 12, 52, 8), new Color(0.28f, 0.18f, 0.10f));
        for (int i = 0; i < 3; i++)
        {
            float flick = Mathf.Sin(Now * (9f + i * 3f) + i) * 4f;
            UI.Glow(new Vector2(fire.x + (i - 1) * 10f, fire.y - 10f - i * 6f + flick), 22f - i * 4f, new Color(1f, 0.7f - i * 0.15f, 0.2f, 0.95f));
        }
        UI.Glow(new Vector2(fire.x, fire.y - 6f), 12f, new Color(1f, 0.95f, 0.6f, 1f));
    }
}
