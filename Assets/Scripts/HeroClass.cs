using UnityEngine;

// 동료의 직업 정보입니다. 여기 숫자는 '노말' 등급 기준이고, 등급이 높을수록 배수로 강해집니다.
// (직업별 숫자는 임시값이에요. 직접 해보면서 조정하면 됩니다.)
public class HeroClass
{
    public string name;        // 화면에 보이는 이름
    public string letter;      // 카드 문양에 쓰는 한 글자
    public string role;        // 짧은 설명
    public Color color;
    public float hp;           // 체력
    public float damage;       // 공격력 (치유 직업은 치유량)
    public float range;        // 공격 사거리
    public float cooldown;     // 공격 간격(초)
    public float speed;        // 이동 속도
    public float splash;       // 범위 공격 반경 (0이면 한 명만 공격)
    public float size;         // 몸 크기
    public bool ranged;        // 원거리 공격(투사체) 여부
    public bool healer;        // 공격 대신 아군을 치유

    // 편성 화면 '자동 배치' 때 먼저 놓아 볼 줄 순서 (0 선두 ~ 4 후미)
    public int[] preferredColumns = { 0, 1, 2, 3, 4 };

    static readonly int[] Front = { 0, 1, 2, 3, 4 };
    static readonly int[] Middle = { 2, 1, 3, 4, 0 };
    static readonly int[] Back = { 3, 2, 4, 1, 0 };
    static readonly int[] Rear = { 4, 3, 2, 1, 0 };

    // ---------- 근접 ----------
    public static readonly HeroClass Soldier = new HeroClass
    {
        name = "하급 병사", letter = "병", role = "근접 · 기본",
        color = new Color(0.60f, 0.55f, 0.45f),
        hp = 90, damage = 9, range = 0.6f, cooldown = 0.9f, speed = 2.5f, size = 0.55f, preferredColumns = Front,
    };

    public static readonly HeroClass Lancer = new HeroClass
    {
        name = "창병", letter = "창", role = "근접 · 긴 창",
        color = new Color(0.40f, 0.62f, 0.95f),
        hp = 110, damage = 13, range = 1.3f, cooldown = 1.0f, speed = 2.5f, size = 0.6f, preferredColumns = Front,
    };

    public static readonly HeroClass Knight = new HeroClass
    {
        name = "기사", letter = "기", role = "근접 · 튼튼함",
        color = new Color(0.55f, 0.65f, 0.80f),
        hp = 160, damage = 11, range = 0.6f, cooldown = 1.0f, speed = 2.3f, size = 0.65f, preferredColumns = Front,
    };

    public static readonly HeroClass Mercenary = new HeroClass
    {
        name = "용병", letter = "검", role = "근접 · 공격형",
        color = new Color(0.80f, 0.45f, 0.30f),
        hp = 100, damage = 15, range = 0.6f, cooldown = 0.9f, speed = 2.6f, size = 0.6f, preferredColumns = Front,
    };

    public static readonly HeroClass Adventurer = new HeroClass
    {
        name = "모험가", letter = "모", role = "근접 · 균형형",
        color = new Color(0.45f, 0.75f, 0.55f),
        hp = 110, damage = 12, range = 0.7f, cooldown = 0.85f, speed = 2.7f, size = 0.6f, preferredColumns = Front,
    };

    public static readonly HeroClass Inspector = new HeroClass
    {
        name = "감찰관", letter = "감", role = "근접 · 빠른 연속 공격",
        color = new Color(0.30f, 0.30f, 0.45f),
        hp = 90, damage = 9, range = 0.6f, cooldown = 0.5f, speed = 2.9f, size = 0.55f, preferredColumns = Front,
    };

    public static readonly HeroClass Porter = new HeroClass
    {
        name = "짐꾼", letter = "짐", role = "근접 · 아주 튼튼함",
        color = new Color(0.70f, 0.55f, 0.35f),
        hp = 170, damage = 8, range = 0.6f, cooldown = 1.1f, speed = 2.3f, size = 0.6f, preferredColumns = Front,
    };

    // ---------- 원거리 ----------
    public static readonly HeroClass Archer = new HeroClass
    {
        name = "궁수", letter = "궁", role = "원거리 · 단일 공격",
        color = new Color(0.35f, 0.85f, 0.40f),
        hp = 60, damage = 10, range = 8f, cooldown = 1.0f, speed = 2.6f, size = 0.5f, ranged = true, preferredColumns = Middle,
    };

    public static readonly HeroClass Mage = new HeroClass
    {
        name = "마법사", letter = "마", role = "원거리 · 범위 공격",
        color = new Color(0.75f, 0.45f, 1.00f),
        hp = 50, damage = 8, range = 7f, cooldown = 1.6f, speed = 2.4f, size = 0.5f, splash = 1.2f, ranged = true, preferredColumns = Back,
    };

    public static readonly HeroClass Dragon = new HeroClass
    {
        name = "폴리모프 용", letter = "용", role = "원거리 · 넓은 범위 공격",
        color = new Color(0.90f, 0.35f, 0.30f),
        hp = 140, damage = 16, range = 6f, cooldown = 1.8f, speed = 2.4f, size = 0.6f, splash = 1.6f, ranged = true, preferredColumns = Middle,
    };

    // ---------- 치유 ----------
    public static readonly HeroClass Saint = new HeroClass
    {
        name = "성녀", letter = "성", role = "아군 치유 · 강함",
        color = new Color(1.00f, 0.92f, 0.55f),
        hp = 70, damage = 18, range = 7.5f, cooldown = 1.4f, speed = 2.4f, size = 0.5f, healer = true, preferredColumns = Rear,
    };

    public static readonly HeroClass Healer = new HeroClass
    {
        name = "힐러", letter = "힐", role = "아군 치유",
        color = new Color(0.95f, 0.80f, 0.75f),
        hp = 60, damage = 13, range = 7.5f, cooldown = 1.3f, speed = 2.4f, size = 0.5f, healer = true, preferredColumns = Rear,
    };

    // 용사 전용 직업
    public static readonly HeroClass Brave = new HeroClass
    {
        name = "용사", letter = "용", role = "근접 · 만능",
        color = new Color(1.00f, 0.82f, 0.25f),
        hp = 150, damage = 14, range = 0.7f, cooldown = 0.8f, speed = 2.6f, size = 0.7f, preferredColumns = Front,
    };
}
