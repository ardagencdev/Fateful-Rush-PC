using System.Collections;
using UnityEngine;

public class SpawnScaleEffect : MonoBehaviour
{
    [Header("Collect Particle")]
    public GameObject collectParticlePrefab;

    [Header("Spawn Effect")]
    public float spawnDuration = 0.2f;

    [Header("Collect Effect")]
    public float collectDuration = 0.12f;

    private Vector3 targetScale;
    private bool isCollecting;
    private Coroutine activeRoutine;

    private void Awake()
    {
        targetScale = transform.localScale;
    }

    private void OnEnable()
    {
        isCollecting = false;
        transform.localScale = Vector3.zero;

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(SpawnEffect());
    }

    private IEnumerator SpawnEffect()
    {
        float duration = Mathf.Max(0.01f, spawnDuration);
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = SmoothStep(Mathf.Clamp01(time / duration));

            transform.localScale =
                Vector3.Lerp(Vector3.zero, targetScale, t);

            yield return null;
        }

        transform.localScale = targetScale;
        activeRoutine = null;
    }

    public void Collect()
    {
        if (isCollecting)
            return;

        isCollecting = true;

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(CollectEffect());
    }

    private IEnumerator CollectEffect()
    {
        Vector3 startScale = transform.localScale;
        float duration = Mathf.Max(0.01f, collectDuration);
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(time / duration);
            t *= t;

            transform.localScale =
                Vector3.Lerp(startScale, Vector3.zero, t);

            yield return null;
        }

        transform.localScale = Vector3.zero;

        if (collectParticlePrefab != null)
        {
            GameObject particle = RuntimeObjectPool.Spawn(
                collectParticlePrefab,
                transform.position,
                Quaternion.identity
            );

            PooledParticleAutoRelease.Arm(particle);
        }

        activeRoutine = null;

        GameObject rootObject =
            transform.parent != null
                ? transform.parent.gameObject
                : gameObject;

        RuntimeObjectPool.Release(rootObject);
    }

    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private void OnDisable()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        isCollecting = false;
        transform.localScale = targetScale;
    }
}
