#if UNITY_ANDROID && !UNITY_EDITOR
using System.Collections;
using UnityEngine;

/// <summary>
/// Re-applies PC-only runtime policy after Android's activity and HPE feature
/// detection are definitely available. This prevents an early mobile 60 FPS
/// preference from accidentally sticking on Google Play Games on PC.
/// </summary>
public sealed class GpgPcRuntimeBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<GpgPcRuntimeBootstrap>() != null)
            return;

        GameObject host = new GameObject("[GPG PC] Runtime Policy");
        DontDestroyOnLoad(host);
        host.AddComponent<GpgPcRuntimeBootstrap>();
    }

    private IEnumerator Start()
    {
        float deadline = Time.realtimeSinceStartup + 5f;

        while (!RuntimePerformancePolicy.IsGooglePlayGamesOnPC &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForSecondsRealtime(0.25f);
        }

        if (!RuntimePerformancePolicy.IsGooglePlayGamesOnPC)
        {
            Destroy(gameObject);
            yield break;
        }

        Application.runInBackground = true;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        RuntimePerformancePolicy.ApplyFrameRate(60);

        Debug.Log(
            "[GPG PC] Runtime policy active. App target FPS: " +
            Application.targetFrameRate +
            " | Unity-reported refresh: " +
            RuntimePerformancePolicy.GetDisplayRefreshRate() +
            " Hz. The Google Play Games client can still apply its own " +
            "per-game refresh limit from Shift+Tab > Visual settings."
        );
    }
}
#endif
