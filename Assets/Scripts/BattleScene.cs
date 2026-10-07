using System.Collections.Generic;
using UnityEngine;

// 레이어로 나뉜 전투 배경 (Resources/StageScenes/stage<번호>/).
//   background.jpg  움직이지 않는 배경
//   <조각>.png       바람에 흔들리는 풀 · 나뭇가지
//   layout.txt      원본 그림(1280x720) 기준 위치 (Art/import_scene.py가 만듦)
//     zoom=1.2            배경을 화면 아래쪽 기준으로 확대 (땅이 넓게 보이도록)
//     lanes=470,690       유닛이 다닐 땅의 위쪽 / 아래쪽 (원본 그림의 y)
//     sway=그림,폭,높이,고정점x,고정점y,장면x,장면y,위상,bend|rotate
// 이 폴더가 없는 스테이지는 예전처럼 Backgrounds/battle_<번호>.jpg 한 장을 씁니다.
public static class BattleScene
{
    public struct Result
    {
        public float laneTop, laneBottom; // 유닛이 다닐 땅 (월드 좌표)
    }

    public static bool TryBuild(int stageNumber, Transform world, Camera cam, out Result result)
    {
        result = default;
        string folder = "StageScenes/stage" + stageNumber + "/";
        var bg = Resources.Load<Texture2D>(folder + "background");
        var layout = Resources.Load<TextAsset>(folder + "layout");
        if (bg == null || layout == null) return false;

        Vector2 canvas = new Vector2(1280f, 720f);
        float zoom = 1f;
        Vector2 lanes = new Vector2(470f, 690f);
        var sways = new List<string[]>();
        foreach (var raw in layout.text.Split('\n'))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string key = line.Substring(0, eq);
            var parts = line.Substring(eq + 1).Split(',');
            switch (key)
            {
                case "canvas": canvas = new Vector2(F(parts[0]), F(parts[1])); break;
                case "zoom": zoom = F(parts[0]); break;
                case "lanes": lanes = new Vector2(F(parts[0]), F(parts[1])); break;
                case "sway": if (parts.Length >= 9) sways.Add(parts); break;
            }
        }

        // 원본 그림 1픽셀 = 월드 몇 칸인지. 화면 높이에 맞춘 뒤 zoom만큼 확대, 가로가 모자라면 더 확대
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        float unit = 2f * halfH * zoom / canvas.y;
        if (canvas.x * unit < 2f * halfW) unit = 2f * halfW / canvas.x;
        float camX = cam.transform.position.x, camY = cam.transform.position.y;
        // 그림 아래쪽을 화면 아래쪽에 맞춤 (위쪽 하늘이 조금 잘림)
        System.Func<float, float, Vector2> toWorld = (px, py) =>
            new Vector2(camX + (px - canvas.x / 2f) * unit, camY - halfH + (canvas.y - py) * unit);

        var bgGo = new GameObject("Backdrop");
        bgGo.transform.SetParent(world, false);
        var sr = bgGo.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(bg, new Rect(0, 0, bg.width, bg.height), new Vector2(0.5f, 0f), bg.height / (canvas.y * unit));
        sr.sortingOrder = -3000;
        bgGo.transform.position = toWorld(canvas.x / 2f, canvas.y);

        int index = 0;
        foreach (var p in sways)
        {
            var tex = Resources.Load<Texture2D>(folder + p[0].Trim());
            if (tex == null) continue;
            float w = F(p[1]), h = F(p[2]), ax = F(p[3]), ay = F(p[4]);
            bool bend = p[8].Trim() == "bend";
            var go = new GameObject("Sway_" + p[0].Trim());
            go.transform.SetParent(world, false);
            go.transform.position = toWorld(F(p[5]), F(p[6]));
            var s = go.AddComponent<SpriteRenderer>();
            // 고정점(풀은 뿌리, 가지는 붙은 곳)을 중심으로 흔들림
            s.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(ax / w, 1f - ay / h), tex.width / (w * unit));
            // 아래쪽 풀은 유닛보다 앞(가까운 풀), 위쪽 가지도 앞에 그림
            s.sortingOrder = bend ? 1500 + index : 1400 + index;
            var sway = go.AddComponent<Sway>();
            sway.phase = F(p[7]);
            sway.amplitude = bend ? 4f : 3f;
            sway.bend = bend;
            index++;
        }

        result.laneTop = toWorld(0f, lanes.x).y;
        result.laneBottom = toWorld(0f, lanes.y).y;
        return true;
    }

    static float F(string s)
    {
        float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
        return v;
    }
}

// 바람에 흔들리는 풀 · 가지. 고정점을 중심으로 천천히 기울었다 돌아옴 (전투 배속과 상관없이 같은 속도)
public class Sway : MonoBehaviour
{
    public float phase;
    public float amplitude = 3f; // 최대 기울기 (도)
    public bool bend;            // 풀: 기울면서 살짝 납작해짐

    void Update()
    {
        float t = Time.unscaledTime * Mathf.PI * 2f / 4f + phase; // 4초에 한 번 크게 흔들림
        float wind = Mathf.Sin(t) * 0.75f + Mathf.Sin(t * 2.3f + phase * 1.7f) * 0.25f;
        transform.localRotation = Quaternion.Euler(0f, 0f, -wind * amplitude);
        if (bend) transform.localScale = new Vector3(1f, 1f - Mathf.Abs(wind) * 0.03f, 1f);
    }
}
