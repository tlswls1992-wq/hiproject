using System.Collections.Generic;
using UnityEngine;

// 전투 한 판(스테이지)을 진행합니다: 전장, 동료 배치, 웨이브, 강화 카드, 전투 화면 UI.
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    // ---- 밸런스 숫자 (자유롭게 바꿔 보세요) ----
    public const int MaxDeploy = 30;     // 전투에 나가는 최대 동료 수
    const float LeaderMaxHp = 300f;      // 용사 체력
    const float SpawnInterval = 0.6f;
    const float WaveBreak = 2f;

    // ---- 전장 배치 (화면 고정, 옆에서 보는 시점) ----
    public const float LaneTop = 2.5f;        // 유닛이 다닐 수 있는 가장 위쪽
    public const float LaneBottom = -5.5f;    // 가장 아래쪽
    public const float LeaderX = -12.3f;      // 용사가 서 있는 위치
    public const float LeaderFrontX = -11.3f; // 적이 여기까지 오면 용사를 공격
    public const float HeroLeash = 3.5f;      // 근접 동료가 자기 자리에서 이만큼 안의 적에게 돌격
    const float FrontLineX = -3f;             // 동료 진형의 맨 앞줄
    const float ColumnGap = 1.0f;             // 줄 사이 간격 (가로)
    const int RowsPerColumn = 7;              // 한 줄에 서는 동료 수 (세로)

    enum EnemyKind { Grunt, Archer, Brute, Boss }

    public readonly List<Unit> heroes = new List<Unit>();
    public readonly List<Unit> enemies = new List<Unit>();

    public Transform World { get; private set; }
    public int Stage { get; private set; }
    public int Wave { get; private set; }
    public int GoldEarned { get; private set; }
    public float LeaderHp { get; private set; }
    public bool IsPaused => World == null || finished || upgradeChoices != null;

    // 강화 카드로 올라가는 보너스 (이번 스테이지에서만 유지)
    float damageBonus = 1f;
    float attackSpeedBonus = 1f;
    float moveBonus = 1f;
    float hpBonus = 1f;

    Camera cam;
    SpriteRenderer leaderRenderer;
    Color leaderColor = new Color(1f, 0.85f, 0.3f);
    float leaderFlashTimer;
    Unit boss;
    readonly List<EnemyKind> spawnQueue = new List<EnemyKind>();
    bool waveInProgress;
    float spawnTimer;
    float waveBreakTimer;
    bool fastForward;
    bool finished;
    bool victory;
    float finishTimer;
    System.Action<bool> onFinished;

    class Upgrade
    {
        public string title;
        public string desc;
        public System.Action apply;
    }

    List<Upgrade> upgradeChoices; // null이 아니면 강화 선택 화면이 떠 있는 상태

    void Awake() { Instance = this; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    float HalfScreenWidth => cam.orthographicSize * cam.aspect;

    // ================= 전투 시작 / 끝 =================

    public void Begin(int stage, System.Action<bool> onFinished)
    {
        End();
        cam = Camera.main;
        this.onFinished = onFinished;
        Stage = stage;
        Wave = 0;
        GoldEarned = 0;
        LeaderHp = LeaderMaxHp;
        finished = false;
        upgradeChoices = null;
        waveInProgress = false;
        waveBreakTimer = 2f;
        boss = null;
        damageBonus = attackSpeedBonus = moveBonus = hpBonus = 1f;

        cam.backgroundColor = Stages.SkyColor(stage);
        World = new GameObject("World").transform;
        BuildBattlefield();

        foreach (var def in SaveData.DeployList(MaxDeploy)) SpawnCompanion(def);
    }

    public void End()
    {
        if (World != null) Destroy(World.gameObject);
        World = null;
        heroes.Clear();
        enemies.Clear();
        spawnQueue.Clear();
        fastForward = false;
        Time.timeScale = 1f;
    }

    void Finish(bool won)
    {
        if (finished) return;
        finished = true;
        victory = won;
        finishTimer = 2f;
        Time.timeScale = 1f;
    }

    // 땅, 바리케이드, 용사를 그립니다. (나중에 진짜 그림으로 바꿀 부분)
    void BuildBattlefield()
    {
        Color ground = Stages.GroundColor(Stage);
        MakeBlock("Ground", new Vector2(0f, (LaneTop + LaneBottom) / 2f - 4f),
            new Vector2(80f, LaneTop - LaneBottom + 9f), ground, -2000);
        MakeBlock("Horizon", new Vector2(0f, LaneTop + 0.9f), new Vector2(80f, 0.3f), UI.Darken(ground, 0.8f), -1999);

        // 용사 앞의 나무 바리케이드
        for (int i = 0; i < 6; i++)
        {
            float y = LaneBottom + i * (LaneTop - LaneBottom) / 5f;
            MakeBlock("Barricade", new Vector2(LeaderFrontX - 0.3f, y), new Vector2(0.35f, 1.1f),
                new Color(0.45f, 0.30f, 0.18f), Mathf.RoundToInt(-y * 20f) * 4 - 2);
        }

        // 용사 (금색 동그라미 + 망토)
        float leaderY = (LaneTop + LaneBottom) / 2f;
        MakeBlock("Cape", new Vector2(LeaderX - 0.15f, leaderY - 0.2f), new Vector2(1.0f, 1.2f), new Color(0.75f, 0.15f, 0.2f), 990);
        var go = new GameObject("Leader");
        go.transform.SetParent(World, false);
        go.transform.position = new Vector2(LeaderX, leaderY);
        go.transform.localScale = Vector3.one * 1.3f;
        leaderRenderer = go.AddComponent<SpriteRenderer>();
        leaderRenderer.sprite = SpriteFactory.Circle();
        leaderRenderer.color = leaderColor;
        leaderRenderer.sortingOrder = 991;
    }

    SpriteRenderer MakeBlock(string blockName, Vector2 center, Vector2 scale, Color color, int order)
    {
        var go = new GameObject(blockName);
        go.transform.SetParent(World, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square();
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    // ================= 매 프레임 =================

    void Update()
    {
        if (World == null) return;

        if (leaderFlashTimer > 0f)
        {
            leaderFlashTimer -= Time.deltaTime;
            leaderRenderer.color = leaderFlashTimer > 0f ? new Color(1f, 0.4f, 0.4f) : leaderColor;
        }

        if (finished)
        {
            finishTimer -= Time.unscaledDeltaTime;
            if (finishTimer <= 0f) onFinished?.Invoke(victory);
            return;
        }
        if (IsPaused) return;
        UpdateWaves();
    }

    // ================= 웨이브 =================

    void UpdateWaves()
    {
        if (waveInProgress)
        {
            if (spawnQueue.Count > 0)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    SpawnEnemy(spawnQueue[0]);
                    spawnQueue.RemoveAt(0);
                    spawnTimer = SpawnInterval;
                }
            }
            else if (enemies.Count == 0)
            {
                waveInProgress = false;
                if (Wave >= Stages.WavesPerStage) Finish(true);
                else
                {
                    waveBreakTimer = WaveBreak;
                    OfferUpgrades();
                }
            }
        }
        else
        {
            waveBreakTimer -= Time.deltaTime;
            if (waveBreakTimer <= 0f) StartNextWave();
        }
    }

    void StartNextWave()
    {
        Wave++;
        waveInProgress = true;
        spawnTimer = 0f;
        spawnQueue.Clear();

        int count = 4 + Stage + Wave * 2;
        for (int i = 0; i < count; i++)
        {
            float r = Random.value;
            if (Stage >= 3 && r < 0.25f) spawnQueue.Add(EnemyKind.Archer);
            else if (Stage >= 4 && r < 0.37f) spawnQueue.Add(EnemyKind.Brute);
            else spawnQueue.Add(EnemyKind.Grunt);
        }
        if (Stages.HasBoss(Stage) && Wave == Stages.WavesPerStage) spawnQueue.Add(EnemyKind.Boss);
    }

    void SpawnEnemy(EnemyKind kind)
    {
        var pos = new Vector2(HalfScreenWidth + 1f, Random.Range(LaneBottom, LaneTop));
        if (kind == EnemyKind.Boss) pos.y = (LaneTop + LaneBottom) / 2f;

        float hpMul = 1f + 0.35f * (Stage - 1) + 0.1f * (Wave - 1);
        float dmgMul = 1f + 0.15f * (Stage - 1);

        var u = CreateUnit(kind.ToString(), Team.Enemy, pos);
        switch (kind)
        {
            case EnemyKind.Brute:
                u.maxHp = 150; u.damage = 12; u.attackRange = 0.5f; u.attackCooldown = 1.2f;
                u.moveSpeed = 1.0f; u.size = 1.0f; u.goldReward = 10;
                u.Setup(SpriteFactory.Square(), new Color(0.6f, 0.1f, 0.1f), Color.clear);
                break;
            case EnemyKind.Archer:
                u.maxHp = 20; u.damage = 6; u.attackRange = 4f; u.attackCooldown = 1.5f;
                u.moveSpeed = 1.3f; u.size = 0.45f; u.goldReward = 4; u.ranged = true;
                u.Setup(SpriteFactory.Square(), new Color(1f, 0.6f, 0.2f), Color.clear);
                break;
            case EnemyKind.Boss:
                bool demonKing = Stage >= Stages.Count;
                u.maxHp = demonKing ? 900 : 500; u.damage = demonKing ? 30 : 20; u.attackRange = 0.8f;
                u.attackCooldown = 1.3f; u.moveSpeed = 0.7f; u.size = demonKing ? 2.0f : 1.6f; u.goldReward = 150;
                u.isBoss = true;
                u.Setup(SpriteFactory.Square(), demonKing ? new Color(0.15f, 0.02f, 0.05f) : new Color(0.35f, 0.1f, 0.45f),
                    new Color(1f, 0.2f, 0.2f));
                boss = u;
                break;
            default:
                u.maxHp = 30; u.damage = 5; u.attackRange = 0.4f; u.attackCooldown = 1f;
                u.moveSpeed = 1.6f; u.size = 0.5f; u.goldReward = 3;
                u.Setup(SpriteFactory.Square(), new Color(0.9f, 0.25f, 0.25f), Color.clear);
                break;
        }
        u.maxHp *= hpMul;
        u.hp = u.maxHp;
        u.damage *= dmgMul;
        u.UpdateHpBar();
        enemies.Add(u);
    }

    // ================= 동료 =================

    Unit CreateUnit(string unitName, Team team, Vector2 pos)
    {
        var go = new GameObject(unitName);
        go.transform.SetParent(World, false);
        go.transform.position = pos;
        var u = go.AddComponent<Unit>();
        u.team = team;
        return u;
    }

    void SpawnCompanion(CompanionDef def)
    {
        var c = def.job;
        float m = RarityInfo.StatMultiplier(def.rarity);
        // 용사 옆에서 나와서 자기 자리로 걸어갑니다.
        var u = CreateUnit(def.name, Team.Hero, new Vector2(LeaderFrontX + 0.5f, Random.Range(LaneBottom, LaneTop)));
        u.heroClass = c;
        u.maxHp = c.hp * m * hpBonus;
        u.damage = c.damage * m;
        u.attackRange = c.range;
        u.attackCooldown = c.cooldown;
        u.moveSpeed = c.speed;
        u.splashRadius = c.splash;
        u.size = c.size;
        u.ranged = c.ranged;
        u.Setup(SpriteFactory.Circle(), c.color, RarityInfo.GetColor(def.rarity));
        heroes.Add(u);
        RefreshFormation();
    }

    // 동료 진형: 전사가 맨 앞, 그 뒤에 궁수, 마법사, 사제 순으로 줄을 섭니다.
    void RefreshFormation()
    {
        int index = 0;
        float rowGap = (LaneTop - LaneBottom) / (RowsPerColumn - 1);
        foreach (var c in HeroClass.All)
        {
            foreach (var h in heroes)
            {
                if (h.heroClass != c) continue;
                int column = index / RowsPerColumn;
                int row = index % RowsPerColumn;
                // 줄마다 살짝 엇갈리게 세워서 겹쳐 보이지 않게 합니다.
                float y = LaneTop - row * rowGap - (column % 2) * rowGap * 0.5f;
                h.formationSlot = new Vector2(FrontLineX - column * ColumnGap, Mathf.Max(y, LaneBottom));
                index++;
            }
            // 직업이 바뀌면 새 줄에서 시작
            if (index % RowsPerColumn != 0) index += RowsPerColumn - index % RowsPerColumn;
        }
    }

    public int CountOf(HeroClass c)
    {
        int n = 0;
        foreach (var h in heroes) if (h.heroClass == c) n++;
        return n;
    }

    // ================= 시너지 & 보너스 =================

    // 같은 직업 3명 → 1단계, 6명 → 2단계
    public int SynergyTier(HeroClass c)
    {
        int n = CountOf(c);
        return n >= 6 ? 2 : n >= 3 ? 1 : 0;
    }

    public float DamageMultiplier(Unit u) => u.team == Team.Hero ? damageBonus : 1f;

    public float MoveMultiplier(Unit u) => u.team == Team.Hero ? moveBonus : 1f;

    public float CooldownMultiplier(Unit u)
    {
        if (u.team != Team.Hero) return 1f;
        float speed = attackSpeedBonus;
        if (u.heroClass == HeroClass.Archer) speed *= 1f + 0.3f * SynergyTier(HeroClass.Archer);
        return 1f / speed;
    }

    public float SplashMultiplier(Unit u)
    {
        if (u.heroClass != HeroClass.Mage) return 1f;
        return 1f + 0.4f * SynergyTier(HeroClass.Mage);
    }

    public float HealMultiplier(Unit u)
    {
        return 1f + 0.3f * SynergyTier(HeroClass.Priest);
    }

    public float DamageTakenMultiplier(Unit u)
    {
        if (u.heroClass != HeroClass.Warrior) return 1f;
        return 1f - 0.2f * SynergyTier(HeroClass.Warrior);
    }

    // ================= 전투 도우미 =================

    public Unit FindNearestOpponent(Unit u)
    {
        var list = u.team == Team.Hero ? enemies : heroes;
        Unit best = null;
        float bestDist = float.MaxValue;
        Vector2 pos = u.transform.position;
        foreach (var other in list)
        {
            if (other == null || !other.IsAlive) continue;
            float d = ((Vector2)other.transform.position - pos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = other; }
        }
        return best;
    }

    public Unit FindMostHurtAlly(Unit healer, float range)
    {
        Unit best = null;
        float lowest = 0.999f;
        Vector2 pos = healer.transform.position;
        foreach (var h in heroes)
        {
            if (h == null || !h.IsAlive) continue;
            float pct = h.hp / h.maxHp;
            if (pct >= lowest) continue;
            if (Vector2.Distance(pos, h.transform.position) > range) continue;
            lowest = pct;
            best = h;
        }
        return best;
    }

    // 같은 편끼리 한 점에 겹치지 않도록 살짝 밀어냅니다.
    public Vector2 SeparationFor(Unit u)
    {
        var list = u.team == Team.Hero ? heroes : enemies;
        Vector2 pos = u.transform.position;
        Vector2 push = Vector2.zero;
        foreach (var other in list)
        {
            if (other == u || other == null) continue;
            float minDist = (u.size + other.size) * 0.5f;
            Vector2 diff = pos - (Vector2)other.transform.position;
            float d = diff.magnitude;
            if (d >= minDist) continue;
            if (d < 0.0001f) diff = Random.insideUnitCircle;
            push += diff.normalized * (minDist - d) / minDist * 3f;
        }
        return push;
    }

    public void ApplyHit(Vector2 point, Unit target, Team attackerTeam, float damage, float splash)
    {
        if (splash <= 0f)
        {
            if (target != null && target.IsAlive) target.TakeDamage(damage);
            return;
        }

        var list = attackerTeam == Team.Hero ? enemies : heroes;
        foreach (var u in new List<Unit>(list))
        {
            if (u != null && Vector2.Distance(point, u.transform.position) <= splash)
                u.TakeDamage(damage);
        }
        SpawnEffect(point, splash * 2f, new Color(1f, 0.8f, 1f, 0.4f));
    }

    // 잠깐 보였다 사라지는 동그란 효과 (폭발, 치유 등)
    public void SpawnEffect(Vector2 point, float diameter, Color color)
    {
        if (World == null) return;
        var fx = new GameObject("Effect");
        fx.transform.SetParent(World, false);
        fx.transform.position = point;
        fx.transform.localScale = Vector3.one * diameter;
        var sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle();
        sr.color = color;
        sr.sortingOrder = 999;
        fx.AddComponent<FadeOut>();
    }

    public void OnUnitDied(Unit u)
    {
        if (u.team == Team.Enemy)
        {
            if (enemies.Remove(u)) GoldEarned += u.goldReward;
        }
        else if (heroes.Remove(u))
        {
            RefreshFormation();
        }
    }

    public void DamageLeader(float amount)
    {
        if (finished) return;
        LeaderHp -= amount;
        leaderFlashTimer = 0.1f;
        if (LeaderHp <= 0f)
        {
            LeaderHp = 0f;
            Finish(false);
        }
    }

    public void Retreat() => Finish(false);

    // ================= 강화 카드 (로그라이크 요소: 이번 스테이지에서만 유지) =================

    void OfferUpgrades()
    {
        var pool = new List<Upgrade>
        {
            new Upgrade { title = "날카로운 무기", desc = "모든 동료 공격력 +15%", apply = () => damageBonus *= 1.15f },
            new Upgrade { title = "빠른 손놀림", desc = "모든 동료 공격 속도 +12%", apply = () => attackSpeedBonus *= 1.12f },
            new Upgrade { title = "행군 훈련", desc = "모든 동료 이동 속도 +15%", apply = () => moveBonus *= 1.15f },
            new Upgrade { title = "강철 갑옷", desc = "모든 동료 최대 체력 +20%", apply = () =>
                {
                    hpBonus *= 1.2f;
                    foreach (var h in heroes) { h.maxHp *= 1.2f; h.hp *= 1.2f; h.UpdateHpBar(); }
                } },
            new Upgrade { title = "치유의 샘", desc = "모든 동료 체력 완전 회복", apply = () =>
                {
                    foreach (var h in heroes) { h.hp = h.maxHp; h.UpdateHpBar(); }
                } },
            new Upgrade { title = "용사의 물약", desc = "용사 체력 +100 회복", apply = () => LeaderHp = Mathf.Min(LeaderMaxHp, LeaderHp + 100f) },
            new Upgrade { title = "전리품", desc = "골드 +" + (30 + Stage * 10), apply = () => GoldEarned += 30 + Stage * 10 },
        };

        upgradeChoices = new List<Upgrade>();
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            upgradeChoices.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
    }

    // ================= 전투 화면 UI =================

    public void DrawHUD()
    {
        if (World == null) return;
        float w = UI.Width;

        // 위쪽 정보 막대
        UI.Fill(new Rect(0, 0, w, 52), new Color(0f, 0f, 0f, 0.45f));
        UI.Text(new Rect(20, 0, 600, 52), $"스테이지 {Stages.Label(Stage)}  ·  {Stages.ChapterName(Stage)}", 22, Color.white, TextAnchor.MiddleLeft, true);
        UI.Text(new Rect(w / 2 - 150, 0, 300, 52), $"웨이브 {Mathf.Max(Wave, 1)} / {Stages.WavesPerStage}", 24, Color.white, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(w - 520, 0, 260, 52), $"골드 +{GoldEarned}", 22, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleRight, true);

        if (UI.Button(new Rect(w - 240, 8, 100, 36), fastForward ? "x2 속도" : "x1 속도", new Color(0.3f, 0.45f, 0.65f), 18, !finished))
        {
            fastForward = !fastForward;
            Time.timeScale = fastForward ? 2f : 1f;
        }
        if (UI.Button(new Rect(w - 125, 8, 105, 36), "후퇴", new Color(0.6f, 0.25f, 0.25f), 18, !finished))
            Retreat();

        // 용사 체력
        UI.Text(new Rect(20, 60, 80, 28), "용사", 20, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleLeft, true);
        UI.Bar(new Rect(80, 64, 260, 22), LeaderHp / LeaderMaxHp, new Color(0.9f, 0.3f, 0.3f));
        UI.Text(new Rect(80, 64, 260, 22), $"{Mathf.CeilToInt(LeaderHp)} / {LeaderMaxHp:0}", 15, Color.white);

        // 시너지
        float y = 100;
        UI.Text(new Rect(20, y, 400, 24), "시너지 (3명 / 6명)", 16, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleLeft);
        foreach (var c in HeroClass.All)
        {
            y += 24;
            int tier = SynergyTier(c);
            string stars = tier == 2 ? " ★★" : tier == 1 ? " ★" : "";
            UI.Text(new Rect(20, y, 420, 24), $"{c.name} {CountOf(c)}명{stars} - {c.synergyText}", 16,
                tier > 0 ? c.color : new Color(0.75f, 0.75f, 0.75f), TextAnchor.MiddleLeft);
        }

        // 보스 체력
        if (boss != null && boss.IsAlive)
        {
            UI.Text(new Rect(w / 2 - 250, 58, 500, 28), Stages.BossName(Stage), 22, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter, true);
            UI.Bar(new Rect(w / 2 - 250, 88, 500, 20), boss.hp / boss.maxHp, new Color(0.8f, 0.15f, 0.2f));
        }

        // 다음 웨이브 안내
        if (!waveInProgress && !IsPaused)
            UI.Text(new Rect(0, 300, w, 50), $"웨이브 {Wave + 1} 시작까지 {waveBreakTimer:0.0}초", 26, Color.white, TextAnchor.MiddleCenter, true);

        if (upgradeChoices != null) DrawUpgradeChoices(w);

        if (finished)
        {
            UI.Fill(UI.Full, new Color(0f, 0f, 0f, 0.4f));
            UI.Text(new Rect(0, 280, w, 80), victory ? "스테이지 클리어!" : "패배...", 56,
                victory ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter, true);
        }
    }

    void DrawUpgradeChoices(float w)
    {
        UI.Fill(UI.Full, new Color(0f, 0f, 0f, 0.6f));
        UI.Text(new Rect(0, 150, w, 50), $"웨이브 {Wave} 클리어! 강화를 하나 고르세요", 32, Color.white, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(0, 195, w, 30), "(이번 스테이지 동안만 유지됩니다)", 18, new Color(1f, 1f, 1f, 0.7f));

        const float cw = 260, ch = 180, gap = 28;
        float x0 = (w - (cw * upgradeChoices.Count + gap * (upgradeChoices.Count - 1))) / 2f;
        for (int i = 0; i < upgradeChoices.Count; i++)
        {
            var u = upgradeChoices[i];
            var r = new Rect(x0 + i * (cw + gap), 250, cw, ch);
            if (UI.Button(r, "", new Color(0.25f, 0.3f, 0.5f)))
            {
                u.apply();
                upgradeChoices = null;
                return;
            }
            UI.Text(new Rect(r.x + 10, r.y + 20, r.width - 20, 40), u.title, 24, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter, true);
            UI.Text(new Rect(r.x + 15, r.y + 70, r.width - 30, 90), u.desc, 19, Color.white);
        }
    }
}
