using System.Collections.Generic;
using UnityEngine;

// 진행 상황(골드, 클리어한 스테이지, 동료 목록)을 컴퓨터에 저장합니다.
public static class SaveData
{
    const string Prefix = "GachaHero.";

    public static int Gold;
    public static int ClearedStage;                               // 가장 멀리 클리어한 스테이지
    public static readonly List<string> Roster = new List<string>(); // 가진 동료 id (중복 = 같은 동료 여러 명)

    public static bool HasSave => PlayerPrefs.GetInt(Prefix + "Started", 0) == 1;

    public static void Load()
    {
        Gold = PlayerPrefs.GetInt(Prefix + "Gold", 0);
        ClearedStage = PlayerPrefs.GetInt(Prefix + "ClearedStage", 0);
        Roster.Clear();
        foreach (var id in PlayerPrefs.GetString(Prefix + "Roster", "").Split(','))
            if (CompanionDef.Find(id) != null) Roster.Add(id);
    }

    public static void Save()
    {
        PlayerPrefs.SetInt(Prefix + "Started", 1);
        PlayerPrefs.SetInt(Prefix + "Gold", Gold);
        PlayerPrefs.SetInt(Prefix + "ClearedStage", ClearedStage);
        PlayerPrefs.SetString(Prefix + "Roster", string.Join(",", Roster));
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        Gold = 0;
        ClearedStage = 0;
        Roster.Clear();
        PlayerPrefs.DeleteKey(Prefix + "Started");
        PlayerPrefs.DeleteKey(Prefix + "Gold");
        PlayerPrefs.DeleteKey(Prefix + "ClearedStage");
        PlayerPrefs.DeleteKey(Prefix + "Roster");
        PlayerPrefs.Save();
    }

    public static int CountOwned(CompanionDef def)
    {
        int n = 0;
        foreach (var id in Roster) if (id == def.id) n++;
        return n;
    }

    // 전투에 나갈 동료: 등급 높은 순서로 최대 max명
    public static List<CompanionDef> DeployList(int max)
    {
        var list = new List<CompanionDef>();
        foreach (var id in Roster) list.Add(CompanionDef.Find(id));
        list.Sort((a, b) => b.rarity.CompareTo(a.rarity));
        if (list.Count > max) list.RemoveRange(max, list.Count - max);
        return list;
    }
}
