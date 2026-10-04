using System.Collections.Generic;
using UnityEngine;

// 동료 등급: 일반 < 희귀 < 영웅 < 전설 < 신화
public enum Rarity { Common, Rare, Epic, Legendary, Mythic }

public static class RarityInfo
{
    static readonly string[] names = { "일반", "희귀", "영웅", "전설", "신화" };
    static readonly Color[] colors =
    {
        new Color(0.75f, 0.75f, 0.75f), // 일반: 회색
        new Color(0.30f, 0.65f, 1.00f), // 희귀: 파랑
        new Color(0.75f, 0.40f, 1.00f), // 영웅: 보라
        new Color(1.00f, 0.78f, 0.20f), // 전설: 금색
        new Color(1.00f, 0.30f, 0.35f), // 신화: 빨강 (화면에서는 무지개로 빛남)
    };
    // 등급별 능력치 배수 (일반 = 1배)
    static readonly float[] statMultipliers = { 1f, 1.5f, 2.2f, 3.2f, 4.5f };

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

// 뽑을 수 있는 동료 한 명의 정보
public class CompanionDef
{
    public string id;
    public string name;
    public string desc;
    public Rarity rarity;
    public HeroClass job;

    CompanionDef(string id, string name, Rarity rarity, HeroClass job, string desc)
    {
        this.id = id; this.name = name; this.rarity = rarity; this.job = job; this.desc = desc;
    }

    // ★ 새 동료를 추가하려면 이 목록에 한 줄을 추가하세요. (id는 겹치지 않게)
    public static readonly CompanionDef[] All =
    {
        new CompanionDef("c_villager",   "마을 청년",     Rarity.Common,    HeroClass.Warrior, "괭이 대신 검을 든 용감한 청년"),
        new CompanionDef("c_hunter",     "사냥꾼",        Rarity.Common,    HeroClass.Archer,  "토끼 사냥이 특기"),
        new CompanionDef("c_apprentice", "견습 마법사",   Rarity.Common,    HeroClass.Mage,    "가끔 주문을 틀린다"),
        new CompanionDef("c_nun",        "수녀",          Rarity.Common,    HeroClass.Priest,  "기도로 상처를 낫게 한다"),

        new CompanionDef("r_mercenary",  "용병 검사",     Rarity.Rare,      HeroClass.Warrior, "돈만 주면 어디든 간다"),
        new CompanionDef("r_ranger",     "숲의 궁수",     Rarity.Rare,      HeroClass.Archer,  "숲에서 자란 명사수"),
        new CompanionDef("r_pyro",       "불꽃 마법사",   Rarity.Rare,      HeroClass.Mage,    "모든 것을 태워 버린다"),
        new CompanionDef("r_cleric",     "성당 사제",     Rarity.Rare,      HeroClass.Priest,  "왕국 대성당 출신"),

        new CompanionDef("e_paladin",    "성기사",        Rarity.Epic,      HeroClass.Warrior, "신의 방패를 든 기사"),
        new CompanionDef("e_sniper",     "그림자 저격수", Rarity.Epic,      HeroClass.Archer,  "한 발이면 충분하다"),
        new CompanionDef("e_frost",      "얼음 마녀",     Rarity.Epic,      HeroClass.Mage,    "북쪽 설산의 마녀"),
        new CompanionDef("e_bishop",     "대주교",        Rarity.Epic,      HeroClass.Priest,  "기적을 일으키는 자"),

        new CompanionDef("l_swordsaint", "검성",          Rarity.Legendary, HeroClass.Warrior, "천 번의 결투에서 진 적 없다"),
        new CompanionDef("l_windarcher", "바람의 명궁",   Rarity.Legendary, HeroClass.Archer,  "바람이 화살을 인도한다"),
        new CompanionDef("l_archmage",   "대마법사",      Rarity.Legendary, HeroClass.Mage,    "마탑의 주인"),
        new CompanionDef("l_saint",      "성녀",          Rarity.Legendary, HeroClass.Priest,  "죽은 자도 일으킨다는 전설"),

        new CompanionDef("m_dragon",     "용기사",        Rarity.Mythic,    HeroClass.Warrior, "용과 계약한 최강의 기사"),
        new CompanionDef("m_sage",       "시간의 현자",   Rarity.Mythic,    HeroClass.Mage,    "시간을 멈추는 대현자"),
    };

    public static CompanionDef Find(string id)
    {
        foreach (var c in All) if (c.id == id) return c;
        return null;
    }

    // 해당 등급 중 아무나 한 명 (그 등급이 없으면 한 단계 아래 등급에서)
    public static CompanionDef RandomOf(Rarity rarity)
    {
        for (int r = (int)rarity; r >= 0; r--)
        {
            var list = new List<CompanionDef>();
            foreach (var c in All) if ((int)c.rarity == r) list.Add(c);
            if (list.Count > 0) return list[Random.Range(0, list.Count)];
        }
        return All[0];
    }
}
