using System.Collections.Generic;
using UnityEngine;

// 게임 전체 흐름을 관리합니다.
// 타이틀 → 오프닝 → 첫 소환(동료 2명) → 캠프(출격/소환/편성/도감/저장) → 전투 → 결과 → 캠프 ...
// 씬에 따로 넣지 않아도 ▶(플레이)를 누르면 자동으로 생성됩니다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    const float HalfWorldWidth = 14f; // 전투 화면에서 항상 보이는 가로 반폭

    enum Page { Title, Story, Camp, GachaSelect, Gacha, Formation, Roster, Battle, Result, SaveLoad, Settings }
    enum Modal { None, ConfirmNewGame, ConfirmQuit }

    // 스토리 장면 그림 종류 (StoryArt.cs)
    enum Art { King, HeroGacha, Victory, Kingdom }

    class StoryLine
    {
        public string speaker; // 비어 있으면 해설
        public string text;
        public Art art;
        public StoryLine(Art art, string speaker, string text) { this.art = art; this.speaker = speaker; this.text = text; }
    }

    // ★ 오프닝 대사 (여기서 고치면 게임에 바로 반영돼요)
    static readonly StoryLine[] OpeningStory =
    {
        new StoryLine(Art.King,      "왕",   "오오... 용사여 왔는가."),
        new StoryLine(Art.King,      "왕",   "나는 올 왕국의 왕."),
        new StoryLine(Art.King,      "왕",   "용사의 모습을 보니 젊은 시절 나의 모습이 떠오르는구나."),
        new StoryLine(Art.King,      "왕",   "부디 세상을 어지럽히는 마왕을 무찔러주길."),
        new StoryLine(Art.King,      "왕",   "마왕을 무찌르고 돌아온다면 자네와 공주를 혼인시키도록 하겠다..."),
        new StoryLine(Art.King,      "왕",   "그럼. 반드시 좋은 결과를 가지고 오게나."),
        new StoryLine(Art.HeroGacha, "용사", "헤헤... 그럼 먼저 동료부터 뽑아 볼까?"),
    };

    static readonly StoryLine[] EndingStory =
    {
        new StoryLine(Art.Victory,   "",     "마침내 마왕이 쓰러졌다."),
        new StoryLine(Art.King,      "왕",   "오오... 용사여, 정말로 마왕을 무찔렀구먼!"),
        new StoryLine(Art.King,      "왕",   "약속대로 자네와 공주의 혼인을 허락하겠네."),
        new StoryLine(Art.HeroGacha, "용사", "헤헤... 그 전에 축하 뽑기 한 번만 더 해도 될까요?"),
        new StoryLine(Art.Kingdom,   "",     "- 끝 -\n(캠프로 돌아가 계속 플레이할 수 있습니다)"),
    };

    static readonly float[] TextSpeeds = { 15f, 30f, 60f }; // 대사 속도: 1초에 나오는 글자 수
    static readonly string[] TextSpeedNames = { "느림", "보통", "빠름" };

    Page page = Page.Title;
    Modal modal = Modal.None;
    BattleManager battle;
    readonly GachaScreen gacha = new GachaScreen();
    readonly FormationScreen formation = new FormationScreen();
    Camera cam;

    StoryLine[] story;
    int storyIndex;
    float storyLineStart;
    bool storySkippable;
    System.Action onStoryEnd;

    int selectedStage = 1;   // 고른 판 번호 (1~100)
    bool saveLoadIsLoad;     // 저장/불러오기 화면: true = 불러오기, false = 저장
    Page returnPage;         // 저장/불러오기/설정 화면에서 돌아갈 곳
    Vector2 rosterScroll;

    // 결과 화면 정보
    bool resultVictory;
    int resultStage;
    int resultKillGold;
    int resultClearGold;
    bool resultFirstClear;
    bool resultFled;
    string resultUnlock;

    string toast;
    float toastUntil;

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
        SaveData.Load(0);
        SetupCamera();
        battle = gameObject.AddComponent<BattleManager>();
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
        cam.backgroundColor = Color.black;
    }

    void Update()
    {
        // 화면 비율이 달라도 전장 전체가 보이도록 맞춥니다.
        cam.orthographicSize = Mathf.Max(8f, HalfWorldWidth / cam.aspect);
    }

    void ShowToast(string text)
    {
        toast = text;
        toastUntil = Time.unscaledTime + 2.5f;
    }

    static float Now => Time.unscaledTime;

    // ================= 화면 그리기 =================

    void OnGUI()
    {
        UI.Begin();
        switch (page)
        {
            case Page.Title: DrawTitle(); break;
            case Page.Story: DrawStory(); break;
            case Page.Camp: DrawCamp(); break;
            case Page.GachaSelect: DrawGachaSelect(); break;
            case Page.Gacha: gacha.Draw(); break;
            case Page.Formation: formation.Draw(() => page = Page.Camp, ShowToast); break;
            case Page.Roster: DrawRoster(); break;
            case Page.Battle: battle.DrawHUD(); break;
            case Page.Result: DrawResult(); break;
            case Page.SaveLoad: DrawSaveLoad(); break;
            case Page.Settings: DrawSettings(); break;
        }

        if (toast != null && Now < toastUntil)
        {
            float w = UI.Width;
            var r = new Rect(w / 2f - 320f, 90f, 640f, 50f);
            UI.Fill(r, new Color(0f, 0f, 0f, 0.8f));
            UI.Text(r, toast, 20, Color.white, TextAnchor.MiddleCenter, true);
        }
    }

    // ---------------- 타이틀 ----------------

    void DrawTitle()
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.08f, 0.10f, 0.30f), new Color(0.35f, 0.15f, 0.40f));

        // 반짝이는 별
        for (int i = 0; i < 40; i++)
        {
            float x = Mathf.Repeat(i * 137.5f, w);
            float y = Mathf.Repeat(i * 89.3f, 450f);
            float a = 0.3f + 0.7f * Mathf.Abs(Mathf.Sin(Now * (0.5f + i % 5 * 0.3f) + i));
            UI.Glow(new Vector2(x, y), 8f, new Color(1f, 1f, 0.9f, a));
        }

        UI.Glow(new Vector2(w / 2f, 170f), 330f, new Color(1f, 0.8f, 0.3f, 0.25f));
        UI.Text(new Rect(0, 100, w, 110), "가챠 용사", 96, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(0, 205, w, 40), "마왕을 무찌를 동료는 뽑기로 정한다!", 24, Color.white);

        // 메뉴 (확인 창이 떠 있을 때는 눌리지 않게 막습니다)
        bool active = modal == Modal.None;
        float bx = w / 2f - 150f, by = 290f;
        const float bh = 62f, gap = 12f;
        if (UI.Button(new Rect(bx, by, 300, bh), "새로 시작", new Color(0.85f, 0.55f, 0.15f), 26, active))
        {
            if (SaveData.HasSave) modal = Modal.ConfirmNewGame;
            else NewGame();
        }
        if (UI.Button(new Rect(bx, by + (bh + gap), 300, bh), "이어하기", new Color(0.3f, 0.5f, 0.75f), 24, active && SaveData.HasSave))
            ContinueGame();
        if (UI.Button(new Rect(bx, by + (bh + gap) * 2, 300, bh), "불러오기", new Color(0.3f, 0.5f, 0.75f), 24, active))
            OpenSaveLoad(true, Page.Title);
        if (UI.Button(new Rect(bx, by + (bh + gap) * 3, 300, bh), "설정", new Color(0.35f, 0.4f, 0.5f), 24, active))
        {
            returnPage = Page.Title;
            page = Page.Settings;
        }
        if (UI.Button(new Rect(bx, by + (bh + gap) * 4, 300, bh), "끝내기", new Color(0.45f, 0.25f, 0.3f), 24, active))
            modal = Modal.ConfirmQuit;

        if (modal == Modal.ConfirmNewGame)
        {
            if (DrawConfirm("새로 시작하면 처음(오프닝)부터 시작해요.\n이어하기 기록은 지워져요.\n(불러오기 슬롯에 저장한 기록은 남아요)", "새로 시작"))
                NewGame();
        }
        else if (modal == Modal.ConfirmQuit)
        {
            if (DrawConfirm("게임을 끝낼까요?", "끝내기"))
                QuitGame();
        }
    }

    // 확인 창. "예"를 누르면 true. 어느 버튼이든 누르면 창이 닫힙니다.
    bool DrawConfirm(string message, string yesLabel)
    {
        float w = UI.Width;
        UI.Fill(UI.Full, new Color(0f, 0f, 0f, 0.7f));
        var box = new Rect(w / 2f - 280f, 230f, 560f, 260f);
        UI.Panel(box, new Color(0.15f, 0.15f, 0.25f));
        UI.Text(new Rect(box.x + 20, box.y + 20, box.width - 40, 130), message, 22, Color.white);
        bool yes = UI.Button(new Rect(box.x + 50, box.y + 170, 210, 64), yesLabel, new Color(0.7f, 0.3f, 0.25f));
        bool no = UI.Button(new Rect(box.xMax - 260, box.y + 170, 210, 64), "취소", new Color(0.35f, 0.4f, 0.6f));
        if (yes || no) modal = Modal.None;
        return yes;
    }

    void NewGame()
    {
        modal = Modal.None;
        SaveData.ResetAll();
        selectedStage = 1;
        // 오프닝을 본 적이 있는 플레이어만 건너뛰기 버튼이 나옵니다.
        PlayStory(OpeningStory, SaveData.OpeningSeen, StartFirstSummon);
    }

    void ContinueGame()
    {
        SaveData.Load(0);
        selectedStage = Mathf.Min(SaveData.ClearedStage + 1, Stages.Count);
        page = Page.Camp;
    }

    static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 처음 동료 두 명은 무료로 고급 뽑기에서 뽑습니다.
    void StartFirstSummon()
    {
        SaveData.OpeningSeen = true;
        page = Page.Gacha;
        gacha.Start(GachaBanner.Advanced, 2, () =>
        {
            SaveData.AutoArrange();
            SaveData.Save();
            page = Page.Camp;
            ShowToast("동료들과 함께 여정을 떠나요! '편성'을 확인하고 '출격'하세요");
        });
    }

    // ---------------- 저장 / 불러오기 ----------------

    void OpenSaveLoad(bool isLoad, Page back)
    {
        saveLoadIsLoad = isLoad;
        returnPage = back;
        page = Page.SaveLoad;
    }

    void DrawSaveLoad()
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.10f, 0.12f, 0.22f), new Color(0.05f, 0.05f, 0.10f));
        UI.Fill(new Rect(0, 0, w, 64), new Color(0f, 0f, 0f, 0.5f));
        UI.Text(new Rect(24, 0, 700, 64), saveLoadIsLoad ? "불러오기" : "저장하기", 28, Color.white, TextAnchor.MiddleLeft, true);

        for (int slot = 0; slot < SaveData.SlotCount; slot++)
        {
            var r = new Rect(80, 100 + slot * 120, w - 160, 105);
            bool has = SaveData.HasSlot(slot);
            UI.Panel(r, new Color(0f, 0f, 0f, 0.45f));
            string label = slot == 0 ? "자동 저장 (이어하기)" : $"슬롯 {slot}";
            UI.Text(new Rect(r.x + 24, r.y + 10, 400, 34), label, 24, slot == 0 ? new Color(1f, 0.85f, 0.3f) : Color.white, TextAnchor.MiddleLeft, true);
            UI.Text(new Rect(r.x + 24, r.y + 44, r.width - 300, 56), SaveData.SlotSummary(slot), 17,
                has ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 1f, 1f, 0.4f), TextAnchor.UpperLeft);

            var btn = new Rect(r.xMax - 230, r.y + 22, 200, 60);
            if (saveLoadIsLoad)
            {
                if (UI.Button(btn, "불러오기", new Color(0.3f, 0.5f, 0.75f), 22, has))
                {
                    SaveData.Load(slot);
                    SaveData.Save(); // 불러온 기록으로 이어하기
                    selectedStage = Mathf.Min(SaveData.ClearedStage + 1, Stages.Count);
                    page = Page.Camp;
                    ShowToast(slot == 0 ? "자동 저장을 불러왔어요" : $"슬롯 {slot}을 불러왔어요");
                }
            }
            else if (slot > 0 && UI.Button(btn, has ? "덮어쓰기" : "여기에 저장", new Color(0.25f, 0.55f, 0.45f), 22))
            {
                SaveData.SaveToSlot(slot);
                ShowToast($"슬롯 {slot}에 저장했어요");
            }
        }

        if (UI.Button(new Rect(80, 600, 180, 56), "◀ 돌아가기", new Color(0.3f, 0.3f, 0.4f), 20))
            page = returnPage;
    }

    // ---------------- 설정 ----------------

    void DrawSettings()
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.12f, 0.12f, 0.18f), new Color(0.05f, 0.05f, 0.08f));
        UI.Fill(new Rect(0, 0, w, 64), new Color(0f, 0f, 0f, 0.5f));
        UI.Text(new Rect(24, 0, 700, 64), "설정", 28, Color.white, TextAnchor.MiddleLeft, true);

        float x = w / 2f - 380f, y = 120f;
        var on = new Color(0.85f, 0.55f, 0.15f);
        var off = new Color(0.3f, 0.32f, 0.4f);

        UI.Text(new Rect(x, y, 260, 56), "대사 속도", 24, Color.white, TextAnchor.MiddleLeft, true);
        for (int i = 0; i < TextSpeedNames.Length; i++)
            if (UI.Button(new Rect(x + 280 + i * 160, y, 145, 56), TextSpeedNames[i], SaveData.TextSpeed == i ? on : off, 22))
                SaveData.TextSpeed = i;

        y += 90;
        UI.Text(new Rect(x, y, 260, 56), "전투 시작 속도", 24, Color.white, TextAnchor.MiddleLeft, true);
        if (UI.Button(new Rect(x + 280, y, 145, 56), "x1", !SaveData.FastBattle ? on : off, 22)) SaveData.FastBattle = false;
        if (UI.Button(new Rect(x + 440, y, 145, 56), "x2", SaveData.FastBattle ? on : off, 22)) SaveData.FastBattle = true;

        y += 90;
        UI.Text(new Rect(x, y, 260, 56), "화면", 24, Color.white, TextAnchor.MiddleLeft, true);
        if (UI.Button(new Rect(x + 280, y, 145, 56), "창 모드", !Screen.fullScreen ? on : off, 22)) Screen.fullScreen = false;
        if (UI.Button(new Rect(x + 440, y, 145, 56), "전체 화면", Screen.fullScreen ? on : off, 22)) Screen.fullScreen = true;

        y += 90;
        UI.Text(new Rect(x, y, 260, 56), "오프닝 건너뛰기", 24, Color.white, TextAnchor.MiddleLeft, true);
        UI.Text(new Rect(x + 280, y, 460, 56), SaveData.OpeningSeen ? "사용 가능 (오프닝을 본 적이 있어요)" : "오프닝을 한 번 보면 사용할 수 있어요", 18,
            new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleLeft);

        if (UI.Button(new Rect(80, 600, 180, 56), "◀ 돌아가기", new Color(0.3f, 0.3f, 0.4f), 20))
            page = returnPage;
    }

    // ---------------- 스토리 ----------------

    void PlayStory(StoryLine[] lines, bool skippable, System.Action onEnd)
    {
        story = lines;
        storyIndex = 0;
        storyLineStart = Now;
        storySkippable = skippable;
        onStoryEnd = onEnd;
        page = Page.Story;
    }

    void DrawStory()
    {
        float w = UI.Width;
        var line = story[storyIndex];
        int shown = Mathf.Min(line.text.Length, Mathf.FloorToInt((Now - storyLineStart) * TextSpeeds[Mathf.Clamp(SaveData.TextSpeed, 0, 2)]));
        bool talking = shown < line.text.Length;

        switch (line.art)
        {
            case Art.King: StoryArt.DrawKing(w, talking); break;
            case Art.HeroGacha: StoryArt.DrawHeroGacha(w); break;
            case Art.Victory: StoryArt.DrawVictory(w); break;
            default: StoryArt.DrawKingdom(w); break;
        }

        // 대사 상자
        var box = new Rect(40, 480, w - 80, 200);
        UI.Panel(box, new Color(0.05f, 0.05f, 0.12f, 0.88f));
        if (line.speaker != "")
        {
            var nameTag = new Rect(box.x + 20, box.y - 22, 160, 44);
            UI.Fill(nameTag, new Color(0.75f, 0.5f, 0.15f));
            UI.Text(nameTag, line.speaker, 22, Color.white, TextAnchor.MiddleCenter, true);
        }

        // 한 글자씩 나타나는 효과
        UI.Text(new Rect(box.x + 40, box.y + 35, box.width - 80, box.height - 60), line.text.Substring(0, shown), 26,
            Color.white, TextAnchor.UpperLeft);
        if (!talking && Mathf.Repeat(Now, 1f) < 0.6f)
            UI.Text(new Rect(box.xMax - 80, box.yMax - 50, 50, 40), "▼", 22, Color.white);

        // 오른쪽 위 건너뛰기 (오프닝은 본 적이 있을 때만)
        if (storySkippable && UI.Button(new Rect(w - 170, 20, 150, 46), "건너뛰기 ▶▶", new Color(0.3f, 0.3f, 0.4f), 18))
        {
            onStoryEnd?.Invoke();
            return;
        }

        if (UI.ClickedAnywhere())
        {
            if (talking) storyLineStart = -1000f; // 글자를 한 번에 다 보여 줌
            else if (storyIndex + 1 < story.Length)
            {
                storyIndex++;
                storyLineStart = Now;
            }
            else onStoryEnd?.Invoke();
        }
    }

    // ---------------- 캠프 (메인 화면) ----------------

    void DrawCamp()
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.15f, 0.20f, 0.35f), new Color(0.30f, 0.22f, 0.20f));
        UI.Fill(new Rect(0, 560, w, 160), new Color(0.22f, 0.32f, 0.20f));

        // 모닥불
        var fire = new Vector2(w / 2f, 620f);
        UI.Glow(fire, 120f + 8f * Mathf.Sin(Now * 7f), new Color(1f, 0.55f, 0.15f, 0.5f));
        UI.Glow(fire, 30f + 4f * Mathf.Sin(Now * 11f), new Color(1f, 0.9f, 0.4f, 0.9f));

        DrawTopBar(w, "용사의 캠프");

        // 왼쪽: 출격 판
        var left = new Rect(40, 90, w * 0.5f - 60, 440);
        UI.Panel(left, new Color(0f, 0f, 0f, 0.45f));
        UI.Text(new Rect(left.x, left.y + 12, left.width, 40), "출격", 30, Color.white, TextAnchor.MiddleCenter, true);

        int maxLevel = Mathf.Min(SaveData.ClearedStage + 1, Stages.Count);
        selectedStage = Mathf.Clamp(selectedStage, 1, maxLevel);
        float cx = left.center.x;
        var arrow = new Color(0.3f, 0.35f, 0.5f);
        // ◀◀ ▶▶ 는 스테이지 단위(10판), ◀ ▶ 는 한 판씩 이동
        if (UI.Button(new Rect(left.x + 15, left.y + 95, 56, 70), "◀◀", arrow, 20, selectedStage > 1))
            selectedStage = Mathf.Max(1, selectedStage - Stages.LevelsPerStage);
        if (UI.Button(new Rect(left.x + 77, left.y + 95, 50, 70), "◀", arrow, 24, selectedStage > 1)) selectedStage--;
        if (UI.Button(new Rect(left.xMax - 127, left.y + 95, 50, 70), "▶", arrow, 24, selectedStage < maxLevel)) selectedStage++;
        if (UI.Button(new Rect(left.xMax - 71, left.y + 95, 56, 70), "▶▶", arrow, 20, selectedStage < maxLevel))
            selectedStage = Mathf.Min(maxLevel, selectedStage + Stages.LevelsPerStage);

        UI.Text(new Rect(cx - 150, left.y + 80, 300, 60), Stages.Label(selectedStage), 48, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(cx - 150, left.y + 138, 300, 34), Stages.StageName(selectedStage), 20, Color.white);
        string info = Stages.HasBoss(selectedStage) ? $"보스: {Stages.BossName(selectedStage)}" : $"웨이브 {Stages.WavesPerLevel}개";
        UI.Text(new Rect(cx - 200, left.y + 180, 400, 30), info, 20,
            Stages.HasBoss(selectedStage) ? new Color(1f, 0.45f, 0.45f) : new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, true);
        bool firstTime = selectedStage > SaveData.ClearedStage;
        int reward = Stages.ClearReward(selectedStage) * (firstTime ? 2 : 1);
        UI.Text(new Rect(cx - 200, left.y + 212, 400, 30), $"클리어 보상: {reward} 골드" + (firstTime ? " (최초 2배)" : ""), 18, new Color(1f, 0.85f, 0.3f));

        if (UI.Button(new Rect(cx - 140, left.y + 260, 280, 90), "출격!", new Color(0.8f, 0.35f, 0.2f), 34))
            StartBattle(selectedStage);
        UI.Text(new Rect(left.x, left.yMax - 75, left.width, 26), $"파티 {SaveData.Party.Count}명 편성됨", 18, Color.white);
        UI.Text(new Rect(left.x, left.yMax - 45, left.width, 30), $"진행도 {SaveData.ClearedStage} / {Stages.Count}판", 18, new Color(1f, 1f, 1f, 0.7f));

        // 오른쪽: 소환 / 편성 / 도감
        var right = new Rect(w * 0.5f + 20, 90, w * 0.5f - 60, 440);
        float pulse = 1f + 0.05f * Mathf.Sin(Now * 3f);
        UI.Glow(new Vector2(right.center.x, right.y + 90), 190f * pulse, new Color(1f, 0.75f, 0.2f, 0.35f));
        if (UI.Button(new Rect(right.x + 30, right.y + 20, right.width - 60, 140), "소환", new Color(0.85f, 0.55f, 0.15f), 44))
            page = Page.GachaSelect;
        UI.Text(new Rect(right.x, right.y + 165, right.width, 26), "골드로 새로운 동료를 뽑아요", 18, new Color(1f, 1f, 1f, 0.8f));

        int unplaced = 0;
        for (int i = 0; i < SaveData.Roster.Count; i++) if (!SaveData.IsPlaced(i)) unplaced++;
        float half = (right.width - 60 - 20) / 2f;
        string formLabel = unplaced > 0 ? $"편성\n(대기 {unplaced}명)" : "편성";
        if (UI.Button(new Rect(right.x + 30, right.y + 210, half, 110), formLabel, new Color(0.25f, 0.55f, 0.45f), 28))
        {
            formation.Open();
            page = Page.Formation;
        }
        if (UI.Button(new Rect(right.x + 50 + half, right.y + 210, half, 110), "도감", new Color(0.3f, 0.45f, 0.7f), 28))
            page = Page.Roster;
        UI.Text(new Rect(right.x, right.y + 330, right.width, 26), $"용사 각성 +{SaveData.HeroAwaken}  ·  동료 {SaveData.Roster.Count}명", 18, new Color(1f, 0.85f, 0.3f));

        // 아래쪽 작은 버튼들
        var small = new Color(0.3f, 0.3f, 0.4f);
        if (UI.Button(new Rect(right.x + 30, right.yMax - 50, 120, 44), "저장", small, 18)) OpenSaveLoad(false, Page.Camp);
        if (UI.Button(new Rect(right.x + 160, right.yMax - 50, 120, 44), "설정", small, 18))
        {
            returnPage = Page.Camp;
            page = Page.Settings;
        }
        if (UI.Button(new Rect(right.xMax - 150, right.yMax - 50, 120, 44), "타이틀로", small, 18))
        {
            SaveData.Save();
            page = Page.Title;
        }
    }

    void DrawTopBar(float w, string title)
    {
        UI.Fill(new Rect(0, 0, w, 64), new Color(0f, 0f, 0f, 0.5f));
        UI.Text(new Rect(24, 0, 700, 64), title, 28, Color.white, TextAnchor.MiddleLeft, true);
        var goldBox = new Rect(w - 260, 12, 236, 40);
        UI.Fill(goldBox, new Color(0f, 0f, 0f, 0.5f));
        UI.Circle(new Vector2(goldBox.x + 22, goldBox.center.y), 12f, new Color(1f, 0.8f, 0.2f));
        UI.Text(new Rect(goldBox.x + 40, goldBox.y, goldBox.width - 52, goldBox.height), $"{SaveData.Gold:N0} 골드", 22,
            new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleRight, true);
    }

    // ---------------- 소환 선택 ----------------

    void DrawGachaSelect()
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.10f, 0.05f, 0.20f), new Color(0.25f, 0.10f, 0.25f));
        DrawTopBar(w, "소환");

        var banners = GachaBanner.All;
        const float gap = 20f;
        float pw = (w - 80f - gap * (banners.Length - 1)) / banners.Length;
        for (int i = 0; i < banners.Length; i++)
        {
            var b = banners[i];
            var r = new Rect(40 + i * (pw + gap), 90, pw, 520);
            UI.Panel(r, new Color(0f, 0f, 0f, 0.5f));
            UI.Fill(new Rect(r.x, r.y, r.width, 90), b.color);
            UI.Text(new Rect(r.x, r.y + 8, r.width, 44), b.name, 30, Color.white, TextAnchor.MiddleCenter, true);
            UI.Text(new Rect(r.x + 8, r.y + 50, r.width - 16, 30), b.desc, 15, new Color(1f, 1f, 1f, 0.85f));

            // 확률표
            float y = r.y + 110;
            UI.Text(new Rect(r.x, y, r.width, 26), "등장 확률", 18, new Color(1f, 1f, 1f, 0.7f));
            for (int k = 0; k < b.rates.Length; k++)
            {
                if (b.rates[k] <= 0f) continue;
                y += 30;
                var rarity = (Rarity)k;
                UI.Text(new Rect(r.x + 30, y, r.width - 60, 28), RarityInfo.Name(rarity), 20, RarityInfo.Animated(rarity), TextAnchor.MiddleLeft, true);
                UI.Text(new Rect(r.x + 30, y, r.width - 60, 28), b.rates[k] + "%", 20, Color.white, TextAnchor.MiddleRight);
            }
            UI.Text(new Rect(r.x + 8, r.y + 300, r.width - 16, 30), $"10회 뽑기 시 {RarityInfo.Name(b.guarantee)} 이상 1명 보장", 14,
                new Color(1f, 0.85f, 0.3f));

            if (!b.IsUnlocked)
            {
                UI.Fill(r, new Color(0f, 0f, 0f, 0.7f));
                UI.Text(new Rect(r.x, r.y + 200, r.width, 100), $"잠김\n\n스테이지 {Stages.Label(b.unlockAfterStage)}\n클리어 시 열림", 22, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, true);
                continue;
            }

            if (UI.Button(new Rect(r.x + 15, r.y + 350, r.width - 30, 70), $"1회 뽑기\n{b.cost:N0} 골드", b.color, 19, SaveData.Gold >= b.cost))
                Summon(b, 1, b.cost);
            if (UI.Button(new Rect(r.x + 15, r.y + 435, r.width - 30, 70), $"10회 뽑기\n{b.TenCost:N0} 골드", UI.Darken(b.color, 0.8f), 19, SaveData.Gold >= b.TenCost))
                Summon(b, 10, b.TenCost);
        }

        if (UI.Button(new Rect(40, 640, 160, 56), "◀ 돌아가기", new Color(0.3f, 0.3f, 0.4f), 20))
            page = Page.Camp;
    }

    void Summon(GachaBanner banner, int count, int cost)
    {
        if (SaveData.Gold < cost) return;
        SaveData.Gold -= cost;
        page = Page.Gacha;
        gacha.Start(banner, count, () => page = Page.GachaSelect);
    }

    // ---------------- 동료 목록 (도감) ----------------

    void DrawRoster()
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.10f, 0.12f, 0.22f), new Color(0.05f, 0.05f, 0.10f));
        DrawTopBar(w, $"동료 도감  ({CountUnique()}/{CompanionDef.All.Length} 종류 · 총 {SaveData.Roster.Count}명)");

        // 높은 등급이 먼저 오도록 정렬
        var defs = new List<CompanionDef>(CompanionDef.All);
        defs.Sort((a, b) => b.rarity.CompareTo(a.rarity));

        const float cw = 140f, ch = 203f, gap = 16f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((w - 80f + gap) / (cw + gap)));
        int rows = Mathf.CeilToInt(defs.Count / (float)perRow);
        var view = new Rect(40, 80, w - 60, 545);
        var content = new Rect(0, 0, view.width - 20, rows * (ch + gap) + 20);

        rosterScroll = GUI.BeginScrollView(view, rosterScroll, content);
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            var r = new Rect((i % perRow) * (cw + gap), 12 + (i / perRow) * (ch + gap), cw, ch);
            int owned = SaveData.CountOwned(def);
            if (owned > 0)
            {
                string cardTag = def.IsHero && SaveData.HeroAwaken > 0 ? "각성 +" + SaveData.HeroAwaken : null;
                GachaScreen.DrawCardFace(r, def, cardTag);
                if (owned > 1)
                {
                    var tag = new Rect(r.xMax - 44, r.yMax - 30, 40, 26);
                    UI.Fill(tag, new Color(0f, 0f, 0f, 0.7f));
                    UI.Text(tag, "x" + owned, 16, Color.white, TextAnchor.MiddleCenter, true, false);
                }
            }
            else
            {
                // 아직 못 뽑은 동료는 ??? 로 표시
                UI.Fill(r, new Color(0.15f, 0.15f, 0.2f));
                UI.Frame(r, UI.Darken(RarityInfo.GetColor(def.rarity), 0.6f), 3f);
                UI.Text(r, "?", 54, new Color(1f, 1f, 1f, 0.2f), TextAnchor.MiddleCenter, true, false);
                UI.Text(new Rect(r.x, r.yMax - 36, r.width, 30), RarityInfo.Name(def.rarity), 15, UI.Darken(RarityInfo.GetColor(def.rarity), 0.8f));
            }
        }
        GUI.EndScrollView();

        if (UI.Button(new Rect(40, 640, 160, 56), "◀ 돌아가기", new Color(0.3f, 0.3f, 0.4f), 20))
            page = Page.Camp;
    }

    int CountUnique()
    {
        int n = 0;
        foreach (var def in CompanionDef.All) if (SaveData.CountOwned(def) > 0) n++;
        return n;
    }

    // ---------------- 전투 & 결과 ----------------

    void StartBattle(int stage)
    {
        page = Page.Battle;
        battle.Begin(stage, OnBattleEnded);
    }

    void OnBattleEnded(bool victory)
    {
        resultVictory = victory;
        resultStage = battle.Level;
        resultFled = battle.Fled;
        resultKillGold = battle.GoldEarned;
        resultFirstClear = victory && resultStage > SaveData.ClearedStage;
        resultClearGold = victory ? Stages.ClearReward(resultStage) * (resultFirstClear ? 2 : 1) : 0;
        resultUnlock = null;

        if (resultFirstClear)
        {
            SaveData.ClearedStage = resultStage;
            foreach (var b in GachaBanner.All)
                if (b.unlockAfterStage == resultStage) resultUnlock = b.name;
        }
        SaveData.Gold += resultKillGold + resultClearGold;
        SaveData.Save();

        battle.End();
        page = Page.Result;
    }

    void DrawResult()
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, resultVictory ? new Color(0.35f, 0.25f, 0.10f) : new Color(0.20f, 0.08f, 0.10f), new Color(0.05f, 0.05f, 0.08f));
        if (resultVictory) UI.Glow(new Vector2(w / 2f, 160f), 300f, new Color(1f, 0.8f, 0.3f, 0.35f));

        UI.Text(new Rect(0, 100, w, 90), resultVictory ? "승리!" : resultFled ? "도망쳤다!" : "패배...", 72,
            resultVictory ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(0, 190, w, 36), $"{Stages.Label(resultStage)}  ·  {Stages.StageName(resultStage)}", 24, Color.white);

        float y = 260;
        UI.Text(new Rect(0, y, w, 32), $"처치 보상  +{resultKillGold} 골드", 24, new Color(1f, 0.85f, 0.3f));
        if (resultVictory)
        {
            y += 40;
            UI.Text(new Rect(0, y, w, 32), $"클리어 보상  +{resultClearGold} 골드" + (resultFirstClear ? "  (최초 클리어 2배!)" : ""), 24, new Color(1f, 0.85f, 0.3f));
        }
        if (resultUnlock != null)
        {
            y += 55;
            UI.Glow(new Vector2(w / 2f, y + 18), 160f, new Color(1f, 0.5f, 0.2f, 0.4f));
            UI.Text(new Rect(0, y, w, 36), $"새로운 소환 해금: {resultUnlock}!", 28, Color.white, TextAnchor.MiddleCenter, true);
        }
        if (!resultVictory)
        {
            y += 50;
            UI.Text(new Rect(0, y, w, 60), "골드로 동료를 더 뽑거나 편성을 바꿔서 다시 도전해 보세요!", 22, new Color(1f, 1f, 1f, 0.8f));
        }

        bool beatDemonKing = resultVictory && resultFirstClear && resultStage >= Stages.Count;
        float bx = w / 2f;
        if (UI.Button(new Rect(bx - 290, 560, 270, 76), "캠프로", new Color(0.3f, 0.45f, 0.7f), 26))
        {
            selectedStage = Mathf.Min(SaveData.ClearedStage + 1, Stages.Count);
            if (beatDemonKing) PlayStory(EndingStory, true, () => page = Page.Camp);
            else page = Page.Camp;
        }

        bool hasNext = resultVictory && resultStage < Stages.Count;
        string again = hasNext ? "다음 스테이지 ▶" : "다시 도전";
        if (!beatDemonKing && UI.Button(new Rect(bx + 20, 560, 270, 76), again, new Color(0.8f, 0.35f, 0.2f), 26))
            StartBattle(hasNext ? resultStage + 1 : resultStage);
    }
}
