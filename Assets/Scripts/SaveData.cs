using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 진행 상황(골드, 클리어한 판, 동료, 편성, 호감도, 회차)을 컴퓨터에 저장합니다.
// 저장 칸: 0번 = 자동 저장('이어하기'), 1~3번 = 직접 저장하는 슬롯('불러오기')
public static class SaveData
{
    const string Prefix = "GachaHero.";
    public const int SlotCount = 4; // 자동 저장 1개 + 슬롯 3개

    // ---- 성장 숫자 (자유롭게 바꿔 보세요) ----
    public const int MaxLevel = 30;          // 최대 레벨
    public const float LevelBonus = 0.02f;   // 레벨 1마다 능력치 +2%
    public const int StarsPerTier = 5;       // 별 5개를 채운 뒤 다음 합성부터 진급 (별 색이 바뀌고 다시 ★1부터)
    public const int MaxStar = 15;           // 최대 성급: ★5 → 1차 진급 ★5 → 2차 진급 ★5 (총 15강)
    public const float StarBonus = 0.30f;    // 성급 1마다 능력치 +30% (성급 강화가 레벨보다 효과가 큼)
    public const float AffinityBonus = 0.05f; // 호감도 1마다 능력치 +5%
    public const int BaseDeploy = 7;         // 처음 출진 가능 인원 (용사 포함). 스테이지를 클리어할 때마다 +1
    static readonly int[] sellPrices = { 30, 60, 120, 250, 500, 1000 }; // 등급별 판매 가격 (성급만큼 곱함)

    // ---- 편성: 선두 ~ 후미 5개의 줄. 줄 안에서는 자유롭게 위치를 정합니다 ----
    public const int Columns = 5;
    public const int MaxPerColumn = 5;   // 한 줄에 놓을 수 있는 최대 인원
    public static readonly string[] ColumnNames = { "선두", "전열", "중열", "후열", "후미" }; // 0 = 맨 앞

    // 가지고 있는 동료 한 명 (같은 캐릭터도 한 명 한 명 따로 성급/레벨을 가짐)
    public class Member
    {
        public int uid;       // 동료마다 다른 번호
        public string id;     // 캐릭터 종류 (CompanionData의 id)
        public int star = 1;  // 성급 (같은 캐릭터를 합쳐서 올림)
        public int level = 1;
        public int exp;
        public CompanionDef Def => CompanionDef.Find(id);
    }

    // 편성된 동료 한 명의 자리
    public class Placement
    {
        public int uid;       // 어떤 동료인지
        public int column;    // 0 = 선두 ... 4 = 후미
        public float fx, fy;  // 줄 안에서의 위치 (0~1). fx: 0 = 앞쪽, fy: 0 = 위쪽
    }

    public static int Gold;
    public static int ClearedStage;     // 이번 회차에서 클리어한 가장 먼 판 번호 (0~100)
    public static int Cycle = 1;        // 회차
    public static bool RunCleared;      // 이번 회차에서 마왕을 쓰러뜨렸는지 (전승 특전이 열림)
    // 지금까지 게임(10-10)을 끝까지 클리어한 횟수
    public static int CompletedRuns => (Cycle - 1) + (RunCleared ? 1 : 0);
    public static readonly List<Member> Roster = new List<Member>();     // 용사 포함
    public static readonly List<Placement> Party = new List<Placement>();
    public static readonly Dictionary<string, int> Affinity = new Dictionary<string, int>();    // 호감도 (회차가 바뀌어도 유지)
    public static readonly Dictionary<string, int> LegacySpent = new Dictionary<string, int>(); // 이번 회차에 쓴 전승 포인트
    public static readonly HashSet<string> SeenEnemies = new HashSet<string>();                 // 도감에 등록된 적
    static int nextUid = 1;

    static string Key(int slot, string name) => (slot == 0 ? Prefix : Prefix + "Slot" + slot + ".") + name;
    static readonly string[] AllKeys =
    {
        "Started", "Gold", "ClearedStage", "HeroAwaken", "Cycle", "RunCleared", "Members", "NextUid", "Party2",
        "Affinity", "LegacySpent", "Seen", "SavedAt", "Roster", "Party", "Formation", "Levels",
    };

    public static bool HasSlot(int slot) => PlayerPrefs.GetInt(Key(slot, "Started"), 0) == 1;
    public static bool HasSave => HasSlot(0);

    // ================= 저장 / 불러오기 =================

    public static void Load(int slot)
    {
        ResetMemory();
        if (!HasSlot(slot)) return;

        Gold = PlayerPrefs.GetInt(Key(slot, "Gold"), 0);
        ClearedStage = PlayerPrefs.GetInt(Key(slot, "ClearedStage"), 0);
        int oldAwaken = PlayerPrefs.GetInt(Key(slot, "HeroAwaken"), 0); // 예전 '각성'은 성급으로 바꿔 줌
        Cycle = Mathf.Max(1, PlayerPrefs.GetInt(Key(slot, "Cycle"), 1));
        RunCleared = PlayerPrefs.GetInt(Key(slot, "RunCleared"), 0) == 1;
        ReadCounts(PlayerPrefs.GetString(Key(slot, "Affinity"), ""), Affinity);
        ReadCounts(PlayerPrefs.GetString(Key(slot, "LegacySpent"), ""), LegacySpent);
        foreach (var id in PlayerPrefs.GetString(Key(slot, "Seen"), "").Split(','))
            if (EnemyDef.Find(id) != null) SeenEnemies.Add(id);

        string members = PlayerPrefs.GetString(Key(slot, "Members"), null);
        if (members != null) LoadMembers(slot, members);
        else LoadOldFormat(slot);

        EnsureHeroMember();
        if (oldAwaken > 0) HeroMember.star = Mathf.Min(MaxStar, HeroMember.star + oldAwaken);
        EnsureHeroPlaced();
        // 예전 저장처럼 출진 가능 인원보다 많이 배치되어 있으면 넘치는 동료를 뺍니다 (용사는 남김)
        int heroUid = HeroMember.uid;
        for (int i = Party.Count - 1; i >= 0 && Party.Count > DeployCap; i--)
            if (Party[i].uid != heroUid) Party.RemoveAt(i);
    }

    const int RemovedCharacterRefund = 100;

    static void LoadMembers(int slot, string members)
    {
        Roster.Clear();
        foreach (var entry in members.Split(','))
        {
            var f = entry.Split(':');
            if (f.Length != 5) continue;
            // 이제 게임에 없는 캐릭터(예전 임시 캐릭터)는 골드로 돌려줍니다.
            if (CompanionDef.Find(f[1]) == null) { Gold += RemovedCharacterRefund; continue; }
            int.TryParse(f[0], out int uid);
            int.TryParse(f[2], out int star);
            int.TryParse(f[3], out int level);
            int.TryParse(f[4], out int exp);
            Roster.Add(new Member { uid = uid, id = f[1], star = Mathf.Clamp(star, 1, MaxStar), level = Mathf.Clamp(level, 1, MaxLevel), exp = Mathf.Max(0, exp) });
        }
        nextUid = PlayerPrefs.GetInt(Key(slot, "NextUid"), 1);
        foreach (var m in Roster) nextUid = Mathf.Max(nextUid, m.uid + 1);

        foreach (var entry in PlayerPrefs.GetString(Key(slot, "Party2"), "").Split(';'))
        {
            var f = entry.Split(':');
            if (f.Length != 4 || !int.TryParse(f[0], out int uid) || MemberByUid(uid) == null) continue;
            if (!int.TryParse(f[1], out int col) || col < 0 || col >= Columns || FindPlacement(uid) != null) continue;
            Party.Add(new Placement { uid = uid, column = col, fx = Mathf.Clamp01(ParseFloat(f[2])), fy = Mathf.Clamp01(ParseFloat(f[3])) });
        }
    }

    // 예전 버전의 저장 파일 (동료 id 목록만 있던 형식)을 새 형식으로 옮깁니다.
    static void LoadOldFormat(int slot)
    {
        Roster.Clear();
        Roster.Add(new Member { uid = 0, id = CompanionDef.HeroId });
        var oldIds = PlayerPrefs.GetString(Key(slot, "Roster"), "").Split(',');
        var indexToUid = new Dictionary<int, int>();
        for (int i = 0; i < oldIds.Length; i++)
        {
            var def = CompanionDef.Find(oldIds[i]);
            if (def == null || def.IsHero) continue;
            indexToUid[i] = nextUid;
            Roster.Add(new Member { uid = nextUid++, id = def.id });
        }

        string party = PlayerPrefs.GetString(Key(slot, "Party"), null);
        if (party == null) { AutoArrange(); return; }
        foreach (var entry in party.Split(';'))
        {
            var f = entry.Split(':');
            if (f.Length != 4 || !int.TryParse(f[1], out int col) || col < 0 || col >= Columns) continue;
            int uid;
            if (f[0] == "H") uid = 0;
            else if (!int.TryParse(f[0], out int idx) || !indexToUid.TryGetValue(idx, out uid)) continue;
            if (FindPlacement(uid) != null) continue;
            Party.Add(new Placement { uid = uid, column = col, fx = Mathf.Clamp01(ParseFloat(f[2])), fy = Mathf.Clamp01(ParseFloat(f[3])) });
        }
    }

    // 자동 저장 (0번 칸)
    public static void Save() => SaveToSlot(0);

    public static void SaveToSlot(int slot)
    {
        PlayerPrefs.SetInt(Key(slot, "Started"), 1);
        PlayerPrefs.SetInt(Key(slot, "Gold"), Gold);
        PlayerPrefs.SetInt(Key(slot, "ClearedStage"), ClearedStage);
        PlayerPrefs.SetInt(Key(slot, "HeroAwaken"), 0); // 예전 '각성'은 이미 성급으로 바뀜
        PlayerPrefs.SetInt(Key(slot, "Cycle"), Cycle);
        PlayerPrefs.SetInt(Key(slot, "RunCleared"), RunCleared ? 1 : 0);

        var members = new List<string>();
        foreach (var m in Roster) members.Add($"{m.uid}:{m.id}:{m.star}:{m.level}:{m.exp}");
        PlayerPrefs.SetString(Key(slot, "Members"), string.Join(",", members));
        PlayerPrefs.SetInt(Key(slot, "NextUid"), nextUid);

        var parts = new List<string>();
        foreach (var p in Party)
            parts.Add($"{p.uid}:{p.column}:{p.fx.ToString("0.###", CultureInfo.InvariantCulture)}:{p.fy.ToString("0.###", CultureInfo.InvariantCulture)}");
        PlayerPrefs.SetString(Key(slot, "Party2"), string.Join(";", parts));

        PlayerPrefs.SetString(Key(slot, "Affinity"), WriteCounts(Affinity));
        PlayerPrefs.SetString(Key(slot, "LegacySpent"), WriteCounts(LegacySpent));
        PlayerPrefs.SetString(Key(slot, "Seen"), string.Join(",", SeenEnemies));
        PlayerPrefs.SetString(Key(slot, "SavedAt"), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        PlayerPrefs.Save();
    }

    // 불러오기 화면에 보여 줄 요약
    public static string SlotSummary(int slot)
    {
        if (!HasSlot(slot)) return "비어 있음";
        int cleared = PlayerPrefs.GetInt(Key(slot, "ClearedStage"), 0);
        int gold = PlayerPrefs.GetInt(Key(slot, "Gold"), 0);
        int cycle = Mathf.Max(1, PlayerPrefs.GetInt(Key(slot, "Cycle"), 1));
        string members = PlayerPrefs.GetString(Key(slot, "Members"), PlayerPrefs.GetString(Key(slot, "Roster"), ""));
        int count = members.Length == 0 ? 0 : members.Split(',').Length;
        string progress = cleared == 0 ? "시작 전" : Stages.Label(cleared) + " 클리어";
        return $"{cycle}회차  ·  {progress}  ·  골드 {gold:N0}  ·  동료 {count}명\n{PlayerPrefs.GetString(Key(slot, "SavedAt"), "")}";
    }

    // 새로 시작: 메모리를 비우고 자동 저장을 지웁니다. (직접 저장한 슬롯 1~3은 남아요)
    public static void ResetAll()
    {
        ResetMemory();
        foreach (var name in AllKeys) PlayerPrefs.DeleteKey(Key(0, name));
        PlayerPrefs.Save();
    }

    // 다음 회차 시작: 호감도와 도감(적)만 남기고 처음부터 다시 시작합니다.
    public static void StartNewCycle()
    {
        var keepAffinity = new Dictionary<string, int>(Affinity);
        var keepSeen = new HashSet<string>(SeenEnemies);
        int cycle = Cycle + 1;
        ResetMemory();
        foreach (var kv in keepAffinity) Affinity[kv.Key] = kv.Value;
        foreach (var id in keepSeen) SeenEnemies.Add(id);
        Cycle = cycle;
        Save();
    }

    static void ResetMemory()
    {
        Gold = 0;
        ClearedStage = 0;
        Cycle = 1;
        RunCleared = false;
        Roster.Clear();
        Party.Clear();
        Affinity.Clear();
        LegacySpent.Clear();
        SeenEnemies.Clear();
        nextUid = 1;
        EnsureHeroMember();
        EnsureHeroPlaced();
    }

    static float ParseFloat(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0.5f;

    static void ReadCounts(string text, Dictionary<string, int> into)
    {
        foreach (var entry in text.Split(','))
        {
            var f = entry.Split(':');
            if (f.Length == 2 && CompanionDef.Find(f[0]) != null && int.TryParse(f[1], out int n) && n > 0) into[f[0]] = n;
        }
    }

    static string WriteCounts(Dictionary<string, int> counts)
    {
        var list = new List<string>();
        foreach (var kv in counts) list.Add(kv.Key + ":" + kv.Value);
        return string.Join(",", list);
    }

    // ================= 오프닝 / 설정 (저장 칸과 상관없이 공통) =================

    public static bool OpeningSeen
    {
        get => PlayerPrefs.GetInt(Prefix + "OpeningSeen", 0) == 1;
        set { PlayerPrefs.SetInt(Prefix + "OpeningSeen", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // 스테이지 보스를 이긴 적이 있는지 (보스 컷씬 건너뛰기용, 저장 칸과 상관없이 공통)
    public static bool IsBossBeaten(int stage) => PlayerPrefs.GetInt(Prefix + "BossBeaten." + stage, 0) == 1;

    public static void MarkBossBeaten(int stage)
    {
        PlayerPrefs.SetInt(Prefix + "BossBeaten." + stage, 1);
        PlayerPrefs.Save();
    }

    public static int TextSpeed // 0 = 느림, 1 = 보통, 2 = 빠름
    {
        get => PlayerPrefs.GetInt(Prefix + "Settings.TextSpeed", 1);
        set { PlayerPrefs.SetInt(Prefix + "Settings.TextSpeed", value); PlayerPrefs.Save(); }
    }

    public static int BattleSpeed // 전투 시작 배속 1~4
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Settings.BattleSpeed", 1), 1, 4);
        set { PlayerPrefs.SetInt(Prefix + "Settings.BattleSpeed", Mathf.Clamp(value, 1, 4)); PlayerPrefs.Save(); }
    }

    // 음량 단계 0~4 (0 = 끔, 4 = 100%)
    public const int VolumeSteps = 4;
    static int musicLevel = -1, sfxLevel = -1;

    public static int MusicLevel
    {
        get { if (musicLevel < 0) musicLevel = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Settings.Music", 3), 0, VolumeSteps); return musicLevel; }
        set { musicLevel = Mathf.Clamp(value, 0, VolumeSteps); PlayerPrefs.SetInt(Prefix + "Settings.Music", musicLevel); PlayerPrefs.Save(); }
    }

    public static int SfxLevel
    {
        get { if (sfxLevel < 0) sfxLevel = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Settings.Sfx", 3), 0, VolumeSteps); return sfxLevel; }
        set { sfxLevel = Mathf.Clamp(value, 0, VolumeSteps); PlayerPrefs.SetInt(Prefix + "Settings.Sfx", sfxLevel); PlayerPrefs.Save(); }
    }

    public static float MusicVolume => MusicLevel / (float)VolumeSteps;
    public static float SfxVolume => SfxLevel / (float)VolumeSteps;

    // ================= 동료 =================

    public static Member MemberByUid(int uid) => Roster.Find(m => m.uid == uid);
    public static Member HeroMember => Roster.Find(m => m.id == CompanionDef.HeroId);

    static void EnsureHeroMember()
    {
        if (HeroMember == null) Roster.Insert(0, new Member { uid = 0, id = CompanionDef.HeroId });
    }

    // 용사를 뺀 동료 수
    public static int CompanionCount => Roster.Count - 1;

    // 뽑기 결과를 반영합니다. 카드에 붙일 표시("NEW", "★3" 등)를 돌려줍니다.
    public static string AddPulled(CompanionDef def)
    {
        if (def.IsHero)
        {
            // 용사 카드가 나오면 용사의 성급이 1 올라가요
            var hero = HeroMember;
            if (hero.star < MaxStar)
            {
                hero.star++;
                return "성급 " + FormationScreen.Stars(hero.star);
            }
            Gold += 300; // 최대 성급이면 골드로 돌려받음
            return "골드 +300";
        }
        bool isNew = CountOwned(def) == 0;
        Roster.Add(new Member { uid = nextUid++, id = def.id });
        return isNew ? "NEW" : null;
    }

    public static int CountOwned(CompanionDef def)
    {
        int n = 0;
        foreach (var m in Roster) if (m.id == def.id) n++;
        return n;
    }

    // 도감/전승에 쓰는 '가진 캐릭터 종류' 목록 (용사 먼저, 등급 높은 순)
    public static List<CompanionDef> OwnedCharacters()
    {
        var list = new List<CompanionDef>();
        foreach (var m in Roster) if (!list.Contains(m.Def)) list.Add(m.Def);
        list.Sort((a, b) => a.IsHero ? -1 : b.IsHero ? 1 : b.rarity.CompareTo(a.rarity));
        return list;
    }

    // 동료 관리 화면의 목록 (용사 먼저, 등급 → 성급 → 레벨 높은 순)
    public static List<Member> SortedMembers()
    {
        var list = new List<Member>(Roster);
        list.Sort((a, b) =>
        {
            if (a.Def.IsHero) return -1;
            if (b.Def.IsHero) return 1;
            int c = b.Def.rarity.CompareTo(a.Def.rarity);
            if (c != 0) return c;
            c = string.Compare(a.id, b.id, System.StringComparison.Ordinal);
            if (c != 0) return c;
            c = b.star.CompareTo(a.star);
            return c != 0 ? c : b.level.CompareTo(a.level);
        });
        return list;
    }

    // 능력치 배수 = 등급 × 레벨 × 성급 × 호감도(용사 제외)
    public static float StatMultiplier(Member m)
    {
        var def = m.Def;
        float mul = RarityInfo.StatMultiplier(def.rarity);
        mul *= 1f + LevelBonus * (m.level - 1);
        mul *= 1f + StarBonus * (m.star - 1);
        if (!def.IsHero) mul *= 1f + AffinityBonus * AffinityOf(def);
        return mul;
    }

    // ---------------- 레벨 (전투에 나가면 경험치를 얻어요) ----------------

    public static int ExpToNext(int level) => 40 + 20 * level;

    // 경험치를 주고, 오른 레벨 수를 돌려줍니다.
    public static int GainExp(Member m, int amount)
    {
        int ups = 0;
        if (m.level >= MaxLevel) return 0;
        m.exp += amount;
        while (m.level < MaxLevel && m.exp >= ExpToNext(m.level))
        {
            m.exp -= ExpToNext(m.level);
            m.level++;
            ups++;
        }
        if (m.level >= MaxLevel) m.exp = 0;
        return ups;
    }

    // ---------------- 성급 강화 (같은 캐릭터를 합치기) ----------------

    public static bool CanMerge(Member main, Member material) =>
        main != null && material != null && main != material && main.id == material.id && !main.Def.IsHero && main.star < MaxStar;

    // material을 재료로 써서 main의 성급을 1 올립니다. 재료는 사라져요.
    public static bool Merge(Member main, Member material)
    {
        if (!CanMerge(main, material)) return false;
        RemoveMember(material);
        main.star++;
        Save();
        return true;
    }

    // ---------------- 일괄 합성 ----------------
    // 캐릭터마다 성급 → 레벨이 가장 높은 동료를 기준으로, 나머지(성급·레벨 낮은 것부터)를 재료로 합칩니다.
    // 기준이 최대 성급이 되면 그다음으로 높은 동료가 새 기준이 돼요. 출진 중인 동료는 재료로 쓰지 않아요.

    // (기준, 재료) 순서대로의 합성 계획 (아직 합성하지는 않음)
    static List<KeyValuePair<Member, Member>> BulkMergePlan()
    {
        var plan = new List<KeyValuePair<Member, Member>>();
        var groups = new Dictionary<string, List<Member>>();
        foreach (var m in Roster)
        {
            if (m.Def.IsHero) continue;
            if (!groups.TryGetValue(m.id, out var g)) groups[m.id] = g = new List<Member>();
            g.Add(m);
        }
        foreach (var g in groups.Values)
        {
            if (g.Count < 2) continue;
            // 기준 후보: 성급 높은 순 → 레벨 높은 순
            g.Sort((x, y) => y.star != x.star ? y.star.CompareTo(x.star) : y.level.CompareTo(x.level));
            var stars = new Dictionary<Member, int>();
            foreach (var m in g) stars[m] = m.star;
            var used = new HashSet<Member>();
            foreach (var main in g)
            {
                if (used.Contains(main)) continue;
                // 재료: 출진 중이 아닌 동료 중 성급·레벨 낮은 것부터
                for (int i = g.Count - 1; i >= 0 && stars[main] < MaxStar; i--)
                {
                    var mat = g[i];
                    if (mat == main || used.Contains(mat) || IsPlaced(mat)) continue;
                    used.Add(mat);
                    stars[main]++;
                    plan.Add(new KeyValuePair<Member, Member>(main, mat));
                }
                used.Add(main);
            }
        }
        return plan;
    }

    // 일괄 합성을 하면 몇 번 합성되는지 (확인 문구용)
    public static int BulkMergeCount() => BulkMergePlan().Count;

    // 일괄 합성 실행. 합성한 횟수를 돌려줍니다.
    public static int BulkMerge()
    {
        var plan = BulkMergePlan();
        foreach (var step in plan)
        {
            RemoveMember(step.Value);
            step.Key.star++;
        }
        if (plan.Count > 0) Save();
        return plan.Count;
    }

    // ---------------- 판매 ----------------

    public static int SellPrice(Member m) => sellPrices[(int)m.Def.rarity] * m.star;

    public static bool Sell(Member m)
    {
        if (m == null || m.Def.IsHero) return false; // 용사는 팔 수 없어요
        Gold += SellPrice(m);
        RemoveMember(m);
        Save();
        return true;
    }

    static void RemoveMember(Member m)
    {
        Party.RemoveAll(p => p.uid == m.uid);
        Roster.Remove(m);
    }

    // ================= 호감도 / 전승 특전 =================

    public static int AffinityOf(CompanionDef def) => Affinity.TryGetValue(def.id, out int n) ? n : 0;

    // 이번 회차에서 이 캐릭터가 도달한 가장 높은 레벨
    public static int MaxLevelOf(CompanionDef def)
    {
        int best = 0;
        foreach (var m in Roster) if (m.id == def.id) best = Mathf.Max(best, m.level);
        return best;
    }

    // 전승 포인트: 이번 회차 레벨 10마다 1 (30레벨이면 3), 이미 쓴 만큼 뺌
    public static int LegacyPoints(CompanionDef def)
    {
        if (def.IsHero) return 0;
        int spent = LegacySpent.TryGetValue(def.id, out int s) ? s : 0;
        return Mathf.Max(0, MaxLevelOf(def) / 10 - spent);
    }

    public static bool RaiseAffinity(CompanionDef def)
    {
        if (LegacyPoints(def) <= 0) return false;
        Affinity[def.id] = AffinityOf(def) + 1;
        LegacySpent[def.id] = (LegacySpent.TryGetValue(def.id, out int s) ? s : 0) + 1;
        Save();
        return true;
    }

    // ================= 편성 =================

    // 출진 가능 인원 (용사 포함): 처음 7명 + 클리어한 스테이지 수
    public static int DeployCap => Mathf.Min(Columns * MaxPerColumn, BaseDeploy + ClearedStage / Stages.LevelsPerStage);

    public static Placement FindPlacement(int uid) => Party.Find(p => p.uid == uid);
    public static bool IsPlaced(Member m) => FindPlacement(m.uid) != null;

    public static int CountInColumn(int column)
    {
        int n = 0;
        foreach (var p in Party) if (p.column == column) n++;
        return n;
    }

    // 동료를 줄에 놓습니다. 놓을 수 없으면 이유를 돌려주고, 성공하면 null.
    public static string Place(Member m, int column, float fx, float fy)
    {
        var existing = FindPlacement(m.uid);
        if (existing == null)
        {
            if (Party.Count >= DeployCap) return $"출진 가능 인원은 {DeployCap}명이에요 (스테이지를 클리어하면 늘어나요)";
            // 이름이 있는 캐릭터는 같은 인물을 두 명 배치할 수 없어요
            if (m.Def.IsNamed && Party.Exists(p => MemberByUid(p.uid).id == m.id))
                return $"{m.Def.name}은(는) 이미 배치되어 있어요 (같은 인물은 한 명만)";
        }
        bool sameColumn = existing != null && existing.column == column;
        if (!sameColumn && CountInColumn(column) >= MaxPerColumn) return $"{ColumnNames[column]}은(는) 꽉 찼어요 (최대 {MaxPerColumn}명)";

        if (existing == null)
        {
            existing = new Placement { uid = m.uid };
            Party.Add(existing);
        }
        existing.column = column;
        existing.fx = Mathf.Clamp01(fx);
        existing.fy = Mathf.Clamp01(fy);
        return null;
    }

    // 편성에서 뺍니다. 용사는 뺄 수 없어요.
    public static void Remove(Member m)
    {
        if (m.Def.IsHero) return;
        Party.RemoveAll(p => p.uid == m.uid);
    }

    public static void ClearParty()
    {
        Party.Clear();
        EnsureHeroPlaced();
    }

    // 용사는 항상 파티에 있어야 합니다. 없으면 선두 가운데에 놓습니다.
    public static void EnsureHeroPlaced()
    {
        var hero = HeroMember;
        if (hero == null || FindPlacement(hero.uid) != null) return;
        Party.Insert(0, new Placement { uid = hero.uid, column = 0, fx = 0.5f, fy = 0.5f });
    }

    // 직업별로 알맞은 줄에 강한 동료부터 자동으로 배치합니다. (출진 인원, 같은 인물 규칙을 지킴)
    public static void AutoArrange()
    {
        Party.Clear();
        EnsureHeroPlaced();

        var order = new List<Member>(Roster);
        order.Remove(HeroMember);
        order.Sort((a, b) => StatMultiplier(b).CompareTo(StatMultiplier(a)));

        foreach (var m in order)
        {
            if (Party.Count >= DeployCap) break;
            int[] columns = m.Def.job.preferredColumns; // 근접은 앞, 원거리는 가운데/뒤, 치유는 맨 뒤
            foreach (int col in columns)
                if (Place(m, col, 0.5f, 0.5f) == null) break;
        }

        // 줄마다 위에서 아래로 고르게 배치 (용사는 선두 가운데)
        for (int col = 0; col < Columns; col++)
        {
            var members = Party.FindAll(p => p.column == col);
            var hero = members.Find(p => p.uid == HeroMember.uid);
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
