#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FatefulRushCombo6xRebalance
{
    private const int StageCount = 5;

    private static readonly int[] ComboMultipliers =
    {
        2, 3, 4, 5, 6
    };

    private static readonly int[] CoinsRequired =
    {
        3, 7, 14, 18, 22
    };

    private static readonly float[] PlayerSpeedMultipliers =
    {
        1.03f, 1.06f, 1.09f, 1.12f, 1.15f
    };

    [MenuItem("Tools/Fateful Rush/Combo/Apply 6x Combo Rebalance")]
    public static void Apply()
    {
        List<LevelConfig> levels = LoadLevels();

        if (levels.Count == 0)
        {
            Debug.LogError(
                "[Fateful Rush Combo] No LevelConfig assets found."
            );
            return;
        }

        int changedLevels = 0;
        int changedTestLevels = 0;

        Undo.SetCurrentGroupName("Fateful Rush 6x Combo Rebalance");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (LevelConfig level in levels)
        {
            if (level == null)
                continue;

            bool isTestLevel = IsTestLevel(level);

            // Normal campaign levels are only changed when combo is enabled.
            // Test Level Config is always normalized to the same combo setup.
            if (!level.comboEnabled && !isTestLevel)
                continue;

            Undo.RecordObject(
                level,
                isTestLevel
                    ? "Apply 6x Combo Rebalance - Test Level"
                    : $"Apply 6x Combo Rebalance - Level {level.levelNumber}"
            );

            if (isTestLevel)
            {
                level.comboEnabled = true;

                // Test config uses the standard post-intro timing.
                level.comboTimeLimit = 2.2f;
                changedTestLevels++;
            }
            else
            {
                // Existing campaign timing rule:
                // Levels 1-10 = 2.5s, Levels 11-40 = 2.2s.
                level.comboTimeLimit =
                    level.levelNumber >= 1 && level.levelNumber <= 10
                        ? 2.5f
                        : 2.2f;
            }

            level.comboSpeedStages = CreateStages();

            EditorUtility.SetDirty(level);
            changedLevels++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log(
            $"[Fateful Rush Combo] Updated {changedLevels} LevelConfig asset(s). " +
            $"Test configs updated: {changedTestLevels}. " +
            "Combo stages now use 2x-6x with coin requirements 3 / 6 / 9 / 15 / 20."
        );

        Validate();
    }

    [MenuItem("Tools/Fateful Rush/Combo/Validate 6x Combo Rebalance")]
    public static void Validate()
    {
        List<LevelConfig> levels = LoadLevels();

        int checkedLevels = 0;
        int errors = 0;
        int testLevels = 0;

        foreach (LevelConfig level in levels)
        {
            if (level == null)
                continue;

            bool isTestLevel = IsTestLevel(level);

            if (!level.comboEnabled && !isTestLevel)
                continue;

            checkedLevels++;

            if (isTestLevel)
                testLevels++;

            if (!IsCorrect(level, isTestLevel))
            {
                errors++;

                Debug.LogError(
                    isTestLevel
                        ? $"[Fateful Rush Combo] Test Level Config ({level.name}) does not match the intended combo setup."
                        : $"[Fateful Rush Combo] Level {level.levelNumber} ({level.levelName}) does not match the intended combo setup.",
                    level
                );
            }
        }

        if (errors == 0)
        {
            Debug.Log(
                $"[Fateful Rush Combo] Validation successful. " +
                $"{checkedLevels} config(s) checked, including {testLevels} test config(s). " +
                "All use the intended 5-stage 2x-6x combo setup."
            );
        }
        else
        {
            Debug.LogError(
                $"[Fateful Rush Combo] Validation finished with {errors} error(s)."
            );
        }
    }

    private static ComboSpeedStage[] CreateStages()
    {
        ComboSpeedStage[] stages =
            new ComboSpeedStage[StageCount];

        for (int i = 0; i < StageCount; i++)
        {
            stages[i] = new ComboSpeedStage
            {
                comboMultiplier = ComboMultipliers[i],
                coinsRequired = CoinsRequired[i],
                playerSpeedMultiplier = PlayerSpeedMultipliers[i]
            };
        }

        return stages;
    }

    private static bool IsCorrect(
        LevelConfig level,
        bool isTestLevel)
    {
        if (isTestLevel)
        {
            if (!level.comboEnabled)
                return false;

            if (!Mathf.Approximately(level.comboTimeLimit, 2.2f))
                return false;
        }
        else
        {
            float expectedTimeLimit =
                level.levelNumber >= 1 && level.levelNumber <= 10
                    ? 2.5f
                    : 2.2f;

            if (!Mathf.Approximately(
                    level.comboTimeLimit,
                    expectedTimeLimit))
            {
                return false;
            }
        }

        if (level.comboSpeedStages == null ||
            level.comboSpeedStages.Length != StageCount)
        {
            return false;
        }

        for (int i = 0; i < StageCount; i++)
        {
            ComboSpeedStage stage = level.comboSpeedStages[i];

            if (stage == null)
                return false;

            if (stage.comboMultiplier != ComboMultipliers[i])
                return false;

            if (stage.coinsRequired != CoinsRequired[i])
                return false;

            if (!Mathf.Approximately(
                    stage.playerSpeedMultiplier,
                    PlayerSpeedMultipliers[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTestLevel(LevelConfig level)
    {
        if (level == null)
            return false;

        if (level.levelNumber == 0)
            return true;

        string assetName =
            string.IsNullOrWhiteSpace(level.name)
                ? string.Empty
                : level.name.ToLowerInvariant();

        string levelName =
            string.IsNullOrWhiteSpace(level.levelName)
                ? string.Empty
                : level.levelName.ToLowerInvariant();

        return assetName.Contains("test") ||
               levelName.Contains("test");
    }

    private static List<LevelConfig> LoadLevels()
    {
        return AssetDatabase
            .FindAssets("t:LevelConfig")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<LevelConfig>)
            .Where(level => level != null)
            .OrderBy(level => level.levelNumber)
            .ToList();
    }
}
#endif
