using System.Collections.Generic;
using UnityEngine;

// 보상 카드 고르기 연출입니다. (라운드를 이기면 / 처음 시작할 때)
//  1) 마법진이 깔리고 카드 3장이 뒤집힌 채 날아와 놓임 → 2) 한 장씩 뒤집힘 (좋은 등급일수록 빛 · 번쩍임이 큼)
//  3) 마우스를 올리면 떠오름, 눌러서 1장 선택 → 4) 고른 카드가 가운데로 올라오며 빛줄기 · 반짝이 → 확인
public class GachaScreen
{
    class Card
    {
        public CompanionDef def;
        public Vector2 home;          // 놓이는 자리 (가운데)
        public float dealAt, flipAt;  // 날아오기 · 뒤집기 시작 시간
        public bool sounded;          // 뒤집힐 때 소리를 냈는지
        public bool dealt;            // 날아올 때 소리를 냈는지
        public float hover;           // 0~1 마우스를 올린 정도
    }

    const float CardW = 220f, CardH = 320f;
    const float DealTime = 0.55f, FlipTime = 0.42f, ChooseTime = 0.7f;

    readonly List<Card> cards = new List<Card>();
    float start, chosenAt, flashAt = -10f;
    int chosen = -1;
    string title, subtitle, resultTag;
    System.Action onDone;
    static Texture2D back, ray;

    static float Now => Time.unscaledTime;

    // 카드 3장을 보여 주고 1장을 고르게 합니다. 고른 동료는 바로 저장돼요.
    public void StartPick(List<CompanionDef> options, string title, string subtitle, System.Action onDone)
    {
        this.title = title;
        this.subtitle = subtitle;
        this.onDone = onDone;
        cards.Clear();
        chosen = -1;
        resultTag = null;
        start = Now;
        float w = UI.Width;
        float dealEnd = 0.3f + (options.Count - 1) * 0.14f + DealTime;
        for (int i = 0; i < options.Count; i++)
        {
            cards.Add(new Card
            {
                def = options[i],
                home = new Vector2(w / 2f + (i - (options.Count - 1) / 2f) * 290f, 330f),
                dealAt = start + 0.3f + i * 0.14f,
                flipAt = start + dealEnd + 0.25f + i * 0.32f,
            });
        }
    }

    bool AllRevealed => cards.Count > 0 && Now > cards[cards.Count - 1].flipAt + FlipTime + 0.2f;

    public void Draw()
    {
        if (back == null) back = Resources.Load<Texture2D>("UI/card_back");
        if (ray == null) ray = MakeRay();
        float w = UI.Width;
        float t = Now - start;

        // ---- 배경: 짙은 밤하늘색 + 가운데 은은한 빛 + 바닥 마법진 ----
        UI.Gradient(UI.Full, new Color(0.04f, 0.03f, 0.10f), new Color(0.10f, 0.05f, 0.14f));
        UI.Glow(new Vector2(w / 2f, 340f), 520f, new Color(0.35f, 0.3f, 0.75f, 0.22f * Mathf.Clamp01(t * 2f)));
        DrawMagicCircle(new Vector2(w / 2f, 600f), Mathf.Clamp01(t * 1.5f), chosen >= 0 ? RarityInfo.Animated(cards[chosen].def.rarity) : new Color(0.6f, 0.75f, 1f));

        // ---- 제목 ----
        float ta = Mathf.Clamp01(t * 2f);
        UI.SharpText(new Rect(0, 40, w, 50), chosen >= 0 ? "새로운 동료!" : title, 34, UI.WithAlpha(UI.Gold, ta), new Color(0.1f, 0.05f, 0.02f, 0.8f * ta));
        if (chosen < 0 && !string.IsNullOrEmpty(subtitle))
            UI.Text(new Rect(0, 88, w, 28), subtitle, 17, UI.WithAlpha(UI.TextSub, ta));

        // ---- 카드 ----
        if (chosen >= 0) DrawChosen(w);
        else DrawChoices(w);

        // 화면 번쩍임
        float f = 1f - (Now - flashAt) / 0.5f;
        if (f > 0f) UI.Fill(UI.Full, new Color(1f, 0.97f, 0.9f, f * 0.85f));
    }

    void DrawChoices(float w)
    {
        bool ready = AllRevealed;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            float d = Now - c.dealAt;
            if (d < 0f) continue;
            if (!c.dealt) { c.dealt = true; AudioManager.Play("swing_fast", 0.25f, 0.1f); }

            // 날아오기: 아래쪽 카드 더미에서 튀어 올라 자리로 (살짝 넘쳤다 돌아오는 움직임)
            float k = Mathf.Clamp01(d / DealTime);
            float e = EaseOutBack(k);
            var from = new Vector2(w / 2f, 820f);
            var pos = Vector2.LerpUnclamped(from, c.home, e);
            float angle = Mathf.LerpUnclamped((i - 1) * -28f, 0f, e);
            float scale = Mathf.LerpUnclamped(0.55f, 1f, e);

            // 고를 수 있을 때: 둥실둥실 + 마우스를 올리면 떠오름
            var hit = new Rect(c.home.x - CardW / 2f, c.home.y - CardH / 2f, CardW, CardH);
            bool over = ready && hit.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                if (over && c.hover < 0.05f) AudioManager.Play("hover", 0.35f, 0.04f);
                c.hover = Mathf.MoveTowards(c.hover, over ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            }
            if (ready)
            {
                pos.y += Mathf.Sin(Now * 1.8f + i * 1.7f) * 4f - c.hover * 16f;
                scale *= 1f + c.hover * 0.06f;
            }

            DrawCard(c, pos, scale, angle, 1f);

            if (ready)
            {
                UI.Text(new Rect(c.home.x - 110f, c.home.y + CardH / 2f + 22f, 220f, 28f), "▲ 이 동료 선택", 17,
                    UI.WithAlpha(over ? UI.Gold : UI.TextSub, 0.6f + 0.4f * c.hover), TextAnchor.MiddleCenter, true);
                if (GUI.Button(hit, GUIContent.none, GUIStyle.none)) Choose(i);
            }
        }
        if (!ready && UI.ClickedAnywhere()) // 기다리기 싫으면 클릭으로 바로 펼침
        {
            float dealEnd = Now - 0.01f;
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].dealAt = Mathf.Min(cards[i].dealAt, dealEnd - DealTime);
                cards[i].flipAt = Mathf.Min(cards[i].flipAt, Now + i * 0.12f);
            }
        }
        if (ready) UI.Text(new Rect(0, 660, w, 30), "한 장을 골라 동료로 맞이하세요", 18, UI.WithAlpha(UI.TextMain, 0.85f));
    }

    void Choose(int i)
    {
        chosen = i;
        chosenAt = Now;
        var def = cards[i].def;
        resultTag = SaveData.AddPulled(def);
        SaveData.Save();
        AudioManager.Play(def.rarity >= Rarity.SuperRare ? "rare" : "gacha", 0.65f, 0.02f);
        flashAt = Now;
    }

    void DrawChosen(float w)
    {
        float t = Now - chosenAt;
        float k = EaseOutCubic(Mathf.Clamp01(t / ChooseTime));
        var center = new Vector2(w / 2f, 330f);
        var c0 = cards[chosen];
        Color rc = RarityInfo.Animated(c0.def.rarity);

        // 고르지 않은 카드: 아래로 가라앉으며 흐려짐
        for (int i = 0; i < cards.Count; i++)
        {
            if (i == chosen) continue;
            var c = cards[i];
            float a = 1f - Mathf.Clamp01(t / 0.45f);
            if (a <= 0f) continue;
            DrawCard(c, c.home + new Vector2(0f, 70f * (1f - a)), 1f - 0.1f * (1f - a), (i < chosen ? -8f : 8f) * (1f - a), a);
        }

        // 뒤에서 회전하는 빛줄기 + 빛 기둥
        float glowA = Mathf.Clamp01(t / 0.5f);
        UI.Glow(center, 330f, UI.WithAlpha(rc, 0.35f * glowA));
        DrawRays(center, 420f * (0.6f + 0.4f * k), UI.WithAlpha(Color.Lerp(rc, Color.white, 0.4f), 0.32f * glowA), 14, Now * 12f);
        DrawRays(center, 330f * (0.6f + 0.4f * k), UI.WithAlpha(UI.Gold, 0.22f * glowA), 9, -Now * 7f + 10f);
        DrawSparkles(center, 260f, rc, glowA);

        var pos = Vector2.Lerp(c0.home, center, k);
        float scale = Mathf.Lerp(1f, 1.18f, k) + Mathf.Sin(Now * 2f) * 0.01f;
        DrawCard(c0, pos, scale, 0f, 1f);

        if (t > 0.6f)
        {
            float a = Mathf.Clamp01((t - 0.6f) / 0.4f);
            var def = c0.def;
            string msg = def.IsHero ? "용사의 성급이 올랐어요!"
                : resultTag == "NEW" ? $"{def.name} 합류!"
                : $"{def.name}  성급 상승!  ({resultTag})";
            UI.SharpText(new Rect(0, 560, w, 44), msg, 30, UI.WithAlpha(new Color(1f, 0.93f, 0.7f), a), new Color(0.12f, 0.06f, 0.02f, 0.85f * a));
            if (t > 0.9f && UI.Button(new Rect(w / 2f - 110f, 620f, 220f, 58f), "확인", UI.Green, 22))
                onDone?.Invoke();
        }
    }

    // 카드 한 장: 가운데 pos, 크기 배율, 기울기(도), 투명도. 뒤집기 중이면 가로로 납작해졌다가 앞면이 나옴
    void DrawCard(Card c, Vector2 pos, float scale, float angle, float alpha)
    {
        float ft = Mathf.Clamp01((Now - c.flipAt) / FlipTime);
        bool front = ft >= 0.5f;
        if (front && !c.sounded)
        {
            c.sounded = true;
            var r0 = c.def.rarity;
            AudioManager.Play(r0 >= Rarity.Unique ? "rare" : "talk_next", r0 >= Rarity.Unique ? 0.55f : 0.5f, 0.03f);
            if (r0 >= Rarity.Legendary) flashAt = Now;
        }
        float squash = Mathf.Abs(Mathf.Cos(ft * Mathf.PI));
        float lift = Mathf.Sin(ft * Mathf.PI) * 22f;
        float cw = CardW * scale * Mathf.Max(0.02f, squash) * (1f + Mathf.Sin(ft * Mathf.PI) * 0.06f);
        float ch = CardH * scale * (1f + Mathf.Sin(ft * Mathf.PI) * 0.06f);
        var r = new Rect(pos.x - cw / 2f, pos.y - ch / 2f - lift, cw, ch);
        var rarity = c.def.rarity;
        Color rc = RarityInfo.Animated(rarity);

        // 그림자
        UI.Glow(new Vector2(pos.x, pos.y + CardH * scale * 0.52f), CardW * scale * 0.55f, new Color(0f, 0f, 0f, 0.35f * alpha));

        // 뒤집기 전에도 좋은 등급은 뒤에서 빛이 남 (확정 연출)
        if (rarity >= Rarity.SuperRare)
        {
            float pulse = 1f + 0.07f * Mathf.Sin(Now * 5f + pos.x);
            UI.Glow(pos, CardW * scale * 0.95f * pulse, UI.WithAlpha(rc, (front ? 0.45f : 0.35f) * alpha));
        }
        // 뒤집힌 직후 빛이 터지며 퍼짐
        float burst = Now - (c.flipAt + FlipTime * 0.5f);
        if (burst > 0f && burst < 0.8f)
        {
            float b = burst / 0.8f;
            UI.Glow(pos, CardW * scale * (0.5f + b * (rarity >= Rarity.Unique ? 2.6f : 1.4f)), UI.WithAlpha(Color.Lerp(Color.white, rc, b), (1f - b) * 0.8f * alpha));
            if (rarity >= Rarity.Unique) DrawRays(pos, CardH * scale * (0.6f + b), UI.WithAlpha(rc, (1f - b) * 0.5f * alpha), 10, Now * 30f);
        }

        var saved = UI.BeginRotate(angle, pos);
        var oldColor = GUI.color;
        if (!front)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            if (back != null) GUI.DrawTexture(r, back, ScaleMode.StretchToFill);
            else { UI.Round(r, new Color(0.10f, 0.12f, 0.30f, alpha)); UI.RoundFrame(r, UI.WithAlpha(UI.Gold, alpha)); }
            // 뒷면 위를 지나가는 빛 (반짝)
            float sheen = Mathf.Repeat(Now * 0.6f + pos.x * 0.002f, 1.6f);
            if (sheen < 1f && r.width > 30f)
                UI.Glow(new Vector2(r.x + r.width * sheen, r.y + r.height * (1f - sheen)), r.width * 0.35f, new Color(1f, 0.95f, 0.8f, 0.18f * alpha));
        }
        else
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            DrawCardFace(r, c.def, null, squash > 0.85f && alpha > 0.5f);
            if (rarity >= Rarity.Legendary && r.width > 40f) // 전설 이상: 금빛 테두리가 반짝
                UI.RoundFrame(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), UI.WithAlpha(rc, (0.6f + 0.4f * Mathf.Sin(Now * 6f)) * alpha));
        }
        GUI.color = oldColor;
        UI.EndRotate(saved);
    }

    // 가운데에서 뻗어 나가는 빛줄기 n개 (spin: 회전 각도)
    static void DrawRays(Vector2 center, float length, Color color, int n, float spin)
    {
        if (ray == null || color.a <= 0.01f) return;
        var old = GUI.color;
        GUI.color = color;
        for (int i = 0; i < n; i++)
        {
            float a = spin + i * 360f / n;
            float wdt = length * (i % 2 == 0 ? 0.16f : 0.10f);
            var saved = UI.BeginRotate(a, center);
            GUI.DrawTexture(new Rect(center.x - wdt / 2f, center.y - length, wdt, length), ray, ScaleMode.StretchToFill);
            UI.EndRotate(saved);
        }
        GUI.color = old;
    }

    // 위로 떠오르며 깜빡이는 반짝이
    static void DrawSparkles(Vector2 center, float radius, Color color, float alpha)
    {
        for (int i = 0; i < 26; i++)
        {
            float seed = i * 0.6180339f;
            float life = Mathf.Repeat(Now * (0.35f + (i % 5) * 0.06f) + seed, 1f);
            float x = center.x + (Mathf.Repeat(seed * 7.3f, 1f) * 2f - 1f) * radius;
            float y = center.y + radius * 0.6f - life * radius * 1.5f;
            float a = Mathf.Sin(life * Mathf.PI) * alpha;
            var p = new Vector2(x + Mathf.Sin(Now * 2f + i) * 6f, y);
            UI.Glow(p, 10f + (i % 3) * 5f, UI.WithAlpha(Color.Lerp(color, Color.white, 0.5f), 0.7f * a));
            UI.Fill(new Rect(p.x - 0.75f, p.y - 5f, 1.5f, 10f), new Color(1f, 1f, 1f, 0.8f * a));
            UI.Fill(new Rect(p.x - 5f, p.y - 0.75f, 10f, 1.5f), new Color(1f, 1f, 1f, 0.8f * a));
        }
    }

    // 바닥에 비스듬히 깔린 마법진: 점으로 된 고리 두 개가 서로 반대로 돌고, 가운데가 빛남
    static void DrawMagicCircle(Vector2 center, float alpha, Color color)
    {
        if (alpha <= 0f) return;
        UI.Glow(center, 300f, UI.WithAlpha(color, 0.18f * alpha));
        for (int ring = 0; ring < 3; ring++)
        {
            int n = ring == 0 ? 48 : ring == 1 ? 32 : 16;
            float rad = ring == 0 ? 420f : ring == 1 ? 320f : 200f;
            float spd = (ring % 2 == 0 ? 6f : -9f) * Mathf.Deg2Rad;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + Now * spd;
                var p = center + new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad * 0.17f);
                float big = (i % 4 == 0) ? 1f : 0.55f;
                UI.Glow(p, 9f * big, UI.WithAlpha(color, 0.5f * alpha));
                UI.Circle(p, 1.6f * big, UI.WithAlpha(Color.white, 0.7f * alpha));
            }
        }
    }

    // 빛줄기 그림: 아래(가운데)가 밝고 끝으로 갈수록, 양옆으로 갈수록 투명
    static Texture2D MakeRay()
    {
        const int w = 32, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float across = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float along = (y + 0.5f) / h;       // 텍스처 아래(0) = 화면에서 아래 = 가운데 쪽(밝음), 위(1) = 빛줄기 끝
                float a = across * across * Mathf.Pow(1f - along, 1.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    // 카드의 캐릭터 그림: 작은 상자 없이 카드 윗부분을 꽉 채움 (그림이 없으면 직업 문양)
    static void DrawCardPicture(Rect r, CompanionDef def, bool usePortrait, float s, Color rc)
    {
        var art = CharacterArt.For(def.id);
        var picture = art == null ? null : usePortrait ? (art.portrait ?? art.full) : (art.full ?? art.portrait);
        if (picture != null)
        {
            bool isPortrait = picture == art.portrait;
            UI.Glow(new Vector2(r.center.x, r.y + 100f * s), 95f * s, new Color(1f, 0.82f, 0.55f, 0.14f));
            var area = isPortrait
                ? new Rect(r.x + 3f, r.y + 3f, r.width - 6f, 202f * s)         // 초상화: 카드 윗부분 전체
                : new Rect(r.x + 6f * s, r.y + 30f * s, r.width - 12f * s, 160f * s); // 전신: 카드 안에 맞춤
            CharacterArt.DrawTexture(area, picture, isPortrait);
        }
        else
        {
            var center = new Vector2(r.center.x, r.y + 120f * s);
            UI.Circle(center, 50f * s, UI.WithAlpha(UI.Bronze, 0.9f));
            UI.Circle(center, 44f * s, def.job.color);
            UI.Text(new Rect(center.x - 50f * s, center.y - 50f * s, 100f * s, 100f * s), def.job.letter,
                Mathf.RoundToInt(40 * s), Color.white, TextAnchor.MiddleCenter, true);
        }
    }

    // 동료 카드 앞면 (동료 목록 화면에서도 사용)
    // star: 성급 (★ 개수), level: 0이면 레벨 표시 안 함
    // usePortrait: 그림이 있으면 전신 대신 초상화를 넣음 (도감용)
    public static void DrawCardFace(Rect r, CompanionDef def, string tag, bool drawText = true, int star = 1, int level = 0, bool usePortrait = false)
    {
        Color rc = RarityInfo.Animated(def.rarity);
        float s = r.height / 300f; // 카드 크기에 맞춰 글자 크기 조절

        // 카드 바탕: 차분한 가죽색 (등급은 테두리 색과 아래 글자로만 표시)
        UI.Round(r, new Color(0.09f, 0.07f, 0.06f));
        if (r.width > 24f) UI.Gradient(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height * 0.6f), new Color(0.24f, 0.18f, 0.13f), new Color(0.10f, 0.075f, 0.06f));
        if (!drawText) { UI.RoundFrame(r, UI.WithAlpha(rc, 0.85f)); return; }

        // 캐릭터 그림을 먼저 그리고, 그 위에 별 · 레벨 · 이름을 얹음
        DrawCardPicture(r, def, usePortrait, s, rc);
        UI.RoundFrame(r, UI.WithAlpha(rc, 0.85f));

        // 성급 (★) 과 레벨
        StarIcons.Draw(new Rect(r.x + 9f * s, r.y + 11f * s, r.width - 18f * s, 24f * s), star,
            level > 0 || !string.IsNullOrEmpty(tag) ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
        if (level > 0)
            UI.Text(new Rect(r.x + 10f * s, r.y + 10f * s, r.width - 20f * s, 30f * s), "Lv." + level,
                Mathf.RoundToInt(18 * s), UI.TextMain, TextAnchor.MiddleRight, true);

        // 이름 아래쪽은 글자가 잘 보이도록 어둡게
        UI.Gradient(new Rect(r.x + 3f, r.y + 175f * s, r.width - 6f, 30f * s), new Color(0.09f, 0.07f, 0.06f, 0f), new Color(0.09f, 0.07f, 0.06f, 1f));
        UI.Round(new Rect(r.x + 3f, r.y + 205f * s, r.width - 6f, r.height - 208f * s), new Color(0.09f, 0.07f, 0.06f, 1f));

        UI.Text(new Rect(r.x + 4f, r.y + 185f * s, r.width - 8f, 36f * s), def.name, Mathf.RoundToInt(26 * s), Color.white, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(r.x + 4f, r.y + 220f * s, r.width - 8f, 26f * s), $"{RarityInfo.Name(def.rarity)} · {def.job.name}",
            Mathf.RoundToInt(18 * s), rc, TextAnchor.MiddleCenter, true);

        // 기본 설정 [종족 / 소속 / 직업 / 성향]
        var profileColor = new Color(1f, 0.92f, 0.7f);
        if (s >= 0.9f)
        {
            UI.Text(new Rect(r.x + 6f, r.y + 248f * s, r.width - 12f, 22f * s), def.Profile, Mathf.RoundToInt(13 * s), profileColor);
            UI.Text(new Rect(r.x + 8f, r.y + 270f * s, r.width - 16f, 26f * s), def.desc, Mathf.RoundToInt(12 * s), UI.TextSub);
        }
        else
        {
            // 작은 카드에서는 두 줄로 나눠서 겹치지 않게 (설명은 생략)
            int size = Mathf.Max(10, Mathf.RoundToInt(15 * s));
            UI.Text(new Rect(r.x + 4f, r.y + 248f * s, r.width - 8f, size + 4), $"{def.race} · {def.faction}", size, profileColor);
            UI.Text(new Rect(r.x + 4f, r.y + 248f * s + size + 4, r.width - 8f, size + 4), $"{def.job.name} · {def.alignment}", size, profileColor);
        }

        if (!string.IsNullOrEmpty(tag))
        {
            float tw = (tag.Length <= 3 ? 64f : 104f) * s;
            var tagRect = new Rect(r.xMax - tw - 8f * s, r.y + 12f * s, tw, 26f * s); // 맨 윗줄 오른쪽 (그림을 가리지 않게)
            UI.Chip(tagRect, tag, def.IsHero ? new Color(0.95f, 0.55f, 0.1f) : new Color(0.9f, 0.25f, 0.3f), Mathf.Max(10, Mathf.RoundToInt(15 * s)));
        }
    }
}
