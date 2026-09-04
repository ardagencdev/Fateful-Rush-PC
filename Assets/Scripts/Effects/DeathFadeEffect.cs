using System.Collections;
using UnityEngine;

public class DeathFadeEffect : MonoBehaviour
{
    [Header("Fade Settings")]
    public float fadeDuration = 0.45f;
    public bool destroyAfterFade = true;

    private SpriteRenderer[] spriteRenderers;
    private Rigidbody2D rb;
    private Collider2D[] colliders;

    private bool isPlaying;

    private void Awake()
    {
        spriteRenderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        rb = GetComponent<Rigidbody2D>();

        colliders =
            GetComponentsInChildren<Collider2D>(true);
    }

    public void Play()
    {
        if (isPlaying)
            return;

        isPlaying = true;

        DisablePhysics();

        StopAllCoroutines();
        StartCoroutine(FadeRoutine());
    }

    private void DisablePhysics()
    {
        if (colliders != null)
        {
            foreach (Collider2D col in colliders)
            {
                if (col != null)
                    col.enabled = false;
            }
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }
    }

    private IEnumerator FadeRoutine()
    {
        if (spriteRenderers == null ||
            spriteRenderers.Length == 0)
        {
            if (destroyAfterFade)
                Destroy(gameObject);

            yield break;
        }

        float safeDuration =
            Mathf.Max(0.01f, fadeDuration);

        Color[] startColors =
            new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                startColors[i] = spriteRenderers[i].color;
        }

        Vector3 startScale =
            transform.localScale;

        Vector3 targetScale =
            Vector3.zero;

        float time = 0f;

        while (time < safeDuration)
        {
            time += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(time / safeDuration);

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer currentRenderer =
                    spriteRenderers[i];

                if (currentRenderer == null)
                    continue;

                Color targetColor =
                    new Color(
                        0f,
                        0f,
                        0f,
                        0f
                    );

                currentRenderer.color =
                    Color.Lerp(
                        startColors[i],
                        targetColor,
                        t
                    );
            }

            transform.localScale =
                Vector3.Lerp(
                    startScale,
                    targetScale,
                    t
                );

            yield return null;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer currentRenderer =
                spriteRenderers[i];

            if (currentRenderer == null)
                continue;

            currentRenderer.color =
                new Color(
                    0f,
                    0f,
                    0f,
                    0f
                );
        }

        transform.localScale =
            targetScale;

        if (destroyAfterFade)
            Destroy(gameObject);
    }
}