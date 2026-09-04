using System.Collections;
using UnityEngine;

public class SpawnPopEffect : MonoBehaviour
{
    [Header("Spawn Effect")]
    public float spawnDuration = 0.25f;
    public float overshoot = 1.12f;

    [Header("Behaviour")]
    public bool playOnStart = false;

    private Vector3 targetScale;
    private Coroutine routine;

    private void Awake()
    {
        targetScale = transform.localScale;
        HideInstant();
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    public void HideInstant()
    {
        StopCurrentRoutine();
        transform.localScale = Vector3.zero;
    }

    public void ShowInstant()
    {
        StopCurrentRoutine();
        transform.localScale = targetScale;
    }

    public void Play()
    {
        StopCurrentRoutine();
        routine = StartCoroutine(PlayRoutine());
    }

    public IEnumerator PlayAndWait()
    {
        StopCurrentRoutine();

        routine = StartCoroutine(PlayRoutine());

        yield return routine;
    }

    private IEnumerator PlayRoutine()
    {
        yield return SpawnRoutine();
        routine = null;
    }

    private IEnumerator SpawnRoutine()
    {
        float duration = Mathf.Max(0.01f, spawnDuration);
        float safeOvershoot = Mathf.Max(1f, overshoot);

        transform.localScale = Vector3.zero;

        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(time / duration);

            float scale =
                Mathf.Sin(t * Mathf.PI * 0.5f) *
                safeOvershoot;

            transform.localScale =
                targetScale * scale;

            yield return null;
        }

        transform.localScale = targetScale;
    }

    private void StopCurrentRoutine()
    {
        if (routine == null)
            return;

        StopCoroutine(routine);
        routine = null;
    }

    private void OnDisable()
    {
        StopCurrentRoutine();
    }
}