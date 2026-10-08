using System.Collections.Generic;
using UnityEngine;

// 편성 화면: 선두 ~ 후미 5개의 줄에 동료를 끌어다 원하는 위치에 놓습니다.
// - 목록 → 줄: 그 위치에 배치 (한 줄 최대 5명, 출진 가능 인원까지)
// - 줄 안이나 다른 줄로 끌기: 위치 옮기기
// - 줄 → 목록: 편성에서 빼기 (용사는 뺄 수 없음)
// - 이름이 있는 캐릭터는 같은 인물을 두 명 배치할 수 없어요
public class FormationScreen
{
    const float ZoneW = 104f;
    const float ZoneH = 470f;
    const float ZoneGap = 8f;
    const float GridX = 40f;
    const float GridY = 110f;
    const float Pad = 30f;          // 줄 가장자리 여백
    const float TokenRadius = 26f;

    SaveData.Member dragging;       // 끌고 있는 동료 (없으면 null)
    bool dragFromParty;
    SaveData.Member inspect;        // 정보 창에 보여 줄 동료
    float listScroll;

    public void Open()
    {
        dragging = null;
        inspect = null;
        listScroll = 0f;
    }

    // 화면에서 왼쪽이 후미, 오른쪽(적이 오는 쪽)이 선두입니다.
    static Rect ZoneRect(int column)
    {
        int screenCol = SaveData.Columns - 1 - column;
        return new Rect(GridX + screenCol * (ZoneW + ZoneGap), GridY, ZoneW, ZoneH);
    }

    // fx 0 = 줄의 앞쪽(오른쪽), fy 0 = 위쪽
    static Vector2 TokenCenter(SaveData.Placement p)
    {
        var z = ZoneRect(p.column);
        return new Vector2(z.x + Pad + (1f - p.fx) * (z.width - Pad * 2f), z.y + Pad + p.fy * (z.height - Pad * 2f));
    }

    static void ToPlacement(Rect z, Vector2 mouse, out float fx, out float fy)
    {
        fx = Mathf.Clamp01(1f - (mouse.x - z.x - Pad) / (z.width - Pad * 2f));
        fy = Mathf.Clamp01((mouse.y - z.y - Pad) / (z.height - Pad * 2f));
    }

    // onExit: 나가기 버튼, toast: 짧은 안내 문구 띄우기
    public void Draw(System.Action onExit, System.Action<string> toast)
    {
        float w = UI.Width;
        var e = Event.current;
        Vector2 mouse = e.mousePosition;
        UI.Backdrop("menu");
        UI.TopBar($"편성   출진 {SaveData.Party.Count} / {SaveData.DeployCap}명", false);

        // ---- 줄 (선두 ~ 후미) ----
        int hoverZone = -1;
        for (int col = 0; col < SaveData.Columns; col++)
        {
            var z = ZoneRect(col);
            bool hover = z.Contains(mouse);
            if (hover) hoverZone = col;
            int count = SaveData.CountInColumn(col);
            UI.Round(z, hover && dragging != null ? new Color(1f, 1f, 1f, 0.16f) : new Color(0f, 0f, 0f, col % 2 == 0 ? 0.35f : 0.25f));
            UI.RoundFrame(z, hover && dragging != null ? UI.WithAlpha(UI.Gold, 0.6f) : new Color(1f, 1f, 1f, 0.10f));
            UI.Text(new Rect(z.x, GridY - 32, ZoneW, 28), $"{SaveData.ColumnNames[col]} {count}/{SaveData.MaxPerColumn}", 17,
                col == 0 ? new Color(1f, 0.6f, 0.5f) : Color.white, TextAnchor.MiddleCenter, true);
        }
        UI.Text(new Rect(GridX, GridY + ZoneH + 4, 5 * (ZoneW + ZoneGap), 24), "적이 오는 방향 →", 16,
            new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleRight);

        // 배치된 동료
        SaveData.Member hoverToken = null;
        foreach (var p in SaveData.Party)
        {
            var m = SaveData.MemberByUid(p.uid);
            if (m == null || (dragFromParty && m == dragging)) continue;
            var center = TokenCenter(p);
            if (Vector2.Distance(center, mouse) <= TokenRadius + 2f) hoverToken = m;
            DrawToken(center, m, TokenRadius);
        }

        // ---- 대기 중인 동료 목록 ----
        float listX = GridX + 5 * (ZoneW + ZoneGap) + 22f;
        var listRect = new Rect(listX, 80, w - listX - 30, 400);
        UI.Panel(listRect);
        UI.Text(new Rect(listRect.x + 14, listRect.y + 6, listRect.width - 28, 28), "대기 중인 동료 (끌어서 줄에 놓기)", 17, UI.TextMain, TextAnchor.MiddleLeft, true);

        var waiting = SaveData.SortedMembers().FindAll(m => !SaveData.IsPlaced(m));

        const float itemW = 96f, itemH = 112f, itemGap = 8f;
        var inner = new Rect(listRect.x + 10, listRect.y + 40, listRect.width - 20, listRect.height - 50);
        int perRow = Mathf.Max(1, Mathf.FloorToInt((inner.width + itemGap) / (itemW + itemGap)));
        int rowsTotal = Mathf.CeilToInt(waiting.Count / (float)perRow);
        float maxScroll = Mathf.Max(0f, rowsTotal * (itemH + itemGap) - inner.height);
        if (e.type == EventType.ScrollWheel && inner.Contains(mouse))
        {
            listScroll += e.delta.y * 20f;
            e.Use();
        }
        listScroll = Mathf.Clamp(listScroll, 0f, maxScroll);

        SaveData.Member hoverItem = null;
        GUI.BeginGroup(inner);
        for (int i = 0; i < waiting.Count; i++)
        {
            var r = new Rect((i % perRow) * (itemW + itemGap), (i / perRow) * (itemH + itemGap) - listScroll, itemW, itemH);
            if (r.yMax < 0 || r.y > inner.height) continue;
            var screenRect = new Rect(r.x + inner.x, r.y + inner.y, r.width, r.height);
            if (screenRect.Contains(mouse) && inner.Contains(mouse)) hoverItem = waiting[i];
            DrawMiniUnit(r, waiting[i]);
        }
        GUI.EndGroup();
        if (waiting.Count == 0)
            UI.Text(inner, SaveData.CompanionCount == 0 ? "아직 동료가 없어요.\n소환에서 뽑아 보세요!" : "모든 동료가 배치되었어요", 18, UI.TextSub);
        if (maxScroll > 0f)
            UI.Text(new Rect(listRect.x, listRect.yMax - 24, listRect.width - 14, 22), "마우스 휠로 스크롤", 13, UI.TextSub, TextAnchor.MiddleRight);

        // ---- 정보 창 ----
        var infoRect = new Rect(listX, 492, w - listX - 30, 128);
        UI.Panel(infoRect);
        if (inspect != null && SaveData.Roster.Contains(inspect))
        {
            var info = inspect.Def;
            float mul = SaveData.StatMultiplier(inspect);
            UI.Text(new Rect(infoRect.x + 16, infoRect.y + 6, infoRect.width - 32, 30),
                $"{info.name}  {Stars(inspect.star)}  Lv.{inspect.level}   [{RarityInfo.Name(info.rarity)}]", 20, RarityInfo.Animated(info.rarity), TextAnchor.MiddleLeft, true);
            UI.Text(new Rect(infoRect.x + 16, infoRect.y + 36, infoRect.width - 32, 24), info.Profile, 16, new Color(1f, 0.92f, 0.7f), TextAnchor.MiddleLeft);
            string power = info.job.healer ? $"치유 {info.job.damage * mul:0}" : $"공격 {info.job.damage * mul:0}";
            UI.Text(new Rect(infoRect.x + 16, infoRect.y + 62, infoRect.width - 32, 24),
                $"{info.job.role}   체력 {info.job.hp * mul:0}   {power}   사거리 {info.job.range:0.#}", 15, UI.TextMain, TextAnchor.MiddleLeft);
            UI.Text(new Rect(infoRect.x + 16, infoRect.y + 90, infoRect.width - 32, 26),
                info.IsNamed ? "이름이 있는 인물 · 파티에 한 명만 배치할 수 있어요" : info.desc, 14, UI.TextSub, TextAnchor.MiddleLeft);
        }
        else
        {
            UI.Text(infoRect, "동료를 누르면 정보가 나와요\n앞줄일수록 먼저 적과 부딪혀요", 16, UI.TextSub);
        }

        // ---- 버튼 ----
        float by = GridY + ZoneH + 36;
        if (UI.Button(new Rect(GridX, by, 170, 54), "◀ 저장 후 나가기", UI.Neutral, 18))
        {
            SaveData.Save();
            onExit();
        }
        if (UI.Button(new Rect(GridX + 185, by, 150, 54), "자동 배치", UI.Green, 19))
            SaveData.AutoArrange();
        if (UI.Button(new Rect(GridX + 350, by, 150, 54), "모두 빼기", UI.Red, 19))
            SaveData.ClearParty();
        UI.Text(new Rect(listX, 630, w - listX - 30, 60),
            $"출진 가능 인원 {SaveData.DeployCap}명 (스테이지를 클리어할 때마다 +1) · 용사는 항상 출진", 14, UI.Gold);

        // ---- 끌어서 놓기 ----
        if (e.type == EventType.MouseDown && e.button == 0 && dragging == null)
        {
            if (hoverToken != null || hoverItem != null)
            {
                dragging = hoverToken ?? hoverItem;
                dragFromParty = hoverToken != null;
                inspect = dragging;
                e.Use();
            }
        }
        else if (e.type == EventType.MouseUp && dragging != null)
        {
            if (hoverZone >= 0)
            {
                ToPlacement(ZoneRect(hoverZone), mouse, out float fx, out float fy);
                string error = SaveData.Place(dragging, hoverZone, fx, fy);
                if (error != null) toast(error);
            }
            else if (dragFromParty && listRect.Contains(mouse))
            {
                if (dragging.Def.IsHero) toast("용사는 뺄 수 없어요");
                else SaveData.Remove(dragging);
            }
            dragging = null;
            e.Use();
        }

        // 끌고 있는 동료를 마우스 위치에 그림
        if (dragging != null) DrawToken(mouse, dragging, TokenRadius + 4f);
    }

    // 성급을 글자로 쓸 때 (버튼 · 확인 창 등): "★12". 화면의 별 그림은 StarIcons.Draw로 그림
    public static int StarTier(int star) => Mathf.Clamp((star - 1) / SaveData.StarsPerTier, 0, 2);
    public static string Stars(int star) => "★" + star;
    public static Color StarColor(int star) => StarIcons.FallbackColor(StarTier(star)); // 노랑 · 파랑 · 빨강

    // 줄에 놓인 동료 (동그라미)
    static void DrawToken(Vector2 center, SaveData.Member m, float radius)
    {
        var def = m.Def;
        Color rc = RarityInfo.Animated(def.rarity);
        if (def.IsHero) UI.Glow(center, radius * 1.8f, new Color(1f, 0.85f, 0.3f, 0.5f));
        var art = CharacterArt.For(def.id);
        if (art != null && art.idle.Length > 0)
        {
            // 대기 그림: 발밑에 등급 색 원, 발이 동그라미 아래쪽에 오도록
            UI.Glow(center + new Vector2(0, radius * 0.7f), radius * 1.2f, UI.WithAlpha(rc, 0.7f));
            art.DrawStanding(new Vector2(center.x, center.y + radius), radius * 3.6f, Time.unscaledTime + center.x * 0.37f, false);
        }
        else
        {
            UI.Circle(center, radius + 4f, rc);
            UI.Circle(center, radius, def.job.color);
            UI.Text(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), def.job.letter, Mathf.RoundToInt(radius * 0.9f),
                Color.white, TextAnchor.MiddleCenter, true);
        }
        UI.Text(new Rect(center.x - 50, center.y + radius + 2, 100, 18), def.name, 12, Color.white, TextAnchor.MiddleCenter, true);
        if (m.star > 1) StarIcons.Draw(new Rect(center.x - 40, center.y - radius - 18, 80, 15), m.star, TextAnchor.MiddleCenter);
    }

    // 목록의 작은 동료 칸
    static void DrawMiniUnit(Rect r, SaveData.Member m)
    {
        var def = m.Def;
        Color rc = RarityInfo.Animated(def.rarity);
        UI.Round(r, UI.Darken(rc, 0.35f));
        UI.RoundFrame(r, rc);
        StarIcons.Draw(new Rect(r.x + 5, r.y + 4, r.width - 12, 15), m.star);
        UI.Text(new Rect(r.x + 6, r.y + 4, r.width - 12, 18), "Lv." + m.level, 12, UI.TextMain, TextAnchor.MiddleRight, true);
        var c = new Vector2(r.center.x, r.y + r.height * 0.45f);
        float rad = r.width * 0.24f;
        var art = CharacterArt.For(def.id);
        if (art != null && art.portrait != null)
        {
            var face = new Rect(r.x + 10, r.y + 22, r.width - 20, r.height - 50);
            CharacterArt.DrawTexture(face, art.portrait, true);
            UI.RoundFrame(face, rc);
        }
        else
        {
            UI.Circle(c, rad + 3f, rc);
            UI.Circle(c, rad, def.job.color);
            UI.Text(new Rect(c.x - rad, c.y - rad, rad * 2, rad * 2), def.job.letter, Mathf.RoundToInt(rad * 1.1f), Color.white, TextAnchor.MiddleCenter, true);
        }
        UI.Text(new Rect(r.x + 2, r.yMax - 26, r.width - 4, 22), def.name, 13, Color.white, TextAnchor.MiddleCenter, true);
    }
}
