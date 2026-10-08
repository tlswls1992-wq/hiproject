using System.Collections.Generic;
using UnityEngine;

// 화면(UI)을 그리는 도우미 모음입니다.
// 모든 화면은 세로 720 기준 좌표로 그리고, 실제 화면 크기에 맞게 자동으로 늘어납니다.
public static class UI
{
    public const float Height = 720f;
    public static float Scale => Screen.height / Height;
    public static float Width => Screen.width / Scale;
    public static Rect Full => new Rect(0f, 0f, Width, Height);

    // ---- 색 테마 (용사 그림에서 뽑은 색: 양피지, 버건디 망토, 네이비 옷, 가죽, 청동) ----
    // UI 전체의 색을 여기서 한 번에 바꿀 수 있어요.
    public static readonly Color Gold = new Color(0.93f, 0.79f, 0.47f);       // 금색 장식, 강조 글자
    public static readonly Color TextMain = new Color(0.96f, 0.91f, 0.81f);   // 양피지색 글자
    public static readonly Color TextSub = new Color(0.78f, 0.70f, 0.58f);    // 흐린 양피지색 글자
    public static readonly Color PanelColor = new Color(0.14f, 0.10f, 0.08f, 0.92f); // 어두운 가죽 판
    public static readonly Color Primary = new Color(0.56f, 0.16f, 0.17f);    // 버건디 (출격, 소환)
    public static readonly Color Blue = new Color(0.19f, 0.27f, 0.42f);       // 네이비
    public static readonly Color Green = new Color(0.29f, 0.37f, 0.22f);      // 이끼색
    public static readonly Color Red = new Color(0.52f, 0.23f, 0.14f);        // 녹슨 적갈색 (판매, 빼기)
    public static readonly Color Neutral = new Color(0.31f, 0.23f, 0.17f);    // 가죽 갈색
    public static readonly Color Plum = new Color(0.39f, 0.24f, 0.40f);       // 자주색
    public static readonly Color Bronze = new Color(0.55f, 0.40f, 0.22f);           // 테두리 청동
    static readonly Color Disabled = new Color(0.20f, 0.17f, 0.15f);

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
    static Font fontRegular, fontBold, fontTitle;
    static bool fontsLoaded;

    // 나눔명조 (Resources/Fonts). 큰 제목은 가장 굵은 글꼴을 써요. 글꼴 파일이 없으면 기본 글꼴.
    static Font FontFor(int size, bool bold)
    {
        if (!fontsLoaded)
        {
            fontsLoaded = true;
            fontRegular = Resources.Load<Font>("Fonts/NanumMyeongjo");
            fontBold = Resources.Load<Font>("Fonts/NanumMyeongjoBold");
            fontTitle = Resources.Load<Font>("Fonts/NanumMyeongjoExtraBold");
        }
        if (size >= 40 && fontTitle != null) return fontTitle;
        return bold ? (fontBold ?? fontRegular) : fontRegular;
    }

    // 글자를 그립니다. (그림자 없이 한 번만 그려서 겹쳐 보이지 않아요)
    public static void Text(Rect r, string text, int size, Color color,
        TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false)
    {
        int key = size * 100 + (int)anchor * 2 + (bold ? 1 : 0);
        if (!textStyles.TryGetValue(key, out var style))
        {
            var font = FontFor(size, bold);
            style = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = size,
                alignment = anchor,
                fontStyle = bold && font == null ? FontStyle.Bold : FontStyle.Normal, // 글꼴 파일이 있으면 굵은 글꼴 파일을 씀
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

    // 또렷한 글자: 화면 실제 해상도 크기의 글꼴로 그려서 (확대해서 번지지 않게) 테두리를 두릅니다.
    //  angle   : 글자 칸 가운데를 기준으로 기울이기 (도)
    //  spacing : 0보다 크면 글자를 한 자씩 이 간격(가상 좌표)으로 고르게 놓음
    public static void SharpText(Rect r, string text, int size, Color color, Color outline,
        float angle = 0f, float spacing = 0f)
    {
        var saved = GUI.matrix;
        float s = Scale;
        GUI.matrix = Matrix4x4.identity;
        var pr = new Rect(r.x * s, r.y * s, r.width * s, r.height * s);
        if (angle != 0f) GUIUtility.RotateAroundPivot(angle, pr.center);
        int px = Mathf.Max(1, Mathf.RoundToInt(size * s));
        float o = Mathf.Max(1f, size * s / 18f); // 테두리 두께 (글자 크기에 맞춤)
        int count = spacing > 0f ? text.Length : 1;
        for (int i = 0; i < count; i++)
        {
            string piece = spacing > 0f ? text[i].ToString() : text;
            if (piece == " ") continue;
            var cr = pr;
            if (spacing > 0f) cr.x += (i - (count - 1) * 0.5f) * spacing * s;
            if (outline.a > 0f)
            {
                Text(new Rect(cr.x, cr.y + o * 1.6f, cr.width, cr.height), piece, px, WithAlpha(outline, outline.a * 0.6f), TextAnchor.MiddleCenter, true); // 그림자
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    Text(new Rect(cr.x + Mathf.Cos(a) * o, cr.y + Mathf.Sin(a) * o, cr.width, cr.height), piece, px, outline, TextAnchor.MiddleCenter, true);
                }
            }
            Text(cr, piece, px, color, TextAnchor.MiddleCenter, true);
        }
        GUI.matrix = saved;
    }

    // ---------------- 버튼, 판, 막대 ----------------

    // 버튼: 가죽 바탕 + 청동/금색 테두리. 마우스를 올리면 금색으로 빛나요. 눌렸으면 true.
    // 인물 그림 뒤에 까는 차분한 배경 (등급 색 대신, 어두운 가죽색에 은은한 불빛)
    public static void PortraitBackdrop(Rect r, bool frame = true)
    {
        Round(r, new Color(0.10f, 0.075f, 0.06f, 0.96f));
        Gradient(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), new Color(0.26f, 0.19f, 0.14f, 0.9f), new Color(0.09f, 0.065f, 0.05f, 0.9f));
        Glow(new Vector2(r.center.x, r.y + r.height * 0.42f), Mathf.Min(r.width, r.height) * 0.55f, new Color(1f, 0.82f, 0.55f, 0.16f));
        if (frame) RoundFrame(r, WithAlpha(Bronze, 0.9f));
    }

    public static bool Button(Rect r, string text, Color color, int fontSize = 22, bool enabled = true)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        Color bg = enabled ? (hover ? Color.Lerp(color, Gold, 0.12f) : color) : Disabled;
        if (hover) Glow(r.center, Mathf.Max(r.width, r.height) * 0.75f, WithAlpha(Gold, 0.18f));
        Round(new Rect(r.x, r.y + 4f, r.width, r.height), new Color(0f, 0f, 0f, 0.45f)); // 그림자
        Round(r, Darken(bg, 0.75f));                                                       // 아래쪽 어두운 면
        Round(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 6f), bg);              // 윗면
        Round(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height * 0.42f), new Color(1f, 0.95f, 0.85f, enabled ? 0.09f : 0.02f)); // 반사광
        RoundFrame(r, enabled ? (hover ? Gold : WithAlpha(Bronze, 0.95f)) : WithAlpha(Bronze, 0.35f));
        Text(r, text, fontSize, enabled ? TextMain : new Color(0.5f, 0.45f, 0.4f), TextAnchor.MiddleCenter, true);
        bool clicked = enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
        if (clicked) AudioManager.Play("click", 0.5f, 0.03f);
        return clicked;
    }

    // 판: 어두운 가죽 + 청동 테두리 + 안쪽 금색 실선 + 모서리 장식
    public static void Panel(Rect r, Color color)
    {
        Round(new Rect(r.x + 2f, r.y + 5f, r.width, r.height), new Color(0f, 0f, 0f, 0.4f)); // 그림자
        Round(r, color);
        RoundFrame(r, WithAlpha(Bronze, 0.9f));
        if (r.width > 60f && r.height > 60f)
        {
            RoundFrame(new Rect(r.x + 5f, r.y + 5f, r.width - 10f, r.height - 10f), WithAlpha(Gold, 0.22f));
            float k = 9f;
            foreach (var c in new[] { new Vector2(r.x + k, r.y + k), new Vector2(r.xMax - k, r.y + k), new Vector2(r.x + k, r.yMax - k), new Vector2(r.xMax - k, r.yMax - k) })
            {
                Circle(c, 3.2f, WithAlpha(Gold, 0.75f));
                Circle(c, 1.4f, Darken(Bronze, 0.6f));
            }
        }
    }

    public static void Panel(Rect r) => Panel(r, PanelColor);

    public static void Bar(Rect r, float pct, Color fill)
    {
        Round(r, new Color(0.05f, 0.03f, 0.02f, 0.75f));
        float w = (r.width - 4f) * Mathf.Clamp01(pct);
        if (w > 1f)
        {
            Round(new Rect(r.x + 2f, r.y + 2f, w, r.height - 4f), fill);
            Fill(new Rect(r.x + 3f, r.y + 3f, Mathf.Max(0f, w - 2f), Mathf.Max(1f, (r.height - 6f) * 0.35f)), new Color(1f, 1f, 1f, 0.18f));
        }
        RoundFrame(r, WithAlpha(Bronze, 0.8f));
    }

    // 작은 이름표 (NEW, 성급 등)
    public static void Chip(Rect r, string text, Color color, int size = 15)
    {
        Round(r, color);
        RoundFrame(r, WithAlpha(Gold, 0.45f));
        Text(r, text, size, TextMain, TextAnchor.MiddleCenter, true);
    }

    // 화면 위쪽 제목 막대: 어두운 나무판 + 금색 두 줄 + 마름모 장식 (오른쪽에 골드 표시)
    public static void TopBar(string title, bool showGold = true)
    {
        float w = Width;
        Fill(new Rect(0, 0, w, 64), new Color(0.11f, 0.07f, 0.05f, 0.94f));
        Textured(new Rect(0, 0, w, 64), new Color(1f, 0.85f, 0.7f, 0.07f));
        Fill(new Rect(0, 60, w, 1), WithAlpha(Gold, 0.7f));
        Fill(new Rect(0, 63, w, 1), WithAlpha(Bronze, 0.8f));
        for (float x = 40; x < w; x += 160) Diamond(new Vector2(x, 61.5f), 4f, Gold);
        Diamond(new Vector2(22, 32), 6f, Gold);
        Text(new Rect(40, 0, w - 340, 62), title, 25, Gold, TextAnchor.MiddleLeft, true);
        if (!showGold) return;
        var goldBox = new Rect(w - 250, 13, 226, 36);
        Round(goldBox, new Color(0f, 0f, 0f, 0.5f));
        RoundFrame(goldBox, WithAlpha(Bronze, 0.9f));
        Circle(new Vector2(goldBox.x + 20, goldBox.center.y), 10f, Gold);
        Circle(new Vector2(goldBox.x + 20, goldBox.center.y), 6f, Darken(Gold, 0.8f));
        Text(new Rect(goldBox.x + 38, goldBox.y, goldBox.width - 52, goldBox.height), $"{SaveData.Gold:N0} 골드", 19,
            Gold, TextAnchor.MiddleRight, true);
    }

    // 작은 마름모 장식
    public static void Diamond(Vector2 c, float size, Color color)
    {
        var saved = BeginRotate(45f, c);
        Fill(new Rect(c.x - size / 2f, c.y - size / 2f, size, size), color);
        EndRotate(saved);
    }

    // ---------------- 배경 그림과 질감 ----------------

    static readonly Dictionary<string, Texture2D> backgrounds = new Dictionary<string, Texture2D>();
    static Texture2D leather;
    static bool leatherLoaded;

    // 배경 그림 (Resources/Backgrounds/이름) 을 화면 가득 그립니다. dim: 0~1 어둡게
    public static void Backdrop(string name, float dim = 0f)
    {
        var tex = Background(name);
        if (tex != null) GUI.DrawTexture(Full, tex, ScaleMode.ScaleAndCrop);
        else Gradient(Full, new Color(0.22f, 0.15f, 0.11f), new Color(0.08f, 0.05f, 0.04f));
        if (dim > 0f) Fill(Full, new Color(0f, 0f, 0f, dim));
    }

    public static Texture2D Background(string name)
    {
        if (!backgrounds.TryGetValue(name, out var tex))
        {
            tex = Resources.Load<Texture2D>("Backgrounds/" + name);
            backgrounds[name] = tex;
        }
        return tex;
    }

    // 가죽 질감을 이어 붙여 덮습니다 (네모난 곳에만 쓰세요)
    public static void Textured(Rect r, Color tint)
    {
        if (!leatherLoaded)
        {
            leatherLoaded = true;
            leather = Resources.Load<Texture2D>("UI/leather");
            if (leather != null) leather.wrapMode = TextureWrapMode.Repeat; // 이어 붙이기
        }
        if (leather == null) return;
        var old = GUI.color;
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(r, leather, new Rect(0f, 0f, r.width / 256f, r.height / 256f));
        GUI.color = old;
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
