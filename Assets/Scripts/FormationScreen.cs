using System.Collections.Generic;
using UnityEngine;

// 편성 화면: 선두 ~ 후미 5개의 줄에 동료를 끌어다 원하는 위치에 놓습니다.
// - 목록 → 줄: 그 위치에 배치 (한 줄 최대 5명)
// - 줄 안이나 다른 줄로 끌기: 위치 옮기기
// - 줄 → 목록: 편성에서 빼기 (용사는 뺄 수 없음)
public class FormationScreen
{
    const float ZoneW = 104f;
    const float ZoneH = 470f;
    const float ZoneGap = 8f;
    const float GridX = 40f;
    const float GridY = 110f;
    const float Pad = 30f;          // 줄 가장자리 여백
    const float TokenRadius = 26f;

    string dragValue;               // 끌고 있는 것: "H" 또는 Roster 번호 (없으면 null)
    bool dragFromParty;
    string inspectValue;            // 정보 창에 보여 줄 동료
    float listScroll;

    public void Open()
    {
        dragValue = null;
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
        UI.Gradient(UI.Full, new Color(0.12f, 0.18f, 0.16f), new Color(0.05f, 0.08f, 0.08f));
        UI.TopBar($"편성   파티 {SaveData.Party.Count}명", false);

        // ---- 줄 (선두 ~ 후미) ----
        int hoverZone = -1;
        for (int col = 0; col < SaveData.Columns; col++)
        {
            var z = ZoneRect(col);
            bool hover = z.Contains(mouse);
            if (hover) hoverZone = col;
            int count = SaveData.CountInColumn(col);
            UI.Round(z, hover && dragValue != null ? new Color(1f, 1f, 1f, 0.16f) : new Color(0f, 0f, 0f, col % 2 == 0 ? 0.35f : 0.25f));
            UI.RoundFrame(z, hover && dragValue != null ? UI.WithAlpha(UI.Gold, 0.6f) : new Color(1f, 1f, 1f, 0.10f));
            UI.Text(new Rect(z.x, GridY - 32, ZoneW, 28), $"{SaveData.ColumnNames[col]} {count}/{SaveData.MaxPerColumn}", 17,
                col == 0 ? new Color(1f, 0.6f, 0.5f) : Color.white, TextAnchor.MiddleCenter, true);
        }
        UI.Text(new Rect(GridX, GridY + ZoneH + 4, 5 * (ZoneW + ZoneGap), 24), "적이 오는 방향 →", 16,
            new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleRight);

        // 배치된 동료
        int hoverToken = -1;
        for (int i = 0; i < SaveData.Party.Count; i++)
        {
            var p = SaveData.Party[i];
            if (dragFromParty && p.value == dragValue) continue;
            var center = TokenCenter(p);
            if (Vector2.Distance(center, mouse) <= TokenRadius + 2f) hoverToken = i;
            DrawToken(center, SaveData.DefOf(p.value), TokenRadius);
        }

        // ---- 대기 중인 동료 목록 (같은 동료끼리 묶어서 표시) ----
        float listX = GridX + 5 * (ZoneW + ZoneGap) + 22f;
        var listRect = new Rect(listX, 80, w - listX - 30, 400);
        UI.Panel(listRect);
        UI.Text(new Rect(listRect.x + 12, listRect.y + 6, listRect.width - 24, 28), "대기 중인 동료 (끌어서 줄에 놓기)", 18, Color.white, TextAnchor.MiddleLeft, true);

        var groups = new List<KeyValuePair<CompanionDef, List<int>>>();
        for (int i = 0; i < SaveData.Roster.Count; i++)
        {
            if (SaveData.IsPlaced(i)) continue;
            var def = CompanionDef.Find(SaveData.Roster[i]);
            var g = groups.Find(x => x.Key == def);
            if (g.Key == null) groups.Add(new KeyValuePair<CompanionDef, List<int>>(def, new List<int> { i }));
            else g.Value.Add(i);
        }
        groups.Sort((a, b) => b.Key.rarity.CompareTo(a.Key.rarity));

        const float itemW = 96f, itemH = 112f, itemGap = 8f;
        var inner = new Rect(listRect.x + 10, listRect.y + 40, listRect.width - 20, listRect.height - 50);
        int perRow = Mathf.Max(1, Mathf.FloorToInt((inner.width + itemGap) / (itemW + itemGap)));
        int rowsTotal = Mathf.CeilToInt(groups.Count / (float)perRow);
        float maxScroll = Mathf.Max(0f, rowsTotal * (itemH + itemGap) - inner.height);
        if (e.type == EventType.ScrollWheel && inner.Contains(mouse))
        {
            listScroll += e.delta.y * 20f;
            e.Use();
        }
        listScroll = Mathf.Clamp(listScroll, 0f, maxScroll);

        int hoverGroup = -1;
        GUI.BeginGroup(inner);
        for (int i = 0; i < groups.Count; i++)
        {
            var r = new Rect((i % perRow) * (itemW + itemGap), (i / perRow) * (itemH + itemGap) - listScroll, itemW, itemH);
            if (r.yMax < 0 || r.y > inner.height) continue;
            var screenRect = new Rect(r.x + inner.x, r.y + inner.y, r.width, r.height);
            if (screenRect.Contains(mouse) && inner.Contains(mouse)) hoverGroup = i;
            DrawMiniUnit(r, groups[i].Key);
            if (groups[i].Value.Count > 1)
            {
                UI.Chip(new Rect(r.xMax - 36, r.y + 4, 32, 22), "x" + groups[i].Value.Count, new Color(0f, 0f, 0f, 0.75f), 13);
            }
        }
        GUI.EndGroup();
        if (groups.Count == 0)
            UI.Text(inner, SaveData.Roster.Count == 0 ? "아직 동료가 없어요.\n소환에서 뽑아 보세요!" : "모든 동료가 배치되었어요", 18, new Color(1f, 1f, 1f, 0.6f));
        if (maxScroll > 0f)
            UI.Text(new Rect(listRect.x, listRect.yMax - 24, listRect.width - 12, 22), "마우스 휠로 스크롤", 13, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleRight);

        // ---- 정보 창 ----
        var infoRect = new Rect(listX, 492, w - listX - 30, 128);
        UI.Panel(infoRect);
        var info = SaveData.DefOf(inspectValue);
        if (info != null)
        {
            float m = RarityInfo.StatMultiplier(info.rarity) * (info.IsHero ? 1f + 0.1f * SaveData.HeroAwaken : 1f);
            string title = info.IsHero ? $"{info.name}  (각성 +{SaveData.HeroAwaken})" : info.name;
            UI.Text(new Rect(infoRect.x + 15, infoRect.y + 6, infoRect.width - 30, 30), $"{title}   [{RarityInfo.Name(info.rarity)}]", 22,
                RarityInfo.Animated(info.rarity), TextAnchor.MiddleLeft, true);
            UI.Text(new Rect(infoRect.x + 15, infoRect.y + 36, infoRect.width - 30, 24), info.Profile, 17, new Color(1f, 0.92f, 0.7f), TextAnchor.MiddleLeft);
            string power = info.job.healer ? $"치유 {info.job.damage * m:0}" : $"공격 {info.job.damage * m:0}";
            UI.Text(new Rect(infoRect.x + 15, infoRect.y + 62, infoRect.width - 30, 24),
                $"{info.job.role}   체력 {info.job.hp * m:0}   {power}   사거리 {info.job.range:0.#}", 16, Color.white, TextAnchor.MiddleLeft);
            UI.Text(new Rect(infoRect.x + 15, infoRect.y + 90, infoRect.width - 30, 26), info.desc, 14, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleLeft);
        }
        else
        {
            UI.Text(infoRect, "동료를 누르면 정보가 나와요\n앞줄일수록 먼저 적과 부딪혀요", 17, new Color(1f, 1f, 1f, 0.6f));
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
        UI.Text(new Rect(listX, 630, w - listX - 30, 60), "용사는 항상 파티에 있어야 해요 (위치만 옮길 수 있어요)", 15, new Color(1f, 0.85f, 0.3f));

        // ---- 끌어서 놓기 ----
        if (e.type == EventType.MouseDown && e.button == 0 && dragValue == null)
        {
            if (hoverToken >= 0)
            {
                dragValue = SaveData.Party[hoverToken].value;
                dragFromParty = true;
                inspectValue = dragValue;
                e.Use();
            }
            else if (hoverGroup >= 0)
            {
                dragValue = groups[hoverGroup].Value[0].ToString();
                dragFromParty = false;
                inspectValue = dragValue;
                e.Use();
            }
        }
        else if (e.type == EventType.MouseUp && dragValue != null)
        {
            if (hoverZone >= 0)
            {
                ToPlacement(ZoneRect(hoverZone), mouse, out float fx, out float fy);
                if (!SaveData.Place(dragValue, hoverZone, fx, fy))
                    toast($"{SaveData.ColumnNames[hoverZone]}은(는) 꽉 찼어요 (최대 {SaveData.MaxPerColumn}명)");
            }
            else if (dragFromParty && listRect.Contains(mouse))
            {
                if (dragValue == SaveData.HeroMark) toast("용사는 뺄 수 없어요");
                else SaveData.Remove(dragValue);
            }
            dragValue = null;
            e.Use();
        }

        // 끌고 있는 동료를 마우스 위치에 그림
        if (dragValue != null) DrawToken(mouse, SaveData.DefOf(dragValue), TokenRadius + 4f);
    }

    // 줄에 놓인 동료 (동그라미)
    static void DrawToken(Vector2 center, CompanionDef def, float radius)
    {
        if (def == null) return;
        Color rc = RarityInfo.Animated(def.rarity);
        if (def.IsHero) UI.Glow(center, radius * 1.8f, new Color(1f, 0.85f, 0.3f, 0.5f));
        UI.Circle(center, radius + 4f, rc);
        UI.Circle(center, radius, def.job.color);
        UI.Text(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), def.job.letter, Mathf.RoundToInt(radius * 0.9f),
            Color.white, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(center.x - 50, center.y + radius + 2, 100, 18), def.name, 12, Color.white, TextAnchor.MiddleCenter, true);
    }

    // 목록의 작은 동료 칸
    static void DrawMiniUnit(Rect r, CompanionDef def)
    {
        if (def == null) return;
        Color rc = RarityInfo.Animated(def.rarity);
        UI.Round(r, UI.Darken(rc, 0.35f));
        UI.RoundFrame(r, rc);
        var c = new Vector2(r.center.x, r.y + r.height * 0.4f);
        float rad = r.width * 0.26f;
        UI.Circle(c, rad + 3f, rc);
        UI.Circle(c, rad, def.job.color);
        UI.Text(new Rect(c.x - rad, c.y - rad, rad * 2, rad * 2), def.job.letter, Mathf.RoundToInt(rad * 1.1f), Color.white, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(r.x + 2, r.yMax - 28, r.width - 4, 24), def.name, 13, Color.white, TextAnchor.MiddleCenter, true);
    }
}
