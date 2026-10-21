using UnityEngine;

// 궁수의 화살, 마법사의 마법 구슬처럼 날아가는 공격입니다.
public class Projectile : MonoBehaviour
{
    const float Speed = 12f;

    Unit target;
    Vector2 lastTargetPos;
    float damage;
    float splash;
    Team team;
    string impactSound;
    float startX;

    bool crit;

    public static void Launch(Unit from, Unit target, float damage, float splash, Color color, string impactSound = null, bool crit = false)
    {
        var go = new GameObject("Projectile");
        go.transform.SetParent(BattleManager.Instance.World, false);
        go.transform.position = from.transform.position;
        go.transform.localScale = Vector3.one * (splash > 0f ? 0.3f : 0.15f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle();
        sr.color = color;
        sr.sortingOrder = 1000; // 유닛들보다 항상 위에 그림

        var p = go.AddComponent<Projectile>();
        p.target = target;
        p.lastTargetPos = target.transform.position;
        p.damage = damage;
        p.splash = splash;
        p.team = from.team;
        p.impactSound = impactSound;
        p.startX = from.transform.position.x;
        p.crit = crit;
    }

    void Update()
    {
        var battle = BattleManager.Instance;
        if (battle == null) { Destroy(gameObject); return; }
        if (battle.IsPaused) return;

        if (target != null && target.IsAlive) lastTargetPos = target.transform.position;

        Vector2 next = Vector2.MoveTowards(transform.position, lastTargetPos, Speed * Time.deltaTime);
        transform.position = next;
        if ((next - lastTargetPos).sqrMagnitude < 0.0001f)
        {
            bool alive = target != null && target.IsAlive;
            battle.ApplyHit(lastTargetPos, alive ? target : null, team, damage, splash, crit);
            if (splash <= 0f) HitFx.Spawn(HitFx.Kind.Spark, alive ? target.HitPoint : lastTargetPos, lastTargetPos.x >= startX, crit ? 1.5f : 1f);
            AudioManager.Play(impactSound, crit ? 0.6f : 0.42f, 0.07f);
            if (crit) { battle.Feel.HitStop(0.04f); battle.Feel.Shake(0.05f); }
            Destroy(gameObject);
        }
    }
}
