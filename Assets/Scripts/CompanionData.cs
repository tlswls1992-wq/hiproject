using System.Collections.Generic;
using UnityEngine;

// 캐릭터 등급: 노말 < 레어 < 슈퍼레어 < 유니크 < 전설 < 신화
public enum Rarity { Normal, Rare, SuperRare, Unique, Legendary, Mythic }

// 캐릭터 성향
public enum Alignment { 혼돈, 중립, 질서, 명예, 실리, 선, 악 }

public static class RarityInfo
{
    static readonly string[] names = { "노말", "레어", "슈퍼레어", "유니크", "전설", "신화" };
    static readonly Color[] colors =
    {
        new Color(0.75f, 0.75f, 0.75f), // 노말: 회색
        new Color(0.30f, 0.65f, 1.00f), // 레어: 파랑
        new Color(0.75f, 0.40f, 1.00f), // 슈퍼레어: 보라
        new Color(1.00f, 0.55f, 0.15f), // 유니크: 주황
        new Color(1.00f, 0.85f, 0.25f), // 전설: 금색
        new Color(1.00f, 0.30f, 0.35f), // 신화: 빨강 (화면에서는 무지개로 빛남)
    };
    // 등급별 능력치 배수 (노말 = 1배)
    static readonly float[] statMultipliers = { 1f, 1.4f, 1.9f, 2.6f, 3.4f, 4.5f };

    public static int Count => names.Length;
    public static string Name(Rarity r) => names[(int)r];
    public static float StatMultiplier(Rarity r) => statMultipliers[(int)r];
    public static int Stars(Rarity r) => (int)r + 1;

    public static Color GetColor(Rarity r) => colors[(int)r];

    // 신화 등급은 시간에 따라 무지개색으로 바뀝니다.
    public static Color Animated(Rarity r)
    {
        if (r != Rarity.Mythic) return GetColor(r);
        return Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.4f, 1f), 0.55f, 1f);
    }
}

// 출현 조건 종류
public enum UnlockKind
{
    Always,      // 처음부터 뽑기에 나옴
    Clears,      // 게임(10-10)을 N번 클리어한 뒤 나옴
    StageClear,  // 특정 스테이지를 클리어한 뒤 나옴 (이후 회차에서도 계속 나옴)
}

// 뽑을 수 있는 캐릭터 한 명의 정보
// 기본 설정: [종족 / 소속 / 직업 / 성향]
public class CompanionDef
{
    public string id;
    public string name;
    public string gender;        // 성별 (지금은 화면에 표시하지 않음)
    public Rarity rarity;
    public string race;          // 종족
    public string faction;       // 소속
    public HeroClass job;        // 직업
    public Alignment alignment;  // 성향
    public UnlockKind unlock;    // 출현 조건
    public int unlockValue;      // Clears: 클리어 횟수 / StageClear: 스테이지 번호(1~10)

    // ※ 인물 설정: 개발용 내부 메모입니다. 게임 화면에는 절대 표시하지 않아요.
    //   (캠프 대화의 말투 등을 정할 때 참고만 합니다.)
    public string note;

    CompanionDef(string id, string name, string gender, Rarity rarity, string race, string faction, HeroClass job,
                 Alignment alignment, string note, UnlockKind unlock = UnlockKind.Always, int unlockValue = 0)
    {
        this.id = id; this.name = name; this.gender = gender; this.rarity = rarity;
        this.race = race; this.faction = faction; this.job = job; this.alignment = alignment;
        this.note = note; this.unlock = unlock; this.unlockValue = unlockValue;
    }

    // [종족/소속/직업/성향] 한 줄 표시
    public string Profile => $"{race} / {faction} / {job.name} / {alignment}";

    // 카드에 보이는 짧은 소개 (내부 설정은 드러내지 않음)
    public string desc => IsHero ? "가챠의 힘을 가진 용사. 뽑으면 성급 상승!" : $"{faction}의 {job.name}";

    // ★ 새 캐릭터를 추가하려면 이 목록에 한 줄을 추가하세요. (id는 겹치지 않게)
    //   순서: id, 이름, 성별, 등급, 종족, 소속, 직업, 성향, 인물 설정(내부용), [출현 조건, 조건 값]
    public static readonly CompanionDef[] All =
    {
        // 용사 본인도 뽑기에서 나옵니다. 용사 카드가 나오면 용사의 성급이 올라가요.
        new CompanionDef(HeroId,      "용사",     "남", Rarity.Unique,    "인간",   "용사 파티", HeroClass.Brave,      Alignment.중립, "용사 본인"),
        new CompanionDef("teo",       "테오",     "남", Rarity.SuperRare, "인간",   "용사 파티", HeroClass.Lancer,     Alignment.명예, "용사의 고향 친구"),
        new CompanionDef("maria",     "마리아",   "여", Rarity.SuperRare, "인간",   "용사 파티", HeroClass.Mage,       Alignment.질서, "용사 모험 동경 / 학자"),
        new CompanionDef("amelia",    "아멜리아", "여", Rarity.SuperRare, "엘프",   "용사 파티", HeroClass.Archer,     Alignment.명예, "츤데레", UnlockKind.Clears, 1),
        new CompanionDef("liliana",   "릴리아나", "여", Rarity.Unique,    "인간",   "용사 파티", HeroClass.Saint,      Alignment.선,   "선택받은 성녀", UnlockKind.Clears, 2),
        new CompanionDef("mira",      "미라",     "여", Rarity.Unique,    "용",     "용사 파티", HeroClass.Dragon,     Alignment.질서, "호기심 많은 용 / 정체를 숨김", UnlockKind.Clears, 3),
        new CompanionDef("goden",     "고덴",     "남", Rarity.SuperRare, "드워프", "용사 파티", HeroClass.Porter,     Alignment.실리, "만능 재주꾼 / 설명충"),
        new CompanionDef("ian",       "이안",     "남", Rarity.Rare,      "인간",   "자유 용병", HeroClass.Adventurer, Alignment.중립, "호기심 넘치는 모험가"),
        new CompanionDef("n_balo",    "발로",     "남", Rarity.Normal,    "인간",   "올 왕국",   HeroClass.Soldier,    Alignment.선,   "올 왕국의 평범한 병사"),
        new CompanionDef("n_deboram", "드보람",   "여", Rarity.Normal,    "인간",   "올 왕국",   HeroClass.Soldier,    Alignment.실리, "올 왕국의 평범한 병사"),
        new CompanionDef("dane",      "데인",     "남", Rarity.Rare,      "인간",   "올 왕국",   HeroClass.Knight,     Alignment.질서, "올 왕국 기사"),
        new CompanionDef("petin",     "페틴",     "여", Rarity.Normal,    "인간",   "자유 용병", HeroClass.Mercenary,  Alignment.실리, "수전노 용병"),
        new CompanionDef("siena",     "시에나",   "여", Rarity.SuperRare, "인간",   "올 왕국",   HeroClass.Inspector,  Alignment.질서, "올 왕국 감찰관", UnlockKind.StageClear, 1),
        new CompanionDef("ozo",       "오죠",     "남", Rarity.Rare,      "인간",   "자유 용병", HeroClass.Mage,       Alignment.혼돈, "야망 있는 마법사"),
        new CompanionDef("michaela",  "미카엘라", "여", Rarity.Rare,      "인간",   "자유 용병", HeroClass.Healer,     Alignment.명예, "의로운 힐러"),
    };

    public const string HeroId = "hero";
    public static CompanionDef Hero => Find(HeroId);
    public bool IsHero => id == HeroId;

    // 이름이 있는 캐릭터는 같은 인물을 파티에 두 명 배치할 수 없어요. (지금은 모든 동료가 이름이 있음)
    // 이름 없는 일반 캐릭터를 나중에 추가하면 여기에 id를 적어 주세요.
    static readonly string[] GenericIds = { };

    public bool IsNamed => !IsHero && System.Array.IndexOf(GenericIds, id) < 0;

    // 지금 뽑기에 나올 수 있는지 (출현 조건 확인)
    public bool IsUnlocked
    {
        get
        {
            switch (unlock)
            {
                case UnlockKind.Clears: return SaveData.CompletedRuns >= unlockValue;
                case UnlockKind.StageClear:
                    return SaveData.CompletedRuns >= 1 || SaveData.ClearedStage >= unlockValue * Stages.LevelsPerStage;
                default: return true;
            }
        }
    }

    // 출현 조건 안내 문구 (도감에서 아직 못 만난 인물에 표시)
    public string UnlockHint
    {
        get
        {
            switch (unlock)
            {
                case UnlockKind.Clears: return $"{unlockValue}회 클리어 후 출현";
                case UnlockKind.StageClear: return $"'{Stages.StageTitle(unlockValue)}' 클리어 후 출현";
                default: return "기본 출현";
            }
        }
    }

    public static CompanionDef Find(string id)
    {
        foreach (var c in All) if (c.id == id) return c;
        return null;
    }

    // 해당 등급 중 아무나 한 명 (출현 조건을 만족한 캐릭터만.
    // 그 등급에 나올 캐릭터가 없으면 한 단계 아래 등급에서)
    public static CompanionDef RandomOf(Rarity rarity)
    {
        for (int r = (int)rarity; r >= 0; r--)
        {
            var list = new List<CompanionDef>();
            foreach (var c in All)
                if ((int)c.rarity == r && c.IsUnlocked) list.Add(c);
            if (list.Count > 0) return list[Random.Range(0, list.Count)];
        }
        return All[0];
    }
}
