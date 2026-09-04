using UnityEngine;
using UnityEngine.SceneManagement;

public static class NearMissFeedback
{
    // A new Near Miss inside this window continues the same streak.
    public const float StreakTimeout = 3.00f;

    // Visual/camera intensity reaches its maximum at this streak.
    // The actual counter itself has no cap.
    private const int MaxVisualStreak = 6;

    private static float lastNearMissTime = -100f;
    private static int currentStreak;

    private static PlayerMovement cachedPlayer;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterAssembliesLoaded
    )]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResetState();
    }

    private static void OnSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        ResetState();
    }

    private static void ResetState()
    {
        lastNearMissTime = -100f;
        currentStreak = 0;
        cachedPlayer = null;
    }

    public static bool TryTrigger(
        Vector3 dangerWorldPosition,
        float closeness01)
    {
        if (!GameStateManager.IsGameplayStarted ||
            GameStateManager.IsGameplayEnded ||
            Time.timeScale <= 0f)
        {
            return false;
        }

        float now = Time.unscaledTime;

        float closeness =
            Mathf.Clamp01(closeness01);

        UpdateStreak(now);
        StatsManager.AddNearMiss(currentStreak);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayNearMissSound(
                dangerWorldPosition,
                closeness
            );
        }

        if (VibrationManager.Instance != null)
        {
            VibrationManager.Instance.VibrateNearMiss(
                closeness
            );
        }

        PlayerMovement player = GetPlayerMovement();
        player?.ApplyNearMissBoost();

        NearMissStreakUI.ShowNearMiss(
            currentStreak,
            closeness
        );

        PlayCameraShake(
            currentStreak,
            closeness
        );

        return true;
    }

    private static void UpdateStreak(float now)
    {
        if (now - lastNearMissTime <= StreakTimeout)
            currentStreak++;
        else
            currentStreak = 1;

        lastNearMissTime = now;
    }

    private static PlayerMovement GetPlayerMovement()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer =
                Object.FindAnyObjectByType<PlayerMovement>();
        }

        return cachedPlayer;
    }

    private static void PlayCameraShake(
        int streak,
        float closeness01)
    {
        if (CameraShake.Instance == null)
            return;

        float streak01 = Mathf.InverseLerp(
            1f,
            MaxVisualStreak,
            Mathf.Clamp(streak, 1, MaxVisualStreak)
        );

        // A very close Near Miss gets a slightly sharper hit,
        // while the streak is still the main source of escalation.
        float closenessFactor =
            Mathf.Lerp(0.82f, 1f, closeness01);

        float duration =
            Mathf.Lerp(0.050f, 0.090f, streak01) *
            closenessFactor;

        float strength =
            Mathf.Lerp(0.022f, 0.065f, streak01) *
            closenessFactor;

        // Soft shake never interrupts a stronger boss/death impact.
        CameraShake.Instance.ShakeSoft(
            duration,
            strength
        );
    }

    public static float GetCloseness01(
        float closestSurfaceDistance,
        float nearMissDistance)
    {
        if (nearMissDistance <= 0f)
            return 0f;

        return Mathf.Clamp01(
            1f -
            Mathf.Max(0f, closestSurfaceDistance) /
            nearMissDistance
        );
    }
}
