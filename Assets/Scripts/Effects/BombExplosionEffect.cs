using UnityEngine;
using System.Collections;

public class BombExplosionEffect : MonoBehaviour
{
    public float duration = 0.25f;
    public float startScale = 0.2f;
    public float endScale = 2.2f;

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        StartCoroutine(ExplosionRoutine());
    }

    private IEnumerator ExplosionRoutine()
    {
        Vector3 start = Vector3.one * startScale;
        Vector3 end = Vector3.one * endScale;

        Color startColor = sr != null ? sr.color : Color.white;
        Color endColor = startColor;
        endColor.a = 0f;

        float time = 0f;

        while (time < duration)
        {
            time += GameStateManager.IsGameplayEnded
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            float t = time / duration;

            transform.localScale = Vector3.Lerp(start, end, t);

            if (sr != null)
                sr.color = Color.Lerp(startColor, endColor, t);

            yield return null;
        }

        Destroy(gameObject);
    }
}