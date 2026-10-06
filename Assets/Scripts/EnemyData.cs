using System.Collections.Generic;
using UnityEngine;

// 적 정보 (전투와 도감에서 사용)
public enum EnemyType { Normal, MidBoss, Boss }

public class EnemyDef
{
    public string id;
    public string name;
    public EnemyType type;
    public int stage = -1;     // 중간 보스/보스가 나오는 스테이지 (0부터). 일반 적은 -1
    public string desc;
    public Color color;

    public string TypeName => type == EnemyType.Boss ? "스테이지 보스" : type == EnemyType.MidBoss ? "중간 보스" : "일반";

    static List<EnemyDef> all;

    // 일반 적 3종 + 스테이지마다 중간 보스 1, 스테이지 보스 1
    public static List<EnemyDef> All
    {
        get
        {
            if (all != null) return all;
            all = new List<EnemyDef>
            {
                new EnemyDef { id = "goblin", name = "고블린", type = EnemyType.Normal, desc = "어디에나 있는 마왕군의 졸병", color = new Color(0.9f, 0.25f, 0.25f) },
                new EnemyDef { id = "goblin_archer", name = "고블린 궁수", type = EnemyType.Normal, desc = "멀리서 화살을 쏜다", color = new Color(1f, 0.6f, 0.2f) },
                new EnemyDef { id = "ogre", name = "오우거", type = EnemyType.Normal, desc = "느리지만 튼튼하고 힘이 세다", color = new Color(0.6f, 0.1f, 0.1f) },
            };
            for (int s = 0; s < Stages.StageCount; s++)
            {
                all.Add(new EnemyDef
                {
                    id = MidBossId(s), name = Stages.MidBossNameByIndex(s), type = EnemyType.MidBoss, stage = s,
                    desc = $"{Stages.StageNameByIndex(s)}의 {Stages.MidBossRound}라운드에 나타난다", color = new Color(0.55f, 0.25f, 0.6f),
                });
                all.Add(new EnemyDef
                {
                    id = BossId(s), name = Stages.BossNameByIndex(s), type = EnemyType.Boss, stage = s,
                    desc = $"{Stages.StageNameByIndex(s)}의 주인", color = s == Stages.StageCount - 1 ? new Color(0.15f, 0.02f, 0.05f) : new Color(0.35f, 0.1f, 0.45f),
                });
            }
            return all;
        }
    }

    public static string MidBossId(int stage) => "mid_" + (stage + 1);
    public static string BossId(int stage) => "boss_" + (stage + 1);

    public static EnemyDef Find(string id) => All.Find(e => e.id == id);
}
