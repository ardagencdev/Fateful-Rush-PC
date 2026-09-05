using UnityEngine;

/// <summary>
/// One-time save generation migration.
///
/// CLOSED BETA builds use generation 0.
/// The first FULL RELEASE build must use generation 1.
/// Going from 0 -> 1 wipes gameplay progression/statistics/skin selection
/// exactly once while preserving user settings such as audio, vibration,
/// FPS mode, HUD opacity, language and control layout.
/// </summary>
public static class ReleaseSaveMigration
{
    private const string SaveGenerationKey =
        "FatefulRush_SaveGeneration";

    // BUILD SWITCH:
    //   Closed beta / pre-release testing = 0
    //   First public full release          = 1
    //
    // Never decrease this number in a later build.
    public const int CurrentSaveGeneration = 1;

    private static bool appliedThisSession;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        ApplyIfNeeded();
    }

    public static void ApplyIfNeeded()
    {
#if UNITY_EDITOR
        // Never touch developer/editor saves. The migration is for shipped
        // player builds only (Android, Google Play Games on PC, Windows, etc.).
        return;
#else
        if (appliedThisSession)
            return;

        appliedThisSession = true;

        bool hasGeneration =
            PlayerPrefs.HasKey(SaveGenerationKey);

        if (!hasGeneration)
        {
            // A player can jump straight from an old closed-beta build to the
            // full release without ever installing the generation-0 bridge
            // build. Detect that legacy gameplay save and wipe it as well.
            if (CurrentSaveGeneration > 0 &&
                HasLegacyGameplaySave())
            {
                ResetGameplaySave();
            }

            PlayerPrefs.SetInt(
                SaveGenerationKey,
                CurrentSaveGeneration
            );

            PlayerPrefs.Save();
            return;
        }

        int storedGeneration =
            Mathf.Max(
                0,
                PlayerPrefs.GetInt(
                    SaveGenerationKey,
                    0
                )
            );

        if (storedGeneration >= CurrentSaveGeneration)
            return;

        ResetGameplaySave();

        PlayerPrefs.SetInt(
            SaveGenerationKey,
            CurrentSaveGeneration
        );

        PlayerPrefs.Save();
#endif
    }

    private static bool HasLegacyGameplaySave()
    {
        if (PlayerPrefs.HasKey("UnlockedLevel"))
            return true;

        if (PlayerPrefs.HasKey(
                PlayerSkinCatalog.SelectedSkinKey))
        {
            return true;
        }

        if (PlayerPrefs.HasKey("Stats_TotalRuns") ||
            PlayerPrefs.HasKey("Stats_TotalDeaths") ||
            PlayerPrefs.HasKey("Stats_TotalCoins") ||
            PlayerPrefs.HasKey("Stats_TotalPlayTime") ||
            PlayerPrefs.HasKey("FR_Ads_Attempts") ||
            PlayerPrefs.HasKey("FR_Ads_AttemptTarget") ||
            PlayerPrefs.HasKey("FR_Ads_MainMenuSeconds") ||
            PlayerPrefs.HasKey("FatefulRush_SignalStable") ||
            PlayerPrefs.HasKey("DebugAllPlayerSkinsUnlocked"))
        {
            return true;
        }

        for (int level = StatsManager.FirstLevelNumber;
             level <= StatsManager.LastLevelNumber;
             level++)
        {
            if (PlayerPrefs.HasKey(
                    "CompletedLevel_" + level) ||
                PlayerPrefs.HasKey(
                    "BestTime_Level_" + level))
            {
                return true;
            }
        }

        return false;
    }

    private static void ResetGameplaySave()
    {
        // Global run / combat / collectible / death-cause stats and best times.
        StatsManager.ResetAllStats();

        // Mission progression.
        for (int level = StatsManager.FirstLevelNumber;
             level <= StatsManager.LastLevelNumber;
             level++)
        {
            PlayerPrefs.DeleteKey(
                "CompletedLevel_" + level
            );

            PlayerPrefs.DeleteKey(
                "BestTime_Level_" + level
            );
        }

        PlayerPrefs.SetInt("UnlockedLevel", 1);

        // Skin progression derives from completed levels. Remove the saved
        // equipped skin and any debug unlock flag so the default skin is used.
        PlayerPrefs.DeleteKey(
            PlayerSkinCatalog.SelectedSkinKey
        );

        PlayerPrefs.DeleteKey(
            "DebugAllPlayerSkinsUnlocked"
        );

        // Closed-beta ad pacing should not carry into the public release.
        // Otherwise an existing tester could receive an ad immediately after
        // updating even though their gameplay progress was reset.
        PlayerPrefs.DeleteKey("FR_Ads_Attempts");
        PlayerPrefs.DeleteKey("FR_Ads_AttemptTarget");
        PlayerPrefs.DeleteKey("FR_Ads_MainMenuSeconds");

        // The footer is gameplay-result state too. Full release must always
        // start from the default SIGNAL // UNSTABLE state.
        PlayerPrefs.DeleteKey("FatefulRush_SignalStable");

        // Cloud-save revision belongs to the discarded gameplay generation.
        PlayerPrefs.DeleteKey("FatefulRush_CloudRevisionUtcTicks");

        // Remove diagnostic-only state left by the PGS troubleshooting build.
        PlayerPrefs.DeleteKey("GPGS_DIAG_LastAuth");
        PlayerPrefs.DeleteKey("GPGS_DIAG_LastUI");
        PlayerPrefs.DeleteKey("GPGS_DIAG_LocalAuthenticated");
        PlayerPrefs.DeleteKey("GPGS_DIAG_PlatformAuthenticated");

        PlayerPrefs.Save();

        LastDeathInfo.Cause = "UNKNOWN";

        Debug.Log(
            "[ReleaseSaveMigration] Gameplay save reset for save generation " +
            CurrentSaveGeneration +
            ". User settings were preserved."
        );
    }
}
