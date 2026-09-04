using System;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
#endif

/// <summary>
/// Fateful Rush Google Play Games leaderboards.
///
/// - Submits a level time only when GameStateManager records a new local PB.
/// - Converts seconds to milliseconds because Play Games "Time" leaderboards
///   require millisecond integer scores.
/// - Re-syncs every locally recorded PB when Play Games authentication becomes
///   available, so a PB made offline is uploaded on a later sign-in.
/// - Supports the standard Play Games UI for all leaderboards or one level.
/// </summary>
public sealed class GooglePlayGamesLeaderboards : MonoBehaviour
{
    private static readonly int[] LeaderboardLevels =
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9,
        11, 12,
        14, 15, 16,
        18, 19, 20,
        22, 23,
        25,
        27, 28,
        30,
        32, 33,
        35, 36,
        38, 39
    };

    private const float AuthenticationPollInterval = 1f;
    private const int NoPendingUi = -1;
    private const int AllLeaderboardsUi = 0;

    private static GooglePlayGamesLeaderboards instance;

    private float nextAuthenticationPollTime;
    private bool wasAuthenticated;
    private int pendingLeaderboardUi = NoPendingUi;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static GooglePlayGamesLeaderboards EnsureInstance()
    {
        if (instance != null)
            return instance;

        GooglePlayGamesLeaderboards existing =
            FindAnyObjectByType<GooglePlayGamesLeaderboards>();

        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject root =
            new GameObject("GooglePlayGamesLeaderboards");

        instance =
            root.AddComponent<GooglePlayGamesLeaderboards>();

        DontDestroyOnLoad(root);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        PollAuthentication(force: true);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextAuthenticationPollTime)
            return;

        PollAuthentication(force: false);
    }

    /// <summary>
    /// Called when a new local PB is saved.
    /// If the player is offline/not authenticated, the local PB remains saved
    /// and will be uploaded automatically on a later authenticated launch.
    /// </summary>
    public static void SubmitBestTime(
        int levelNumber,
        float seconds)
    {
        EnsureInstance()
            .SubmitBestTimeInternal(
                levelNumber,
                seconds
            );
    }

    /// <summary>
    /// Opens the standard Google Play Games UI containing all 29 leaderboards.
    /// If sign-in is not ready yet, manual sign-in is requested first and the
    /// UI opens automatically when authentication succeeds.
    /// </summary>
    public static void ShowAllLeaderboardsUI()
    {
        EnsureInstance()
            .RequestLeaderboardUi(
                AllLeaderboardsUi
            );
    }

    /// <summary>
    /// Opens one specific level leaderboard.
    /// </summary>
    public static void ShowLevelLeaderboardUI(
        int levelNumber)
    {
        if (!FatefulRushLeaderboardIds.TryGetId(
                levelNumber,
                out _))
        {
            Debug.LogWarning(
                "[GooglePlayGamesLeaderboards] " +
                $"Level {levelNumber} has no Best Time leaderboard."
            );

            return;
        }

        EnsureInstance()
            .RequestLeaderboardUi(
                levelNumber
            );
    }

    /// <summary>
    /// Safe to call manually, but normally authentication detection invokes it.
    /// </summary>
    public static void SyncLocalBestTimes()
    {
        EnsureInstance()
            .SyncLocalBestTimesInternal();
    }

    private void PollAuthentication(
        bool force)
    {
        nextAuthenticationPollTime =
            Time.unscaledTime +
            AuthenticationPollInterval;

        bool authenticated =
            IsPlayGamesReady();

        if (authenticated &&
            (!wasAuthenticated || force))
        {
            SyncLocalBestTimesInternal();
        }

        if (authenticated &&
            pendingLeaderboardUi != NoPendingUi)
        {
            int requestedUi =
                pendingLeaderboardUi;

            pendingLeaderboardUi =
                NoPendingUi;

            OpenLeaderboardUiInternal(
                requestedUi
            );
        }

        wasAuthenticated =
            authenticated;
    }

    private void SubmitBestTimeInternal(
        int levelNumber,
        float seconds)
    {
        if (!TryBuildSubmission(
                levelNumber,
                seconds,
                out string leaderboardId,
                out long milliseconds))
        {
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsPlayGamesReady())
            return;

        PlayGamesPlatform.Instance.ReportScore(
            milliseconds,
            leaderboardId,
            success =>
            {
                if (!success)
                {
                    Debug.LogWarning(
                        "[GooglePlayGamesLeaderboards] " +
                        $"Score submission failed for Level {levelNumber}. " +
                        $"Time: {milliseconds} ms"
                    );
                }
            }
        );
#endif
    }

    private void SyncLocalBestTimesInternal()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsPlayGamesReady())
            return;

        for (int i = 0; i < LeaderboardLevels.Length; i++)
        {
            int levelNumber =
                LeaderboardLevels[i];

            string key =
                "BestTime_Level_" +
                levelNumber;

            float bestTime =
                PlayerPrefs.GetFloat(
                    key,
                    -1f
                );

            if (bestTime <= 0f)
                continue;

            SubmitBestTimeInternal(
                levelNumber,
                bestTime
            );
        }
#endif
    }

    private void RequestLeaderboardUi(
        int requestedLevel)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (IsPlayGamesReady())
        {
            OpenLeaderboardUiInternal(
                requestedLevel
            );

            return;
        }

        pendingLeaderboardUi =
            requestedLevel;

        // Reuse the game's existing PGS sign-in flow.
        GooglePlayGamesManager.ManualSignIn();
#else
        Debug.Log(
            "[GooglePlayGamesLeaderboards] " +
            "Leaderboard UI is only available in an Android player build."
        );
#endif
    }

    private void OpenLeaderboardUiInternal(
        int requestedLevel)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsPlayGamesReady())
        {
            pendingLeaderboardUi =
                requestedLevel;

            return;
        }

        if (requestedLevel ==
            AllLeaderboardsUi)
        {
            PlayGamesPlatform.Instance
                .ShowLeaderboardUI();

            return;
        }

        if (!FatefulRushLeaderboardIds.TryGetId(
                requestedLevel,
                out string leaderboardId))
        {
            Debug.LogWarning(
                "[GooglePlayGamesLeaderboards] " +
                $"No leaderboard ID for Level {requestedLevel}."
            );

            return;
        }

        PlayGamesPlatform.Instance
            .ShowLeaderboardUI(
                leaderboardId
            );
#else
        Debug.Log(
            "[GooglePlayGamesLeaderboards] " +
            "Leaderboard UI is only available in an Android player build."
        );
#endif
    }

    private static bool TryBuildSubmission(
        int levelNumber,
        float seconds,
        out string leaderboardId,
        out long milliseconds)
    {
        milliseconds = 0L;

        if (!FatefulRushLeaderboardIds.TryGetId(
                levelNumber,
                out leaderboardId))
        {
            return false;
        }

        if (seconds <= 0f ||
            float.IsNaN(seconds) ||
            float.IsInfinity(seconds))
        {
            Debug.LogWarning(
                "[GooglePlayGamesLeaderboards] " +
                $"Invalid best time for Level {levelNumber}: {seconds}"
            );

            return false;
        }

        milliseconds =
            Math.Max(
                1L,
                (long)Math.Round(
                    seconds * 1000d,
                    MidpointRounding.AwayFromZero
                )
            );

        return true;
    }

    private static bool IsPlayGamesReady()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return PlayGamesPlatform.Instance != null &&
               PlayGamesPlatform.Instance.IsAuthenticated();
#else
        return false;
#endif
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
