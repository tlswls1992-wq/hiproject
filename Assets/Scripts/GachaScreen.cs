using System.Collections.Generic;
using UnityEngine;

// 모바일 게임 스타일의 뽑기 연출입니다.
// 1) 마법진 소환 (가장 좋은 등급의 색으로 빛이 바뀜) → 2) 뒤집힌 카드 등장 → 3) 클릭해서 카드 뒤집기
public class GachaScreen
{
    class Card
    {
        public CompanionDef def;
        public string tag;            // "NEW", "각성 +2" 같은 표시 (없으면 null)
        public float flipStart = -1f; // 뒤집기 시작한 시간 (-1이면 아직 안 뒤집음)
    }

    const float PortalDuration = 2.4f;
    const float FlipDuration = 0.35f;

    readonly List<Card> cards = new List<Card>();
    bool inPortal;
    float phaseStart;
    float flashStart = -10f;
    Rarity best;
    string bannerName;
    System.Action onClose;

    static float Now => Time.unscaledTime;

    // 뽑기를 실행하고 결과를 바로 저장한 뒤 연출을 시작합니다. (골드 차감은 부르는 쪽에서)
    public void Start(GachaBanner banner, int count, System.Action onClose)
    {
        this.onClose = onClose;
        bannerName = banner.name;
        cards.Clear();
        best = Rarity.Normal;

        foreach (var def in banner.Roll(count))
        {
            cards.Add(new Card { def = def, tag = SaveData.AddPulled(def) });
            if (def.rarity > best) best = def.rarity;
        }
        SaveData.Save();

        inPortal = true;
        phaseStart = Now;
    }

    public void Draw()
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.03f, 0.02f, 0.10f), new Color(0.12f, 0.05f, 0.20f));

        if (inPortal) DrawPortal(w);
        else DrawCards(w);

        // 화면 번쩍임
        float f = 1f - (Now - flashStart) / 0.6f;
        if (f > 0f) UI.Fill(UI.Full, new Color(1f, 1f, 1f, f));
    }

    // 가장 좋은 결과에 따라 마법진 색이 바뀝니다 (모바일 가챠의 '확정 연출')
    Color HintColor()
    {
        if (best >= Rarity.SuperRare) return RarityInfo.Animated(best);
        return new Color(0.5f, 0.75f, 1f);
    }

    void DrawPortal(float w)
    {
        float t = Mathf.Clamp01((Now - phaseStart) / PortalDuration);
        var center = new Vector2(w / 2f, 320f);
        // 처음에는 하얀 빛 → 절반이 지나면 등급 색으로 변함
        Color c = Color.Lerp(new Color(0.8f, 0.9f, 1f), HintColor(), Mathf.Clamp01((t - 0.45f) * 3f));

        // 바깥에서 안으로 빨려 들어가는 빛 알갱이
        for (int i = 0; i < 40; i++)
        {
            float p = Mathf.Repeat(Now * 0.8f + i * 0.137f, 1f);
            float ang = i * 2.39996f;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            UI.Glow(center + dir * (420f * (1f - p)), 10f + 6f * p, UI.WithAlpha(c, p * 0.9f));
        }

        // 가운데 빛
        float pulse = 1f + 0.08f * Mathf.Sin(Now * 10f);
        UI.Glow(center, (90f + 240f * t * t) * pulse, UI.WithAlpha(c, 0.35f + 0.5f * t));
        UI.Glow(center, 60f * pulse, UI.WithAlpha(Color.white, 0.6f + 0.4f * t));

        // 점점 빨라지며 도는 마법진 구슬
        for (int ring = 0; ring < 2; ring++)
        {
            int n = ring == 0 ? 12 : 8;
            float radius = (ring == 0 ? 170f : 110f) - 30f * t;
            float speed = (ring == 0 ? 1f : -1.6f) * (1f + 4f * t * t);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + Now * speed;
                var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.45f) * radius;
                UI.Glow(p, 26f, UI.WithAlpha(c, 0.8f));
                UI.Circle(p, 5f, Color.white);
            }
        }

        UI.Text(new Rect(0, 560, w, 40), bannerName + " 소환 중...", 28, Color.white, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(0, 600, w, 30), "(클릭하면 건너뛰기)", 16, new Color(1f, 1f, 1f, 0.5f));

        if (t >= 1f || UI.ClickedAnywhere())
        {
            inPortal = false;
            phaseStart = Now;
            flashStart = Now;
        }
    }

    void DrawCards(float w)
    {
        int n = cards.Count;
        int perRow = n <= 5 ? n : Mathf.CeilToInt(n / 2f);
        int rows = Mathf.CeilToInt(n / (float)perRow);
        float cw = rows == 1 ? (n <= 2 ? 220f : 170f) : 150f;
        float ch = cw * 1.45f;
        const float gap = 22f;
        float top = rows == 1 ? 300f - ch / 2f : 90f;

        bool allFlipped = true;
        float lastFlipEnd = 0f;
        for (int i = 0; i < n; i++)
        {
            int row = i / perRow, col = i % perRow;
            int inThisRow = Mathf.Min(perRow, n - row * perRow);
            float x0 = (w - (cw * inThisRow + gap * (inThisRow - 1))) / 2f;
            var r = new Rect(x0 + col * (cw + gap), top + row * (ch + gap), cw, ch);

            var card = cards[i];
            float appear = Mathf.Clamp01((Now - phaseStart - i * 0.08f) / 0.3f);
            if (appear <= 0f) { allFlipped = false; continue; }
            r.y += 40f * (1f - appear);

            DrawCard(card, r, appear);

            if (card.flipStart < 0f)
            {
                allFlipped = false;
                if (appear >= 1f && GUI.Button(r, GUIContent.none, GUIStyle.none)) Flip(card, 0f);
            }
            else lastFlipEnd = Mathf.Max(lastFlipEnd, card.flipStart + FlipDuration);
        }

        if (!allFlipped)
        {
            UI.Text(new Rect(0, 560, w, 30), "카드를 눌러서 뒤집으세요", 20, new Color(1f, 1f, 1f, 0.8f));
            if (UI.Button(new Rect(w / 2f - 110f, 610f, 220f, 60f), "모두 열기", new Color(0.35f, 0.4f, 0.7f)))
            {
                float delay = 0f;
                foreach (var c in cards)
                {
                    if (c.flipStart >= 0f) continue;
                    Flip(c, delay);
                    delay += 0.12f;
                }
            }
        }
        else if (Now > lastFlipEnd + 0.3f)
        {
            int newCount = 0;
            bool awakened = false;
            foreach (var c in cards)
            {
                if (c.tag == "NEW") newCount++;
                if (c.def.IsHero) awakened = true;
            }
            string msg = newCount > 0 ? $"새로운 동료 {newCount}명이 합류했어요!" : "동료들이 합류했어요!";
            if (awakened) msg += "  용사가 각성했어요!";
            msg += "  (편성에서 배치하세요)";
            UI.Text(new Rect(0, 555, w, 40), msg, 24, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter, true);
            if (UI.Button(new Rect(w / 2f - 110f, 610f, 220f, 60f), "확인", new Color(0.25f, 0.6f, 0.35f)))
                onClose?.Invoke();
        }
    }

    void Flip(Card card, float delay)
    {
        card.flipStart = Now + delay;
        // 전설 이상은 뒤집히는 순간 화면이 번쩍!
        if (card.def.rarity >= Rarity.Legendary) flashStart = card.flipStart + FlipDuration * 0.5f;
    }

    void DrawCard(Card card, Rect r, float alpha)
    {
        var rarity = card.def.rarity;
        Color rc = RarityInfo.Animated(rarity);
        float flipT = card.flipStart < 0f ? 0f : Mathf.Clamp01((Now - card.flipStart) / FlipDuration);
        bool showFront = flipT >= 0.5f;
        float widthScale = Mathf.Abs(Mathf.Cos(flipT * Mathf.PI));
        var rr = new Rect(r.center.x - r.width * widthScale / 2f, r.y, r.width * widthScale, r.height);

        // 슈퍼레어 이상은 뒤집기 전부터 뒤에서 빛이 납니다
        if (rarity >= Rarity.SuperRare)
        {
            float pulse = 1f + 0.06f * Mathf.Sin(Now * 6f);
            UI.Glow(r.center, r.width * 0.95f * pulse, UI.WithAlpha(rc, 0.55f * alpha));
        }

        // 유니크 이상: 뒤집힌 직후 빛이 퍼져 나감
        float burst = Now - (card.flipStart + FlipDuration);
        if (card.flipStart >= 0f && rarity >= Rarity.Unique && burst > 0f && burst < 0.9f)
            UI.Glow(r.center, r.width * (0.6f + burst * 2.5f), UI.WithAlpha(rc, 1f - burst / 0.9f));

        if (!showFront)
        {
            Color frame = rarity >= Rarity.SuperRare ? rc : new Color(0.75f, 0.75f, 0.85f);
            UI.Fill(rr, new Color(0.10f, 0.12f, 0.30f, alpha));
            UI.Fill(new Rect(rr.x + 8f, rr.y + 8f, rr.width - 16f, rr.height - 16f), new Color(0.16f, 0.18f, 0.42f, alpha));
            UI.Frame(rr, UI.WithAlpha(frame, alpha), 4f);
            if (widthScale > 0.5f) UI.Text(rr, "?", Mathf.RoundToInt(r.width * 0.4f), UI.WithAlpha(frame, alpha), TextAnchor.MiddleCenter, true);
            return;
        }

        DrawCardFace(rr, card.def, card.tag, widthScale > 0.85f);
    }

    // 동료 카드 앞면 (동료 목록 화면에서도 사용)
    public static void DrawCardFace(Rect r, CompanionDef def, string tag, bool drawText = true)
    {
        Color rc = RarityInfo.Animated(def.rarity);
        float s = r.height / 300f; // 카드 크기에 맞춰 글자 크기 조절

        UI.Fill(r, UI.Darken(rc, 0.3f));
        UI.Gradient(new Rect(r.x + 6f, r.y + 6f, r.width - 12f, r.height - 12f), UI.Darken(rc, 0.55f), new Color(0.08f, 0.08f, 0.12f), 12);
        UI.Frame(r, rc, Mathf.Max(3f, 5f * s));
        if (!drawText) return;

        // 별 (등급)
        UI.Text(new Rect(r.x, r.y + 10f * s, r.width, 30f * s), new string('★', RarityInfo.Stars(def.rarity)),
            Mathf.RoundToInt(22 * s), new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, true);

        // 직업 문양
        var center = new Vector2(r.center.x, r.y + 120f * s);
        UI.Glow(center, 75f * s, UI.WithAlpha(def.job.color, 0.5f));
        UI.Circle(center, 50f * s, rc);
        UI.Circle(center, 44f * s, def.job.color);
        UI.Text(new Rect(center.x - 50f * s, center.y - 50f * s, 100f * s, 100f * s), def.job.letter,
            Mathf.RoundToInt(40 * s), Color.white, TextAnchor.MiddleCenter, true);

        UI.Text(new Rect(r.x + 4f, r.y + 185f * s, r.width - 8f, 36f * s), def.name, Mathf.RoundToInt(26 * s), Color.white, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(r.x + 4f, r.y + 222f * s, r.width - 8f, 26f * s), $"{RarityInfo.Name(def.rarity)} · {def.job.name}",
            Mathf.RoundToInt(18 * s), rc, TextAnchor.MiddleCenter, true);
        // 기본 설정 [종족 / 소속 / 직업 / 성향]
        UI.Text(new Rect(r.x + 6f, r.y + 248f * s, r.width - 12f, 22f * s), def.Profile, Mathf.RoundToInt(13 * s),
            new Color(1f, 0.92f, 0.7f), TextAnchor.MiddleCenter, false, false);
        UI.Text(new Rect(r.x + 8f, r.y + 270f * s, r.width - 16f, 28f * s), def.desc, Mathf.RoundToInt(12 * s),
            new Color(1f, 1f, 1f, 0.65f), TextAnchor.UpperCenter, false, false);

        if (!string.IsNullOrEmpty(tag))
        {
            float tw = (tag.Length <= 3 ? 70f : 110f) * s;
            var tagRect = new Rect(r.xMax - tw + 6f * s, r.y - 10f * s, tw, 28f * s);
            UI.Fill(tagRect, def.IsHero ? new Color(1f, 0.6f, 0.1f) : new Color(1f, 0.25f, 0.3f));
            UI.Text(tagRect, tag, Mathf.RoundToInt(17 * s), Color.white, TextAnchor.MiddleCenter, true, false);
        }
    }
}
