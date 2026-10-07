using System.Collections.Generic;
using UnityEngine;

// 적 그림이 없을 때, 코드로 적의 모습을 그려 줍니다. (사람, 짐승, 곰, 전갈, 와이번, 샌드웜, 골렘, 인어, 상어, 크라켄)
// 모양 · 색 · 무기 · 꾸밈은 EnemyData.cs에서 정해요. 모든 적은 왼쪽(용사 쪽)을 보고 있어요.
// 진짜 그림(Resources/Enemies/<id>/idle ...)이 생기면 그 그림이 대신 쓰여요.
public static class EnemyLook
{
    public const int Size = 128;      // 그림 한 장의 픽셀 크기
    public const float Ground = 0.04f; // 그림 안에서 발이 닿는 높이 (아래에서 4%)

    public class Look
    {
        public Texture2D texture;
        public Sprite sprite;
        public float top;   // 그림에서 가장 높은 곳 (0~1, 체력바 위치용)
        public float scale; // 몸 크기(size) 1당 그림 크기 (월드 단위)
        public bool hovers; // 공중에 떠 있는 적 (와이번, 상어, 크라켄)
    }

    static readonly Dictionary<string, Look> cache = new Dictionary<string, Look>();
    static readonly Dictionary<string, Texture2D> fullArt = new Dictionary<string, Texture2D>();

    // 진짜 적 전신 그림 (Resources/Enemies/<id>/full.png). 없으면 null
    public static Texture2D FullArt(string id)
    {
        if (!fullArt.TryGetValue(id, out var tex))
        {
            tex = Resources.Load<Texture2D>("Enemies/" + id + "/full");
            fullArt[id] = tex;
        }
        return tex;
    }

    public static Look For(EnemyDef def)
    {
        if (cache.TryGetValue(def.id, out var look) && look.texture != null) return look;
        var p = new Painter(Size);
        Paint(p, def);
        var tex = p.Finish(new Color(0.10f, 0.07f, 0.06f));
        look = new Look
        {
            texture = tex,
            sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, Ground), Size),
            top = p.Top,
            scale = ScaleOf(def.shape),
            hovers = def.shape == EnemyShape.Wyvern || def.shape == EnemyShape.Shark || def.shape == EnemyShape.Kraken,
        };
        cache[def.id] = look;
        return look;
    }

    static float ScaleOf(EnemyShape s)
    {
        switch (s)
        {
            case EnemyShape.Kraken: return 2.4f;
            case EnemyShape.Worm: return 2.7f;
            case EnemyShape.Bear: return 2.9f;
            case EnemyShape.Golem: return 2.9f;
            case EnemyShape.Shark: return 3.0f;
            case EnemyShape.Beast:
            case EnemyShape.Lynx:
            case EnemyShape.Scorpion: return 3.1f;
            case EnemyShape.Wyvern: return 3.3f;
            default: return 3.5f; // 사람, 인어
        }
    }

    // ================================================================ 그리기

    static Color Dark(Color c, float k = 0.7f) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    static Color Light(Color c, float k = 0.3f) => Color.Lerp(c, Color.white, k);
    static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    static readonly Color Steel = new Color(0.80f, 0.82f, 0.86f);
    static readonly Color WoodC = new Color(0.50f, 0.34f, 0.19f);
    static readonly Color Boot = new Color(0.26f, 0.18f, 0.12f);
    static readonly Color EyeDark = new Color(0.08f, 0.06f, 0.06f);

    static void Paint(Painter p, EnemyDef d)
    {
        switch (d.shape)
        {
            case EnemyShape.Beast: Wolf(p, d, false); break;
            case EnemyShape.Lynx: Wolf(p, d, true); break;
            case EnemyShape.Bear: Bear(p, d); break;
            case EnemyShape.Scorpion: Scorpion(p, d); break;
            case EnemyShape.Wyvern: Wyvern(p, d); break;
            case EnemyShape.Worm: Worm(p, d); break;
            case EnemyShape.Golem: Golem(p, d); break;
            case EnemyShape.Shark: Shark(p, d); break;
            case EnemyShape.Kraken: Kraken(p, d); break;
            case EnemyShape.Mermaid: Humanoid(p, d, true); break;
            default: Humanoid(p, d, false); break;
        }
    }

    // ---------------- 사람 모양 ----------------
    static void Humanoid(Painter p, EnemyDef d, bool mermaid)
    {
        Color body = d.color, skin = d.skin;
        Color hair = Color.Lerp(Dark(body, 0.6f), new Color(0.25f, 0.17f, 0.10f), 0.5f);
        if ((d.deco & EnemyDeco.Halo) != 0 || (d.deco & EnemyDeco.Wings) != 0) hair = new Color(0.95f, 0.85f, 0.55f);
        if (d.shape == EnemyShape.Mermaid) hair = Light(d.color, 0.15f);
        System.Func<EnemyDeco, bool> has = x => (d.deco & x) != 0;
        float hx = 0.47f, hy = 0.66f, hr = 0.115f; // 머리
        if (has(EnemyDeco.Beard) && !mermaid && d.weapon != EnemyWeapon.Bow && d.size < 0.7f) { hy = 0.62f; } // 드워프처럼 키 작게

        // 날개 · 망토 (몸 뒤)
        if (has(EnemyDeco.Wings))
        {
            Color w = new Color(0.98f, 0.97f, 0.93f);
            p.Ellipse(0.70f, 0.60f, 0.20f, 0.11f, Dark(w, 0.88f), -25f);
            p.Ellipse(0.72f, 0.70f, 0.17f, 0.08f, w, -35f);
            p.Ellipse(0.68f, 0.52f, 0.14f, 0.07f, w, -15f);
        }
        if (has(EnemyDeco.Cape)) p.Tri(0.52f, 0.54f, 0.78f, 0.12f, 0.50f, 0.12f, Dark(d.accent == Steel ? body : Color.Lerp(body, d.accent, 0.3f), 0.6f));
        if (has(EnemyDeco.LongHair)) p.Ellipse(hx + 0.07f, hy - 0.06f, 0.10f, 0.16f, hair);

        // 다리 또는 인어 꼬리
        if (mermaid)
        {
            p.Capsule(0.50f, 0.30f, 0.58f, 0.15f, 0.085f, body);
            p.Capsule(0.58f, 0.15f, 0.70f, 0.08f, 0.06f, body);
            p.Tri(0.70f, 0.09f, 0.86f, 0.20f, 0.88f, 0.00f, Light(body, 0.2f));
            for (int i = 0; i < 3; i++) p.Ellipse(0.52f + i * 0.05f, 0.24f - i * 0.05f, 0.03f, 0.015f, Light(body, 0.25f), -30f);
        }
        else
        {
            p.Capsule(0.55f, 0.30f, 0.58f, 0.08f, 0.05f, Dark(body, 0.55f));
            p.Capsule(0.46f, 0.30f, 0.42f, 0.08f, 0.055f, Dark(body, 0.65f));
            p.Ellipse(0.585f, 0.055f, 0.06f, 0.035f, Boot);
            p.Ellipse(0.405f, 0.055f, 0.065f, 0.035f, Boot);
        }

        // 뒷팔
        p.Capsule(0.57f, 0.49f, 0.62f, 0.34f, 0.042f, Dark(body, 0.6f));
        // 몸통
        Color torso = mermaid ? Color.Lerp(skin, body, 0.25f) : body;
        p.Box(0.50f, 0.41f, 0.12f, 0.12f, 0.05f, torso);
        if (mermaid) p.Box(0.50f, 0.47f, 0.12f, 0.04f, 0.03f, body);
        else p.Box(0.50f, 0.31f, 0.125f, 0.018f, 0.005f, Dark(body, 0.45f));
        if (has(EnemyDeco.Halo) || d.type == EnemyType.Boss) p.Box(0.50f, 0.42f, 0.03f, 0.08f, 0.01f, A(d.accent, 0.9f)); // 가슴 장식

        // 머리
        if (has(EnemyDeco.Hood)) p.Circle(hx + 0.02f, hy + 0.01f, hr + 0.035f, Dark(body, 0.8f));
        if (has(EnemyDeco.ElfEars)) p.Tri(hx + 0.06f, hy + 0.01f, hx + 0.17f, hy + 0.12f, hx + 0.08f, hy + 0.06f, skin);
        p.Circle(hx, hy, hr, skin);
        if (!has(EnemyDeco.Hood) && !has(EnemyDeco.Helmet)) p.Ellipse(hx + 0.03f, hy + 0.06f, hr * 1.02f, hr * 0.62f, hair); // 머리카락
        if (has(EnemyDeco.Beard)) p.Ellipse(hx - 0.03f, hy - 0.07f, 0.085f, 0.07f, hair);
        if (has(EnemyDeco.Tusks))
        {
            p.Tri(hx - 0.09f, hy - 0.05f, hx - 0.06f, hy - 0.05f, hx - 0.08f, hy + 0.0f, Color.white);
            p.Tri(hx - 0.03f, hy - 0.06f, hx + 0.0f, hy - 0.06f, hx - 0.02f, hy - 0.01f, Color.white);
        }
        if (has(EnemyDeco.Mask)) p.Box(hx - 0.03f, hy + 0.005f, 0.09f, 0.03f, 0.015f, new Color(0.15f, 0.13f, 0.14f));
        // 눈 (왼쪽을 봄)
        Color eye = d.type == EnemyType.Normal ? EyeDark : Dark(d.accent, 0.8f);
        if (has(EnemyDeco.Mask)) eye = new Color(1f, 0.9f, 0.7f);
        p.Circle(hx - 0.075f, hy + 0.005f, 0.017f, eye);
        p.Circle(hx - 0.015f, hy + 0.01f, 0.015f, eye);
        if (has(EnemyDeco.Helmet))
        {
            p.Ellipse(hx + 0.005f, hy + 0.055f, hr + 0.02f, 0.075f, Steel);
            p.Box(hx + 0.005f, hy + 0.036f, hr + 0.018f, 0.011f, 0.004f, Dark(Steel, 0.55f)); // 투구 테
            p.Ellipse(hx - 0.02f, hy + 0.085f, 0.03f, 0.015f, Light(Steel, 0.5f));          // 반사광
            if (d.type != EnemyType.Normal) p.Ellipse(hx + 0.05f, hy + 0.14f, 0.07f, 0.025f, d.accent, -20f); // 장식 깃
            p.Box(hx - 0.045f, hy + 0.0f, 0.012f, 0.045f, 0.004f, Dark(Steel, 0.8f));
        }
        if (has(EnemyDeco.Headband)) p.Box(hx + 0.005f, hy + 0.06f, hr + 0.005f, 0.02f, 0.01f, d.accent, -8f);
        if (has(EnemyDeco.Crown))
        {
            Color g = new Color(1f, 0.82f, 0.3f);
            p.Box(hx + 0.01f, hy + 0.105f, 0.08f, 0.02f, 0.005f, g);
            for (int i = 0; i < 3; i++) p.Tri(hx - 0.06f + i * 0.07f, hy + 0.12f, hx + 0.0f + i * 0.07f, hy + 0.12f, hx - 0.03f + i * 0.07f, hy + 0.18f, g);
        }
        if (has(EnemyDeco.Halo)) p.Ring(hx + 0.02f, hy + 0.19f, 0.085f, 0.022f, 0.012f, new Color(1f, 0.92f, 0.5f));

        // 앞팔 + 무기
        bool twoHanded = d.weapon == EnemyWeapon.Bow || d.weapon == EnemyWeapon.Crossbow;
        Vector2 hand = twoHanded ? new Vector2(0.28f, 0.46f) : new Vector2(0.34f, 0.38f);
        Weapon(p, d, hand, true);
        p.Capsule(0.45f, 0.49f, hand.x, hand.y, 0.042f, Dark(body, 0.9f));
        p.Circle(hand.x, hand.y, 0.035f, skin);
        Weapon(p, d, hand, false);
        if (has(EnemyDeco.Shield))
        {
            Color sc = d.accent == Steel ? Dark(body, 0.9f) : d.accent;
            p.Ellipse(0.37f, 0.38f, 0.085f, 0.14f, Dark(Steel, 0.75f));
            p.Ellipse(0.37f, 0.38f, 0.07f, 0.125f, sc);
            p.Circle(0.37f, 0.38f, 0.022f, Steel);
        }
    }

    // behind = true: 손 뒤에 가려지는 부분 (자루 등), false: 손 앞에 보이는 부분
    static void Weapon(Painter p, EnemyDef d, Vector2 h, bool behind)
    {
        Color metal = d.accent == WoodC || d.accent.grayscale < 0.35f ? Steel : (d.type == EnemyType.Normal ? Steel : Color.Lerp(Steel, d.accent, 0.4f));
        switch (d.weapon)
        {
            case EnemyWeapon.Sword:
                if (behind) p.Box(h.x - 0.06f, h.y + 0.16f, 0.022f, 0.16f, 0.008f, metal, 22f);
                else p.Box(h.x, h.y + 0.02f, 0.055f, 0.013f, 0.005f, Dark(WoodC, 0.8f), 22f);
                break;
            case EnemyWeapon.Dagger:
                if (behind) p.Box(h.x - 0.04f, h.y + 0.07f, 0.016f, 0.07f, 0.006f, metal, 35f);
                break;
            case EnemyWeapon.Spear:
                if (behind)
                {
                    p.Capsule(h.x + 0.06f, h.y - 0.30f, h.x - 0.10f, h.y + 0.48f, 0.014f, WoodC);
                    p.Tri(h.x - 0.13f, h.y + 0.46f, h.x - 0.07f, h.y + 0.47f, h.x - 0.12f, h.y + 0.58f, metal);
                }
                break;
            case EnemyWeapon.Bow:
                if (behind)
                {
                    p.Capsule(h.x + 0.01f, h.y + 0.02f, h.x - 0.01f, h.y + 0.30f, 0.004f, new Color(0.95f, 0.92f, 0.85f));
                    p.Capsule(h.x + 0.01f, h.y + 0.02f, h.x - 0.01f, h.y - 0.26f, 0.004f, new Color(0.95f, 0.92f, 0.85f));
                }
                else
                {
                    p.Capsule(h.x - 0.01f, h.y + 0.30f, h.x - 0.06f, h.y + 0.14f, 0.015f, WoodC);
                    p.Capsule(h.x - 0.06f, h.y + 0.14f, h.x - 0.06f, h.y - 0.10f, 0.015f, WoodC);
                    p.Capsule(h.x - 0.06f, h.y - 0.10f, h.x - 0.01f, h.y - 0.26f, 0.015f, WoodC);
                }
                break;
            case EnemyWeapon.Crossbow:
                if (!behind)
                {
                    p.Box(h.x - 0.02f, h.y, 0.11f, 0.018f, 0.006f, WoodC);
                    p.Capsule(h.x - 0.12f, h.y - 0.08f, h.x - 0.12f, h.y + 0.08f, 0.012f, Dark(Steel, 0.8f));
                }
                break;
            case EnemyWeapon.Staff:
                if (behind)
                {
                    p.Capsule(h.x + 0.02f, h.y - 0.32f, h.x - 0.03f, h.y + 0.40f, 0.016f, WoodC);
                    p.Circle(h.x - 0.035f, h.y + 0.45f, 0.10f, A(d.accent, 0.3f));
                    p.Circle(h.x - 0.035f, h.y + 0.45f, 0.045f, Light(d.accent, 0.3f));
                }
                break;
            case EnemyWeapon.Hammer:
                if (behind)
                {
                    p.Capsule(h.x + 0.02f, h.y - 0.04f, h.x - 0.08f, h.y + 0.30f, 0.016f, WoodC);
                    p.Box(h.x - 0.09f, h.y + 0.33f, 0.08f, 0.05f, 0.012f, metal, 18f);
                }
                break;
            case EnemyWeapon.Axe:
                if (behind)
                {
                    p.Capsule(h.x + 0.02f, h.y - 0.04f, h.x - 0.07f, h.y + 0.30f, 0.015f, WoodC);
                    p.Ellipse(h.x - 0.12f, h.y + 0.26f, 0.06f, 0.075f, metal, 18f);
                }
                break;
            case EnemyWeapon.Sling:
                if (!behind)
                {
                    p.Capsule(h.x, h.y, h.x - 0.05f, h.y - 0.08f, 0.008f, WoodC);
                    p.Circle(h.x - 0.055f, h.y - 0.10f, 0.026f, new Color(0.55f, 0.53f, 0.5f));
                }
                break;
        }
    }

    // ---------------- 짐승 ----------------
    static void Wolf(Painter p, EnemyDef d, bool lynx)
    {
        Color c = d.color, back = Dark(c, 0.72f);
        p.Capsule(0.47f, 0.30f, 0.48f, 0.06f, lynx ? 0.045f : 0.035f, back);
        p.Capsule(0.74f, 0.30f, 0.77f, 0.06f, lynx ? 0.045f : 0.035f, back);
        if (lynx) p.Capsule(0.80f, 0.40f, 0.88f, 0.44f, 0.035f, c);
        else p.Capsule(0.80f, 0.40f, 0.94f, 0.55f, 0.04f, c);
        p.Ellipse(0.57f, 0.38f, 0.27f, lynx ? 0.15f : 0.13f, c);
        p.Ellipse(0.57f, 0.33f, 0.20f, 0.06f, Light(c, 0.3f));
        p.Capsule(0.38f, 0.30f, 0.36f, 0.06f, lynx ? 0.048f : 0.038f, c);
        p.Capsule(0.66f, 0.30f, 0.68f, 0.06f, lynx ? 0.048f : 0.038f, c);
        p.Circle(0.28f, 0.50f, lynx ? 0.125f : 0.11f, c);
        p.Ellipse(0.17f, 0.45f, 0.08f, 0.05f, Light(c, 0.15f));
        p.Circle(0.10f, 0.46f, 0.02f, EyeDark);
        p.Tri(0.27f, 0.58f, 0.36f, 0.72f, 0.37f, 0.56f, Dark(c, 0.85f));
        if (lynx) p.Capsule(0.36f, 0.72f, 0.37f, 0.78f, 0.008f, EyeDark);
        p.Circle(0.235f, 0.535f, 0.022f, d.accent);
        p.Circle(0.228f, 0.535f, 0.010f, EyeDark);
        if (lynx) for (int i = 0; i < 4; i++) p.Circle(0.50f + i * 0.08f, 0.42f - (i % 2) * 0.04f, 0.017f, Dark(c, 0.6f));
    }

    static void Bear(Painter p, EnemyDef d)
    {
        Color c = d.color, back = Dark(c, 0.72f);
        p.Capsule(0.48f, 0.30f, 0.47f, 0.07f, 0.065f, back);
        p.Capsule(0.74f, 0.30f, 0.76f, 0.07f, 0.065f, back);
        p.Ellipse(0.56f, 0.40f, 0.30f, 0.22f, c);
        p.Capsule(0.38f, 0.30f, 0.36f, 0.07f, 0.07f, c);
        p.Capsule(0.66f, 0.30f, 0.68f, 0.07f, 0.07f, c);
        p.Circle(0.24f, 0.64f, 0.045f, c);
        p.Circle(0.34f, 0.63f, 0.045f, c);
        p.Circle(0.24f, 0.64f, 0.022f, Light(c, 0.3f));
        p.Circle(0.26f, 0.50f, 0.14f, c);
        p.Ellipse(0.15f, 0.45f, 0.075f, 0.055f, Color.Lerp(c, d.accent, 0.5f));
        p.Circle(0.09f, 0.47f, 0.022f, EyeDark);
        p.Circle(0.21f, 0.54f, 0.018f, EyeDark);
        if (d.type != EnemyType.Normal) p.Box(0.33f, 0.56f, 0.012f, 0.06f, 0.004f, A(d.accent, 0.9f), 30f); // 상처
    }

    static void Scorpion(Painter p, EnemyDef d)
    {
        Color c = d.color, dk = Dark(c, 0.7f);
        for (int i = 0; i < 3; i++)
        {
            float x = 0.35f + i * 0.09f;
            p.Capsule(x, 0.17f, x - 0.05f, 0.04f, 0.012f, dk);
            p.Capsule(x + 0.02f, 0.17f, x + 0.07f, 0.04f, 0.012f, dk);
        }
        p.Capsule(0.62f, 0.20f, 0.74f, 0.32f, 0.045f, c);
        p.Capsule(0.74f, 0.32f, 0.76f, 0.48f, 0.04f, c);
        p.Capsule(0.76f, 0.48f, 0.68f, 0.58f, 0.035f, c);
        p.Capsule(0.68f, 0.58f, 0.58f, 0.56f, 0.03f, c);
        p.Tri(0.58f, 0.60f, 0.50f, 0.52f, 0.58f, 0.52f, d.accent);
        p.Ellipse(0.46f, 0.19f, 0.19f, 0.085f, c);
        for (int i = 0; i < 3; i++) p.Ellipse(0.40f + i * 0.08f, 0.22f, 0.035f, 0.05f, Light(c, 0.12f));
        p.Ellipse(0.26f, 0.18f, 0.08f, 0.06f, c);
        p.Capsule(0.24f, 0.20f, 0.14f, 0.27f, 0.022f, c);
        p.Ellipse(0.09f, 0.29f, 0.065f, 0.04f, c, 10f);
        p.Ellipse(0.07f, 0.25f, 0.05f, 0.025f, dk, -10f);
        p.Circle(0.22f, 0.21f, 0.014f, d.accent);
    }

    static void Wyvern(Painter p, EnemyDef d)
    {
        Color c = d.color, wing = Dark(c, 0.75f);
        p.Tri(0.55f, 0.48f, 0.90f, 0.95f, 0.80f, 0.44f, wing);
        p.Capsule(0.70f, 0.40f, 0.94f, 0.28f, 0.03f, c);
        p.Tri(0.92f, 0.33f, 0.99f, 0.25f, 0.92f, 0.22f, d.accent);
        p.Capsule(0.52f, 0.32f, 0.50f, 0.20f, 0.025f, Dark(c, 0.8f));
        p.Capsule(0.62f, 0.32f, 0.64f, 0.20f, 0.025f, Dark(c, 0.8f));
        p.Ellipse(0.56f, 0.42f, 0.18f, 0.11f, c);
        p.Ellipse(0.55f, 0.38f, 0.13f, 0.05f, Light(c, 0.35f));
        p.Capsule(0.43f, 0.47f, 0.30f, 0.62f, 0.05f, c);
        p.Ellipse(0.23f, 0.64f, 0.09f, 0.05f, c, 10f);
        p.Tri(0.27f, 0.68f, 0.33f, 0.76f, 0.32f, 0.66f, Dark(c, 0.7f));
        p.Circle(0.21f, 0.66f, 0.016f, d.accent);
        p.Tri(0.48f, 0.50f, 0.62f, 0.98f, 0.70f, 0.50f, Light(wing, 0.15f));
    }

    static void Worm(Painter p, EnemyDef d)
    {
        Color c = d.color;
        p.Ellipse(0.72f, 0.05f, 0.24f, 0.05f, Dark(c, 0.55f));
        float[,] seg = { { 0.72f, 0.10f, 0.15f }, { 0.64f, 0.24f, 0.14f }, { 0.54f, 0.37f, 0.13f }, { 0.44f, 0.49f, 0.125f }, { 0.34f, 0.59f, 0.12f } };
        for (int i = 0; i < 5; i++)
        {
            p.Circle(seg[i, 0], seg[i, 1], seg[i, 2], i % 2 == 0 ? c : Dark(c, 0.85f));
            p.Circle(seg[i, 0] + 0.03f, seg[i, 1] + 0.04f, seg[i, 2] * 0.45f, Light(c, 0.2f));
        }
        p.Circle(0.27f, 0.61f, 0.075f, new Color(0.35f, 0.08f, 0.10f));
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f;
            float x = 0.27f + Mathf.Cos(a) * 0.06f, y = 0.61f + Mathf.Sin(a) * 0.06f;
            p.Tri(x, y, x + (0.27f - x) * 0.5f + 0.01f, y + (0.61f - y) * 0.5f, x + (0.27f - x) * 0.5f - 0.01f, y + (0.61f - y) * 0.5f + 0.01f, new Color(1f, 0.97f, 0.9f));
        }
        p.Circle(0.27f, 0.61f, 0.025f, d.accent);
    }

    static void Golem(Painter p, EnemyDef d)
    {
        Color c = d.color, dk = Dark(c, 0.7f);
        p.Box(0.70f, 0.40f, 0.06f, 0.15f, 0.02f, dk);
        p.Box(0.42f, 0.13f, 0.065f, 0.11f, 0.02f, dk);
        p.Box(0.60f, 0.13f, 0.065f, 0.11f, 0.02f, Dark(c, 0.8f));
        p.Box(0.51f, 0.43f, 0.19f, 0.17f, 0.03f, c);
        p.Box(0.46f, 0.70f, 0.09f, 0.08f, 0.02f, c);
        p.Box(0.30f, 0.37f, 0.07f, 0.16f, 0.02f, Light(c, 0.08f));
        p.Box(0.30f, 0.19f, 0.08f, 0.05f, 0.02f, dk);
        p.Capsule(0.45f, 0.52f, 0.56f, 0.38f, 0.006f, Dark(c, 0.5f));
        p.Capsule(0.62f, 0.55f, 0.58f, 0.46f, 0.006f, Dark(c, 0.5f));
        p.Circle(0.50f, 0.45f, 0.07f, A(d.accent, 0.35f));
        p.Circle(0.50f, 0.45f, 0.03f, Light(d.accent, 0.4f));
        p.Box(0.42f, 0.71f, 0.04f, 0.015f, 0.006f, Light(d.accent, 0.3f));
    }

    static void Shark(Painter p, EnemyDef d)
    {
        Color c = d.color;
        p.Tri(0.82f, 0.40f, 0.99f, 0.62f, 0.97f, 0.22f, Dark(c, 0.85f));
        p.Tri(0.46f, 0.50f, 0.58f, 0.74f, 0.64f, 0.50f, Dark(c, 0.9f));
        p.Ellipse(0.50f, 0.40f, 0.38f, 0.14f, c);
        p.Ellipse(0.46f, 0.35f, 0.30f, 0.07f, new Color(0.93f, 0.93f, 0.90f));
        p.Tri(0.42f, 0.33f, 0.50f, 0.17f, 0.55f, 0.33f, Dark(c, 0.8f));
        for (int i = 0; i < 3; i++) p.Capsule(0.32f + i * 0.03f, 0.44f, 0.31f + i * 0.03f, 0.36f, 0.004f, Dark(c, 0.6f));
        p.Circle(0.21f, 0.45f, 0.018f, EyeDark);
        p.Capsule(0.13f, 0.36f, 0.26f, 0.35f, 0.008f, new Color(0.35f, 0.1f, 0.12f));
        for (int i = 0; i < 4; i++) p.Tri(0.15f + i * 0.03f, 0.36f, 0.17f + i * 0.03f, 0.36f, 0.16f + i * 0.03f, 0.33f, Color.white);
    }

    static void Kraken(Painter p, EnemyDef d)
    {
        Color c = d.color, dk = Dark(c, 0.75f);
        for (int k = 0; k < 7; k++)
        {
            float bx = 0.30f + k * 0.07f;
            float px = bx, py = 0.42f;
            float dir = (k - 3) * 0.035f;
            for (int s = 1; s <= 5; s++)
            {
                float nx = bx + dir * s + Mathf.Sin(s * 1.3f + k) * 0.035f, ny = 0.42f - s * 0.075f;
                p.Capsule(px, py, nx, ny, 0.035f - s * 0.005f, k % 2 == 0 ? c : dk);
                px = nx; py = ny;
            }
        }
        p.Ellipse(0.52f, 0.62f, 0.26f, 0.28f, c);
        p.Ellipse(0.56f, 0.72f, 0.14f, 0.12f, Light(c, 0.15f));
        foreach (var s in new[] { new Vector2(0.62f, 0.78f), new Vector2(0.70f, 0.62f), new Vector2(0.48f, 0.82f) }) p.Circle(s.x, s.y, 0.025f, dk);
        p.Circle(0.38f, 0.56f, 0.06f, new Color(1f, 0.98f, 0.9f));
        p.Circle(0.53f, 0.54f, 0.065f, new Color(1f, 0.98f, 0.9f));
        p.Circle(0.37f, 0.55f, 0.032f, d.accent);
        p.Circle(0.52f, 0.53f, 0.035f, d.accent);
        p.Circle(0.365f, 0.55f, 0.014f, EyeDark);
        p.Circle(0.515f, 0.53f, 0.015f, EyeDark);
    }

    // ================================================================ 화가

    // 모양을 '부호 있는 거리'(모양 안쪽이면 음수)로 그려서 테두리가 부드럽게 나오도록 합니다.
    class Painter
    {
        readonly int n;
        readonly Color[] px;
        public float Top { get; private set; }

        public Painter(int n) { this.n = n; px = new Color[n * n]; }

        delegate float Sdf(float u, float v);

        void Draw(Sdf sdf, Color c, float minU, float minV, float maxU, float maxV)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minU * n) - 2), x1 = Mathf.Min(n - 1, Mathf.CeilToInt(maxU * n) + 2);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minV * n) - 2), y1 = Mathf.Min(n - 1, Mathf.CeilToInt(maxV * n) + 2);
            for (int y = y0; y <= y1; y++)
            {
                float v = (y + 0.5f) / n;
                for (int x = x0; x <= x1; x++)
                {
                    float u = (x + 0.5f) / n;
                    float a = Mathf.Clamp01(0.5f - sdf(u, v) * n) * c.a;
                    if (a <= 0f) continue;
                    int i = y * n + x;
                    var dst = px[i];
                    float outA = a + dst.a * (1f - a);
                    if (outA <= 0f) continue;
                    px[i] = new Color(
                        (c.r * a + dst.r * dst.a * (1f - a)) / outA,
                        (c.g * a + dst.g * dst.a * (1f - a)) / outA,
                        (c.b * a + dst.b * dst.a * (1f - a)) / outA,
                        outA);
                }
            }
        }

        public void Circle(float cx, float cy, float r, Color c) =>
            Draw((u, v) => Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) - r, c, cx - r, cy - r, cx + r, cy + r);

        public void Ellipse(float cx, float cy, float rx, float ry, Color c, float angle = 0f)
        {
            float cs = Mathf.Cos(angle * Mathf.Deg2Rad), sn = Mathf.Sin(angle * Mathf.Deg2Rad);
            float m = Mathf.Max(rx, ry);
            Draw((u, v) =>
            {
                float dx = u - cx, dy = v - cy;
                float lx = dx * cs + dy * sn, ly = -dx * sn + dy * cs;
                float k = Mathf.Sqrt((lx / rx) * (lx / rx) + (ly / ry) * (ly / ry));
                return (k - 1f) * Mathf.Min(rx, ry);
            }, c, cx - m, cy - m, cx + m, cy + m);
        }

        public void Capsule(float ax, float ay, float bx, float by, float r, Color c)
        {
            float pax0 = bx - ax, pay0 = by - ay;
            float len2 = Mathf.Max(1e-6f, pax0 * pax0 + pay0 * pay0);
            Draw((u, v) =>
            {
                float pax = u - ax, pay = v - ay;
                float h = Mathf.Clamp01((pax * pax0 + pay * pay0) / len2);
                float dx = pax - pax0 * h, dy = pay - pay0 * h;
                return Mathf.Sqrt(dx * dx + dy * dy) - r;
            }, c, Mathf.Min(ax, bx) - r, Mathf.Min(ay, by) - r, Mathf.Max(ax, bx) + r, Mathf.Max(ay, by) + r);
        }

        // 둥근 모서리 사각형 (hx, hy = 가로/세로 절반 크기)
        public void Box(float cx, float cy, float hx, float hy, float round, Color c, float angle = 0f)
        {
            float cs = Mathf.Cos(angle * Mathf.Deg2Rad), sn = Mathf.Sin(angle * Mathf.Deg2Rad);
            float m = Mathf.Sqrt(hx * hx + hy * hy);
            Draw((u, v) =>
            {
                float dx = u - cx, dy = v - cy;
                float lx = Mathf.Abs(dx * cs + dy * sn) - (hx - round), ly = Mathf.Abs(-dx * sn + dy * cs) - (hy - round);
                float ox = Mathf.Max(lx, 0f), oy = Mathf.Max(ly, 0f);
                return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(lx, ly), 0f) - round;
            }, c, cx - m, cy - m, cx + m, cy + m);
        }

        public void Tri(float ax, float ay, float bx, float by, float cx, float cy, Color c)
        {
            Draw((u, v) => TriDist(u, v, ax, ay, bx, by, cx, cy), c,
                Mathf.Min(ax, Mathf.Min(bx, cx)), Mathf.Min(ay, Mathf.Min(by, cy)), Mathf.Max(ax, Mathf.Max(bx, cx)), Mathf.Max(ay, Mathf.Max(by, cy)));
        }

        // 납작한 고리 (천사 고리)
        public void Ring(float cx, float cy, float rx, float ry, float w, Color c)
        {
            Draw((u, v) =>
            {
                float k = Mathf.Sqrt(((u - cx) / rx) * ((u - cx) / rx) + ((v - cy) / ry) * ((v - cy) / ry));
                return Mathf.Abs(k - 1f) * ry - w * 0.5f;
            }, c, cx - rx - w, cy - ry - w, cx + rx + w, cy + ry + w);
        }

        static float TriDist(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            // 삼각형까지의 거리 (안쪽이면 음수)
            float e0x = bx - ax, e0y = by - ay, e1x = cx - bx, e1y = cy - by, e2x = ax - cx, e2y = ay - cy;
            float v0x = px - ax, v0y = py - ay, v1x = px - bx, v1y = py - by, v2x = px - cx, v2y = py - cy;
            float h0 = Mathf.Clamp01((v0x * e0x + v0y * e0y) / (e0x * e0x + e0y * e0y));
            float h1 = Mathf.Clamp01((v1x * e1x + v1y * e1y) / (e1x * e1x + e1y * e1y));
            float h2 = Mathf.Clamp01((v2x * e2x + v2y * e2y) / (e2x * e2x + e2y * e2y));
            float d0x = v0x - e0x * h0, d0y = v0y - e0y * h0;
            float d1x = v1x - e1x * h1, d1y = v1y - e1y * h1;
            float d2x = v2x - e2x * h2, d2y = v2y - e2y * h2;
            float s = Mathf.Sign(e0x * e2y - e0y * e2x);
            float dist = Mathf.Min(d0x * d0x + d0y * d0y, Mathf.Min(d1x * d1x + d1y * d1y, d2x * d2x + d2y * d2y));
            float sx = Mathf.Min(s * (v0x * e0y - v0y * e0x), Mathf.Min(s * (v1x * e1y - v1y * e1x), s * (v2x * e2y - v2y * e2x)));
            return -Mathf.Sqrt(dist) * Mathf.Sign(sx);
        }

        // 테두리선 + 위쪽이 밝은 음영을 넣고 텍스처로 만듦
        public Texture2D Finish(Color outline)
        {
            var result = new Color[px.Length];
            int top = 0;
            for (int y = 0; y < n; y++)
            {
                float shade = 0.84f + 0.26f * y / n;
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    var c = px[i];
                    if (c.a > 0.5f) top = Mathf.Max(top, y);
                    // 주변 2픽셀 안에 몸이 있으면 테두리
                    float near = 0f;
                    for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if (dx * dx + dy * dy > 5) continue;
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue;
                        near = Mathf.Max(near, px[yy * n + xx].a);
                    }
                    var lit = new Color(Mathf.Min(1f, c.r * shade), Mathf.Min(1f, c.g * shade), Mathf.Min(1f, c.b * shade), c.a);
                    float oa = Mathf.Clamp01(near * 1.6f - 0.6f) * (1f - c.a);
                    float outA = c.a + oa;
                    result[i] = outA <= 0f ? Color.clear : new Color(
                        (lit.r * c.a + outline.r * oa) / outA,
                        (lit.g * c.a + outline.g * oa) / outA,
                        (lit.b * c.a + outline.b * oa) / outA,
                        Mathf.Clamp01(outA));
                }
            }
            Top = (top + 1f) / n;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels(result);
            tex.Apply();
            return tex;
        }
    }
}
