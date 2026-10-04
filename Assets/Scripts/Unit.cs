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
    public Vector2 formationSlot;    // 영웅이 진을 치고 서 있을 자리
    public bool isBoss;

    const float BarWidth = 0.8f;

    float cooldownTimer;
    float flashTimer;
    Color baseColor;
    SpriteRenderer body;
    SpriteRenderer ring;
    SpriteRenderer hpBack;
    SpriteRenderer hpFillRenderer;
    Transform hpFill;

    public bool IsAlive => hp > 0f;

    // 능력치를 정한 다음에 호출해서 모양과 체력바를 만듭니다.
    // ringColor를 주면 몸 뒤에 등급 색 테두리를 그립니다.
    public void Setup(Sprite sprite, Color color, Color ringColor)
    {
        hp = maxHp;
        baseColor = color;

        if (ringColor.a > 0f)
        {
            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localScale = Vector3.one * (size + 0.18f);
            ring = ringGo.AddComponent<SpriteRenderer>();
            ring.sprite = sprite;
            ring.color = ringColor;
        }

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(transform, false);
        bodyGo.transform.localScale = Vector3.one * size;
        body = bodyGo.AddComponent<SpriteRenderer>();
        body.sprite = sprite;
        body.color = color;

        float barY = size * 0.5f + 0.15f;
        hpBack = MakeBar("HpBack", new Color(0f, 0f, 0f, 0.6f), barY);
        hpBack.transform.localScale = new Vector3(BarWidth, 0.1f, 1f);
        hpFillRenderer = MakeBar("HpFill", team == Team.Hero ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f), barY);
        hpFill = hpFillRenderer.transform;
        UpdateHpBar();
        UpdateSorting();
    }

    SpriteRenderer MakeBar(string barName, Color color, float y)
    {
        var go = new GameObject(barName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square();
        sr.color = color;
        return sr;
    }

    public void UpdateHpBar()
    {
        if (hpFill == null) return;
        float pct = Mathf.Clamp01(hp / maxHp);
        hpFill.localScale = new Vector3(BarWidth * pct, 0.1f, 1f);
        hpFill.localPosition = new Vector3(-BarWidth * (1f - pct) / 2f, hpFill.localPosition.y, 0f);
    }

    // 옆에서 보는 화면이라 아래쪽(앞쪽)에 있는 유닛이 위에 그려지도록 순서를 정합니다.
    void UpdateSorting()
    {
        int order = Mathf.RoundToInt(-transform.position.y * 20f) * 4;
        if (ring != null) ring.sortingOrder = order - 1;
        body.sortingOrder = order;
        hpBack.sortingOrder = order + 1;
        hpFillRenderer.sortingOrder = order + 2;
    }

    void Update()
    {
        var battle = BattleManager.Instance;
        if (!IsAlive || battle == null || battle.IsPaused) return;

        float dt = Time.deltaTime;
        cooldownTimer -= dt;
        if (flashTimer > 0f)
        {
            flashTimer -= dt;
            body.color = flashTimer > 0f ? Color.white : baseColor;
        }

        Vector2 pos = transform.position;
        Unit target = battle.FindNearestOpponent(this);
        Vector2 moveDir = team == Team.Hero ? HeroThink(pos, target) : EnemyThink(pos, target, battle);

        Vector2 velocity = moveDir * moveSpeed * battle.MoveMultiplier(this) + battle.SeparationFor(this);
        Vector2 next = pos + velocity * dt;
        next.y = Mathf.Clamp(next.y, BattleManager.LaneBottom, BattleManager.LaneTop);
        transform.position = next;
        UpdateSorting();
    }

    // 영웅: 자기 자리를 지키며 싸웁니다. 근접 영웅만 가까이 온 적에게 돌격했다가 돌아옵니다.
    Vector2 HeroThink(Vector2 pos, Unit target)
    {
        if (heroClass != null && heroClass.healer) return HealerThink(pos);

        bool tooFar = Vector2.Distance(pos, formationSlot) > BattleManager.HeroLeash + 1.5f;
        if (target != null && !tooFar)
        {
            if (InRange(pos, target)) { TryAttack(target); return Vector2.zero; }
            if (!ranged && Vector2.Distance(target.transform.position, formationSlot) <= BattleManager.HeroLeash)
                return DirectionTo(pos, target.transform.position);
        }
        return Vector2.Distance(pos, formationSlot) > 0.1f ? DirectionTo(pos, formationSlot) : Vector2.zero;
    }

    // 사제: 자리를 지키며 사거리 안에서 가장 많이 다친 아군을 치유합니다.
    Vector2 HealerThink(Vector2 pos)
    {
        var battle = BattleManager.Instance;
        if (cooldownTimer <= 0f)
        {
            Unit ally = battle.FindMostHurtAlly(this, attackRange);
            if (ally != null)
            {
                cooldownTimer = attackCooldown * battle.CooldownMultiplier(this);
                ally.Heal(damage * battle.HealMultiplier(this));
                battle.SpawnEffect(ally.transform.position, ally.size + 0.6f, new Color(0.5f, 1f, 0.5f, 0.5f));
            }
        }
        return Vector2.Distance(pos, formationSlot) > 0.1f ? DirectionTo(pos, formationSlot) : Vector2.zero;
    }

    // 적: 오른쪽에서 왼쪽으로 행군하다가, 가까운 동료가 있으면 싸우고, 용사에게 닿으면 용사를 공격합니다.
    Vector2 EnemyThink(Vector2 pos, Unit target, BattleManager battle)
    {
        if (target != null)
        {
            if (InRange(pos, target)) { TryAttack(target); return Vector2.zero; }
            float aggro = attackRange + size * 0.5f + 1.5f;
            if (Vector2.Distance(pos, target.transform.position) <= aggro)
                return DirectionTo(pos, target.transform.position);
        }

        if (pos.x - size * 0.5f > BattleManager.LeaderFrontX + attackRange) return Vector2.left;

        if (cooldownTimer <= 0f)
        {
            cooldownTimer = attackCooldown;
            battle.DamageLeader(damage);
        }
        return Vector2.zero;
    }

    bool InRange(Vector2 pos, Unit target)
    {
        return Vector2.Distance(pos, target.transform.position) <= attackRange + (size + target.size) * 0.5f;
    }

    static Vector2 DirectionTo(Vector2 from, Vector2 to) => (to - from).normalized;

    void TryAttack(Unit target)
    {
        if (cooldownTimer > 0f) return;
        var battle = BattleManager.Instance;
        cooldownTimer = attackCooldown * battle.CooldownMultiplier(this);
        float dmg = damage * battle.DamageMultiplier(this);
        float splash = splashRadius * battle.SplashMultiplier(this);

        if (ranged) Projectile.Launch(this, target, dmg, splash, baseColor);
        else battle.ApplyHit(target.transform.position, target, team, dmg, splash);
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        hp = Mathf.Min(maxHp, hp + amount);
        UpdateHpBar();
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive) return;
        var battle = BattleManager.Instance;
        hp -= amount * (battle != null ? battle.DamageTakenMultiplier(this) : 1f);
        flashTimer = 0.08f;
        if (hp <= 0f)
        {
            hp = 0f;
            if (battle != null) battle.OnUnitDied(this);
            Destroy(gameObject);
        }
        else
        {
            UpdateHpBar();
        }
    }
}
