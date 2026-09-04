using UnityEngine;

/// <summary>
/// Flushes deferred gameplay statistics at safe lifecycle points.
/// No scene setup is required.
/// </summary>
public sealed class StatsPersistenceHook : MonoBehaviour
{
    private static StatsPersistenceHook instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject hookObject = new GameObject("Stats Persistence Hook");
        instance = hookObject.AddComponent<StatsPersistenceHook>();
        DontDestroyOnLoad(hookObject);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            StatsManager.SaveIfDirty();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            StatsManager.SaveIfDirty();
    }

    private void OnApplicationQuit()
    {
        StatsManager.SaveIfDirty();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
