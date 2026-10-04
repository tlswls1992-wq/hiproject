using UnityEngine;

public enum Team { Hero, Enemy }

// 영웅과 적 모두 이 스크립트로 움직이고 싸웁니다.
public class Unit : MonoBehaviour
{
    public Team team;
    public HeroClass heroClass;      // 영웅일 때만 사용 (적은 null)
    public float maxHp;
    public float hp;
    public float damage;
    public float attackRange;
    public float attackCooldown;
    public float moveSpeed;
    public float splashRadius;
    public float size;
    public bool ranged;
    public int goldReward;           // 적을 잡았을 때 얻는 골드
    public Vector2 formationOffset;  // 부대 안에서 서 있을 자리

    const float BarWidth = 0.8f;

    float cooldownTimer;
    float flashTimer;
    Color baseColor;
    SpriteRenderer body;
    Transform hpFill;

    public bool IsAlive => hp > 0f;

    // 능력치를 정한 다음에 호출해서 모양과 체력바를 만듭니다.
    public void Setup(Sprite sprite, Color color)
    {
        hp = maxHp;
        baseColor = color;

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(transform, false);
        bodyGo.transform.localScale = Vector3.one * size;
        body = bodyGo.AddComponent<SpriteRenderer>();
        body.sprite = sprite;
        body.color = color;
        body.sortingOrder = 1;

        float barY = size * 0.5f + 0.15f;
        MakeBar("HpBack", new Color(0f, 0f, 0f, 0.6f), barY, 2).localScale = new Vector3(BarWidth, 0.1f, 1f);
        hpFill = MakeBar("HpFill", team == Team.Hero ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f), barY, 3);
        UpdateHpBar();
    }

    Transform MakeBar(string barName, Color color, float y, int order)
    {
        var go = new GameObject(barName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square();
        sr.color = color;
        sr.sortingOrder = order;
        return go.transform;
    }

    public void UpdateHpBar()
    {
        if (hpFill == null) return;
        float pct = Mathf.Clamp01(hp / maxHp);
        hpFill.localScale = new Vector3(BarWidth * pct, 0.1f, 1f);
        hpFill.localPosition = new Vector3(-BarWidth * (1f - pct) / 2f, hpFill.localPosition.y, 0f);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!IsAlive || gm == null || gm.IsPaused) return;

        float dt = Time.deltaTime;
        cooldownTimer -= dt;
        if (flashTimer > 0f)
        {
            flashTimer -= dt;
            body.color = flashTimer > 0f ? Color.white : baseColor;
        }

        Vector2 pos = transform.position;
        Unit target = gm.FindNearestOpponent(this);
        Vector2 moveDir = Vector2.zero;

        if (team == Team.Hero)
        {
            // 영웅: 부대 집결지 근처의 적만 쫓아가고, 너무 멀어지면 자기 자리로 돌아옵니다.
            Vector2 slot = gm.RallyPoint + formationOffset;
            bool tooFar = Vector2.Distance(pos, gm.RallyPoint) > GameManager.HeroAggroRadius + 2f;
            bool targetNearRally = target != null &&
                Vector2.Distance(target.transform.position, gm.RallyPoint) <= GameManager.HeroAggroRadius;

            if (!tooFar && target != null && InRange(pos, target)) TryAttack(target);
            else if (!tooFar && targetNearRally) moveDir = DirectionTo(pos, target.transform.position);
            else if (Vector2.Distance(pos, slot) > 0.1f) moveDir = DirectionTo(pos, slot);
        }
        else if (target != null)
        {
            // 적: 가장 가까운 영웅에게 달려가서 공격합니다.
            if (InRange(pos, target)) TryAttack(target);
            else moveDir = DirectionTo(pos, target.transform.position);
        }

        Vector2 velocity = moveDir * moveSpeed * gm.MoveMultiplier(this) + gm.SeparationFor(this);
        transform.position = pos + velocity * dt;
    }

    bool InRange(Vector2 pos, Unit target)
    {
        return Vector2.Distance(pos, target.transform.position) <= attackRange + (size + target.size) * 0.5f;
    }

    static Vector2 DirectionTo(Vector2 from, Vector2 to) => (to - from).normalized;

    void TryAttack(Unit target)
    {
        if (cooldownTimer > 0f) return;
        var gm = GameManager.Instance;
        cooldownTimer = attackCooldown * gm.CooldownMultiplier(this);
        float dmg = damage * gm.DamageMultiplier(this);
        float splash = splashRadius * gm.SplashMultiplier(this);

        if (ranged) Projectile.Launch(this, target, dmg, splash, baseColor);
        else gm.ApplyHit(target.transform.position, target, team, dmg, splash);
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive) return;
        var gm = GameManager.Instance;
        hp -= amount * (gm != null ? gm.DamageTakenMultiplier(this) : 1f);
        flashTimer = 0.08f;
        if (hp <= 0f)
        {
            hp = 0f;
            if (gm != null) gm.OnUnitDied(this);
            Destroy(gameObject);
        }
        else
        {
            UpdateHpBar();
        }
    }
}
