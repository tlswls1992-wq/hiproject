using UnityEngine;

public enum Team { Hero, Enemy }

// 동료(용사 포함)와 적 모두 이 스크립트로 싸웁니다.
public class Unit : MonoBehaviour
{
    public Team team;
    public HeroClass heroClass;      // 영웅일 때만 사용 (적은 null)
    public float maxHp;
    public float hp;
    public float damage;
    public float attackRange;
    public float attackCooldown;
    public float moveSpeed;          // 적만 움직입니다 (동료는 칸에 고정)
    public float splashRadius;
    public float size;
    public bool ranged;
    public int goldReward;           // 적을 잡았을 때 얻는 골드
    public bool isBoss;
    public bool isLeader;            // 용사

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
        if (team == Team.Hero)
        {
            // 동료: 편성한 칸에서 움직이지 않고 싸웁니다.
            HeroThink(pos, battle);
            return;
        }

        Vector2 moveDir = EnemyThink(pos, battle);
        Vector2 velocity = moveDir * moveSpeed + battle.SeparationFor(this);
        Vector2 next = pos + velocity * dt;
        next.y = Mathf.Clamp(next.y, BattleManager.LaneBottom, BattleManager.LaneTop);
        transform.position = next;
        UpdateSorting();
    }

    // 동료: 사거리 안에 들어온 가장 가까운 적을 공격합니다. 사제는 다친 아군을 치유합니다.
    void HeroThink(Vector2 pos, BattleManager battle)
    {
        if (cooldownTimer > 0f) return;

        if (heroClass != null && heroClass.healer)
        {
            Unit ally = battle.FindMostHurtAlly(this, attackRange * battle.RangeMultiplier(this));
            if (ally == null) return;
            cooldownTimer = attackCooldown * battle.CooldownMultiplier(this);
            ally.Heal(damage * battle.HealMultiplier(this));
            battle.SpawnEffect(ally.transform.position, ally.size + 0.6f, new Color(0.5f, 1f, 0.5f, 0.5f));
            return;
        }

        Unit target = battle.FindNearestOpponent(this);
        if (target != null && InRange(pos, target)) TryAttack(target);
    }

    // 적: 자기 줄을 따라 왼쪽으로 행군하다가, 가장 가까운 동료에게 다가가 공격합니다.
    Vector2 EnemyThink(Vector2 pos, BattleManager battle)
    {
        Unit target = battle.FindNearestOpponent(this);
        if (target == null) return Vector2.zero;
        if (InRange(pos, target)) { TryAttack(target); return Vector2.zero; }

        Vector2 tpos = target.transform.position;
        if (pos.x - tpos.x > 2.5f + attackRange) return Vector2.left;
        return DirectionTo(pos, tpos);
    }

    bool InRange(Vector2 pos, Unit target)
    {
        float range = attackRange * BattleManager.Instance.RangeMultiplier(this);
        return Vector2.Distance(pos, target.transform.position) <= range + (size + target.size) * 0.5f;
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
