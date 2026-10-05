using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 진행 상황(골드, 클리어한 판, 동료 목록, 편성, 용사 각성)을 컴퓨터에 저장합니다.
// 저장 칸: 0번 = 자동 저장('이어하기'), 1~3번 = 직접 저장하는 슬롯('불러오기')
public static class SaveData
{
    const string Prefix = "GachaHero.";
    public const int SlotCount = 4; // 자동 저장 1개 + 슬롯 3개

    // ---- 편성: 선두 ~ 후미 5개의 줄. 줄 안에서는 자유롭게 위치를 정합니다 ----
    public const int Columns = 5;
    public const int MaxPerColumn = 5;   // 한 줄에 놓을 수 있는 최대 인원
    public static readonly string[] ColumnNames = { "선두", "전열", "중열", "후열", "후미" }; // 0 = 맨 앞
    public const string HeroMark = "H";  // 편성에서 용사를 나타내는 표시
    public const int MaxAwaken = 10;     // 용사 최대 각성 단계
    public const int MaxLevel = 20;      // 강화 최대 레벨
    public const float LevelBonus = 0.08f; // 강화 1레벨마다 능력치 +8%
    const int DuplicateRefund = 100;     // 예전 저장의 중복 캐릭터를 정리할 때 돌려주는 골드

    // 편성된 동료 한 명의 자리
    public class Placement
    {
        public string value;  // "H"(용사) 또는 Roster 번호
        public int column;    // 0 = 선두 ... 4 = 후미
        public float fx, fy;  // 줄 안에서의 위치 (0~1). fx: 0 = 앞쪽, fy: 0 = 위쪽
    }

    public static int Gold;
    public static int ClearedStage;                                  // 클리어한 가장 먼 판 번호 (0~100)
    public static int HeroAwaken;                                    // 용사 각성 단계 (용사를 뽑을 때마다 +1)
    public static readonly List<string> Roster = new List<string>(); // 가진 동료 id (중복 = 같은 동료 여러 명, 용사 제외)
    public static readonly List<Placement> Party = new List<Placement>();
    public static readonly Dictionary<string, int> Levels = new Dictionary<string, int>(); // 캐릭터별 강화 레벨 (없으면 1)

    static string Key(int slot, string name) => (slot == 0 ? Prefix : Prefix + "Slot" + slot + ".") + name;

    public static bool HasSlot(int slot) => PlayerPrefs.GetInt(Key(slot, "Started"), 0) == 1;
    public static bool HasSave => HasSlot(0);

    // ---------------- 저장 / 불러오기 ----------------

    public static void Load(int slot)
    {
        ResetMemory();
        if (!HasSlot(slot)) return;

        Gold = PlayerPrefs.GetInt(Key(slot, "Gold"), 0);
        ClearedStage = PlayerPrefs.GetInt(Key(slot, "ClearedStage"), 0);
        HeroAwaken = PlayerPrefs.GetInt(Key(slot, "HeroAwaken"), 0);
        // 동료 목록. 이름이 있는 캐릭터가 두 명 이상 있으면(예전 저장) 한 명만 남기고 골드로 돌려줍니다.
        var oldToNew = new Dictionary<int, int>();
        var rawRoster = PlayerPrefs.GetString(Key(slot, "Roster"), "").Split(',');
        for (int i = 0; i < rawRoster.Length; i++)
        {
            var def = CompanionDef.Find(rawRoster[i]);
            if (def == null || def.IsHero) continue;
            if (def.IsNamed && Roster.Contains(def.id)) { Gold += DuplicateRefund; continue; }
            oldToNew[i] = Roster.Count;
            Roster.Add(def.id);
        }

        foreach (var entry in PlayerPrefs.GetString(Key(slot, "Levels"), "").Split(','))
        {
            var f = entry.Split(':');
            if (f.Length == 2 && CompanionDef.Find(f[0]) != null && int.TryParse(f[1], out int lv))
                Levels[f[0]] = Mathf.Clamp(lv, 1, MaxLevel);
        }

        string party = PlayerPrefs.GetString(Key(slot, "Party"), null);
        if (party == null) AutoArrange(); // 예전 저장 파일이면 자동으로 배치
        else
        {
            foreach (var entry in party.Split(';'))
            {
                var f = entry.Split(':');
                if (f.Length != 4) continue;
                if (f[0] != HeroMark)
                {
                    // 중복 정리로 목록 번호가 바뀌었을 수 있으니 새 번호로 바꿔 줍니다.
                    if (!int.TryParse(f[0], out int oldIdx) || !oldToNew.TryGetValue(oldIdx, out int newIdx)) continue;
                    f[0] = newIdx.ToString();
                }
                if (!IsValidValue(f[0]) || Find(f[0]) != null) continue;
                if (!int.TryParse(f[1], out int col) || col < 0 || col >= Columns) continue;
                Party.Add(new Placement
                {
                    value = f[0], column = col,
                    fx = Mathf.Clamp01(ParseFloat(f[2])), fy = Mathf.Clamp01(ParseFloat(f[3])),
                });
            }
            EnsureHeroPlaced();
        }
    }

    // 자동 저장 (0번 칸)
    public static void Save() => SaveToSlot(0);

    public static void SaveToSlot(int slot)
    {
        PlayerPrefs.SetInt(Key(slot, "Started"), 1);
        PlayerPrefs.SetInt(Key(slot, "Gold"), Gold);
        PlayerPrefs.SetInt(Key(slot, "ClearedStage"), ClearedStage);
        PlayerPrefs.SetInt(Key(slot, "HeroAwaken"), HeroAwaken);
        PlayerPrefs.SetString(Key(slot, "Roster"), string.Join(",", Roster));
        var parts = new List<string>();
        foreach (var p in Party)
            parts.Add($"{p.value}:{p.column}:{p.fx.ToString("0.###", CultureInfo.InvariantCulture)}:{p.fy.ToString("0.###", CultureInfo.InvariantCulture)}");
        PlayerPrefs.SetString(Key(slot, "Party"), string.Join(";", parts));
        var levels = new List<string>();
        foreach (var kv in Levels) levels.Add(kv.Key + ":" + kv.Value);
        PlayerPrefs.SetString(Key(slot, "Levels"), string.Join(",", levels));
        PlayerPrefs.SetString(Key(slot, "SavedAt"), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        PlayerPrefs.Save();
    }

    // 불러오기 화면에 보여 줄 요약
    public static string SlotSummary(int slot)
    {
        if (!HasSlot(slot)) return "비어 있음";
        int cleared = PlayerPrefs.GetInt(Key(slot, "ClearedStage"), 0);
        int gold = PlayerPrefs.GetInt(Key(slot, "Gold"), 0);
        string roster = PlayerPrefs.GetString(Key(slot, "Roster"), "");
        int count = roster.Length == 0 ? 0 : roster.Split(',').Length;
        int awaken = PlayerPrefs.GetInt(Key(slot, "HeroAwaken"), 0);
        string progress = cleared == 0 ? "시작 전" : Stages.Label(cleared) + " 클리어";
        return $"{progress}  ·  골드 {gold:N0}  ·  동료 {count}명  ·  용사 각성 +{awaken}\n{PlayerPrefs.GetString(Key(slot, "SavedAt"), "")}";
    }

    // 새로 시작: 메모리를 비우고 자동 저장을 지웁니다. (직접 저장한 슬롯 1~3은 남아요)
    public static void ResetAll()
    {
        ResetMemory();
        foreach (var name in new[] { "Started", "Gold", "ClearedStage", "HeroAwaken", "Roster", "Party", "Formation", "Levels", "SavedAt" })
            PlayerPrefs.DeleteKey(Key(0, name));
        PlayerPrefs.Save();
    }

    static void ResetMemory()
    {
        Gold = 0;
        ClearedStage = 0;
        HeroAwaken = 0;
        Roster.Clear();
        Party.Clear();
        Levels.Clear();
        EnsureHeroPlaced();
    }

    static float ParseFloat(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0.5f;

    // ---------------- 오프닝 / 설정 (저장 칸과 상관없이 공통) ----------------

    public static bool OpeningSeen
    {
        get => PlayerPrefs.GetInt(Prefix + "OpeningSeen", 0) == 1;
        set { PlayerPrefs.SetInt(Prefix + "OpeningSeen", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static int TextSpeed // 0 = 느림, 1 = 보통, 2 = 빠름
    {
        get => PlayerPrefs.GetInt(Prefix + "Settings.TextSpeed", 1);
        set { PlayerPrefs.SetInt(Prefix + "Settings.TextSpeed", value); PlayerPrefs.Save(); }
    }

    public static bool FastBattle // 전투를 2배속으로 시작
    {
        get => PlayerPrefs.GetInt(Prefix + "Settings.FastBattle", 0) == 1;
        set { PlayerPrefs.SetInt(Prefix + "Settings.FastBattle", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // ---------------- 뽑기 ----------------

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
        if (def.IsNamed && !isNew)
        {
            Gold += DuplicateRefund; // (뽑기에서 막고 있지만, 혹시 중복이 나오면 골드로)
            return "골드 +" + DuplicateRefund;
        }
        Roster.Add(def.id);
        return isNew ? "NEW" : null;
    }

    // ---------------- 강화 ----------------

    public static int LevelOf(CompanionDef def) => Levels.TryGetValue(def.id, out int lv) ? lv : 1;

    // 강화 레벨에 따른 능력치 배수
    public static float LevelMultiplier(CompanionDef def) => 1f + LevelBonus * (LevelOf(def) - 1);

    // 다음 레벨로 강화하는 데 드는 골드
    public static int EnhanceCost(CompanionDef def) => (50 + 30 * (int)def.rarity) * LevelOf(def);

    public static bool Enhance(CompanionDef def)
    {
        int lv = LevelOf(def);
        int cost = EnhanceCost(def);
        if (lv >= MaxLevel || Gold < cost) return false;
        Gold -= cost;
        Levels[def.id] = lv + 1;
        Save();
        return true;
    }

    // 캠프에서 보여 줄 '가진 캐릭터' 목록 (용사 먼저, 같은 캐릭터는 한 번만, 등급 높은 순)
    public static List<CompanionDef> OwnedCharacters()
    {
        var list = new List<CompanionDef>();
        foreach (var id in Roster)
        {
            var def = CompanionDef.Find(id);
            if (!list.Contains(def)) list.Add(def);
        }
        list.Sort((a, b) => b.rarity.CompareTo(a.rarity));
        list.Insert(0, CompanionDef.Hero);
        return list;
    }

    public static int CountOwned(CompanionDef def)
    {
        if (def.IsHero) return 1;
        int n = 0;
        foreach (var id in Roster) if (id == def.id) n++;
        return n;
    }

    // ---------------- 편성 ----------------

    static bool IsValidValue(string value)
    {
        if (value == HeroMark) return true;
        return int.TryParse(value, out int idx) && idx >= 0 && idx < Roster.Count;
    }

    public static CompanionDef DefOf(string value)
    {
        if (value == HeroMark) return CompanionDef.Hero;
        if (int.TryParse(value, out int idx) && idx >= 0 && idx < Roster.Count) return CompanionDef.Find(Roster[idx]);
        return null;
    }

    public static Placement Find(string value)
    {
        foreach (var p in Party) if (p.value == value) return p;
        return null;
    }

    public static bool IsPlaced(int rosterIndex) => Find(rosterIndex.ToString()) != null;

    public static int CountInColumn(int column)
    {
        int n = 0;
        foreach (var p in Party) if (p.column == column) n++;
        return n;
    }

    // 동료를 줄에 놓습니다. 그 줄이 꽉 찼으면 false.
    public static bool Place(string value, int column, float fx, float fy)
    {
        var existing = Find(value);
        bool sameColumn = existing != null && existing.column == column;
        if (!sameColumn && CountInColumn(column) >= MaxPerColumn) return false;
        if (existing == null)
        {
            existing = new Placement { value = value };
            Party.Add(existing);
        }
        existing.column = column;
        existing.fx = Mathf.Clamp01(fx);
        existing.fy = Mathf.Clamp01(fy);
        return true;
    }

    // 편성에서 뺍니다. 용사는 뺄 수 없어요.
    public static void Remove(string value)
    {
        if (value == HeroMark) return;
        Party.RemoveAll(p => p.value == value);
    }

    public static void ClearParty()
    {
        Party.Clear();
        EnsureHeroPlaced();
    }

    // 용사는 항상 파티에 있어야 합니다. 없으면 선두 가운데에 놓습니다.
    public static void EnsureHeroPlaced()
    {
        if (Find(HeroMark) != null) return;
        Party.Add(new Placement { value = HeroMark, column = 0, fx = 0.5f, fy = 0.5f });
    }

    // 직업별로 알맞은 줄에 등급 높은 동료부터 자동으로 배치하고, 줄 안에서는 위아래로 고르게 벌려 세웁니다.
    public static void AutoArrange()
    {
        Party.Clear();
        Party.Add(new Placement { value = HeroMark, column = 0 });

        var order = new List<int>();
        for (int i = 0; i < Roster.Count; i++) order.Add(i);
        order.Sort((a, b) => CompanionDef.Find(Roster[b]).rarity.CompareTo(CompanionDef.Find(Roster[a]).rarity));

        foreach (int idx in order)
        {
            var job = CompanionDef.Find(Roster[idx]).job;
            int[] columns =
                job == HeroClass.Warrior || job == HeroClass.Soldier ? new[] { 0, 1, 2, 3, 4 } :
                job == HeroClass.Archer ? new[] { 2, 1, 3, 4, 0 } :
                job == HeroClass.Mage ? new[] { 3, 2, 4, 1, 0 } :
                new[] { 4, 3, 2, 1, 0 }; // 사제
            foreach (int col in columns)
            {
                if (CountInColumn(col) >= MaxPerColumn) continue;
                Party.Add(new Placement { value = idx.ToString(), column = col });
                break;
            }
        }

        // 줄마다 위에서 아래로 고르게 배치 (용사는 선두 가운데)
        for (int col = 0; col < Columns; col++)
        {
            var members = Party.FindAll(p => p.column == col);
            var hero = members.Find(p => p.value == HeroMark);
            if (hero != null)
            {
                members.Remove(hero);
                members.Insert(members.Count / 2, hero);
            }
            for (int i = 0; i < members.Count; i++)
            {
                members[i].fx = 0.5f;
                members[i].fy = (i + 0.5f) / members.Count;
            }
        }
    }
}
