using System.Collections.Generic;
using UnityEngine;

// 게임 전체를 관리합니다: 웨이브, 골드, 영웅 고용, 시너지, 강화 선택, 화면 UI.
// 씬에 따로 넣지 않아도 ▶(플레이)를 누르면 자동으로 생성됩니다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ---- 밸런스 숫자 (자유롭게 바꿔 보세요) ----
    public const int MaxHeroes = 50;
    const int StartGold = 30;
    const float CastleMaxHp = 200f;
    const float SpawnInterval = 0.5f;
    const float WaveBreak = 2f;

    // ---- 전장 배치 (화면 고정, 옆에서 보는 시점) ----
    public const float LaneTop = 2.5f;       // 유닛이 다닐 수 있는 가장 위쪽
    public const float LaneBottom = -5.5f;   // 가장 아래쪽
    public const float CastleX = -12.5f;     // 성의 가운데 위치
    public const float CastleFrontX = -11.3f; // 적이 여기까지 오면 성을 공격
    public const float HeroLeash = 3.5f;     // 근접 영웅이 자기 자리에서 이만큼 안의 적에게 돌격
    const float FrontLineX = -3f;            // 영웅 진형의 맨 앞줄
    const float ColumnGap = 1.0f;            // 줄 사이 간격 (가로)
    const int RowsPerColumn = 7;             // 한 줄에 서는 영웅 수 (세로)
    const float HalfWorldWidth = 14f;        // 화면에 항상 보이는 가로 반폭

    public readonly List<Unit> heroes = new List<Unit>();
    public readonly List<Unit> enemies = new List<Unit>();

    public Transform World { get; private set; }
    public int Gold { get; private set; }
    public int Wave { get; private set; }
    public float CastleHp { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsPaused => IsGameOver || upgradeChoices != null;

    // 강화 카드로 올라가는 전체 보너스
    float damageBonus = 1f;
    float attackSpeedBonus = 1f;
    float moveBonus = 1f;
    float hpBonus = 1f;

    Camera cam;
    SpriteRenderer castleRenderer;
    float castleFlashTimer;
    bool waveInProgress;
    int enemiesLeftToSpawn;
    int brutesLeftToSpawn;
    float spawnTimer;
    float waveBreakTimer;
    bool fastForward;
    int bestWave;

    class Upgrade
    {
        public string title;
        public string desc;
        public System.Action apply;
    }

    List<Upgrade> upgradeChoices; // null이 아니면 강화 선택 화면이 떠 있는 상태

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        if (FindAnyObjectByType<GameManager>() != null) return;
        new GameObject("GameManager").AddComponent<GameManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        bestWave = PlayerPrefs.GetInt("BestWave", 0);
        SetupCamera();
        StartNewRun();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
    }

    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.45f, 0.68f, 0.88f); // 하늘색
        FitCamera();
    }

    // 화면 비율이 달라도 성부터 오른쪽 끝까지 전장 전체가 보이도록 맞춥니다.
    void FitCamera()
    {
        cam.orthographicSize = Mathf.Max(8f, HalfWorldWidth / cam.aspect);
    }

    float HalfScreenWidth => cam.orthographicSize * cam.aspect;

    // ================= 판(런) 시작 =================

    void StartNewRun()
    {
        if (World != null) Destroy(World.gameObject);
        heroes.Clear();
        enemies.Clear();

        World = new GameObject("World").transform;
        BuildBattlefield();

        Gold = StartGold;
        Wave = 0;
        CastleHp = CastleMaxHp;
        IsGameOver = false;
        upgradeChoices = null;
        waveInProgress = false;
        waveBreakTimer = 3f;
        damageBonus = attackSpeedBonus = moveBonus = hpBonus = 1f;

        SpawnHero(HeroClass.Warrior);
        SpawnHero(HeroClass.Warrior);
        SpawnHero(HeroClass.Archer);
    }

    // 땅과 성을 그립니다. (나중에 진짜 그림으로 바꿀 부분)
    void BuildBattlefield()
    {
        MakeBlock("Ground", new Vector2(0f, (LaneTop + LaneBottom) / 2f - 4f),
            new Vector2(80f, LaneTop - LaneBottom + 9f), new Color(0.40f, 0.62f, 0.32f), -2000);
        MakeBlock("Horizon", new Vector2(0f, LaneTop + 0.9f), new Vector2(80f, 0.3f), new Color(0.32f, 0.50f, 0.26f), -1999);

        float castleBottom = LaneBottom - 0.8f;
        float castleTop = LaneTop + 2.5f;
        castleRenderer = MakeBlock("Castle", new Vector2(CastleX, (castleTop + castleBottom) / 2f),
            new Vector2(2.4f, castleTop - castleBottom), new Color(0.55f, 0.55f, 0.6f), -1500);
        for (int i = 0; i < 3; i++)
            MakeBlock("Battlement", new Vector2(CastleX - 0.8f + i * 0.8f, castleTop + 0.3f),
                new Vector2(0.5f, 0.6f), new Color(0.5f, 0.5f, 0.55f), -1500);
        MakeBlock("Gate", new Vector2(CastleX + 0.6f, castleBottom + 1.2f), new Vector2(1.2f, 2.4f),
            new Color(0.35f, 0.25f, 0.18f), -1499);
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
        FitCamera();

        if (castleFlashTimer > 0f)
        {
            castleFlashTimer -= Time.deltaTime;
            castleRenderer.color = castleFlashTimer > 0f ? new Color(0.9f, 0.5f, 0.5f) : new Color(0.55f, 0.55f, 0.6f);
        }

        if (IsPaused) return;
        UpdateWaves();
    }

    // ================= 웨이브 =================

    void UpdateWaves()
    {
        if (waveInProgress)
        {
            if (enemiesLeftToSpawn > 0)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    bool brute = brutesLeftToSpawn > 0 && enemiesLeftToSpawn <= brutesLeftToSpawn;
                    if (brute) brutesLeftToSpawn--;
                    SpawnEnemy(brute);
                    enemiesLeftToSpawn--;
                    spawnTimer = SpawnInterval;
                }
            }
            else if (enemies.Count == 0)
            {
                // 웨이브 클리어!
                waveInProgress = false;
                Gold += 5 + Wave;
                waveBreakTimer = WaveBreak;
                OfferUpgrades();
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
        brutesLeftToSpawn = Wave % 5 == 0 ? Wave / 5 : 0; // 5웨이브마다 거대 적 등장
        enemiesLeftToSpawn = 4 + Wave * 2 + brutesLeftToSpawn;
        spawnTimer = 0f;
    }

    void SpawnEnemy(bool brute)
    {
        // 화면 오른쪽 바깥에서 등장
        var pos = new Vector2(HalfScreenWidth + 1f, Random.Range(LaneBottom, LaneTop));

        float hpMul = 1f + 0.2f * (Wave - 1);
        float dmgMul = 1f + 0.08f * (Wave - 1);
        bool shaman = !brute && Wave >= 4 && Random.value < 0.25f;

        var u = CreateUnit(brute ? "Brute" : shaman ? "Shaman" : "Grunt", Team.Enemy, pos);
        if (brute)
        {
            u.maxHp = 200; u.damage = 15; u.attackRange = 0.5f; u.attackCooldown = 1.2f;
            u.moveSpeed = 1.0f; u.size = 1.1f; u.goldReward = 15;
            u.Setup(SpriteFactory.Square(), new Color(0.6f, 0.1f, 0.1f));
        }
        else if (shaman)
        {
            u.maxHp = 20; u.damage = 6; u.attackRange = 4f; u.attackCooldown = 1.5f;
            u.moveSpeed = 1.3f; u.size = 0.45f; u.goldReward = 3; u.ranged = true;
            u.Setup(SpriteFactory.Square(), new Color(1f, 0.6f, 0.2f));
        }
        else
        {
            u.maxHp = 30; u.damage = 5; u.attackRange = 0.4f; u.attackCooldown = 1f;
            u.moveSpeed = 1.6f; u.size = 0.5f; u.goldReward = 2;
            u.Setup(SpriteFactory.Square(), new Color(0.9f, 0.25f, 0.25f));
        }
        u.maxHp *= hpMul;
        u.hp = u.maxHp;
        u.damage *= dmgMul;
        enemies.Add(u);
    }

    // ================= 영웅 =================

    Unit CreateUnit(string unitName, Team team, Vector2 pos)
    {
        var go = new GameObject(unitName);
        go.transform.SetParent(World, false);
        go.transform.position = pos;
        var u = go.AddComponent<Unit>();
        u.team = team;
        return u;
    }

    void SpawnHero(HeroClass c)
    {
        if (heroes.Count >= MaxHeroes) return;
        // 성문에서 나와서 자기 자리로 걸어갑니다.
        var u = CreateUnit(c.name, Team.Hero, new Vector2(CastleFrontX, Random.Range(LaneBottom, LaneBottom + 2f)));
        u.heroClass = c;
        u.maxHp = c.hp * hpBonus;
        u.damage = c.damage;
        u.attackRange = c.range;
        u.attackCooldown = c.cooldown;
        u.moveSpeed = c.speed;
        u.splashRadius = c.splash;
        u.size = c.size;
        u.ranged = c.ranged;
        u.Setup(SpriteFactory.Circle(), c.color);
        heroes.Add(u);
        RefreshFormation();
    }

    // 영웅 진형: 전사가 맨 앞, 그 뒤에 궁수, 맨 뒤에 마법사가 줄을 섭니다.
    void RefreshFormation()
    {
        int index = 0;
        foreach (var c in HeroClass.All)
        {
            foreach (var h in heroes)
            {
                if (h.heroClass != c) continue;
                int column = index / RowsPerColumn;
                int row = index % RowsPerColumn;
                float rowGap = (LaneTop - LaneBottom) / (RowsPerColumn - 1);
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

    public int CostOf(HeroClass c) => c.baseCost + CountOf(c) * 2;

    void Recruit(HeroClass c)
    {
        int cost = CostOf(c);
        if (Gold < cost || heroes.Count >= MaxHeroes) return;
        Gold -= cost;
        SpawnHero(c);
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

        var fx = new GameObject("Explosion");
        fx.transform.SetParent(World, false);
        fx.transform.position = point;
        fx.transform.localScale = Vector3.one * splash * 2f;
        var sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle();
        sr.color = new Color(1f, 0.8f, 1f, 0.4f);
        sr.sortingOrder = 4;
        fx.AddComponent<FadeOut>();
    }

    public void OnUnitDied(Unit u)
    {
        if (u.team == Team.Enemy)
        {
            if (enemies.Remove(u)) Gold += u.goldReward;
        }
        else if (heroes.Remove(u))
        {
            RefreshFormation();
        }
    }

    public void DamageCastle(float amount)
    {
        if (IsGameOver) return;
        CastleHp -= amount;
        castleFlashTimer = 0.1f;
        if (CastleHp <= 0f)
        {
            CastleHp = 0f;
            GameOver();
        }
    }

    void GameOver()
    {
        IsGameOver = true;
        if (Wave > bestWave)
        {
            bestWave = Wave;
            PlayerPrefs.SetInt("BestWave", bestWave);
            PlayerPrefs.Save();
        }
    }

    // ================= 강화 카드 (로그라이크 요소) =================

    void OfferUpgrades()
    {
        var pool = new List<Upgrade>
        {
            new Upgrade { title = "날카로운 무기", desc = "모든 영웅 공격력 +15%", apply = () => damageBonus *= 1.15f },
            new Upgrade { title = "빠른 손놀림", desc = "모든 영웅 공격 속도 +12%", apply = () => attackSpeedBonus *= 1.12f },
            new Upgrade { title = "행군 훈련", desc = "모든 영웅 이동 속도 +15%", apply = () => moveBonus *= 1.15f },
            new Upgrade { title = "강철 갑옷", desc = "모든 영웅 최대 체력 +20%", apply = () =>
                {
                    hpBonus *= 1.2f;
                    foreach (var h in heroes) { h.maxHp *= 1.2f; h.hp *= 1.2f; h.UpdateHpBar(); }
                } },
            new Upgrade { title = "치유의 샘", desc = "모든 영웅 체력 완전 회복", apply = () =>
                {
                    foreach (var h in heroes) { h.hp = h.maxHp; h.UpdateHpBar(); }
                } },
            new Upgrade { title = "성벽 보수", desc = "성 체력 +80 회복", apply = () => CastleHp = Mathf.Min(CastleMaxHp, CastleHp + 80f) },
            new Upgrade { title = "전리품", desc = "골드 +" + (20 + Wave * 3), apply = () => Gold += 20 + Wave * 3 },
        };
        foreach (var c in HeroClass.All)
        {
            var hc = c;
            pool.Add(new Upgrade { title = "지원군: " + hc.name, desc = hc.name + " 2명 무료 합류", apply = () => { SpawnHero(hc); SpawnHero(hc); } });
        }

        upgradeChoices = new List<Upgrade>();
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            upgradeChoices.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
    }

    void ChooseUpgrade(Upgrade u)
    {
        u.apply();
        upgradeChoices = null;
    }

    // ================= 화면 UI =================

    void OnGUI()
    {
        // 화면 크기와 상관없이 1280x720 기준으로 그립니다.
        float scale = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float w = Screen.width / scale;

        var big = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
        big.normal.textColor = Color.white;
        var small = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        small.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
        var button = new GUIStyle(GUI.skin.button) { fontSize = 18 };

        GUI.Label(new Rect(20, 12, 900, 34), $"웨이브 {Wave}    골드 {Gold}    영웅 {heroes.Count}/{MaxHeroes}    성 체력 {Mathf.CeilToInt(CastleHp)}/{CastleMaxHp:0}", big);
        if (!waveInProgress && !IsPaused && waveBreakTimer > 0f)
            GUI.Label(new Rect(20, 44, 400, 26), $"다음 웨이브까지 {waveBreakTimer:0.0}초", small);

        // 시너지 표시
        float y = 80;
        GUI.Label(new Rect(20, y, 300, 26), "[ 시너지: 3명 / 6명 ]", small);
        foreach (var c in HeroClass.All)
        {
            y += 26;
            int tier = SynergyTier(c);
            var style = new GUIStyle(small);
            style.normal.textColor = tier > 0 ? c.color : new Color(0.6f, 0.6f, 0.6f);
            string stars = tier == 2 ? "★★" : tier == 1 ? "★" : "";
            GUI.Label(new Rect(20, y, 400, 26), $"{c.name} {CountOf(c)}명 {stars} - {c.synergyText}", style);
        }

        // 오른쪽 위: 속도 버튼과 조작법
        if (GUI.Button(new Rect(w - 120, 12, 100, 40), fastForward ? "속도 x2" : "속도 x1", button))
        {
            fastForward = !fastForward;
            Time.timeScale = fastForward ? 2f : 1f;
        }
        var helpStyle = new GUIStyle(small) { alignment = TextAnchor.UpperRight };
        GUI.Label(new Rect(w - 420, 58, 400, 26), "적이 성에 닿지 못하게 막아내세요!", helpStyle);
        GUI.Label(new Rect(w - 420, 82, 400, 26), $"최고 기록: 웨이브 {bestWave}", helpStyle);

        // 아래: 영웅 고용 버튼
        const float bw = 200, bh = 70, gap = 12;
        float x0 = (w - (bw * HeroClass.All.Length + gap * (HeroClass.All.Length - 1))) / 2f;
        for (int i = 0; i < HeroClass.All.Length; i++)
        {
            var c = HeroClass.All[i];
            int cost = CostOf(c);
            GUI.enabled = !IsPaused && Gold >= cost && heroes.Count < MaxHeroes;
            if (GUI.Button(new Rect(x0 + i * (bw + gap), 720 - bh - 16, bw, bh), $"{c.name} 고용 ({cost}골드)\n{c.role}", button))
                Recruit(c);
        }
        GUI.enabled = true;

        if (upgradeChoices != null) DrawUpgradeChoices(w, big, button);
        if (IsGameOver) DrawGameOver(w, big, button);
    }

    void DrawUpgradeChoices(float w, GUIStyle big, GUIStyle button)
    {
        GUI.Box(new Rect(0, 0, w, 720), "");
        var title = new GUIStyle(big) { alignment = TextAnchor.MiddleCenter, fontSize = 32 };
        GUI.Label(new Rect(0, 170, w, 50), $"웨이브 {Wave} 클리어! 강화를 하나 고르세요", title);

        const float cw = 260, ch = 160, gap = 24;
        float x0 = (w - (cw * upgradeChoices.Count + gap * (upgradeChoices.Count - 1))) / 2f;
        var cardStyle = new GUIStyle(button) { fontSize = 20, wordWrap = true };
        for (int i = 0; i < upgradeChoices.Count; i++)
        {
            var u = upgradeChoices[i];
            if (GUI.Button(new Rect(x0 + i * (cw + gap), 260, cw, ch), $"{u.title}\n\n{u.desc}", cardStyle))
            {
                ChooseUpgrade(u);
                break;
            }
        }
    }

    void DrawGameOver(float w, GUIStyle big, GUIStyle button)
    {
        // 반투명 상자를 두 번 겹쳐서 배경을 더 어둡게 만듭니다.
        GUI.Box(new Rect(0, 0, w, 720), "");
        GUI.Box(new Rect(0, 0, w, 720), "");
        var title = new GUIStyle(big) { alignment = TextAnchor.MiddleCenter, fontSize = 40 };
        GUI.Label(new Rect(0, 220, w, 60), "성이 함락되었습니다!", title);
        var sub = new GUIStyle(big) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0, 290, w, 40), $"웨이브 {Wave}까지 버텼습니다   (최고 기록: {bestWave})", sub);
        if (GUI.Button(new Rect(w / 2 - 110, 360, 220, 60), "다시 시작", button))
            StartNewRun();
    }
}
