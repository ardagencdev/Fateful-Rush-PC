using System.Collections;
using UnityEngine;

/// <summary>
/// Marker + lifetime helper for one-shot sounds that are intentionally allowed
/// to finish after gameplay has ended (for example a lethal Space Bomb hit).
/// </summary>
[DisallowMultipleComponent]
public sealed class GameEndPersistentAudio : MonoBehaviour
{
    private Coroutine cleanupRoutine;

    public void DestroyAfterRealtime(float seconds)
    {
        if (cleanupRoutine != null)
            StopCoroutine(cleanupRoutine);

        cleanupRoutine = StartCoroutine(
            DestroyAfterRealtimeRoutine(Mathf.Max(0.05f, seconds))
        );
    }

    private IEnumerator DestroyAfterRealtimeRoutine(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Destroy(gameObject);
    }
}
