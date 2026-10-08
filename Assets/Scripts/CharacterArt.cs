using System.Collections.Generic;
using UnityEngine;

// 캐릭터 그림을 불러옵니다. 그림이 있는 캐릭터만 그림으로 나오고, 없는 캐릭터는 지금처럼 동그라미로 나와요.
//
// 그림 위치: Assets/Resources/Characters/<캐릭터 id>/
//   portrait.png        초상화 (도감, 대화)
//   full.png            전신 (뽑기 카드, 동료 카드)
//   idle/idle_00.png... 대기 모습 (야영지, 편성, 전투 중 대기) - 여러 장이면 움직여요
//   run/run_00.png...   달리는 모습 (전투 중 이동) - 한 장이면 위아래로 흔들어서 달리는 느낌을 냄
//   attack/attack_00... 공격 모습 (전투)
//   anim.txt            장면마다 보여 줄 시간(밀리초). 예) attack=450,160,90,80,140,220
public class CharacterArt
{
    public Texture2D portrait;
    public Texture2D full;
    public Texture2D[] idle;
    public Texture2D[] run;
    public Texture2D[] attack;
    float[] idleTimes;   // 장면마다 보여 줄 시간(초)
    float[] runTimes;
    float[] attackTimes;
    Sprite[] idleSprites;
    Sprite[] runSprites;
    Sprite[] attackSprites;

    // 몬스터 그림용 정보 (anim.txt): 그림이 보는 방향, 픽셀 밀도, 발 위치, 키와 몸 폭
    public bool facesLeft;        // facing=left (몬스터는 왼쪽을 봄)
    float unitPx;                 // unit=256: 이 그림에서 '캔버스 한 칸(BattleCanvasHeight)'에 해당하는 픽셀 (없으면 그림 높이)
    float feetPx = -1f;           // feet=16: 그림 아래에서 발까지 픽셀 (없으면 FeetPivot 비율)
    public float topUnits;        // top=0.47: 발에서 머리끝까지 높이 (캔버스 칸 단위, 0이면 모름)
    public float bodyUnits;       // body=0.33: 몸의 반폭 (캔버스 칸 단위, 0이면 모름)

    // 전투에서 그림 한 장(정사각형 캔버스)의 높이 (게임 세계 단위). 캐릭터가 커 보이면 줄이세요.
    public const float BattleCanvasHeight = 4.68f; // 전투 그림 높이 (월드 단위). 예전 2.6의 2배에서 0.9배로
    // 캔버스 아래에서 발이 있는 높이 비율 (그림의 이 지점이 유닛의 발 위치가 됨)
    // (변환 스크립트가 모든 동작의 발을 그림 아래 6% 지점에 맞춰 둠)
    public const float FeetPivot = 0.065f;

    static readonly Dictionary<string, CharacterArt> cache = new Dictionary<string, CharacterArt>();

    public bool HasBattleSprites => idle.Length > 0;
    public bool HasRun => run.Length > 0;
    public float AttackLength => Sum(attackTimes);

    // 캐릭터 그림 (없으면 null)
    public static CharacterArt For(string id) => Load("Characters/", id);

    // 적 그림 (Resources/Enemies/<적 id>/). 없으면 null → 코드로 그린 모습(EnemyLook)을 씀
    public static CharacterArt ForEnemy(string id) => Load("Enemies/", id);

    static CharacterArt Load(string root, string id)
    {
        string key = root + id;
        if (cache.TryGetValue(key, out var cached)) return cached;
        string path = root + id + "/";
        var art = new CharacterArt
        {
            portrait = Resources.Load<Texture2D>(path + "portrait"),
            full = Resources.Load<Texture2D>(path + "full"),
            idle = LoadFrames(path + "idle"),
            run = LoadFrames(path + "run"),
            attack = LoadFrames(path + "attack"),
        };
        if (art.portrait == null && art.full == null && art.idle.Length == 0 && art.run.Length == 0 && art.attack.Length == 0) art = null;
        else art.ReadTimes(Resources.Load<TextAsset>(path + "anim"));
        cache[key] = art;
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
        runTimes = DefaultTimes(run.Length, 0.08f);
        attackTimes = DefaultTimes(attack.Length, 0.1f);
        if (anim == null) return;
        foreach (var raw in anim.text.Split('\n'))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string key0 = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim();
            float num;
            float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out num);
            if (key0 == "facing") { facesLeft = value == "left"; continue; }
            if (key0 == "unit") { unitPx = num; continue; }
            if (key0 == "feet") { feetPx = num; continue; }
            if (key0 == "top") { topUnits = num; continue; }
            if (key0 == "body") { bodyUnits = num; continue; }
            var parts = value.Split(',');
            var times = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                times[i] = (int.TryParse(parts[i].Trim(), out int ms) ? Mathf.Max(ms, 20) : 100) / 1000f;
            string key = line.Substring(0, eq).Trim();
            if (key == "idle" && times.Length == idle.Length) idleTimes = times;
            if (key == "run" && times.Length == run.Length) runTimes = times;
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

    public Sprite RunSprite(float t)
    {
        if (run.Length == 0) return null;
        if (runSprites == null) runSprites = MakeSprites(run);
        return runSprites[Mathf.Max(0, FrameAt(runTimes, t, true))];
    }

    // 장면이 한 장뿐인 동작은 코드로 움직임을 줘야 하는지
    public bool RunIsStill => run.Length <= 1;
    public bool IdleIsStill => idle.Length <= 1;

    // 공격을 시작하고 t초 지났을 때의 장면 (공격이 끝났으면 null). speed가 크면 빨리 재생
    public Sprite AttackSprite(float t, float speed)
    {
        if (attack.Length == 0) return null;
        if (attackSprites == null) attackSprites = MakeSprites(attack);
        int i = FrameAt(attackTimes, t * speed, false);
        return i >= 0 ? attackSprites[i] : null;
    }

    Sprite[] MakeSprites(Texture2D[] frames)
    {
        var sprites = new Sprite[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            var tex = frames[i];
            float ppu = (unitPx > 0f ? unitPx : tex.height) / BattleCanvasHeight;
            float pivotY = feetPx >= 0f ? feetPx / tex.height : FeetPivot;
            sprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, pivotY), ppu);
        }
        return sprites;
    }

    // 서 있는 캐릭터 그리기 (야영지, 편성 화면용): 발 위치 feet에 맞추고, 한 장짜리 대기 그림이면 숨 쉬듯 움직임.
    // faceLeft면 좌우를 뒤집어 그려요. (화면 회전/뒤집기 대신 그림 좌표를 뒤집는 방식이라 항상 제대로 보여요)
    public void DrawStanding(Vector2 feet, float height, float time, bool faceLeft)
    {
        var tex = IdleTexture(time);
        if (tex == null) return;
        float sx = 1f, sy = 1f;
        if (IdleIsStill)
        {
            float breath = Mathf.Sin(time * 2.4f);
            sx = 1f - breath * 0.006f;
            sy = 1f + breath * 0.014f;
        }
        float w = height * sx, h = height * sy;
        var r = new Rect(feet.x - w / 2f, feet.y - h * (1f - FeetPivot), w, h);
        GUI.DrawTextureWithTexCoords(r, tex, faceLeft ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f));
    }

    // 사각형 안에 그림을 그립니다. crop이면 사각형을 꽉 채우도록 잘라서, 아니면 비율을 유지해서 맞춰 그려요.
    public static void DrawTexture(Rect r, Texture2D tex, bool crop)
    {
        if (tex == null) return;
        GUI.DrawTexture(r, tex, crop ? ScaleMode.ScaleAndCrop : ScaleMode.ScaleToFit);
    }
}
