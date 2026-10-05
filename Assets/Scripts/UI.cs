using UnityEngine;

// 화면(UI)을 그리는 도우미 모음입니다.
// 모든 화면은 세로 720 기준 좌표로 그리고, 실제 화면 크기에 맞게 자동으로 늘어납니다.
public static class UI
{
    public const float Height = 720f;
    static float Scale => Screen.height / Height;
    public static float Width => Screen.width / Scale;
    public static Rect Full => new Rect(0f, 0f, Width, Height);

    public static void Begin()
    {
        GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
        GUI.color = Color.white;
    }

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

    public static void Text(Rect r, string text, int size, Color color,
        TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false, bool shadow = true)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            alignment = anchor,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            wordWrap = true,
            clipping = TextClipping.Overflow,
        };
        if (shadow)
        {
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.6f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
        }
        style.normal.textColor = color;
        GUI.Label(r, text, style);
    }

    // 모바일 게임 느낌의 두툼한 버튼. 눌렸으면 true를 돌려줍니다.
    public static bool Button(Rect r, string text, Color color, int fontSize = 22, bool enabled = true)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        Color bg = enabled ? (hover ? Color.Lerp(color, Color.white, 0.2f) : color) : new Color(0.32f, 0.32f, 0.35f);
        Fill(new Rect(r.x, r.y + 5f, r.width, r.height), Darken(bg, 0.45f));
        Fill(r, bg);
        Fill(new Rect(r.x, r.y, r.width, r.height * 0.45f), new Color(1f, 1f, 1f, 0.12f));
        Frame(r, Darken(bg, 0.6f), 2f);
        Text(r, text, fontSize, enabled ? Color.white : new Color(0.65f, 0.65f, 0.65f), TextAnchor.MiddleCenter, true);
        return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    // 둥근 모서리 대신 테두리가 있는 반투명 판
    public static void Panel(Rect r, Color color)
    {
        Fill(r, color);
        Frame(r, new Color(1f, 1f, 1f, 0.15f), 2f);
    }

    public static void Bar(Rect r, float pct, Color fill)
    {
        Fill(r, new Color(0f, 0f, 0f, 0.6f));
        Fill(new Rect(r.x + 2f, r.y + 2f, (r.width - 4f) * Mathf.Clamp01(pct), r.height - 4f), fill);
    }

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
