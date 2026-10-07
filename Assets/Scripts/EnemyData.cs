using System.Collections.Generic;
using UnityEngine;

// 적 정보 (전투와 도감에서 사용)
//   Normal  : 일반 적 (여러 마리)
//   Elite   : 이름 있는 적 '(1)' - 다른 적과 함께 나오지만 한 전투에 한 명만
//   MidBoss : 5라운드 중간 보스
//   Boss    : 10라운드 스테이지 보스
public enum EnemyType { Normal, Elite, MidBoss, Boss }

// 싸우는 방식 (능력치가 여기서 정해져요)
public enum EnemyRole
{
    Melee,   // 근접 기본
    Fast,    // 빠르고 약함
    Tank,    // 튼튼함
    Brute,   // 크고 힘셈
    Reach,   // 창 (조금 긴 사거리)
    Ranged,  // 활 · 석궁 · 투석
    Caster,  // 마법 (범위 공격)
    Healer,  // 아군 치유
}

// 그림이 없을 때 코드로 그리는 몸 모양 (EnemyLook.cs)
public enum EnemyShape { Humanoid, Beast, Bear, Lynx, Scorpion, Wyvern, Worm, Golem, Mermaid, Shark, Kraken }

// 사람 모양 적이 드는 무기
public enum EnemyWeapon { None, Sword, Dagger, Spear, Bow, Crossbow, Staff, Hammer, Axe, Sling }

// 사람 모양 적의 꾸밈 (여러 개를 | 로 겹칠 수 있음)
[System.Flags]
public enum EnemyDeco
{
    None = 0, Hood = 1, Helmet = 2, ElfEars = 4, Beard = 8, Tusks = 16, Wings = 32, Halo = 64,
    Crown = 128, Shield = 256, Headband = 512, LongHair = 1024, Cape = 2048, Mask = 4096,
}

public class EnemyDef
{
    public string id;
    public string name;
    public EnemyType type;
    public EnemyRole role;
    public int stage = -1;     // 나오는 스테이지 (0부터). 여러 스테이지에 나오는 적은 -1
    public string desc;

    // 겉모습
    public EnemyShape shape;
    public EnemyWeapon weapon;
    public EnemyDeco deco;
    public Color color;        // 몸 · 옷 주 색
    public Color accent;       // 무기 · 장식 · 눈 색
    public Color skin = new Color(0.93f, 0.78f, 0.64f);
    public float size = 0.5f;  // 몸 크기 (충돌 · 그림 크기)

    public string TypeName =>
        type == EnemyType.Boss ? "스테이지 보스" : type == EnemyType.MidBoss ? "중간 보스" : type == EnemyType.Elite ? "정예" : "일반";
    public bool IsNamed => type != EnemyType.Normal;
    public bool IsRanged => role == EnemyRole.Ranged || role == EnemyRole.Caster;

    // ---------------- 능력치 ----------------
    // 일반 적: 역할별 기본값 / 정예 · 보스: 등급별 기본값 x 역할 배율
    // (판이 올라갈수록 BattleManager에서 체력 · 공격력이 더 올라가요)
    public float Hp => type == EnemyType.Normal ? RoleHp[(int)role] : TierHp * RoleHpFactor[(int)role];
    public float Damage => type == EnemyType.Normal ? RoleDmg[(int)role] : TierDmg * RoleDmgFactor[(int)role];
    public float Range => RoleRange[(int)role];
    public float Cooldown => RoleCooldown[(int)role];
    public float Speed => RoleSpeed[(int)role] * (type == EnemyType.Boss || type == EnemyType.MidBoss ? 0.6f : 1f);
    public float Splash => role == EnemyRole.Caster ? 0.9f : 0f;
    public int Gold => type == EnemyType.Boss ? 150 : type == EnemyType.MidBoss ? 60 : type == EnemyType.Elite ? 20 : RoleGold[(int)role];

    float TierHp => type == EnemyType.Boss ? 500f : type == EnemyType.MidBoss ? 300f : 110f;
    float TierDmg => type == EnemyType.Boss ? 20f : type == EnemyType.MidBoss ? 14f : 9f;

    //                                         근접   빠름   튼튼   힘셈   창     원거리 마법   치유
    static readonly float[] RoleHp =        { 32f,   22f,   70f,   120f,  34f,   20f,   22f,   26f };
    static readonly float[] RoleDmg =       { 5f,    4f,    5f,    11f,   6f,    6f,    5f,    7f };
    static readonly float[] RoleRange =     { 0.4f,  0.4f,  0.5f,  0.5f,  1.1f,  4f,    3.8f,  3.5f };
    static readonly float[] RoleCooldown =  { 1.0f,  0.7f,  1.3f,  1.3f,  1.1f,  1.5f,  1.8f,  1.6f };
    static readonly float[] RoleSpeed =     { 1.6f,  2.3f,  1.1f,  1.0f,  1.5f,  1.3f,  1.2f,  1.3f };
    static readonly int[] RoleGold =        { 3,     3,     6,     10,    4,     4,     5,     5 };
    static readonly float[] RoleHpFactor =  { 1f,    0.75f, 1.4f,  1.3f,  1f,    0.7f,  0.7f,  0.8f };
    static readonly float[] RoleDmgFactor = { 1f,    0.8f,  0.8f,  1.3f,  1f,    1f,    1f,    0.9f };

    // ---------------- 적 목록 ----------------

    static List<EnemyDef> all;

    public static List<EnemyDef> All
    {
        get
        {
            if (all != null) return all;
            all = new List<EnemyDef>();
            Build();
            return all;
        }
    }

    public static EnemyDef Find(string id) => All.Find(e => e.id == id);

    // 짧게 쓰기 위한 도우미
    static EnemyDef Add(int stage, string id, string name, EnemyType type, EnemyRole role, EnemyShape shape,
        Color color, Color accent, float size, EnemyWeapon weapon = EnemyWeapon.None, EnemyDeco deco = EnemyDeco.None, Color? skin = null)
    {
        var d = new EnemyDef
        {
            id = id, name = name, type = type, role = role, shape = shape, color = color, accent = accent,
            size = size, weapon = weapon, deco = deco, stage = stage,
        };
        if (skin.HasValue) d.skin = skin.Value;
        d.desc = stage >= 0 ? $"'{Stages.StageNameByIndex(stage)}'에서 만날 수 있다" : "어디에나 있는 마왕군";
        all.Add(d);
        return d;
    }

    static Color C(float r, float g, float b) => new Color(r, g, b);

    const EnemyType N = EnemyType.Normal, E = EnemyType.Elite, M = EnemyType.MidBoss, B = EnemyType.Boss;

    static readonly Color Steel = new Color(0.78f, 0.80f, 0.84f);
    static readonly Color Wood = new Color(0.52f, 0.36f, 0.20f);
    static readonly Color GoldC = new Color(0.95f, 0.78f, 0.30f);
    static readonly Color OrcSkin = new Color(0.45f, 0.62f, 0.32f);
    static readonly Color ElfSkin = new Color(0.96f, 0.86f, 0.74f);
    static readonly Color MerSkin = new Color(0.70f, 0.88f, 0.86f);
    static readonly Color AngelSkin = new Color(0.99f, 0.92f, 0.84f);

    static void Build()
    {
        // ---- 1. 올 왕국 외곽 숲 ----
        Add(0, "wolf", "늑대", N, EnemyRole.Fast, EnemyShape.Beast, C(0.48f, 0.50f, 0.55f), C(1f, 0.85f, 0.3f), 0.5f);
        Add(0, "bear", "곰", N, EnemyRole.Brute, EnemyShape.Bear, C(0.42f, 0.28f, 0.18f), C(0.95f, 0.85f, 0.6f), 0.8f);
        Add(0, "big_bear", "커다란 곰", M, EnemyRole.Brute, EnemyShape.Bear, C(0.36f, 0.22f, 0.14f), C(1f, 0.4f, 0.3f), 1.5f);
        Add(0, "bandit1", "도적1", N, EnemyRole.Melee, EnemyShape.Humanoid, C(0.35f, 0.30f, 0.25f), Steel, 0.5f, EnemyWeapon.Dagger, EnemyDeco.Mask);
        Add(0, "bandit2", "도적2", N, EnemyRole.Ranged, EnemyShape.Humanoid, C(0.30f, 0.36f, 0.24f), Wood, 0.5f, EnemyWeapon.Bow, EnemyDeco.Hood);
        Add(0, "bandit3", "도적3", N, EnemyRole.Tank, EnemyShape.Humanoid, C(0.45f, 0.32f, 0.22f), Steel, 0.6f, EnemyWeapon.Axe, EnemyDeco.Beard);
        Add(0, "oslo", "오슬로", E, EnemyRole.Brute, EnemyShape.Humanoid, C(0.55f, 0.25f, 0.18f), Steel, 0.7f, EnemyWeapon.Axe, EnemyDeco.Beard | EnemyDeco.Headband);
        Add(0, "shilla", "쉴라", E, EnemyRole.Ranged, EnemyShape.Humanoid, C(0.28f, 0.42f, 0.30f), Wood, 0.6f, EnemyWeapon.Bow, EnemyDeco.LongHair | EnemyDeco.Hood);
        Add(0, "frank", "도적 두목 프랑크", B, EnemyRole.Melee, EnemyShape.Humanoid, C(0.50f, 0.18f, 0.16f), GoldC, 1.0f, EnemyWeapon.Sword, EnemyDeco.Beard | EnemyDeco.Cape);

        // ---- 2. 동부 평원 ----
        Add(1, "elf_archer", "엘프 궁병", N, EnemyRole.Ranged, EnemyShape.Humanoid, C(0.30f, 0.55f, 0.35f), Wood, 0.5f, EnemyWeapon.Bow, EnemyDeco.ElfEars | EnemyDeco.LongHair, ElfSkin);
        Add(1, "elf_tracker", "엘프 추적자", N, EnemyRole.Fast, EnemyShape.Humanoid, C(0.25f, 0.40f, 0.28f), Steel, 0.5f, EnemyWeapon.Dagger, EnemyDeco.ElfEars | EnemyDeco.Hood, ElfSkin);
        Add(1, "julian", "엘프 경비대장 줄리안", M, EnemyRole.Ranged, EnemyShape.Humanoid, C(0.20f, 0.50f, 0.40f), GoldC, 1.0f, EnemyWeapon.Bow, EnemyDeco.ElfEars | EnemyDeco.Cape | EnemyDeco.LongHair, ElfSkin);
        Add(1, "orc1", "초원 오크1", N, EnemyRole.Melee, EnemyShape.Humanoid, C(0.50f, 0.38f, 0.25f), Steel, 0.55f, EnemyWeapon.Axe, EnemyDeco.Tusks, OrcSkin);
        Add(1, "orc2", "초원 오크2", N, EnemyRole.Brute, EnemyShape.Humanoid, C(0.42f, 0.30f, 0.22f), Wood, 0.75f, EnemyWeapon.Hammer, EnemyDeco.Tusks, OrcSkin);
        Add(1, "orc3", "초원 오크3", N, EnemyRole.Ranged, EnemyShape.Humanoid, C(0.55f, 0.45f, 0.30f), Wood, 0.55f, EnemyWeapon.Sling, EnemyDeco.Tusks | EnemyDeco.Headband, OrcSkin);
        Add(1, "wengaram", "오크 전사 웬가람", E, EnemyRole.Brute, EnemyShape.Humanoid, C(0.55f, 0.20f, 0.15f), Steel, 0.75f, EnemyWeapon.Axe, EnemyDeco.Tusks | EnemyDeco.Helmet, OrcSkin);
        Add(1, "fingaram", "오크 전사장 핀가람", B, EnemyRole.Brute, EnemyShape.Humanoid, C(0.40f, 0.15f, 0.12f), GoldC, 1.1f, EnemyWeapon.Axe, EnemyDeco.Tusks | EnemyDeco.Helmet | EnemyDeco.Cape, OrcSkin);

        // ---- 3. 올드락 산맥 ----
        Add(2, "wyvern", "와이번", N, EnemyRole.Melee, EnemyShape.Wyvern, C(0.45f, 0.55f, 0.35f), C(1f, 0.8f, 0.3f), 0.6f);
        Add(2, "scorpion", "산맥 전갈", N, EnemyRole.Tank, EnemyShape.Scorpion, C(0.55f, 0.40f, 0.25f), C(0.9f, 0.3f, 0.2f), 0.6f);
        Add(2, "lynx", "스라소니", N, EnemyRole.Fast, EnemyShape.Lynx, C(0.70f, 0.58f, 0.40f), C(0.4f, 1f, 0.5f), 0.5f);
        Add(2, "baby_worm", "새끼 산맥 샌드웜", N, EnemyRole.Melee, EnemyShape.Worm, C(0.72f, 0.60f, 0.42f), C(0.9f, 0.3f, 0.3f), 0.55f);
        Add(2, "sandworm", "산맥 샌드웜", M, EnemyRole.Brute, EnemyShape.Worm, C(0.66f, 0.52f, 0.35f), C(0.95f, 0.3f, 0.25f), 1.6f);
        Add(2, "dwarf_guard", "드워프 경비병", N, EnemyRole.Melee, EnemyShape.Humanoid, C(0.40f, 0.35f, 0.55f), Steel, 0.5f, EnemyWeapon.Axe, EnemyDeco.Beard | EnemyDeco.Helmet);
        Add(2, "dwarf_shield", "드워프 방패병", N, EnemyRole.Tank, EnemyShape.Humanoid, C(0.35f, 0.40f, 0.50f), Steel, 0.55f, EnemyWeapon.Hammer, EnemyDeco.Beard | EnemyDeco.Helmet | EnemyDeco.Shield);
        Add(2, "dwarf_slinger", "드워프 투석병", N, EnemyRole.Caster, EnemyShape.Humanoid, C(0.50f, 0.38f, 0.30f), C(0.6f, 0.6f, 0.6f), 0.5f, EnemyWeapon.Sling, EnemyDeco.Beard);
        Add(2, "talim", "드워프 문지기 대장 탈림", B, EnemyRole.Tank, EnemyShape.Humanoid, C(0.30f, 0.30f, 0.45f), GoldC, 1.0f, EnemyWeapon.Hammer, EnemyDeco.Beard | EnemyDeco.Helmet | EnemyDeco.Shield);

        // ---- 4. 카니 해안 ----
        Add(3, "mer_patrol", "인어 순찰대", N, EnemyRole.Melee, EnemyShape.Mermaid, C(0.25f, 0.55f, 0.65f), Steel, 0.5f, EnemyWeapon.Sword, EnemyDeco.LongHair, MerSkin);
        Add(3, "mer_spear", "인어 창병", N, EnemyRole.Reach, EnemyShape.Mermaid, C(0.20f, 0.45f, 0.60f), Steel, 0.5f, EnemyWeapon.Spear, EnemyDeco.Helmet, MerSkin);
        Add(3, "mer_shaman", "인어 주술사", N, EnemyRole.Caster, EnemyShape.Mermaid, C(0.40f, 0.35f, 0.65f), C(0.4f, 1f, 0.9f), 0.5f, EnemyWeapon.Staff, EnemyDeco.LongHair | EnemyDeco.Headband, MerSkin);
        Add(3, "mudiar", "인어 순찰대장 무디아르", M, EnemyRole.Reach, EnemyShape.Mermaid, C(0.15f, 0.40f, 0.55f), GoldC, 1.1f, EnemyWeapon.Spear, EnemyDeco.Helmet | EnemyDeco.Cape, MerSkin);
        Add(3, "shark", "거대 상어", N, EnemyRole.Brute, EnemyShape.Shark, C(0.45f, 0.52f, 0.62f), C(1f, 1f, 1f), 0.8f);
        Add(3, "pirate", "해적", N, EnemyRole.Melee, EnemyShape.Humanoid, C(0.60f, 0.20f, 0.18f), Steel, 0.5f, EnemyWeapon.Sword, EnemyDeco.Headband | EnemyDeco.Beard);
        Add(3, "mer_pirate", "인어 해적", N, EnemyRole.Ranged, EnemyShape.Mermaid, C(0.55f, 0.22f, 0.25f), Wood, 0.5f, EnemyWeapon.Crossbow, EnemyDeco.Headband, MerSkin);
        Add(3, "kraken", "크라켄", B, EnemyRole.Brute, EnemyShape.Kraken, C(0.55f, 0.25f, 0.40f), C(1f, 0.85f, 0.3f), 1.8f);

        // ---- 5. 포빌리아 왕국 검문소 ----
        Color pov = C(0.62f, 0.15f, 0.20f);
        Add(4, "pov_sword", "포빌리아 검병", N, EnemyRole.Melee, EnemyShape.Humanoid, pov, Steel, 0.5f, EnemyWeapon.Sword, EnemyDeco.Helmet);
        Add(4, "pov_archer", "포빌리아 궁수", N, EnemyRole.Ranged, EnemyShape.Humanoid, pov, Wood, 0.5f, EnemyWeapon.Bow, EnemyDeco.Hood);
        Add(4, "pov_spear", "포빌리아 창병", N, EnemyRole.Reach, EnemyShape.Humanoid, pov, Steel, 0.5f, EnemyWeapon.Spear, EnemyDeco.Helmet);
        Add(4, "kellin", "포빌리아 기병대장 켈린", M, EnemyRole.Reach, EnemyShape.Humanoid, C(0.55f, 0.12f, 0.18f), GoldC, 1.1f, EnemyWeapon.Spear, EnemyDeco.Helmet | EnemyDeco.Cape | EnemyDeco.Shield);
        Add(4, "pov_knight", "포빌리아 기사", N, EnemyRole.Tank, EnemyShape.Humanoid, C(0.65f, 0.65f, 0.70f), pov, 0.6f, EnemyWeapon.Sword, EnemyDeco.Helmet | EnemyDeco.Shield);
        Add(4, "pov_xbow", "포빌리아 석궁병", N, EnemyRole.Ranged, EnemyShape.Humanoid, C(0.50f, 0.14f, 0.18f), Wood, 0.5f, EnemyWeapon.Crossbow, EnemyDeco.Helmet);
        Add(4, "pov_assassin", "포빌리아 암살자", N, EnemyRole.Fast, EnemyShape.Humanoid, C(0.18f, 0.16f, 0.22f), Steel, 0.5f, EnemyWeapon.Dagger, EnemyDeco.Hood | EnemyDeco.Mask);
        Add(4, "antonio", "포빌리아 기사장 안토니오", E, EnemyRole.Tank, EnemyShape.Humanoid, C(0.75f, 0.75f, 0.80f), GoldC, 0.8f, EnemyWeapon.Sword, EnemyDeco.Helmet | EnemyDeco.Shield | EnemyDeco.Cape);
        Add(4, "sine", "포빌리아 메이드장 시네", E, EnemyRole.Fast, EnemyShape.Humanoid, C(0.15f, 0.15f, 0.20f), Steel, 0.7f, EnemyWeapon.Dagger, EnemyDeco.Headband | EnemyDeco.LongHair);
        Add(4, "iris", "포빌리아 제4공주 이리스", B, EnemyRole.Caster, EnemyShape.Humanoid, C(0.70f, 0.20f, 0.45f), C(1f, 0.5f, 0.9f), 1.0f, EnemyWeapon.Staff, EnemyDeco.Crown | EnemyDeco.LongHair | EnemyDeco.Cape);

        // ---- 6. 은혜의 땅 ----
        Color stone = C(0.62f, 0.60f, 0.55f);
        Add(5, "golem_battle", "전투 골렘", N, EnemyRole.Melee, EnemyShape.Golem, stone, C(1f, 0.5f, 0.2f), 0.6f);
        Add(5, "golem_guard", "방어 골렘", N, EnemyRole.Tank, EnemyShape.Golem, C(0.50f, 0.52f, 0.56f), C(0.4f, 0.7f, 1f), 0.7f);
        Add(5, "golem_magic", "마법 골렘", N, EnemyRole.Caster, EnemyShape.Golem, C(0.55f, 0.50f, 0.65f), C(0.8f, 0.5f, 1f), 0.6f);
        Add(5, "giant_golem", "거대한 골렘", M, EnemyRole.Tank, EnemyShape.Golem, C(0.58f, 0.56f, 0.50f), C(1f, 0.85f, 0.3f), 1.7f);
        Color white = C(0.92f, 0.90f, 0.84f);
        Add(5, "angel_warrior", "천족 전사", N, EnemyRole.Melee, EnemyShape.Humanoid, white, GoldC, 0.5f, EnemyWeapon.Sword, EnemyDeco.Wings | EnemyDeco.Helmet, AngelSkin);
        Add(5, "angel_mage", "천족 마법사", N, EnemyRole.Caster, EnemyShape.Humanoid, C(0.75f, 0.80f, 0.95f), C(0.6f, 0.85f, 1f), 0.5f, EnemyWeapon.Staff, EnemyDeco.Wings | EnemyDeco.LongHair, AngelSkin);
        Add(5, "angel_monk", "천족 수도사", N, EnemyRole.Fast, EnemyShape.Humanoid, C(0.85f, 0.75f, 0.55f), GoldC, 0.5f, EnemyWeapon.None, EnemyDeco.Wings | EnemyDeco.Headband, AngelSkin);
        Add(5, "angel_healer", "천족 치유사", N, EnemyRole.Healer, EnemyShape.Humanoid, white, C(0.5f, 1f, 0.6f), 0.5f, EnemyWeapon.Staff, EnemyDeco.Wings | EnemyDeco.Halo | EnemyDeco.LongHair, AngelSkin);
        Add(5, "belena", "천사장 벨레나", B, EnemyRole.Caster, EnemyShape.Humanoid, C(0.95f, 0.92f, 0.80f), GoldC, 1.1f, EnemyWeapon.Spear, EnemyDeco.Wings | EnemyDeco.Halo | EnemyDeco.LongHair | EnemyDeco.Cape, AngelSkin);

        // ---- 7~10 스테이지 (아직 대본이 없어서 예전 적을 씀) ----
        Add(-1, "goblin", "고블린", N, EnemyRole.Melee, EnemyShape.Humanoid, C(0.45f, 0.35f, 0.25f), Steel, 0.45f, EnemyWeapon.Dagger, EnemyDeco.Tusks, C(0.55f, 0.70f, 0.30f));
        Add(-1, "goblin_archer", "고블린 궁수", N, EnemyRole.Ranged, EnemyShape.Humanoid, C(0.40f, 0.40f, 0.25f), Wood, 0.45f, EnemyWeapon.Bow, EnemyDeco.Hood, C(0.55f, 0.70f, 0.30f));
        Add(-1, "ogre", "오우거", N, EnemyRole.Brute, EnemyShape.Humanoid, C(0.45f, 0.30f, 0.25f), Wood, 0.9f, EnemyWeapon.Hammer, EnemyDeco.Tusks, C(0.70f, 0.55f, 0.45f));
        for (int s = 6; s < Stages.StageCount; s++)
        {
            bool demonKing = s == Stages.StageCount - 1;
            Add(s, MidBossId(s), Stages.LegacyMidBossName(s), M, EnemyRole.Melee, EnemyShape.Humanoid, C(0.40f, 0.20f, 0.45f), C(1f, 0.4f, 0.3f), 1.2f,
                EnemyWeapon.Sword, EnemyDeco.Helmet | EnemyDeco.Cape, C(0.6f, 0.5f, 0.6f));
            Add(s, BossId(s), Stages.LegacyBossName(s), B, EnemyRole.Brute, EnemyShape.Humanoid,
                demonKing ? C(0.15f, 0.04f, 0.08f) : C(0.35f, 0.12f, 0.40f), C(1f, 0.25f, 0.2f), demonKing ? 1.5f : 1.2f,
                EnemyWeapon.Sword, EnemyDeco.Crown | EnemyDeco.Cape | EnemyDeco.Tusks, C(0.55f, 0.35f, 0.45f));
        }
    }

    // 7~10 스테이지 보스 id (예전 저장의 도감 기록과 맞추기 위해 그대로 둠)
    public static string MidBossId(int stage) => "mid_" + (stage + 1);
    public static string BossId(int stage) => "boss_" + (stage + 1);
}
