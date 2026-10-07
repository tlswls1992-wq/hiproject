using System.Collections.Generic;
using UnityEngine;

// 전투 한 판을 진행합니다: 전장, 편성대로 동료 배치, 웨이브, 전투 보너스, 전투 화면 UI.
// 파티원(용사 포함)이 모두 쓰러지면 패배, 모든 웨이브를 물리치면 승리입니다.
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    // ---- 밸런스 숫자 (자유롭게 바꿔 보세요) ----
    const float PartySpeedScale = 0.9f;  // 용사 파티 이동 속도 (1 = 직업 기본 속도). 0.9 = 10% 느리게
    const float EnemyStartGap = 6f;      // 웨이브가 시작될 때 파티 맨 앞과 적 진형 사이의 거리

    // ---- 전장 배치 (화면 고정, 옆에서 보는 시점) ----
    public const float LaneTop = 2.5f;       // 유닛이 다닐 수 있는 가장 위쪽
    public const float LaneBottom = -5.5f;   // 가장 아래쪽
    const float FrontX = -2f;                // 선두 줄의 가운데 가로 위치
    const float ColumnGap = 1.8f;            // 줄 사이 간격 (선두 → 후미 방향)
    const float ZoneWidth = 1.5f;            // 한 줄의 가로 폭

    public readonly List<Unit> heroes = new List<Unit>();
    public readonly List<Unit> enemies = new List<Unit>();

    public Transform World { get; private set; }
    public int Level { get; private set; }   // 1~100번째 판
    public int Wave { get; private set; }
    public int GoldEarned { get; private set; }
    public readonly List<int> DeployedUids = new List<int>(); // 이번 전투에 나간 동료 (경험치 지급용)
    public bool IsPaused => World == null || finished || upgradeChoices != null || userPaused || talk != null;
    bool userPaused; // 스페이스 바로 일시정지
    public bool HasEnemies => enemies.Count > 0;

    // 전투 보너스로 올라가는 보너스 (이번 판에서만 유지)
    float damageBonus = 1f;
    float attackSpeedBonus = 1f;
    float rangeBonus = 1f;
    float hpBonus = 1f;

    Camera cam;
    Unit leader;
    Unit boss;
    string bossName;
    int partySize;
    bool waveInProgress;
    int speed = 1; // 전투 배속 1~4
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

    static float ColumnCenterX(int column) => FrontX - column * ColumnGap;

    // 편성한 자리의 실제 위치 (column 0 = 선두, fx 0 = 줄의 앞쪽, fy 0 = 위쪽)
    public static Vector2 PlacementPosition(int column, float fx, float fy) =>
        new Vector2(ColumnCenterX(column) + (0.5f - fx) * ZoneWidth, LaneTop - fy * (LaneTop - LaneBottom));

    public Vector2 ClampToArena(Vector2 p)
    {
        float half = HalfScreenWidth;
        p.x = Mathf.Clamp(p.x, -half + 0.5f, half + 2f);
        p.y = Mathf.Clamp(p.y, LaneBottom, LaneTop);
        return p;
    }

    // ================= 전투 시작 / 끝 =================

    public void Begin(int level, System.Action<bool> onFinished)
    {
        End();
        cam = Camera.main;
        this.onFinished = onFinished;
        Level = level;
        Wave = 0;
        GoldEarned = 0;
        finished = false;
        upgradeChoices = null;
        waveInProgress = false;
        boss = null;
        talk = null;
        spawnedNamed.Clear();
        damageBonus = attackSpeedBonus = rangeBonus = hpBonus = 1f;

        cam.backgroundColor = Stages.SkyColor(level);
        World = new GameObject("World").transform;
        BuildBattlefield();

        SaveData.EnsureHeroPlaced();
        DeployedUids.Clear();
        foreach (var p in SaveData.Party)
        {
            if (DeployedUids.Count >= SaveData.DeployCap) break; // 출진 가능 인원까지만
            var m = SaveData.MemberByUid(p.uid);
            if (m == null) continue;
            SpawnCompanion(m, PlacementPosition(p.column, p.fx, p.fy));
            DeployedUids.Add(m.uid);
        }
        partySize = heroes.Count;

        userPaused = false;
        speed = SaveData.BattleSpeed;
        Time.timeScale = speed;

        AudioManager.PlayMusic("battle");
        // 스테이지 첫 라운드: 맵 입장 멘트
        if (Stages.SubOf(level) == 1) StartTalk(null, Stages.Script(level).entrance);
    }

    public void End()
    {
        if (World != null) Destroy(World.gameObject);
        World = null;
        heroes.Clear();
        enemies.Clear();
        leader = null;
        speed = 1;
        Time.timeScale = 1f;
    }

    void Finish(bool won)
    {
        if (finished) return;
        finished = true;
        victory = won;
        finishTimer = 2f;
        Time.timeScale = 1f;
        talk = null;
        AudioManager.StopMusic();
        AudioManager.Play(won ? "win" : "lose", 0.8f, 0f);
    }

    // 땅과 편성 줄을 그립니다. (나중에 진짜 그림으로 바꿀 부분)
    void BuildBattlefield()
    {
        var bg = UI.Background("battle_" + (Stages.StageOf(Level) + 1));
        if (bg != null)
        {
            // 스테이지 배경 그림을 화면 가득 (그림의 지평선이 위쪽 27% 근처)
            var go = new GameObject("Backdrop");
            go.transform.SetParent(World, false);
            go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            float ppu = bg.height / (cam.orthographicSize * 2f);
            sr.sprite = Sprite.Create(bg, new Rect(0, 0, bg.width, bg.height), new Vector2(0.5f, 0.5f), ppu);
            sr.sortingOrder = -3000;
            float needW = HalfScreenWidth * 2f, haveW = bg.width / ppu;
            if (haveW < needW) go.transform.localScale = Vector3.one * (needW / haveW); // 화면이 더 넓으면 키움
        }
        else
        {
            Color ground = Stages.GroundColor(Level);
            MakeBlock("Ground", new Vector2(0f, (LaneTop + LaneBottom) / 2f - 4f),
                new Vector2(80f, LaneTop - LaneBottom + 9f), ground, -2000);
            MakeBlock("Horizon", new Vector2(0f, LaneTop + 1.2f), new Vector2(80f, 0.3f), UI.Darken(ground, 0.8f), -1999);
        }

        // 선두 ~ 후미 줄 표시 (시작 위치): 은은한 그림자 띠
        for (int col = 0; col < SaveData.Columns; col++)
            MakeBlock("Column", new Vector2(ColumnCenterX(col), (LaneTop + LaneBottom) / 2f),
                new Vector2(ZoneWidth, LaneTop - LaneBottom + 1f), new Color(0.1f, 0.06f, 0.03f, col % 2 == 0 ? 0.10f : 0.05f), -1900);
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
        HandleKeys();

        if (finished)
        {
            finishTimer -= Time.unscaledDeltaTime;
            if (finishTimer <= 0f) onFinished?.Invoke(victory);
            return;
        }
        if (talk != null) { UpdateTalk(); return; }
        if (IsPaused) return;
        UpdateWaves();
    }

    // 키보드: 숫자 1~4 = 배속, 스페이스 = 일시정지/계속
    void HandleKeys()
    {
        if (finished || talk != null) return;
        int key = GameInput.SpeedKeyPressed();
        if (key > 0) SetSpeed(key);
        if (GameInput.PausePressed() && upgradeChoices == null)
        {
            userPaused = !userPaused;
            Time.timeScale = userPaused ? 0f : speed;
        }
    }

    void SetSpeed(int value)
    {
        speed = Mathf.Clamp(value, 1, 4);
        if (!userPaused) Time.timeScale = speed;
    }

    // ================= 사념파 대사 (맵 입장 멘트, 보스 대사) =================
    // 전투가 잠시 멈추고, 화면 위에 대사가 한 줄씩 떠오릅니다. 보스 그림이 있으면 오른쪽에 전신이 나와요.
    // 클릭: 다음 줄 (다 나왔으면 닫기) · 왼쪽 Ctrl: 빨리 감기 · 스테이지를 깬 적이 있으면 건너뛰기 버튼

    class Talk
    {
        public string speaker;      // 비어 있으면 해설 (맵 입장 멘트)
        public Texture2D image;     // 보스 전신 그림 (Resources/Enemies/<id>/full.png)
        public readonly List<string> lines = new List<string>();
        public readonly List<float> shownAt = new List<float>(); // 줄마다 나타난 시각
        public float clock;         // 대사 진행 시간 (빨리 감기하면 빨라짐)
        public float nextAt;        // 다음 줄이 나올 시각
        public float openedAt;      // 화면에 뜬 실제 시각 (어둡게 깔리는 효과용)
        public bool skippable;
    }

    Talk talk;
    static readonly float[] TalkCharsPerSecond = { 12f, 22f, 40f }; // 설정의 대사 속도 (느림/보통/빠름)

    void StartTalk(EnemyDef speaker, ScriptLine[] lines)
    {
        if (lines == null || lines.Length == 0) return;
        var deployed = new List<CompanionDef>();
        foreach (int uid in DeployedUids)
        {
            var m = SaveData.MemberByUid(uid);
            if (m != null) deployed.Add(m.Def);
        }
        var t = new Talk
        {
            speaker = speaker != null ? speaker.name : "",
            image = speaker != null ? EnemyLook.FullArt(speaker.id) : null,
            skippable = SaveData.IsBossBeaten(Stages.StageOf(Level)),
            openedAt = Time.unscaledTime,
            nextAt = 0.5f,
        };
        foreach (var line in lines)
            if (line.Applies(deployed)) t.lines.Add(line.text);
        if (t.lines.Count == 0) return;
        talk = t;
    }

    void UpdateTalk()
    {
        float speedUp = GameInput.FastForwardHeld() ? 4f : 1f;
        talk.clock += Time.unscaledDeltaTime * speedUp;
        if (talk.shownAt.Count < talk.lines.Count)
        {
            if (talk.clock >= talk.nextAt) RevealTalkLine();
        }
        else if (talk.clock >= talk.nextAt + 1.8f) talk = null; // 마지막 줄 뒤 잠깐 기다렸다 전투 재개
    }

    void RevealTalkLine()
    {
        string line = talk.lines[talk.shownAt.Count];
        talk.shownAt.Add(talk.clock);
        float cps = TalkCharsPerSecond[Mathf.Clamp(SaveData.TextSpeed, 0, 2)];
        talk.nextAt = talk.clock + 0.6f + line.Length / cps;
        AudioManager.Play("whisper", 0.5f, 0.1f);
    }

    void DrawTalk(float w)
    {
        float appear = Mathf.Clamp01((Time.unscaledTime - talk.openedAt) / 0.4f);
        UI.Fill(UI.Full, new Color(0.03f, 0.01f, 0.06f, 0.55f * appear));

        bool narration = talk.speaker == "";
        float textRight = w - 90f;
        if (talk.image != null)
        {
            // 오른쪽에 보스 전신
            float h = 640f, iw = h * talk.image.width / talk.image.height;
            var r = new Rect(w - iw - 30f, 720f - h - 10f + (1f - appear) * 30f, iw, h);
            UI.Glow(r.center, h * 0.45f, new Color(0.6f, 0.2f, 0.8f, 0.25f * appear));
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, appear);
            GUI.DrawTexture(r, talk.image, ScaleMode.ScaleToFit);
            GUI.color = old;
            textRight = r.x - 20f;
        }

        float lineH = 54f;
        float blockH = talk.lines.Count * lineH + (narration ? 0f : 44f);
        float y = 340f - blockH / 2f;
        float x = 90f, width = textRight - x;
        var anchor = narration && talk.image == null ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
        if (!narration)
        {
            UI.Text(new Rect(x, y, width, 36), talk.speaker, 22, UI.WithAlpha(UI.Gold, appear), anchor, true);
            UI.Fill(new Rect(x, y + 38, Mathf.Min(260f, width), 1), UI.WithAlpha(UI.Gold, 0.5f * appear));
            y += 44f;
        }
        for (int i = 0; i < talk.shownAt.Count; i++)
        {
            float t = Mathf.Clamp01((talk.clock - talk.shownAt[i]) / 0.6f);
            string line = talk.lines[i];
            bool aside = line.StartsWith("(");
            // 머릿속에 울리는 느낌: 살짝 흔들리며 떠오름
            float wobble = Mathf.Sin(Time.unscaledTime * 1.7f + i * 1.3f) * 2.5f;
            var r = new Rect(x + wobble, y + i * lineH + (1f - t) * 16f, width, lineH);
            Color main = narration ? UI.TextMain : new Color(0.93f, 0.88f, 1f);
            if (aside) main = new Color(0.75f, 0.72f, 0.70f);
            int size = aside ? 20 : 30;
            // 은은한 빛 번짐 (같은 글자를 흐리게 여러 번)
            Color aura = narration ? new Color(1f, 0.8f, 0.5f, 0.22f * t) : new Color(0.7f, 0.45f, 1f, 0.28f * t);
            for (int k = 0; k < 4; k++)
            {
                float ox = k % 2 == 0 ? -2f : 2f, oy = k < 2 ? -2f : 2f;
                UI.Text(new Rect(r.x + ox, r.y + oy, r.width, r.height), line, size, aura, anchor, true);
            }
            UI.Text(r, line, size, UI.WithAlpha(main, t), anchor, true);
        }

        UI.Text(new Rect(0, 672, w * 0.6f, 28), "클릭: 다음   ·   왼쪽 Ctrl: 빨리 감기", 14, UI.WithAlpha(UI.TextSub, 0.8f * appear));
        if (talk.skippable && UI.Button(new Rect(w - 176, 70, 150, 44), "건너뛰기 ▶▶", UI.WithAlpha(UI.Neutral, 0.9f), 17))
        {
            talk = null;
            return;
        }
        if (UI.ClickedAnywhere())
        {
            if (talk.shownAt.Count < talk.lines.Count) RevealTalkLine();
            else talk = null;
        }
    }

    // ================= 웨이브 =================

    void UpdateWaves()
    {
        // 웨이브는 기다리지 않고 바로 시작 (전투 보너스를 고르면 곧바로 다음 웨이브)
        if (!waveInProgress)
        {
            StartNextWave();
            return;
        }
        if (enemies.Count > 0) return;

        waveInProgress = false;
        if (Wave >= Stages.WavesPerLevel) Finish(true);
        else OfferUpgrades();
    }

    // 웨이브의 적들을 처음부터 오른쪽에 진을 친 채로 한꺼번에 배치합니다.
    // 적은 용사 파티가 가까이 오면(접근 거리 안) 그때부터 싸워요.
    // 어떤 적이 나오는지는 StageScript.cs(스테이지 대본)에서 정해요.
    void StartNextWave()
    {
        Wave++;
        waveInProgress = true;

        var script = Stages.Script(Level);
        int round = Stages.SubOf(Level);
        bool latePhase = round > Stages.MidBossRound;
        string[] pool = latePhase ? script.late : script.early;
        bool lastWave = Wave == Stages.WavesPerLevel;

        // 일반 적
        var troops = new List<EnemyDef>();
        int count = 4 + Mathf.Min(Level / 5, 12) + Wave * 2;
        for (int i = 0; i < count; i++) troops.Add(EnemyDef.Find(pool[Random.Range(0, pool.Length)]));

        // 후반 라운드(6~9)에는 '(1)' 인물이 가끔 섞여 나옴 (한 전투에 한 명씩만)
        if (latePhase && !Stages.HasBoss(Level))
            foreach (var id in script.lateElites)
                if (!spawnedNamed.Contains(id) && Random.value < 0.35f && troops.Count > 0)
                {
                    troops[troops.Count - 1] = EnemyDef.Find(id);
                    spawnedNamed.Add(id);
                }

        // 앞줄은 근접, 뒷줄은 원거리 · 마법 · 치유
        troops.Sort((x, y) => (x.IsRanged || x.role == EnemyRole.Healer ? 1 : 0).CompareTo(y.IsRanged || y.role == EnemyRole.Healer ? 1 : 0));

        // 배치할 공간: 파티보다 충분히 오른쪽 ~ 화면 오른쪽 끝
        float rightmostHero = -99f;
        foreach (var h in heroes) if (h != null) rightmostHero = Mathf.Max(rightmostHero, h.transform.position.x);
        float maxX = HalfScreenWidth - 0.8f;
        float minX = Mathf.Max(1.5f, rightmostHero + EnemyStartGap);
        if (maxX - minX < 3f) minX = maxX - 3f;

        const int rows = 5;
        int columns = Mathf.CeilToInt(troops.Count / (float)rows);
        float colGap = columns > 1 ? Mathf.Min(1.4f, (maxX - minX) / (columns - 1)) : 0f;
        for (int i = 0; i < troops.Count; i++)
        {
            int col = i / rows, row = i % rows;
            float x = minX + col * colGap + Random.Range(-0.25f, 0.25f);
            float y = LaneTop - (row + 0.5f) * (LaneTop - LaneBottom) / rows + Random.Range(-0.35f, 0.35f);
            SpawnEnemy(troops[i], new Vector2(Mathf.Min(x, maxX), y), false);
        }

        // 마지막 웨이브: 5라운드는 중간 보스, 10라운드는 최종 보스(+ 함께 나오는 인물)가 진형 맨 뒤에
        if (!lastWave) return;
        float midY = (LaneTop + LaneBottom) / 2f;
        var bossPos = new Vector2(maxX - 0.6f, midY);
        if (Stages.HasBoss(Level))
        {
            for (int i = 0; i < script.bossGroup.Length; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                SpawnEnemy(EnemyDef.Find(script.bossGroup[i]), new Vector2(maxX - 2.2f, midY + side * (2.2f + i / 2 * 1.2f)), false);
            }
            var def = EnemyDef.Find(script.boss);
            SpawnEnemy(def, bossPos, true);
            AudioManager.PlayMusic("boss");
            StartTalk(def, script.bossLines);
        }
        else if (Stages.HasMidBoss(Level))
        {
            var def = EnemyDef.Find(script.midBoss);
            SpawnEnemy(def, bossPos, true);
            AudioManager.PlayMusic("boss");
            StartTalk(def, script.midBossLines);
        }
    }

    readonly HashSet<string> spawnedNamed = new HashSet<string>(); // 이번 전투에 이미 나온 '(1)' 인물

    void SpawnEnemy(EnemyDef def, Vector2 pos, bool mainBoss)
    {
        if (def == null) return;
        float hpMul = 1f + 0.12f * (Level - 1) + 0.1f * (Wave - 1);
        float dmgMul = 1f + 0.05f * (Level - 1);
        if (def.id == EnemyDef.BossId(Stages.StageCount - 1)) { hpMul *= 1.4f; dmgMul *= 1.5f; } // 마왕은 더 강하게
        SaveData.SeenEnemies.Add(def.id); // 도감에 등록
        if (def.IsNamed) spawnedNamed.Add(def.id);

        var u = CreateUnit(def.name, Team.Enemy, pos);
        u.maxHp = def.Hp * hpMul;
        u.damage = def.Damage * dmgMul;
        u.attackRange = def.Range;
        u.attackCooldown = def.Cooldown;
        u.moveSpeed = def.Speed;
        u.size = def.size;
        u.splashRadius = def.Splash;
        u.ranged = def.IsRanged;
        u.healer = def.role == EnemyRole.Healer;
        u.goldReward = def.Gold;
        u.isBoss = def.type == EnemyType.Boss || def.type == EnemyType.MidBoss;
        u.title = def.IsNamed ? def.name : null;
        u.projectileColor = def.IsRanged ? Color.Lerp(def.accent, Color.white, 0.3f) : def.accent;
        u.attackSound = EnemySound(def);
        u.impactSound = def.role == EnemyRole.Caster ? "explode" : null;
        u.look = EnemyLook.For(def);
        u.Setup(SpriteFactory.Square(), def.color, Color.clear);
        if (mainBoss)
        {
            boss = u;
            bossName = def.name;
        }
        enemies.Add(u);
    }

    static string EnemySound(EnemyDef def)
    {
        if (def.role == EnemyRole.Healer) return null; // 치유 소리는 따로 남
        switch (def.shape)
        {
            case EnemyShape.Beast: case EnemyShape.Lynx: case EnemyShape.Bear: case EnemyShape.Shark:
            case EnemyShape.Worm: case EnemyShape.Wyvern: case EnemyShape.Scorpion:
                return def.role == EnemyRole.Brute || def.size >= 1f ? "heavy" : "bite";
            case EnemyShape.Golem: case EnemyShape.Kraken: return def.role == EnemyRole.Caster ? "magic" : "heavy";
        }
        switch (def.weapon)
        {
            case EnemyWeapon.Bow: case EnemyWeapon.Crossbow: case EnemyWeapon.Sling: return "arrow";
            case EnemyWeapon.Staff: return "magic";
            case EnemyWeapon.Spear: return "stab";
            case EnemyWeapon.Hammer: case EnemyWeapon.Axe: return def.role == EnemyRole.Brute || def.role == EnemyRole.Tank ? "heavy" : "slash";
            default: return "slash";
        }
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

    void SpawnCompanion(SaveData.Member member, Vector2 pos)
    {
        var def = member.Def;
        var c = def.job;
        float m = SaveData.StatMultiplier(member); // 등급 × 레벨 × 성급 × 호감도

        var u = CreateUnit(def.name, Team.Hero, pos);
        u.heroClass = c;
        u.faction = def.faction;
        u.isLeader = def.IsHero;
        u.homePosition = pos;
        u.moveSpeed = c.speed * PartySpeedScale;
        u.maxHp = c.hp * m * hpBonus;
        u.damage = c.damage * m;
        u.attackRange = c.range;
        u.attackCooldown = c.cooldown;
        u.splashRadius = c.splash;
        u.size = c.size;
        u.ranged = c.ranged;
        u.healer = c.healer;
        u.projectileColor = c.color;
        u.attackSound = c.healer ? null : c.ranged ? (c.splash > 0f ? "magic" : "arrow")
            : c == HeroClass.Lancer ? "stab" : c == HeroClass.Porter || c == HeroClass.Knight ? "heavy" : "slash";
        u.impactSound = c.splash > 0f ? "explode" : null;
        u.art = CharacterArt.For(def.id); // 그림이 있으면 그림으로 나옴
        u.Setup(SpriteFactory.Circle(), c.color, RarityInfo.GetColor(def.rarity));
        heroes.Add(u);
        if (def.IsHero) leader = u;
    }

    // ================= 시너지 & 보너스 =================
    // 같은 소속 동료가 2명 → 1단계, 4명 → 2단계 (용사는 숫자에 안 들어가지만 '용사 파티' 효과는 받음)
    // 효과는 그 소속 동료들에게만 적용됩니다.

    public class Synergy
    {
        public string faction;
        public string effect;
        public Color color;
    }

    public static readonly Synergy[] Synergies =
    {
        new Synergy { faction = "용사 파티", effect = "공격력·치유량 증가", color = new Color(1.00f, 0.80f, 0.35f) },
        new Synergy { faction = "올 왕국",   effect = "받는 피해 감소",     color = new Color(0.45f, 0.65f, 1.00f) },
        new Synergy { faction = "자유 용병", effect = "공격 속도 증가",     color = new Color(0.55f, 0.90f, 0.50f) },
    };

    public int CountOf(string faction)
    {
        int n = 0;
        foreach (var h in heroes) if (!h.isLeader && h.faction == faction) n++;
        return n;
    }

    public int SynergyTier(string faction)
    {
        int n = CountOf(faction);
        return n >= 4 ? 2 : n >= 2 ? 1 : 0;
    }

    int TierFor(Unit u, string faction) => u.team == Team.Hero && u.faction == faction ? SynergyTier(faction) : 0;

    public float DamageMultiplier(Unit u)
    {
        if (u.team != Team.Hero) return 1f;
        return damageBonus * (1f + 0.15f * TierFor(u, "용사 파티"));
    }

    public float RangeMultiplier(Unit u) => u.team == Team.Hero && (u.ranged || u.heroClass.healer) ? rangeBonus : 1f;

    public float CooldownMultiplier(Unit u)
    {
        if (u.team != Team.Hero) return 1f;
        float speed = attackSpeedBonus * (1f + 0.15f * TierFor(u, "자유 용병"));
        return 1f / speed;
    }

    public float SplashMultiplier(Unit u) => 1f;

    public float HealMultiplier(Unit u) => 1f + 0.15f * TierFor(u, "용사 파티");

    public float DamageTakenMultiplier(Unit u) => 1f - 0.12f * TierFor(u, "올 왕국");

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

    // 체력 비율이 가장 낮은 아군 (다친 아군이 없으면 null)
    public Unit FindMostHurtAlly(Unit healer)
    {
        Unit best = null;
        float lowest = 0.999f;
        foreach (var h in healer.team == Team.Hero ? heroes : enemies)
        {
            if (h == null || !h.IsAlive) continue;
            float pct = h.hp / h.maxHp;
            if (pct >= lowest) continue;
            lowest = pct;
            best = h;
        }
        return best;
    }

    // 치유 직업이 따라갈, 가장 가까운 싸우는 아군 (치유 직업 제외)
    public Unit FindNearestFighter(Unit healer)
    {
        Unit best = null;
        float bestDist = float.MaxValue;
        Vector2 pos = healer.transform.position;
        foreach (var h in healer.team == Team.Hero ? heroes : enemies)
        {
            if (h == null || !h.IsAlive || h.healer) continue;
            float d = ((Vector2)h.transform.position - pos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = h; }
        }
        return best;
    }

    // 같은 편끼리 한 점에 겹치지 않도록 살짝 밀어냅니다.
    public Vector2 SeparationFor(Unit u)
    {
        Vector2 pos = u.transform.position;
        Vector2 push = Vector2.zero;
        foreach (var other in u.team == Team.Hero ? heroes : enemies)
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

    // ================= 전투 보너스 (로그라이크 요소: 이번 판에서만 유지) =================

    void OfferUpgrades()
    {
        var pool = new List<Upgrade>
        {
            new Upgrade { title = "날카로운 무기", desc = "모든 동료 공격력 +8%", apply = () => damageBonus *= 1.08f },
            new Upgrade { title = "빠른 손놀림", desc = "모든 동료 공격 속도 +6%", apply = () => attackSpeedBonus *= 1.06f },
            new Upgrade { title = "매의 눈", desc = "원거리·치유 동료의 사거리 +8%", apply = () => rangeBonus *= 1.08f },
            new Upgrade { title = "강철 갑옷", desc = "모든 동료 최대 체력 +10%", apply = () =>
                {
                    hpBonus *= 1.1f;
                    foreach (var h in heroes) { h.maxHp *= 1.1f; h.hp *= 1.1f; h.UpdateHpBar(); }
                } },
            new Upgrade { title = "치유의 샘", desc = "모든 동료 체력 50% 회복", apply = () =>
                {
                    foreach (var h in heroes) { h.hp = Mathf.Min(h.maxHp, h.hp + h.maxHp * 0.5f); h.UpdateHpBar(); }
                } },
            new Upgrade { title = "전리품", desc = "골드 +" + (15 + Level * 5 / 2), apply = () => GoldEarned += 15 + Level * 5 / 2 },
        };
        if (leader != null && leader.IsAlive)
        {
            pool.Add(new Upgrade { title = "용사의 함성", desc = "용사 공격력 +25%, 체력 50% 회복", apply = () =>
                {
                    if (leader == null) return;
                    leader.damage *= 1.25f;
                    leader.hp = Mathf.Min(leader.maxHp, leader.hp + leader.maxHp * 0.5f);
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

        // 위쪽 정보 막대: 왼쪽 판 정보 / 가운데 웨이브 / 오른쪽 골드와 버튼
        UI.Fill(new Rect(0, 0, w, 56), new Color(0.03f, 0.03f, 0.06f, 0.7f));
        UI.Fill(new Rect(0, 55, w, 1), UI.WithAlpha(UI.Gold, 0.35f));
        UI.Text(new Rect(20, 0, w * 0.36f, 56), $"{Stages.Label(Level)}  ·  {Stages.StageName(Level)}", 19, UI.TextMain, TextAnchor.MiddleLeft, true);
        UI.Chip(new Rect(w / 2 - 80, 12, 160, 32), $"웨이브 {Mathf.Max(Wave, 1)} / {Stages.WavesPerLevel}", UI.Neutral, 18);
        UI.Text(new Rect(w - 470, 0, 140, 56), $"+{GoldEarned} 골드", 18, UI.Gold, TextAnchor.MiddleRight, true);
        // 배속: 누를 때마다 x1 → x2 → x3 → x4 → x1
        if (UI.Button(new Rect(w - 316, 10, 296, 36), $"전투 속도  x{speed}   (눌러서 변경)", UI.Blue, 16, !finished && talk == null))
            SetSpeed(speed % 4 + 1);

        // 왼쪽: 파티 상태
        var party = new Rect(16, 66, 300, 44);
        UI.Panel(party, UI.WithAlpha(UI.PanelColor, 0.82f));
        UI.Text(new Rect(party.x + 16, party.y + 8, party.width - 32, 28), $"파티 생존  {heroes.Count} / {partySize}", 17, UI.TextMain, TextAnchor.MiddleLeft, true);

        // 오른쪽: 시너지
        var syn = new Rect(w - 316, 66, 300, 40 + Synergies.Length * 24);
        UI.Panel(syn, UI.WithAlpha(UI.PanelColor, 0.82f));
        UI.Text(new Rect(syn.x + 16, syn.y + 6, syn.width - 32, 26), "소속 시너지  (2명 / 4명)", 15, UI.TextSub, TextAnchor.MiddleLeft, true);
        float y = syn.y + 32;
        foreach (var sy in Synergies)
        {
            int tier = SynergyTier(sy.faction);
            string stars = tier == 2 ? "★★" : tier == 1 ? "★" : "";
            Color col = tier > 0 ? sy.color : UI.WithAlpha(UI.TextSub, 0.7f);
            UI.Text(new Rect(syn.x + 16, y, 150, 24), $"{sy.faction} {CountOf(sy.faction)} {stars}", 15, col, TextAnchor.MiddleLeft, true);
            UI.Text(new Rect(syn.x + 150, y, syn.width - 166, 24), sy.effect, 14, col, TextAnchor.MiddleRight);
            y += 24;
        }

        // 이름 있는 적(정예 · 보스)은 머리 위에 이름표
        foreach (var e in enemies)
        {
            if (e == null || e.title == null) continue;
            var p = UI.WorldToUI(cam, e.NameAnchor);
            UI.Text(new Rect(p.x - 120, p.y - 26, 240, 24), e.title, 14, e.isBoss ? new Color(1f, 0.55f, 0.5f) : new Color(1f, 0.85f, 0.55f), TextAnchor.MiddleCenter, true);
        }

        // 보스 체력 (가운데)
        if (boss != null && boss.IsAlive)
        {
            float bw = Mathf.Min(420f, w - 680f);
            UI.Text(new Rect(w / 2 - bw / 2, 64, bw, 26), bossName, 19, new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleCenter, true);
            UI.Bar(new Rect(w / 2 - bw / 2, 92, bw, 18), boss.hp / boss.maxHp, new Color(0.85f, 0.18f, 0.22f));
        }

        // 키 안내 (화면 아래)
        UI.Text(new Rect(16, UI.Height - 30, 600, 24), "1~4: 배속   ·   Space: 일시정지", 14, UI.WithAlpha(UI.TextMain, 0.7f), TextAnchor.MiddleLeft);

        // 일시정지
        if (userPaused && !finished)
        {
            UI.Fill(UI.Full, new Color(0f, 0f, 0f, 0.45f));
            var r = new Rect(w / 2 - 220, 270, 440, 110);
            UI.Panel(r);
            UI.Text(new Rect(r.x, r.y + 14, r.width, 50), "일시정지", 34, UI.Gold, TextAnchor.MiddleCenter, true);
            UI.Text(new Rect(r.x, r.y + 64, r.width, 30), "Space를 누르면 계속해요", 17, UI.TextSub);
        }

        if (upgradeChoices != null) DrawUpgradeChoices(w);
        if (talk != null && !finished) DrawTalk(w);

        if (finished)
        {
            UI.Fill(UI.Full, new Color(0f, 0f, 0f, 0.45f));
            string text = victory ? "클리어!" : "파티 전멸...";
            UI.Text(new Rect(0, 280, w, 80), text, 56, victory ? UI.Gold : new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleCenter, true);
        }
    }

    // 웨이브 사이에 고르는 전투 보너스 (캠프의 '강화'와는 다른, 이번 판에서만 유지되는 효과)
    void DrawUpgradeChoices(float w)
    {
        UI.Fill(UI.Full, new Color(0f, 0f, 0f, 0.65f));
        UI.Text(new Rect(0, 140, w, 50), $"웨이브 {Wave} 클리어!", 34, UI.Gold, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(0, 188, w, 30), "전투 보너스를 하나 고르세요  (이번 판 동안만 유지)", 18, UI.TextSub);

        const float cw = 260, ch = 190, gap = 28;
        float x0 = (w - (cw * upgradeChoices.Count + gap * (upgradeChoices.Count - 1))) / 2f;
        for (int i = 0; i < upgradeChoices.Count; i++)
        {
            var u = upgradeChoices[i];
            var r = new Rect(x0 + i * (cw + gap), 245, cw, ch);
            if (UI.Button(r, "", UI.Neutral))
            {
                u.apply();
                upgradeChoices = null;
                return;
            }
            UI.Text(new Rect(r.x + 12, r.y + 24, r.width - 24, 36), u.title, 22, UI.Gold, TextAnchor.MiddleCenter, true);
            UI.Fill(new Rect(r.x + 40, r.y + 70, r.width - 80, 1), UI.WithAlpha(UI.Gold, 0.3f));
            UI.Text(new Rect(r.x + 18, r.y + 84, r.width - 36, 80), u.desc, 18, UI.TextMain);
        }
    }
}
