using System.Collections.Generic;
using UnityEngine;

// 게임 전체 흐름을 관리합니다.
// 타이틀 → 스토리 → 첫 소환(동료 2명) → 캠프(출격/소환/동료) → 전투 → 결과 → 캠프 ...
// 씬에 따로 넣지 않아도 ▶(플레이)를 누르면 자동으로 생성됩니다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    const float HalfWorldWidth = 14f; // 전투 화면에서 항상 보이는 가로 반폭

    enum Page { Title, Story, Camp, GachaSelect, Gacha, Roster, Battle, Result }

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
        new StoryLine(Art.Hero,     "", "하지만 이 용사에게는 검술도, 마법도 없었다."),
        new StoryLine(Art.Hero,     "용사", "...폐하, 제가 가진 힘은 단 하나뿐입니다."),
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

    int selectedStage = 1;
    bool confirmNewGame;
    Vector2 rosterScroll;

    // 결과 화면 정보
    bool resultVictory;
    int resultStage;
    int resultKillGold;
    int resultClearGold;
    bool resultFirstClear;
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
            page = Page.Camp;
            ShowToast("동료 2명과 함께 여정을 떠나요! '출격'을 눌러 보세요");
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
        var fire = new Vector2(w / 2f, 600f);
        UI.Glow(fire, 120f + 8f * Mathf.Sin(Now * 7f), new Color(1f, 0.55f, 0.15f, 0.5f));
        UI.Glow(fire, 30f + 4f * Mathf.Sin(Now * 11f), new Color(1f, 0.9f, 0.4f, 0.9f));

        DrawTopBar(w, "용사의 캠프");

        // 왼쪽: 출격 판
        var left = new Rect(40, 90, w * 0.5f - 60, 420);
        UI.Panel(left, new Color(0f, 0f, 0f, 0.45f));
        UI.Text(new Rect(left.x, left.y + 15, left.width, 40), "출격", 30, Color.white, TextAnchor.MiddleCenter, true);

        int maxStage = Mathf.Min(SaveData.ClearedStage + 1, Stages.Count);
        selectedStage = Mathf.Clamp(selectedStage, 1, maxStage);
        float cx = left.center.x;
        if (UI.Button(new Rect(left.x + 25, left.y + 100, 60, 80), "◀", new Color(0.3f, 0.35f, 0.5f), 26, selectedStage > 1)) selectedStage--;
        if (UI.Button(new Rect(left.xMax - 85, left.y + 100, 60, 80), "▶", new Color(0.3f, 0.35f, 0.5f), 26, selectedStage < maxStage)) selectedStage++;
        UI.Text(new Rect(cx - 200, left.y + 80, 400, 60), $"스테이지 {Stages.Label(selectedStage)}", 40, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(cx - 200, left.y + 140, 400, 34), Stages.ChapterName(selectedStage), 22, Color.white);
        string info = Stages.HasBoss(selectedStage) ? $"보스: {Stages.BossName(selectedStage)}" : $"웨이브 {Stages.WavesPerStage}개";
        UI.Text(new Rect(cx - 200, left.y + 180, 400, 30), info, 20,
            Stages.HasBoss(selectedStage) ? new Color(1f, 0.45f, 0.45f) : new Color(1f, 1f, 1f, 0.8f));
        bool firstTime = selectedStage > SaveData.ClearedStage;
        int reward = Stages.ClearReward(selectedStage) * (firstTime ? 2 : 1);
        UI.Text(new Rect(cx - 200, left.y + 212, 400, 30), $"클리어 보상: {reward} 골드" + (firstTime ? " (최초 2배)" : ""), 18, new Color(1f, 0.85f, 0.3f));

        if (UI.Button(new Rect(cx - 140, left.y + 270, 280, 90), "출격!", new Color(0.8f, 0.35f, 0.2f), 34, SaveData.Roster.Count > 0))
            StartBattle(selectedStage);
        UI.Text(new Rect(left.x, left.yMax - 45, left.width, 30), $"진행도 {SaveData.ClearedStage} / {Stages.Count}", 18, new Color(1f, 1f, 1f, 0.7f));

        // 오른쪽: 소환 / 동료
        var right = new Rect(w * 0.5f + 20, 90, w * 0.5f - 60, 420);
        float pulse = 1f + 0.05f * Mathf.Sin(Now * 3f);
        UI.Glow(new Vector2(right.center.x, right.y + 110), 200f * pulse, new Color(1f, 0.75f, 0.2f, 0.35f));
        if (UI.Button(new Rect(right.x + 30, right.y + 30, right.width - 60, 160), "소환", new Color(0.85f, 0.55f, 0.15f), 44))
            page = Page.GachaSelect;
        UI.Text(new Rect(right.x, right.y + 195, right.width, 30), "골드로 새로운 동료를 뽑아요", 18, new Color(1f, 1f, 1f, 0.8f));

        if (UI.Button(new Rect(right.x + 30, right.y + 250, right.width - 60, 100), $"동료 ({SaveData.Roster.Count}명)", new Color(0.3f, 0.45f, 0.7f), 30))
            page = Page.Roster;
        if (UI.Button(new Rect(right.xMax - 160, right.yMax - 50, 130, 44), "타이틀로", new Color(0.3f, 0.3f, 0.4f), 18))
            page = Page.Title;

        // 아래: 출전할 동료 미리보기
        var deploy = SaveData.DeployList(BattleManager.MaxDeploy);
        float px = 40;
        UI.Text(new Rect(40, 530, 600, 26), $"출전 동료 {deploy.Count}명 (등급 높은 순, 최대 {BattleManager.MaxDeploy}명)", 16, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleLeft);
        foreach (var def in deploy)
        {
            var p = new Vector2(px + 14, 580);
            UI.Circle(p, 15f, RarityInfo.Animated(def.rarity));
            UI.Circle(p, 12f, def.job.color);
            px += 34;
            if (px > w - 40) break;
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
                GachaScreen.DrawCardFace(r, def, false);
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
        foreach (var def in CompanionDef.All) if (SaveData.Roster.Contains(def.id)) n++;
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
        resultStage = battle.Stage;
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

        UI.Text(new Rect(0, 100, w, 90), resultVictory ? "승리!" : "패배...", 72,
            resultVictory ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleCenter, true);
        UI.Text(new Rect(0, 190, w, 36), $"스테이지 {Stages.Label(resultStage)}  ·  {Stages.ChapterName(resultStage)}", 24, Color.white);

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
            UI.Text(new Rect(0, y, w, 60), "골드로 동료를 더 뽑아서 다시 도전해 보세요!", 22, new Color(1f, 1f, 1f, 0.8f));
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
