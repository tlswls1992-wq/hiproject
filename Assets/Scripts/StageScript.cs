using System.Collections.Generic;

// ★ 스테이지 대본: 스테이지마다 나오는 적, 보스, 대사, 엔딩 선택지
//
//   1~4라운드   : early(전반 출연 적)
//   5라운드     : early + 중간 보스 (웨이브 2에 함께 등장)
//   6~9라운드   : late(후반 출연 적) + lateElites('(1)' 표시 인물: 나오면 한 명만, 가끔 섞여 나옴)
//   10라운드    : late + 최종 보스와 함께 나오는 인물(bossGroup) + 최종 보스 (웨이브 2에 함께 등장)
//
// 대사는 전투 화면 위에 사념파처럼 한 줄씩 떠올라요. 보스 그림(Resources/Enemies/<id>/full.png)이 있으면 오른쪽에 나와요.
public class ScriptLine
{
    public string text;
    public string condition; // 비어 있으면 항상. "race:엘프" = 진형에 그 종족이 있을 때, "member:amelia" = 진형에 그 인물이 있을 때

    public ScriptLine(string text, string condition = null) { this.text = text; this.condition = condition; }

    // 출진한 동료(deployed)를 보고 이 대사를 말할지 정함
    public bool Applies(IEnumerable<CompanionDef> deployed)
    {
        if (string.IsNullOrEmpty(condition)) return true;
        var parts = condition.Split(':');
        if (parts.Length != 2) return true;
        foreach (var d in deployed)
        {
            if (parts[0] == "race" && d.race == parts[1]) return true;
            if (parts[0] == "member" && d.id == parts[1]) return true;
        }
        return false;
    }
}

public class StageChoice
{
    public string text;  // 선택지 문장
    public int toStage;  // 가는 스테이지 (1~10)
    public StageChoice(string text, int toStage) { this.text = text; this.toStage = toStage; }
}

public class StageScript
{
    public string[] early;
    public string midBoss;
    public string[] late;
    public string[] lateElites = { };
    public string[] bossGroup = { };   // 최종 보스와 함께 나오는 인물들
    public string boss;
    public ScriptLine[] entrance = { }; // 맵 입장 멘트 (1라운드 시작할 때)
    public ScriptLine[] midBossLines = { };
    public ScriptLine[] bossLines = { };
    public StageChoice[] endingChoices = { };

    static ScriptLine[] L(params string[] lines)
    {
        var list = new ScriptLine[lines.Length];
        for (int i = 0; i < lines.Length; i++) list[i] = new ScriptLine(lines[i]);
        return list;
    }

    public static StageScript Of(int stage) => All[UnityEngine.Mathf.Clamp(stage, 0, All.Length - 1)];

    static StageScript[] all;
    static StageScript[] All => all ?? (all = Build());

    static StageScript[] Build() => new[]
    {
        // ---------------- 1. 올 왕국 외곽 숲 ----------------
        new StageScript
        {
            early = new[] { "wolf", "wolf", "bear" },
            midBoss = "big_bear",
            late = new[] { "bandit1", "bandit2", "bandit3" },
            lateElites = new[] { "oslo", "shilla" },
            bossGroup = new[] { "oslo", "shilla" },
            boss = "frank",
            entrance = L("올 왕국 외곽의 울창한 숲", "어째서인지 치안이 좋지 않다", "지갑도 목숨도 단단히 챙기도록"),
            midBossLines = L("우옹...", "우엉...", "우어어엉!!!", "(대충 내 집에서 나가라는 뜻)"),
            bossLines = L("뭐... 용사? 왕국에서 왔다면 돈이 많겠군",
                          "돈은 전부 빼앗고 노예로 만들어 포빌리아에 팔아치워야 겠어.",
                          "뭣! 용사는 상인이 아니라 돈이 없다고?!?!?!"),
            endingChoices = new[] { new StageChoice("이제 겨우 왕국을 벗어났다. 동부 평원으로 가자!", 2) },
        },

        // ---------------- 2. 동부 평원 ----------------
        new StageScript
        {
            early = new[] { "elf_archer", "elf_tracker" },
            midBoss = "julian",
            late = new[] { "orc1", "orc2", "orc3" },
            lateElites = new[] { "wengaram" },
            boss = "fingaram",
            entrance = L("넓디 넓은 동부 평원", "말을 타고 달려도 끝이 보이지 않는다", "하지만 수 많은 갈림길이 있을지도...?"),
            midBossLines = new[]
            {
                new ScriptLine("용사... 이곳은 인간이 출입이 금지된 곳!"),
                new ScriptLine("네가 가야할 곳으로 돌아가라."),
                new ScriptLine("저항할 시 여왕님의 이름으로 너희를 처단하겠다!"),
                new ScriptLine("배신자 있군....", "race:엘프"),
                new ScriptLine("아멜리아...! 왜 저들과!!", "member:amelia"),
            },
            bossLines = L("인간? 어떻게 동부 평원까지 왔지!", "전사로서 승부를 신청한다!", "나를 이기면, 길을 열어주지."),
            endingChoices = new[] { new StageChoice("평원을 넘었더니 이제는 산맥까지 넘어가라는 거야?", 3) },
        },

        // ---------------- 3. 올드락 산맥 ----------------
        new StageScript
        {
            early = new[] { "wyvern", "scorpion", "baby_worm" },
            midBoss = "sandworm",
            late = new[] { "dwarf_guard", "dwarf_shield", "dwarf_slinger" },
            boss = "talim",
            entrance = L("대륙의 제일 높은 올드락 산맥", "정말 여기를 오르란 말이야?", "지하로 가는 길이 있다는 소문이..."),
            midBossLines = L("키...(배가)", "키에....(너무....)", "키에엑!!!!!(고파!!!!)"),
            bossLines = L("인간 용사. 잘못된 길로 들어왔다.", "조용히 산맥을 넘어가라.", "다른 의도가 있다면 이 몸의 망치 맛을 보게 될거다!"),
            endingChoices = new[] { new StageChoice("시원한 바다가 우리를 기다리고 있어! 배를 타고 어디든 갈 수 있다는데!", 4) },
        },

        // ---------------- 4. 카니 해안 ----------------
        new StageScript
        {
            early = new[] { "mer_patrol", "mer_spear", "mer_shaman" },
            midBoss = "mudiar",
            late = new[] { "shark", "pirate", "mer_pirate" },
            boss = "kraken",
            entrance = L("시원한 바다다!!", "배를 타면 어디든 갈 수 있어!", "폭풍우를 만난다면 예기치 못한 곳으로 갈 지도...."),
            midBossLines = L("용사. 바다엔 어떤 일로 온 거지?", "네가 여제님을 도울 수 있다면...", "아니. 아무것도. 인간에게 기대하면 안 돼."),
            bossLines = L("우우우오오오!", "에에에에엑!", "(바다에 쓰레기를 버리지 맙시다)"),
            endingChoices = new[] { new StageChoice("포빌리아 왕국은 가장 큰 인간 왕국이래. 검문소에서 별 탈 없이 통과해야 할텐데", 5) },
        },

        // ---------------- 5. 포빌리아 왕국 검문소 ----------------
        new StageScript
        {
            early = new[] { "pov_sword", "pov_archer", "pov_spear" },
            midBoss = "kellin",
            late = new[] { "pov_knight", "pov_xbow", "pov_assassin" },
            bossGroup = new[] { "antonio", "sine" },
            boss = "iris",
            entrance = L("커다란 왕국 포빌리아", "올 왕국의 5배가 넘는다는 소문이 있다", "욕망과 배신이 넘치는 무서운 왕국이라던데..."),
            midBossLines = L("포빌리아의 영토를 침범하는 자는 가만두지 않겠다.", "전군. 적을 격퇴하라!", "잠깐... 통행증이 있다고? 취소!! 취소!! 다들 돌아와!!!"),
            bossLines = L("용사... 꽤 내 취향이잖아.", "내가 이기면 앞으로 내 부하가 되는거야.", "마왕? 대륙 끝에 쭈그리고 있는 그런 것 따윈 알게 뭐야?"),
            endingChoices = new[] { new StageChoice("이제 인간의 영역은 끝났어. 날개 달린 천족이라니... 우리를 도와 줄까?", 6) },
        },

        // ---------------- 6. 은혜의 땅 ----------------
        new StageScript
        {
            early = new[] { "golem_battle", "golem_guard", "golem_magic" },
            midBoss = "giant_golem",
            late = new[] { "angel_warrior", "angel_mage", "angel_monk", "angel_healer" },
            boss = "belena",
            entrance = L("하늘에서 내려왔다는 전설의 천족", "심판의 날을 기다리고 있다는데...", "도대체 누굴 심판한다는 거지?"),
            midBossLines = L("끼리릭.", "끼리릭.", "침입자. 섬열. 즉시 말살."),
            bossLines = L("여기는 약속된 은혜의 땅. 아무나 들어올 수 없다.", "용사라고? 그렇다면 이야기가 달라지지.", "네가 마왕을 쓰러뜨릴 수 있도록 단련해 주마."),
            endingChoices = new[] { new StageChoice("불을 뿜고 하늘을 날아다니는 용의 영토를 지나야 하는데... 다들 소리내지 말고 가자!", 7) },
        },

        // ---------------- 7. 노란 용의 동굴 ----------------
        new StageScript
        {
            early = new[] { "dragontooth_spear", "dragontooth_archer" },
            midBoss = "liandra",
            late = new[] { "silver_statue", "gold_statue" },
            boss = "oderion",
            entrance = L("탐욕스럽고 똑똑한 용들...!", "용의 동굴엔 어마어마한 금은보화가 숨겨져 있대", "조금은 훔쳐가도 아무도 모르겠지?"),
            midBossLines = L("너희 인간들 여기가 어딘지 알고 들어온거야?", "제 발로 들어온 인간은 200년 만인가...", "나랑 재미있게 놀다가... 죽어줘."),
            bossLines = L("용사라... 몇 십년 만이군", "저번 용사는 속임수로 왕이 되었다지?", "너는 정직한 녀석이길 바란다 용사여."),
            endingChoices = new[] { new StageChoice("노란 용이 무슨 말을 하는거지... 나 이전 용사는 도대체 누구인거야? 지금은 마왕을 처히는데 집중하자!", 8) },
        },

        // ---------------- 8. 붉은 정글 ----------------
        new StageScript
        {
            early = new[] { "beast_warrior1", "beast_warrior2", "beast_archer1" },
            midBoss = "imta",
            late = new[] { "beast_hunter", "beast_healer", "beast_shaman" },
            boss = "sisibel",
            entrance = L("원래는 푸른 정글이었다는 이야기가 있어", "대전쟁의 영향으로 나무가 전부 붉게 변했대", "잠깐... 사람이 동물 귀가 있는거 같은데???"),
            midBossLines = L("인간이 어째서 여기에...", "너희는 맹약을 저버렸다.", "붉은 정글을 벗어날 수 없을 것이다."),
            bossLines = L("인간, 엘프, 드워프 너희들 모두 우리를 무시하지", "대전쟁에 우리를 끌어들여 놓고, 우리의 터전을 망가뜨렸다.", "그 책임... 용사인 너에게 묻겠다!"),
            endingChoices = new[] { new StageChoice("이 앞에 수년에 걸쳐 대전쟁이 벌어진 곳이래. 무엇 때문에 그렇게 싸웠을까?", 9) },
        },

        // ---------------- 9. 옛 대전쟁터 ----------------
        new StageScript
        {
            early = new[] { "skeleton", "ghoul", "wraith" },
            midBoss = "lich",
            late = new[] { "empty_armor", "empty_archer" },
            boss = "ankara",
            entrance = L("수많은 종족들이 모여서 전쟁을 벌인 곳이야", "도대체 무엇 때문에 싸웠을까?", "잠깐... 누가 땅에서 튀어나왔어!!!"),
            midBossLines = L("왔구나... 살아있는 자여....", "대전쟁은 끝나지 않았다....", "보이는 모든 것들은 대지로 돌아가"),
            bossLines = L("모든 것은 선택의 연속입니다.", "여기는 어리석은 선택의 흔적.", "나는 그 기억을 지키는 마지막 책."),
            endingChoices = new[] { new StageChoice("과연 이 전쟁의 진실을 아는 사람이 있을까? 그나저나... 마왕성이 눈앞이다!", 10) },
        },

        // ---------------- 10. 마왕성 ----------------
        new StageScript
        {
            early = new[] { "demon_warrior", "demon_mage", "demon_shield" },
            midBoss = "hoffman",
            late = new[] { "demon_mage", "demon_knight", "demon_general" },
            boss = "arin",
            entrance = L("드디어 마왕성이다!", "마왕 내가 널 무찌르고 진짜 용사가 되겠어!!", "그런데... 마왕이 어딘가 낯이 익은데?"),
            midBossLines = L("안녕하십니까. 마왕님을 모시는 호프만입니다.", "올 왕국에서 오신 분들이라고요... 그렇다면", "마왕님께는 한 발자국도 보내드릴 수 없습니다."),
            bossLines = L("나를 이렇게 만든 것도 모자라 이젠 죽이겠다고?", "지독하네... 인간이란.", "어울려 줄게. 아무것도 모르는 용사여."),
            // 마지막 스테이지: 이 선택지를 고르면 엔딩으로
            endingChoices = new[] { new StageChoice("마왕을 무찔렀다. 모든 것이 끝났다. 이제 올 왕국으로 돌아가자. 하지만 마왕이 한 말은... 무슨 뜻일까?", 10) },
        },
    };
}
