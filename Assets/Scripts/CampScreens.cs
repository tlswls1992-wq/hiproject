using System.Collections.Generic;
using UnityEngine;

// 캠프의 '대화'와 '강화' 화면입니다.

// ---------------- 대사 모음 ----------------
public static class CampTalk
{
    // ★ 캐릭터 전용 대사 (id: 대사들). 여기에 없는 캐릭터는 성향에 맞는 대사를 말해요.
    static readonly Dictionary<string, string[]> lines = new Dictionary<string, string[]>
    {
        { CompanionDef.HeroId, new[]
            {
                "헤헤, 오늘은 뭐가 나올까?",
                "마왕을 무찌르고 돌아가면 공주님과... 헤헤.",
                "뽑기는 과학이야. ...아마도.",
                "다들 고마워. 내 힘은 너희를 만나는 거니까.",
            } },
        { "n_balo", new[]
            {
                "용사님! 오늘도 정의를 위해 싸우겠습니다!",
                "올 왕국의 병사로서 부끄럽지 않게 싸우겠습니다!",
                "드보람 녀석, 또 어디서 쉬고 있는 거야...",
            } },
        { "n_deboram", new[]
            {
                "...수당은 언제 나와요?",
                "위험수당은 따로 쳐 주시는 거죠?",
                "발로 녀석은 너무 열심이라 옆에 있으면 피곤해요.",
            } },
    };

    static readonly Dictionary<Alignment, string[]> byAlignment = new Dictionary<Alignment, string[]>
    {
        { Alignment.혼돈, new[] { "규칙? 그런 건 재미없잖아.", "오늘은 어떤 난리가 날까? 기대되는데!" } },
        { Alignment.중립, new[] { "흐름에 맡기는 거지.", "어느 편도 아니야. 지금은 네 편이지만." } },
        { Alignment.질서, new[] { "규율이 승리를 만듭니다.", "작전대로 움직이면 이길 수 있습니다." } },
        { Alignment.명예, new[] { "명예롭게 싸우겠다.", "등을 보이는 일은 없을 것이다." } },
        { Alignment.실리, new[] { "보수만 확실하면 돼.", "남는 장사라면 어디든 가지." } },
        { Alignment.선, new[] { "모두를 지키고 싶어요.", "다친 사람이 없어야 할 텐데..." } },
        { Alignment.악, new[] { "흐흐... 언젠가 두고 보자고.", "마왕 다음은 누구 차례일까?" } },
    };

    public static string[] LinesOf(CompanionDef def)
    {
        if (lines.TryGetValue(def.id, out var own)) return own;
        return byAlignment.TryGetValue(def.alignment, out var common) ? common : new[] { "..." };
    }
}

// ---------------- 캐릭터 목록 (대화/강화 화면 왼쪽) ----------------
public static class CharacterList
{
    const float RowH = 64f;
    const float RowGap = 6f;

    // 목록을 그리고, 누른 캐릭터를 selected로 돌려줍니다.
    public static void Draw(Rect area, List<CompanionDef> items, ref CompanionDef selected, ref float scroll, bool showLevel)
    {
        var e = Event.current;
        UI.Panel(area);
        var inner = new Rect(area.x + 8, area.y + 8, area.width - 16, area.height - 16);
        float maxScroll = Mathf.Max(0f, items.Count * (RowH + RowGap) - inner.height);
        if (e.type == EventType.ScrollWheel && inner.Contains(e.mousePosition))
        {
            scroll += e.delta.y * 20f;
            e.Use();
        }
        scroll = Mathf.Clamp(scroll, 0f, maxScroll);

        GUI.BeginGroup(inner);
        for (int i = 0; i < items.Count; i++)
        {
            var def = items[i];
            var r = new Rect(0, i * (RowH + RowGap) - scroll, inner.width, RowH);
            if (r.yMax < 0 || r.y > inner.height) continue;
            bool isSelected = def == selected;
            bool hover = r.Contains(e.mousePosition);
            UI.Round(r, isSelected ? new Color(1f, 1f, 1f, 0.16f) : hover ? new Color(1f, 1f, 1f, 0.08f) : new Color(1f, 1f, 1f, 0.03f));
            if (isSelected) UI.RoundFrame(r, UI.WithAlpha(UI.Gold, 0.7f));

            Color rc = RarityInfo.Animated(def.rarity);
            var c = new Vector2(r.x + 34, r.center.y);
            UI.Circle(c, 23f, rc);
            UI.Circle(c, 20f, def.job.color);
            UI.Text(new Rect(c.x - 20, c.y - 20, 40, 40), def.job.letter, 18, Color.white, TextAnchor.MiddleCenter, true);
            UI.Text(new Rect(r.x + 66, r.y + 8, r.width - 140, 26), def.name, 19, UI.TextMain, TextAnchor.MiddleLeft, true);
            UI.Text(new Rect(r.x + 66, r.y + 34, r.width - 140, 22), $"{RarityInfo.Name(def.rarity)} · {def.job.name}", 14, rc, TextAnchor.MiddleLeft);
            if (showLevel)
                UI.Text(new Rect(r.xMax - 80, r.y, 70, RowH), $"Lv.{SaveData.LevelOf(def)}", 18, UI.Gold, TextAnchor.MiddleRight, true);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) selected = def;
        }
        GUI.EndGroup();
    }

    // 큰 초상 (동그라미)
    public static void DrawPortrait(Vector2 center, CompanionDef def, float radius)
    {
        Color rc = RarityInfo.Animated(def.rarity);
        UI.Glow(center, radius * 1.9f, UI.WithAlpha(rc, 0.35f));
        UI.Circle(center, radius + 6f, rc);
        UI.Circle(center, radius, def.job.color);
        UI.Text(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), def.job.letter,
            Mathf.RoundToInt(radius * 0.9f), Color.white, TextAnchor.MiddleCenter, true);
    }
}

// ---------------- 대화 화면 ----------------
public class TalkScreen
{
    CompanionDef selected;
    int lineIndex;
    float lineStart;
    float scroll;

    static float Now => Time.unscaledTime;

    public void Open()
    {
        selected = CompanionDef.Hero;
        lineIndex = 0;
        lineStart = Now;
        scroll = 0f;
    }

    public void Draw(System.Action onExit, float charsPerSecond)
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.14f, 0.12f, 0.20f), new Color(0.22f, 0.15f, 0.12f));
        var fire = new Vector2(w * 0.68f, 640f);
        UI.Glow(fire, 200f + 10f * Mathf.Sin(Now * 6f), new Color(1f, 0.5f, 0.15f, 0.30f));
        UI.TopBar("대화");

        var before = selected;
        CharacterList.Draw(new Rect(24, 84, 340, 540), SaveData.OwnedCharacters(), ref selected, ref scroll, false);
        if (selected != before) { lineIndex = 0; lineStart = Now; }
        if (selected == null) selected = CompanionDef.Hero;

        // 오른쪽: 초상과 대사
        float rx = 400f, rw = w - rx - 30f;
        var portrait = new Vector2(rx + rw / 2f, 230f + Mathf.Sin(Now * 2f) * 3f);
        CharacterList.DrawPortrait(portrait, selected, 90f);
        UI.Text(new Rect(rx, 340, rw, 34), selected.name, 28, UI.TextMain, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(rx, 374, rw, 24), selected.Profile, 16, UI.TextSub);

        var lines = CampTalk.LinesOf(selected);
        string line = lines[lineIndex % lines.Length];
        int shown = Mathf.Min(line.Length, Mathf.FloorToInt((Now - lineStart) * charsPerSecond));
        var box = new Rect(rx, 420, rw, 140);
        UI.Panel(box);
        var nameTag = new Rect(box.x + 20, box.y - 18, 150, 36);
        UI.Chip(nameTag, selected.name, UI.Darken(UI.Primary, 0.9f), 17);
        UI.Text(new Rect(box.x + 30, box.y + 30, box.width - 60, box.height - 50), line.Substring(0, shown), 22, UI.TextMain, TextAnchor.UpperLeft);

        if (UI.Button(new Rect(box.xMax - 170, box.yMax + 14, 170, 50), "다음 대사 ▶", UI.Blue, 18))
        {
            if (shown < line.Length) lineStart = -1000f;
            else { lineIndex++; lineStart = Now; }
        }
        if (UI.Button(new Rect(24, 640, 170, 54), "◀ 돌아가기", UI.Neutral, 18))
            onExit();
    }
}

// ---------------- 강화 화면 ----------------
public class EnhanceScreen
{
    CompanionDef selected;
    float scroll;
    float flashStart = -10f;

    static float Now => Time.unscaledTime;

    public void Open()
    {
        selected = CompanionDef.Hero;
        scroll = 0f;
    }

    public void Draw(System.Action onExit, System.Action<string> toast)
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.10f, 0.13f, 0.22f), new Color(0.05f, 0.06f, 0.10f));
        UI.TopBar("강화");

        CharacterList.Draw(new Rect(24, 84, 340, 540), SaveData.OwnedCharacters(), ref selected, ref scroll, true);
        if (selected == null) selected = CompanionDef.Hero;
        var def = selected;

        float rx = 400f, rw = w - rx - 30f;
        // 카드
        var card = new Rect(rx + 10, 96, 230, 334);
        float f = 1f - (Now - flashStart) / 0.6f;
        if (f > 0f) UI.Glow(card.center, 260f, UI.WithAlpha(UI.Gold, 0.6f * f));
        GachaScreen.DrawCardFace(card, def, def.IsHero && SaveData.HeroAwaken > 0 ? "각성 +" + SaveData.HeroAwaken : null);

        // 능력치 비교
        int lv = SaveData.LevelOf(def);
        bool maxed = lv >= SaveData.MaxLevel;
        float baseMul = RarityInfo.StatMultiplier(def.rarity) * (def.IsHero ? 1f + 0.1f * SaveData.HeroAwaken : 1f);
        float now = baseMul * (1f + SaveData.LevelBonus * (lv - 1));
        float next = baseMul * (1f + SaveData.LevelBonus * lv);

        var info = new Rect(card.xMax + 30, 96, rx + rw - card.xMax - 30, 334);
        UI.Panel(info);
        UI.Text(new Rect(info.x + 24, info.y + 16, info.width - 48, 40), maxed ? $"Lv.{lv}  (최대)" : $"Lv.{lv}  →  Lv.{lv + 1}", 30, UI.Gold, TextAnchor.MiddleLeft, true);
        UI.Text(new Rect(info.x + 24, info.y + 58, info.width - 48, 24), $"강화 1레벨마다 능력치 +{SaveData.LevelBonus * 100:0}%  (최대 Lv.{SaveData.MaxLevel})", 15, UI.TextSub, TextAnchor.MiddleLeft);

        string powerName = def.job.healer ? "치유" : "공격";
        DrawStatRow(new Rect(info.x + 24, info.y + 100, info.width - 48, 36), "체력", def.job.hp * now, def.job.hp * next, maxed);
        DrawStatRow(new Rect(info.x + 24, info.y + 142, info.width - 48, 36), powerName, def.job.damage * now, def.job.damage * next, maxed);
        UI.Text(new Rect(info.x + 24, info.y + 184, info.width - 48, 30), $"사거리 {def.job.range:0.#}   ·   {def.job.role}", 17, UI.TextSub, TextAnchor.MiddleLeft);

        int cost = SaveData.EnhanceCost(def);
        bool canAfford = SaveData.Gold >= cost;
        string label = maxed ? "최대 레벨" : $"강화하기  ({cost:N0} 골드)";
        if (UI.Button(new Rect(info.x + 24, info.yMax - 90, info.width - 48, 66), label, UI.Primary, 22, !maxed && canAfford))
        {
            if (SaveData.Enhance(def))
            {
                flashStart = Now;
                toast($"{def.name} 강화 성공! Lv.{SaveData.LevelOf(def)}");
            }
        }
        if (!maxed && !canAfford)
            UI.Text(new Rect(info.x, info.yMax + 8, info.width, 24), "골드가 부족해요", 16, new Color(1f, 0.5f, 0.5f));

        UI.Text(new Rect(rx, 470, rw, 60), def.IsHero
            ? "용사는 강화와 각성(뽑기에서 용사 카드)으로 강해져요."
            : def.IsNamed ? "이름이 있는 캐릭터는 한 명만 얻을 수 있어요."
            : "같은 캐릭터를 여러 명 가지고 있으면 모두 함께 강화돼요.", 17, UI.TextSub);

        if (UI.Button(new Rect(24, 640, 170, 54), "◀ 돌아가기", UI.Neutral, 18))
            onExit();
    }

    static void DrawStatRow(Rect r, string name, float now, float next, bool maxed)
    {
        UI.Round(r, new Color(1f, 1f, 1f, 0.05f));
        UI.Text(new Rect(r.x + 14, r.y, 100, r.height), name, 18, UI.TextSub, TextAnchor.MiddleLeft, true);
        UI.Text(new Rect(r.x + 110, r.y, 120, r.height), $"{now:0}", 20, UI.TextMain, TextAnchor.MiddleLeft, true);
        if (!maxed)
            UI.Text(new Rect(r.x + 200, r.y, r.width - 210, r.height), $"→  {next:0}  (+{next - now:0})", 20, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleLeft, true);
    }
}
