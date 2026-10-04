using UnityEngine;

// 스테이지 정보: 3장 x 5스테이지 = 총 15스테이지. 각 장의 마지막은 보스전입니다.
public static class Stages
{
    public const int Count = 15;
    public const int WavesPerStage = 3;

    static readonly string[] chapterNames = { "1장 · 왕국의 초원", "2장 · 어둠의 숲", "3장 · 마왕성" };
    static readonly string[] bossNames = { "오크 대장", "어둠의 기사", "마왕" };
    static readonly Color[] skyColors =
    {
        new Color(0.45f, 0.68f, 0.88f),
        new Color(0.20f, 0.28f, 0.35f),
        new Color(0.25f, 0.08f, 0.12f),
    };
    static readonly Color[] groundColors =
    {
        new Color(0.40f, 0.62f, 0.32f),
        new Color(0.20f, 0.35f, 0.22f),
        new Color(0.30f, 0.25f, 0.25f),
    };

    public static int Chapter(int stage) => Mathf.Clamp((stage - 1) / 5, 0, 2);
    public static string ChapterName(int stage) => chapterNames[Chapter(stage)];
    public static bool HasBoss(int stage) => stage % 5 == 0;
    public static string BossName(int stage) => bossNames[Chapter(stage)];
    public static Color SkyColor(int stage) => skyColors[Chapter(stage)];
    public static Color GroundColor(int stage) => groundColors[Chapter(stage)];

    // 클리어 보상 골드 (처음 클리어하면 2배)
    public static int ClearReward(int stage) => 150 + stage * 50;

    public static string Label(int stage) => $"{Chapter(stage) + 1}-{(stage - 1) % 5 + 1}";
}
