using System.Collections.Generic;
using UnityEngine;

// 화면(UI)을 그리는 도우미 모음입니다.
// 모든 화면은 세로 720 기준 좌표로 그리고, 실제 화면 크기에 맞게 자동으로 늘어납니다.
public static class UI
{
    public const float Height = 720f;
    static float Scale => Screen.height / Height;
    public static float Width => Screen.width / Scale;
    public static Rect Full => new Rect(0f, 0f, Width, Height);

    // ---- 색 테마 (UI 전체의 색을 여기서 한 번에 바꿀 수 있어요) ----
    public static readonly Color Gold = new Color(1f, 0.84f, 0.36f);
    public static readonly Color TextMain = new Color(0.96f, 0.96f, 0.98f);
    public static readonly Color TextSub = new Color(0.74f, 0.76f, 0.84f);
    public static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.13f, 0.82f);
    public static readonly Color Primary = new Color(0.90f, 0.56f, 0.18f);  // 주요 버튼 (출격, 소환)
    public static readonly Color Blue = new Color(0.27f, 0.47f, 0.76f);
    public static readonly Color Green = new Color(0.22f, 0.58f, 0.45f);
    public static readonly Color Red = new Color(0.70f, 0.27f, 0.30f);
    public static readonly Color Neutral = new Color(0.25f, 0.27f, 0.35f);
    static readonly Color Disabled = new Color(0.22f, 0.23f, 0.27f);

    public static void Begin()
    {
        GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
        GUI.color = Color.white;
    }

    // ---------------- 기본 도형 ----------------

    public static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    public static void Frame(Rect r, Color c, float t = 3f)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    // 위에서 아래로 색이 바뀌는 배경
    public static void Gradient(Rect r, Color top, Color bottom, int bands = 24)
    {
        float h = r.height / bands;
        for (int i = 0; i < bands; i++)
            Fill(new Rect(r.x, r.y + i * h, r.width, h + 1f), Color.Lerp(top, bottom, i / (float)(bands - 1)));
    }

    public static void Glow(Vector2 center, float radius, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), SpriteFactory.GlowTexture());
        GUI.color = old;
    }

    public static void Circle(Vector2 center, float radius, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), SpriteFactory.CircleTexture());
        GUI.color = old;
    }

    // ---------------- 둥근 모서리 ----------------

    static GUIStyle roundBig, roundSmall, ringBig, ringSmall;

    // 둥근 사각형 그림을 만들고, 늘려도 모서리가 찌그러지지 않게(9분할) 설정합니다.
    static GUIStyle MakeRoundStyle(int radius, int ring)
    {
        int size = radius * 2 + 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 가장 가까운 모서리 원의 중심까지 거리로 안쪽/바깥쪽 판단 (부드러운 가장자리)
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude;
                float a = Mathf.Clamp01(radius - d + 0.5f);
                if (ring > 0) a -= Mathf.Clamp01(radius - ring - d + 0.5f); // 테두리만 남기고 안쪽은 비움
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        var style = new GUIStyle();
        style.normal.background = tex;
        style.border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1);
        return style;
    }

    static void DrawStyled(GUIStyle style, Rect r, Color c)
    {
        if (Event.current.type != EventType.Repaint) return;
        var old = GUI.color;
        GUI.color = c;
        style.Draw(r, false, false, false, false);
        GUI.color = old;
    }

    static bool IsSmall(Rect r) => Mathf.Min(r.width, r.height) < 44f;

    public static void Round(Rect r, Color c)
    {
        if (Mathf.Min(r.width, r.height) < 16f) { Fill(r, c); return; } // 아주 작은 것은 그냥 네모로
        if (roundBig == null) { roundBig = MakeRoundStyle(14, 0); roundSmall = MakeRoundStyle(7, 0); }
        DrawStyled(IsSmall(r) ? roundSmall : roundBig, r, c);
    }

    public static void RoundFrame(Rect r, Color c)
    {
        if (Mathf.Min(r.width, r.height) < 16f) { Frame(r, c, 2f); return; }
        if (ringBig == null) { ringBig = MakeRoundStyle(14, 3); ringSmall = MakeRoundStyle(7, 2); }
        DrawStyled(IsSmall(r) ? ringSmall : ringBig, r, c);
    }

    // ---------------- 글자 ----------------

    static readonly Dictionary<int, GUIStyle> textStyles = new Dictionary<int, GUIStyle>();

    // 글자를 그립니다. (그림자 없이 한 번만 그려서 겹쳐 보이지 않아요)
    public static void Text(Rect r, string text, int size, Color color,
        TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false)
    {
        int key = size * 100 + (int)anchor * 2 + (bold ? 1 : 0);
        if (!textStyles.TryGetValue(key, out var style))
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = anchor,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = true,
                clipping = TextClipping.Clip, // 칸을 넘치는 글자는 잘라서, 다른 글자와 겹치지 않게
            };
            style.padding = new RectOffset(0, 0, 0, 0);
            style.margin = new RectOffset(0, 0, 0, 0);
            textStyles[key] = style;
        }
        // 마우스를 올렸을 때 등 모든 상태의 글자색을 똑같이 맞춰서, 색이 바뀌며 겹쳐 보이는 현상을 막습니다.
        style.normal.textColor = color;
        style.hover.textColor = color;
        style.active.textColor = color;
        style.focused.textColor = color;
        GUI.Label(r, text, style);
    }

    // ---------------- 버튼, 판, 막대 ----------------

    // 둥근 버튼. 눌렸으면 true를 돌려줍니다.
    public static bool Button(Rect r, string text, Color color, int fontSize = 22, bool enabled = true)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        Color bg = enabled ? (hover ? Color.Lerp(color, Color.white, 0.15f) : color) : Disabled;
        Round(new Rect(r.x, r.y + 3f, r.width, r.height), new Color(0f, 0f, 0f, 0.35f)); // 그림자
        Round(r, bg);
        Round(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height * 0.45f), new Color(1f, 1f, 1f, enabled ? 0.10f : 0.03f));
        if (hover) RoundFrame(r, new Color(1f, 1f, 1f, 0.45f));
        Text(r, text, fontSize, enabled ? Color.white : new Color(0.55f, 0.56f, 0.6f), TextAnchor.MiddleCenter, true);
        return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    // 둥근 반투명 판
    public static void Panel(Rect r, Color color)
    {
        Round(r, color);
        RoundFrame(r, new Color(1f, 1f, 1f, 0.08f));
    }

    public static void Panel(Rect r) => Panel(r, PanelColor);

    public static void Bar(Rect r, float pct, Color fill)
    {
        Round(r, new Color(0f, 0f, 0f, 0.55f));
        float w = (r.width - 4f) * Mathf.Clamp01(pct);
        if (w > 1f) Round(new Rect(r.x + 2f, r.y + 2f, w, r.height - 4f), fill);
    }

    // 작은 이름표 (NEW, 각성 등)
    public static void Chip(Rect r, string text, Color color, int size = 15)
    {
        Round(r, color);
        Text(r, text, size, Color.white, TextAnchor.MiddleCenter, true);
    }

    // 화면 위쪽 제목 막대 (오른쪽에 골드 표시)
    public static void TopBar(string title, bool showGold = true)
    {
        float w = Width;
        Fill(new Rect(0, 0, w, 64), new Color(0.03f, 0.03f, 0.06f, 0.75f));
        Fill(new Rect(0, 63, w, 1), WithAlpha(Gold, 0.35f));
        Text(new Rect(28, 0, w - 340, 64), title, 26, TextMain, TextAnchor.MiddleLeft, true);
        if (!showGold) return;
        var goldBox = new Rect(w - 250, 14, 226, 36);
        Round(goldBox, new Color(0f, 0f, 0f, 0.45f));
        Circle(new Vector2(goldBox.x + 20, goldBox.center.y), 10f, Gold);
        Text(new Rect(goldBox.x + 38, goldBox.y, goldBox.width - 52, goldBox.height), $"{SaveData.Gold:N0} 골드", 20,
            Gold, TextAnchor.MiddleRight, true);
    }

    // ---------------- 기타 ----------------

    // 회전해서 그리기: BeginRotate로 돌리고, 그린 다음 EndRotate에 돌려받은 값을 넘겨 원래대로 되돌립니다.
    public static Matrix4x4 BeginRotate(float angle, Vector2 pivot)
    {
        var saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, pivot * Scale);
        return saved;
    }

    public static void EndRotate(Matrix4x4 saved) => GUI.matrix = saved;

    public static Color Darken(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    // 버튼이 아닌 곳을 클릭했는지 (연출 건너뛰기, 대사 넘기기용). 버튼을 다 그린 다음에 호출하세요.
    public static bool ClickedAnywhere()
    {
        var e = Event.current;
        if (e.type != EventType.MouseDown) return false;
        e.Use();
        return true;
    }

    // 게임 세계 좌표 → 화면(UI) 좌표
    public static Vector2 WorldToUI(Camera cam, Vector3 world)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        return new Vector2(sp.x / Scale, (Screen.height - sp.y) / Scale);
    }
}
