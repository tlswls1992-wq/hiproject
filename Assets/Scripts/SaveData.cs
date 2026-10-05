using System.Collections.Generic;
using UnityEngine;

// 진행 상황(골드, 클리어한 판, 동료 목록, 편성, 용사 각성)을 컴퓨터에 저장합니다.
public static class SaveData
{
    const string Prefix = "GachaHero.";

    // ---- 편성 칸: 5줄(선두~후미) x 5칸 ----
    public const int Columns = 5;
    public const int Rows = 5;
    public const int CellCount = Columns * Rows;
    public static readonly string[] ColumnNames = { "선두", "전열", "중열", "후열", "후미" }; // 0 = 맨 앞
    public const string HeroMark = "H";   // 편성 칸에 용사가 있다는 표시
    public const int MaxAwaken = 10;      // 용사 최대 각성 단계

    public static int Gold;
    public static int ClearedStage;                                  // 클리어한 가장 먼 판 번호 (0~100)
    public static int HeroAwaken;                                    // 용사 각성 단계 (용사를 뽑을 때마다 +1)
    public static readonly List<string> Roster = new List<string>(); // 가진 동료 id (중복 = 같은 동료 여러 명, 용사 제외)
    // 편성: 칸 번호 = 줄 * 5 + 칸. 값은 "" (비어 있음), "H" (용사), 또는 Roster 번호
    public static readonly string[] Formation = new string[CellCount];

    public static bool HasSave => PlayerPrefs.GetInt(Prefix + "Started", 0) == 1;

    public static int CellIndex(int column, int row) => column * Rows + row;

    public static void Load()
    {
        Gold = PlayerPrefs.GetInt(Prefix + "Gold", 0);
        ClearedStage = PlayerPrefs.GetInt(Prefix + "ClearedStage", 0);
        HeroAwaken = PlayerPrefs.GetInt(Prefix + "HeroAwaken", 0);
        Roster.Clear();
        foreach (var id in PlayerPrefs.GetString(Prefix + "Roster", "").Split(','))
        {
            var def = CompanionDef.Find(id);
            if (def != null && !def.IsHero) Roster.Add(id);
        }

        ClearFormation();
        var cells = PlayerPrefs.GetString(Prefix + "Formation", "").Split(',');
        if (cells.Length == CellCount)
        {
            for (int i = 0; i < CellCount; i++)
                if (cells[i] == HeroMark || RosterIndexOf(cells[i]) >= 0) Formation[i] = cells[i];
            EnsureHeroPlaced();
        }
        else
        {
            AutoArrange(); // 예전 저장 파일이면 자동으로 배치
        }
    }

    public static void Save()
    {
        PlayerPrefs.SetInt(Prefix + "Started", 1);
        PlayerPrefs.SetInt(Prefix + "Gold", Gold);
        PlayerPrefs.SetInt(Prefix + "ClearedStage", ClearedStage);
        PlayerPrefs.SetInt(Prefix + "HeroAwaken", HeroAwaken);
        PlayerPrefs.SetString(Prefix + "Roster", string.Join(",", Roster));
        PlayerPrefs.SetString(Prefix + "Formation", string.Join(",", Formation));
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        Gold = 0;
        ClearedStage = 0;
        HeroAwaken = 0;
        Roster.Clear();
        ClearFormation();
        EnsureHeroPlaced();
        foreach (var key in new[] { "Started", "Gold", "ClearedStage", "HeroAwaken", "Roster", "Formation" })
            PlayerPrefs.DeleteKey(Prefix + key);
        PlayerPrefs.Save();
    }

    // 뽑기 결과를 반영합니다. 카드에 붙일 표시("NEW", "각성 +3" 등)를 돌려줍니다.
    public static string AddPulled(CompanionDef def)
    {
        if (def.IsHero)
        {
            if (HeroAwaken < MaxAwaken)
            {
                HeroAwaken++;
                return "각성 +" + HeroAwaken;
            }
            Gold += 300; // 최대 각성이면 골드로 돌려받음
            return "골드 +300";
        }
        bool isNew = !Roster.Contains(def.id);
        Roster.Add(def.id);
        return isNew ? "NEW" : null;
    }

    public static int CountOwned(CompanionDef def)
    {
        if (def.IsHero) return 1;
        int n = 0;
        foreach (var id in Roster) if (id == def.id) n++;
        return n;
    }

    // ---------------- 편성 ----------------

    static int RosterIndexOf(string cell)
    {
        if (int.TryParse(cell, out int idx) && idx >= 0 && idx < Roster.Count) return idx;
        return -1;
    }

    // 칸에 있는 동료 (비어 있으면 null)
    public static CompanionDef DefAt(int cell)
    {
        string v = Formation[cell];
        if (v == HeroMark) return CompanionDef.Hero;
        int idx = RosterIndexOf(v);
        return idx >= 0 ? CompanionDef.Find(Roster[idx]) : null;
    }

    public static bool IsPlaced(int rosterIndex)
    {
        string key = rosterIndex.ToString();
        foreach (var v in Formation) if (v == key) return true;
        return false;
    }

    public static int PlacedCount()
    {
        int n = 0;
        foreach (var v in Formation) if (!string.IsNullOrEmpty(v)) n++;
        return n;
    }

    public static void ClearFormation()
    {
        for (int i = 0; i < CellCount; i++) Formation[i] = "";
    }

    // 용사는 항상 파티에 있어야 합니다. 없으면 선두 가운데(또는 빈 칸)에 놓습니다.
    public static void EnsureHeroPlaced()
    {
        foreach (var v in Formation) if (v == HeroMark) return;
        int center = CellIndex(0, Rows / 2);
        if (string.IsNullOrEmpty(Formation[center])) { Formation[center] = HeroMark; return; }
        for (int i = 0; i < CellCount; i++)
            if (string.IsNullOrEmpty(Formation[i])) { Formation[i] = HeroMark; return; }
        Formation[center] = HeroMark; // 꽉 차 있으면 한 명을 빼고 넣음
    }

    // 직업별로 알맞은 줄에 등급 높은 동료부터 자동으로 배치합니다.
    public static void AutoArrange()
    {
        ClearFormation();
        Formation[CellIndex(0, Rows / 2)] = HeroMark;

        var order = new List<int>();
        for (int i = 0; i < Roster.Count; i++) order.Add(i);
        order.Sort((a, b) => CompanionDef.Find(Roster[b]).rarity.CompareTo(CompanionDef.Find(Roster[a]).rarity));

        int[] rowOrder = { 2, 1, 3, 0, 4 }; // 가운데부터 채움
        foreach (int idx in order)
        {
            var job = CompanionDef.Find(Roster[idx]).job;
            int[] columns =
                job == HeroClass.Warrior ? new[] { 0, 1, 2, 3, 4 } :
                job == HeroClass.Archer ? new[] { 2, 1, 3, 4, 0 } :
                job == HeroClass.Mage ? new[] { 3, 2, 4, 1, 0 } :
                new[] { 4, 3, 2, 1, 0 }; // 사제
            bool placed = false;
            foreach (int col in columns)
            {
                foreach (int row in rowOrder)
                {
                    int cell = CellIndex(col, row);
                    if (!string.IsNullOrEmpty(Formation[cell])) continue;
                    Formation[cell] = idx.ToString();
                    placed = true;
                    break;
                }
                if (placed) break;
            }
            if (!placed) break; // 칸이 다 찼음
        }
    }
}
