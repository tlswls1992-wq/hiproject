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
        "은혜의 땅", "노란 용의 동굴", "붉은 정글", "옛 대전쟁터", "마왕성",
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
    public static StageScript Script(int level) => StageScript.Of(StageOf(level));
    public static string BossName(int level) => EnemyDef.Find(Script(level).boss).name;
    public static string MidBossName(int level) => EnemyDef.Find(Script(level).midBoss).name;
    public static Color SkyColor(int level) => skyColors[StageOf(level)];
    public static Color GroundColor(int level) => groundColors[StageOf(level)];

    // 0부터 시작하는 스테이지 번호로 이름 얻기 (도감용)
    public static string StageNameByIndex(int stage) => stageNames[stage];

    // 클리어 보상 골드 (처음 클리어하면 2배)
    public static int ClearReward(int level) => 100 + level * 20;
}
