using UnityEngine;

// 스테이지 정보: 10스테이지 x 10라운드 = 총 100판 (1-1 ~ 10-10).
// 게임 안에서는 판마다 1부터 100까지 번호(level)를 씁니다. 예) 3-7 = 27번째 판
// 각 스테이지의 5라운드에는 중간 보스, 10라운드에는 스테이지 보스가 나옵니다.
public static class Stages
{
    public const int StageCount = 10;
    public const int LevelsPerStage = 10;
    public const int Count = StageCount * LevelsPerStage; // 100판
    public const int WavesPerLevel = 2;
    public const int MidBossRound = 5;

    // ★ 스테이지 이름
    static readonly string[] stageNames =
    {
        "올 왕국 외곽 숲", "동부 평원", "올드락 산맥", "카니 해안", "포빌리아 왕국 검문소",
        "은혜의 땅", "노란 용의 굴", "붉은 정글", "옛 대전쟁터", "마왕성",
    };

    // ★ 중간 보스 (5라운드)와 스테이지 보스 (10라운드) 이름 (임시)
    static readonly string[] midBossNames =
    {
        "숲 도적 두목", "평원 늑대 우두머리", "산적 대장", "해적 갑판장", "검문소 경비대장",
        "타락한 수도사", "새끼 황룡", "정글 주술사", "망령 기사", "마왕군 사천왕",
    };
    static readonly string[] bossNames =
    {
        "거대 멧돼지 왕", "켄타우로스 족장", "바위 거인", "거대 게 카니", "포빌리아 기사단장",
        "거짓 성자", "노란 용", "붉은 표범 여왕", "전쟁의 망령 장군", "마왕",
    };

    // ★ 스테이지 보스 등장 대사 (컷씬). 보스를 한 번 이기면 다음부터는 건너뛸 수 있어요.
    static readonly string[][] bossLines =
    {
        new[] { "꾸에에엑! 내 숲에 발을 들인 놈이 누구냐!", "네놈들 전부 오늘 저녁거리다!" },
        new[] { "인간 따위가 이 평원을 달릴 자격이 있다고 생각하나?", "켄타우로스의 창을 받아라!" },
        new[] { "......쿠구구구.", "산을... 어지럽히는 자... 돌이 되어라......" },
        new[] { "철컥철컥! 이 해안은 내 집게발 아래에 있다!", "모래 속에 파묻어 주마!" },
        new[] { "멈춰라. 포빌리아 왕국은 수상한 자를 통과시키지 않는다.", "통행증이 없다면... 검으로 증명해라!" },
        new[] { "어서 오세요, 길 잃은 어린 양들이여.", "여기서 영원히 쉬게 해 드리지요... 영원히." },
        new[] { "크하하하! 내 보물을 노리고 왔느냐, 작은 것들아!", "황금빛 불꽃에 타 버려라!" },
        new[] { "후후... 정글의 사냥꾼은 소리 없이 다가가는 법.", "너희는 이미 내 사냥감이야." },
        new[] { "아직... 전쟁은... 끝나지 않았다......", "모든 산 자를 이 전쟁터에 묻어 주마!" },
        new[] { "가챠의 힘을 가진 용사라... 기다리고 있었다.", "운으로 여기까지 왔다면, 운이 다할 때까지 놀아 주마!" },
    };

    static readonly Color[] skyColors =
    {
        new Color(0.40f, 0.62f, 0.55f), // 외곽 숲
        new Color(0.55f, 0.75f, 0.95f), // 평원
        new Color(0.60f, 0.65f, 0.75f), // 산맥
        new Color(0.50f, 0.78f, 0.95f), // 해안
        new Color(0.70f, 0.72f, 0.80f), // 검문소
        new Color(0.95f, 0.88f, 0.65f), // 은혜의 땅
        new Color(0.30f, 0.22f, 0.10f), // 용의 굴
        new Color(0.55f, 0.25f, 0.20f), // 붉은 정글
        new Color(0.45f, 0.40f, 0.38f), // 옛 대전쟁터
        new Color(0.20f, 0.04f, 0.08f), // 마왕성
    };
    static readonly Color[] groundColors =
    {
        new Color(0.22f, 0.45f, 0.25f),
        new Color(0.55f, 0.70f, 0.35f),
        new Color(0.45f, 0.42f, 0.40f),
        new Color(0.88f, 0.80f, 0.58f),
        new Color(0.55f, 0.52f, 0.48f),
        new Color(0.70f, 0.75f, 0.40f),
        new Color(0.45f, 0.35f, 0.15f),
        new Color(0.35f, 0.40f, 0.18f),
        new Color(0.38f, 0.32f, 0.26f),
        new Color(0.25f, 0.15f, 0.18f),
    };

    public static int StageOf(int level) => Mathf.Clamp((level - 1) / LevelsPerStage, 0, StageCount - 1); // 0부터 시작
    public static int SubOf(int level) => (level - 1) % LevelsPerStage + 1;                                // 라운드 1~10

    public static string Label(int level) => $"{StageOf(level) + 1}-{SubOf(level)}";
    public static string StageName(int level) => $"스테이지 {StageOf(level) + 1} · {stageNames[StageOf(level)]}";
    public static string StageTitle(int stageNumber) => stageNames[Mathf.Clamp(stageNumber - 1, 0, StageCount - 1)]; // 스테이지 번호(1~10)의 이름만
    public static bool HasBoss(int level) => SubOf(level) == LevelsPerStage;
    public static bool HasMidBoss(int level) => SubOf(level) == MidBossRound;
    public static string BossName(int level) => bossNames[StageOf(level)];
    public static string MidBossName(int level) => midBossNames[StageOf(level)];
    public static string[] BossLines(int level) => bossLines[StageOf(level)];
    public static Color SkyColor(int level) => skyColors[StageOf(level)];
    public static Color GroundColor(int level) => groundColors[StageOf(level)];

    // 0부터 시작하는 스테이지 번호로 이름 얻기 (도감용)
    public static string StageNameByIndex(int stage) => stageNames[stage];
    public static string BossNameByIndex(int stage) => bossNames[stage];
    public static string MidBossNameByIndex(int stage) => midBossNames[stage];

    // 클리어 보상 골드 (처음 클리어하면 2배)
    public static int ClearReward(int level) => 100 + level * 20;
}
