using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

public sealed class GooglePlayGamesManager : MonoBehaviour
{
    private enum AchievementKey
    {
        FirstRush,
        WarmingUp,
        HalfwayThere,
        NoTurningBack,
        FateDefied,
        CloseCall,
        ThreadTheNeedle,
        LivingOnTheEdge,
        Untouchable,
        ComboMaster,
        MagneticAttraction,
        FirstContact,
        DivideAndConquer,
        BehindCover,
        DarkFate,
        GoldenFate,
        StillStanding,
        TooStubbornToQuit,
        EchoInitiate,
        DoubleTrouble,
        ShadowArmy,
        QuickReflexes,
        BlinkAndYouMissMe,
        BornToRush,
        Counterattack,
        Payback,
        Reaper,
        PocketChange,
        TreasureHunter,
        FortuneFavorsTheFast,
        SuitUp,
        IronResolve,
        TimeBender,
        MasterOfTime,
        BadStep,
        BombMagnet,
        GroundZero,
        BurnedOnce,
        LightShowCasualty,
        Laserproof
    }

    private const int NearMiss10Target = 10;
    private const int NearMiss50Target = 50;
    private const int NearMiss250Target = 250;
    private const int MagnetCoinTarget = 100;

    private const int Death50Target = 50;
    private const int Death100Target = 100;

    private const int Ability10Target = 10;
    private const int Ability50Target = 50;
    private const int Ability250Target = 250;

    private const int ArmorKill10Target = 10;
    private const int ArmorKill50Target = 50;
    private const int ArmorKill100Target = 100;

    private const int Coin1000Target = 1000;
    private const int Coin5000Target = 5000;
    private const int Coin10000Target = 10000;

    private const int ArmorUse50Target = 50;
    private const int ArmorUse100Target = 100;
    private const int SlowUse50Target = 50;
    private const int SlowUse100Target = 100;

    private const int BombDeath10Target = 10;
    private const int BombDeath25Target = 25;
    private const int LaserDeath10Target = 10;
    private const int LaserDeath25Target = 25;

    private static readonly bool DiagnosticsEnabled = false;

    private static GooglePlayGamesManager instance;

    private bool authenticationStarted;
    private bool authenticated;

    private string lastAuthenticationStatus = "NotStarted";
    private string lastAchievementsUiStatus = "NotRequested";

    public static bool IsAuthenticated =>
        instance != null && instance.authenticated;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // Run save-generation migration before authentication. This prevents
        // closed-beta progress from being pushed back to Google Play on the
        // first production launch.
        ReleaseSaveMigration.ApplyIfNeeded();

        EnsureInstance();
    }

    private static GooglePlayGamesManager EnsureInstance()
    {
        if (instance != null)
            return instance;

        GooglePlayGamesManager existing =
            FindAnyObjectByType<GooglePlayGamesManager>();

        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject root =
            new GameObject("GooglePlayGamesManager");

        instance =
            root.AddComponent<GooglePlayGamesManager>();

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
        Initialize();
    }

    public void Initialize()
    {
        if (authenticationStarted)
            return;

        authenticationStarted = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Public release: diagnostic logs/toasts are disabled.
        PlayGamesPlatform.DebugLogEnabled = DiagnosticsEnabled;

        LogDiagnostic(
            "Initialize -> starting automatic authentication."
        );

        PlayGamesPlatform.Instance.Authenticate(
            ProcessAuthentication
        );
#else
        authenticated = false;
        lastAuthenticationStatus = "NotAndroidPlayer";
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void ProcessAuthentication(
        SignInStatus status)
    {
        lastAuthenticationStatus = status.ToString();
        authenticated =
            status == SignInStatus.Success;

        SaveDiagnosticState();

        LogDiagnostic(
            "Automatic authentication result: " + status +
            " | platformAuthenticated=" +
            PlayGamesPlatform.Instance.IsAuthenticated()
        );

        if (!authenticated)
        {
            Debug.LogWarning(
                "[GooglePlayGames] Automatic authentication failed: " +
                status
            );

            return;
        }

        Debug.Log(
            "[GooglePlayGames] Authenticated successfully."
        );

        BeginCloudSyncThenSyncAchievements();
    }
#endif

    public static void ManualSignIn()
    {
        EnsureInstance().ManualSignInInternal(false);
    }

    public static void ShowAchievementsUI()
    {
        GooglePlayGamesManager manager =
            EnsureInstance();

#if UNITY_ANDROID && !UNITY_EDITOR
        manager.LogDiagnostic(
            "Achievements button pressed. " +
            manager.BuildDiagnosticSummary()
        );

        manager.ShowAchievementsUIInternal();
#else
        manager.LogDiagnostic(
            "Achievements button pressed outside an Android player build."
        );
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void ShowAchievementsUIInternal()
    {
        bool platformAuthenticated =
            PlayGamesPlatform.Instance.IsAuthenticated();

        LogDiagnostic(
            "ShowAchievementsUIInternal -> " +
            "localAuthenticated=" + authenticated +
            " | platformAuthenticated=" + platformAuthenticated +
            " | authStatus=" + lastAuthenticationStatus
        );

        if (!IsReady())
        {
            ShowDiagnosticToast(
                "PGS not authenticated. Trying manual sign-in..."
            );

            ManualSignInInternal(true);
            return;
        }

        ShowDiagnosticToast(
            "PGS authenticated. Opening achievements..."
        );

        PlayGamesPlatform.Instance.ShowAchievementsUI(
            ProcessAchievementsUiStatus
        );
    }

    private void ProcessAchievementsUiStatus(
        UIStatus status)
    {
        lastAchievementsUiStatus = status.ToString();
        SaveDiagnosticState();

        LogDiagnostic(
            "Achievements UI callback: " + status +
            " | " + BuildDiagnosticSummary()
        );

        ShowDiagnosticToast(
            "Achievements UI: " + status
        );
    }
#endif

    private void ManualSignInInternal(
        bool showAchievementsAfterSignIn)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bool platformAuthenticated =
            PlayGamesPlatform.Instance.IsAuthenticated();

        LogDiagnostic(
            "ManualSignInInternal requested. " +
            "localAuthenticated=" + authenticated +
            " | platformAuthenticated=" + platformAuthenticated +
            " | showAchievementsAfterSignIn=" +
            showAchievementsAfterSignIn
        );

        if (authenticated || platformAuthenticated)
        {
            authenticated = true;
            lastAuthenticationStatus = SignInStatus.Success.ToString();
            SaveDiagnosticState();
            BeginCloudSyncThenSyncAchievements(
                showAchievementsAfterSignIn
                    ? (System.Action)ShowAchievementsUIInternal
                    : null
            );

            return;
        }

        PlayGamesPlatform.Instance.ManuallyAuthenticate(
            status =>
            {
                lastAuthenticationStatus = status.ToString();
                authenticated =
                    status == SignInStatus.Success;

                SaveDiagnosticState();

                LogDiagnostic(
                    "Manual authentication result: " + status +
                    " | platformAuthenticated=" +
                    PlayGamesPlatform.Instance.IsAuthenticated()
                );

                ShowDiagnosticToast(
                    "PGS sign-in: " + status
                );

                if (authenticated)
                {
                    BeginCloudSyncThenSyncAchievements(
                        showAchievementsAfterSignIn
                            ? (System.Action)ShowAchievementsUIInternal
                            : null
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[GooglePlayGames] Manual authentication failed: " +
                        status
                    );
                }
            }
        );
#endif
    }

    public static void NotifyLevelCompleted(
        int levelNumber)
    {
        GooglePlayGamesManager manager =
            EnsureInstance();

        switch (levelNumber)
        {
            case 1:
                manager.Unlock(AchievementKey.FirstRush);
                break;

            case 10:
                manager.Unlock(AchievementKey.WarmingUp);
                break;

            case 20:
                manager.Unlock(AchievementKey.HalfwayThere);
                break;

            case 30:
                manager.Unlock(AchievementKey.NoTurningBack);
                break;

            case 40:
                manager.Unlock(AchievementKey.FateDefied);
                break;
        }
    }

    public static void NotifyNearMissTotal(
        int totalNearMisses)
    {
        GooglePlayGamesManager manager = EnsureInstance();
        int safeTotal = Mathf.Max(0, totalNearMisses);

        if (safeTotal >= 1)
            manager.Unlock(AchievementKey.CloseCall);

        manager.PushProgressIfUseful(
            AchievementKey.ThreadTheNeedle,
            safeTotal,
            NearMiss10Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.LivingOnTheEdge,
            safeTotal,
            NearMiss50Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.Untouchable,
            safeTotal,
            NearMiss250Target
        );
    }

    public static void NotifyComboReached(
        int comboMultiplier)
    {
        if (comboMultiplier < 6)
            return;

        EnsureInstance().Unlock(
            AchievementKey.ComboMaster
        );
    }

    public static void NotifyMagnetCoinTotal(
        int totalMagnetCoins)
    {
        EnsureInstance().PushProgressIfUseful(
            AchievementKey.MagneticAttraction,
            Mathf.Max(0, totalMagnetCoins),
            MagnetCoinTarget
        );
    }

    public static void NotifyTotalDeaths(
        int totalDeaths)
    {
        GooglePlayGamesManager manager = EnsureInstance();
        int safeTotal = Mathf.Max(0, totalDeaths);

        manager.PushProgressIfUseful(
            AchievementKey.StillStanding,
            safeTotal,
            Death50Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.TooStubbornToQuit,
            safeTotal,
            Death100Target
        );
    }

    public static void NotifyCloneUseTotal(
        int totalUses)
    {
        GooglePlayGamesManager manager = EnsureInstance();
        int safeTotal = Mathf.Max(0, totalUses);

        manager.PushProgressIfUseful(
            AchievementKey.EchoInitiate,
            safeTotal,
            Ability10Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.DoubleTrouble,
            safeTotal,
            Ability50Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.ShadowArmy,
            safeTotal,
            Ability250Target
        );
    }

    public static void NotifyDashUseTotal(
        int totalUses)
    {
        GooglePlayGamesManager manager = EnsureInstance();
        int safeTotal = Mathf.Max(0, totalUses);

        manager.PushProgressIfUseful(
            AchievementKey.QuickReflexes,
            safeTotal,
            Ability10Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.BlinkAndYouMissMe,
            safeTotal,
            Ability50Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.BornToRush,
            safeTotal,
            Ability250Target
        );
    }

    public static void NotifyArmorEnemyKillTotal(
        int totalKills)
    {
        GooglePlayGamesManager manager = EnsureInstance();
        int safeTotal = Mathf.Max(0, totalKills);

        manager.PushProgressIfUseful(
            AchievementKey.Counterattack,
            safeTotal,
            ArmorKill10Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.Payback,
            safeTotal,
            ArmorKill50Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.Reaper,
            safeTotal,
            ArmorKill100Target
        );
    }

    public static void NotifyTotalCoins(
        int totalCoins)
    {
        GooglePlayGamesManager manager = EnsureInstance();
        int safeTotal = Mathf.Max(0, totalCoins);

        manager.PushProgressIfUseful(
            AchievementKey.PocketChange,
            safeTotal,
            Coin1000Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.TreasureHunter,
            safeTotal,
            Coin5000Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.FortuneFavorsTheFast,
            safeTotal,
            Coin10000Target
        );
    }

    public static void NotifyArmorUseTotal(
        int totalUses)
    {
        GooglePlayGamesManager manager = EnsureInstance();
        int safeTotal = Mathf.Max(0, totalUses);

        manager.PushProgressIfUseful(
            AchievementKey.SuitUp,
            safeTotal,
            ArmorUse50Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.IronResolve,
            safeTotal,
            ArmorUse100Target
        );
    }

    public static void NotifySlowUseTotal(
        int totalUses)
    {
        GooglePlayGamesManager manager = EnsureInstance();
        int safeTotal = Mathf.Max(0, totalUses);

        manager.PushProgressIfUseful(
            AchievementKey.TimeBender,
            safeTotal,
            SlowUse50Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.MasterOfTime,
            safeTotal,
            SlowUse100Target
        );
    }

    public static void NotifySpaceBombTriggerTotal(
        int totalTriggers)
    {
        if (totalTriggers < 1)
            return;

        EnsureInstance().Unlock(
            AchievementKey.BadStep
        );
    }

    public static void NotifySpaceBombDeathTotal(
        int totalDeaths)
    {
        GooglePlayGamesManager manager = EnsureInstance();
        int safeTotal = Mathf.Max(0, totalDeaths);

        manager.PushProgressIfUseful(
            AchievementKey.BombMagnet,
            safeTotal,
            BombDeath10Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.GroundZero,
            safeTotal,
            BombDeath25Target
        );
    }

    public static void NotifyLaserDeathTotal(
        int totalDeaths)
    {
        GooglePlayGamesManager manager = EnsureInstance();
        int safeTotal = Mathf.Max(0, totalDeaths);

        if (safeTotal >= 1)
            manager.Unlock(AchievementKey.BurnedOnce);

        manager.PushProgressIfUseful(
            AchievementKey.LightShowCasualty,
            safeTotal,
            LaserDeath10Target
        );

        manager.PushProgressIfUseful(
            AchievementKey.Laserproof,
            safeTotal,
            LaserDeath25Target
        );
    }

    public static void NotifyBossEncounter()
    {
        EnsureInstance().Unlock(
            AchievementKey.FirstContact
        );
    }

    public static void NotifyBossSplit()
    {
        EnsureInstance().Unlock(
            AchievementKey.DivideAndConquer
        );
    }

    public static void NotifyBossAoeEvade()
    {
        EnsureInstance().Unlock(
            AchievementKey.BehindCover
        );
    }

    public static void NotifySkinEquipped(
        string skinId)
    {
        if (string.IsNullOrWhiteSpace(skinId))
            return;

        string normalized = skinId
            .Trim()
            .ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        if (normalized == "dark" ||
            normalized == "black")
        {
            EnsureInstance().Unlock(
                AchievementKey.DarkFate
            );

            return;
        }

        if (normalized == "gold" ||
            normalized == "golden")
        {
            EnsureInstance().Unlock(
                AchievementKey.GoldenFate
            );
        }
    }

    private void BeginCloudSyncThenSyncAchievements(
        System.Action onFinished = null)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        FatefulRushCloudSave.SyncAfterAuthentication(
            () =>
            {
                SyncProgressFromLocalSave();
                GooglePlayGamesLeaderboards.SyncLocalBestTimes();
                onFinished?.Invoke();
            }
        );
#else
        SyncProgressFromLocalSave();
        onFinished?.Invoke();
#endif
    }

    private void SyncProgressFromLocalSave()
    {
        if (!IsReady())
            return;

        SyncLevelAchievement(1, AchievementKey.FirstRush);
        SyncLevelAchievement(10, AchievementKey.WarmingUp);
        SyncLevelAchievement(20, AchievementKey.HalfwayThere);
        SyncLevelAchievement(30, AchievementKey.NoTurningBack);
        SyncLevelAchievement(40, AchievementKey.FateDefied);

        int nearMisses = StatsManager.GetNearMisses();

        if (nearMisses >= 1)
            Unlock(AchievementKey.CloseCall);

        SetStepsAtLeast(
            AchievementKey.ThreadTheNeedle,
            nearMisses,
            NearMiss10Target
        );

        SetStepsAtLeast(
            AchievementKey.LivingOnTheEdge,
            nearMisses,
            NearMiss50Target
        );

        SetStepsAtLeast(
            AchievementKey.Untouchable,
            nearMisses,
            NearMiss250Target
        );

        if (StatsManager.GetHighestCombo() >= 6)
            Unlock(AchievementKey.ComboMaster);

        SetStepsAtLeast(
            AchievementKey.MagneticAttraction,
            StatsManager.GetMagnetCoins(),
            MagnetCoinTarget
        );

        SetStepsAtLeast(
            AchievementKey.StillStanding,
            StatsManager.GetActualDeaths(),
            Death50Target
        );

        SetStepsAtLeast(
            AchievementKey.TooStubbornToQuit,
            StatsManager.GetActualDeaths(),
            Death100Target
        );

        SyncAbilityProgress(
            StatsManager.GetCloneUses(),
            AchievementKey.EchoInitiate,
            AchievementKey.DoubleTrouble,
            AchievementKey.ShadowArmy
        );

        SyncAbilityProgress(
            StatsManager.GetDashUses(),
            AchievementKey.QuickReflexes,
            AchievementKey.BlinkAndYouMissMe,
            AchievementKey.BornToRush
        );

        int armorEnemyKills = StatsManager.GetArmorEnemyKills();

        SetStepsAtLeast(
            AchievementKey.Counterattack,
            armorEnemyKills,
            ArmorKill10Target
        );

        SetStepsAtLeast(
            AchievementKey.Payback,
            armorEnemyKills,
            ArmorKill50Target
        );

        SetStepsAtLeast(
            AchievementKey.Reaper,
            armorEnemyKills,
            ArmorKill100Target
        );

        int totalCoins = StatsManager.GetTotalCoins();

        SetStepsAtLeast(
            AchievementKey.PocketChange,
            totalCoins,
            Coin1000Target
        );

        SetStepsAtLeast(
            AchievementKey.TreasureHunter,
            totalCoins,
            Coin5000Target
        );

        SetStepsAtLeast(
            AchievementKey.FortuneFavorsTheFast,
            totalCoins,
            Coin10000Target
        );

        int armorUses = StatsManager.GetArmorBuffUses();

        SetStepsAtLeast(
            AchievementKey.SuitUp,
            armorUses,
            ArmorUse50Target
        );

        SetStepsAtLeast(
            AchievementKey.IronResolve,
            armorUses,
            ArmorUse100Target
        );

        int slowUses = StatsManager.GetSlowBuffUses();

        SetStepsAtLeast(
            AchievementKey.TimeBender,
            slowUses,
            SlowUse50Target
        );

        SetStepsAtLeast(
            AchievementKey.MasterOfTime,
            slowUses,
            SlowUse100Target
        );

        int bombDeaths = StatsManager.GetDeathCauseCount("SPACE BOMB");
        int bombTriggers = StatsManager.GetSpaceBombTriggers();

        if (bombTriggers > 0 || bombDeaths > 0)
            Unlock(AchievementKey.BadStep);

        SetStepsAtLeast(
            AchievementKey.BombMagnet,
            bombDeaths,
            BombDeath10Target
        );

        SetStepsAtLeast(
            AchievementKey.GroundZero,
            bombDeaths,
            BombDeath25Target
        );

        int laserDeaths = StatsManager.GetLaserDeaths();

        if (laserDeaths > 0)
            Unlock(AchievementKey.BurnedOnce);

        SetStepsAtLeast(
            AchievementKey.LightShowCasualty,
            laserDeaths,
            LaserDeath10Target
        );

        SetStepsAtLeast(
            AchievementKey.Laserproof,
            laserDeaths,
            LaserDeath25Target
        );

        if (StatsManager.GetBossEncounters() > 0)
            Unlock(AchievementKey.FirstContact);

        if (StatsManager.GetBossSplits() > 0)
            Unlock(AchievementKey.DivideAndConquer);

        if (StatsManager.GetBossAoeEvades() > 0)
            Unlock(AchievementKey.BehindCover);

        string selectedSkinId =
            PlayerPrefs.GetString(
                PlayerSkinCatalog.SelectedSkinKey,
                string.Empty
            );

        NotifySkinEquipped(selectedSkinId);
    }

    private void SyncAbilityProgress(
        int totalUses,
        AchievementKey tenKey,
        AchievementKey fiftyKey,
        AchievementKey twoHundredFiftyKey)
    {
        SetStepsAtLeast(
            tenKey,
            totalUses,
            Ability10Target
        );

        SetStepsAtLeast(
            fiftyKey,
            totalUses,
            Ability50Target
        );

        SetStepsAtLeast(
            twoHundredFiftyKey,
            totalUses,
            Ability250Target
        );
    }

    private void SyncLevelAchievement(
        int levelNumber,
        AchievementKey key)
    {
        bool completed =
            PlayerPrefs.GetInt(
                "CompletedLevel_" + levelNumber,
                0
            ) == 1;

        if (completed)
            Unlock(key);
    }

    private void PushProgressIfUseful(
        AchievementKey key,
        int currentSteps,
        int targetSteps)
    {
        int safeSteps = Mathf.Max(0, currentSteps);
        int safeTarget = Mathf.Max(1, targetSteps);

        if (!ShouldPushProgress(safeSteps, safeTarget))
            return;

        SetStepsAtLeast(
            key,
            safeSteps,
            safeTarget
        );
    }

    private static bool ShouldPushProgress(
        int currentSteps,
        int targetSteps)
    {
        if (currentSteps <= 0)
            return false;

        if (currentSteps == 1 || currentSteps >= targetSteps)
            return true;

        int interval;

        if (targetSteps <= 10)
            interval = 2;
        else if (targetSteps <= 50)
            interval = 5;
        else if (targetSteps <= 100)
            interval = 10;
        else if (targetSteps <= 250)
            interval = 25;
        else if (targetSteps <= 1000)
            interval = 100;
        else if (targetSteps <= 5000)
            interval = 500;
        else
            interval = 1000;

        return currentSteps % interval == 0;
    }

    private bool IsReady()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (!authenticated &&
                PlayGamesPlatform.Instance.IsAuthenticated())
            {
                authenticated = true;
            }

            return authenticated;
        }
        catch (System.Exception exception)
        {
            authenticated = false;
            lastAuthenticationStatus = "RuntimeError";

            Debug.LogError(
                "[GooglePlayGames] Runtime authentication check failed. " +
                "Gameplay will continue without achievement sync.",
                this
            );
            Debug.LogException(exception, this);
            return false;
        }
#else
        return false;
#endif
    }

    private void Unlock(
        AchievementKey key)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsReady())
            return;

        string achievementId =
            ResolveAchievementId(key);

        if (string.IsNullOrWhiteSpace(achievementId))
            return;

        try
        {
            PlayGamesPlatform.Instance.UnlockAchievement(
                achievementId,
                success =>
                {
                    if (!success)
                    {
                        Debug.LogWarning(
                            "[GooglePlayGames] Achievement unlock failed: " +
                            key
                        );
                        return;
                    }

                    LogDiagnostic(
                        "Achievement unlock succeeded: " + key
                    );
                }
            );
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "[GooglePlayGames] Achievement unlock call threw an exception. " +
                "Gameplay is protected and will continue.",
                this
            );
            Debug.LogException(exception, this);
        }
#endif
    }

    private void SetStepsAtLeast(
        AchievementKey key,
        int currentSteps,
        int targetSteps)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsReady())
            return;

        int safeTarget = Mathf.Max(1, targetSteps);
        int safeSteps = Mathf.Clamp(currentSteps, 0, safeTarget);

        if (safeSteps <= 0)
            return;

        string achievementId =
            ResolveAchievementId(key);

        if (string.IsNullOrWhiteSpace(achievementId))
            return;

        try
        {
            PlayGamesPlatform.Instance.SetStepsAtLeast(
                achievementId,
                safeSteps,
                success =>
                {
                    if (!success)
                    {
                        Debug.LogWarning(
                            "[GooglePlayGames] Achievement progress failed: " +
                            key + " | " +
                            safeSteps + "/" + safeTarget
                        );
                        return;
                    }

                    LogDiagnostic(
                        "Achievement progress succeeded: " +
                        key + " | " +
                        safeSteps + "/" + safeTarget
                    );
                }
            );
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "[GooglePlayGames] Achievement progress call threw an exception. " +
                "Gameplay is protected and will continue.",
                this
            );
            Debug.LogException(exception, this);
        }
#endif
    }

    private string BuildDiagnosticSummary()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bool platformAuthenticated = false;

        try
        {
            platformAuthenticated =
                PlayGamesPlatform.Instance.IsAuthenticated();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                "[GPGS-DIAG] Platform auth query failed: " +
                exception.Message
            );
        }

        return
            "localAuth=" + authenticated +
            " | platformAuth=" + platformAuthenticated +
            " | lastAuth=" + lastAuthenticationStatus +
            " | lastUI=" + lastAchievementsUiStatus;
#else
        return
            "localAuth=" + authenticated +
            " | lastAuth=" + lastAuthenticationStatus +
            " | lastUI=" + lastAchievementsUiStatus;
#endif
    }

    private void SaveDiagnosticState()
    {
        if (!DiagnosticsEnabled)
            return;

        try
        {
            PlayerPrefs.SetString(
                "GPGS_DIAG_LastAuth",
                lastAuthenticationStatus
            );

            PlayerPrefs.SetString(
                "GPGS_DIAG_LastUI",
                lastAchievementsUiStatus
            );

            PlayerPrefs.SetInt(
                "GPGS_DIAG_LocalAuthenticated",
                authenticated ? 1 : 0
            );

#if UNITY_ANDROID && !UNITY_EDITOR
            bool platformAuthenticated = false;

            try
            {
                platformAuthenticated =
                    PlayGamesPlatform.Instance.IsAuthenticated();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    "[GPGS-DIAG] Platform auth save query failed: " +
                    exception.Message
                );
            }

            PlayerPrefs.SetInt(
                "GPGS_DIAG_PlatformAuthenticated",
                platformAuthenticated ? 1 : 0
            );
#endif

            PlayerPrefs.Save();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                "[GPGS-DIAG] Saving diagnostic state failed: " +
                exception.Message
            );
        }
    }

    private void LogDiagnostic(
        string message)
    {
        if (!DiagnosticsEnabled)
            return;

        Debug.Log(
            "[GPGS-DIAG] " + message
        );
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void ShowDiagnosticToast(
        string message)
    {
        if (!DiagnosticsEnabled ||
            string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            AndroidJavaClass unityPlayer =
                new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer"
                );

            AndroidJavaObject activity =
                unityPlayer.GetStatic<AndroidJavaObject>(
                    "currentActivity"
                );

            activity.Call(
                "runOnUiThread",
                new AndroidJavaRunnable(
                    () =>
                    {
                        AndroidJavaClass toastClass =
                            new AndroidJavaClass(
                                "android.widget.Toast"
                            );

                        AndroidJavaObject toast =
                            toastClass.CallStatic<AndroidJavaObject>(
                                "makeText",
                                activity,
                                message,
                                1
                            );

                        toast.Call("show");
                    }
                )
            );
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                "[GPGS-DIAG] Android Toast failed: " +
                exception.Message
            );
        }
    }
#endif

    private static string ResolveAchievementId(
        AchievementKey key)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        switch (key)
        {
            case AchievementKey.FirstRush:
                return GPGSIds.achievement_first_rush;
            case AchievementKey.WarmingUp:
                return GPGSIds.achievement_warming_up;
            case AchievementKey.HalfwayThere:
                return GPGSIds.achievement_halfway_there;
            case AchievementKey.NoTurningBack:
                return GPGSIds.achievement_no_turning_back;
            case AchievementKey.FateDefied:
                return GPGSIds.achievement_fate_defied;
            case AchievementKey.CloseCall:
                return GPGSIds.achievement_close_call;
            case AchievementKey.ThreadTheNeedle:
                return GPGSIds.achievement_thread_the_needle;
            case AchievementKey.LivingOnTheEdge:
                return GPGSIds.achievement_living_on_the_edge;
            case AchievementKey.Untouchable:
                return GPGSIds.achievement_untouchable;
            case AchievementKey.ComboMaster:
                return GPGSIds.achievement_combo_master;
            case AchievementKey.MagneticAttraction:
                return GPGSIds.achievement_magnetic_attraction;
            case AchievementKey.FirstContact:
                return GPGSIds.achievement_first_contact;
            case AchievementKey.DivideAndConquer:
                return GPGSIds.achievement_divide_and_conquer;
            case AchievementKey.BehindCover:
                return GPGSIds.achievement_behind_cover;
            case AchievementKey.DarkFate:
                return GPGSIds.achievement_dark_fate;
            case AchievementKey.GoldenFate:
                return GPGSIds.achievement_golden_fate;
            case AchievementKey.StillStanding:
                return GPGSIds.achievement_still_standing;
            case AchievementKey.TooStubbornToQuit:
                return GPGSIds.achievement_too_stubborn_to_quit;
            case AchievementKey.EchoInitiate:
                return GPGSIds.achievement_echo_initiate;
            case AchievementKey.DoubleTrouble:
                return GPGSIds.achievement_double_trouble;
            case AchievementKey.ShadowArmy:
                return GPGSIds.achievement_shadow_army;
            case AchievementKey.QuickReflexes:
                return GPGSIds.achievement_quick_reflexes;
            case AchievementKey.BlinkAndYouMissMe:
                return GPGSIds.achievement_blink_and_you_miss_me;
            case AchievementKey.BornToRush:
                return GPGSIds.achievement_born_to_rush;
            case AchievementKey.Counterattack:
                return GPGSIds.achievement_counterattack;
            case AchievementKey.Payback:
                return GPGSIds.achievement_payback;
            case AchievementKey.Reaper:
                return GPGSIds.achievement_reaper;
            case AchievementKey.PocketChange:
                return GPGSIds.achievement_pocket_change;
            case AchievementKey.TreasureHunter:
                return GPGSIds.achievement_treasure_hunter;
            case AchievementKey.FortuneFavorsTheFast:
                return GPGSIds.achievement_fortune_favors_the_fast;
            case AchievementKey.SuitUp:
                return GPGSIds.achievement_suit_up;
            case AchievementKey.IronResolve:
                return GPGSIds.achievement_iron_resolve;
            case AchievementKey.TimeBender:
                return GPGSIds.achievement_time_bender;
            case AchievementKey.MasterOfTime:
                return GPGSIds.achievement_master_of_time;
            case AchievementKey.BadStep:
                return GPGSIds.achievement_bad_step;
            case AchievementKey.BombMagnet:
                return GPGSIds.achievement_bomb_magnet;
            case AchievementKey.GroundZero:
                return GPGSIds.achievement_ground_zero;
            case AchievementKey.BurnedOnce:
                return GPGSIds.achievement_burned_once;
            case AchievementKey.LightShowCasualty:
                return GPGSIds.achievement_light_show_casualty;
            case AchievementKey.Laserproof:
                return GPGSIds.achievement_laserproof;
            default:
                return string.Empty;
        }
#else
        return string.Empty;
#endif
    }
}
