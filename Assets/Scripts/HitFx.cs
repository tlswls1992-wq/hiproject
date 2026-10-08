using System.Collections.Generic;
using UnityEngine;

// 공격이 맞을 때 잠깐 번쩍이는 작은 효과입니다. (그림 파일 없이 코드로 그림)
//  Slash  : 칼/단검이 지나간 초승달 모양 궤적
//  Thrust : 창으로 찌른 가는 빛줄기
//  Impact : 둔기·큰 몸통 박치기의 퍽! 하는 별 모양 충격
//  Claw   : 발톱·이빨의 세 줄 할퀸 자국
//  Spark  : 화살·돌멩이가 꽂힐 때 작은 불꽃
public class HitFx : MonoBehaviour
{
    public enum Kind { None, Slash, Thrust, Impact, Claw, Spark }

    const int TexSize = 64;
    static readonly Dictionary<Kind, Sprite> sprites = new Dictionary<Kind, Sprite>();

    float duration;
    float elapsed;
    float startScale, endScale;
    float spin;
    SpriteRenderer sr;
    Color color;

    // facingRight: 공격한 쪽이 오른쪽을 보고 있는지 (효과 방향을 맞춤)
    public static void Spawn(Kind kind, Vector2 point, bool facingRight)
    {
        var battle = BattleManager.Instance;
        if (kind == Kind.None || battle == null || battle.World == null) return;

        float size = 1f, dur = 0.2f, grow = 1.25f, angle = 0f, spin = 0f;
        Color c = new Color(1f, 1f, 0.92f, 0.95f);
        switch (kind)
        {
            case Kind.Slash:  size = 1.15f; dur = 0.2f;  grow = 1.15f; angle = Random.Range(-25f, 25f); spin = -90f; break;
            case Kind.Thrust: size = 1.25f; dur = 0.16f; grow = 1.2f;  angle = Random.Range(-8f, 8f); c = new Color(0.95f, 0.98f, 1f, 0.95f); break;
            case Kind.Impact: size = 0.85f; dur = 0.2f;  grow = 1.45f; angle = Random.Range(0f, 45f); c = new Color(1f, 0.86f, 0.5f, 0.95f); break;
            case Kind.Claw:   size = 0.95f; dur = 0.22f; grow = 1.1f;  angle = Random.Range(-12f, 12f); c = new Color(1f, 0.55f, 0.5f, 0.95f); break;
            case Kind.Spark:  size = 0.55f; dur = 0.16f; grow = 1.5f;  angle = Random.Range(0f, 45f); c = new Color(1f, 0.92f, 0.6f, 0.95f); break;
        }

        var go = new GameObject("HitFx");
        go.transform.SetParent(battle.World, false);
        go.transform.position = point + Random.insideUnitCircle * 0.12f;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, facingRight ? angle : -angle);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = SpriteFor(kind);
        r.color = c;
        r.flipX = !facingRight;
        r.sortingOrder = 1001; // 유닛·투사체보다 위

        var fx = go.AddComponent<HitFx>();
        fx.sr = r;
        fx.color = c;
        fx.duration = dur;
        fx.startScale = size * 0.75f;
        fx.endScale = size * 0.75f * grow;
        fx.spin = facingRight ? spin : -spin;
        go.transform.localScale = Vector3.one * fx.startScale;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        if (t >= 1f) { Destroy(gameObject); return; }
        float ease = 1f - (1f - t) * (1f - t);                 // 처음엔 빠르게 퍼지고 끝에서 느려짐
        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, ease);
        if (spin != 0f) transform.Rotate(0f, 0f, spin * Time.deltaTime);
        float a = t < 0.35f ? 1f : 1f - (t - 0.35f) / 0.65f;   // 잠깐 또렷하다가 사라짐
        sr.color = new Color(color.r, color.g, color.b, color.a * a * a);
    }

    // ---------------- 모양 그리기 ----------------

    static Sprite SpriteFor(Kind kind)
    {
        if (sprites.TryGetValue(kind, out var cached) && cached != null) return cached;
        var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < TexSize; y++)
        {
            for (int x = 0; x < TexSize; x++)
            {
                // -1 ~ 1 좌표 (가운데가 0)
                float u = (x + 0.5f) / TexSize * 2f - 1f;
                float v = (y + 0.5f) / TexSize * 2f - 1f;
                float a = Mathf.Clamp01(Shape(kind, u, v));
                // 가운데는 하얗게, 가장자리는 효과 색이 남도록 (곱하는 색이 그대로 보임)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f), TexSize);
        sprites[kind] = sprite;
        return sprite;
    }

    static float Shape(Kind kind, float u, float v)
    {
        switch (kind)
        {
            case Kind.Slash:
            {
                // 오른쪽으로 볼록한 초승달: 반지름 0.7 원의 오른쪽 부분, 양 끝으로 갈수록 가늘어짐
                float cx = -0.15f;
                float d = Mathf.Sqrt((u - cx) * (u - cx) + v * v);
                float ang = Mathf.Atan2(v, u - cx);                  // -PI ~ PI, 오른쪽이 0
                float along = Mathf.Abs(ang) / 1.25f;               // 0(가운데) ~ 1(끝)
                if (along >= 1f) return 0f;
                float width = 0.16f * (1f - along * along);
                float edge = 1f - Mathf.Abs(d - 0.72f) / Mathf.Max(0.001f, width);
                return Soft(edge) * (1f - along * 0.4f);
            }
            case Kind.Thrust:
            {
                // 가로로 긴 빛줄기: 앞(오른쪽)이 굵고 뒤로 갈수록 가늘게 + 끝에 작은 반짝임
                if (u < -0.95f || u > 0.75f) return Flash(u - 0.55f, v, 0.28f);
                float k = (u + 0.95f) / 1.7f;                        // 0(뒤) ~ 1(앞)
                float width = 0.02f + 0.07f * k;
                float line = Soft(1f - Mathf.Abs(v) / width) * (0.35f + 0.65f * k);
                return Mathf.Max(line, Flash(u - 0.55f, v, 0.28f));
            }
            case Kind.Impact:
            {
                // 8갈래 뾰족한 별 + 가운데 동그란 빛
                float d = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(v, u);
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 4f)), 6f);
                float radius = 0.3f + 0.62f * spikes;
                float star = Soft((radius - d) / 0.08f);
                float core = Soft((0.28f - d) / 0.1f);
                float ring = Soft(1f - Mathf.Abs(d - 0.5f) / 0.04f) * 0.5f;
                return Mathf.Max(Mathf.Max(star, core), ring);
            }
            case Kind.Claw:
            {
                // 비스듬한 세 줄 (가운데 줄이 가장 길게), 양 끝은 가늘게
                float best = 0f;
                for (int i = -1; i <= 1; i++)
                {
                    // 축을 45도쯤 돌림
                    float along = (u * 0.8f + v * 0.6f);
                    float across = (-u * 0.6f + v * 0.8f) - i * 0.3f;
                    float len = i == 0 ? 0.85f : 0.68f;
                    float t = Mathf.Abs(along - i * 0.06f) / len;
                    if (t >= 1f) continue;
                    float width = 0.075f * (1f - t * t);
                    best = Mathf.Max(best, Soft(1f - Mathf.Abs(across) / Mathf.Max(0.001f, width)));
                }
                return best;
            }
            case Kind.Spark:
                return Flash(u, v, 0.95f);
        }
        return 0f;
    }

    // 4갈래 반짝임 (+ 모양 빛줄기와 가운데 점)
    static float Flash(float u, float v, float r)
    {
        float au = Mathf.Abs(u) / r, av = Mathf.Abs(v) / r;
        float cross = Mathf.Max(Soft(1f - au) * Soft(1f - av / 0.12f * (1f + au * 2f) * 0.5f),
                                Soft(1f - av) * Soft(1f - au / 0.12f * (1f + av * 2f) * 0.5f));
        float dot = Soft(1f - Mathf.Sqrt(au * au + av * av) / 0.35f);
        return Mathf.Max(cross, dot);
    }

    static float Soft(float x) => Mathf.Clamp01(x * 2.5f);
}
