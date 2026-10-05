using System.Collections.Generic;
using UnityEngine;

// 게임 전체 흐름을 관리합니다.
// 타이틀 → 스토리 → 첫 소환(동료 2명) → 캠프(출격/소환/동료) → 전투 → 결과 → 캠프 ...
// 씬에 따로 넣지 않아도 ▶(플레이)를 누르면 자동으로 생성됩니다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    const float HalfWorldWidth = 14f; // 전투 화면에서 항상 보이는 가로 반폭

    enum Page { Title, Story, Camp, GachaSelect, Gacha, Formation, Roster, Battle, Result }

    // 스토리 장면 그림 종류
    enum Art { Kingdom, Darkness, King, Hero, Gacha, Victory }

    class StoryLine
    {
        public string speaker; // 비어 있으면 해설
        public string text;
        public Art art;
        public StoryLine(Art art, string speaker, string text) { this.art = art; this.speaker = speaker; this.text = text; }
    }

    static readonly StoryLine[] OpeningStory =
    {
        new StoryLine(Art.Kingdom,  "", "평화로운 아르카 왕국."),
        new StoryLine(Art.Darkness, "", "어느 날, 북쪽 땅에서 마왕이 깨어났다.\n마왕군은 마을을 불태우며 왕국으로 진격해 오고 있다."),
        new StoryLine(Art.King,     "국왕", "용사여, 그대만이 희망이오.\n마왕을 무찌르고 왕국을 구해 주시오!"),
        new StoryLine(Art.Hero,     "", "하지만 용사 혼자서는 마왕군을 당해낼 수 없었다."),
        new StoryLine(Art.Hero,     "용사", "...폐하, 제게는 특별한 힘이 하나 있습니다."),
        new StoryLine(Art.Gacha,    "", "운명의 동료를 불러내는 힘.\n사람들은 그것을 '가챠'라고 불렀다."),
        new StoryLine(Art.Gacha,    "용사", "좋아, 먼저 함께 떠날 동료 두 명을 불러내자!"),
    };

    static readonly StoryLine[] EndingStory =
    {
        new StoryLine(Art.Victory,  "", "마침내 마왕이 쓰러졌다."),
        new StoryLine(Art.King,     "국왕", "용사여, 그리고 용감한 동료들이여!\n왕국의 이름으로 감사하오!"),
        new StoryLine(Art.Hero,     "용사", "모두 덕분입니다. ...그런데 폐하, 뽑기 한 번만 더 해도 될까요?"),
        new StoryLine(Art.Kingdom,  "", "- 끝 -\n(캠프로 돌아가 계속 플레이할 수 있습니다)"),
    };

    Page page = Page.Title;
    BattleManager battle;
    readonly GachaScreen gacha = new GachaScreen();
    Camera cam;

    StoryLine[] story;
    int storyIndex;
    float storyLineStart;
    System.Action onStoryEnd;

    int selectedStage = 1; // 고른 판 번호 (1~100)
    bool confirmNewGame;
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
        SaveData.Load();
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
            case Page.Formation: DrawFormation(); break;
            case Page.Roster: DrawRoster(); break;
            case Page.Battle: battle.DrawHUD(); break;
            case Page.Result: DrawResult(); break;
        }

        if (toast != null && Now < toastUntil)
        {
            float w = UI.Width;
            var r = new Rect(w / 2f - 300f, 90f, 600f, 50f);
            UI.Fill(r, new Color(0f, 0f, 0f, 0.75f));
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

        UI.Glow(new Vector2(w / 2f, 210f), 330f, new Color(1f, 0.8f, 0.3f, 0.25f));
        UI.Text(new Rect(0, 150, w, 110), "가챠 용사", 96, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(0, 260, w, 40), "마왕을 무찌를 동료는 뽑기로 정한다!", 24, Color.white);

        float bx = w / 2f - 140f;
        if (SaveData.HasSave)
        {
            if (UI.Button(new Rect(bx, 380, 280, 70), "이어하기", new Color(0.85f, 0.55f, 0.15f), 28))
            {
                selectedStage = Mathf.Min(SaveData.ClearedStage + 1, Stages.Count);
                page = Page.Camp;
            }
            if (UI.Button(new Rect(bx, 470, 280, 60), "새로 시작", new Color(0.35f, 0.4f, 0.6f)))
                confirmNewGame = true;
        }
        else if (UI.Button(new Rect(bx, 400, 280, 80), "시작하기", new Color(0.85f, 0.55f, 0.15f), 30))
        {
            NewGame();
        }

        if (confirmNewGame)
        {
            UI.Fill(UI.Full, new Color(0f, 0f, 0f, 0.7f));
            var box = new Rect(w / 2f - 260f, 240f, 520f, 240f);
            UI.Panel(box, new Color(0.15f, 0.15f, 0.25f));
            UI.Text(new Rect(box.x, box.y + 20, box.width, 100), "새로 시작하면 지금까지의\n동료와 골드가 모두 사라져요.\n정말 새로 시작할까요?", 22, Color.white);
            if (UI.Button(new Rect(box.x + 40, box.y + 150, 200, 60), "새로 시작", new Color(0.7f, 0.25f, 0.25f)))
            {
                confirmNewGame = false;
                NewGame();
            }
            if (UI.Button(new Rect(box.xMax - 240, box.y + 150, 200, 60), "취소", new Color(0.35f, 0.4f, 0.6f)))
                confirmNewGame = false;
        }
    }

    void NewGame()
    {
        SaveData.ResetAll();
        selectedStage = 1;
        PlayStory(OpeningStory, StartFirstSummon);
    }

    // 처음 동료 두 명은 무료로 고급 뽑기에서 뽑습니다.
    void StartFirstSummon()
    {
        page = Page.Gacha;
        gacha.Start(GachaBanner.Advanced, 2, () =>
        {
            SaveData.AutoArrange();
            SaveData.Save();
            page = Page.Camp;
            ShowToast("동료들과 함께 여정을 떠나요! '편성'을 확인하고 '출격'하세요");
        });
    }

    // ---------------- 스토리 ----------------

    void PlayStory(StoryLine[] lines, System.Action onEnd)
    {
        story = lines;
        storyIndex = 0;
        storyLineStart = Now;
        onStoryEnd = onEnd;
        page = Page.Story;
    }

    void DrawStory()
    {
        float w = UI.Width;
        var line = story[storyIndex];
        DrawStoryArt(line.art, w);

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
        int shown = Mathf.Min(line.text.Length, Mathf.FloorToInt((Now - storyLineStart) * 30f));
        UI.Text(new Rect(box.x + 40, box.y + 35, box.width - 80, box.height - 60), line.text.Substring(0, shown), 26,
            Color.white, TextAnchor.UpperLeft);
        if (shown >= line.text.Length && Mathf.Repeat(Now, 1f) < 0.6f)
            UI.Text(new Rect(box.xMax - 80, box.yMax - 50, 50, 40), "▼", 22, Color.white);

        if (UI.Button(new Rect(w - 170, 20, 150, 46), "건너뛰기 ▶▶", new Color(0.3f, 0.3f, 0.4f), 18))
        {
            onStoryEnd?.Invoke();
            return;
        }

        if (UI.ClickedAnywhere())
        {
            if (shown < line.text.Length) storyLineStart = -1000f; // 글자를 한 번에 다 보여 줌
            else if (storyIndex + 1 < story.Length)
            {
                storyIndex++;
                storyLineStart = Now;
            }
            else onStoryEnd?.Invoke();
        }
    }

    // 장면 그림 (진짜 일러스트가 생기면 이 부분을 이미지로 바꾸면 됩니다)
    void DrawStoryArt(Art art, float w)
    {
        var center = new Vector2(w / 2f, 250f);
        switch (art)
        {
            case Art.Kingdom:
                UI.Gradient(UI.Full, new Color(0.45f, 0.70f, 0.95f), new Color(0.85f, 0.90f, 1f));
                UI.Fill(new Rect(0, 400, w, 320), new Color(0.40f, 0.65f, 0.32f));
                DrawCastle(center.x, 400f, new Color(0.85f, 0.85f, 0.9f));
                break;
            case Art.Darkness:
                UI.Gradient(UI.Full, new Color(0.10f, 0.0f, 0.02f), new Color(0.45f, 0.08f, 0.05f));
                UI.Glow(new Vector2(center.x, 170f), 260f, new Color(1f, 0.15f, 0.1f, 0.6f));
                UI.Circle(new Vector2(center.x - 40f, 170f), 14f, new Color(1f, 0.9f, 0.2f)); // 마왕의 눈
                UI.Circle(new Vector2(center.x + 40f, 170f), 14f, new Color(1f, 0.9f, 0.2f));
                UI.Fill(new Rect(0, 400, w, 320), new Color(0.12f, 0.05f, 0.05f));
                DrawCastle(center.x, 400f, new Color(0.15f, 0.1f, 0.1f));
                break;
            case Art.King:
                UI.Gradient(UI.Full, new Color(0.35f, 0.10f, 0.20f), new Color(0.15f, 0.05f, 0.10f));
                UI.Fill(new Rect(center.x - 60f, 0, 120f, 720f), new Color(0.6f, 0.1f, 0.15f)); // 레드카펫
                DrawPortrait(center, new Color(0.95f, 0.8f, 0.6f), true, new Color(0.5f, 0.1f, 0.6f));
                break;
            case Art.Hero:
                UI.Gradient(UI.Full, new Color(0.15f, 0.25f, 0.45f), new Color(0.05f, 0.08f, 0.15f));
                DrawPortrait(center, new Color(0.95f, 0.8f, 0.6f), false, new Color(0.75f, 0.15f, 0.2f));
                break;
            case Art.Gacha:
                UI.Gradient(UI.Full, new Color(0.05f, 0.03f, 0.15f), new Color(0.20f, 0.08f, 0.30f));
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f + Now;
                    var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.45f) * 170f;
                    UI.Glow(p, 26f, new Color(1f, 0.8f, 0.3f, 0.8f));
                }
                UI.Glow(center, 160f + 15f * Mathf.Sin(Now * 4f), new Color(1f, 0.8f, 0.3f, 0.7f));
                UI.Glow(center, 60f, Color.white);
                break;
            case Art.Victory:
                UI.Gradient(UI.Full, new Color(1f, 0.85f, 0.5f), new Color(0.95f, 0.55f, 0.35f));
                UI.Glow(center, 300f, new Color(1f, 1f, 0.8f, 0.8f));
                DrawPortrait(center, new Color(0.95f, 0.8f, 0.6f), false, new Color(0.75f, 0.15f, 0.2f));
                break;
        }
    }

    static void DrawCastle(float cx, float groundY, Color c)
    {
        UI.Fill(new Rect(cx - 150f, groundY - 140f, 300f, 140f), c);
        UI.Fill(new Rect(cx - 190f, groundY - 200f, 70f, 200f), c);
        UI.Fill(new Rect(cx + 120f, groundY - 200f, 70f, 200f), c);
        UI.Fill(new Rect(cx - 45f, groundY - 260f, 90f, 260f), c);
        UI.Fill(new Rect(cx - 25f, groundY - 70f, 50f, 70f), UI.Darken(c, 0.4f));
    }

    static void DrawPortrait(Vector2 center, Color skin, bool crown, Color cloth)
    {
        UI.Fill(new Rect(center.x - 110f, center.y + 60f, 220f, 200f), cloth);
        UI.Circle(center, 80f, skin);
        UI.Circle(center + new Vector2(-25f, -5f), 7f, Color.black);
        UI.Circle(center + new Vector2(25f, -5f), 7f, Color.black);
        if (crown)
        {
            var gold = new Color(1f, 0.8f, 0.2f);
            UI.Fill(new Rect(center.x - 60f, center.y - 105f, 120f, 30f), gold);
            for (int i = 0; i < 3; i++) UI.Fill(new Rect(center.x - 60f + i * 48f, center.y - 135f, 24f, 32f), gold);
        }
        else
        {
            UI.Fill(new Rect(center.x - 80f, center.y - 85f, 160f, 40f), new Color(0.35f, 0.2f, 0.1f)); // 머리카락
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
        UI.Text(new Rect(left.x, left.yMax - 75, left.width, 26), $"파티 {SaveData.PlacedCount()} / {SaveData.CellCount}명 배치됨", 18, Color.white);
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
        string formLabel = unplaced > 0 && SaveData.PlacedCount() < SaveData.CellCount ? $"편성\n(대기 {unplaced}명)" : "편성";
        if (UI.Button(new Rect(right.x + 30, right.y + 210, half, 110), formLabel, new Color(0.25f, 0.55f, 0.45f), 28))
            OpenFormation();
        if (UI.Button(new Rect(right.x + 50 + half, right.y + 210, half, 110), "도감", new Color(0.3f, 0.45f, 0.7f), 28))
            page = Page.Roster;
        UI.Text(new Rect(right.x, right.y + 330, right.width, 26), $"용사 각성 +{SaveData.HeroAwaken}  ·  동료 {SaveData.Roster.Count}명", 18, new Color(1f, 0.85f, 0.3f));
        if (UI.Button(new Rect(right.xMax - 160, right.yMax - 50, 130, 44), "타이틀로", new Color(0.3f, 0.3f, 0.4f), 18))
            page = Page.Title;
    }

    // ---------------- 편성 (5줄 x 5칸) ----------------
    // 오른쪽 목록에서 동료를 끌어다 칸에 놓습니다. 칸끼리 끌면 자리를 바꾸고, 목록으로 끌면 뺍니다.

    const float CellSize = 92f;
    const float CellGap = 6f;
    const float GridX = 40f;
    const float GridY = 110f;

    string dragValue;        // 끌고 있는 것: "H" 또는 Roster 번호 (없으면 null)
    int dragFromCell = -1;   // 칸에서 끌기 시작했으면 그 칸 번호
    string inspectValue;     // 정보 창에 보여 줄 동료
    float listScroll;

    void OpenFormation()
    {
        dragValue = null;
        dragFromCell = -1;
        listScroll = 0f;
        page = Page.Formation;
    }

    // 화면에서 왼쪽이 후미, 오른쪽(적이 오는 쪽)이 선두입니다.
    Rect CellRect(int column, int row)
    {
        int screenCol = SaveData.Columns - 1 - column;
        return new Rect(GridX + screenCol * (CellSize + CellGap), GridY + row * (CellSize + CellGap), CellSize, CellSize);
    }

    static CompanionDef DefOf(string value)
    {
        if (value == SaveData.HeroMark) return CompanionDef.Hero;
        if (int.TryParse(value, out int idx) && idx >= 0 && idx < SaveData.Roster.Count) return CompanionDef.Find(SaveData.Roster[idx]);
        return null;
    }

    void DrawFormation()
    {
        float w = UI.Width;
        var e = Event.current;
        UI.Gradient(UI.Full, new Color(0.12f, 0.18f, 0.16f), new Color(0.05f, 0.08f, 0.08f));
        DrawTopBar(w, $"편성  ({SaveData.PlacedCount()} / {SaveData.CellCount})");

        // ---- 칸 ----
        int hoverCell = -1;
        for (int col = 0; col < SaveData.Columns; col++)
        {
            var top = CellRect(col, 0);
            UI.Text(new Rect(top.x, GridY - 30, CellSize, 26), SaveData.ColumnNames[col], 18,
                col == 0 ? new Color(1f, 0.6f, 0.5f) : Color.white, TextAnchor.MiddleCenter, true);
            for (int row = 0; row < SaveData.Rows; row++)
            {
                int cell = SaveData.CellIndex(col, row);
                var r = CellRect(col, row);
                bool hover = r.Contains(e.mousePosition);
                if (hover) hoverCell = cell;
                UI.Fill(r, hover && dragValue != null ? new Color(1f, 1f, 1f, 0.25f) : new Color(0f, 0f, 0f, 0.35f));
                UI.Frame(r, new Color(1f, 1f, 1f, 0.15f), 2f);
                string v = SaveData.Formation[cell];
                if (!string.IsNullOrEmpty(v) && !(dragFromCell == cell && dragValue != null))
                    DrawMiniUnit(r, DefOf(v));
            }
        }
        UI.Text(new Rect(GridX, GridY + 5 * (CellSize + CellGap), 5 * (CellSize + CellGap), 26), "적이 오는 방향 →", 16,
            new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleRight);

        // ---- 대기 중인 동료 목록 (같은 동료끼리 묶어서 표시) ----
        float listX = GridX + 5 * (CellSize + CellGap) + 30f;
        var listRect = new Rect(listX, 80, w - listX - 30, 400);
        UI.Panel(listRect, new Color(0f, 0f, 0f, 0.4f));
        UI.Text(new Rect(listRect.x + 12, listRect.y + 6, listRect.width - 24, 28), "대기 중인 동료 (끌어서 칸에 놓기)", 18, Color.white, TextAnchor.MiddleLeft, true);

        var groups = new List<KeyValuePair<CompanionDef, List<int>>>();
        for (int i = 0; i < SaveData.Roster.Count; i++)
        {
            if (SaveData.IsPlaced(i)) continue;
            var def = CompanionDef.Find(SaveData.Roster[i]);
            var g = groups.Find(x => x.Key == def);
            if (g.Key == null) groups.Add(new KeyValuePair<CompanionDef, List<int>>(def, new List<int> { i }));
            else g.Value.Add(i);
        }
        groups.Sort((a, b) => b.Key.rarity.CompareTo(a.Key.rarity));

        const float itemW = 96f, itemH = 112f, itemGap = 8f;
        var inner = new Rect(listRect.x + 10, listRect.y + 40, listRect.width - 20, listRect.height - 50);
        int perRow = Mathf.Max(1, Mathf.FloorToInt((inner.width + itemGap) / (itemW + itemGap)));
        int rowsTotal = Mathf.CeilToInt(groups.Count / (float)perRow);
        float maxScroll = Mathf.Max(0f, rowsTotal * (itemH + itemGap) - inner.height);
        if (e.type == EventType.ScrollWheel && inner.Contains(e.mousePosition))
        {
            listScroll = Mathf.Clamp(listScroll + e.delta.y * 20f, 0f, maxScroll);
            e.Use();
        }
        listScroll = Mathf.Clamp(listScroll, 0f, maxScroll);

        int hoverGroup = -1;
        Vector2 mouse = e.mousePosition; // 그룹 안에서는 마우스 좌표가 달라지므로 미리 저장
        GUI.BeginGroup(inner);
        for (int i = 0; i < groups.Count; i++)
        {
            var r = new Rect((i % perRow) * (itemW + itemGap), (i / perRow) * (itemH + itemGap) - listScroll, itemW, itemH);
            if (r.yMax < 0 || r.y > inner.height) continue;
            var screenRect = new Rect(r.x + inner.x, r.y + inner.y, r.width, r.height);
            if (screenRect.Contains(mouse) && inner.Contains(mouse)) hoverGroup = i;
            DrawMiniUnit(r, groups[i].Key);
            if (groups[i].Value.Count > 1)
            {
                var tag = new Rect(r.xMax - 34, r.y + 2, 32, 22);
                UI.Fill(tag, new Color(0f, 0f, 0f, 0.7f));
                UI.Text(tag, "x" + groups[i].Value.Count, 14, Color.white, TextAnchor.MiddleCenter, true, false);
            }
        }
        GUI.EndGroup();
        if (groups.Count == 0)
            UI.Text(inner, SaveData.Roster.Count == 0 ? "아직 동료가 없어요.\n소환에서 뽑아 보세요!" : "모든 동료가 배치되었어요", 18, new Color(1f, 1f, 1f, 0.6f));
        if (maxScroll > 0f)
            UI.Text(new Rect(listRect.x, listRect.yMax - 24, listRect.width - 12, 22), "마우스 휠로 스크롤", 13, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleRight);

        // ---- 정보 창 ----
        var infoRect = new Rect(listX, 495, w - listX - 30, 120);
        UI.Panel(infoRect, new Color(0f, 0f, 0f, 0.4f));
        var info = DefOf(inspectValue);
        if (info != null)
        {
            float m = RarityInfo.StatMultiplier(info.rarity) * (info.IsHero ? 1f + 0.1f * SaveData.HeroAwaken : 1f);
            string title = info.IsHero ? $"{info.name}  (각성 +{SaveData.HeroAwaken})" : info.name;
            UI.Text(new Rect(infoRect.x + 15, infoRect.y + 8, infoRect.width - 30, 30), title, 22, RarityInfo.Animated(info.rarity), TextAnchor.MiddleLeft, true);
            UI.Text(new Rect(infoRect.x + 15, infoRect.y + 40, infoRect.width - 30, 26),
                $"{RarityInfo.Name(info.rarity)} · {info.job.name} ({info.job.role})", 17, Color.white, TextAnchor.MiddleLeft);
            string power = info.job.healer ? $"치유 {info.job.damage * m:0}" : $"공격 {info.job.damage * m:0}";
            UI.Text(new Rect(infoRect.x + 15, infoRect.y + 68, infoRect.width - 30, 26),
                $"체력 {info.job.hp * m:0}   {power}   사거리 {info.job.range:0.#}", 17, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft);
            UI.Text(new Rect(infoRect.x + 15, infoRect.y + 92, infoRect.width - 30, 24), info.desc, 14, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleLeft);
        }
        else
        {
            UI.Text(infoRect, "동료를 누르면 정보가 나와요\n근접 동료는 앞줄, 원거리 동료는 뒷줄이 좋아요", 17, new Color(1f, 1f, 1f, 0.6f));
        }

        // ---- 버튼 ----
        if (UI.Button(new Rect(GridX, 640, 170, 56), "◀ 저장 후 나가기", new Color(0.3f, 0.3f, 0.4f), 18))
        {
            SaveData.Save();
            page = Page.Camp;
        }
        if (UI.Button(new Rect(GridX + 185, 640, 150, 56), "자동 배치", new Color(0.25f, 0.55f, 0.45f), 20))
            SaveData.AutoArrange();
        if (UI.Button(new Rect(GridX + 350, 640, 150, 56), "모두 빼기", new Color(0.6f, 0.3f, 0.3f), 20))
        {
            SaveData.ClearFormation();
            SaveData.EnsureHeroPlaced();
        }
        UI.Text(new Rect(listX, 630, w - listX - 30, 70), "용사는 항상 파티에 있어야 해요 (자리만 옮길 수 있어요)", 15, new Color(1f, 0.85f, 0.3f));

        // ---- 끌어서 놓기 ----
        if (e.type == EventType.MouseDown && e.button == 0 && dragValue == null)
        {
            if (hoverCell >= 0 && !string.IsNullOrEmpty(SaveData.Formation[hoverCell]))
            {
                dragValue = SaveData.Formation[hoverCell];
                dragFromCell = hoverCell;
                inspectValue = dragValue;
                e.Use();
            }
            else if (hoverGroup >= 0)
            {
                dragValue = groups[hoverGroup].Value[0].ToString();
                dragFromCell = -1;
                inspectValue = dragValue;
                e.Use();
            }
        }
        else if (e.type == EventType.MouseUp && dragValue != null)
        {
            if (hoverCell >= 0) DropOnCell(hoverCell);
            else if (dragFromCell >= 0 && listRect.Contains(e.mousePosition) && dragValue != SaveData.HeroMark)
                SaveData.Formation[dragFromCell] = ""; // 목록으로 끌어 놓으면 빼기
            dragValue = null;
            dragFromCell = -1;
            e.Use();
        }

        // 끌고 있는 동료를 마우스 위치에 그림
        if (dragValue != null)
        {
            var def = DefOf(dragValue);
            if (def != null) DrawMiniUnit(new Rect(e.mousePosition.x - 46, e.mousePosition.y - 46, 92, 92), def);
        }
    }

    void DropOnCell(int target)
    {
        string displaced = SaveData.Formation[target];
        if (dragFromCell >= 0)
        {
            // 칸 ↔ 칸: 자리 바꾸기
            SaveData.Formation[dragFromCell] = displaced;
            SaveData.Formation[target] = dragValue;
        }
        else
        {
            // 목록 → 칸: 원래 있던 동료는 목록으로 (용사는 뺄 수 없음)
            if (displaced == SaveData.HeroMark) return;
            SaveData.Formation[target] = dragValue;
        }
    }

    // 편성 화면의 작은 동료 칸
    static void DrawMiniUnit(Rect r, CompanionDef def)
    {
        if (def == null) return;
        Color rc = RarityInfo.Animated(def.rarity);
        UI.Fill(r, UI.Darken(rc, 0.35f));
        UI.Frame(r, def.IsHero ? new Color(1f, 0.85f, 0.3f) : rc, def.IsHero ? 4f : 3f);
        var c = new Vector2(r.center.x, r.y + r.height * 0.4f);
        float rad = r.width * 0.26f;
        UI.Circle(c, rad + 3f, rc);
        UI.Circle(c, rad, def.job.color);
        UI.Text(new Rect(c.x - rad, c.y - rad, rad * 2, rad * 2), def.job.letter, Mathf.RoundToInt(rad * 1.1f), Color.white, TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(r.x + 2, r.yMax - 28, r.width - 4, 24), def.name, 13, Color.white, TextAnchor.MiddleCenter, true);
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
            if (beatDemonKing) PlayStory(EndingStory, () => page = Page.Camp);
            else page = Page.Camp;
        }

        bool hasNext = resultVictory && resultStage < Stages.Count;
        string again = hasNext ? "다음 스테이지 ▶" : "다시 도전";
        if (!beatDemonKing && UI.Button(new Rect(bx + 20, 560, 270, 76), again, new Color(0.8f, 0.35f, 0.2f), 26))
            StartBattle(hasNext ? resultStage + 1 : resultStage);
    }
}
