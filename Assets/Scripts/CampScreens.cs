using System.Collections.Generic;
using UnityEngine;

// 야영지에서 들어가는 화면들: 동료(성급 강화 · 판매), 도감(인물 · 적 · 아이템), 전승 특전(호감도)

// ---------------- 대사 모음 (야영지에서 말을 걸면 나오는 대사) ----------------
public static class CampTalk
{
    // ★ 캐릭터 전용 대사 (id: 대사들). 여기에 없는 캐릭터는 성향에 맞는 대사를 말해요.
    static readonly Dictionary<string, string[]> lines = new Dictionary<string, string[]>
    {
        { CompanionDef.HeroId, new[]
            {
                "헤헤, 다음엔 뭐가 나올까?",
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

// ---------------- 공통 그림 ----------------
public static class Portrait
{
    public static void Draw(Vector2 center, CompanionDef def, float radius, bool glow = true)
    {
        Color rc = RarityInfo.Animated(def.rarity);
        if (glow) UI.Glow(center, radius * 1.9f, UI.WithAlpha(rc, 0.35f));
        UI.Circle(center, radius + Mathf.Max(3f, radius * 0.12f), rc);
        UI.Circle(center, radius, def.job.color);
        UI.Text(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), def.job.letter,
            Mathf.RoundToInt(radius * 0.9f), Color.white, TextAnchor.MiddleCenter, true);
    }

    public static string Hearts(int n) => n <= 0 ? "" : new string('♥', n);
}

// ---------------- 동료 화면: 성급 강화 · 판매 ----------------
public class MemberScreen
{
    enum Confirm { None, Merge, Sell }

    SaveData.Member selected;
    SaveData.Member material;
    float scroll;
    Confirm confirm;
    float flashStart = -10f;

    static float Now => Time.unscaledTime;

    public void Open()
    {
        selected = SaveData.HeroMember;
        material = null;
        scroll = 0f;
        confirm = Confirm.None;
    }

    public void Draw(System.Action onExit, System.Action<string> toast)
    {
        float w = UI.Width;
        var e = Event.current;
        UI.Gradient(UI.Full, new Color(0.10f, 0.13f, 0.22f), new Color(0.05f, 0.06f, 0.10f));
        UI.TopBar($"동료   {SaveData.CompanionCount}명  ·  성급 강화 / 판매");
        bool active = confirm == Confirm.None;

        if (selected == null || !SaveData.Roster.Contains(selected)) selected = SaveData.HeroMember;
        if (material != null && !SaveData.CanMerge(selected, material)) material = null;

        // ---- 왼쪽: 동료 목록 ----
        var list = SaveData.SortedMembers();
        var area = new Rect(24, 84, 340, 540);
        UI.Panel(area);
        var inner = new Rect(area.x + 8, area.y + 8, area.width - 16, area.height - 16);
        const float rowH = 60f, rowGap = 6f;
        float maxScroll = Mathf.Max(0f, list.Count * (rowH + rowGap) - inner.height);
        if (active && e.type == EventType.ScrollWheel && inner.Contains(e.mousePosition)) { scroll += e.delta.y * 20f; e.Use(); }
        scroll = Mathf.Clamp(scroll, 0f, maxScroll);
        GUI.BeginGroup(inner);
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            var r = new Rect(0, i * (rowH + rowGap) - scroll, inner.width, rowH);
            if (r.yMax < 0 || r.y > inner.height) continue;
            bool isSel = m == selected;
            UI.Round(r, isSel ? new Color(1f, 1f, 1f, 0.16f) : r.Contains(e.mousePosition) ? new Color(1f, 1f, 1f, 0.08f) : new Color(1f, 1f, 1f, 0.03f));
            if (isSel) UI.RoundFrame(r, UI.WithAlpha(UI.Gold, 0.7f));
            Portrait.Draw(new Vector2(r.x + 32, r.center.y), m.Def, 20f, false);
            UI.Text(new Rect(r.x + 62, r.y + 6, r.width - 130, 26), m.Def.name, 18, UI.TextMain, TextAnchor.MiddleLeft, true);
            UI.Text(new Rect(r.x + 62, r.y + 32, r.width - 130, 22), $"{FormationScreen.Stars(m.star)}  Lv.{m.level}", 14, UI.Gold, TextAnchor.MiddleLeft);
            if (SaveData.IsPlaced(m)) UI.Chip(new Rect(r.xMax - 58, r.y + 18, 50, 24), "출진", UI.Green, 13);
            if (active && GUI.Button(r, GUIContent.none, GUIStyle.none)) { selected = m; material = null; }
        }
        GUI.EndGroup();

        // ---- 오른쪽: 카드와 정보 ----
        var def = selected.Def;
        float rx = 390f, rw = w - rx - 24f;
        var card = new Rect(rx, 90, 220, 320);
        float f = 1f - (Now - flashStart) / 0.7f;
        if (f > 0f) UI.Glow(card.center, 280f, UI.WithAlpha(UI.Gold, 0.7f * f));
        GachaScreen.DrawCardFace(card, def, def.IsHero && SaveData.HeroAwaken > 0 ? "각성 +" + SaveData.HeroAwaken : null, true, selected.star, selected.level);

        var info = new Rect(card.xMax + 20, 84, rx + rw - card.xMax - 20, 330);
        UI.Panel(info);
        float mul = SaveData.StatMultiplier(selected);
        UI.Text(new Rect(info.x + 20, info.y + 12, info.width - 40, 34), $"{def.name}  {FormationScreen.Stars(selected.star)}", 24, UI.TextMain, TextAnchor.MiddleLeft, true);
        UI.Text(new Rect(info.x + 20, info.y + 46, info.width - 40, 24), def.Profile, 15, new Color(1f, 0.92f, 0.7f), TextAnchor.MiddleLeft);

        // 레벨과 경험치
        bool maxLv = selected.level >= SaveData.MaxLevel;
        UI.Text(new Rect(info.x + 20, info.y + 80, 150, 24), $"Lv.{selected.level} / {SaveData.MaxLevel}", 17, UI.Gold, TextAnchor.MiddleLeft, true);
        UI.Bar(new Rect(info.x + 160, info.y + 84, info.width - 180, 16), maxLv ? 1f : selected.exp / (float)SaveData.ExpToNext(selected.level), new Color(0.4f, 0.8f, 1f));
        UI.Text(new Rect(info.x + 20, info.y + 106, info.width - 40, 20), maxLv ? "최대 레벨" : $"경험치 {selected.exp} / {SaveData.ExpToNext(selected.level)}  (전투에 나가면 올라요)", 13, UI.TextSub, TextAnchor.MiddleLeft);

        string powerName = def.job.healer ? "치유" : "공격";
        UI.Text(new Rect(info.x + 20, info.y + 134, info.width - 40, 26),
            $"체력 {def.job.hp * mul:0}    {powerName} {def.job.damage * mul:0}    사거리 {def.job.range:0.#}", 18, UI.TextMain, TextAnchor.MiddleLeft, true);
        UI.Text(new Rect(info.x + 20, info.y + 162, info.width - 40, 22),
            $"성급 +{SaveData.StarBonus * 100 * (selected.star - 1):0}%  ·  레벨 +{SaveData.LevelBonus * 100 * (selected.level - 1):0}%" +
            (def.IsHero ? $"  ·  각성 +{SaveData.AwakenBonus * 100 * SaveData.HeroAwaken:0}%" : $"  ·  호감도 +{SaveData.AffinityBonus * 100 * SaveData.AffinityOf(def):0}%"),
            14, UI.TextSub, TextAnchor.MiddleLeft);

        // 판매
        int price = SaveData.SellPrice(selected);
        if (UI.Button(new Rect(info.x + 20, info.yMax - 70, 220, 54), def.IsHero ? "판매 불가 (용사)" : $"판매  +{price:N0} 골드", UI.Red, 17, active && !def.IsHero))
            confirm = Confirm.Sell;

        // ---- 성급 강화 ----
        var mergeBox = new Rect(rx, 430, rw, 194);
        UI.Panel(mergeBox);
        UI.Text(new Rect(mergeBox.x + 20, mergeBox.y + 10, mergeBox.width - 40, 28), "성급 강화  ·  같은 캐릭터를 재료로 합쳐서 ★을 올려요", 17, UI.TextMain, TextAnchor.MiddleLeft, true);
        if (def.IsHero)
        {
            UI.Text(new Rect(mergeBox.x + 20, mergeBox.y + 50, mergeBox.width - 40, 60),
                $"용사는 뽑기에서 용사 카드가 나오면 각성해요. (현재 각성 +{SaveData.HeroAwaken} / {SaveData.MaxAwaken})", 16, UI.TextSub, TextAnchor.UpperLeft);
        }
        else if (selected.star >= SaveData.MaxStar)
        {
            UI.Text(new Rect(mergeBox.x + 20, mergeBox.y + 50, mergeBox.width - 40, 40), $"최대 성급 ({SaveData.MaxStar}성)이에요!", 18, UI.Gold, TextAnchor.MiddleLeft, true);
        }
        else
        {
            var candidates = SaveData.SortedMembers().FindAll(m => SaveData.CanMerge(selected, m));
            if (candidates.Count == 0)
                UI.Text(new Rect(mergeBox.x + 20, mergeBox.y + 50, mergeBox.width - 40, 40), $"재료로 쓸 {def.name}이(가) 없어요. 뽑기에서 한 명 더 얻어 보세요.", 16, UI.TextSub, TextAnchor.MiddleLeft);
            else
            {
                UI.Text(new Rect(mergeBox.x + 20, mergeBox.y + 42, mergeBox.width - 40, 22), "재료 선택:", 14, UI.TextSub, TextAnchor.MiddleLeft);
                float cx = mergeBox.x + 20;
                foreach (var c in candidates)
                {
                    var b = new Rect(cx, mergeBox.y + 68, 130, 46);
                    if (b.xMax > mergeBox.xMax - 260) break;
                    string label = $"{FormationScreen.Stars(c.star)} Lv.{c.level}" + (SaveData.IsPlaced(c) ? " (출진)" : "");
                    if (UI.Button(b, label, c == material ? UI.Primary : UI.Neutral, 13, active)) material = c;
                    cx += 140;
                }
            }
            var go = new Rect(mergeBox.xMax - 240, mergeBox.y + 68, 220, 54);
            if (UI.Button(go, $"★{selected.star} → ★{selected.star + 1}  강화", new Color(0.50f, 0.36f, 0.72f), 18, active && material != null))
                confirm = Confirm.Merge;
            UI.Text(new Rect(mergeBox.x + 20, mergeBox.yMax - 40, mergeBox.width - 40, 26),
                $"강화하면 성급 1마다 능력치 +{SaveData.StarBonus * 100:0}% · 재료로 쓴 동료는 사라져요", 14, UI.TextSub, TextAnchor.MiddleLeft);
        }

        if (UI.Button(new Rect(24, 640, 170, 50), "◀ 돌아가기", UI.Neutral, 18, active))
            onExit();

        // ---- 확인 창 ----
        if (confirm == Confirm.Merge && material != null)
        {
            int result = ConfirmBox($"{def.name} ★{selected.star} Lv.{selected.level}을(를) 강화할까요?\n\n재료: {def.name} ★{material.star} Lv.{material.level}  (사라져요)", "강화");
            if (result == 1 && SaveData.Merge(selected, material))
            {
                flashStart = Now;
                toast($"{def.name} {selected.star}성 달성!");
                material = null;
            }
        }
        else if (confirm == Confirm.Sell)
        {
            int result = ConfirmBox($"{def.name} ★{selected.star} Lv.{selected.level}을(를) 판매할까요?\n\n+{price:N0} 골드", "판매");
            if (result == 1 && SaveData.Sell(selected))
            {
                toast($"{def.name}을(를) 판매했어요 (+{price:N0} 골드)");
                selected = SaveData.HeroMember;
            }
        }
    }

    // 0 = 아직 고르는 중, 1 = 예, 2 = 취소
    int ConfirmBox(string message, string yes)
    {
        float w = UI.Width;
        UI.Fill(UI.Full, new Color(0f, 0f, 0f, 0.7f));
        var box = new Rect(w / 2f - 280f, 220f, 560f, 270f);
        UI.Panel(box, new Color(0.10f, 0.11f, 0.18f, 0.97f));
        UI.Text(new Rect(box.x + 30, box.y + 24, box.width - 60, 140), message, 19, UI.TextMain);
        if (UI.Button(new Rect(box.x + 50, box.y + 186, 210, 58), yes, UI.Red)) { confirm = Confirm.None; return 1; }
        if (UI.Button(new Rect(box.xMax - 260, box.y + 186, 210, 58), "취소", UI.Neutral)) { confirm = Confirm.None; return 2; }
        return 0;
    }
}

// ---------------- 도감: 인물 · 적 · 아이템 ----------------
public class DexScreen
{
    enum Tab { People, Enemies, Items }
    Tab tab;
    Vector2 scroll;

    public void Open()
    {
        tab = Tab.People;
        scroll = Vector2.zero;
    }

    public void Draw(System.Action onExit)
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.10f, 0.12f, 0.22f), new Color(0.05f, 0.05f, 0.10f));
        UI.TopBar("도감");

        // 탭
        string[] names = { "인물", "적", "아이템" };
        for (int i = 0; i < names.Length; i++)
        {
            var r = new Rect(24 + i * 150, 76, 140, 44);
            if (UI.Button(r, names[i], (int)tab == i ? UI.Primary : UI.Neutral, 18)) { tab = (Tab)i; scroll = Vector2.zero; }
        }

        var view = new Rect(24, 132, w - 40, 494);
        if (tab == Tab.People) DrawPeople(view);
        else if (tab == Tab.Enemies) DrawEnemies(view);
        else
        {
            UI.Panel(view);
            UI.Text(view, "아이템 도감은 준비 중이에요", 22, UI.TextSub);
        }

        if (UI.Button(new Rect(24, 640, 170, 50), "◀ 돌아가기", UI.Neutral, 18))
            onExit();
    }

    void DrawPeople(Rect view)
    {
        var defs = new List<CompanionDef>(CompanionDef.All);
        defs.Sort((a, b) => b.rarity.CompareTo(a.rarity));
        int owned = 0;
        foreach (var d in defs) if (SaveData.CountOwned(d) > 0) owned++;
        UI.Text(new Rect(500, 76, view.width - 480, 44), $"{owned} / {defs.Count} 종류  ·  ♥ 호감도는 전승 특전에서 올릴 수 있어요", 15, UI.TextSub, TextAnchor.MiddleRight);

        const float cw = 140f, ch = 203f, gap = 16f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((view.width - 20f + gap) / (cw + gap)));
        int rows = Mathf.CeilToInt(defs.Count / (float)perRow);
        var content = new Rect(0, 0, view.width - 20, rows * (ch + gap + 22) + 20);
        scroll = GUI.BeginScrollView(view, scroll, content);
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            var r = new Rect((i % perRow) * (cw + gap), 8 + (i / perRow) * (ch + gap + 22), cw, ch);
            int count = SaveData.CountOwned(def);
            if (count > 0)
            {
                GachaScreen.DrawCardFace(r, def, def.IsHero && SaveData.HeroAwaken > 0 ? "각성 +" + SaveData.HeroAwaken : null);
                if (count > 1) UI.Chip(new Rect(r.xMax - 46, r.yMax - 32, 40, 24), "x" + count, new Color(0f, 0f, 0f, 0.75f), 14);
            }
            else
            {
                UI.Round(r, new Color(0.13f, 0.14f, 0.19f));
                UI.RoundFrame(r, UI.Darken(RarityInfo.GetColor(def.rarity), 0.6f));
                UI.Text(r, "?", 54, new Color(1f, 1f, 1f, 0.2f), TextAnchor.MiddleCenter, true);
                UI.Text(new Rect(r.x, r.yMax - 36, r.width, 30), RarityInfo.Name(def.rarity), 15, UI.Darken(RarityInfo.GetColor(def.rarity), 0.8f));
            }
            // 호감도 (용사는 없음)
            if (!def.IsHero)
            {
                int aff = SaveData.AffinityOf(def);
                UI.Text(new Rect(r.x, r.yMax + 2, r.width, 20), aff > 0 ? "호감도 " + Portrait.Hearts(aff) : "호감도 -", 13,
                    aff > 0 ? new Color(1f, 0.5f, 0.6f) : UI.WithAlpha(UI.TextSub, 0.6f));
            }
        }
        GUI.EndScrollView();
    }

    void DrawEnemies(Rect view)
    {
        var all = EnemyDef.All;
        int seen = 0;
        foreach (var en in all) if (SaveData.SeenEnemies.Contains(en.id)) seen++;
        UI.Text(new Rect(500, 76, view.width - 480, 44), $"{seen} / {all.Count} 종류  ·  전투에서 만나면 등록돼요", 15, UI.TextSub, TextAnchor.MiddleRight);

        const float cw = 220f, ch = 120f, gap = 14f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((view.width - 20f + gap) / (cw + gap)));
        int rows = Mathf.CeilToInt(all.Count / (float)perRow);
        var content = new Rect(0, 0, view.width - 20, rows * (ch + gap) + 20);
        scroll = GUI.BeginScrollView(view, scroll, content);
        for (int i = 0; i < all.Count; i++)
        {
            var en = all[i];
            var r = new Rect((i % perRow) * (cw + gap), 8 + (i / perRow) * (ch + gap), cw, ch);
            bool known = SaveData.SeenEnemies.Contains(en.id);
            UI.Panel(r);
            Color typeColor = en.type == EnemyType.Boss ? UI.Red : en.type == EnemyType.MidBoss ? new Color(0.5f, 0.3f, 0.65f) : UI.Neutral;
            var icon = new Rect(r.x + 14, r.y + 20, 56, 56);
            if (known)
            {
                UI.Round(icon, en.color);
                UI.RoundFrame(icon, en.type == EnemyType.Normal ? new Color(1f, 1f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f));
                UI.Text(new Rect(r.x + 82, r.y + 12, r.width - 92, 26), en.name, 17, UI.TextMain, TextAnchor.MiddleLeft, true);
                UI.Text(new Rect(r.x + 82, r.y + 66, r.width - 92, 44), en.desc, 12, UI.TextSub, TextAnchor.UpperLeft);
            }
            else
            {
                UI.Round(icon, new Color(0.2f, 0.2f, 0.25f));
                UI.Text(icon, "?", 30, new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleCenter, true);
                UI.Text(new Rect(r.x + 82, r.y + 12, r.width - 92, 26), "???", 17, UI.TextSub, TextAnchor.MiddleLeft, true);
                if (en.stage >= 0)
                    UI.Text(new Rect(r.x + 82, r.y + 66, r.width - 92, 44), $"스테이지 {en.stage + 1}", 12, UI.TextSub, TextAnchor.UpperLeft);
            }
            UI.Chip(new Rect(r.x + 82, r.y + 40, 96, 22), en.TypeName, typeColor, 12);
        }
        GUI.EndScrollView();
    }
}

// ---------------- 전승 특전: 호감도 올리기 ----------------
public class LegacyScreen
{
    Vector2 scroll;
    bool confirmNewCycle;

    public void Open()
    {
        scroll = Vector2.zero;
        confirmNewCycle = false;
    }

    // onNewCycle: '다음 회차 시작'을 확정했을 때
    public void Draw(System.Action onExit, System.Action onNewCycle, System.Action<string> toast)
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.22f, 0.10f, 0.20f), new Color(0.08f, 0.05f, 0.10f));
        UI.TopBar($"전승 특전   {SaveData.Cycle}회차 클리어", false);
        bool active = !confirmNewCycle;

        UI.Text(new Rect(24, 74, w - 48, 50),
            "이번 회차에서 캐릭터가 도달한 레벨 10마다 호감도를 1 올릴 수 있어요 (30레벨이면 3).\n호감도는 다음 회차에도 이어지고, 1마다 능력치 +5%가 돼요. (용사는 호감도가 없어요)",
            15, UI.TextSub, TextAnchor.MiddleLeft);

        var chars = SaveData.OwnedCharacters().FindAll(d => !d.IsHero);
        var view = new Rect(24, 132, w - 48, 490);
        const float rowH = 70f, gap = 8f;
        var content = new Rect(0, 0, view.width - 20, chars.Count * (rowH + gap) + 10);
        scroll = GUI.BeginScrollView(view, scroll, content);
        for (int i = 0; i < chars.Count; i++)
        {
            var def = chars[i];
            var r = new Rect(0, i * (rowH + gap), content.width, rowH);
            UI.Panel(r);
            Portrait.Draw(new Vector2(r.x + 40, r.center.y), def, 24f, false);
            UI.Text(new Rect(r.x + 76, r.y + 8, 260, 28), def.name, 19, UI.TextMain, TextAnchor.MiddleLeft, true);
            UI.Text(new Rect(r.x + 76, r.y + 38, 260, 24), $"이번 회차 최고 Lv.{SaveData.MaxLevelOf(def)}", 14, UI.TextSub, TextAnchor.MiddleLeft);
            int aff = SaveData.AffinityOf(def);
            UI.Text(new Rect(r.x + 340, r.y, 240, rowH), aff > 0 ? "호감도 " + Portrait.Hearts(aff) : "호감도 0", 18,
                aff > 0 ? new Color(1f, 0.5f, 0.6f) : UI.TextSub, TextAnchor.MiddleLeft, true);
            int points = SaveData.LegacyPoints(def);
            UI.Text(new Rect(r.xMax - 360, r.y, 160, rowH), $"남은 포인트 {points}", 16, points > 0 ? UI.Gold : UI.TextSub, TextAnchor.MiddleRight, true);
            if (UI.Button(new Rect(r.xMax - 180, r.y + 13, 164, 44), "호감도 +1", new Color(0.8f, 0.35f, 0.5f), 17, active && points > 0)
                && SaveData.RaiseAffinity(def))
                toast($"{def.name}의 호감도가 올랐어요! ({Portrait.Hearts(SaveData.AffinityOf(def))})");
        }
        GUI.EndScrollView();
        if (chars.Count == 0) UI.Text(view, "동료가 없어요", 20, UI.TextSub);

        if (UI.Button(new Rect(24, 640, 170, 50), "◀ 돌아가기", UI.Neutral, 18, active))
            onExit();
        if (UI.Button(new Rect(w - 324, 636, 300, 56), $"{SaveData.Cycle + 1}회차 시작하기 ▶", UI.Primary, 20, active))
            confirmNewCycle = true;

        if (confirmNewCycle)
        {
            UI.Fill(UI.Full, new Color(0f, 0f, 0f, 0.7f));
            var box = new Rect(w / 2f - 290f, 210f, 580f, 290f);
            UI.Panel(box, new Color(0.10f, 0.11f, 0.18f, 0.97f));
            UI.Text(new Rect(box.x + 30, box.y + 24, box.width - 60, 160),
                $"{SaveData.Cycle + 1}회차를 시작할까요?\n\n동료 · 골드 · 스테이지 진행은 처음부터 다시 시작하고,\n호감도와 적 도감만 이어져요.\n(남은 전승 포인트는 사라져요)", 18, UI.TextMain);
            if (UI.Button(new Rect(box.x + 50, box.y + 206, 220, 58), "시작하기", UI.Primary)) { confirmNewCycle = false; onNewCycle(); }
            if (UI.Button(new Rect(box.xMax - 270, box.y + 206, 220, 58), "취소", UI.Neutral)) confirmNewCycle = false;
        }
    }
}
