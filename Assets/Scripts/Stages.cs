using UnityEngine;

// 스테이지 정보: 10스테이지 x 10판 = 총 100판 (1-1 ~ 10-10).
// 게임 안에서는 판마다 1부터 100까지 번호(level)를 씁니다. 예) 3-7 = 27번째 판
// 각 스테이지의 10번째 판은 보스전입니다.
public static class Stages
{
    public const int StageCount = 10;
    public const int LevelsPerStage = 10;
    public const int Count = StageCount * LevelsPerStage; // 100판
    public const int WavesPerLevel = 3;

    // ★ 스테이지 이름과 보스 이름은 여기서 바꿀 수 있어요.
    static readonly string[] stageNames =
    {
        "왕국의 초원", "속삭이는 숲", "버려진 광산", "사막의 유적", "얼어붙은 설원",
        "독안개 늪지", "불타는 화산", "망자의 묘지", "마왕군 요새", "마왕성",
    };
    static readonly string[] bossNames =
    {
        "고블린 왕", "거대 늑대", "광산 골렘", "미라 왕", "서리 거인",
        "늪의 히드라", "화염 군주", "리치", "어둠의 기사", "마왕",
    };
    static readonly Color[] skyColors =
    {
        new Color(0.45f, 0.68f, 0.88f), new Color(0.30f, 0.45f, 0.40f), new Color(0.18f, 0.16f, 0.15f),
        new Color(0.95f, 0.75f, 0.45f), new Color(0.75f, 0.85f, 0.95f), new Color(0.35f, 0.40f, 0.30f),
        new Color(0.45f, 0.15f, 0.08f), new Color(0.18f, 0.18f, 0.25f), new Color(0.22f, 0.12f, 0.18f),
        new Color(0.20f, 0.04f, 0.08f),
    };
    static readonly Color[] groundColors =
    {
        new Color(0.40f, 0.62f, 0.32f), new Color(0.22f, 0.40f, 0.25f), new Color(0.35f, 0.30f, 0.25f),
        new Color(0.85f, 0.70f, 0.45f), new Color(0.88f, 0.92f, 0.96f), new Color(0.30f, 0.38f, 0.25f),
        new Color(0.30f, 0.18f, 0.15f), new Color(0.28f, 0.30f, 0.28f), new Color(0.30f, 0.25f, 0.28f),
        new Color(0.25f, 0.15f, 0.18f),
    };

    public static int StageOf(int level) => Mathf.Clamp((level - 1) / LevelsPerStage, 0, StageCount - 1); // 0부터 시작
    public static int SubOf(int level) => (level - 1) % LevelsPerStage + 1;                                // 1~10

    public static string Label(int level) => $"{StageOf(level) + 1}-{SubOf(level)}";
    public static string StageName(int level) => $"스테이지 {StageOf(level) + 1} · {stageNames[StageOf(level)]}";
    public static bool HasBoss(int level) => SubOf(level) == LevelsPerStage;
    public static string BossName(int level) => bossNames[StageOf(level)];
    public static Color SkyColor(int level) => skyColors[StageOf(level)];
    public static Color GroundColor(int level) => groundColors[StageOf(level)];

    // 클리어 보상 골드 (처음 클리어하면 2배)
    public static int ClearReward(int level) => 100 + level * 20;
}
