using UnityEngine;

// 스토리 장면 그림입니다. 도형으로 그린 임시 그림이며, 움직이는 연출이 들어 있습니다.
// 나중에 진짜 일러스트가 생기면 이 파일의 그림을 이미지로 바꾸면 됩니다.
public static class StoryArt
{
    static float Now => Time.unscaledTime;

    static readonly Color Skin = new Color(0.98f, 0.82f, 0.66f);
    static readonly Color Gold = new Color(1f, 0.82f, 0.25f);

    // ---------------- 왕의 알현실 ----------------
    // talking: 대사가 나오는 중이면 입이 움직입니다.
    public static void DrawKing(float w, bool talking)
    {
        UI.Gradient(UI.Full, new Color(0.30f, 0.10f, 0.18f), new Color(0.12f, 0.04f, 0.08f));

        // 바닥과 레드카펫
        UI.Fill(new Rect(0, 420, w, 300), new Color(0.35f, 0.28f, 0.25f));
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            float half = Mathf.Lerp(70f, 170f, t);
            UI.Fill(new Rect(w / 2f - half, 420 + i * 50, half * 2f, 51), new Color(0.65f, 0.08f, 0.12f));
        }

        // 기둥과 깃발
        for (int side = -1; side <= 1; side += 2)
        {
            for (int k = 0; k < 2; k++)
            {
                float x = w / 2f + side * (300f + k * 170f) - 35f;
                UI.Fill(new Rect(x, 40, 70, 400), new Color(0.55f, 0.50f, 0.55f));
                UI.Fill(new Rect(x - 8, 30, 86, 24), new Color(0.65f, 0.60f, 0.65f));
                UI.Fill(new Rect(x - 8, 425, 86, 20), new Color(0.65f, 0.60f, 0.65f));
                float sway = Mathf.Sin(Now * 1.5f + k + side) * 4f;
                UI.Fill(new Rect(x + 12 + sway, 70, 46, 150), new Color(0.15f, 0.25f, 0.6f));
                UI.Circle(new Vector2(x + 35 + sway, 130), 12f, Gold);
            }
        }

        // 창문 빛
        UI.Glow(new Vector2(w / 2f, 80f), 260f, new Color(1f, 0.9f, 0.6f, 0.25f));

        // 왕좌
        var center = new Vector2(w / 2f, 250f);
        UI.Fill(new Rect(center.x - 130, 70, 260, 330), Gold);
        UI.Fill(new Rect(center.x - 110, 90, 220, 300), new Color(0.6f, 0.08f, 0.15f));
        for (int i = 0; i < 3; i++) UI.Circle(new Vector2(center.x - 80 + i * 80, 70), 18f, Gold);

        DrawKingFigure(center + new Vector2(0f, Mathf.Sin(Now * 2f) * 3f), talking);
    }

    static void DrawKingFigure(Vector2 c, bool talking)
    {
        // 망토와 옷
        UI.Fill(new Rect(c.x - 120, c.y + 55, 240, 200), new Color(0.45f, 0.12f, 0.55f));
        UI.Fill(new Rect(c.x - 120, c.y + 55, 240, 26), Color.white); // 흰 털 장식
        for (int i = 0; i < 8; i++) UI.Circle(new Vector2(c.x - 105 + i * 30, c.y + 68), 3f, Color.black);
        UI.Fill(new Rect(c.x - 12, c.y + 81, 24, 170), Gold);

        // 홀(지팡이)
        UI.Fill(new Rect(c.x + 120, c.y + 20, 10, 230), Gold);
        UI.Glow(new Vector2(c.x + 125, c.y + 15), 30f, new Color(1f, 0.3f, 0.3f, 0.8f));
        UI.Circle(new Vector2(c.x + 125, c.y + 15), 12f, new Color(0.9f, 0.1f, 0.2f));

        // 얼굴
        UI.Circle(c, 72f, Skin);
        // 수염
        UI.Circle(c + new Vector2(0, 55), 58f, new Color(0.95f, 0.95f, 0.95f));
        UI.Fill(new Rect(c.x - 58, c.y + 20, 116, 40), new Color(0.95f, 0.95f, 0.95f));
        UI.Circle(c + new Vector2(0, 5), 52f, Skin);
        // 콧수염
        UI.Circle(c + new Vector2(-18, 30), 16f, new Color(0.92f, 0.92f, 0.92f));
        UI.Circle(c + new Vector2(18, 30), 16f, new Color(0.92f, 0.92f, 0.92f));
        // 입 (말할 때 움직임)
        float open = talking ? 4f + Mathf.Abs(Mathf.Sin(Now * 14f)) * 7f : 3f;
        UI.Fill(new Rect(c.x - 9, c.y + 44, 18, open), new Color(0.4f, 0.1f, 0.1f));
        // 눈 (가끔 깜빡임)
        bool blink = Mathf.Repeat(Now, 3.2f) < 0.12f;
        for (int side = -1; side <= 1; side += 2)
        {
            var eye = c + new Vector2(side * 24f, -8f);
            if (blink) UI.Fill(new Rect(eye.x - 8, eye.y, 16, 3), Color.black);
            else UI.Circle(eye, 6f, Color.black);
            UI.Fill(new Rect(eye.x - 14, eye.y - 16, 28, 5), new Color(0.9f, 0.9f, 0.9f)); // 흰 눈썹
        }
        // 왕관
        UI.Fill(new Rect(c.x - 58, c.y - 92, 116, 28), Gold);
        for (int i = 0; i < 3; i++)
        {
            float x = c.x - 58 + i * 46;
            UI.Fill(new Rect(x, c.y - 120, 24, 30), Gold);
            UI.Circle(new Vector2(x + 12, c.y - 120), 7f, Gold);
        }
        UI.Circle(new Vector2(c.x, c.y - 78), 8f, new Color(0.9f, 0.1f, 0.2f));
        UI.Circle(new Vector2(c.x - 36, c.y - 78), 5f, new Color(0.2f, 0.5f, 1f));
        UI.Circle(new Vector2(c.x + 36, c.y - 78), 5f, new Color(0.2f, 0.5f, 1f));
    }

    // ---------------- 헤헤 웃으며 카드를 뽑는 용사 ----------------

    public static void DrawHeroGacha(float w)
    {
        UI.Gradient(UI.Full, new Color(0.10f, 0.06f, 0.25f), new Color(0.30f, 0.12f, 0.35f));

        // 반짝이는 별
        for (int i = 0; i < 30; i++)
        {
            float x = Mathf.Repeat(i * 151.7f, w);
            float y = Mathf.Repeat(i * 73.1f, 460f);
            float a = 0.2f + 0.8f * Mathf.Abs(Mathf.Sin(Now * (0.7f + i % 4 * 0.4f) + i));
            UI.Glow(new Vector2(x, y), 7f, new Color(1f, 1f, 0.85f, a));
        }

        float bob = Mathf.Sin(Now * 3f) * 5f;
        var c = new Vector2(w / 2f - 40f, 225f + bob);

        // 바닥의 마법진
        UI.Glow(new Vector2(c.x, 440f), 230f, new Color(1f, 0.8f, 0.3f, 0.35f));
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f + Now * 0.8f;
            UI.Glow(new Vector2(c.x + Mathf.Cos(a) * 190f, 440f + Mathf.Sin(a) * 40f), 16f, new Color(1f, 0.85f, 0.4f, 0.8f));
        }

        // 주위를 도는 카드들
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2f + Now * 0.6f;
            var p = new Vector2(c.x + Mathf.Cos(a) * 250f, c.y + Mathf.Sin(a) * 90f);
            float angle = Mathf.Sin(Now * 2f + i) * 20f;
            var saved = UI.BeginRotate(angle, p);
            DrawCardBack(new Rect(p.x - 22, p.y - 32, 44, 64));
            UI.EndRotate(saved);
        }

        // 몸 (망토 → 옷)
        UI.Fill(new Rect(c.x - 85, c.y + 55, 170, 190), new Color(0.75f, 0.15f, 0.2f));
        UI.Fill(new Rect(c.x - 65, c.y + 55, 130, 180), new Color(0.2f, 0.4f, 0.8f));
        UI.Fill(new Rect(c.x - 65, c.y + 140, 130, 16), new Color(0.45f, 0.3f, 0.15f)); // 허리띠
        UI.Fill(new Rect(c.x - 10, c.y + 140, 20, 16), Gold);

        // 왼손: 카드 뭉치
        UI.Fill(new Rect(c.x - 120, c.y + 80, 50, 22), new Color(0.2f, 0.4f, 0.8f)); // 팔
        for (int i = 0; i < 4; i++) DrawCardBack(new Rect(c.x - 150 + i * 2, c.y + 50 - i * 3, 40, 58));
        UI.Circle(new Vector2(c.x - 120, c.y + 92), 13f, Skin);

        // 오른손: 머리 위로 카드를 뽑아 듦 (1.6초마다 한 장씩, 뒤집히며 빛남)
        float cycle = 1.6f;
        float t = Mathf.Repeat(Now, cycle) / cycle;
        int pick = Mathf.FloorToInt(Now / cycle);
        var cardColor = RarityInfo.GetColor((Rarity)(pick % RarityInfo.Count));
        float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2.5f));
        var hand = new Vector2(c.x + 95, c.y + 40 - 110 * lift);
        UI.Fill(new Rect(c.x + 55, Mathf.Min(hand.y, c.y + 70), 22, Mathf.Abs(c.y + 70 - hand.y) + 10), new Color(0.2f, 0.4f, 0.8f)); // 팔
        float flip = Mathf.Clamp01((t - 0.4f) / 0.2f);
        float widthScale = Mathf.Abs(Mathf.Cos(flip * Mathf.PI));
        var cardRect = new Rect(hand.x - 30 * widthScale, hand.y - 95, 60 * widthScale, 86);
        if (flip >= 0.5f)
        {
            float burst = Mathf.Clamp01((t - 0.6f) / 0.4f);
            UI.Glow(new Vector2(hand.x, hand.y - 52), 60f + 120f * burst, UI.WithAlpha(cardColor, 0.9f * (1f - burst)));
            UI.Glow(new Vector2(hand.x, hand.y - 52), 55f, UI.WithAlpha(cardColor, 0.6f));
            UI.Fill(cardRect, cardColor);
            UI.Fill(new Rect(cardRect.x + 5, cardRect.y + 5, Mathf.Max(0, cardRect.width - 10), cardRect.height - 10), UI.Darken(cardColor, 0.5f));
            if (widthScale > 0.8f) UI.Text(cardRect, "★", 30, Color.white, TextAnchor.MiddleCenter, true);
        }
        else DrawCardBack(cardRect);
        UI.Circle(hand, 13f, Skin);

        // 머리
        UI.Circle(c, 70f, Skin);
        var hair = new Color(0.40f, 0.24f, 0.12f);
        UI.Fill(new Rect(c.x - 70, c.y - 70, 140, 34), hair);
        for (int i = 0; i < 5; i++) UI.Circle(new Vector2(c.x - 56 + i * 28, c.y - 66), 20f, hair);
        // 헤헤 웃는 눈 (^ ^)
        UI.Text(new Rect(c.x - 52, c.y - 38, 40, 56), "^", 40, new Color(0.2f, 0.1f, 0.05f), TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(c.x + 12, c.y - 38, 40, 56), "^", 40, new Color(0.2f, 0.1f, 0.05f), TextAnchor.MiddleCenter, true);
        // 볼 터치
        UI.Glow(c + new Vector2(-42, 18), 18f, new Color(1f, 0.45f, 0.5f, 0.7f));
        UI.Glow(c + new Vector2(42, 18), 18f, new Color(1f, 0.45f, 0.5f, 0.7f));
        // 활짝 웃는 입 (반원)
        UI.Circle(c + new Vector2(0, 24), 22f, new Color(0.55f, 0.12f, 0.12f));
        UI.Fill(new Rect(c.x - 24, c.y, 48, 24), Skin);
        UI.Fill(new Rect(c.x - 16, c.y + 24, 32, 6), Color.white); // 이빨

        // 말풍선 "헤헤"
        float pop = 1f + 0.08f * Mathf.Abs(Mathf.Sin(Now * 5f));
        var bubble = new Rect(c.x - 190, c.y - 150 - 6f * Mathf.Abs(Mathf.Sin(Now * 5f)), 120 * pop, 60 * pop);
        UI.Fill(bubble, Color.white);
        UI.Fill(new Rect(bubble.xMax - 30, bubble.yMax - 2, 16, 18), Color.white);
        UI.Text(bubble, "헤헤", 30, new Color(0.3f, 0.15f, 0.1f), TextAnchor.MiddleCenter, true);
    }

    static void DrawCardBack(Rect r)
    {
        UI.Fill(r, new Color(0.85f, 0.75f, 0.45f));
        UI.Fill(new Rect(r.x + 3, r.y + 3, Mathf.Max(0, r.width - 6), r.height - 6), new Color(0.15f, 0.18f, 0.45f));
        if (r.width > 20) UI.Text(r, "?", Mathf.RoundToInt(r.height * 0.4f), new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, true);
    }

    // ---------------- 승리 / 왕국 ----------------

    public static void DrawVictory(float w)
    {
        UI.Gradient(UI.Full, new Color(1f, 0.85f, 0.5f), new Color(0.95f, 0.55f, 0.35f));
        UI.Glow(new Vector2(w / 2f, 250f), 300f + 20f * Mathf.Sin(Now * 2f), new Color(1f, 1f, 0.8f, 0.8f));
        for (int i = 0; i < 24; i++)
        {
            // 꽃가루
            float x = Mathf.Repeat(i * 97.3f + Now * 30f, w);
            float y = Mathf.Repeat(i * 53.9f + Now * (60f + i % 5 * 15f), 720f);
            UI.Fill(new Rect(x, y, 8, 8), Color.HSVToRGB(Mathf.Repeat(i * 0.13f, 1f), 0.6f, 1f));
        }
    }

    public static void DrawKingdom(float w)
    {
        UI.Gradient(UI.Full, new Color(0.45f, 0.70f, 0.95f), new Color(0.85f, 0.90f, 1f));
        UI.Fill(new Rect(0, 400, w, 320), new Color(0.40f, 0.65f, 0.32f));
        float cx = w / 2f, gy = 400f;
        var c = new Color(0.85f, 0.85f, 0.9f);
        UI.Fill(new Rect(cx - 150f, gy - 140f, 300f, 140f), c);
        UI.Fill(new Rect(cx - 190f, gy - 200f, 70f, 200f), c);
        UI.Fill(new Rect(cx + 120f, gy - 200f, 70f, 200f), c);
        UI.Fill(new Rect(cx - 45f, gy - 260f, 90f, 260f), c);
        UI.Fill(new Rect(cx - 25f, gy - 70f, 50f, 70f), UI.Darken(c, 0.4f));
        float sway = Mathf.Sin(Now * 2f) * 6f;
        UI.Fill(new Rect(cx - 2, gy - 320, 4, 60), Color.gray);
        UI.Fill(new Rect(cx + 2, gy - 318, 40 + sway, 24), new Color(0.15f, 0.25f, 0.6f));
    }
}
