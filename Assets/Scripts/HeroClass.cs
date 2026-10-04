using UnityEngine;

// 영웅 직업 정보입니다. 숫자를 바꾸면 게임 밸런스가 바뀝니다.
public class HeroClass
{
    public string name;        // 화면에 보이는 이름
    public string role;        // 짧은 설명
    public string synergyText; // 3명/6명 모였을 때 효과 설명
    public Color color;
    public int baseCost;       // 고용 기본 가격 (같은 직업이 늘수록 비싸짐)
    public float hp;           // 체력
    public float damage;       // 공격력
    public float range;        // 공격 사거리
    public float cooldown;     // 공격 간격(초)
    public float speed;        // 이동 속도
    public float splash;       // 범위 공격 반경 (0이면 한 명만 공격)
    public float size;         // 몸 크기
    public bool ranged;        // 원거리 공격(투사체) 여부

    public static readonly HeroClass Warrior = new HeroClass
    {
        name = "전사", role = "근접 · 튼튼함", synergyText = "받는 피해 감소",
        color = new Color(0.30f, 0.60f, 1.00f), baseCost = 10,
        hp = 120, damage = 12, range = 0.6f, cooldown = 0.8f, speed = 2.6f, size = 0.6f,
    };

    public static readonly HeroClass Archer = new HeroClass
    {
        name = "궁수", role = "원거리 · 단일 공격", synergyText = "공격 속도 증가",
        color = new Color(0.35f, 0.85f, 0.40f), baseCost = 15,
        hp = 60, damage = 10, range = 6.5f, cooldown = 1.0f, speed = 2.6f, size = 0.5f, ranged = true,
    };

    public static readonly HeroClass Mage = new HeroClass
    {
        name = "마법사", role = "원거리 · 범위 공격", synergyText = "폭발 범위 증가",
        color = new Color(0.75f, 0.45f, 1.00f), baseCost = 20,
        hp = 50, damage = 8, range = 5.5f, cooldown = 1.6f, speed = 2.4f, size = 0.5f, splash = 1.2f, ranged = true,
    };

    public static readonly HeroClass[] All = { Warrior, Archer, Mage };
}
