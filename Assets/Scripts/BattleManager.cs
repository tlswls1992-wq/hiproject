using System.Collections.Generic;
using UnityEngine;

// 전투 한 판을 진행합니다: 전장, 편성 칸에 동료 배치, 웨이브, 강화 카드, 전투 화면 UI.
// 파티원(용사 포함)이 모두 쓰러지면 패배, 모든 웨이브를 막으면 승리입니다.
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    // ---- 밸런스 숫자 (자유롭게 바꿔 보세요) ----
    const float SpawnInterval = 0.6f;
    const float WaveBreak = 2f;
    const float AwakenBonus = 0.1f;          // 용사 각성 1단계마다 능력치 +10%

    // ---- 전장 배치 (화면 고정, 옆에서 보는 시점) ----
    public const float LaneTop = 2.5f;       // 맨 윗줄의 높이
    public const float LaneBottom = -5.5f;   // 맨 아랫줄의 높이
    const float FrontX = -1f;                // 선두 줄의 가로 위치
    const float ColumnGap = 1.6f;            // 줄 사이 간격 (선두 → 후미 방향)

    enum EnemyKind { Grunt, Archer, Brute, Boss }

    public readonly List<Unit> heroes = new List<Unit>();
    public readonly List<Unit> enemies = new List<Unit>();

    public Transform World { get; private set; }
    public int Level { get; private set; }   // 1~100번째 판
    public int Wave { get; private set; }
    public int GoldEarned { get; private set; }
    public bool Fled { get; private set; }   // 도망쳤는지
    public bool IsPaused => World == null || finished || upgradeChoices != null;

    // 강화 카드로 올라가는 보너스 (이번 판에서만 유지)
    float damageBonus = 1f;
    float attackSpeedBonus = 1f;
    float rangeBonus = 1f;
    float hpBonus = 1f;

    Camera cam;
    Unit leader;
    Unit boss;
    int partySize;
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

    static float RowGap => (LaneTop - LaneBottom) / (SaveData.Rows - 1);

    // 편성 칸의 실제 위치 (column 0 = 선두)
    public static Vector2 CellPosition(int column, int row) =>
        new Vector2(FrontX - column * ColumnGap, LaneTop - row * RowGap);

    // ================= 전투 시작 / 끝 =================

    public void Begin(int level, System.Action<bool> onFinished)
    {
        End();
        cam = Camera.main;
        this.onFinished = onFinished;
        Level = level;
        Wave = 0;
        GoldEarned = 0;
        Fled = false;
        finished = false;
        upgradeChoices = null;
        waveInProgress = false;
        waveBreakTimer = 2f;
        boss = null;
        damageBonus = attackSpeedBonus = rangeBonus = hpBonus = 1f;

        cam.backgroundColor = Stages.SkyColor(level);
        World = new GameObject("World").transform;
        BuildBattlefield();

        SaveData.EnsureHeroPlaced();
        for (int col = 0; col < SaveData.Columns; col++)
        {
            for (int row = 0; row < SaveData.Rows; row++)
            {
                var def = SaveData.DefAt(SaveData.CellIndex(col, row));
                if (def != null) SpawnCompanion(def, CellPosition(col, row));
            }
        }
        partySize = heroes.Count;
    }

    public void End()
    {
        if (World != null) Destroy(World.gameObject);
        World = null;
        heroes.Clear();
        enemies.Clear();
        spawnQueue.Clear();
        leader = null;
        fastForward = false;
        Time.timeScale = 1f;
    }

    void Finish(bool won)
    {
        if (finished) return;
        finished = true;
        victory = won;
        finishTimer = Fled ? 1f : 2f;
        Time.timeScale = 1f;
    }

    // 땅과 편성 칸을 그립니다. (나중에 진짜 그림으로 바꿀 부분)
    void BuildBattlefield()
    {
        Color ground = Stages.GroundColor(Level);
        MakeBlock("Ground", new Vector2(0f, (LaneTop + LaneBottom) / 2f - 4f),
            new Vector2(80f, LaneTop - LaneBottom + 9f), ground, -2000);
        MakeBlock("Horizon", new Vector2(0f, LaneTop + 1.2f), new Vector2(80f, 0.3f), UI.Darken(ground, 0.8f), -1999);

        // 5줄 x 5칸 편성 칸 표시
        for (int col = 0; col < SaveData.Columns; col++)
            for (int row = 0; row < SaveData.Rows; row++)
                MakeBlock("Cell", CellPosition(col, row), new Vector2(ColumnGap - 0.25f, RowGap - 0.4f),
                    new Color(0f, 0f, 0f, col % 2 == 0 ? 0.12f : 0.07f), -1900);
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
                if (Wave >= Stages.WavesPerLevel) Finish(true);
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

        int count = 4 + Mathf.Min(Level / 5, 12) + Wave * 2;
        for (int i = 0; i < count; i++)
        {
            float r = Random.value;
            if (Level >= 5 && r < 0.25f) spawnQueue.Add(EnemyKind.Archer);
            else if (Level >= 8 && r < 0.37f) spawnQueue.Add(EnemyKind.Brute);
            else spawnQueue.Add(EnemyKind.Grunt);
        }
        if (Stages.HasBoss(Level) && Wave == Stages.WavesPerLevel) spawnQueue.Add(EnemyKind.Boss);
    }

    void SpawnEnemy(EnemyKind kind)
    {
        // 편성 칸과 같은 5개의 줄 중 하나를 따라 들어옵니다.
        int lane = kind == EnemyKind.Boss ? SaveData.Rows / 2 : Random.Range(0, SaveData.Rows);
        var pos = new Vector2(HalfScreenWidth + 1f, LaneTop - lane * RowGap + Random.Range(-0.3f, 0.3f));

        float hpMul = 1f + 0.12f * (Level - 1) + 0.1f * (Wave - 1);
        float dmgMul = 1f + 0.05f * (Level - 1);

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
                bool demonKing = Level >= Stages.Count;
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

    void SpawnCompanion(CompanionDef def, Vector2 pos)
    {
        var c = def.job;
        float m = RarityInfo.StatMultiplier(def.rarity);
        if (def.IsHero) m *= 1f + AwakenBonus * SaveData.HeroAwaken;

        var u = CreateUnit(def.name, Team.Hero, pos);
        u.heroClass = c;
        u.isLeader = def.IsHero;
        u.maxHp = c.hp * m * hpBonus;
        u.damage = c.damage * m;
        u.attackRange = c.range;
        u.attackCooldown = c.cooldown;
        u.splashRadius = c.splash;
        u.size = c.size;
        u.ranged = c.ranged;
        u.Setup(SpriteFactory.Circle(), c.color, RarityInfo.GetColor(def.rarity));
        heroes.Add(u);
        if (def.IsHero) leader = u;
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

    public float RangeMultiplier(Unit u) => u.team == Team.Hero && (u.ranged || u.heroClass.healer) ? rangeBonus : 1f;

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

    // 적끼리 한 점에 겹치지 않도록 살짝 밀어냅니다.
    public Vector2 SeparationFor(Unit u)
    {
        Vector2 pos = u.transform.position;
        Vector2 push = Vector2.zero;
        foreach (var other in enemies)
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
            if (heroes.Count == 0) Finish(false); // 파티 전멸
        }
    }

    public void Flee()
    {
        Fled = true;
        Finish(false);
    }

    // ================= 강화 카드 (로그라이크 요소: 이번 판에서만 유지) =================

    void OfferUpgrades()
    {
        var pool = new List<Upgrade>
        {
            new Upgrade { title = "날카로운 무기", desc = "모든 동료 공격력 +15%", apply = () => damageBonus *= 1.15f },
            new Upgrade { title = "빠른 손놀림", desc = "모든 동료 공격 속도 +12%", apply = () => attackSpeedBonus *= 1.12f },
            new Upgrade { title = "매의 눈", desc = "원거리 동료와 사제의 사거리 +15%", apply = () => rangeBonus *= 1.15f },
            new Upgrade { title = "강철 갑옷", desc = "모든 동료 최대 체력 +20%", apply = () =>
                {
                    hpBonus *= 1.2f;
                    foreach (var h in heroes) { h.maxHp *= 1.2f; h.hp *= 1.2f; h.UpdateHpBar(); }
                } },
            new Upgrade { title = "치유의 샘", desc = "모든 동료 체력 완전 회복", apply = () =>
                {
                    foreach (var h in heroes) { h.hp = h.maxHp; h.UpdateHpBar(); }
                } },
            new Upgrade { title = "전리품", desc = "골드 +" + (30 + Level * 5), apply = () => GoldEarned += 30 + Level * 5 },
        };
        if (leader != null && leader.IsAlive)
        {
            pool.Add(new Upgrade { title = "용사의 함성", desc = "용사 공격력 +50%, 체력 회복", apply = () =>
                {
                    if (leader == null) return;
                    leader.damage *= 1.5f;
                    leader.hp = leader.maxHp;
                    leader.UpdateHpBar();
                } });
        }

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
        UI.Text(new Rect(20, 0, 600, 52), $"{Stages.Label(Level)}  ·  {Stages.StageName(Level)}", 22, Color.white, TextAnchor.MiddleLeft, true);
        UI.Text(new Rect(w / 2 - 150, 0, 300, 52), $"웨이브 {Mathf.Max(Wave, 1)} / {Stages.WavesPerLevel}", 24, Color.white, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(w - 560, 0, 220, 52), $"골드 +{GoldEarned}", 22, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleRight, true);

        if (UI.Button(new Rect(w - 320, 8, 100, 36), fastForward ? "x2 속도" : "x1 속도", new Color(0.3f, 0.45f, 0.65f), 18, !finished))
        {
            fastForward = !fastForward;
            Time.timeScale = fastForward ? 2f : 1f;
        }
        if (UI.Button(new Rect(w - 205, 8, 185, 36), "도망쳐 용사!", new Color(0.65f, 0.25f, 0.25f), 18, !finished))
            Flee();

        // 파티 상태
        UI.Text(new Rect(20, 58, 400, 28), $"파티 생존 {heroes.Count} / {partySize}", 20, Color.white, TextAnchor.MiddleLeft, true);
        if (leader != null && leader.IsAlive)
        {
            UI.Text(new Rect(20, 86, 60, 24), "용사", 18, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleLeft, true);
            UI.Bar(new Rect(70, 89, 200, 18), leader.hp / leader.maxHp, new Color(1f, 0.75f, 0.2f));
        }
        else if (leader != null || partySize > 0)
        {
            UI.Text(new Rect(20, 86, 300, 24), "용사가 쓰러졌다!", 18, new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleLeft, true);
        }

        // 줄 이름 (선두 ~ 후미)
        for (int col = 0; col < SaveData.Columns; col++)
        {
            var p = UI.WorldToUI(cam, CellPosition(col, 0) + new Vector2(0f, RowGap * 0.5f + 0.2f));
            UI.Text(new Rect(p.x - 40, p.y - 26, 80, 24), SaveData.ColumnNames[col], 16, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter, true);
        }

        // 시너지 (오른쪽)
        float y = 60;
        UI.Text(new Rect(w - 420, y, 400, 24), "시너지 (3명 / 6명)", 16, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleRight);
        foreach (var c in HeroClass.All)
        {
            y += 24;
            int tier = SynergyTier(c);
            string stars = tier == 2 ? " ★★" : tier == 1 ? " ★" : "";
            UI.Text(new Rect(w - 420, y, 400, 24), $"{c.name} {CountOf(c)}명{stars} - {c.synergyText}", 16,
                tier > 0 ? c.color : new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleRight);
        }

        // 보스 체력
        if (boss != null && boss.IsAlive)
        {
            UI.Text(new Rect(w / 2 - 250, 58, 500, 28), Stages.BossName(Level), 22, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter, true);
            UI.Bar(new Rect(w / 2 - 250, 88, 500, 20), boss.hp / boss.maxHp, new Color(0.8f, 0.15f, 0.2f));
        }

        // 다음 웨이브 안내
        if (!waveInProgress && !IsPaused)
            UI.Text(new Rect(0, 300, w, 50), $"웨이브 {Wave + 1} 시작까지 {waveBreakTimer:0.0}초", 26, Color.white, TextAnchor.MiddleCenter, true);

        if (upgradeChoices != null) DrawUpgradeChoices(w);

        if (finished)
        {
            UI.Fill(UI.Full, new Color(0f, 0f, 0f, 0.4f));
            string text = victory ? "클리어!" : Fled ? "후다닥! 도망쳤다..." : "파티 전멸...";
            UI.Text(new Rect(0, 280, w, 80), text, 56,
                victory ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter, true);
        }
    }

    void DrawUpgradeChoices(float w)
    {
        UI.Fill(UI.Full, new Color(0f, 0f, 0f, 0.6f));
        UI.Text(new Rect(0, 150, w, 50), $"웨이브 {Wave} 클리어! 강화를 하나 고르세요", 32, Color.white, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(0, 195, w, 30), "(이번 판 동안만 유지됩니다)", 18, new Color(1f, 1f, 1f, 0.7f));

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
