using System.Collections.Generic;
using UnityEngine;

// 게임 전체 흐름을 관리합니다.
// 타이틀 → 오프닝 → 첫 소환(동료 2명) → 캠프(출격/소환/편성/도감/저장) → 전투 → 결과 → 캠프 ...
// 씬에 따로 넣지 않아도 ▶(플레이)를 누르면 자동으로 생성됩니다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    const float HalfWorldWidth = 14f; // 전투 화면에서 항상 보이는 가로 반폭

    enum Page { Title, Story, Camp, GachaSelect, Gacha, Formation, Enhance, Talk, Roster, Battle, Result, SaveLoad, Settings }
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
    readonly EnhanceScreen enhance = new EnhanceScreen();
    readonly TalkScreen talk = new TalkScreen();
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
            case Page.Enhance: enhance.Draw(() => page = Page.Camp, ShowToast); break;
            case Page.Talk: talk.Draw(() => page = Page.Camp, TextSpeeds[Mathf.Clamp(SaveData.TextSpeed, 0, 2)]); break;
            case Page.Roster: DrawRoster(); break;
            case Page.Battle: battle.DrawHUD(); break;
            case Page.Result: DrawResult(); break;
            case Page.SaveLoad: DrawSaveLoad(); break;
            case Page.Settings: DrawSettings(); break;
        }

        if (toast != null && Now < toastUntil)
        {
            float w = UI.Width;
            var r = new Rect(w / 2f - 320f, 80f, 640f, 48f);
            UI.Round(r, new Color(0.02f, 0.02f, 0.05f, 0.88f));
            UI.RoundFrame(r, UI.WithAlpha(UI.Gold, 0.5f));
            UI.Text(r, toast, 19, UI.TextMain, TextAnchor.MiddleCenter, true);
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
        const float bh = 58f, gap = 14f;
        if (UI.Button(new Rect(bx, by, 300, bh), "새로 시작", UI.Primary, 24, active))
        {
            if (SaveData.HasSave) modal = Modal.ConfirmNewGame;
            else NewGame();
        }
        if (UI.Button(new Rect(bx, by + (bh + gap), 300, bh), "이어하기", UI.Blue, 22, active && SaveData.HasSave))
            ContinueGame();
        if (UI.Button(new Rect(bx, by + (bh + gap) * 2, 300, bh), "불러오기", UI.Blue, 22, active))
            OpenSaveLoad(true, Page.Title);
        if (UI.Button(new Rect(bx, by + (bh + gap) * 3, 300, bh), "설정", UI.Neutral, 22, active))
        {
            returnPage = Page.Title;
            page = Page.Settings;
        }
        if (UI.Button(new Rect(bx, by + (bh + gap) * 4, 300, bh), "끝내기", UI.Neutral, 22, active))
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
        UI.Panel(box, new Color(0.10f, 0.11f, 0.18f, 0.97f));
        UI.Text(new Rect(box.x + 30, box.y + 24, box.width - 60, 120), message, 21, UI.TextMain);
        bool yes = UI.Button(new Rect(box.x + 50, box.y + 170, 210, 60), yesLabel, UI.Red);
        bool no = UI.Button(new Rect(box.xMax - 260, box.y + 170, 210, 60), "취소", UI.Neutral);
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
        UI.TopBar(saveLoadIsLoad ? "불러오기" : "저장하기", false);

        for (int slot = 0; slot < SaveData.SlotCount; slot++)
        {
            var r = new Rect(80, 90 + slot * 124, w - 160, 108);
            bool has = SaveData.HasSlot(slot);
            UI.Panel(r);
            string label = slot == 0 ? "자동 저장 (이어하기)" : $"슬롯 {slot}";
            UI.Text(new Rect(r.x + 28, r.y + 12, 400, 32), label, 22, slot == 0 ? UI.Gold : UI.TextMain, TextAnchor.MiddleLeft, true);
            UI.Text(new Rect(r.x + 28, r.y + 48, r.width - 300, 52), SaveData.SlotSummary(slot), 16,
                has ? UI.TextSub : UI.WithAlpha(UI.TextSub, 0.5f), TextAnchor.UpperLeft);

            var btn = new Rect(r.xMax - 230, r.y + 22, 200, 60);
            if (saveLoadIsLoad)
            {
                if (UI.Button(btn, "불러오기", UI.Blue, 20, has))
                {
                    SaveData.Load(slot);
                    SaveData.Save(); // 불러온 기록으로 이어하기
                    selectedStage = Mathf.Min(SaveData.ClearedStage + 1, Stages.Count);
                    page = Page.Camp;
                    ShowToast(slot == 0 ? "자동 저장을 불러왔어요" : $"슬롯 {slot}을 불러왔어요");
                }
            }
            else if (slot > 0 && UI.Button(btn, has ? "덮어쓰기" : "여기에 저장", UI.Green, 20))
            {
                SaveData.SaveToSlot(slot);
                ShowToast($"슬롯 {slot}에 저장했어요");
            }
        }

        if (UI.Button(new Rect(32, 636, 170, 50), "◀ 돌아가기", UI.Neutral, 18))
            page = returnPage;
    }

    // ---------------- 설정 ----------------

    void DrawSettings()
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.12f, 0.12f, 0.18f), new Color(0.05f, 0.05f, 0.08f));
        UI.TopBar("설정", false);
        UI.Panel(new Rect(w / 2f - 420f, 96f, 840f, 400f));

        float x = w / 2f - 380f, y = 120f;
        var on = UI.Primary;
        var off = UI.Neutral;

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

        if (UI.Button(new Rect(32, 636, 170, 50), "◀ 돌아가기", UI.Neutral, 18))
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
        var box = new Rect(40, 486, w - 80, 196);
        UI.Panel(box, new Color(0.04f, 0.04f, 0.09f, 0.90f));
        if (line.speaker != "")
            UI.Chip(new Rect(box.x + 24, box.y - 20, 150, 40), line.speaker, UI.Darken(UI.Primary, 0.9f), 20);

        // 한 글자씩 나타나는 효과
        UI.Text(new Rect(box.x + 40, box.y + 38, box.width - 100, box.height - 60), line.text.Substring(0, shown), 25,
            UI.TextMain, TextAnchor.UpperLeft);
        if (!talking && Mathf.Repeat(Now, 1f) < 0.6f)
            UI.Text(new Rect(box.xMax - 80, box.yMax - 50, 50, 40), "▼", 22, Color.white);

        // 오른쪽 위 건너뛰기 (오프닝은 본 적이 있을 때만)
        if (storySkippable && UI.Button(new Rect(w - 176, 20, 150, 44), "건너뛰기 ▶▶", UI.WithAlpha(UI.Neutral, 0.9f), 17))
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
        UI.Gradient(UI.Full, new Color(0.12f, 0.15f, 0.28f), new Color(0.24f, 0.17f, 0.15f));
        UI.Fill(new Rect(0, 600, w, 120), new Color(0.16f, 0.22f, 0.15f));
        var fire = new Vector2(w / 2f, 650f);
        UI.Glow(fire, 160f + 10f * Mathf.Sin(Now * 7f), new Color(1f, 0.5f, 0.15f, 0.35f));
        UI.Glow(fire, 26f + 4f * Mathf.Sin(Now * 11f), new Color(1f, 0.85f, 0.4f, 0.9f));

        UI.TopBar("용사의 캠프");

        // ---- 왼쪽: 출격 ----
        var left = new Rect(32, 84, w * 0.46f - 32, 500);
        UI.Panel(left);
        UI.Text(new Rect(left.x + 24, left.y + 14, left.width - 48, 32), "출격", 22, UI.TextSub, TextAnchor.MiddleLeft, true);

        int maxLevel = Mathf.Min(SaveData.ClearedStage + 1, Stages.Count);
        selectedStage = Mathf.Clamp(selectedStage, 1, maxLevel);
        float cx = left.center.x;
        var arrow = UI.Neutral;
        // ◀◀ ▶▶ 는 스테이지 단위(10판), ◀ ▶ 는 한 판씩 이동
        float ay = left.y + 78;
        if (UI.Button(new Rect(left.x + 20, ay, 52, 60), "◀◀", arrow, 18, selectedStage > 1))
            selectedStage = Mathf.Max(1, selectedStage - Stages.LevelsPerStage);
        if (UI.Button(new Rect(left.x + 78, ay, 46, 60), "◀", arrow, 20, selectedStage > 1)) selectedStage--;
        if (UI.Button(new Rect(left.xMax - 124, ay, 46, 60), "▶", arrow, 20, selectedStage < maxLevel)) selectedStage++;
        if (UI.Button(new Rect(left.xMax - 72, ay, 52, 60), "▶▶", arrow, 18, selectedStage < maxLevel))
            selectedStage = Mathf.Min(maxLevel, selectedStage + Stages.LevelsPerStage);
        UI.Text(new Rect(cx - 110, ay - 4, 220, 68), Stages.Label(selectedStage), 52, UI.Gold, TextAnchor.MiddleCenter, true);

        UI.Text(new Rect(left.x, left.y + 150, left.width, 30), Stages.StageName(selectedStage), 21, UI.TextMain);
        bool boss = Stages.HasBoss(selectedStage);
        var chip = new Rect(cx - 110, left.y + 190, 220, 32);
        UI.Chip(chip, boss ? $"보스  {Stages.BossName(selectedStage)}" : $"웨이브 {Stages.WavesPerLevel}개", boss ? UI.Red : UI.Neutral, 16);
        bool firstTime = selectedStage > SaveData.ClearedStage;
        int reward = Stages.ClearReward(selectedStage) * (firstTime ? 2 : 1);
        UI.Text(new Rect(left.x, left.y + 232, left.width, 28), $"클리어 보상  {reward:N0} 골드" + (firstTime ? "  (최초 2배)" : ""), 17, UI.Gold);

        if (UI.Button(new Rect(cx - 150, left.y + 290, 300, 86), "출격!", UI.Primary, 34))
            StartBattle(selectedStage);

        // 진행도 막대
        var prog = new Rect(left.x + 24, left.yMax - 70, left.width - 48, 14);
        UI.Bar(prog, SaveData.ClearedStage / (float)Stages.Count, UI.Gold);
        UI.Text(new Rect(left.x + 24, left.yMax - 50, left.width - 48, 26),
            $"진행도 {SaveData.ClearedStage} / {Stages.Count}판   ·   파티 {SaveData.Party.Count}명", 15, UI.TextSub, TextAnchor.MiddleLeft);

        // ---- 오른쪽: 메뉴 ----
        float rx = left.xMax + 24, rw = w - rx - 32;
        var summon = new Rect(rx, 84, rw, 150);
        UI.Glow(summon.center, 200f * (1f + 0.04f * Mathf.Sin(Now * 3f)), new Color(1f, 0.7f, 0.2f, 0.25f));
        if (Tile(summon, "소환", "골드로 새로운 동료를 뽑아요", UI.Primary, 40)) page = Page.GachaSelect;

        int unplaced = 0;
        for (int i = 0; i < SaveData.Roster.Count; i++) if (!SaveData.IsPlaced(i)) unplaced++;
        float tw = (rw - 16) / 2f, th = 160f, ty = summon.yMax + 16;
        if (Tile(new Rect(rx, ty, tw, th), "편성", unplaced > 0 ? $"대기 중 {unplaced}명" : $"파티 {SaveData.Party.Count}명", UI.Green, 30))
        {
            formation.Open();
            page = Page.Formation;
        }
        if (Tile(new Rect(rx + tw + 16, ty, tw, th), "강화", "골드로 레벨 올리기", new Color(0.50f, 0.36f, 0.72f), 30))
        {
            enhance.Open();
            page = Page.Enhance;
        }
        ty += th + 16;
        if (Tile(new Rect(rx, ty, tw, th), "대화", "동료와 이야기하기", new Color(0.62f, 0.40f, 0.26f), 30))
        {
            talk.Open();
            page = Page.Talk;
        }
        if (Tile(new Rect(rx + tw + 16, ty, tw, th), "도감", $"{CountUnique()} / {CompanionDef.All.Length} 종류", UI.Blue, 30))
            page = Page.Roster;

        // ---- 아래: 상태와 작은 버튼 ----
        UI.Text(new Rect(32, 640, 500, 40), $"용사 Lv.{SaveData.LevelOf(CompanionDef.Hero)}  ·  각성 +{SaveData.HeroAwaken}  ·  동료 {SaveData.Roster.Count}명",
            17, UI.TextSub, TextAnchor.MiddleLeft);
        float bx = w - 32 - 3 * 130 - 2 * 10;
        if (UI.Button(new Rect(bx, 636, 130, 48), "저장", UI.Neutral, 18)) OpenSaveLoad(false, Page.Camp);
        if (UI.Button(new Rect(bx + 140, 636, 130, 48), "설정", UI.Neutral, 18))
        {
            returnPage = Page.Camp;
            page = Page.Settings;
        }
        if (UI.Button(new Rect(bx + 280, 636, 130, 48), "타이틀로", UI.Neutral, 18))
        {
            SaveData.Save();
            page = Page.Title;
        }
    }

    // 큰 메뉴 버튼 (제목 + 작은 설명)
    static bool Tile(Rect r, string title, string sub, Color color, int titleSize)
    {
        bool clicked = UI.Button(r, "", color);
        UI.Text(new Rect(r.x, r.y + r.height * 0.5f - titleSize - 2, r.width, titleSize + 12), title, titleSize, Color.white, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(r.x + 10, r.y + r.height * 0.5f + 14, r.width - 20, 24), sub, 16, new Color(1f, 1f, 1f, 0.8f));
        return clicked;
    }

    // ---------------- 소환 선택 ----------------

    void DrawGachaSelect()
    {
        float w = UI.Width;
        UI.Gradient(UI.Full, new Color(0.09f, 0.05f, 0.18f), new Color(0.20f, 0.08f, 0.22f));
        UI.TopBar("소환");

        var banners = GachaBanner.All;
        const float gap = 18f;
        float pw = (w - 64f - gap * (banners.Length - 1)) / banners.Length;
        for (int i = 0; i < banners.Length; i++)
        {
            var b = banners[i];
            var r = new Rect(32 + i * (pw + gap), 84, pw, 530);
            UI.Panel(r);
            var head = new Rect(r.x + 8, r.y + 8, r.width - 16, 88);
            UI.Round(head, b.color);
            UI.Text(new Rect(head.x, head.y + 10, head.width, 38), b.name, 28, Color.white, TextAnchor.MiddleCenter, true);
            UI.Text(new Rect(head.x + 8, head.y + 50, head.width - 16, 26), b.desc, 14, new Color(1f, 1f, 1f, 0.85f));

            // 확률표
            float y = r.y + 112;
            UI.Text(new Rect(r.x + 24, y, r.width - 48, 24), "등장 확률", 15, UI.TextSub, TextAnchor.MiddleLeft, true);
            for (int k = 0; k < b.rates.Length; k++)
            {
                if (b.rates[k] <= 0f) continue;
                y += 30;
                var rarity = (Rarity)k;
                UI.Round(new Rect(r.x + 16, y, r.width - 32, 26), new Color(1f, 1f, 1f, 0.04f));
                UI.Text(new Rect(r.x + 28, y, r.width - 56, 26), RarityInfo.Name(rarity), 17, RarityInfo.Animated(rarity), TextAnchor.MiddleLeft, true);
                UI.Text(new Rect(r.x + 28, y, r.width - 56, 26), b.rates[k] + "%", 17, UI.TextMain, TextAnchor.MiddleRight);
            }
            UI.Text(new Rect(r.x + 12, r.y + 320, r.width - 24, 40), $"10회 뽑기 시\n{RarityInfo.Name(b.guarantee)} 이상 1명 보장", 14, UI.Gold);

            if (!b.IsUnlocked)
            {
                UI.Round(r, new Color(0f, 0f, 0f, 0.72f));
                UI.Text(new Rect(r.x, r.y + 210, r.width, 40), "잠김", 28, UI.TextMain, TextAnchor.MiddleCenter, true);
                UI.Text(new Rect(r.x, r.y + 255, r.width, 50), $"{Stages.Label(b.unlockAfterStage)} 클리어 시 열림", 17, UI.TextSub);
                continue;
            }

            if (UI.Button(new Rect(r.x + 16, r.y + 378, r.width - 32, 64), $"1회  ·  {b.cost:N0} 골드", b.color, 18, SaveData.Gold >= b.cost))
                Summon(b, 1, b.cost);
            if (UI.Button(new Rect(r.x + 16, r.y + 452, r.width - 32, 64), $"10회  ·  {b.TenCost:N0} 골드", UI.Darken(b.color, 0.8f), 18, SaveData.Gold >= b.TenCost))
                Summon(b, 10, b.TenCost);
        }

        if (UI.Button(new Rect(32, 636, 170, 50), "◀ 돌아가기", UI.Neutral, 18))
            page = Page.Camp;
        UI.Text(new Rect(220, 636, w - 260, 50), "이름이 있는 동료는 한 번만 나와요 · 용사 카드는 각성으로 바뀌어요", 15, UI.TextSub, TextAnchor.MiddleLeft);
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
        UI.TopBar($"도감   {CountUnique()} / {CompanionDef.All.Length} 종류  ·  총 {SaveData.Roster.Count}명");

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
                    UI.Chip(new Rect(r.xMax - 46, r.yMax - 32, 40, 24), "x" + owned, new Color(0f, 0f, 0f, 0.75f), 14);
                }
            }
            else
            {
                // 아직 못 뽑은 동료는 ??? 로 표시
                UI.Round(r, new Color(0.13f, 0.14f, 0.19f));
                UI.RoundFrame(r, UI.Darken(RarityInfo.GetColor(def.rarity), 0.6f));
                UI.Text(r, "?", 54, new Color(1f, 1f, 1f, 0.2f), TextAnchor.MiddleCenter, true);
                UI.Text(new Rect(r.x, r.yMax - 36, r.width, 30), RarityInfo.Name(def.rarity), 15, UI.Darken(RarityInfo.GetColor(def.rarity), 0.8f));
            }
        }
        GUI.EndScrollView();

        if (UI.Button(new Rect(32, 636, 170, 50), "◀ 돌아가기", UI.Neutral, 18))
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
        if (UI.Button(new Rect(bx - 290, 560, 270, 72), "캠프로", UI.Blue, 24))
        {
            selectedStage = Mathf.Min(SaveData.ClearedStage + 1, Stages.Count);
            if (beatDemonKing) PlayStory(EndingStory, true, () => page = Page.Camp);
            else page = Page.Camp;
        }

        bool hasNext = resultVictory && resultStage < Stages.Count;
        string again = hasNext ? "다음 스테이지 ▶" : "다시 도전";
        if (!beatDemonKing && UI.Button(new Rect(bx + 20, 560, 270, 72), again, UI.Primary, 24))
            StartBattle(hasNext ? resultStage + 1 : resultStage);
    }
}
