using System.Collections.Generic;
using UnityEngine;

// 보상 카드의 등급 확률표(일반/고급/전설/신화)입니다. 스테이지가 높을수록 좋은 표를 써요 (CardDraft).
// (라운드를 이길 때마다 카드 3장 중 1장을 골라요)
// 확률 배열 순서: 노말, 레어, 슈퍼레어, 유니크, 전설, 신화
public class GachaBanner
{
    public string name;
    public string desc;
    public int cost;             // 1회 가격 (10회는 9회 가격)
    public int unlockAfterStage; // 이 판(1~100번째)을 클리어하면 열림 (0이면 처음부터 열림)
    public float[] rates;        // 등급별 확률(%) : 노말, 레어, 슈퍼레어, 유니크, 전설, 신화
    public Rarity guarantee;     // 10회 뽑기 시 최소 1명 보장 등급
    public Color color;

    public bool IsUnlocked => SaveData.ClearedStage >= unlockAfterStage;
    public int TenCost => cost * 9;

    public static readonly GachaBanner Normal = new GachaBanner
    {
        name = "일반 뽑기", desc = "기본적인 동료를 모집합니다", cost = 100, unlockAfterStage = 0,
        rates = new float[] { 65, 27, 7, 1, 0, 0 }, guarantee = Rarity.Rare,
        color = new Color(0.22f, 0.32f, 0.46f), // 네이비
    };

    public static readonly GachaBanner Advanced = new GachaBanner
    {
        name = "고급 뽑기", desc = "실력 있는 동료를 모집합니다", cost = 300, unlockAfterStage = 0,
        rates = new float[] { 25, 45, 22, 7, 1, 0 }, guarantee = Rarity.SuperRare,
        color = new Color(0.38f, 0.24f, 0.42f), // 자주
    };

    public static readonly GachaBanner Legendary = new GachaBanner
    {
        name = "전설 뽑기", desc = "전설 속 영웅을 부릅니다", cost = 1000, unlockAfterStage = 20, // 2-10 클리어
        rates = new float[] { 0, 20, 40, 28, 11, 1 }, guarantee = Rarity.Unique,
        color = new Color(0.62f, 0.45f, 0.18f), // 청동금
    };

    public static readonly GachaBanner Mythic = new GachaBanner
    {
        name = "신화 뽑기", desc = "신화의 존재를 소환합니다", cost = 3000, unlockAfterStage = 50, // 5-10 클리어
        rates = new float[] { 0, 0, 25, 40, 27, 8 }, guarantee = Rarity.Legendary,
        color = new Color(0.55f, 0.14f, 0.18f), // 버건디
    };

    public static readonly GachaBanner[] All = { Normal, Advanced, Legendary, Mythic };

    // 뽑기 실행. 같은 캐릭터도 여러 번 나올 수 있어요 (같은 캐릭터를 합쳐서 성급을 올릴 수 있음).
    public List<CompanionDef> Roll(int count)
    {
        var results = new List<CompanionDef>();
        for (int i = 0; i < count; i++) results.Add(RollOne(Rarity.Normal));

        // 10회 뽑기 보장: 보장 등급 이상이 하나도 없으면 하나를 바꿔 줌
        if (count >= 10 && !results.Exists(c => c.rarity >= guarantee))
            results[Random.Range(0, count)] = RollOne(guarantee);
        return results;
    }

    public CompanionDef RollOne(Rarity minRarity)
    {
        float total = 0f;
        int highest = (int)minRarity;
        for (int i = (int)minRarity; i < rates.Length; i++)
        {
            total += rates[i];
            if (rates[i] > 0f) highest = i;
        }

        float roll = Random.value * total;
        for (int i = (int)minRarity; i < rates.Length; i++)
        {
            if (rates[i] <= 0f) continue;
            if (roll < rates[i]) return CompanionDef.RandomOf((Rarity)i);
            roll -= rates[i];
        }
        return CompanionDef.RandomOf((Rarity)highest);
    }
}

// 보상 카드 3장 나누기: 라운드를 이기면 3장이 나오고 그중 1장을 골라요.
public static class CardDraft
{
    public const int Count = 3;

    // 스테이지(맵)에 따라 등급 확률표가 좋아짐: 1맵 일반, 2~3맵 고급, 4~6맵 전설, 7맵~ 신화
    public static GachaBanner TierFor(int level)
    {
        int stage = Stages.StageOf(level);
        return stage <= 0 ? GachaBanner.Normal : stage <= 2 ? GachaBanner.Advanced : stage <= 5 ? GachaBanner.Legendary : GachaBanner.Mythic;
    }

    // 서로 다른 동료 3장. bonus면(보스 · 중간 보스를 이긴 판) 그 표의 보장 등급 이상이 최소 1장
    public static List<CompanionDef> Deal(GachaBanner tier, bool bonus)
    {
        var cards = new List<CompanionDef>();
        for (int tries = 0; cards.Count < Count && tries < 40; tries++)
        {
            var c = tier.RollOne(Rarity.Normal);
            if (!cards.Contains(c)) cards.Add(c);
        }
        while (cards.Count < Count) cards.Add(tier.RollOne(Rarity.Normal));
        if (bonus && !cards.Exists(c => c.rarity >= tier.guarantee))
        {
            for (int tries = 0; tries < 20; tries++)
            {
                var c = tier.RollOne(tier.guarantee);
                if (!cards.Contains(c)) { cards[Random.Range(0, Count)] = c; break; }
            }
        }
        return cards;
    }
}
