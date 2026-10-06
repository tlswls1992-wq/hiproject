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

// 뽑을 수 있는 캐릭터 한 명의 정보
// 기본 설정: [종족 / 소속 / 직업 / 성향]
public class CompanionDef
{
    public string id;
    public string name;
    public Rarity rarity;
    public string race;          // 종족
    public string faction;       // 소속
    public HeroClass job;        // 직업
    public Alignment alignment;  // 성향
    public string desc;

    CompanionDef(string id, string name, Rarity rarity, string race, string faction, HeroClass job, Alignment alignment, string desc)
    {
        this.id = id; this.name = name; this.rarity = rarity;
        this.race = race; this.faction = faction; this.job = job; this.alignment = alignment;
        this.desc = desc;
    }

    // [종족/소속/직업/성향] 한 줄 표시
    public string Profile => $"{race} / {faction} / {job.name} / {alignment}";

    // ★ 새 캐릭터를 추가하려면 이 목록에 한 줄을 추가하세요. (id는 겹치지 않게)
    //   순서: id, 이름, 등급, 종족, 소속, 직업, 성향, 한 줄 설명
    public static readonly CompanionDef[] All =
    {
        // 용사 본인도 뽑기에서 나옵니다. 용사가 나오면 '각성'해서 강해져요.
        new CompanionDef(HeroId, "용사", Rarity.Unique, "인간", "용사 파티", HeroClass.Brave, Alignment.중립, "가챠의 힘을 가진 용사. 뽑으면 각성!"),

        new CompanionDef("n_balo",       "발로",          Rarity.Normal,    "인간",     "올 왕국",     HeroClass.Soldier, Alignment.선,   "정의감 하나는 누구에게도 지지 않는 신참 병사"),
        new CompanionDef("n_deboram",    "드보람",        Rarity.Normal,    "인간",     "올 왕국",     HeroClass.Soldier, Alignment.실리, "받은 만큼만 일하는 현실적인 병사"),
        new CompanionDef("c_villager",   "마을 청년",     Rarity.Normal,    "인간",     "올 왕국",     HeroClass.Warrior, Alignment.선,   "괭이 대신 검을 든 용감한 청년"),
        new CompanionDef("c_hunter",     "사냥꾼",        Rarity.Normal,    "인간",     "자유민",      HeroClass.Archer,  Alignment.중립, "토끼 사냥이 특기"),
        new CompanionDef("c_apprentice", "견습 마법사",   Rarity.Normal,    "인간",     "마탑",        HeroClass.Mage,    Alignment.혼돈, "가끔 주문을 틀린다"),
        new CompanionDef("c_nun",        "수녀",          Rarity.Normal,    "인간",     "성교회",      HeroClass.Priest,  Alignment.선,   "기도로 상처를 낫게 한다"),

        new CompanionDef("r_mercenary",  "용병 검사",     Rarity.Rare,      "인간",     "용병단",      HeroClass.Warrior, Alignment.실리, "돈만 주면 어디든 간다"),
        new CompanionDef("r_ranger",     "숲의 궁수",     Rarity.Rare,      "엘프",     "숲의 부족",   HeroClass.Archer,  Alignment.중립, "숲에서 자란 명사수"),
        new CompanionDef("r_pyro",       "불꽃 마법사",   Rarity.Rare,      "인간",     "마탑",        HeroClass.Mage,    Alignment.혼돈, "모든 것을 태워 버린다"),
        new CompanionDef("r_cleric",     "성당 사제",     Rarity.Rare,      "인간",     "성교회",      HeroClass.Priest,  Alignment.질서, "올 왕국 대성당 출신"),

        new CompanionDef("e_paladin",    "성기사",        Rarity.SuperRare, "인간",     "성교회",      HeroClass.Warrior, Alignment.명예, "신의 방패를 든 기사"),
        new CompanionDef("e_sniper",     "그림자 저격수", Rarity.SuperRare, "하프엘프", "그림자 길드", HeroClass.Archer,  Alignment.실리, "한 발이면 충분하다"),
        new CompanionDef("e_frost",      "얼음 마녀",     Rarity.SuperRare, "인간",     "북방 설산",   HeroClass.Mage,    Alignment.혼돈, "북쪽 설산의 마녀"),
        new CompanionDef("e_bishop",     "대주교",        Rarity.SuperRare, "인간",     "성교회",      HeroClass.Priest,  Alignment.질서, "기적을 일으키는 자"),

        new CompanionDef("l_swordsaint", "검성",          Rarity.Legendary, "인간",     "자유민",      HeroClass.Warrior, Alignment.명예, "천 번의 결투에서 진 적 없다"),
        new CompanionDef("l_windarcher", "바람의 명궁",   Rarity.Legendary, "엘프",     "숲의 부족",   HeroClass.Archer,  Alignment.선,   "바람이 화살을 인도한다"),
        new CompanionDef("l_archmage",   "대마법사",      Rarity.Legendary, "인간",     "마탑",        HeroClass.Mage,    Alignment.질서, "마탑의 주인"),
        new CompanionDef("l_saint",      "성녀",          Rarity.Legendary, "인간",     "성교회",      HeroClass.Priest,  Alignment.선,   "죽은 자도 일으킨다는 전설"),

        new CompanionDef("m_dragon",     "용기사",        Rarity.Mythic,    "용인족",   "용의 계약자", HeroClass.Warrior, Alignment.명예, "용과 계약한 최강의 기사"),
        new CompanionDef("m_sage",       "시간의 현자",   Rarity.Mythic,    "불명",     "시간의 탑",   HeroClass.Mage,    Alignment.중립, "시간을 멈추는 대현자"),
    };

    public const string HeroId = "hero";
    public static CompanionDef Hero => Find(HeroId);
    public bool IsHero => id == HeroId;

    // 이름 없는 일반 캐릭터 (같은 캐릭터를 파티에 여러 명 배치할 수 있음).
    // 여기에 없는 캐릭터는 '이름이 있는 캐릭터'라서 같은 인물을 파티에 두 명 배치할 수 없어요.
    static readonly string[] GenericIds =
    {
        "c_villager", "c_hunter", "c_apprentice", "c_nun",
        "r_mercenary", "r_ranger", "r_pyro", "r_cleric",
    };

    public bool IsNamed => !IsHero && System.Array.IndexOf(GenericIds, id) < 0;

    public static CompanionDef Find(string id)
    {
        foreach (var c in All) if (c.id == id) return c;
        return null;
    }

    // 해당 등급 중 아무나 한 명 (그 등급에 캐릭터가 없으면 한 단계 아래 등급에서)
    public static CompanionDef RandomOf(Rarity rarity)
    {
        for (int r = (int)rarity; r >= 0; r--)
        {
            var list = new List<CompanionDef>();
            foreach (var c in All)
                if ((int)c.rarity == r) list.Add(c);
            if (list.Count > 0) return list[Random.Range(0, list.Count)];
        }
        return All[0];
    }
}
