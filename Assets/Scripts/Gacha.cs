using System.Collections.Generic;
using UnityEngine;

// 뽑기 종류(일반/고급/전설/신화)와 확률입니다.
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
        color = new Color(0.35f, 0.55f, 0.75f),
    };

    public static readonly GachaBanner Advanced = new GachaBanner
    {
        name = "고급 뽑기", desc = "실력 있는 동료를 모집합니다", cost = 300, unlockAfterStage = 0,
        rates = new float[] { 25, 45, 22, 7, 1, 0 }, guarantee = Rarity.SuperRare,
        color = new Color(0.55f, 0.35f, 0.80f),
    };

    public static readonly GachaBanner Legendary = new GachaBanner
    {
        name = "전설 뽑기", desc = "전설 속 영웅을 부릅니다", cost = 1000, unlockAfterStage = 20, // 2-10 클리어
        rates = new float[] { 0, 20, 40, 28, 11, 1 }, guarantee = Rarity.Unique,
        color = new Color(0.85f, 0.60f, 0.15f),
    };

    public static readonly GachaBanner Mythic = new GachaBanner
    {
        name = "신화 뽑기", desc = "신화의 존재를 소환합니다", cost = 3000, unlockAfterStage = 50, // 5-10 클리어
        rates = new float[] { 0, 0, 25, 40, 27, 8 }, guarantee = Rarity.Legendary,
        color = new Color(0.80f, 0.20f, 0.30f),
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

    CompanionDef RollOne(Rarity minRarity)
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
