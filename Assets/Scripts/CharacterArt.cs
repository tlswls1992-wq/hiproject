using System.Collections.Generic;
using UnityEngine;

// 캐릭터 그림을 불러옵니다. 그림이 있는 캐릭터만 그림으로 나오고, 없는 캐릭터는 지금처럼 동그라미로 나와요.
//
// 그림 위치: Assets/Resources/Characters/<캐릭터 id>/
//   portrait.png        초상화 (도감, 대화)
//   full.png            전신 (뽑기 카드, 동료 카드)
//   idle/idle_00.png... 대기 모습 (야영지, 편성, 전투 중 대기) - 여러 장이면 움직여요
//   attack/attack_00... 공격 모습 (전투)
//   anim.txt            장면마다 보여 줄 시간(밀리초). 예) attack=450,160,90,80,140,220
public class CharacterArt
{
    public Texture2D portrait;
    public Texture2D full;
    public Texture2D[] idle;
    public Texture2D[] attack;
    float[] idleTimes;   // 장면마다 보여 줄 시간(초)
    float[] attackTimes;
    Sprite[] idleSprites;
    Sprite[] attackSprites;

    // 전투에서 그림 한 장(정사각형 캔버스)의 높이 (게임 세계 단위). 캐릭터가 커 보이면 줄이세요.
    public const float BattleCanvasHeight = 2.6f;
    // 캔버스 아래에서 발이 있는 높이 비율 (그림의 이 지점이 유닛의 발 위치가 됨)
    public const float FeetPivot = 0.08f;

    static readonly Dictionary<string, CharacterArt> cache = new Dictionary<string, CharacterArt>();

    public bool HasBattleSprites => idle.Length > 0;
    public float AttackLength => Sum(attackTimes);

    // 캐릭터 그림 (없으면 null)
    public static CharacterArt For(string id)
    {
        if (cache.TryGetValue(id, out var cached)) return cached;
        string path = "Characters/" + id + "/";
        var art = new CharacterArt
        {
            portrait = Resources.Load<Texture2D>(path + "portrait"),
            full = Resources.Load<Texture2D>(path + "full"),
            idle = LoadFrames(path + "idle"),
            attack = LoadFrames(path + "attack"),
        };
        if (art.portrait == null && art.full == null && art.idle.Length == 0 && art.attack.Length == 0) art = null;
        else art.ReadTimes(Resources.Load<TextAsset>(path + "anim"));
        cache[id] = art;
        return art;
    }

    static Texture2D[] LoadFrames(string folder)
    {
        var frames = Resources.LoadAll<Texture2D>(folder);
        System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
        return frames;
    }

    void ReadTimes(TextAsset anim)
    {
        idleTimes = DefaultTimes(idle.Length, 0.12f);
        attackTimes = DefaultTimes(attack.Length, 0.1f);
        if (anim == null) return;
        foreach (var raw in anim.text.Split('\n'))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            var parts = line.Substring(eq + 1).Split(',');
            var times = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                times[i] = (int.TryParse(parts[i].Trim(), out int ms) ? Mathf.Max(ms, 20) : 100) / 1000f;
            string key = line.Substring(0, eq).Trim();
            if (key == "idle" && times.Length == idle.Length) idleTimes = times;
            if (key == "attack" && times.Length == attack.Length) attackTimes = times;
        }
    }

    static float[] DefaultTimes(int count, float each)
    {
        var t = new float[count];
        for (int i = 0; i < count; i++) t[i] = each;
        return t;
    }

    static float Sum(float[] times)
    {
        float s = 0f;
        foreach (var t in times) s += t;
        return s;
    }

    // 시간 t(초)일 때 보여 줄 장면 번호. loop가 아니면 끝난 뒤 -1
    static int FrameAt(float[] times, float t, bool loop)
    {
        if (times.Length == 0) return -1;
        float total = Sum(times);
        if (loop) t = Mathf.Repeat(t, total);
        else if (t >= total) return -1;
        for (int i = 0; i < times.Length; i++)
        {
            if (t < times[i]) return i;
            t -= times[i];
        }
        return times.Length - 1;
    }

    // ---- 화면(UI)용 ----

    public Texture2D IdleTexture(float t)
    {
        int i = FrameAt(idleTimes, t, true);
        return i >= 0 ? idle[i] : full;
    }

    // ---- 전투용 스프라이트 ----

    public Sprite IdleSprite(float t)
    {
        if (idleSprites == null) idleSprites = MakeSprites(idle);
        int i = FrameAt(idleTimes, t, true);
        return i >= 0 ? idleSprites[i] : null;
    }

    // 공격을 시작하고 t초 지났을 때의 장면 (공격이 끝났으면 null). speed가 크면 빨리 재생
    public Sprite AttackSprite(float t, float speed)
    {
        if (attack.Length == 0) return null;
        if (attackSprites == null) attackSprites = MakeSprites(attack);
        int i = FrameAt(attackTimes, t * speed, false);
        return i >= 0 ? attackSprites[i] : null;
    }

    static Sprite[] MakeSprites(Texture2D[] frames)
    {
        var sprites = new Sprite[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            var tex = frames[i];
            float ppu = tex.height / BattleCanvasHeight;
            sprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, FeetPivot), ppu);
        }
        return sprites;
    }

    // 사각형 안에 그림을 그립니다. crop이면 사각형을 꽉 채우도록 잘라서, 아니면 비율을 유지해서 맞춰 그려요.
    public static void DrawTexture(Rect r, Texture2D tex, bool crop)
    {
        if (tex == null) return;
        GUI.DrawTexture(r, tex, crop ? ScaleMode.ScaleAndCrop : ScaleMode.ScaleToFit);
    }
}
