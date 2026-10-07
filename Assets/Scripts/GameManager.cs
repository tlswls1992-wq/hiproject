using System.Collections.Generic;
using UnityEngine;

// 게임 전체 흐름을 관리합니다.
// 타이틀 → 오프닝 → 첫 소환(동료 2명) → 캠프(출격/소환/편성/도감/저장) → 전투 → 결과 → 캠프 ...
// 씬에 따로 넣지 않아도 ▶(플레이)를 누르면 자동으로 생성됩니다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    const float HalfWorldWidth = 14f; // 전투 화면에서 항상 보이는 가로 반폭

    enum Page { Title, Story, Camp, GachaSelect, Gacha, Formation, Members, Dex, Legacy, Battle, Result, SaveLoad, Settings }
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
        new StoryLine(Art.Kingdom,   "",     "- 끝 -\n(야영지에서 계속 플레이하거나, '전승 특전'에서 다음 회차를 시작할 수 있어요)"),
    };

    static readonly float[] TextSpeeds = { 15f, 30f, 60f }; // 대사 속도: 1초에 나오는 글자 수
    static readonly string[] TextSpeedNames = { "느림", "보통", "빠름" };

    Page page = Page.Title;
    Modal modal = Modal.None;
    BattleManager battle;
    readonly GachaScreen gacha = new GachaScreen();
    readonly FormationScreen formation = new FormationScreen();
    readonly MemberScreen members = new MemberScreen();
    readonly DexScreen dex = new DexScreen();
    readonly LegacyScreen legacy = new LegacyScreen();
    Camera cam;

    StoryLine[] story;
    int storyIndex;
    float storyLineStart;
    bool storySkippable;
    System.Action onStoryEnd;

    int selectedStage = 1;   // 고른 판 번호 (1~100)
    bool saveLoadIsLoad;     // 저장/불러오기 화면: true = 불러오기, false = 저장
    Page returnPage;         // 저장/불러오기/설정 화면에서 돌아갈 곳

    // 결과 화면 정보
    bool resultVictory;
    int resultStage;
    int resultKillGold;
    int resultClearGold;
    bool resultFirstClear;
    int resultExp;
    readonly List<string> resultNotes = new List<string>();
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
        FastForwardDialogue();
        // 전투 중 음악은 BattleManager가 정하고, 결과 화면은 승리/패배 소리만. 나머지 화면은 야영지 음악
        if (page != Page.Battle && page != Page.Result) AudioManager.PlayMusic("camp");
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
            case Page.Members: members.Draw(() => page = Page.Camp, ShowToast); break;
            case Page.Dex: dex.Draw(() => page = Page.Camp); break;
            case Page.Legacy: legacy.Draw(() => page = Page.Camp, StartNextCycle, ShowToast); break;
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
        // 노을 진 왕국 배경 + 떠다니는 빛가루
        UI.Backdrop("title");
        for (int i = 0; i < 24; i++)
        {
            float x = Mathf.Repeat(i * 97.3f + Now * 10f, w);
            float y = 560f - Mathf.Repeat(i * 41.7f + Now * (14f + i % 4 * 5f), 520f);
            UI.Glow(new Vector2(x, y), 5f + i % 3, new Color(1f, 0.85f, 0.55f, 0.55f));
        }

        // 가운데: 제목
        float cx = w / 2f;
        UI.Glow(new Vector2(cx, 165f), 300f, new Color(0f, 0f, 0f, 0.35f));
        UI.Text(new Rect(cx - 320, 100, 640, 120), "가챠 용사", 92, UI.Gold, TextAnchor.MiddleCenter, true);
        UI.Fill(new Rect(cx - 200, 222, 400, 1), UI.WithAlpha(UI.Gold, 0.7f));
        UI.Diamond(new Vector2(cx, 222.5f), 8f, UI.Gold);
        UI.Text(new Rect(cx - 320, 232, 640, 36), "마왕을 무찌를 동료는 뽑기로 정한다!", 21, UI.TextMain);

        // 메뉴 (확인 창이 떠 있을 때는 눌리지 않게 막습니다)
        bool active = modal == Modal.None;
        float bx = cx - 150f, by = 300f;
        const float bh = 56f, gap = 12f;
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
        UI.Panel(box, UI.WithAlpha(UI.PanelColor, 0.98f));
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
        UI.Backdrop("menu");
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
        UI.Backdrop("menu");
        UI.TopBar("설정", false);
        UI.Panel(new Rect(w / 2f - 420f, 86f, 840f, 530f));

        float x = w / 2f - 380f, y = 106f;
        var on = UI.Primary;
        var off = UI.Neutral;

        UI.Text(new Rect(x, y, 260, 56), "대사 속도", 24, Color.white, TextAnchor.MiddleLeft, true);
        for (int i = 0; i < TextSpeedNames.Length; i++)
            if (UI.Button(new Rect(x + 280 + i * 160, y, 145, 56), TextSpeedNames[i], SaveData.TextSpeed == i ? on : off, 22))
                SaveData.TextSpeed = i;

        y += 76;
        UI.Text(new Rect(x, y, 260, 56), "전투 시작 속도", 24, Color.white, TextAnchor.MiddleLeft, true);
        for (int sp = 1; sp <= 4; sp++)
            if (UI.Button(new Rect(x + 280 + (sp - 1) * 118, y, 106, 56), "x" + sp, SaveData.BattleSpeed == sp ? on : off, 22))
                SaveData.BattleSpeed = sp;

        y += 76;
        string[] volumeNames = { "끔", "25%", "50%", "75%", "100%" };
        UI.Text(new Rect(x, y, 260, 56), "배경음악", 24, Color.white, TextAnchor.MiddleLeft, true);
        for (int v = 0; v <= SaveData.VolumeSteps; v++)
            if (UI.Button(new Rect(x + 280 + v * 94, y, 86, 56), volumeNames[v], SaveData.MusicLevel == v ? on : off, 19))
                SaveData.MusicLevel = v;

        y += 76;
        UI.Text(new Rect(x, y, 260, 56), "효과음", 24, Color.white, TextAnchor.MiddleLeft, true);
        for (int v = 0; v <= SaveData.VolumeSteps; v++)
            if (UI.Button(new Rect(x + 280 + v * 94, y, 86, 56), volumeNames[v], SaveData.SfxLevel == v ? on : off, 19))
                SaveData.SfxLevel = v;

        y += 76;
        UI.Text(new Rect(x, y, 260, 56), "화면", 24, Color.white, TextAnchor.MiddleLeft, true);
        if (UI.Button(new Rect(x + 280, y, 145, 56), "창 모드", !Screen.fullScreen ? on : off, 22)) Screen.fullScreen = false;
        if (UI.Button(new Rect(x + 440, y, 145, 56), "전체 화면", Screen.fullScreen ? on : off, 22)) Screen.fullScreen = true;

        y += 76;
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
        UI.Panel(box, UI.WithAlpha(UI.PanelColor, 0.94f));
        // 용사가 말할 때는 대화 상자 왼쪽 위에 초상화
        float textX = box.x + 40;
        var heroArt = line.speaker == "용사" ? CharacterArt.For(CompanionDef.HeroId) : null;
        if (heroArt != null && heroArt.portrait != null)
        {
            var face = new Rect(box.x + 20, box.y - 120, 200, 216);
            UI.Round(new Rect(face.x - 3, face.y - 3, face.width + 6, face.height + 6), UI.Gold);
            CharacterArt.DrawTexture(face, heroArt.portrait, true);
            textX = face.xMax + 24;
        }
        if (line.speaker != "")
            UI.Chip(new Rect(textX - 16, box.y - 20, 150, 40), line.speaker, UI.Darken(UI.Primary, 0.9f), 20);

        // 한 글자씩 나타나는 효과
        UI.Text(new Rect(textX, box.y + 38, box.xMax - textX - 60, box.height - 60), line.text.Substring(0, shown), 25,
            UI.TextMain, TextAnchor.UpperLeft);
        if (!talking && Mathf.Repeat(Now, 1f) < 0.6f)
            UI.Text(new Rect(box.xMax - 80, box.yMax - 50, 50, 40), "▼", 22, Color.white);

        // 오른쪽 위 건너뛰기 (오프닝은 본 적이 있을 때만)
        if (storySkippable && UI.Button(new Rect(w - 176, 20, 150, 44), "건너뛰기 ▶▶", UI.WithAlpha(UI.Neutral, 0.9f), 17))
        {
            onStoryEnd?.Invoke();
            return;
        }

        UI.Text(new Rect(box.x + 40, box.yMax - 30, 400, 22), "클릭: 다음   ·   왼쪽 Ctrl: 빨리 감기", 13, UI.WithAlpha(UI.TextSub, 0.8f), TextAnchor.MiddleLeft);
        if (UI.ClickedAnywhere()) AdvanceStory();
    }

    // 다음 대사로 (글자가 다 안 나왔으면 먼저 다 보여 줌)
    void AdvanceStory()
    {
        var line = story[storyIndex];
        bool talking = Mathf.FloorToInt((Now - storyLineStart) * TextSpeeds[Mathf.Clamp(SaveData.TextSpeed, 0, 2)]) < line.text.Length;
        if (talking) storyLineStart = -1000f; // 글자를 한 번에 다 보여 줌
        else if (storyIndex + 1 < story.Length)
        {
            storyIndex++;
            storyLineStart = Now;
        }
        else onStoryEnd?.Invoke();
    }

    // 야영지 대화의 다음 대사 (마지막 대사 뒤에는 닫힘)
    void AdvanceTalk()
    {
        if (talkMember == null) return;
        var lines = CampTalk.LinesOf(talkMember.Def);
        string line = lines[talkIndex % lines.Length];
        int shown = Mathf.FloorToInt((Now - talkStart) * TextSpeeds[Mathf.Clamp(SaveData.TextSpeed, 0, 2)]);
        if (shown < line.Length) talkStart = -1000f;
        else if (talkIndex + 1 < lines.Length) { talkIndex++; talkStart = Now; }
        else talkMember = null;
    }

    // 왼쪽 Ctrl을 누르고 있으면 대사를 빠르게 넘김 (0.12초마다 한 줄)
    float nextFastForward;

    void FastForwardDialogue()
    {
        if (!GameInput.FastForwardHeld() || Now < nextFastForward) return;
        if (page == Page.Story && story != null)
        {
            nextFastForward = Now + 0.12f;
            storyLineStart = -1000f;
            AdvanceStory();
        }
        else if (page == Page.Camp && talkMember != null)
        {
            nextFastForward = Now + 0.12f;
            talkStart = -1000f;
            AdvanceTalk();
        }
    }

    // ---------------- 야영지 (메인 화면) ----------------
    // 모닥불 주위에 출진 중인 동료들이 둘러앉아 있고, 누르면 말을 걸 수 있어요.

    SaveData.Member talkMember;  // 지금 이야기 중인 동료 (없으면 null)
    int talkIndex;
    float talkStart;

    void DrawCamp()
    {
        float w = UI.Width;
        var e = Event.current;

        // 밤 숲속 공터 배경 + 반짝이는 별 몇 개
        UI.Backdrop("camp");
        for (int i = 0; i < 14; i++)
        {
            float x = Mathf.Repeat(i * 137.5f, w);
            float y = Mathf.Repeat(i * 47.3f, 220f) + 70f;
            float a = 0.15f + 0.6f * Mathf.Abs(Mathf.Sin(Now * (0.4f + i % 5 * 0.25f) + i));
            UI.Glow(new Vector2(x, y), 6f, new Color(1f, 0.97f, 0.85f, a));
        }

        UI.TopBar($"야영지   ·   {SaveData.Cycle}회차");

        // ---- 가운데: 모닥불과 둘러앉은 동료 ----
        float sceneL = 350f, sceneR = w - 270f;
        var fire = new Vector2((sceneL + sceneR) / 2f, 470f);
        var sitters = new List<SaveData.Member>();
        foreach (var p in SaveData.Party)
        {
            var m = SaveData.MemberByUid(p.uid);
            if (m != null && sitters.Count < 12) sitters.Add(m);
        }

        float rx = Mathf.Min(240f, (sceneR - sceneL) / 2f - 50f), ry = 100f;
        var seats = new List<KeyValuePair<SaveData.Member, Vector2>>();
        for (int i = 0; i < sitters.Count; i++)
        {
            float ang = (-90f + 360f * i / sitters.Count) * Mathf.Deg2Rad;
            seats.Add(new KeyValuePair<SaveData.Member, Vector2>(sitters[i], fire + new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry)));
        }
        seats.Sort((a, b) => a.Value.y.CompareTo(b.Value.y)); // 뒤쪽(위)부터 그림

        SaveData.Member clickedSitter = null;
        bool fireDrawn = false;
        foreach (var seat in seats)
        {
            if (!fireDrawn && seat.Value.y > fire.y) { DrawCampfire(fire); fireDrawn = true; }
            var pos = seat.Value;
            var def = seat.Key.Def;
            float scale = Mathf.Lerp(0.85f, 1.1f, (pos.y - (fire.y - ry)) / (2f * ry)); // 앞쪽일수록 크게
            float radius = 24f * scale;
            var hit = new Rect(pos.x - radius - 6, pos.y - radius - 6, radius * 2 + 12, radius * 2 + 30);
            bool hover = hit.Contains(e.mousePosition);
            if (hover || seat.Key == talkMember) UI.Glow(pos, radius * 2.2f, new Color(1f, 0.85f, 0.4f, 0.45f));
            UI.Glow(pos + new Vector2(0, radius * 0.9f), radius * 1.4f, new Color(0f, 0f, 0f, 0.35f)); // 그림자
            var art = CharacterArt.For(def.id);
            if (art != null && art.idle.Length > 0)
            {
                // 대기 그림 (발이 자리 위치에 오도록, 모닥불 쪽을 바라봄, 캐릭터마다 숨 쉬는 박자가 조금씩 다름)
                float h = 150f * scale;
                var feet = new Vector2(pos.x, pos.y + radius);
                art.DrawStanding(feet, h, Now + pos.x * 0.37f, pos.x > fire.x);
                var pic = new Rect(pos.x - h / 2f, feet.y - h * (1f - CharacterArt.FeetPivot), h, h);
                hit = new Rect(pos.x - h * 0.3f, pic.y + h * 0.1f, h * 0.6f, h * 0.9f);
                hover = hit.Contains(e.mousePosition);
            }
            else Portrait.Draw(pos + new Vector2(0, Mathf.Sin(Now * 2f + pos.x) * 1.5f), def, radius, false);
            UI.Text(new Rect(pos.x - 60, pos.y + radius + 4, 120, 20), def.name, 13, hover ? UI.Gold : UI.TextMain, TextAnchor.MiddleCenter, true);
            if (GUI.Button(hit, GUIContent.none, GUIStyle.none)) clickedSitter = seat.Key;
        }
        if (!fireDrawn) DrawCampfire(fire);

        if (clickedSitter != null)
        {
            talkMember = clickedSitter;
            talkIndex = 0;
            talkStart = Now;
        }

        // ---- 왼쪽: 출격 ----
        var left = new Rect(20, 80, 316, 540);
        UI.Panel(left);
        UI.Text(new Rect(left.x + 20, left.y + 12, left.width - 40, 28), "출격", 20, UI.TextSub, TextAnchor.MiddleLeft, true);

        int maxLevel = Mathf.Min(SaveData.ClearedStage + 1, Stages.Count);
        selectedStage = Mathf.Clamp(selectedStage, 1, maxLevel);
        float cx = left.center.x;
        float ay = left.y + 52;
        // ◀◀ ▶▶ 는 스테이지 단위(10라운드), ◀ ▶ 는 한 라운드씩 이동
        if (UI.Button(new Rect(left.x + 14, ay, 46, 56), "◀◀", UI.Neutral, 15, selectedStage > 1))
            selectedStage = Mathf.Max(1, selectedStage - Stages.LevelsPerStage);
        if (UI.Button(new Rect(left.x + 64, ay, 40, 56), "◀", UI.Neutral, 18, selectedStage > 1)) selectedStage--;
        if (UI.Button(new Rect(left.xMax - 104, ay, 40, 56), "▶", UI.Neutral, 18, selectedStage < maxLevel)) selectedStage++;
        if (UI.Button(new Rect(left.xMax - 60, ay, 46, 56), "▶▶", UI.Neutral, 15, selectedStage < maxLevel))
            selectedStage = Mathf.Min(maxLevel, selectedStage + Stages.LevelsPerStage);
        UI.Text(new Rect(cx - 52, ay - 4, 104, 64), Stages.Label(selectedStage), 40, UI.Gold, TextAnchor.MiddleCenter, true);

        UI.Text(new Rect(left.x + 12, left.y + 120, left.width - 24, 48), Stages.StageName(selectedStage), 17, UI.TextMain);
        string roundInfo = Stages.HasBoss(selectedStage) ? $"스테이지 보스  {Stages.BossName(selectedStage)}"
            : Stages.HasMidBoss(selectedStage) ? $"중간 보스  {Stages.MidBossName(selectedStage)}"
            : $"라운드 {Stages.SubOf(selectedStage)}  ·  웨이브 {Stages.WavesPerLevel}개";
        UI.Chip(new Rect(left.x + 20, left.y + 174, left.width - 40, 30), roundInfo,
            Stages.HasBoss(selectedStage) ? UI.Red : Stages.HasMidBoss(selectedStage) ? UI.Plum : UI.Neutral, 14);
        bool firstTime = selectedStage > SaveData.ClearedStage;
        int reward = Stages.ClearReward(selectedStage) * (firstTime ? 2 : 1);
        UI.Text(new Rect(left.x, left.y + 212, left.width, 24), $"클리어 보상 {reward:N0} 골드" + (firstTime ? " (최초 2배)" : ""), 15, UI.Gold);

        if (UI.Button(new Rect(left.x + 24, left.y + 250, left.width - 48, 80), "출격!", UI.Primary, 32))
            StartBattle(selectedStage);

        UI.Text(new Rect(left.x + 20, left.y + 350, left.width - 40, 24), $"출진 인원  {SaveData.Party.Count} / {SaveData.DeployCap}명", 16, UI.TextMain, TextAnchor.MiddleLeft, true);
        UI.Text(new Rect(left.x + 20, left.y + 376, left.width - 40, 40), "스테이지를 클리어할 때마다 출진 인원이 1명 늘어요", 13, UI.TextSub, TextAnchor.UpperLeft);
        UI.Text(new Rect(left.x + 20, left.y + 430, left.width - 40, 24), $"진행도  {SaveData.ClearedStage} / {Stages.Count}", 15, UI.TextSub, TextAnchor.MiddleLeft);
        UI.Bar(new Rect(left.x + 20, left.y + 458, left.width - 40, 14), SaveData.ClearedStage / (float)Stages.Count, UI.Gold);
        UI.Text(new Rect(left.x + 20, left.y + 484, left.width - 40, 40), $"용사 {FormationScreen.Stars(SaveData.HeroMember.star)}  ·  동료 {SaveData.CompanionCount}명", 14, UI.TextSub, TextAnchor.MiddleLeft);

        // ---- 오른쪽: 메뉴 ----
        float mx = w - 250f, mw = 230f, my = 80f;
        if (UI.Button(new Rect(mx, my, mw, 70), "소환", UI.Primary, 28)) page = Page.GachaSelect;
        my += 80;
        if (UI.Button(new Rect(mx, my, mw, 54), "편성", UI.Green, 20)) { formation.Open(); page = Page.Formation; }
        my += 64;
        if (UI.Button(new Rect(mx, my, mw, 54), "동료  (강화 · 판매)", UI.Plum, 18)) { members.Open(); page = Page.Members; }
        my += 64;
        if (UI.Button(new Rect(mx, my, mw, 54), "도감", UI.Blue, 20)) { dex.Open(); page = Page.Dex; }
        my += 64;
        if (SaveData.RunCleared)
        {
            if (UI.Button(new Rect(mx, my, mw, 54), "전승 특전", new Color(0.55f, 0.24f, 0.30f), 20)) { legacy.Open(); page = Page.Legacy; }
            my += 64;
        }
        float half = (mw - 10f) / 2f;
        if (UI.Button(new Rect(mx, my, half, 48), "저장", UI.Neutral, 17)) OpenSaveLoad(false, Page.Camp);
        if (UI.Button(new Rect(mx + half + 10, my, half, 48), "설정", UI.Neutral, 17)) { returnPage = Page.Camp; page = Page.Settings; }
        my += 58;
        if (UI.Button(new Rect(mx, my, mw, 48), "타이틀로", UI.Neutral, 17))
        {
            SaveData.Save();
            talkMember = null;
            page = Page.Title;
        }

        // ---- 대화 상자 ----
        if (talkMember != null && SaveData.Roster.Contains(talkMember))
        {
            var lines = CampTalk.LinesOf(talkMember.Def);
            string line = lines[talkIndex % lines.Length];
            int shown = Mathf.Min(line.Length, Mathf.FloorToInt((Now - talkStart) * TextSpeeds[Mathf.Clamp(SaveData.TextSpeed, 0, 2)]));
            var box = new Rect(sceneL, 600, sceneR - sceneL, 100);
            UI.Panel(box, UI.WithAlpha(UI.PanelColor, 0.95f));
            float textX = box.x + 24;
            var talkArt = CharacterArt.For(talkMember.Def.id);
            if (talkArt != null && talkArt.portrait != null)
            {
                // 대화 상자 왼쪽 위로 초상화
                var face = new Rect(box.x + 14, box.y - 92, 170, 184);
                UI.Round(new Rect(face.x - 3, face.y - 3, face.width + 6, face.height + 6), RarityInfo.Animated(talkMember.Def.rarity));
                CharacterArt.DrawTexture(face, talkArt.portrait, true);
                textX = face.xMax + 18;
            }
            UI.Chip(new Rect(textX - 4, box.y - 16, 150, 32), talkMember.Def.name, UI.Darken(UI.Primary, 0.9f), 16);
            UI.Text(new Rect(textX, box.y + 26, box.xMax - textX - 60, box.height - 34), line.Substring(0, shown), 19, UI.TextMain, TextAnchor.UpperLeft);
            if (UI.Button(new Rect(box.xMax - 44, box.y + 8, 34, 30), "X", UI.Neutral, 14)) talkMember = null;
            else if (UI.ClickedAnywhere()) AdvanceTalk(); // 아무 곳이나 누르면 다음 대사
        }
        else
        {
            talkMember = null;
            UI.Text(new Rect(sceneL, 630, sceneR - sceneL, 40),
                sitters.Count > 1 ? "모닥불 주위의 동료를 눌러서 말을 걸어 보세요" : "동료를 뽑고 편성하면 야영지에 함께 앉아요", 15, UI.TextSub);
        }
    }

    static void DrawCampfire(Vector2 fire)
    {
        UI.Glow(fire, 170f + 10f * Mathf.Sin(Now * 7f), new Color(1f, 0.5f, 0.15f, 0.35f));
        UI.Fill(new Rect(fire.x - 34, fire.y + 6, 68, 10), new Color(0.35f, 0.22f, 0.12f));
        UI.Fill(new Rect(fire.x - 26, fire.y + 12, 52, 8), new Color(0.28f, 0.18f, 0.10f));
        for (int i = 0; i < 3; i++)
        {
            float flick = Mathf.Sin(Now * (9f + i * 3f) + i) * 4f;
            UI.Glow(new Vector2(fire.x + (i - 1) * 10f, fire.y - 10f - i * 6f + flick), 22f - i * 4f, new Color(1f, 0.7f - i * 0.15f, 0.2f, 0.95f));
        }
        UI.Glow(new Vector2(fire.x, fire.y - 6f), 12f, new Color(1f, 0.95f, 0.6f, 1f));
    }

    // ---------------- 소환 선택 ----------------

    void DrawGachaSelect()
    {
        float w = UI.Width;
        UI.Backdrop("menu");
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
        UI.Text(new Rect(220, 636, w - 260, 50), "같은 동료가 또 나오면 동료 화면에서 합쳐 성급을 올릴 수 있어요 · 용사 카드가 나오면 용사의 성급이 올라가요", 15, UI.TextSub, TextAnchor.MiddleLeft);
    }

    void Summon(GachaBanner banner, int count, int cost)
    {
        if (SaveData.Gold < cost) return;
        SaveData.Gold -= cost;
        page = Page.Gacha;
        gacha.Start(banner, count, () => page = Page.GachaSelect);
    }

    // ---------------- 전투 & 결과 ----------------

    // 맵 입장 멘트와 보스 대사는 전투 화면 위에 사념파처럼 나와요 (BattleManager의 '사념파 대사')
    void StartBattle(int level)
    {
        talkMember = null;
        BeginBattle(level);
    }

    void BeginBattle(int level)
    {
        page = Page.Battle;
        battle.Begin(level, OnBattleEnded);
    }

    void OnBattleEnded(bool victory)
    {
        resultVictory = victory;
        resultStage = battle.Level;
        resultKillGold = battle.GoldEarned;
        resultFirstClear = victory && resultStage > SaveData.ClearedStage;
        resultClearGold = victory ? Stages.ClearReward(resultStage) * (resultFirstClear ? 2 : 1) : 0;
        resultUnlock = null;
        resultNotes.Clear();

        int oldCap = SaveData.DeployCap;
        if (resultFirstClear)
        {
            SaveData.ClearedStage = resultStage;
            foreach (var b in GachaBanner.All)
                if (b.unlockAfterStage == resultStage) resultUnlock = b.name;
        }
        if (victory && Stages.HasBoss(resultStage)) SaveData.MarkBossBeaten(Stages.StageOf(resultStage));
        if (SaveData.DeployCap > oldCap) resultNotes.Add($"출진 가능 인원 +1  (이제 {SaveData.DeployCap}명)");

        // 출진한 동료 모두 경험치 획득 (이기면 많이, 져도 조금)
        resultExp = victory ? 30 + resultStage * 4 : 10 + resultStage * 2;
        var levelUps = new List<string>();
        foreach (int uid in battle.DeployedUids)
        {
            var m = SaveData.MemberByUid(uid);
            if (m != null && SaveData.GainExp(m, resultExp) > 0) levelUps.Add($"{m.Def.name} Lv.{m.level}");
        }
        if (levelUps.Count > 0)
        {
            string shown = string.Join(", ", levelUps.GetRange(0, Mathf.Min(5, levelUps.Count)));
            resultNotes.Add("레벨 업!  " + shown + (levelUps.Count > 5 ? $" 외 {levelUps.Count - 5}명" : ""));
        }

        SaveData.Gold += resultKillGold + resultClearGold;
        SaveData.Save();

        battle.End();
        page = Page.Result;
    }

    void DrawResult()
    {
        float w = UI.Width;
        UI.Backdrop(resultVictory ? "title" : "menu", resultVictory ? 0.45f : 0.2f);
        if (resultVictory) UI.Glow(new Vector2(w / 2f, 150f), 300f, new Color(1f, 0.8f, 0.3f, 0.35f));

        UI.Text(new Rect(0, 80, w, 90), resultVictory ? "승리!" : "패배...", 72,
            resultVictory ? UI.Gold : new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(0, 170, w, 34), $"{Stages.Label(resultStage)}  ·  {Stages.StageName(resultStage)}", 22, UI.TextMain);

        var box = new Rect(w / 2f - 330f, 220f, 660f, 300f);
        UI.Panel(box);
        float y = box.y + 20;
        UI.Text(new Rect(box.x, y, box.width, 30), $"처치 보상  +{resultKillGold:N0} 골드", 21, UI.Gold);
        if (resultVictory)
        {
            y += 36;
            UI.Text(new Rect(box.x, y, box.width, 30), $"클리어 보상  +{resultClearGold:N0} 골드" + (resultFirstClear ? "  (최초 2배!)" : ""), 21, UI.Gold);
        }
        y += 36;
        UI.Text(new Rect(box.x, y, box.width, 30), $"출진한 동료 경험치  +{resultExp}", 19, new Color(0.5f, 0.85f, 1f));
        foreach (var note in resultNotes)
        {
            y += 34;
            UI.Text(new Rect(box.x + 20, y, box.width - 40, 30), note, 17, UI.TextMain);
        }
        if (resultUnlock != null)
        {
            y += 40;
            UI.Text(new Rect(box.x, y, box.width, 32), $"새로운 소환 해금: {resultUnlock}!", 22, Color.white, TextAnchor.MiddleCenter, true);
        }
        if (!resultVictory)
        {
            y += 40;
            UI.Text(new Rect(box.x + 20, y, box.width - 40, 50), "동료를 더 뽑거나 성급 강화, 편성을 바꿔서 다시 도전해 보세요!", 17, UI.TextSub);
        }

        bool beatDemonKing = resultVictory && resultFirstClear && resultStage >= Stages.Count;
        float bx = w / 2f;
        if (beatDemonKing)
        {
            if (UI.Button(new Rect(bx - 150, 556, 300, 72), "엔딩 보기 ▶", UI.Primary, 24))
                PlayStory(EndingStory, true, () =>
                {
                    SaveData.RunCleared = true; // 전승 특전 열림
                    SaveData.Save();
                    selectedStage = Stages.Count;
                    page = Page.Camp;
                    ShowToast($"{SaveData.Cycle}회차 클리어! 야영지에서 '전승 특전'을 열 수 있어요");
                });
            return;
        }

        // 스테이지 보스를 이기면 '스테이지 엔딩 선택지'로 다음 목적지를 고릅니다.
        var choices = Stages.Script(resultStage).endingChoices;
        if (resultVictory && Stages.HasBoss(resultStage) && choices.Length > 0)
        {
            UI.Text(new Rect(0, 528, w, 30), "어디로 갈까?", 19, UI.Gold, TextAnchor.MiddleCenter, true);
            float cy = 566f - (choices.Length - 1) * 70f;
            for (int i = 0; i < choices.Length; i++)
            {
                var c = choices[i];
                var r = new Rect(bx - 470, cy + i * 70f, 940, 62);
                if (UI.Button(r, "", UI.Primary, 18))
                {
                    selectedStage = Mathf.Min(Mathf.Min((c.toStage - 1) * Stages.LevelsPerStage + 1, SaveData.ClearedStage + 1), Stages.Count);
                    page = Page.Camp;
                    ShowToast($"다음 목적지: {Stages.StageTitle(c.toStage)}");
                }
                UI.Text(new Rect(r.x + 24, r.y, r.width - 220, r.height - 4), $"{i + 1}. {c.text}", 18, UI.TextMain, TextAnchor.MiddleLeft, true);
                UI.Text(new Rect(r.xMax - 210, r.y, 190, r.height - 4), "→ " + Stages.StageTitle(c.toStage), 16, UI.Gold, TextAnchor.MiddleRight, true);
            }
            return;
        }

        // 스테이지 보스전(10라운드)이 끝나면 이기든 지든 무조건 야영지로 돌아갑니다.
        bool bossFight = Stages.HasBoss(resultStage);
        var campRect = bossFight ? new Rect(bx - 150, 556, 300, 72) : new Rect(bx - 290, 556, 270, 72);
        if (UI.Button(campRect, "야영지로", UI.Blue, 24))
        {
            selectedStage = Mathf.Min(SaveData.ClearedStage + 1, Stages.Count);
            page = Page.Camp;
        }
        if (bossFight) return;
        bool hasNext = resultVictory && resultStage < Stages.Count;
        if (UI.Button(new Rect(bx + 20, 556, 270, 72), hasNext ? "다음 라운드 ▶" : "다시 도전", UI.Primary, 24))
            StartBattle(hasNext ? resultStage + 1 : resultStage);
    }

    // ---------------- 회차 ----------------

    void StartNextCycle()
    {
        SaveData.StartNewCycle();
        selectedStage = 1;
        talkMember = null;
        ShowToast($"{SaveData.Cycle}회차 시작!");
        PlayStory(OpeningStory, true, StartFirstSummon);
    }
}
