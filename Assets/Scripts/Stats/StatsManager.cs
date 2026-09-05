using UnityEngine;

public static class StatsManager
{
    public const int FirstLevelNumber = 1;
    public const int LastLevelNumber = 40;

    private const string TotalRunsKey = "Stats_TotalRuns";
    private const string TotalWinsKey = "Stats_TotalWins";
    private const string TotalDeathsKey = "Stats_TotalDeaths";
    private const string TotalCoinsKey = "Stats_TotalCoins";
    private const string TotalCoinValueKey = "Stats_TotalCoinValue";
    private const string NormalCoinsKey = "Stats_NormalCoins";
    private const string GoldCoinsKey = "Stats_GoldCoins";
    private const string RareCoinsKey = "Stats_RareCoins";
    private const string DashUsesKey = "Stats_DashUses";
    private const string CloneUsesKey = "Stats_CloneUses";
    private const string SlowBuffUsesKey = "Stats_SlowBuffUses";
    private const string ArmorBuffUsesKey = "Stats_ArmorBuffUses";

    // Key name is kept for backward compatibility with existing saves.
    // In the UI this statistic is presented as Armor Saves.
    private const string ArmorSavesKey = "Stats_ArmorKills";

    // Achievement-specific cumulative counters introduced with PGS support.
    private const string ArmorEnemyKillsKey = "Stats_ArmorEnemyKills";
    private const string SpaceBombTriggersKey = "Stats_SpaceBombTriggers";

    private const string TotalPlayTimeKey = "Stats_TotalPlayTime";
    private const string BestTimeLevelPrefix = "BestTime_Level_";
    private const string BestTimeDevRoomKey = "BestTime_DevRoom";

    // Performance / records.
    private const string TotalScoreKey = "Stats_TotalScore";
    private const string CompletedScoreTotalKey = "Stats_CompletedScoreTotal";
    private const string ScoreRunsKey = "Stats_ScoreRuns";
    private const string BestRunScoreKey = "Stats_BestRunScore";
    private const string MostCoinsInRunKey = "Stats_MostCoinsInRun";
    private const string LongestRunTimeKey = "Stats_LongestRunTime";
    private const string CurrentWinStreakKey = "Stats_CurrentWinStreak";
    private const string BestWinStreakKey = "Stats_BestWinStreak";

    // Combo.
    private const string HighestComboKey = "Stats_HighestCombo";
    private const string LongestComboChainKey = "Stats_LongestComboChain";
    private const string MaxComboReachedKey = "Stats_MaxComboReached";
    private const string ComboBonusScoreKey = "Stats_ComboBonusScore";

    // Near miss.
    private const string NearMissesKey = "Stats_NearMisses";
    private const string BestNearMissStreakKey = "Stats_BestNearMissStreak";

    // Combo magnet / combat interactions.
    private const string MagnetCoinsKey = "Stats_MagnetCoins";
    private const string BeaconsDestroyedKey = "Stats_BeaconsDestroyed";
    private const string HuntersStunnedKey = "Stats_HuntersStunned";
    private const string BossEncountersKey = "Stats_BossEncounters";
    private const string BossSplitsKey = "Stats_BossSplits";
    private const string BossAoeEvadesKey = "Stats_BossAoeEvades";
    private const string MiniBossAoeEvadesKey = "Stats_MiniBossAoeEvades";

    // Mode records.
    private const string ScoreModeRunsKey = "Stats_Mode_Score_Runs";
    private const string ScoreModeWinsKey = "Stats_Mode_Score_Wins";
    private const string SurvivalModeRunsKey = "Stats_Mode_Survival_Runs";
    private const string SurvivalModeWinsKey = "Stats_Mode_Survival_Wins";
    private const string TimedScoreModeRunsKey = "Stats_Mode_TimedScore_Runs";
    private const string TimedScoreModeWinsKey = "Stats_Mode_TimedScore_Wins";

    // Death causes.
    private const string DeathStalkerKey = "Stats_Death_STALKER";
    private const string DeathHunterKey = "Stats_Death_HUNTER";
    private const string DeathBlasterKey = "Stats_Death_BLASTER";
    private const string DeathLaserBulletKey = "Stats_Death_LASER_BULLET";
    private const string DeathLaserWallKey = "Stats_Death_LASER_WALL";
    private const string DeathBossKey = "Stats_Death_BOSS";
    private const string DeathMiniBossKey = "Stats_Death_MINI_BOSS";
    private const string DeathSpaceBombKey = "Stats_Death_SPACE_BOMB";
    private const string DeathTimeExpiredKey = "Stats_Death_TIME_EXPIRED";
    private const string DeathUnknownKey = "Stats_Death_UNKNOWN";

    public static readonly string[] TrackedDeathCauses =
    {
        "STALKER",
        "HUNTER",
        "BLASTER",
        "LASER BULLET",
        "LASER WALL",
        "BOSS",
        "MINI BOSS",
        "SPACE BOMB",
        "TIME EXPIRED",
        "UNKNOWN"
    };

    private static bool dirty;

    public static bool HasUnsavedChanges => dirty;

    public static void AddRun() => AddInt(TotalRunsKey);
    public static void AddWin() => AddInt(TotalWinsKey);

    public static void AddDeath()
    {
        // Existing stat semantics are preserved: every lost run increments
        // TotalDeaths. Achievement death progress is updated after the exact
        // death cause is recorded so TIME EXPIRED losses can be excluded.
        AddInt(TotalDeathsKey);
    }

    public static void AddDashUse()
    {
        AddInt(DashUsesKey);
        GooglePlayGamesManager.NotifyDashUseTotal(GetDashUses());
    }

    public static void AddCloneUse()
    {
        AddInt(CloneUsesKey);
        GooglePlayGamesManager.NotifyCloneUseTotal(GetCloneUses());
    }

    public static void AddSlowBuffUse()
    {
        AddInt(SlowBuffUsesKey);
        GooglePlayGamesManager.NotifySlowUseTotal(GetSlowBuffUses());
    }

    public static void AddArmorBuffUse()
    {
        AddInt(ArmorBuffUsesKey);
        GooglePlayGamesManager.NotifyArmorUseTotal(GetArmorBuffUses());
    }

    public static void AddArmorSave() => AddInt(ArmorSavesKey);

    public static void AddArmorEnemyKill()
    {
        AddInt(ArmorEnemyKillsKey);
        GooglePlayGamesManager.NotifyArmorEnemyKillTotal(
            GetArmorEnemyKills()
        );
    }

    public static void AddSpaceBombTrigger()
    {
        AddInt(SpaceBombTriggersKey);
        GooglePlayGamesManager.NotifySpaceBombTriggerTotal(
            GetSpaceBombTriggers()
        );
    }

    public static void AddCoin(int value, CoinType coinType)
    {
        AddInt(TotalCoinsKey);

        if (value > 0)
            AddInt(TotalCoinValueKey, value);

        switch (coinType)
        {
            case CoinType.Normal:
                AddInt(NormalCoinsKey);
                break;

            case CoinType.Gold:
                AddInt(GoldCoinsKey);
                break;

            case CoinType.Rare:
                AddInt(RareCoinsKey);
                break;

            default:
                Debug.LogWarning($"[StatsManager] Unknown coin type: {coinType}");
                break;
        }

        GooglePlayGamesManager.NotifyTotalCoins(
            GetTotalCoins()
        );
    }

    public static void AddScore(int gainedScore, int baseCoinValue)
    {
        if (gainedScore <= 0)
            return;

        AddInt(TotalScoreKey, gainedScore);

        int bonus = gainedScore - Mathf.Max(0, baseCoinValue);
        if (bonus > 0)
            AddInt(ComboBonusScoreKey, bonus);
    }

    public static void RecordComboProgress(
        int comboMultiplier,
        int chainLength,
        bool reachedNewStage)
    {
        int safeCombo = Mathf.Max(1, comboMultiplier);
        int safeChain = Mathf.Max(0, chainLength);

        SetMaxInt(HighestComboKey, safeCombo);
        SetMaxInt(LongestComboChainKey, safeChain);

        if (reachedNewStage && safeCombo == 6)
            AddInt(MaxComboReachedKey);

        if (safeCombo >= 6)
        {
            GooglePlayGamesManager.NotifyComboReached(
                safeCombo
            );
        }
    }

    public static void AddNearMiss(int streak)
    {
        AddInt(NearMissesKey);
        SetMaxInt(BestNearMissStreakKey, Mathf.Max(1, streak));

        GooglePlayGamesManager.NotifyNearMissTotal(
            GetNearMisses()
        );
    }

    public static void AddMagnetCoin()
    {
        AddInt(MagnetCoinsKey);

        GooglePlayGamesManager.NotifyMagnetCoinTotal(
            GetMagnetCoins()
        );
    }

    public static void AddBeaconDestroyed() => AddInt(BeaconsDestroyedKey);
    public static void AddHunterStun() => AddInt(HuntersStunnedKey);

    public static void AddBossEncounter()
    {
        AddInt(BossEncountersKey);
        GooglePlayGamesManager.NotifyBossEncounter();
    }

    public static void AddBossSplit()
    {
        AddInt(BossSplitsKey);
        GooglePlayGamesManager.NotifyBossSplit();
    }

    public static void AddBossAoeEvade()
    {
        AddInt(BossAoeEvadesKey);
        GooglePlayGamesManager.NotifyBossAoeEvade();
    }

    public static void AddMiniBossAoeEvade() => AddInt(MiniBossAoeEvadesKey);

    public static void RecordRunDetails(
        bool won,
        int finalScore,
        float duration,
        WinConditionType mode,
        int coinsCollectedThisRun,
        string deathCause = null)
    {
        if (duration > 0f &&
            !float.IsNaN(duration) &&
            !float.IsInfinity(duration))
        {
            SetMaxFloat(LongestRunTimeKey, duration);
        }

        SetMaxInt(MostCoinsInRunKey, Mathf.Max(0, coinsCollectedThisRun));

        bool usesScore =
            mode == WinConditionType.ReachScore ||
            mode == WinConditionType.ReachScoreWithinTime;

        if (usesScore)
        {
            AddInt(ScoreRunsKey);
            AddInt(CompletedScoreTotalKey, Mathf.Max(0, finalScore));
            SetMaxInt(BestRunScoreKey, Mathf.Max(0, finalScore));
        }

        RecordModeResult(mode, won);

        if (won)
        {
            int current = GetInt(CurrentWinStreakKey) + 1;
            SetInt(CurrentWinStreakKey, current);
            SetMaxInt(BestWinStreakKey, current);
        }
        else
        {
            SetInt(CurrentWinStreakKey, 0);
            AddDeathCause(deathCause);
        }
    }

    private static void RecordModeResult(WinConditionType mode, bool won)
    {
        switch (mode)
        {
            case WinConditionType.ReachScore:
                AddInt(ScoreModeRunsKey);
                if (won) AddInt(ScoreModeWinsKey);
                break;

            case WinConditionType.SurviveTime:
                AddInt(SurvivalModeRunsKey);
                if (won) AddInt(SurvivalModeWinsKey);
                break;

            case WinConditionType.ReachScoreWithinTime:
                AddInt(TimedScoreModeRunsKey);
                if (won) AddInt(TimedScoreModeWinsKey);
                break;
        }
    }

    private static void AddDeathCause(string cause)
    {
        string key = GetDeathCauseKey(cause);
        AddInt(key);

        GooglePlayGamesManager.NotifyTotalDeaths(
            GetActualDeaths()
        );

        if (key == DeathSpaceBombKey)
        {
            GooglePlayGamesManager.NotifySpaceBombDeathTotal(
                GetDeathCauseCount("SPACE BOMB")
            );
            return;
        }

        // Google Play laser achievements intentionally count only the
        // horizontal / vertical LaserWall hazards. Enemy projectile
        // "LASER BULLET" deaths remain in the stats panel but do not advance
        // Burned Once / Light Show Casualty / Laserproof.
        if (key == DeathLaserWallKey)
        {
            GooglePlayGamesManager.NotifyLaserDeathTotal(
                GetLaserDeaths()
            );
        }
    }

    public static void AddPlayTime(float seconds)
    {
        if (seconds <= 0f ||
            float.IsNaN(seconds) ||
            float.IsInfinity(seconds))
        {
            return;
        }

        float currentPlayTime = PlayerPrefs.GetFloat(TotalPlayTimeKey, 0f);
        PlayerPrefs.SetFloat(TotalPlayTimeKey, currentPlayTime + seconds);
        dirty = true;
    }

    public static void SetBestTime(string key, float time)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogWarning("[StatsManager] Best time key is empty.");
            return;
        }

        if (time <= 0f ||
            float.IsNaN(time) ||
            float.IsInfinity(time))
        {
            Debug.LogWarning($"[StatsManager] Invalid best time: {time}");
            return;
        }

        float currentBestTime = PlayerPrefs.GetFloat(key, Mathf.Infinity);

        if (time >= currentBestTime)
            return;

        PlayerPrefs.SetFloat(key, time);
        dirty = true;
    }

    public static void SaveIfDirty()
    {
        if (!dirty)
            return;

        PlayerPrefs.Save();
        dirty = false;

        // End-of-run stats are a meaningful progression checkpoint. Cloud
        // writes are debounced by FatefulRushCloudSave, so this does not
        // create a network write for every stat mutation.
        FatefulRushCloudSave.RequestUpload();
    }

    public static int GetTotalRuns() => GetInt(TotalRunsKey);
    public static int GetTotalWins() => GetInt(TotalWinsKey);
    public static int GetTotalDeaths() => GetInt(TotalDeathsKey);

    public static int GetActualDeaths()
    {
        int allLosses = GetInt(TotalDeathsKey);
        int timeExpired = GetInt(DeathTimeExpiredKey);
        return Mathf.Max(0, allLosses - timeExpired);
    }

    public static int GetTotalCoins() => GetInt(TotalCoinsKey);
    public static int GetTotalCoinValue() => GetInt(TotalCoinValueKey);
    public static int GetNormalCoins() => GetInt(NormalCoinsKey);
    public static int GetGoldCoins() => GetInt(GoldCoinsKey);
    public static int GetRareCoins() => GetInt(RareCoinsKey);
    public static int GetDashUses() => GetInt(DashUsesKey);
    public static int GetCloneUses() => GetInt(CloneUsesKey);
    public static int GetSlowBuffUses() => GetInt(SlowBuffUsesKey);
    public static int GetArmorBuffUses() => GetInt(ArmorBuffUsesKey);
    public static int GetArmorKills() => GetInt(ArmorSavesKey);
    public static int GetArmorSaves() => GetInt(ArmorSavesKey);
    public static int GetArmorEnemyKills() => GetInt(ArmorEnemyKillsKey);
    public static int GetSpaceBombTriggers() => GetInt(SpaceBombTriggersKey);

    public static int GetLaserDeaths()
    {
        // Achievement-facing laser deaths: only horizontal / vertical
        // LaserWall hazards. Laser bullets are tracked separately and are
        // deliberately excluded from Google Play achievement progress.
        return GetInt(DeathLaserWallKey);
    }

    public static float GetTotalPlayTime() => GetFloat(TotalPlayTimeKey);

    public static int GetTotalScore() => GetInt(TotalScoreKey);
    public static int GetBestRunScore() => GetInt(BestRunScoreKey);
    public static int GetMostCoinsInRun() => GetInt(MostCoinsInRunKey);
    public static int GetScoreRuns() => GetInt(ScoreRunsKey);
    public static float GetLongestRunTime() => GetFloat(LongestRunTimeKey);
    public static int GetCurrentWinStreak() => GetInt(CurrentWinStreakKey);
    public static int GetBestWinStreak() => GetInt(BestWinStreakKey);
    public static int GetHighestCombo() => Mathf.Max(1, GetInt(HighestComboKey));
    public static int GetLongestComboChain() => GetInt(LongestComboChainKey);
    public static int GetMaxComboReachedCount() => GetInt(MaxComboReachedKey);
    public static int GetComboBonusScore() => GetInt(ComboBonusScoreKey);
    public static int GetNearMisses() => GetInt(NearMissesKey);
    public static int GetBestNearMissStreak() => GetInt(BestNearMissStreakKey);
    public static int GetMagnetCoins() => GetInt(MagnetCoinsKey);
    public static int GetBeaconsDestroyed() => GetInt(BeaconsDestroyedKey);
    public static int GetHuntersStunned() => GetInt(HuntersStunnedKey);
    public static int GetBossEncounters() => GetInt(BossEncountersKey);
    public static int GetBossSplits() => GetInt(BossSplitsKey);
    public static int GetBossAoeEvades() => GetInt(BossAoeEvadesKey);
    public static int GetMiniBossAoeEvades() => GetInt(MiniBossAoeEvadesKey);

    public static float GetAverageRunTime()
    {
        int runs = GetTotalRuns();
        return runs > 0 ? GetTotalPlayTime() / runs : 0f;
    }

    public static float GetAverageScorePerScoreRun()
    {
        int scoreRuns = GetScoreRuns();
        return scoreRuns > 0
            ? GetInt(CompletedScoreTotalKey) / (float)scoreRuns
            : 0f;
    }

    public static int GetModeRuns(WinConditionType mode)
    {
        switch (mode)
        {
            case WinConditionType.ReachScore:
                return GetInt(ScoreModeRunsKey);
            case WinConditionType.SurviveTime:
                return GetInt(SurvivalModeRunsKey);
            case WinConditionType.ReachScoreWithinTime:
                return GetInt(TimedScoreModeRunsKey);
            default:
                return 0;
        }
    }

    public static int GetModeWins(WinConditionType mode)
    {
        switch (mode)
        {
            case WinConditionType.ReachScore:
                return GetInt(ScoreModeWinsKey);
            case WinConditionType.SurviveTime:
                return GetInt(SurvivalModeWinsKey);
            case WinConditionType.ReachScoreWithinTime:
                return GetInt(TimedScoreModeWinsKey);
            default:
                return 0;
        }
    }

    public static int GetDeathCauseCount(string cause)
    {
        return GetInt(GetDeathCauseKey(cause));
    }

    public static string GetNemesis(out int count)
    {
        string bestCause = "NONE";
        int bestCount = 0;

        for (int i = 0; i < TrackedDeathCauses.Length; i++)
        {
            string cause = TrackedDeathCauses[i];

            if (cause == "TIME EXPIRED" || cause == "UNKNOWN")
                continue;

            int current = GetDeathCauseCount(cause);

            if (current > bestCount)
            {
                bestCount = current;
                bestCause = cause;
            }
        }

        count = bestCount;
        return bestCause;
    }

    public static int GetCompletedLevelCount()
    {
        int completed = 0;

        for (int level = FirstLevelNumber; level <= LastLevelNumber; level++)
        {
            if (PlayerPrefs.GetInt("CompletedLevel_" + level, 0) == 1)
                completed++;
        }

        return completed;
    }

    public static int GetHighestCompletedLevel()
    {
        for (int level = LastLevelNumber; level >= FirstLevelNumber; level--)
        {
            if (PlayerPrefs.GetInt("CompletedLevel_" + level, 0) == 1)
                return level;
        }

        return 0;
    }

    public static int GetRecordedBestTimeCount()
    {
        int count = 0;

        for (int level = FirstLevelNumber; level <= LastLevelNumber; level++)
        {
            if (PlayerPrefs.HasKey(BestTimeLevelPrefix + level))
                count++;
        }

        return count;
    }

    public static float GetLevelBestTime(int levelNumber)
    {
        if (levelNumber < FirstLevelNumber || levelNumber > LastLevelNumber)
            return -1f;

        return PlayerPrefs.GetFloat(BestTimeLevelPrefix + levelNumber, -1f);
    }

    public static float GetDevRoomBestTime()
    {
        return PlayerPrefs.GetFloat(BestTimeDevRoomKey, -1f);
    }

    public static int GetInt(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return 0;

        return PlayerPrefs.GetInt(key, 0);
    }

    public static float GetFloat(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return 0f;

        return PlayerPrefs.GetFloat(key, 0f);
    }

    private static string GetDeathCauseKey(string cause)
    {
        string normalized = string.IsNullOrWhiteSpace(cause)
            ? "UNKNOWN"
            : cause.Trim().ToUpperInvariant();

        switch (normalized)
        {
            case "STALKER": return DeathStalkerKey;
            case "HUNTER": return DeathHunterKey;
            case "BLASTER": return DeathBlasterKey;
            case "LASER BULLET": return DeathLaserBulletKey;
            case "LASER WALL": return DeathLaserWallKey;
            case "BOSS": return DeathBossKey;
            case "MINI BOSS": return DeathMiniBossKey;
            case "SPACE BOMB": return DeathSpaceBombKey;
            case "TIME EXPIRED": return DeathTimeExpiredKey;
            default: return DeathUnknownKey;
        }
    }

    private static void AddInt(string key, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(key) || amount == 0)
            return;

        int currentValue = PlayerPrefs.GetInt(key, 0);
        long newValue = (long)currentValue + amount;

        int safeValue;

        if (newValue <= 0L)
            safeValue = 0;
        else if (newValue >= int.MaxValue)
            safeValue = int.MaxValue;
        else
            safeValue = (int)newValue;

        PlayerPrefs.SetInt(key, safeValue);
        dirty = true;
    }

    private static void SetInt(string key, int value)
    {
        int safeValue = Mathf.Max(0, value);

        if (PlayerPrefs.GetInt(key, 0) == safeValue)
            return;

        PlayerPrefs.SetInt(key, safeValue);
        dirty = true;
    }

    private static void SetMaxInt(string key, int value)
    {
        int safeValue = Mathf.Max(0, value);
        int current = PlayerPrefs.GetInt(key, 0);

        if (safeValue <= current)
            return;

        PlayerPrefs.SetInt(key, safeValue);
        dirty = true;
    }

    private static void SetMaxFloat(string key, float value)
    {
        if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
            return;

        float current = PlayerPrefs.GetFloat(key, 0f);

        if (value <= current)
            return;

        PlayerPrefs.SetFloat(key, value);
        dirty = true;
    }

    public static void ResetAllStats()
    {
        DeleteKeys(
            TotalRunsKey,
            TotalWinsKey,
            TotalDeathsKey,
            TotalCoinsKey,
            TotalCoinValueKey,
            NormalCoinsKey,
            GoldCoinsKey,
            RareCoinsKey,
            DashUsesKey,
            CloneUsesKey,
            SlowBuffUsesKey,
            ArmorBuffUsesKey,
            ArmorSavesKey,
            ArmorEnemyKillsKey,
            SpaceBombTriggersKey,
            TotalPlayTimeKey,
            TotalScoreKey,
            CompletedScoreTotalKey,
            ScoreRunsKey,
            BestRunScoreKey,
            MostCoinsInRunKey,
            LongestRunTimeKey,
            CurrentWinStreakKey,
            BestWinStreakKey,
            HighestComboKey,
            LongestComboChainKey,
            MaxComboReachedKey,
            ComboBonusScoreKey,
            NearMissesKey,
            BestNearMissStreakKey,
            MagnetCoinsKey,
            BeaconsDestroyedKey,
            HuntersStunnedKey,
            BossEncountersKey,
            BossSplitsKey,
            BossAoeEvadesKey,
            MiniBossAoeEvadesKey,
            ScoreModeRunsKey,
            ScoreModeWinsKey,
            SurvivalModeRunsKey,
            SurvivalModeWinsKey,
            TimedScoreModeRunsKey,
            TimedScoreModeWinsKey,
            DeathStalkerKey,
            DeathHunterKey,
            DeathBlasterKey,
            DeathLaserBulletKey,
            DeathLaserWallKey,
            DeathBossKey,
            DeathMiniBossKey,
            DeathSpaceBombKey,
            DeathTimeExpiredKey,
            DeathUnknownKey
        );

        for (int levelNumber = FirstLevelNumber;
             levelNumber <= LastLevelNumber;
             levelNumber++)
        {
            PlayerPrefs.DeleteKey(BestTimeLevelPrefix + levelNumber);
        }

        PlayerPrefs.DeleteKey(BestTimeDevRoomKey);

        PlayerPrefs.Save();
        dirty = false;
    }

    private static void DeleteKeys(params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(keys[i]))
                PlayerPrefs.DeleteKey(keys[i]);
        }
    }
}
