using UnityEngine;

// 동료의 직업 정보입니다. 여기 숫자는 '일반' 등급 기준이고, 등급이 높을수록 배수로 강해집니다.
public class HeroClass
{
    public string name;        // 화면에 보이는 이름
    public string letter;      // 카드 문양에 쓰는 한 글자
    public string role;        // 짧은 설명
    public string synergyText; // 3명/6명 모였을 때 효과 설명
    public Color color;
    public float hp;           // 체력
    public float damage;       // 공격력 (사제는 치유량)
    public float range;        // 공격 사거리
    public float cooldown;     // 공격 간격(초)
    public float speed;        // 이동 속도
    public float splash;       // 범위 공격 반경 (0이면 한 명만 공격)
    public float size;         // 몸 크기
    public bool ranged;        // 원거리 공격(투사체) 여부
    public bool healer;        // 공격 대신 아군을 치유

    public static readonly HeroClass Soldier = new HeroClass
    {
        name = "하급 병사", letter = "병", role = "근접 · 기본", synergyText = "공격력 증가",
        color = new Color(0.60f, 0.55f, 0.45f),
        hp = 90, damage = 9, range = 0.6f, cooldown = 0.9f, speed = 2.5f, size = 0.55f,
    };

    public static readonly HeroClass Warrior = new HeroClass
    {
        name = "전사", letter = "전", role = "근접 · 튼튼함", synergyText = "받는 피해 감소",
        color = new Color(0.30f, 0.60f, 1.00f),
        hp = 120, damage = 12, range = 0.6f, cooldown = 0.8f, speed = 2.6f, size = 0.6f,
    };

    public static readonly HeroClass Archer = new HeroClass
    {
        name = "궁수", letter = "궁", role = "원거리 · 단일 공격", synergyText = "공격 속도 증가",
        color = new Color(0.35f, 0.85f, 0.40f),
        hp = 60, damage = 10, range = 8f, cooldown = 1.0f, speed = 2.6f, size = 0.5f, ranged = true,
    };

    public static readonly HeroClass Mage = new HeroClass
    {
        name = "마법사", letter = "마", role = "원거리 · 범위 공격", synergyText = "폭발 범위 증가",
        color = new Color(0.75f, 0.45f, 1.00f),
        hp = 50, damage = 8, range = 7f, cooldown = 1.6f, speed = 2.4f, size = 0.5f, splash = 1.2f, ranged = true,
    };

    public static readonly HeroClass Priest = new HeroClass
    {
        name = "사제", letter = "사", role = "아군 치유", synergyText = "치유량 증가",
        color = new Color(1.00f, 0.92f, 0.55f),
        hp = 55, damage = 12, range = 7.5f, cooldown = 1.4f, speed = 2.4f, size = 0.5f, healer = true,
    };

    // 용사 전용 직업 (시너지 계산에는 들어가지 않음)
    public static readonly HeroClass Brave = new HeroClass
    {
        name = "용사", letter = "용", role = "근접 · 만능", synergyText = "",
        color = new Color(1.00f, 0.82f, 0.25f),
        hp = 150, damage = 14, range = 0.7f, cooldown = 0.8f, speed = 2.6f, size = 0.7f,
    };

    // 시너지가 있는 직업들
    public static readonly HeroClass[] All = { Soldier, Warrior, Archer, Mage, Priest };
}
