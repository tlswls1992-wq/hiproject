using UnityEngine;

public enum Team { Hero, Enemy }

// 동료(용사 포함)와 적 모두 이 스크립트로 싸웁니다.
public class Unit : MonoBehaviour
{
    public Team team;
    public HeroClass heroClass;      // 영웅일 때만 사용 (적은 null)
    public string faction;           // 소속 (동료만 사용, 시너지 계산용)
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
    public Vector2 homePosition;     // 편성한 자리 (동료만 사용)
    public bool isBoss;
    public bool isLeader;            // 용사
    public CharacterArt art;         // 캐릭터 그림 (있으면 동그라미 대신 그림으로 나옴, Setup 전에 넣기)
    public EnemyLook.Look look;      // 적 모습 (코드로 그린 그림, Setup 전에 넣기)
    public bool healer;              // 공격 대신 아군을 치유
    public string title;             // 이름표 (정예 · 보스만 머리 위에 표시)
    public Color projectileColor = Color.white;
    public string attackSound;       // 공격할 때 효과음 (AudioManager)
    public string impactSound;       // 투사체가 맞을 때 효과음

    const float BarWidth = 0.8f;

    float cooldownTimer;
    float flashTimer;
    Color baseColor;
    SpriteRenderer body;
    SpriteRenderer ring;
    SpriteRenderer hpBack;
    SpriteRenderer hpFillRenderer;
    Transform hpFill;
    float animClock;                 // 대기 그림 재생용 시간
    float attackAnimStart = -100f;   // 공격 그림을 시작한 시간
    bool facingRight = true;         // 그림은 오른쪽(적이 오는 쪽)을 보고 있음

    bool UsesArt => art != null && art.HasBattleSprites;
    bool UsesLook => !UsesArt && look != null;

    public bool IsAlive => hp > 0f;

    // 능력치를 정한 다음에 호출해서 모양과 체력바를 만듭니다.
    // ringColor를 주면 몸 뒤에 등급 색 테두리를 그립니다.
    public void Setup(Sprite sprite, Color color, Color ringColor)
    {
        hp = maxHp;
        baseColor = color;

        if (UsesLook) ringColor = new Color(0f, 0f, 0f, 0.6f); // 코드로 그린 적은 발밑에 그림자
        if (ringColor.a > 0f)
        {
            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(transform, false);
            ring = ringGo.AddComponent<SpriteRenderer>();
            ring.sprite = SpriteFactory.Circle();
            if (UsesLook)
            {
                float w = size * look.scale * 0.42f;
                ringGo.transform.localPosition = new Vector3(0f, -size * 0.5f, 0f);
                ringGo.transform.localScale = new Vector3(w, w * 0.28f, 1f);
                ring.color = ringColor;
            }
            else if (UsesArt)
            {
                // 그림이 있으면 발밑에 등급 색 납작한 원 (그림자처럼)
                ringGo.transform.localPosition = new Vector3(0f, -size * 0.5f, 0f);
                ringGo.transform.localScale = new Vector3(size + 0.6f, (size + 0.6f) * 0.32f, 1f);
                ring.color = new Color(ringColor.r, ringColor.g, ringColor.b, 0.55f);
            }
            else
            {
                ringGo.transform.localScale = Vector3.one * (size + 0.18f);
                ring.sprite = sprite;
                ring.color = ringColor;
            }
        }

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(transform, false);
        body = bodyGo.AddComponent<SpriteRenderer>();
        float barY = size * 0.5f + 0.15f;
        if (UsesArt)
        {
            // 그림의 발 위치가 동그라미 아래쪽에 오도록 놓음 (그림 크기는 CharacterArt.BattleCanvasHeight)
            bodyGo.transform.localPosition = new Vector3(0f, -size * 0.5f, 0f);
            body.sprite = art.IdleSprite(0f);
            baseColor = color = Color.white;
            body.color = Color.white;
            lastX = transform.position.x;
            lastY = transform.position.y;
            barY = -size * 0.5f + CharacterArt.BattleCanvasHeight * (1f - CharacterArt.FeetPivot) * 0.92f;
        }
        else if (UsesLook)
        {
            // 코드로 그린 적: 발이 동그라미 아래쪽에 오도록 놓고, 몸 크기에 맞춰 키움
            bodyGo.transform.localPosition = new Vector3(0f, -size * 0.5f, 0f);
            bodyGo.transform.localScale = Vector3.one * size * look.scale;
            body.sprite = look.sprite;
            baseColor = color = Color.white;
            body.color = Color.white;
            lastX = transform.position.x;
            lastY = transform.position.y;
            barY = -size * 0.5f + size * look.scale * (look.top - EnemyLook.Ground) + 0.18f + (look.hovers ? size * 0.35f : 0f);
        }
        else
        {
            bodyGo.transform.localScale = Vector3.one * size;
            body.sprite = sprite;
            body.color = color;
        }

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

    // 이름표를 붙일 위치 (체력바 바로 위)
    public Vector3 NameAnchor => transform.position + new Vector3(0f, hpBack != null ? hpBack.transform.localPosition.y + 0.15f : size, 0f);

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
            // 맞으면 잠깐 번쩍 (그림은 붉게, 동그라미는 하얗게)
            body.color = flashTimer > 0f ? (UsesArt || UsesLook ? new Color(1f, 0.55f, 0.55f) : Color.white) : baseColor;
        }
        if (UsesArt) Animate(dt);
        else if (UsesLook) AnimateLook(dt);

        Vector2 pos = transform.position;
        Vector2 moveDir = team == Team.Hero ? HeroThink(pos, battle) : EnemyThink(pos, battle);
        Vector2 velocity = moveDir * moveSpeed + battle.SeparationFor(this);
        transform.position = battle.ClampToArena(pos + velocity * dt);
        UpdateSorting();
    }

    // 동료: 근거리는 적에게 다가가서 공격하고, 원거리는 사거리 안에 들어올 때까지 다가가서 공격합니다.
    // 적이 없으면 편성한 자리로 돌아갑니다.
    Vector2 HeroThink(Vector2 pos, BattleManager battle)
    {
        if (healer) return HealerThink(pos, battle);

        Unit target = battle.FindNearestOpponent(this);
        if (target == null) return ToHome(pos);
        if (InRange(pos, target)) { TryAttack(target); return Vector2.zero; }
        return DirectionTo(pos, target.transform.position);
    }

    // 치유 직업(성녀·힐러): 다친 아군이 있으면 사거리까지 다가가서 치유하고, 없으면 아군 뒤를 따라갑니다.
    Vector2 HealerThink(Vector2 pos, BattleManager battle)
    {
        float range = attackRange * battle.RangeMultiplier(this);
        Unit ally = battle.FindMostHurtAlly(this);
        if (ally != null)
        {
            if (Vector2.Distance(pos, ally.transform.position) > range) return DirectionTo(pos, ally.transform.position);
            if (cooldownTimer <= 0f)
            {
                cooldownTimer = attackCooldown * battle.CooldownMultiplier(this);
                ally.Heal(damage * battle.HealMultiplier(this));
                attackAnimStart = Time.time;
                AudioManager.Play("heal", 0.45f);
                battle.SpawnEffect(ally.transform.position, ally.size + 0.6f, new Color(0.5f, 1f, 0.5f, 0.5f));
            }
            return Vector2.zero;
        }

        if (battle.FindNearestOpponent(this) == null) return team == Team.Hero ? ToHome(pos) : Vector2.zero;
        Unit friend = battle.FindNearestFighter(this);
        if (friend != null && Vector2.Distance(pos, friend.transform.position) > range * 0.6f)
            return DirectionTo(pos, friend.transform.position);
        return Vector2.zero;
    }

    Vector2 ToHome(Vector2 pos) => Vector2.Distance(pos, homePosition) > 0.1f ? DirectionTo(pos, homePosition) : Vector2.zero;

    // 적: 처음에는 제자리에 진을 치고 있다가, 파티원이 접근 거리 안에 들어오면 싸우기 시작합니다.
    //     (한 번 싸우기 시작하거나 공격을 받으면 끝까지 쫓아가요)
    public const float EnemyAggroRange = 4.5f;
    bool engaged;

    Vector2 EnemyThink(Vector2 pos, BattleManager battle)
    {
        Unit target = battle.FindNearestOpponent(this);
        if (target == null) return Vector2.zero;
        if (!engaged)
        {
            float aggro = Mathf.Max(EnemyAggroRange, attackRange + 1.5f) + size * 0.5f;
            if (Vector2.Distance(pos, target.transform.position) > aggro) return Vector2.zero; // 아직 대기
            engaged = true;
        }
        if (healer) return HealerThink(pos, battle);
        if (InRange(pos, target)) { TryAttack(target); return Vector2.zero; }
        return DirectionTo(pos, target.transform.position);
    }

    bool InRange(Vector2 pos, Unit target)
    {
        float range = attackRange * BattleManager.Instance.RangeMultiplier(this);
        return Vector2.Distance(pos, target.transform.position) <= range + (size + target.size) * 0.5f;
    }

    static Vector2 DirectionTo(Vector2 from, Vector2 to) => (to - from).normalized;

    // 그림이 있는 캐릭터의 동작: 공격 > 달리기 > 대기 순서로 정해서 자연스럽게 바꿈. 움직이는 방향을 바라봄
    void Animate(float dt)
    {
        animClock += dt;
        Vector2 now = transform.position;
        float vx = dt > 0f ? (now.x - lastX) / dt : 0f;
        float moved = dt > 0f ? Mathf.Abs(vx) + Mathf.Abs(now.y - lastY) / dt : 0f;
        // 살짝 밀리는 정도로는 방향을 바꾸지 않음
        if (vx > 0.5f) facingRight = true;
        else if (vx < -0.5f) facingRight = false;
        lastX = now.x;
        lastY = now.y;

        // 잠깐 멈췄다 움직이는 걸 반복해도 깜빡이지 않게, 멈춘 뒤 0.2초 동안은 달리기 유지
        runHold = moved > moveSpeed * 0.3f ? 0.2f : runHold - dt;
        bool running = runHold > 0f;
        if (running) runClock += dt;
        // 달리기 흔들림이 갑자기 시작/멈추지 않도록 0 ~ 1 사이로 부드럽게 바뀌는 값
        runBlend = Mathf.MoveTowards(runBlend, running ? 1f : 0f, dt * 6f);

        // 공격 그림이 공격 간격보다 길면 빨리 재생해서 다음 공격 전에 끝나게 함
        float cd = Mathf.Max(0.2f, attackCooldown * 0.9f);
        float speed = Mathf.Max(1f, art.AttackLength / cd);
        var atk = art.AttackSprite(Time.time - attackAnimStart, speed);

        var bodyT = body.transform;
        Vector3 basePos = new Vector3(0f, -size * 0.5f, 0f);
        Vector3 scale = Vector3.one;
        Quaternion rot = Quaternion.identity;
        if (atk != null)
        {
            body.sprite = atk;
        }
        else if (running && art.HasRun)
        {
            body.sprite = art.RunSprite(runClock);
            if (art.RunIsStill)
            {
                // 달리기 그림이 한 장이면: 발걸음(1초에 약 3걸음)에 맞춰 아주 살짝, 부드럽게 오르내림.
                // (예전에는 크게 튀어서 통통 튀는 것처럼 보였음)
                float phase = runClock * Mathf.PI * RunStepsPerSecond;
                float lift = Mathf.Sin(phase);
                lift *= lift;                                   // 0 ~ 1, 바닥에서 뾰족하지 않고 둥글게
                basePos.y += lift * 0.035f * runBlend;
                float sway = Mathf.Sin(phase * 0.5f) * 1.2f * runBlend; // 걸음마다 상체가 아주 살짝 좌우로
                rot = Quaternion.Euler(0f, 0f, facingRight ? sway : -sway);
            }
        }
        else
        {
            body.sprite = art.IdleSprite(animClock);
            if (art.IdleIsStill)
            {
                // 대기 그림이 한 장이면: 숨 쉬듯 아주 살짝 커졌다 작아짐 (발 위치는 그대로)
                float breath = Mathf.Sin(animClock * 2.4f);
                scale = new Vector3(1f - breath * 0.006f, 1f + breath * 0.014f, 1f);
            }
        }
        bodyT.localPosition = basePos;
        bodyT.localScale = scale;
        bodyT.localRotation = rot;
        body.flipX = !facingRight;
    }

    // 코드로 그린 적의 움직임: 대기 중엔 숨쉬기, 걸을 땐 통통 걸음, 공격할 땐 앞으로 덤벼듦.
    // 그림은 왼쪽을 보고 있으므로 오른쪽으로 갈 때만 뒤집음
    void AnimateLook(float dt)
    {
        animClock += dt;
        Vector2 now = transform.position;
        float vx = dt > 0f ? (now.x - lastX) / dt : 0f;
        float moved = dt > 0f ? Mathf.Abs(vx) + Mathf.Abs(now.y - lastY) / dt : 0f;
        if (vx > 0.3f) facingRight = true;
        else if (vx < -0.3f) facingRight = false;
        lastX = now.x;
        lastY = now.y;
        runHold = moved > moveSpeed * 0.3f ? 0.2f : runHold - dt;
        bool walking = runHold > 0f;
        if (walking) runClock += dt;
        runBlend = Mathf.MoveTowards(runBlend, walking ? 1f : 0f, dt * 6f);

        float s = size * look.scale;
        Vector3 pos = new Vector3(0f, -size * 0.5f, 0f);
        Vector3 scale = Vector3.one * s;
        float tilt = 0f;

        if (look.hovers)
        {
            // 떠 있는 적: 공중에서 천천히 오르내림
            pos.y += size * 0.35f + Mathf.Sin(animClock * 2.6f + transform.position.y) * 0.08f * s;
        }
        else
        {
            // 숨쉬기 + 걸음
            float breath = Mathf.Sin(animClock * 2.2f + transform.position.x);
            scale = new Vector3(s * (1f - breath * 0.012f), s * (1f + breath * 0.02f), 1f);
            float phase = runClock * Mathf.PI * 3.2f;
            float hop = Mathf.Sin(phase); hop *= hop;
            pos.y += hop * 0.07f * s * runBlend;
            tilt = Mathf.Sin(phase * 0.5f) * 4f * runBlend;
        }

        // 공격: 0.25초 동안 앞으로 덤볐다가 돌아옴
        float atk = Time.time - attackAnimStart;
        if (atk >= 0f && atk < 0.25f)
        {
            float k = Mathf.Sin(atk / 0.25f * Mathf.PI);
            pos.x += (facingRight ? 1f : -1f) * k * 0.18f * s;
            tilt += k * 10f;
        }

        var t = body.transform;
        t.localPosition = pos;
        t.localScale = scale;
        t.localRotation = Quaternion.Euler(0f, 0f, facingRight ? -tilt : tilt);
        body.flipX = facingRight;
    }

    float lastY;
    float runHold;
    float runClock;
    float runBlend;
    const float RunStepsPerSecond = 3f;

    float lastX;

    void TryAttack(Unit target)
    {
        if (cooldownTimer > 0f) return;
        var battle = BattleManager.Instance;
        cooldownTimer = attackCooldown * battle.CooldownMultiplier(this);
        attackAnimStart = Time.time;
        facingRight = target.transform.position.x >= transform.position.x;
        float dmg = damage * battle.DamageMultiplier(this);
        float splash = splashRadius * battle.SplashMultiplier(this);

        AudioManager.Play(attackSound, team == Team.Hero ? 0.55f : 0.4f);
        if (ranged) Projectile.Launch(this, target, dmg, splash, UsesArt || UsesLook ? projectileColor : baseColor, impactSound);
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
        engaged = true; // 맞으면 바로 싸우기 시작
        flashTimer = 0.08f;
        if (hp <= 0f)
        {
            hp = 0f;
            if (battle != null)
            {
                battle.OnUnitDied(this);
                // 쓰러질 때 연기처럼 퍼지는 효과
                battle.SpawnEffect(transform.position, size * 1.6f, new Color(0.9f, 0.85f, 0.8f, 0.45f));
                if (team == Team.Enemy) AudioManager.Play("die", 0.35f);
            }
            Destroy(gameObject);
        }
        else
        {
            UpdateHpBar();
        }
    }
}
