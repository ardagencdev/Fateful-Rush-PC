#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FatefulRushFinalDifficultyRebalance
{
    private const float PlayerBaseSpeed = 6.4f;
    private const int MaxNormalEnemies = 3;
    private const int MaxStaticObstacles = 5;

    // Index = level number. Index 0 is unused.
    private static readonly int[] NormalCounts =
    {
        0,
        0, // 1
        1, // 2
        1, // 3
        2, // 4
        2, // 5
        2, // 6
        2, // 7
        2, // 8
        3, // 9
        3, // 10
        3, // 11
        3, // 12
        3, // 13
        3, // 14
        3, // 15
        3, // 16
        3, // 17
        3, // 18
        3, // 19
        3, // 20
        3, // 21
        3, // 22
        3, // 23
        3, // 24
        3, // 25
        3, // 26
        3, // 27
        3, // 28
        3, // 29
        3, // 30
        3, // 31
        3, // 32
        3, // 33
        3, // 34
        3, // 35
        3, // 36
        3, // 37
        3, // 38
        3, // 39
        3  // 40
    };

    // First Stalker is deliberately early in L2. Later intervals become
    // wider as the arena gains Projectile/Hunter/Boss/Beacon/trap pressure.
    private static readonly float[] NormalSpawnIntervals =
    {
        0f,
        0f,  // 1
        1.8f,// 2
        2.2f,// 3
        2.8f,// 4
        3.0f,// 5
        3.3f,// 6
        3.7f,// 7
        4.2f,// 8
        4.7f,// 9
        5.8f,// 10
        4.8f,// 11
        5.0f,// 12
        6.0f,// 13
        4.9f,// 14
        5.1f,// 15
        5.3f,// 16
        5.8f,// 17
        5.5f,// 18
        5.3f,// 19
        6.1f,// 20
        6.3f,// 21
        5.5f,// 22
        5.7f,// 23
        6.0f,// 24
        5.6f,// 25
        6.0f,// 26
        5.5f,// 27
        6.1f,// 28
        6.0f,// 29
        6.4f,// 30
        7.0f,// 31
        5.9f,// 32
        5.7f,// 33
        7.0f,// 34
        5.7f,// 35
        6.3f,// 36
        7.2f,// 37
        5.5f,// 38
        6.0f,// 39
        7.4f // 40
    };

    [MenuItem("Tools/Fateful Rush/Apply Final Difficulty Rebalance")]
    public static void Apply()
    {
        List<LevelConfig> levels = LoadLevels();

        if (levels.Count == 0)
        {
            Debug.LogError("[Fateful Rush Balance] No LevelConfig assets found.");
            return;
        }

        Undo.SetCurrentGroupName("Fateful Rush Final Difficulty Rebalance");
        int undoGroup = Undo.GetCurrentGroup();

        DangerBalanceProfile sharedProfile = FindSharedProfile(levels);
        if (sharedProfile != null)
        {
            Undo.RecordObject(sharedProfile, "Rebalance Danger Profile");
            ApplyDangerProfile(sharedProfile);
            EditorUtility.SetDirty(sharedProfile);
        }
        else
        {
            Debug.LogWarning(
                "[Fateful Rush Balance] No shared DangerBalanceProfile was found. " +
                "Level assignments will still be changed, but D1-D5 behaviour cannot be retuned."
            );
        }

        foreach (LevelConfig level in levels)
        {
            if (level == null)
                continue;

            Undo.RecordObject(level, $"Rebalance Level {level.levelNumber}");
            ApplyLevel(level);
            EditorUtility.SetDirty(level);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log(
            $"[Fateful Rush Balance] Applied to {levels.Count} LevelConfig assets. " +
            "Background/starfield properties were not modified."
        );

        Validate();
    }

    [MenuItem("Tools/Fateful Rush/Validate Final Difficulty Rebalance")]
    public static void Validate()
    {
        List<LevelConfig> levels = LoadLevels();
        int errors = 0;
        int warnings = 0;

        foreach (LevelConfig level in levels)
        {
            if (level == null)
                continue;

            SerializedObject so = new SerializedObject(level);
            int n = ReadInt(so, "levelNumber", level.levelNumber);

            int normalCount = ReadInt(so, "normalEnemyCount", 0);
            if (normalCount > MaxNormalEnemies)
            {
                errors++;
                Debug.LogError(
                    $"[Balance Validation] Level {n}: Stalker count is {normalCount}; max is {MaxNormalEnemies}.",
                    level
                );
            }

            int spawnedObstacles = CountSpawnedStaticObstacles(so);
            if (spawnedObstacles > MaxStaticObstacles)
            {
                errors++;
                Debug.LogError(
                    $"[Balance Validation] Level {n}: static obstacle spawn count is {spawnedObstacles}; max is {MaxStaticObstacles}.",
                    level
                );
            }

            int winCondition = ReadEnumIndex(so, "winCondition", 0);
            bool comboEnabled = ReadBool(so, "comboEnabled", false);

            if (winCondition == 1 && comboEnabled)
            {
                errors++;
                Debug.LogError(
                    $"[Balance Validation] Level {n}: Survive Time must have Combo OFF.",
                    level
                );
            }

            float playerSpeed = ReadFloat(so, "playerMoveSpeed", PlayerBaseSpeed);
            if (!Mathf.Approximately(playerSpeed, PlayerBaseSpeed))
            {
                warnings++;
                Debug.LogWarning(
                    $"[Balance Validation] Level {n}: player speed is {playerSpeed:0.##}; intended base is {PlayerBaseSpeed:0.##}.",
                    level
                );
            }

            if (n == 1 && HasAnyLethalThreat(so))
            {
                errors++;
                Debug.LogError(
                    "[Balance Validation] Level 1 must remain safe/onboarding only.",
                    level
                );
            }

            if (n == 2)
            {
                if (normalCount < 1)
                {
                    errors++;
                    Debug.LogError(
                        "[Balance Validation] Level 2 must introduce one Stalker with Combo.",
                        level
                    );
                }

                if (!comboEnabled)
                {
                    errors++;
                    Debug.LogError(
                        "[Balance Validation] Level 2 must have Combo enabled.",
                        level
                    );
                }
            }

            if (n >= 2 && !HasAnyLethalThreat(so))
            {
                errors++;
                Debug.LogError(
                    $"[Balance Validation] Level {n}: no lethal pressure is active. Every level after L1 must feel unsafe.",
                    level
                );
            }

            if (winCondition != 1)
            {
                float totalChance =
                    (ReadBool(so, "normalCoinEnabled", false) ? ReadFloat(so, "normalCoinChance", 0f) : 0f) +
                    (ReadBool(so, "goldCoinEnabled", false) ? ReadFloat(so, "goldCoinChance", 0f) : 0f) +
                    (ReadBool(so, "rareCoinEnabled", false) ? ReadFloat(so, "rareCoinChance", 0f) : 0f);

                if (!Mathf.Approximately(totalChance, 100f))
                {
                    warnings++;
                    Debug.LogWarning(
                        $"[Balance Validation] Level {n}: enabled coin chances total {totalChance:0.##}%, expected 100%.",
                        level
                    );
                }

                float rareChance = ReadBool(so, "rareCoinEnabled", false)
                    ? ReadFloat(so, "rareCoinChance", 0f)
                    : 0f;

                if (rareChance > 6.01f)
                {
                    warnings++;
                    Debug.LogWarning(
                        $"[Balance Validation] Level {n}: Rare Coin chance is {rareChance:0.##}%; final balance target is at most 6%.",
                        level
                    );
                }
            }
        }

        DangerBalanceProfile profile = FindSharedProfile(levels);
        if (profile != null)
        {
            SerializedObject profileSO = new SerializedObject(profile);
            SerializedProperty normalArray = profileSO.FindProperty("normalEnemyLevels");

            if (normalArray != null && normalArray.isArray)
            {
                for (int i = 0; i < normalArray.arraySize; i++)
                {
                    SerializedProperty tier = normalArray.GetArrayElementAtIndex(i);
                    SerializedProperty maxSpeed = tier.FindPropertyRelative("maxSpeed");

                    if (maxSpeed != null && maxSpeed.floatValue >= PlayerBaseSpeed)
                    {
                        errors++;
                        Debug.LogError(
                            $"[Balance Validation] Normal Enemy D{i + 1} maxSpeed = {maxSpeed.floatValue:0.##}. " +
                            $"It must stay below player base speed {PlayerBaseSpeed:0.##}.",
                            profile
                        );
                    }
                }
            }
        }

        if (levels.Count != 40)
        {
            warnings++;
            Debug.LogWarning(
                $"[Balance Validation] Found {levels.Count} LevelConfig assets; expected 40 campaign levels."
            );
        }

        Debug.Log(
            $"[Fateful Rush Balance] Validation finished: {errors} error(s), {warnings} warning(s)."
        );
    }

    private static void ApplyLevel(LevelConfig level)
    {
        SerializedObject so = new SerializedObject(level);
        so.Update();

        int n = Mathf.Clamp(ReadInt(so, "levelNumber", level.levelNumber), 1, 40);
        int winCondition = ReadEnumIndex(so, "winCondition", 0);
        bool usesScore = winCondition == 0 || winCondition == 2;
        bool usesTime = winCondition == 1 || winCondition == 2;

        // Stable muscle memory across the whole campaign.
        SetFloat(so, "playerMoveSpeed", PlayerBaseSpeed);

        // Difficulty presentation.
        SetInt(so, "missionDifficulty", GetMissionDifficulty(n));

        // Level 1 is the only intentionally safe level.
        if (n == 1)
        {
            SetInt(so, "normalEnemyCount", 0);
            SetInt(so, "projectileEnemyCount", 0);
            SetInt(so, "hunterEnemyCount", 0);
            SetInt(so, "beaconEnemyCount", 0);
            SetBool(so, "bossEnabled", false);
            SetBool(so, "verticalLaserEnabled", false);
            SetBool(so, "horizontalLaserEnabled", false);
            SetBool(so, "bombTrapEnabled", false);
            SetBool(so, "comboEnabled", false);
            ClearComboStages(so);
            DisableAllStaticObstacles(so);
            SetInt(so, "randomObstacleCount", 0);
            ApplyScoreEconomy(so, n, usesScore, winCondition);
            ApplyProgressionMetadata(so, n);
            so.ApplyModifiedPropertiesWithoutUndo();
            return;
        }

        // Stalker pressure curve. Never exceeds 3.
        int targetNormalCount = NormalCounts[n];
        SetInt(so, "normalEnemyCount", Mathf.Clamp(targetNormalCount, 1, MaxNormalEnemies));
        SetFloat(so, "normalEnemySpawnInterval", NormalSpawnIntervals[n]);
        SetDanger(so, "normalEnemyDanger", GetNormalTier(n));
        SetBool(so, "normalEnemyCustomOverride", false);

        // L2 is a focused Combo + Stalker introduction.
        if (n == 2)
        {
            SetInt(so, "projectileEnemyCount", 0);
            SetInt(so, "hunterEnemyCount", 0);
            SetInt(so, "beaconEnemyCount", 0);
            SetBool(so, "bossEnabled", false);
            SetBool(so, "verticalLaserEnabled", false);
            SetBool(so, "horizontalLaserEnabled", false);
            SetBool(so, "bombTrapEnabled", false);
        }

        RebalanceExistingThreats(so, n, usesTime);
        ApplyCombo(so, n, usesScore, winCondition);
        ApplyScoreEconomy(so, n, usesScore, winCondition);
        ApplyBossTiming(so, winCondition);
        CapStaticObstacles(so, n);
        ApplyProgressionMetadata(so, n);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RebalanceExistingThreats(
        SerializedObject so,
        int levelNumber,
        bool usesTime)
    {
        // Projectile: no more than 2 at once. Higher tiers are primarily
        // cadence/aim pressure rather than body speed.
        int projectileCount = ReadInt(so, "projectileEnemyCount", 0);
        if (projectileCount > 0)
        {
            projectileCount = Mathf.Min(projectileCount, 2);

            int tier = GetProjectileTier(levelNumber, projectileCount);
            if (tier == 5 && projectileCount > 1)
                tier = 4;

            SetInt(so, "projectileEnemyCount", projectileCount);
            SetDanger(so, "projectileEnemyDanger", tier);
            SetBool(so, "projectileEnemyCustomOverride", false);

            float minInterval = usesTime ? 10f : 8.5f;
            SetFloat(
                so,
                "projectileEnemySpawnInterval",
                Mathf.Max(ReadFloat(so, "projectileEnemySpawnInterval", minInterval), minInterval)
            );
        }

        // Hunter: max 2. D5 is reserved for a single Hunter.
        int hunterCount = ReadInt(so, "hunterEnemyCount", 0);
        if (hunterCount > 0)
        {
            hunterCount = Mathf.Min(hunterCount, 2);

            int tier = GetHunterTier(levelNumber, hunterCount);
            if (tier == 5 && hunterCount > 1)
                tier = 4;

            SetInt(so, "hunterEnemyCount", hunterCount);
            SetDanger(so, "hunterEnemyDanger", tier);
            SetBool(so, "hunterEnemyCustomOverride", false);

            float minInterval = usesTime ? 16f : 13.5f;
            SetFloat(
                so,
                "hunterEnemySpawnInterval",
                Mathf.Max(ReadFloat(so, "hunterEnemySpawnInterval", minInterval), minInterval)
            );
        }

        if (ReadBool(so, "bossEnabled", false))
        {
            SetDanger(so, "bossDanger", GetBossTier(levelNumber));
            SetBool(so, "bossCustomOverride", false);
        }

        int beaconCount = ReadInt(so, "beaconEnemyCount", 0);
        if (beaconCount > 0)
        {
            SetInt(so, "beaconEnemyCount", 1);
            SetDanger(so, "beaconEnemyDanger", GetBeaconTier(levelNumber));
            SetBool(so, "beaconEnemyCustomOverride", false);
        }

        if (ReadBool(so, "verticalLaserEnabled", false))
        {
            SetDanger(so, "verticalLaserDanger", GetVerticalLaserTier(levelNumber));
            SetBool(so, "verticalLaserCustomOverride", false);
        }

        if (ReadBool(so, "horizontalLaserEnabled", false))
        {
            SetDanger(so, "horizontalLaserDanger", GetHorizontalLaserTier(levelNumber));
            SetBool(so, "horizontalLaserCustomOverride", false);
        }

        if (ReadBool(so, "bombTrapEnabled", false))
        {
            SetDanger(so, "bombDanger", GetBombTier(levelNumber));
            SetBool(so, "bombCustomOverride", false);
        }
    }

    private static void ApplyCombo(
        SerializedObject so,
        int levelNumber,
        bool usesScore,
        int winCondition)
    {
        if (!usesScore || levelNumber == 1)
        {
            SetBool(so, "comboEnabled", false);
            ClearComboStages(so);
            return;
        }

        SetBool(so, "comboEnabled", true);

        float timeLimit;
        if (levelNumber <= 5)
            timeLimit = 2.25f;
        else if (levelNumber <= 13)
            timeLimit = 2.18f;
        else if (levelNumber <= 26)
            timeLimit = 2.12f;
        else
            timeLimit = 2.08f;

        // Timed-score needs a little more route consistency, not a larger multiplier.
        if (winCondition == 2)
            timeLimit += 0.05f;

        SetFloat(so, "comboTimeLimit", timeLimit);

        SerializedProperty stages = so.FindProperty("comboSpeedStages");
        if (stages == null || !stages.isArray)
            return;

        if (levelNumber <= 5)
        {
            ResizeStages(stages, 1);
            SetComboStage(stages, 0, 2, 4, 1.02f);
        }
        else if (levelNumber <= 26)
        {
            ResizeStages(stages, 2);
            SetComboStage(stages, 0, 2, 4, 1.02f);
            SetComboStage(stages, 1, 3, 10, levelNumber <= 13 ? 1.04f : 1.045f);
        }
        else
        {
            ResizeStages(stages, 3);
            SetComboStage(stages, 0, 2, 4, 1.03f);
            SetComboStage(stages, 1, 3, 10, 1.055f);
            SetComboStage(stages, 2, 4, 18, levelNumber >= 35 ? 1.08f : 1.07f);
        }

        // Old serialized fallback fields may still exist in some project revisions.
        // Keep them aligned without relying on them.
        SetFloatIfExists(so, "playerComboSpeedBonus", levelNumber >= 27 ? 1.03f : 1.02f);
        SetIntIfExists(so, "maxCombo", levelNumber <= 5 ? 2 : (levelNumber <= 26 ? 3 : 4));
    }

    private static void ApplyScoreEconomy(
        SerializedObject so,
        int levelNumber,
        bool usesScore,
        int winCondition)
    {
        if (!usesScore)
        {
            SetBool(so, "normalCoinEnabled", false);
            SetBool(so, "goldCoinEnabled", false);
            SetBool(so, "rareCoinEnabled", false);
            return;
        }

        SetBool(so, "normalCoinEnabled", true);
        SetIntIfExists(so, "normalCoinValue", 1);
        SetIntIfExists(so, "goldCoinValue", 3);
        SetIntIfExists(so, "rareCoinValue", 5);

        bool gold = levelNumber >= 6;
        bool rare = levelNumber >= 14;

        SetBool(so, "goldCoinEnabled", gold);
        SetBool(so, "rareCoinEnabled", rare);

        if (!gold)
        {
            SetFloat(so, "normalCoinChance", 100f);
            SetFloat(so, "goldCoinChance", 0f);
            SetFloat(so, "rareCoinChance", 0f);
        }
        else if (!rare)
        {
            SetFloat(so, "normalCoinChance", 78f);
            SetFloat(so, "goldCoinChance", 22f);
            SetFloat(so, "rareCoinChance", 0f);
        }
        else if (levelNumber <= 20)
        {
            SetFloat(so, "normalCoinChance", 74f);
            SetFloat(so, "goldCoinChance", 22f);
            SetFloat(so, "rareCoinChance", 4f);
        }
        else if (levelNumber <= 30)
        {
            SetFloat(so, "normalCoinChance", 73f);
            SetFloat(so, "goldCoinChance", 22f);
            SetFloat(so, "rareCoinChance", 5f);
        }
        else
        {
            SetFloat(so, "normalCoinChance", 72f);
            SetFloat(so, "goldCoinChance", 22f);
            SetFloat(so, "rareCoinChance", 6f);
        }

        // Timed-score gets supply cadence, not inflated Rare Coin odds.
        float spawnInterval;
        int maxCoins;

        if (winCondition == 2)
        {
            spawnInterval =
                levelNumber <= 20 ? 0.78f :
                levelNumber <= 30 ? 0.76f :
                0.74f;

            maxCoins = 9;
        }
        else
        {
            spawnInterval =
                levelNumber <= 5 ? 0.92f :
                levelNumber <= 13 ? 0.88f :
                levelNumber <= 26 ? 0.85f :
                0.82f;

            maxCoins =
                levelNumber <= 10 ? 7 :
                levelNumber <= 25 ? 8 :
                9;
        }

        SetFloat(so, "coinSpawnInterval", spawnInterval);
        SetInt(so, "maxCoinCount", maxCoins);
    }

    private static void ApplyBossTiming(SerializedObject so, int winCondition)
    {
        if (!ReadBool(so, "bossEnabled", false))
            return;

        int winScore = ReadInt(so, "winScore", 1);
        float timeLimit = ReadFloat(so, "timeLimit", 1f);

        if (winCondition == 0) // Reach Score
        {
            SetEnumIndex(so, "bossSpawnCondition", 0);
            SetInt(
                so,
                "bossSpawnScore",
                Mathf.Clamp(Mathf.RoundToInt(winScore * 0.72f), 1, Mathf.Max(1, winScore - 1))
            );
        }
        else if (winCondition == 1) // Survive Time
        {
            SetEnumIndex(so, "bossSpawnCondition", 1);

            // In this project bossSpawnTime is remaining countdown time.
            // 65% remaining = boss enters after ~35% of the mission has elapsed.
            SetFloat(
                so,
                "bossSpawnTime",
                Mathf.Clamp(timeLimit * 0.65f, 1f, Mathf.Max(1f, timeLimit - 1f))
            );
        }
        else // Reach Score Within Time
        {
            // Score trigger guarantees the player meets the Boss before the target,
            // independent of how quickly coins were routed.
            SetEnumIndex(so, "bossSpawnCondition", 0);
            SetInt(
                so,
                "bossSpawnScore",
                Mathf.Clamp(Mathf.RoundToInt(winScore * 0.68f), 1, Mathf.Max(1, winScore - 1))
            );
        }
    }

    private static void CapStaticObstacles(SerializedObject so, int levelNumber)
    {
        if (levelNumber == 1)
        {
            DisableAllStaticObstacles(so);
            SetInt(so, "randomObstacleCount", 0);
            return;
        }

        int spawnMode = ReadEnumIndex(so, "obstacleSpawnMode", 0);
        SerializedProperty obstacles = so.FindProperty("levelObstacles");

        if (spawnMode == 1)
        {
            int enabledPool = CountEnabledObstaclePool(obstacles);
            int requested = ReadInt(so, "randomObstacleCount", 0);
            requested = Mathf.Clamp(requested, 0, MaxStaticObstacles);

            if (enabledPool > 0)
                requested = Mathf.Min(requested, enabledPool);

            SetInt(so, "randomObstacleCount", requested);
            return;
        }

        if (obstacles == null || !obstacles.isArray)
            return;

        int enabled = 0;

        for (int i = 0; i < obstacles.arraySize; i++)
        {
            SerializedProperty item = obstacles.GetArrayElementAtIndex(i);
            SerializedProperty prefab = item.FindPropertyRelative("prefab");
            SerializedProperty active = item.FindPropertyRelative("enabled");

            if (prefab == null || active == null)
                continue;

            if (prefab.objectReferenceValue == null || !active.boolValue)
                continue;

            enabled++;

            if (enabled > MaxStaticObstacles)
                active.boolValue = false;
        }
    }

    private static void ApplyProgressionMetadata(SerializedObject so, int levelNumber)
    {
        SerializedProperty progression = so.FindProperty("mechanicProgression");
        if (progression == null)
            return;

        SetRelativeEnum(progression, "normalEnemy",
            levelNumber == 2 ? 1 : 0);

        SetRelativeEnum(progression, "combo",
            levelNumber == 2 ? 1 : 0);

        SetRelativeEnum(progression, "goldCoin",
            levelNumber == 6 ? 1 : 0);

        SetRelativeEnum(progression, "rareCoin",
            levelNumber == 14 ? 1 : 0);

        if (levelNumber == 1)
            SetRelativeEnum(progression, "normalCoin", 1);
    }

    private static void ApplyDangerProfile(DangerBalanceProfile profile)
    {
        SerializedObject so = new SerializedObject(profile);
        so.Update();

        // NORMAL ENEMY
        // Critical rule: every D1-D5 maxSpeed stays below the 6.4 player base speed.
        ApplyNormalTier(so, 0, 2.4f, 3.0f, 5.20f, 0.11f, 3.0f, 0.28f, 1.9f, 0.78f, 0.72f);
        ApplyNormalTier(so, 1, 2.8f, 3.5f, 5.60f, 0.14f, 3.3f, 0.32f, 2.2f, 0.82f, 0.78f);
        ApplyNormalTier(so, 2, 3.2f, 4.0f, 5.90f, 0.16f, 3.6f, 0.36f, 2.5f, 0.86f, 0.84f);
        ApplyNormalTier(so, 3, 3.6f, 4.4f, 6.15f, 0.18f, 3.9f, 0.40f, 2.8f, 0.90f, 0.90f);
        ApplyNormalTier(so, 4, 4.0f, 4.8f, 6.30f, 0.20f, 4.2f, 0.44f, 3.1f, 0.94f, 0.96f);

        // PROJECTILE ENEMY
        ApplyProjectileTier(so, 0, 2.6f, 7.2f, 4.2f, 1.85f, 5.4f, 0.48f, 2.2f, 3.4f, 0.16f, 1.6f);
        ApplyProjectileTier(so, 1, 2.9f, 7.0f, 4.1f, 1.60f, 5.8f, 0.54f, 1.9f, 3.0f, 0.21f, 1.9f);
        ApplyProjectileTier(so, 2, 3.2f, 6.8f, 4.0f, 1.35f, 6.2f, 0.60f, 1.6f, 2.6f, 0.27f, 2.2f);
        ApplyProjectileTier(so, 3, 3.5f, 6.6f, 3.9f, 1.15f, 6.6f, 0.67f, 1.3f, 2.2f, 0.33f, 2.5f);
        ApplyProjectileTier(so, 4, 3.8f, 6.4f, 3.8f, 1.00f, 7.0f, 0.74f, 1.1f, 1.9f, 0.38f, 2.8f);

        // HUNTER
        ApplyHunterTier(so, 0, 6.4f, 1.60f, 1.45f, 1.25f, 12.5f, 0.55f, 1.30f);
        ApplyHunterTier(so, 1, 6.3f, 1.45f, 1.30f, 1.10f, 13.5f, 0.58f, 1.15f);
        ApplyHunterTier(so, 2, 6.2f, 1.30f, 1.15f, 0.95f, 14.5f, 0.60f, 1.00f);
        ApplyHunterTier(so, 3, 6.1f, 1.15f, 1.00f, 0.82f, 15.5f, 0.62f, 0.90f);
        ApplyHunterTier(so, 4, 6.0f, 1.00f, 0.90f, 0.72f, 16.5f, 0.64f, 0.80f);

        // BOSS body speed also remains below player speed.
        ApplyBossTier(so, 0, 4.0f, 6.0f, 1.10f, 1.10f, 3.2f);
        ApplyBossTier(so, 1, 4.4f, 6.5f, 0.95f, 1.15f, 3.6f);
        ApplyBossTier(so, 2, 4.8f, 7.0f, 0.80f, 1.20f, 4.0f);
        ApplyBossTier(so, 3, 5.2f, 7.5f, 0.68f, 1.30f, 4.5f);
        ApplyBossTier(so, 4, 5.6f, 8.0f, 0.58f, 1.40f, 5.0f);

        // BEACON
        // Normal max-speed multiplier is always exactly 1.0.
        ApplyBeaconTier(so, 0, 3.4f, 2.4f, 0.75f, 1.8f, 10f, 1.10f, 1.08f, 1.00f, 1.06f, 1.06f, 1.06f, 0.95f, 0.95f, 1.05f, 0.95f, 2.6f, 6.0f, 0.45f, 8f);
        ApplyBeaconTier(so, 1, 3.0f, 2.1f, 0.65f, 1.75f, 11f, 1.12f, 1.10f, 1.00f, 1.08f, 1.08f, 1.10f, 0.92f, 0.92f, 1.08f, 0.92f, 2.8f, 5.8f, 0.50f, 7f);
        ApplyBeaconTier(so, 2, 2.6f, 1.85f, 0.55f, 1.70f, 12f, 1.14f, 1.12f, 1.00f, 1.10f, 1.10f, 1.12f, 0.89f, 0.89f, 1.11f, 0.89f, 3.0f, 5.6f, 0.55f, 6f);
        ApplyBeaconTier(so, 3, 2.2f, 1.60f, 0.45f, 1.65f, 13f, 1.16f, 1.14f, 1.00f, 1.13f, 1.13f, 1.16f, 0.85f, 0.85f, 1.15f, 0.85f, 3.2f, 5.4f, 0.60f, 5f);
        ApplyBeaconTier(so, 4, 1.9f, 1.40f, 0.38f, 1.60f, 14f, 1.18f, 1.15f, 1.00f, 1.15f, 1.15f, 1.18f, 0.82f, 0.82f, 1.18f, 0.82f, 3.4f, 5.2f, 0.65f, 4f);

        // LASERS
        ApplyLaserTier(so, "verticalLaserLevels", 0, 12f, 18f, 2.4f, 1.10f, 0.40f, 1.00f);
        ApplyLaserTier(so, "verticalLaserLevels", 1, 9f, 15f, 2.1f, 1.25f, 0.45f, 1.00f);
        ApplyLaserTier(so, "verticalLaserLevels", 2, 7f, 12f, 1.75f, 1.40f, 0.50f, 1.08f);
        ApplyLaserTier(so, "verticalLaserLevels", 3, 5.5f, 9.5f, 1.45f, 1.55f, 0.58f, 1.15f);
        ApplyLaserTier(so, "verticalLaserLevels", 4, 4.5f, 8f, 1.20f, 1.70f, 0.65f, 1.22f);

        ApplyLaserTier(so, "horizontalLaserLevels", 0, 12f, 18f, 2.4f, 1.10f, 0.40f, 1.00f);
        ApplyLaserTier(so, "horizontalLaserLevels", 1, 9f, 15f, 2.1f, 1.25f, 0.45f, 1.00f);
        ApplyLaserTier(so, "horizontalLaserLevels", 2, 7f, 12f, 1.75f, 1.40f, 0.50f, 1.08f);
        ApplyLaserTier(so, "horizontalLaserLevels", 3, 5.5f, 9.5f, 1.45f, 1.55f, 0.58f, 1.15f);
        ApplyLaserTier(so, "horizontalLaserLevels", 4, 4.5f, 8f, 1.20f, 1.70f, 0.65f, 1.22f);

        // SPACE BOMB
        ApplyBombTier(so, 0, 8.5f, 12f, 2, 0.60f);
        ApplyBombTier(so, 1, 7.0f, 10.5f, 2, 0.50f);
        ApplyBombTier(so, 2, 5.8f, 9.0f, 3, 0.42f);
        ApplyBombTier(so, 3, 4.8f, 7.5f, 4, 0.34f);
        ApplyBombTier(so, 4, 4.0f, 6.5f, 4, 0.28f);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyNormalTier(
        SerializedObject so, int tier,
        float minStart, float maxStart, float maxSpeed, float acceleration,
        float predictionThreshold, float predictionTime, float maxPrediction,
        float separationRadius, float separationStrength)
    {
        SetTierFloat(so, "normalEnemyLevels", tier, "minStartSpeed", minStart);
        SetTierFloat(so, "normalEnemyLevels", tier, "maxStartSpeed", maxStart);
        SetTierFloat(so, "normalEnemyLevels", tier, "maxSpeed", maxSpeed);
        SetTierFloat(so, "normalEnemyLevels", tier, "speedIncreaseRate", acceleration);
        SetTierBool(so, "normalEnemyLevels", tier, "predictionEnabled", true);
        SetTierFloat(so, "normalEnemyLevels", tier, "predictionDistanceThreshold", predictionThreshold);
        SetTierFloat(so, "normalEnemyLevels", tier, "predictionTime", predictionTime);
        SetTierFloat(so, "normalEnemyLevels", tier, "maxPredictionDistance", maxPrediction);
        SetTierBool(so, "normalEnemyLevels", tier, "separationEnabled", true);
        SetTierFloat(so, "normalEnemyLevels", tier, "separationRadius", separationRadius);
        SetTierFloat(so, "normalEnemyLevels", tier, "separationStrength", separationStrength);
    }

    private static void ApplyProjectileTier(
        SerializedObject so, int tier,
        float moveSpeed, float stoppingDistance, float retreatDistance,
        float fireRate, float projectileSpeed,
        float strafeMultiplier, float strafeMin, float strafeMax,
        float predictionTime, float maxPrediction)
    {
        string a = "projectileEnemyLevels";
        SetTierFloat(so, a, tier, "moveSpeed", moveSpeed);
        SetTierFloat(so, a, tier, "stoppingDistance", stoppingDistance);
        SetTierFloat(so, a, tier, "retreatDistance", retreatDistance);
        SetTierFloat(so, a, tier, "fireRate", fireRate);
        SetTierFloat(so, a, tier, "projectileSpeed", projectileSpeed);
        SetTierBool(so, a, tier, "strafeEnabled", true);
        SetTierFloat(so, a, tier, "strafeSpeedMultiplier", strafeMultiplier);
        SetTierFloat(so, a, tier, "strafeDirectionChangeMinTime", strafeMin);
        SetTierFloat(so, a, tier, "strafeDirectionChangeMaxTime", strafeMax);
        SetTierBool(so, a, tier, "predictiveAimEnabled", true);
        SetTierFloat(so, a, tier, "predictionTime", predictionTime);
        SetTierFloat(so, a, tier, "maxPredictionDistance", maxPrediction);
        SetTierFloat(so, a, tier, "predictionDistanceThreshold", 3.0f + tier * 0.3f);
        SetTierBool(so, a, tier, "separationEnabled", true);
        SetTierFloat(so, a, tier, "separationRadius", 0.90f + tier * 0.05f);
        SetTierFloat(so, a, tier, "separationStrength", 0.45f + tier * 0.05f);
    }

    private static void ApplyHunterTier(
        SerializedObject so, int tier,
        float prepareDistance, float reposition, float recovery,
        float warning, float chargeSpeed, float maxCharge, float stun)
    {
        string a = "hunterEnemyLevels";
        SetTierFloat(so, a, tier, "prepareDistance", prepareDistance);
        SetTierFloat(so, a, tier, "repositionTime", reposition);
        SetTierFloat(so, a, tier, "recoveryTime", recovery);
        SetTierFloat(so, a, tier, "warningDuration", warning);
        SetTierFloat(so, a, tier, "chargeSpeed", chargeSpeed);
        SetTierFloat(so, a, tier, "maxChargeTime", maxCharge);
        SetTierFloat(so, a, tier, "stunDuration", stun);
    }

    private static void ApplyBossTier(
        SerializedObject so, int tier,
        float speed, float smoothness, float splitDelay, float splitDistance, float miniBossSpeed)
    {
        string a = "bossLevels";
        SetTierFloat(so, a, tier, "speed", speed);
        SetTierFloat(so, a, tier, "directionSmoothness", smoothness);
        SetTierBool(so, a, tier, "canSplit", true);
        SetTierFloat(so, a, tier, "splitDelay", splitDelay);
        SetTierFloat(so, a, tier, "splitDistance", splitDistance);
        SetTierFloat(so, a, tier, "miniBossSpeed", miniBossSpeed);
    }

    private static void ApplyBeaconTier(
        SerializedObject so, int tier,
        float activationDelay, float pulseInterval, float retargetInterval, float targetStopDistance,
        float buffDuration, float sizeMultiplier, float normalSpeedMultiplier, float normalMaxSpeedMultiplier,
        float projectileMoveMultiplier, float projectileShotMultiplier, float projectileFireMultiplier,
        float hunterRepositionMultiplier, float hunterWarningMultiplier, float hunterChargeMultiplier,
        float hunterStunMultiplier, float moveSpeed, float safeDistance, float wanderStrength,
        float respawnDelay)
    {
        string a = "beaconEnemyLevels";
        SetTierFloat(so, a, tier, "activationDelay", activationDelay);
        SetTierFloat(so, a, tier, "pulseInterval", pulseInterval);
        SetTierFloat(so, a, tier, "retargetInterval", retargetInterval);
        SetTierFloat(so, a, tier, "targetStopDistance", targetStopDistance);
        SetTierFloat(so, a, tier, "buffDuration", buffDuration);
        SetTierFloat(so, a, tier, "buffSizeMultiplier", sizeMultiplier);
        SetTierFloat(so, a, tier, "normalSpeedMultiplier", normalSpeedMultiplier);
        SetTierFloat(so, a, tier, "normalMaxSpeedMultiplier", normalMaxSpeedMultiplier);
        SetTierFloat(so, a, tier, "projectileMoveMultiplier", projectileMoveMultiplier);
        SetTierFloat(so, a, tier, "projectileShotMultiplier", projectileShotMultiplier);
        SetTierFloat(so, a, tier, "projectileFireMultiplier", projectileFireMultiplier);
        SetTierFloat(so, a, tier, "hunterRepositionMultiplier", hunterRepositionMultiplier);
        SetTierFloat(so, a, tier, "hunterWarningMultiplier", hunterWarningMultiplier);
        SetTierFloat(so, a, tier, "hunterChargeMultiplier", hunterChargeMultiplier);
        SetTierFloat(so, a, tier, "hunterStunMultiplier", hunterStunMultiplier);
        SetTierFloat(so, a, tier, "moveSpeed", moveSpeed);
        SetTierFloat(so, a, tier, "safeDistanceFromPlayer", safeDistance);
        SetTierFloat(so, a, tier, "wanderStrength", wanderStrength);

        // Optional in newer project revisions.
        SetTierFloat(so, a, tier, "respawnDelay", respawnDelay);
    }

    private static void ApplyLaserTier(
        SerializedObject so, string arrayName, int tier,
        float minSpawn, float maxSpawn, float warning, float life, float width, float extra)
    {
        SetTierFloat(so, arrayName, tier, "minSpawnTime", minSpawn);
        SetTierFloat(so, arrayName, tier, "maxSpawnTime", maxSpawn);
        SetTierFloat(so, arrayName, tier, "warningDuration", warning);
        SetTierFloat(so, arrayName, tier, "lifeTime", life);
        SetTierFloat(so, arrayName, tier, "width", width);
        SetTierFloat(so, arrayName, tier, "sizeExtra", extra);
    }

    private static void ApplyBombTier(
        SerializedObject so, int tier,
        float minSpawn, float maxSpawn, int maxCount, float safeTime)
    {
        string a = "bombLevels";
        SetTierFloat(so, a, tier, "minSpawnTime", minSpawn);
        SetTierFloat(so, a, tier, "maxSpawnTime", maxSpawn);
        SetTierInt(so, a, tier, "maxBombCount", maxCount);
        SetTierFloat(so, a, tier, "spawnSafeTime", safeTime);
    }

    private static int GetMissionDifficulty(int level)
    {
        if (level == 1) return 0;
        if (level == 2) return 1;
        if (level <= 5) return 2;
        if (level <= 13) return 3;
        if (level <= 28) return 4;
        return 5;
    }

    private static int GetNormalTier(int level)
    {
        if (level <= 5) return 1;
        if (level <= 14) return 2;
        if (level <= 27) return 3;
        return 4; // Three Stalkers + other threats: D5 is intentionally avoided.
    }

    private static int GetProjectileTier(int level, int count)
    {
        if (level <= 8) return 1;
        if (level <= 18) return 2;
        if (level <= 30) return 3;
        if (level <= 38) return 4;
        return count == 1 ? 5 : 4;
    }

    private static int GetHunterTier(int level, int count)
    {
        if (level <= 15) return 1;
        if (level <= 22) return 2;
        if (level <= 31) return 3;
        if (level <= 38) return 4;
        return count == 1 ? 5 : 4;
    }

    private static int GetBossTier(int level)
    {
        if (level <= 19) return 1;
        if (level <= 25) return 2;
        if (level <= 32) return 3;
        if (level <= 38) return 4;
        return 5;
    }

    private static int GetBeaconTier(int level)
    {
        if (level <= 18) return 1;
        if (level <= 25) return 2;
        if (level <= 33) return 3;
        return 4;
    }

    private static int GetVerticalLaserTier(int level)
    {
        if (level == 17) return 1;
        if (level <= 24) return 2;
        if (level <= 32) return 3;
        if (level <= 38) return 4;
        return 5;
    }

    private static int GetHorizontalLaserTier(int level)
    {
        if (level == 21) return 1;
        if (level <= 26) return 2;
        if (level <= 34) return 3;
        if (level <= 39) return 4;
        return 5;
    }

    private static int GetBombTier(int level)
    {
        if (level == 9) return 1;
        if (level <= 17) return 2;
        if (level <= 28) return 3;
        if (level <= 36) return 4;
        return 5;
    }

    private static List<LevelConfig> LoadLevels()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelConfig");
        List<LevelConfig> levels = new List<LevelConfig>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);

            if (config != null && config.levelNumber >= 1 && config.levelNumber <= 40)
                levels.Add(config);
        }

        return levels.OrderBy(x => x.levelNumber).ToList();
    }

    private static DangerBalanceProfile FindSharedProfile(List<LevelConfig> levels)
    {
        foreach (LevelConfig level in levels)
        {
            if (level == null)
                continue;

            SerializedObject so = new SerializedObject(level);
            SerializedProperty p = so.FindProperty("dangerBalanceProfile");

            if (p != null && p.objectReferenceValue is DangerBalanceProfile profile)
                return profile;
        }

        return null;
    }

    private static void SetComboStage(
        SerializedProperty stages,
        int index,
        int multiplier,
        int coins,
        float speedMultiplier)
    {
        if (stages == null || !stages.isArray || index < 0 || index >= stages.arraySize)
            return;

        SerializedProperty stage = stages.GetArrayElementAtIndex(index);
        SetRelativeInt(stage, "comboMultiplier", multiplier);
        SetRelativeInt(stage, "coinsRequired", coins);
        SetRelativeFloat(stage, "playerSpeedMultiplier", speedMultiplier);
    }

    private static void ResizeStages(SerializedProperty stages, int size)
    {
        if (stages != null && stages.isArray)
            stages.arraySize = Mathf.Max(0, size);
    }

    private static void ClearComboStages(SerializedObject so)
    {
        SerializedProperty stages = so.FindProperty("comboSpeedStages");
        if (stages != null && stages.isArray)
            stages.arraySize = 0;

        SetIntIfExists(so, "maxCombo", 1);
    }

    private static int CountSpawnedStaticObstacles(SerializedObject so)
    {
        int mode = ReadEnumIndex(so, "obstacleSpawnMode", 0);

        if (mode == 1)
            return ReadInt(so, "randomObstacleCount", 0);

        SerializedProperty obstacles = so.FindProperty("levelObstacles");
        return CountEnabledObstaclePool(obstacles);
    }

    private static int CountEnabledObstaclePool(SerializedProperty obstacles)
    {
        if (obstacles == null || !obstacles.isArray)
            return 0;

        int count = 0;

        for (int i = 0; i < obstacles.arraySize; i++)
        {
            SerializedProperty item = obstacles.GetArrayElementAtIndex(i);
            SerializedProperty prefab = item.FindPropertyRelative("prefab");
            SerializedProperty enabled = item.FindPropertyRelative("enabled");

            if (prefab != null &&
                enabled != null &&
                enabled.boolValue &&
                prefab.objectReferenceValue != null)
            {
                count++;
            }
        }

        return count;
    }

    private static void DisableAllStaticObstacles(SerializedObject so)
    {
        SerializedProperty obstacles = so.FindProperty("levelObstacles");
        if (obstacles == null || !obstacles.isArray)
            return;

        for (int i = 0; i < obstacles.arraySize; i++)
        {
            SerializedProperty enabled =
                obstacles.GetArrayElementAtIndex(i).FindPropertyRelative("enabled");

            if (enabled != null)
                enabled.boolValue = false;
        }
    }

    private static bool HasAnyLethalThreat(SerializedObject so)
    {
        return
            ReadInt(so, "normalEnemyCount", 0) > 0 ||
            ReadInt(so, "projectileEnemyCount", 0) > 0 ||
            ReadInt(so, "hunterEnemyCount", 0) > 0 ||
            ReadInt(so, "beaconEnemyCount", 0) > 0 ||
            ReadBool(so, "bossEnabled", false) ||
            ReadBool(so, "verticalLaserEnabled", false) ||
            ReadBool(so, "horizontalLaserEnabled", false) ||
            ReadBool(so, "bombTrapEnabled", false);
    }

    private static void SetTierFloat(
        SerializedObject so, string arrayName, int tier, string field, float value)
    {
        SerializedProperty p = GetTierField(so, arrayName, tier, field);
        if (p != null)
            p.floatValue = value;
    }

    private static void SetTierInt(
        SerializedObject so, string arrayName, int tier, string field, int value)
    {
        SerializedProperty p = GetTierField(so, arrayName, tier, field);
        if (p != null)
            p.intValue = value;
    }

    private static void SetTierBool(
        SerializedObject so, string arrayName, int tier, string field, bool value)
    {
        SerializedProperty p = GetTierField(so, arrayName, tier, field);
        if (p != null)
            p.boolValue = value;
    }

    private static SerializedProperty GetTierField(
        SerializedObject so, string arrayName, int tier, string field)
    {
        SerializedProperty array = so.FindProperty(arrayName);
        if (array == null || !array.isArray || tier < 0 || tier >= array.arraySize)
            return null;

        return array.GetArrayElementAtIndex(tier).FindPropertyRelative(field);
    }

    private static void SetDanger(SerializedObject so, string name, int tier)
    {
        SetEnumIndex(so, name, Mathf.Clamp(tier - 1, 0, 4));
    }

    private static void SetEnumIndex(SerializedObject so, string name, int index)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null)
            p.enumValueIndex = Mathf.Max(0, index);
    }

    private static int ReadEnumIndex(SerializedObject so, string name, int fallback)
    {
        SerializedProperty p = so.FindProperty(name);
        return p != null ? p.enumValueIndex : fallback;
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null)
            p.intValue = value;
    }

    private static void SetIntIfExists(SerializedObject so, string name, int value)
    {
        SetInt(so, name, value);
    }

    private static int ReadInt(SerializedObject so, string name, int fallback)
    {
        SerializedProperty p = so.FindProperty(name);
        return p != null ? p.intValue : fallback;
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null)
            p.floatValue = value;
    }

    private static void SetFloatIfExists(SerializedObject so, string name, float value)
    {
        SetFloat(so, name, value);
    }

    private static float ReadFloat(SerializedObject so, string name, float fallback)
    {
        SerializedProperty p = so.FindProperty(name);
        return p != null ? p.floatValue : fallback;
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null)
            p.boolValue = value;
    }

    private static bool ReadBool(SerializedObject so, string name, bool fallback)
    {
        SerializedProperty p = so.FindProperty(name);
        return p != null ? p.boolValue : fallback;
    }

    private static void SetRelativeInt(SerializedProperty parent, string name, int value)
    {
        SerializedProperty p = parent.FindPropertyRelative(name);
        if (p != null)
            p.intValue = value;
    }

    private static void SetRelativeFloat(SerializedProperty parent, string name, float value)
    {
        SerializedProperty p = parent.FindPropertyRelative(name);
        if (p != null)
            p.floatValue = value;
    }

    private static void SetRelativeEnum(SerializedProperty parent, string name, int enumIndex)
    {
        SerializedProperty p = parent.FindPropertyRelative(name);
        if (p != null)
            p.enumValueIndex = Mathf.Max(0, enumIndex);
    }
}
#endif
