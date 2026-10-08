using System.Collections.Generic;
using UnityEngine;

// 성급 별 그림 (Resources/UI/Stars/<색>/star_00~49.png, 빛이 표면을 스치는 4초 반복 애니메이션)
//   ★1~5   : 노란 별 1~5개
//   ★6~10  : 별 5칸 중 앞에서부터 파란 별, 나머지는 노란 별   (예: ★6 = 파노노노노)
//   ★11~15 : 별 5칸 중 앞에서부터 빨간 별, 나머지는 파란 별   (예: ★12 = 빨빨파파파)
public static class StarIcons
{
    static readonly string[] colorNames = { "yellow", "blue", "red" };
    static readonly Texture2D[][] frames = new Texture2D[3][];
    static bool loaded;
    const float FrameSeconds = 0.08f;

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        for (int c = 0; c < 3; c++)
        {
            var list = new List<Texture2D>(Resources.LoadAll<Texture2D>("UI/Stars/" + colorNames[c]));
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            frames[c] = list.ToArray();
        }
    }

    // 5칸 중 i번째 칸의 색 (0 노랑, 1 파랑, 2 빨강). 빈 칸이면 -1
    public static int SlotColor(int star, int slot)
    {
        int tier = Mathf.Clamp((star - 1) / SaveData.StarsPerTier, 0, 2);
        int filled = (star - 1) % SaveData.StarsPerTier + 1;
        if (tier == 0) return slot < filled ? 0 : -1;
        return slot < filled ? tier : tier - 1;
    }

    public static int SlotCount(int star) => star > SaveData.StarsPerTier ? SaveData.StarsPerTier : Mathf.Max(1, star);

    // 별 줄의 가로 길이 (높이 size 기준)
    public static float Width(int star, float size) => SlotCount(star) * size * 0.86f + size * 0.14f;

    // 별 줄을 그림. r의 높이가 별 크기, anchor로 왼쪽/가운데/오른쪽 정렬
    public static void Draw(Rect r, int star, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        Load();
        float size = r.height;
        float width = Width(star, size);
        float x = anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.UpperCenter
            ? r.center.x - width / 2f
            : anchor == TextAnchor.MiddleRight || anchor == TextAnchor.UpperRight ? r.xMax - width : r.x;
        int count = SlotCount(star);
        for (int i = 0; i < count; i++)
        {
            int color = SlotColor(star, i);
            if (color < 0) continue;
            var cell = new Rect(x + i * size * 0.86f, r.y, size, size);
            var tex = Frame(color, i);
            if (tex != null) GUI.DrawTexture(cell, tex, ScaleMode.ScaleToFit);
            else UI.Text(cell, "★", Mathf.RoundToInt(size), FallbackColor(color), TextAnchor.MiddleCenter, true);
        }
    }

    // 칸마다 조금씩 늦게 빛이 지나가도록 (왼쪽에서 오른쪽으로 물결)
    static Texture2D Frame(int color, int slot)
    {
        var list = frames[color];
        if (list == null || list.Length == 0) return null;
        int f = Mathf.FloorToInt(Time.unscaledTime / FrameSeconds) + slot * 3;
        return list[f % list.Length];
    }

    public static Color FallbackColor(int color) =>
        color == 2 ? new Color(1f, 0.35f, 0.32f) : color == 1 ? new Color(0.4f, 0.6f, 1f) : UI.Gold;
}
