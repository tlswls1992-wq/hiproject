using System.Collections.Generic;
using UnityEngine;

// 타격감 묶음: 피해 숫자 · 치명타 · 멈칫(히트 스톱) · 화면 흔들림 · 마지막 처치 슬로 모션.
// 액션 RPG에서 흔히 보는, 눈이 피곤하지 않은 정도의 연출로 맞췄어요.
//  - 피해 숫자: 톡 튀어나왔다가 위로 떠오르며 사라짐. 일반은 흰색, 치명타는 크고 노랗게 "!", 동료가 받은 피해는 작은 붉은색, 회복은 초록 "+"
//  - 멈칫: 치명타 · 보스 공격 · 마지막 처치 때만 아주 잠깐 (자주 걸려 끊기지 않게 0.35초에 한 번까지)
//  - 화면 흔들림: 큰 공격에만 살짝, 금방 잦아듦
public class BattleFeel
{
    public const float CritChance = 0.12f;      // 동료 공격의 치명타 확률
    public const float CritMultiplier = 1.75f;  // 치명타 피해 배율

    class Popup
    {
        public Vector3 world;   // 처음 뜬 자리 (월드 좌표)
        public string text;
        public Color color;
        public int size;
        public float born, life, drift;
        public bool crit;
    }

    readonly List<Popup> popups = new List<Popup>();
    float hitStopLeft, hitStopReadyAt, slowLeft, slowScale = 1f, shake;
    Vector3 camBase;
    Camera cam;

    static float Now => Time.unscaledTime;

    public void Begin(Camera camera)
    {
        cam = camera;
        camBase = cam != null ? cam.transform.position : Vector3.zero;
        popups.Clear();
        hitStopLeft = slowLeft = shake = 0f;
    }

    public void End()
    {
        if (cam != null) cam.transform.position = camBase;
        popups.Clear();
        hitStopLeft = slowLeft = shake = 0f;
    }

    // ---------------- 피해 숫자 ----------------

    public void Damage(Vector3 at, float amount, bool crit, bool toAlly)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(amount));
        if (crit) Add(at, n + "!", new Color(1f, 0.84f, 0.28f), 30, 0.9f, true);
        else if (toAlly) Add(at, n.ToString(), new Color(1f, 0.48f, 0.42f), 18, 0.7f, false);
        else Add(at, n.ToString(), new Color(1f, 0.97f, 0.9f), 22, 0.7f, false);
    }

    public void Heal(Vector3 at, float amount)
    {
        int n = Mathf.RoundToInt(amount);
        if (n >= 1) Add(at, "+" + n, new Color(0.55f, 1f, 0.55f), 20, 0.8f, false);
    }

    void Add(Vector3 at, string text, Color color, int size, float life, bool crit)
    {
        if (popups.Count >= 60) popups.RemoveAt(0); // 너무 많으면 오래된 것부터
        popups.Add(new Popup
        {
            world = at, text = text, color = color, size = size, born = Now, life = life, crit = crit,
            drift = Random.Range(-14f, 14f),
        });
    }

    // ---------------- 멈칫 · 흔들림 · 슬로 ----------------

    // 아주 잠깐 거의 멈춤 (맞는 순간의 묵직함). 너무 자주 걸리지 않게 간격을 둠
    public void HitStop(float seconds)
    {
        if (Now < hitStopReadyAt) return;
        hitStopLeft = Mathf.Max(hitStopLeft, seconds);
        hitStopReadyAt = Now + 0.35f;
    }

    public void Shake(float amount) => shake = Mathf.Max(shake, amount);

    // 일정 시간 동안 느리게 (마지막 처치 등)
    public void Slow(float seconds, float scale)
    {
        slowLeft = Mathf.Max(slowLeft, seconds);
        slowScale = scale;
    }

    // 매 프레임: 시간 배율과 카메라 흔들림. speed = 전투 배속, active = 전투가 진행 중인지 (멈춤 · 끝이면 false)
    public void Update(int speed, bool active)
    {
        float dt = Time.unscaledDeltaTime;
        if (hitStopLeft > 0f) hitStopLeft -= dt;
        if (slowLeft > 0f) slowLeft -= dt;
        if (active)
        {
            float mult = hitStopLeft > 0f ? 0.05f : slowLeft > 0f ? slowScale : 1f;
            Time.timeScale = speed * mult;
        }
        if (cam == null) return;
        if (shake > 0.002f)
        {
            shake *= Mathf.Exp(-dt * 10f);
            var o = Random.insideUnitCircle * shake;
            cam.transform.position = camBase + new Vector3(o.x, o.y, 0f);
        }
        else if (cam.transform.position != camBase) cam.transform.position = camBase;
    }

    // 화면에 피해 숫자 그리기 (전투 화면 UI보다 먼저 그려서 위쪽 판에 가려지게)
    public void Draw()
    {
        if (cam == null) return;
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var p = popups[i];
            float t = (Now - p.born) / p.life;
            if (t >= 1f) { popups.RemoveAt(i); continue; }
            var s = UI.WorldToUI(cam, p.world);
            // 톡 튀어나옴 (처음엔 크게 → 원래 크기), 그다음 위로 떠오르며 흐려짐
            float pop = t < 0.12f ? Mathf.Lerp(p.crit ? 1.8f : 1.45f, 1f, t / 0.12f) : 1f;
            float rise = (1f - Mathf.Pow(1f - t, 2f)) * (p.crit ? 46f : 38f);
            float a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            int size = Mathf.RoundToInt(p.size * pop / 2f) * 2; // 짝수로 맞춰 글꼴 모양 캐시를 아낌
            var r = new Rect(s.x - 80f + p.drift * t, s.y - 26f - rise, 160f, 44f);
            UI.SharpText(r, p.text, size, UI.WithAlpha(p.color, a), new Color(0.08f, 0.04f, 0.02f, 0.9f * a));
        }
    }
}

// 쓰러진 적: 몸 그림이 살짝 뒤로 밀려나며 어두워지고 사라짐
public class Corpse : MonoBehaviour
{
    SpriteRenderer r;
    Vector3 start, push;
    Color baseColor;
    float born;
    const float Life = 0.45f;

    public static void From(SpriteRenderer body, Vector2 direction)
    {
        if (body == null || body.sprite == null) return;
        var go = new GameObject("Corpse");
        go.transform.SetParent(body.transform.parent != null ? body.transform.parent.parent : null, true);
        go.transform.position = body.transform.position;
        go.transform.localScale = body.transform.lossyScale;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = body.sprite;
        r.flipX = body.flipX;
        r.sortingOrder = body.sortingOrder;
        r.color = body.color;
        var c = go.AddComponent<Corpse>();
        c.r = r;
        c.start = go.transform.position;
        c.push = new Vector3(direction.x * 0.45f, 0.12f, 0f);
        c.baseColor = new Color(1f, 0.75f, 0.75f, 1f);
        c.born = Time.time;
    }

    void Update()
    {
        float t = (Time.time - born) / Life;
        if (t >= 1f) { Destroy(gameObject); return; }
        float e = 1f - (1f - t) * (1f - t);
        transform.position = start + push * e;
        float a = 1f - t;
        r.color = new Color(baseColor.r * (1f - 0.5f * t), baseColor.g * (1f - 0.6f * t), baseColor.b * (1f - 0.6f * t), a * a);
    }
}
