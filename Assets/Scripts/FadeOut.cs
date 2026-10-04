using UnityEngine;

// 폭발 효과처럼 잠깐 보였다가 서서히 사라지는 그림에 붙입니다.
public class FadeOut : MonoBehaviour
{
    public float duration = 0.25f;

    float elapsed;
    SpriteRenderer sr;
    Color startColor;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        startColor = sr.color;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float k = 1f - elapsed / duration;
        if (k <= 0f) { Destroy(gameObject); return; }
        sr.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * k);
    }
}
