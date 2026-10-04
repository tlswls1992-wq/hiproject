using System.Collections.Generic;
using UnityEngine;

// 그림 파일 없이 코드로 동그라미/네모 모양 그림을 만들어 주는 도우미입니다.
// 나중에 진짜 캐릭터 그림을 넣으면 이 파일은 필요 없어집니다.
public static class SpriteFactory
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Circle() => Get("circle", true);
    public static Sprite Square() => Get("square", false);

    static Sprite Get(string key, bool circle)
    {
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = !circle || new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude <= r - 1f;
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }
        tex.Apply();

        // pixelsPerUnit = size 이므로 크기 1짜리 그림이 됩니다.
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        cache[key] = sprite;
        return sprite;
    }
}
