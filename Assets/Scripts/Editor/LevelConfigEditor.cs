using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelConfig))]
public class LevelConfigEditor : Editor
{
    private static readonly int[] DefaultComboCoins =
    {
        3, 7, 14, 18, 22
    };

    private static readonly float[] DefaultComboSpeedMultipliers =
    {
        1.03f, 1.06f, 1.09f, 1.12f, 1.15f
    };

    private enum EditorViewMode
    {
        Basic,
        Advanced
    }

    private enum MechanicId
    {
        ReachScoreMode,
        SurviveTimeMode,
        TimedScoreMode,
        Dash,
        Clone,
        Combo,
        NormalCoin,
        GoldCoin,
        RareCoin,
        StaticObstacles,
        NormalEnemy,
        ProjectileEnemy,
        HunterEnemy,
        Boss,
        BeaconEnemy,
        Armor,
        Slow,
        VerticalLaser,
        HorizontalLaser,
        SpaceBomb
    }

    private enum ValidationSeverity
    {
        Error,
        Warning,
        Suggestion
    }

    private sealed class MechanicDescriptor
    {
        public readonly MechanicId id;
        public readonly string category;
        public readonly string label;
        public readonly string propertyPath;
        public readonly Func<LevelConfig, bool> isActive;
        public readonly string briefingText;

        public MechanicDescriptor(
            MechanicId id,
            string category,
            string label,
            string propertyPath,
            Func<LevelConfig, bool> isActive,
            string briefingText)
        {
            this.id = id;
            this.category = category;
            this.label = label;
            this.propertyPath = propertyPath;
            this.isActive = isActive;
            this.briefingText = briefingText;
        }
    }

    private sealed class ValidationIssue
    {
        public readonly ValidationSeverity severity;
        public readonly string message;
        public readonly Action<LevelConfig> fix;
        public readonly string fixLabel;
        public readonly string propertyPath;

        public ValidationIssue(
            ValidationSeverity severity,
            string message,
            Action<LevelConfig> fix = null,
            string fixLabel = "AUTO FIX",
            string propertyPath = null)
        {
            this.severity = severity;
            this.message = message;
            this.fix = fix;
            this.fixLabel = fixLabel;
            this.propertyPath = propertyPath;
        }
    }

    private sealed class TimelineRow
    {
        public string label;
        public string tooltip;
        public Color color;
        public bool hasWindow;
        public float windowStart;
        public float windowEnd;
        public readonly List<float> markers = new List<float>();
    }

    private static readonly MechanicDescriptor[] MechanicDescriptors =
    {
        new MechanicDescriptor(
            MechanicId.ReachScoreMode,
            "MISSION MODE",
            "Reach Score",
            "mechanicProgression.reachScoreMode",
            config => config.winCondition == WinConditionType.ReachScore,
            "This mission is completed by collecting the required score."
        ),
        new MechanicDescriptor(
            MechanicId.SurviveTimeMode,
            "MISSION MODE",
            "Survive Time",
            "mechanicProgression.surviveTimeMode",
            config => config.winCondition == WinConditionType.SurviveTime,
            "This mission removes score pressure. Stay alive until the countdown reaches zero."
        ),
        new MechanicDescriptor(
            MechanicId.TimedScoreMode,
            "MISSION MODE",
            "Timed Score",
            "mechanicProgression.timedScoreMode",
            config => config.winCondition == WinConditionType.ReachScoreWithinTime,
            "Collect the required score before the countdown reaches zero."
        ),
        new MechanicDescriptor(
            MechanicId.Dash,
            "PLAYER TOOLS",
            "Dash",
            "mechanicProgression.dash",
            config => config.dashEnabled,
            "Dash gives you a short burst of movement. Use it to escape danger and create a safer route."
        ),
        new MechanicDescriptor(
            MechanicId.Clone,
            "PLAYER TOOLS",
            "Clone",
            "mechanicProgression.clone",
            config => config.cloneEnabled,
            "Deploy the clone to redirect enemy attention. After use, it becomes available again when its cooldown ends."
        ),
        new MechanicDescriptor(
            MechanicId.Combo,
            "SCORING / ARENA",
            "Combo",
            "mechanicProgression.combo",
            config => config.UsesScore && config.comboEnabled,
            "Collect coins quickly to build your combo and increase your movement speed."
        ),
        new MechanicDescriptor(
            MechanicId.NormalCoin,
            "SCORING / ARENA",
            "1-Point Coin",
            "mechanicProgression.normalCoin",
            config => config.UsesScore && config.normalCoinEnabled,
            "Standard coins are worth 1 point and form the foundation of the scoring route."
        ),
        new MechanicDescriptor(
            MechanicId.GoldCoin,
            "SCORING / ARENA",
            "3-Point Coin",
            "mechanicProgression.goldCoin",
            config => config.UsesScore && config.goldCoinEnabled,
            "Gold coins are worth 3 points. Reaching them can shorten the mission, but may require a riskier route."
        ),
        new MechanicDescriptor(
            MechanicId.RareCoin,
            "SCORING / ARENA",
            "5-Point Coin",
            "mechanicProgression.rareCoin",
            config => config.UsesScore && config.rareCoinEnabled,
            "Rare coins are worth 5 points. Treat them as high-value opportunities rather than safe pickups."
        ),
        new MechanicDescriptor(
            MechanicId.StaticObstacles,
            "SCORING / ARENA",
            "Static Obstacles",
            "mechanicProgression.staticObstacles",
            HasActiveObstacles,
            "Static obstacles break direct routes and force you to plan movement around the arena."
        ),
        new MechanicDescriptor(
            MechanicId.NormalEnemy,
            "ENEMIES",
            "Normal Enemy",
            "mechanicProgression.normalEnemy",
            config => config.normalEnemyCount > 0,
            "Normal enemies pursue you continuously. Keep moving and avoid letting several enemies close in at once."
        ),
        new MechanicDescriptor(
            MechanicId.ProjectileEnemy,
            "ENEMIES",
            "Projectile Enemy",
            "mechanicProgression.projectileEnemy",
            config => config.projectileEnemyCount > 0,
            "Projectile enemies attack from range. Keep moving and watch the firing line before committing to a route."
        ),
        new MechanicDescriptor(
            MechanicId.HunterEnemy,
            "ENEMIES",
            "Hunter Enemy",
            "mechanicProgression.hunterEnemy",
            config => config.hunterEnemyCount > 0,
            "Hunters reposition, warn, then charge. Read the warning and move out of the attack path."
        ),
        new MechanicDescriptor(
            MechanicId.Boss,
            "ENEMIES",
            "Boss",
            "mechanicProgression.boss",
            config => config.bossEnabled,
            "The boss enters after the mission trigger is reached. Preserve space and prepare for its stronger pressure."
        ),
        new MechanicDescriptor(
            MechanicId.BeaconEnemy,
            "ENEMIES",
            "Beacon Enemy",
            "mechanicProgression.beaconEnemy",
            config => config.beaconEnemyCount > 0,
            "The beacon strengthens nearby enemies. Reposition before its buff turns an ordinary encounter into heavy pressure."
        ),
        new MechanicDescriptor(
            MechanicId.Armor,
            "POWER UPS",
            "Armor",
            "mechanicProgression.armor",
            config => config.armorEnabled,
            "Armor absorbs one lethal hit. It is protection, not permission to stop moving."
        ),
        new MechanicDescriptor(
            MechanicId.Slow,
            "POWER UPS",
            "Slow",
            "mechanicProgression.slow",
            config => config.slowEnabled,
            "The slow power-up temporarily reduces enemy and hazard pressure."
        ),
        new MechanicDescriptor(
            MechanicId.VerticalLaser,
            "TRAPS",
            "Vertical Laser",
            "mechanicProgression.verticalLaser",
            config => config.verticalLaserEnabled,
            "Vertical lasers announce their position before activating. Leave the marked lane before the warning ends."
        ),
        new MechanicDescriptor(
            MechanicId.HorizontalLaser,
            "TRAPS",
            "Horizontal Laser",
            "mechanicProgression.horizontalLaser",
            config => config.horizontalLaserEnabled,
            "Horizontal lasers cut across the arena after a warning. Read the safe side and move early."
        ),
        new MechanicDescriptor(
            MechanicId.SpaceBomb,
            "TRAPS",
            "Space Bomb",
            "mechanicProgression.spaceBomb",
            config => config.bombTrapEnabled,
            "Space bombs create lethal zones inside the arena. Do not let them block your next escape route."
        )
    };

    private static readonly string[] MechanicStatusLabels =
    {
        "Already Known",
        "Introduced In This Level",
        "Final Challenge"
    };

    private static List<LevelConfig> cachedLevelConfigs;
    private static double cachedLevelConfigTime;

    private EditorViewMode viewMode;

    private bool summaryExpanded = true;
    private bool coreExpanded = true;
    private bool briefingExpanded = true;
    private bool musicExpanded = true;
    private bool playerExpanded = true;
    private bool abilitiesExpanded;
    private bool comboExpanded;
    private bool backgroundExpanded;
    private bool coinsExpanded = true;
    private bool obstaclesExpanded = true;
    private bool balanceExpanded = true;
    private bool enemiesExpanded = true;
    private bool powerUpsExpanded;
    private bool trapsExpanded = true;
    private bool timelineExpanded = true;
    private bool progressionExpanded = true;
    private bool validationExpanded = true;
    private bool showInactiveMechanics;
    private string focusedValidationPropertyPath;

    private bool normalEnemyExpanded = true;
    private bool projectileEnemyExpanded = true;
    private bool hunterEnemyExpanded = true;
    private bool bossExpanded = true;
    private bool beaconExpanded = true;

    private bool IsAdvanced => true;

    private WinConditionType SelectedWinCondition =>
        (WinConditionType)EnumValue("winCondition");

    private bool UsesScore =>
        SelectedWinCondition == WinConditionType.ReachScore ||
        SelectedWinCondition == WinConditionType.ReachScoreWithinTime;

    private void OnEnable()
    {
        EnsureProgressionMetadata();
    }

    public override void OnInspectorGUI()
    {
        EnsureProgressionMetadata();
        serializedObject.Update();

        DrawMainHeader();
        DrawSummary();

        // Core gameplay composition comes first.
        DrawCore();
        DrawPlayer();
        DrawAbilities();
        DrawCombo();
        DrawBackground();
        DrawCoins();
        DrawObstacles();
        DrawEnemies();
        DrawTraps();
        DrawPowerUps();

        // Design analysis and presentation come after gameplay is composed.
        DrawPacingTimeline();
        DrawMechanicProgression();
        DrawMissionBriefing();
        DrawMusic();
        DrawValidation();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMainHeader()
    {
        EditorGUILayout.Space(6);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };

        EditorGUILayout.LabelField(
            "VOID RUSH — LEVEL DESIGN",
            titleStyle,
            GUILayout.Height(28f)
        );

        EditorGUILayout.LabelField(
            "Complete level composition and behaviour settings",
             EditorStyles.centeredGreyMiniLabel
);

        EditorGUILayout.Space(4);
    }

    private void DrawViewModeToolbar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        viewMode = (EditorViewMode)GUILayout.Toolbar(
            (int)viewMode,
            new[] { "BASIC DESIGN", "ADVANCED / OVERRIDES" }
        );

        EditorGUILayout.Space(3);
        EditorGUILayout.HelpBox(
            IsAdvanced
                ? "Advanced mode exposes technical player settings and optional per-level danger overrides. Presets remain the recommended default."
                : "Basic mode contains the fields required for fast level design. Enemy and trap behaviour comes from the selected danger tiers.",
            MessageType.Info
        );

        EditorGUILayout.EndVertical();
    }

    private void DrawSummary()
    {
        FoldoutBox(
            "LEVEL SUMMARY",
            ref summaryExpanded,
            () =>
            {
                if (serializedObject.isEditingMultipleObjects)
                {
                    Help("Detailed summary is available when a single LevelConfig is selected.");
                    return;
                }

                LevelConfig config = target as LevelConfig;

                if (config == null)
                    return;

                SummaryRow("Level", $"{config.levelNumber} — {config.levelName}");
                SummaryRow("Objective", GetWinConditionSummary(config));
                SummaryRow("Mission Stars", $"{config.SafeMissionDifficulty}/5");

                float dangerAverage = config.GetActiveDangerAverage();
                SummaryRow(
                    "Danger Average",
                    dangerAverage > 0f
                        ? $"D{dangerAverage:0.0}"
                        : "No active threats"
                );

                SummaryRow("Enemies", GetEnemySummary(config));
                SummaryRow("Hazards", GetHazardSummary(config));
                SummaryRow("Boss", GetBossSummary(config));
                SummaryRow("New Mechanics", GetProgressionSummary(
                    config,
                    MechanicProgressionStatus.IntroducedHere
                ));
                SummaryRow("Final Challenges", GetProgressionSummary(
                    config,
                    MechanicProgressionStatus.FinalChallenge
                ));
                SummaryRow("Music", config.gameplayMusic != null
                    ? config.gameplayMusic.name
                    : "Not Assigned");
            }
        );
    }

    private void DrawGlobalWarnings()
    {
        if (serializedObject.isEditingMultipleObjects)
            return;

        LevelConfig config = target as LevelConfig;

        if (config == null)
            return;

        bool hasThreat =
            config.normalEnemyCount > 0 ||
            config.projectileEnemyCount > 0 ||
            config.hunterEnemyCount > 0 ||
            config.beaconEnemyCount > 0 ||
            config.bossEnabled ||
            config.verticalLaserEnabled ||
            config.horizontalLaserEnabled ||
            config.bombTrapEnabled;

        if (hasThreat && !config.HasDangerProfile)
        {
            EditorGUILayout.HelpBox(
                "Danger Balance Profile is not assigned. The game will remain functional by using the old hidden LevelConfig values, but danger tier selection will not change behaviour until a profile is assigned.",
                MessageType.Warning
            );
        }

        if (!hasThreat)
        {
            Warning("This level contains no enemies, boss or traps.");
        }

        if (config.UsesScore &&
            !config.normalCoinEnabled &&
            !config.goldCoinEnabled &&
            !config.rareCoinEnabled)
        {
            Warning("This objective requires score, but every coin type is disabled.");
        }

        if (config.UsesScore && config.maxCoinCount <= 0)
        {
            Warning("This objective requires score, but Max Coin Count is 0.");
        }

        float enabledChance = 0f;

        if (config.normalCoinEnabled)
            enabledChance += config.normalCoinChance;
        if (config.goldCoinEnabled)
            enabledChance += config.goldCoinChance;
        if (config.rareCoinEnabled)
            enabledChance += config.rareCoinChance;

        if (config.UsesScore &&
            enabledChance > 0f &&
            !Mathf.Approximately(enabledChance, 100f))
        {
            Help($"Enabled coin chances total {enabledChance:0.##}%. A total of 100% is recommended.");
        }

        if (config.beaconEnemyCount > 0 &&
            config.normalEnemyCount <= 0 &&
            config.projectileEnemyCount <= 0 &&
            config.hunterEnemyCount <= 0)
        {
            Help("Beacon is enabled without Normal, Projectile or Hunter enemies. Its buff will have very few useful targets.");
        }

        if (config.bossEnabled)
        {
            if (config.EffectiveBossSpawnCondition == BossSpawnCondition.Score &&
                config.bossSpawnScore >= config.winScore)
            {
                Warning("Boss Spawn Score should be lower than Win Score.");
            }

            if (config.EffectiveBossSpawnCondition == BossSpawnCondition.Time &&
                config.bossSpawnTime >= config.timeLimit)
            {
                Warning("Boss Spawn Time should be lower than the level Time Limit.");
            }
        }

        int extremeCount = CountExtremeThreats(config);

        if (extremeCount >= 3)
        {
            Warning(
                $"This level combines {extremeCount} D4/D5 threats. Test reaction windows carefully, especially when their spawn timings overlap."
            );
        }

        if (HasAnyCustomOverride(config))
        {
            Help("This level contains custom danger overrides. Future balance-profile changes will not affect those overridden threats.");
        }
    }

    private void DrawCore()
    {
        FoldoutBox(
            "LEVEL / WIN CONDITION",
            ref coreExpanded,
            () =>
            {
                Prop("levelNumber");
                Prop("levelName");
                Space();
                Prop("winCondition");

                switch (SelectedWinCondition)
                {
                    case WinConditionType.ReachScore:
                        Prop("winScore");
                        Help("Collect the target score. Timer HUD and survival countdown are disabled.");
                        break;

                    case WinConditionType.SurviveTime:
                        Prop("timeLimit");
                        Help("Survive until the countdown reaches zero. Coins, score and combo are disabled at runtime.");
                        break;

                    case WinConditionType.ReachScoreWithinTime:
                        Prop("winScore");
                        Prop("timeLimit");
                        Help("Reach the score before the countdown reaches zero.");
                        break;
                }
            }
        );
    }

    private void DrawMissionBriefing()
    {
        FoldoutBox(
            "MISSION PRESENTATION",
            ref briefingExpanded,
            () =>
            {
                Help(
                    "Build the gameplay first, then describe it here. The draft generator uses the objective, active mechanics and progression tags."
                );

                Prop("briefingTitle");
                Prop("missionDifficulty");
                Prop("briefingModeDescription");
                Prop("briefingObjectiveDescription");
                Prop("briefingPages", true);

                EditorGUILayout.Space(5);

                using (new EditorGUI.DisabledScope(
                    serializedObject.isEditingMultipleObjects))
                {
                    if (GUILayout.Button(
                        "GENERATE BRIEFING DRAFT",
                        GUILayout.Height(26f)))
                    {
                        GenerateBriefingDraftWithConfirmation();
                    }
                }

                LevelConfig config = GetSingleConfig();

                if (config == null)
                    return;

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    "BRIEFING PREVIEW",
                    EditorStyles.miniBoldLabel
                );

                EditorGUILayout.LabelField(
                    config.GetEffectiveModeDescription(),
                    EditorStyles.boldLabel
                );

                EditorGUILayout.LabelField(
                    config.GetEffectiveObjectiveDescription(),
                    EditorStyles.wordWrappedMiniLabel
                );

                int extraPageCount =
                    CountNonEmptyBriefingPages(config.briefingPages);

                EditorGUILayout.LabelField(
                    $"Total Pages: {1 + extraPageCount} " +
                    $"(1 objective + {extraPageCount} extra)",
                    EditorStyles.miniLabel
                );

                EditorGUILayout.EndVertical();
            }
        );
    }

    private void DrawMusic()
    {
        FoldoutBox(
            "GAMEPLAY MUSIC",
            ref musicExpanded,
            () => Prop("gameplayMusic")
        );
    }

    private void DrawPlayer()
    {
        FoldoutBox(
            "PLAYER",
            ref playerExpanded,
            () => Prop("playerMoveSpeed")
        );
    }

    private void DrawAbilities()
    {
        FoldoutBox(
            "PLAYER ABILITIES",
            ref abilitiesExpanded,
            () =>
            {
                Prop("dashEnabled");

                if (BoolValue("dashEnabled") && IsAdvanced)
                {
                    Prop("dashDistance");
                    Prop("dashDuration");
                    Prop("dashCooldown");
                }

                Space();
                Prop("cloneEnabled");

                if (BoolValue("cloneEnabled") && IsAdvanced)
                {
                    Prop("cloneDuration");
                    Prop("cloneCooldown");
                }
            }
        );
    }

    private void DrawCombo()
    {
        FoldoutBox(
            "COMBO / HUD",
            ref comboExpanded,
            () =>
            {
                if (!UsesScore)
                {
                    Help("Combo is automatically disabled for Survive Time missions.");
                    return;
                }

                Prop("comboEnabled");

                if (!BoolValue("comboEnabled"))
                    return;

                Prop("comboTimeLimit");

                if (IsAdvanced)
                {
                    Space();
                    MiniTitle("COMBO SPEED SETTINGS");
                    DrawComboSpeedStages();
                }
            }
        );
    }


    private void DrawComboSpeedStages()
    {
        SerializedProperty stages =
            serializedObject.FindProperty("comboSpeedStages");

        if (stages == null || !stages.isArray)
        {
            EditorGUILayout.HelpBox(
                "Missing serialized array: comboSpeedStages",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            "CUSTOM COMBO SPEED STAGES",
            EditorStyles.miniBoldLabel
        );

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(
            stages.arraySize == 1
                ? "1 stage"
                : $"{stages.arraySize} stages",
            EditorStyles.miniLabel,
            GUILayout.Width(58f)
        );
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "Each stage defines which combo multiplier appears, how many chained coins unlock it, and how fast the player moves while it is active.",
            EditorStyles.wordWrappedMiniLabel
        );

        EditorGUILayout.Space(4);

        for (int i = 0; i < stages.arraySize; i++)
        {
            SerializedProperty stage = stages.GetArrayElementAtIndex(i);
            SerializedProperty comboMultiplier =
                stage.FindPropertyRelative("comboMultiplier");
            SerializedProperty coinsRequired =
                stage.FindPropertyRelative("coinsRequired");
            SerializedProperty speedMultiplier =
                stage.FindPropertyRelative("playerSpeedMultiplier");

            int comboValue = comboMultiplier != null
                ? comboMultiplier.intValue
                : i + 2;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"STAGE {i + 1}  •  COMBO ×{comboValue}",
                EditorStyles.boldLabel
            );

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(i == 0))
            {
                if (GUILayout.Button("▲", GUILayout.Width(26f)))
                    stages.MoveArrayElement(i, i - 1);
            }

            using (new EditorGUI.DisabledScope(i >= stages.arraySize - 1))
            {
                if (GUILayout.Button("▼", GUILayout.Width(26f)))
                    stages.MoveArrayElement(i, i + 1);
            }

            if (GUILayout.Button("REMOVE", GUILayout.Width(68f)))
            {
                stages.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            EditorGUILayout.EndHorizontal();

            if (comboMultiplier != null)
            {
                EditorGUILayout.PropertyField(
                    comboMultiplier,
                    new GUIContent(
                        "Displayed Combo",
                        "The combo multiplier shown for this stage, such as ×2 or ×3."
                    )
                );
            }

            if (coinsRequired != null)
            {
                EditorGUILayout.PropertyField(
                    coinsRequired,
                    new GUIContent(
                        "Coins Needed",
                        "Number of coins that must be collected within the combo window to unlock this stage."
                    )
                );
            }

            if (speedMultiplier != null)
            {
                EditorGUILayout.PropertyField(
                    speedMultiplier,
                    new GUIContent(
                        "Player Speed Multiplier",
                        "Movement speed multiplier applied while this combo stage is active. 1.25 means 25% faster."
                    )
                );
            }

            if (coinsRequired != null &&
                speedMultiplier != null)
            {
                float percentage =
                    Mathf.Max(0f, speedMultiplier.floatValue - 1f) * 100f;

                EditorGUILayout.LabelField(
                    $"Unlocks after {coinsRequired.intValue} chained coins • Player moves {percentage:0.#}% faster",
                    EditorStyles.wordWrappedMiniLabel
                );
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        if (GUILayout.Button("+ ADD COMBO SPEED STAGE"))
        {
            int index = stages.arraySize;
            stages.InsertArrayElementAtIndex(index);

            SerializedProperty newStage =
                stages.GetArrayElementAtIndex(index);

            SerializedProperty comboMultiplier =
                newStage.FindPropertyRelative("comboMultiplier");
            SerializedProperty coinsRequired =
                newStage.FindPropertyRelative("coinsRequired");
            SerializedProperty speedMultiplier =
                newStage.FindPropertyRelative("playerSpeedMultiplier");

            if (comboMultiplier != null)
                comboMultiplier.intValue = index + 2;

            if (coinsRequired != null)
            {
                coinsRequired.intValue =
                    index < DefaultComboCoins.Length
                        ? DefaultComboCoins[index]
                        : DefaultComboCoins[DefaultComboCoins.Length - 1] +
                          (index - DefaultComboCoins.Length + 1) * 4;
            }

            if (speedMultiplier != null)
            {
                speedMultiplier.floatValue =
                    index < DefaultComboSpeedMultipliers.Length
                        ? DefaultComboSpeedMultipliers[index]
                        : DefaultComboSpeedMultipliers[
                            DefaultComboSpeedMultipliers.Length - 1
                          ];
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBackground()
    {
        FoldoutBox(
            "BACKGROUND / STARFIELD",
            ref backgroundExpanded,
            () =>
            {
                Prop("randomizeNearStarsColor");

                if (!BoolValue("randomizeNearStarsColor"))
                    Prop("nearStarsColor");

                if (IsAdvanced)
                {
                    Prop("nearStarsSpeedMultiplier");
                    Prop("nearStarsSizeMultiplier");
                    Prop("starfieldDensityMultiplier");
                }
            }
        );
    }

    private void DrawCoins()
    {
        FoldoutBox(
            "COINS",
            ref coinsExpanded,
            () =>
            {
                if (!UsesScore)
                {
                    Help("Coins are automatically disabled for this win condition.");
                    return;
                }

                Prop("coinSpawnInterval");
                Prop("maxCoinCount");
                Space();
                DrawCoin("Normal Coin", "normalCoinEnabled", "normalCoinChance", "normalCoinValue");
                DrawCoin("Gold Coin", "goldCoinEnabled", "goldCoinChance", "goldCoinValue");
                DrawCoin("Rare Coin", "rareCoinEnabled", "rareCoinChance", "rareCoinValue");
            }
        );
    }

    private void DrawObstacles()
    {
        FoldoutBox(
            "STATIC OBSTACLES",
            ref obstaclesExpanded,
            () =>
            {
                Prop("obstacleSpawnMode");
                Space();

                if (EnumValue("obstacleSpawnMode") ==
                    (int)ObstacleSpawnMode.Random)
                {
                    Prop("randomObstacleCount");

                    Space();

                    EditorGUILayout.LabelField(
                        "RANDOM OBSTACLE POOL",
                        EditorStyles.boldLabel
                    );

                    Prop("levelObstacles", true);

                    Help(
                        "Random mode randomly selects unique prefabs from this pool. " +
                        "Only entries with Enabled turned on and a valid prefab assigned can spawn."
                    );
                }
                else
                {
                    EditorGUILayout.LabelField(
                        "FIXED OBSTACLES",
                        EditorStyles.boldLabel
                    );

                    Prop("levelObstacles", true);

                    Help(
                        "Every enabled prefab in this list spawns once."
                    );
                }
            }
        );
    }

    private void DrawDangerBalance()
    {
        FoldoutBox(
            "DANGER BALANCE PROFILE",
            ref balanceExpanded,
            () =>
            {
                Prop("dangerBalanceProfile");

                SerializedProperty profileProperty =
                    serializedObject.FindProperty("dangerBalanceProfile");

                DangerBalanceProfile profile =
                    profileProperty != null
                        ? profileProperty.objectReferenceValue as DangerBalanceProfile
                        : null;

                EditorGUILayout.BeginHorizontal();

                using (new EditorGUI.DisabledScope(profile == null))
                {
                    if (GUILayout.Button("SELECT PROFILE"))
                    {
                        Selection.activeObject = profile;
                        EditorGUIUtility.PingObject(profile);
                    }
                }

                if (GUILayout.Button("CREATE BALANCED PROFILE"))
                    CreateBalancedProfile(profileProperty);

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);
                Help(
                    "Counts, enemy spawn intervals and boss trigger timing stay in this LevelConfig. Movement, attacks, reaction windows, buffs and trap intensity come from the selected D1–D5 tiers."
                );
            }
        );
    }

    private void DrawEnemies()
    {
        FoldoutBox(
            "ENEMIES",
            ref enemiesExpanded,
            () =>
            {
                DrawNormalEnemy();
                DrawProjectileEnemy();
                DrawHunterEnemy();
                DrawBoss();
                DrawBeacon();
            }
        );
    }

    private void DrawNormalEnemy()
    {
        NestedFoldout(
            "NORMAL ENEMY",
            ref normalEnemyExpanded,
            () =>
            {
                Prop("normalEnemyCount");

                if (IntValue("normalEnemyCount") <= 0)
                    return;

                Prop("normalEnemySpawnInterval");
                DrawDangerSelection(
                    "normalEnemyDanger",
                    "normalEnemyCustomOverride",
                    "normalEnemyOverride"
                );

                DrawNormalPreview();
            }
        );
    }

    private void DrawProjectileEnemy()
    {
        NestedFoldout(
            "PROJECTILE ENEMY",
            ref projectileEnemyExpanded,
            () =>
            {
                Prop("projectileEnemyCount");

                if (IntValue("projectileEnemyCount") <= 0)
                    return;

                Prop("projectileEnemySpawnInterval");
                DrawDangerSelection(
                    "projectileEnemyDanger",
                    "projectileEnemyCustomOverride",
                    "projectileEnemyOverride"
                );

                DrawProjectilePreview();
            }
        );
    }

    private void DrawHunterEnemy()
    {
        NestedFoldout(
            "HUNTER ENEMY",
            ref hunterEnemyExpanded,
            () =>
            {
                Prop("hunterEnemyCount");

                if (IntValue("hunterEnemyCount") <= 0)
                    return;

                Prop("hunterEnemySpawnInterval");
                DrawDangerSelection(
                    "hunterEnemyDanger",
                    "hunterEnemyCustomOverride",
                    "hunterEnemyOverride"
                );

                DrawHunterPreview();
            }
        );
    }

    private void DrawBoss()
    {
        NestedFoldout(
            "BOSS",
            ref bossExpanded,
            () =>
            {
                Prop("bossEnabled");

                if (!BoolValue("bossEnabled"))
                    return;

                switch (SelectedWinCondition)
                {
                    case WinConditionType.ReachScore:
                        ForceBossCondition(BossSpawnCondition.Score);
                        Prop("bossSpawnScore");
                        break;

                    case WinConditionType.SurviveTime:
                        ForceBossCondition(BossSpawnCondition.Time);
                        Prop("bossSpawnTime");
                        break;

                    case WinConditionType.ReachScoreWithinTime:
                        Prop("bossSpawnCondition");

                        if ((BossSpawnCondition)EnumValue("bossSpawnCondition") ==
                            BossSpawnCondition.Score)
                        {
                            Prop("bossSpawnScore");
                        }
                        else
                        {
                            Prop("bossSpawnTime");
                        }
                        break;
                }

                DrawDangerSelection(
                    "bossDanger",
                    "bossCustomOverride",
                    "bossOverride"
                );

                DrawBossPreview();
            }
        );
    }

    private void DrawBeacon()
    {
        NestedFoldout(
            "BEACON ENEMY",
            ref beaconExpanded,
            () =>
            {
                Prop("beaconEnemyCount");

                if (IntValue("beaconEnemyCount") <= 0)
                    return;

                Prop("beaconMinSpawnTime");
                Prop("beaconMaxSpawnTime");
                ValidateMinMax("beaconMinSpawnTime", "beaconMaxSpawnTime", "Beacon Spawn Time");

                DrawDangerSelection(
                    "beaconEnemyDanger",
                    "beaconEnemyCustomOverride",
                    "beaconEnemyOverride"
                );

                DrawBeaconPreview();
            }
        );
    }

    private void DrawPowerUps()
    {
        FoldoutBox(
            "POWER UPS",
            ref powerUpsExpanded,
            () =>
            {
                Prop("armorEnabled");

                if (BoolValue("armorEnabled"))
                {
                    Prop("armorMinSpawnTime");
                    Prop("armorMaxSpawnTime");
                    ValidateMinMax("armorMinSpawnTime", "armorMaxSpawnTime", "Armor Spawn Time");

                    if (IsAdvanced)
                        Prop("armorImmuneDuration");
                }

                Space();
                Prop("slowEnabled");

                if (BoolValue("slowEnabled"))
                {
                    Prop("slowMinSpawnTime");
                    Prop("slowMaxSpawnTime");
                    ValidateMinMax("slowMinSpawnTime", "slowMaxSpawnTime", "Slow Spawn Time");

                    if (IsAdvanced)
                    {
                        Prop("slowMultiplier");
                        Prop("slowDuration");
                    }
                }
            }
        );
    }

    private void DrawTraps()
    {
        FoldoutBox(
            "TRAPS / LASERS",
            ref trapsExpanded,
            () =>
            {
                DrawTrap(
                    "VERTICAL LASER",
                    "verticalLaserEnabled",
                    "verticalLaserDanger",
                    "verticalLaserCustomOverride",
                    "verticalLaserOverride",
                    DrawVerticalLaserPreview
                );

                Space();

                DrawTrap(
                    "HORIZONTAL LASER",
                    "horizontalLaserEnabled",
                    "horizontalLaserDanger",
                    "horizontalLaserCustomOverride",
                    "horizontalLaserOverride",
                    DrawHorizontalLaserPreview
                );

                Space();

                DrawTrap(
                    "SPACE BOMB",
                    "bombTrapEnabled",
                    "bombDanger",
                    "bombCustomOverride",
                    "bombOverride",
                    DrawBombPreview
                );
            }
        );
    }

    private void DrawTrap(
        string title,
        string enabledProperty,
        string dangerProperty,
        string overrideToggleProperty,
        string overrideProperty,
        Action preview)
    {
        MiniTitle(title);
        Prop(enabledProperty);

        if (!BoolValue(enabledProperty))
            return;

        DrawDangerSelection(
            dangerProperty,
            overrideToggleProperty,
            overrideProperty
        );

        preview?.Invoke();
    }

    private void DrawDangerSelection(
        string dangerProperty,
        string overrideToggleProperty,
        string overrideProperty)
    {
        Prop(dangerProperty);

        DangerLevel level = DangerValue(dangerProperty);

        EditorGUILayout.HelpBox(
            $"{DangerLevelUtility.GetDisplayName(level)}\n" +
            DangerLevelUtility.GetDescription(level),
            MessageType.None
        );

        bool overrideEnabled = BoolValue(overrideToggleProperty);

        if (IsAdvanced)
        {
            Prop(overrideToggleProperty);

            if (BoolValue(overrideToggleProperty))
            {
                EditorGUILayout.HelpBox(
                    "Custom override disconnects this threat from the shared profile for this level only.",
                    MessageType.Warning
                );

                Prop(overrideProperty, true);
            }
        }
        else if (overrideEnabled)
        {
            Warning("CUSTOM OVERRIDE is active. Switch to Advanced mode to edit it.");
        }
    }

    private void DrawNormalPreview()
    {
        LevelConfig config = GetSingleConfig();
        if (config == null) return;

        NormalEnemyDangerSettings settings = config.ResolveNormalEnemyDanger();

        DrawResolvedBox(
            "RESOLVED NORMAL ENEMY",
            new[]
            {
                $"Start Speed: {settings.minStartSpeed:0.##} – {settings.maxStartSpeed:0.##}",
                $"Max Speed: {settings.maxSpeed:0.##}",
                $"Acceleration: {settings.speedIncreaseRate:0.###}/s",
                $"Prediction: {(settings.predictionEnabled ? "ON" : "OFF")}",
                $"Separation: {(settings.separationEnabled ? "ON" : "OFF")}"
            }
        );
    }

    private void DrawProjectilePreview()
    {
        LevelConfig config = GetSingleConfig();
        if (config == null) return;

        ProjectileEnemyDangerSettings settings = config.ResolveProjectileEnemyDanger();

        DrawResolvedBox(
            "RESOLVED PROJECTILE ENEMY",
            new[]
            {
                $"Move Speed: {settings.moveSpeed:0.##}",
                $"Fire Interval: {settings.fireRate:0.##}s",
                $"Projectile Speed: {settings.projectileSpeed:0.##}",
                $"Burst: {settings.shotsPerBurst} shots",
                $"Reload: {settings.reloadDuration:0.##}s",
                $"Reload Retreat: {settings.reloadRetreatDistance:0.##}",
                $"Combat Range: {settings.retreatDistance:0.##} – {settings.stoppingDistance:0.##}",
                $"Predictive Aim: {(settings.predictiveAimEnabled ? "ON" : "OFF")}"
            }
        );
    }

    private void DrawHunterPreview()
    {
        LevelConfig config = GetSingleConfig();
        if (config == null) return;

        HunterEnemyDangerSettings settings = config.ResolveHunterEnemyDanger();

        DrawResolvedBox(
            "RESOLVED HUNTER",
            new[]
            {
                $"Reposition: {settings.repositionTime:0.##}s",
                $"Warning: {settings.warningDuration:0.##}s",
                $"Charge Speed: {settings.chargeSpeed:0.##}",
                $"Max Charge: {settings.maxChargeTime:0.##}s",
                $"Stun: {settings.stunDuration:0.##}s"
            }
        );
    }

    private void DrawBossPreview()
    {
        LevelConfig config = GetSingleConfig();
        if (config == null) return;

        BossDangerSettings settings = config.ResolveBossDanger();

        DrawResolvedBox(
            "RESOLVED BOSS",
            new[]
            {
                $"Move Speed: {settings.speed:0.##}",
                $"Direction Smoothness: {settings.directionSmoothness:0.##}",
                $"Can Split: {(settings.canSplit ? "YES" : "NO")}",
                $"Split Delay: {settings.splitDelay:0.##}s",
                $"Mini Boss Speed: {settings.miniBossSpeed:0.##}"
            }
        );
    }

    private void DrawBeaconPreview()
    {
        LevelConfig config = GetSingleConfig();
        if (config == null) return;

        BeaconEnemyDangerSettings settings = config.ResolveBeaconEnemyDanger();

        DrawResolvedBox(
            "RESOLVED BEACON",
            new[]
            {
                $"Activation Delay: {settings.activationDelay:0.##}s",
                $"Buff Duration: {settings.buffDuration:0.##}s",
                $"Normal Speed Buff: ×{settings.normalSpeedMultiplier:0.##}",
                $"Projectile Fire Buff: ×{settings.projectileFireMultiplier:0.##}",
                $"Hunter Warning Multiplier: ×{settings.hunterWarningMultiplier:0.##}"
            }
        );
    }

    private void DrawVerticalLaserPreview()
    {
        LevelConfig config = GetSingleConfig();
        if (config == null) return;
        DrawLaserPreview("RESOLVED VERTICAL LASER", config.ResolveVerticalLaserDanger());
    }

    private void DrawHorizontalLaserPreview()
    {
        LevelConfig config = GetSingleConfig();
        if (config == null) return;
        DrawLaserPreview("RESOLVED HORIZONTAL LASER", config.ResolveHorizontalLaserDanger());
    }

    private void DrawLaserPreview(string title, LaserDangerSettings settings)
    {
        DrawResolvedBox(
            title,
            new[]
            {
                $"Spawn Window: {settings.minSpawnTime:0.##} – {settings.maxSpawnTime:0.##}s",
                $"Warning: {settings.warningDuration:0.##}s",
                $"Lifetime: {settings.lifeTime:0.##}s",
                $"Width: {settings.width:0.##}",
                $"Size Extra: {settings.sizeExtra:0.##}"
            }
        );
    }

    private void DrawBombPreview()
    {
        LevelConfig config = GetSingleConfig();
        if (config == null) return;

        BombDangerSettings settings = config.ResolveBombDanger();

        DrawResolvedBox(
            "RESOLVED SPACE BOMB",
            new[]
            {
                $"Spawn Window: {settings.minSpawnTime:0.##} – {settings.maxSpawnTime:0.##}s",
                $"Maximum Active Bombs: {settings.maxBombCount}",
                $"Spawn Safety: {settings.spawnSafeTime:0.##}s"
            }
        );
    }

    private void DrawResolvedBox(string title, IEnumerable<string> rows)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);

        foreach (string row in rows)
            EditorGUILayout.LabelField(row, EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();
    }

    private void DrawCoin(
        string title,
        string enabledProperty,
        string chanceProperty,
        string valueProperty)
    {
        MiniTitle(title);
        Prop(enabledProperty);

        if (!BoolValue(enabledProperty))
            return;

        if (IsAdvanced)
        {
            Prop(chanceProperty);
            Prop(valueProperty);
        }
    }


    private void DrawPacingTimeline()
    {
        FoldoutBox(
            "PACING TIMELINE",
            ref timelineExpanded,
            () =>
            {
                LevelConfig config = GetSingleConfig();

                if (config == null)
                {
                    Help("The pacing timeline is available when a single LevelConfig is selected.");
                    return;
                }

                bool estimated;
                float duration = GetTimelineDuration(config, out estimated);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    estimated
                        ? $"Estimated Mission Length: {duration:0.#}s"
                        : $"Mission Length: {duration:0.#}s",
                    EditorStyles.boldLabel
                );

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField(
                    estimated ? "ESTIMATED" : "EXACT",
                    EditorStyles.miniBoldLabel,
                    GUILayout.Width(72f)
                );
                EditorGUILayout.EndHorizontal();

                if (estimated)
                {
                    Help(
                        "Reach Score has no fixed duration. This estimate assumes the player collects spawned coins consistently and is used only for pacing analysis."
                    );
                }

                List<TimelineRow> rows =
                    BuildTimelineRows(config, duration);

                if (rows.Count == 0)
                {
                    Help("No timed spawns or mission events are active.");
                    return;
                }

                DrawTimelineAxis(duration);

                foreach (TimelineRow row in rows)
                    DrawTimelineRow(row, duration);

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField(
                    "Bars = first random window    Markers = repeated/expected spawns through mission end",
                    EditorStyles.centeredGreyMiniLabel
                );
            }
        );
    }

    private static float GetTimelineDuration(
        LevelConfig config,
        out bool estimated)
    {
        if (config.UsesTime)
        {
            estimated = false;
            return Mathf.Max(1f, config.SafeTimeLimit);
        }

        estimated = true;

        float totalChance = 0f;
        float weightedValue = 0f;

        if (config.normalCoinEnabled)
        {
            totalChance += config.normalCoinChance;
            weightedValue +=
                config.normalCoinChance *
                Mathf.Max(1, config.normalCoinValue);
        }

        if (config.goldCoinEnabled)
        {
            totalChance += config.goldCoinChance;
            weightedValue +=
                config.goldCoinChance *
                Mathf.Max(1, config.goldCoinValue);
        }

        if (config.rareCoinEnabled)
        {
            totalChance += config.rareCoinChance;
            weightedValue +=
                config.rareCoinChance *
                Mathf.Max(1, config.rareCoinValue);
        }

        float expectedCoinValue =
            totalChance > 0f
                ? weightedValue / totalChance
                : 1f;

        float requiredCoins =
            config.SafeWinScore /
            Mathf.Max(0.01f, expectedCoinValue);

        float estimatedDuration =
            requiredCoins *
            Mathf.Max(0.01f, config.coinSpawnInterval);

        return Mathf.Clamp(estimatedDuration, 8f, 180f);
    }

    private static List<TimelineRow> BuildTimelineRows(
        LevelConfig config,
        float duration)
    {
        List<TimelineRow> rows = new List<TimelineRow>();

        Color coinColor = GetTimelineColor(
            new Color(0.35f, 0.72f, 0.95f, 1f),
            new Color(0.10f, 0.45f, 0.70f, 1f)
        );

        Color enemyColor = GetTimelineColor(
            new Color(0.95f, 0.42f, 0.42f, 1f),
            new Color(0.72f, 0.15f, 0.15f, 1f)
        );

        Color bossColor = GetTimelineColor(
            new Color(0.95f, 0.35f, 0.82f, 1f),
            new Color(0.65f, 0.10f, 0.52f, 1f)
        );

        Color supportColor = GetTimelineColor(
            new Color(0.35f, 0.88f, 0.62f, 1f),
            new Color(0.10f, 0.58f, 0.32f, 1f)
        );

        Color hazardColor = GetTimelineColor(
            new Color(1f, 0.68f, 0.28f, 1f),
            new Color(0.82f, 0.38f, 0.05f, 1f)
        );

        if (config.UsesScore &&
            (config.normalCoinEnabled ||
             config.goldCoinEnabled ||
             config.rareCoinEnabled))
        {
            TimelineRow coins = new TimelineRow
            {
                label = "Coins",
                tooltip =
                    $"A coin spawn attempt occurs every {config.coinSpawnInterval:0.##} seconds.",
                color = coinColor
            };

            AddRepeatedMarkers(
                coins.markers,
                Mathf.Max(0.01f, config.coinSpawnInterval),
                Mathf.Min(
                    24,
                    Mathf.CeilToInt(
                        duration /
                        Mathf.Max(0.01f, config.coinSpawnInterval)
                    )
                )
            );

            rows.Add(coins);
        }

        AddEnemyTimelineRow(
            rows,
            "Normal Enemy",
            config.normalEnemyCount,
            config.normalEnemySpawnInterval,
            enemyColor
        );

        AddEnemyTimelineRow(
            rows,
            "Projectile Enemy",
            config.projectileEnemyCount,
            config.projectileEnemySpawnInterval,
            enemyColor
        );

        AddEnemyTimelineRow(
            rows,
            "Hunter Enemy",
            config.hunterEnemyCount,
            config.hunterEnemySpawnInterval,
            enemyColor
        );

        if (config.beaconEnemyCount > 0)
        {
            rows.Add(
                CreateWindowRow(
                    "Beacon Enemy",
                    config.beaconMinSpawnTime,
                    config.beaconMaxSpawnTime,
                    enemyColor,
                    $"First beacon spawn window. Maximum count: {config.beaconEnemyCount}."
                )
            );
        }

        if (config.bossEnabled)
        {
            float bossTime;
            string tooltip;

            if (config.EffectiveBossSpawnCondition ==
                BossSpawnCondition.Time)
            {
                bossTime = config.bossSpawnTime;
                tooltip =
                    $"Boss triggers after {config.bossSpawnTime:0.##} seconds.";
            }
            else
            {
                float progress =
                    config.SafeBossSpawnScore /
                    (float)Mathf.Max(1, config.SafeWinScore);

                bossTime = duration * Mathf.Clamp01(progress);
                tooltip =
                    $"Boss triggers at {config.SafeBossSpawnScore} score. " +
                    "Its timeline position is estimated from score progress.";
            }

            TimelineRow boss = new TimelineRow
            {
                label = "Boss",
                tooltip = tooltip,
                color = bossColor
            };

            boss.markers.Add(bossTime);
            rows.Add(boss);
        }

        if (config.armorEnabled)
        {
            rows.Add(
                CreateWindowRow(
                    "Armor",
                    config.armorMinSpawnTime,
                    config.armorMaxSpawnTime,
                    supportColor,
                    "Possible armor spawn window."
                )
            );
        }

        if (config.slowEnabled)
        {
            rows.Add(
                CreateWindowRow(
                    "Slow",
                    config.slowMinSpawnTime,
                    config.slowMaxSpawnTime,
                    supportColor,
                    "Possible slow power-up spawn window."
                )
            );
        }

        if (config.verticalLaserEnabled)
        {
            LaserDangerSettings settings =
                config.ResolveVerticalLaserDanger();

            rows.Add(
                CreateRepeatingRandomSpawnRow(
                    "Vertical Laser",
                    settings.minSpawnTime,
                    settings.maxSpawnTime,
                    duration,
                    settings.warningDuration,
                    hazardColor,
                    $"First spawn occurs between {settings.minSpawnTime:0.##}-{settings.maxSpawnTime:0.##}s. " +
                    $"It then repeats with the same random delay until the mission ends. " +
                    $"Warning: {settings.warningDuration:0.##}s."
                )
            );
        }

        if (config.horizontalLaserEnabled)
        {
            LaserDangerSettings settings =
                config.ResolveHorizontalLaserDanger();

            rows.Add(
                CreateRepeatingRandomSpawnRow(
                    "Horizontal Laser",
                    settings.minSpawnTime,
                    settings.maxSpawnTime,
                    duration,
                    settings.warningDuration,
                    hazardColor,
                    $"First spawn occurs between {settings.minSpawnTime:0.##}-{settings.maxSpawnTime:0.##}s. " +
                    $"It then repeats with the same random delay until the mission ends. " +
                    $"Warning: {settings.warningDuration:0.##}s."
                )
            );
        }

        if (config.bombTrapEnabled)
        {
            BombDangerSettings settings =
                config.ResolveBombDanger();

            rows.Add(
                CreateRepeatingRandomSpawnRow(
                    "Space Bomb",
                    settings.minSpawnTime,
                    settings.maxSpawnTime,
                    duration,
                    0f,
                    hazardColor,
                    $"First spawn occurs between {settings.minSpawnTime:0.##}-{settings.maxSpawnTime:0.##}s. " +
                    $"It keeps attempting spawns until the mission ends while below the active-bomb limit. " +
                    $"Maximum active bombs: {settings.maxBombCount}."
                )
            );
        }

        return rows;
    }

    private static void AddEnemyTimelineRow(
        List<TimelineRow> rows,
        string label,
        int count,
        float interval,
        Color color)
    {
        if (count <= 0)
            return;

        TimelineRow row = new TimelineRow
        {
            label = label,
            tooltip =
                $"{count} total spawn(s), one every {interval:0.##} seconds.",
            color = color
        };

        AddRepeatedMarkers(
            row.markers,
            Mathf.Max(0.01f, interval),
            Mathf.Min(count, 24)
        );

        rows.Add(row);
    }

    private static TimelineRow CreateWindowRow(
        string label,
        float start,
        float end,
        Color color,
        string tooltip)
    {
        return new TimelineRow
        {
            label = label,
            tooltip = tooltip,
            color = color,
            hasWindow = true,
            windowStart = Mathf.Max(0f, start),
            windowEnd = Mathf.Max(start, end)
        };
    }

    private static TimelineRow CreateRepeatingRandomSpawnRow(
        string label,
        float minimumDelay,
        float maximumDelay,
        float duration,
        float cycleExtraTime,
        Color color,
        string tooltip)
    {
        float safeMinimum = Mathf.Max(0f, minimumDelay);
        float safeMaximum = Mathf.Max(safeMinimum, maximumDelay);

        TimelineRow row = CreateWindowRow(
            label,
            safeMinimum,
            safeMaximum,
            color,
            tooltip
        );

        float averageDelay =
            Mathf.Max(0.01f, (safeMinimum + safeMaximum) * 0.5f);

        float repeatInterval =
            averageDelay + Mathf.Max(0f, cycleExtraTime);

        float expectedSpawnTime = averageDelay;
        int markerCount = 0;

        while (expectedSpawnTime <= duration && markerCount < 24)
        {
            row.markers.Add(expectedSpawnTime);
            expectedSpawnTime += repeatInterval;
            markerCount++;
        }

        return row;
    }

    private static void AddRepeatedMarkers(
        List<float> markers,
        float interval,
        int count)
    {
        float safeInterval = Mathf.Max(0.01f, interval);

        for (int i = 1; i <= count; i++)
            markers.Add(safeInterval * i);
    }

    private static void DrawTimelineAxis(float duration)
    {
        Rect rect = GUILayoutUtility.GetRect(
            0f,
            28f,
            GUILayout.ExpandWidth(true)
        );

        const float labelWidth = 132f;
        Rect track = new Rect(
            rect.x + labelWidth,
            rect.y + 3f,
            Mathf.Max(20f, rect.width - labelWidth - 4f),
            rect.height - 6f
        );

        DrawTimelineBackground(track);

        GUIStyle labelStyle = new GUIStyle(
            EditorStyles.centeredGreyMiniLabel
        );

        for (int i = 0; i <= 4; i++)
        {
            float normalized = i / 4f;
            float x = track.x + track.width * normalized;

            EditorGUI.DrawRect(
                new Rect(x, track.y, 1f, track.height),
                GetTimelineGridColor()
            );

            Rect labelRect = new Rect(
                x - 25f,
                track.y,
                50f,
                track.height
            );

            GUI.Label(
                labelRect,
                $"{duration * normalized:0.#}s",
                labelStyle
            );
        }
    }

    private static void DrawTimelineRow(
        TimelineRow row,
        float duration)
    {
        Rect rect = GUILayoutUtility.GetRect(
            0f,
            24f,
            GUILayout.ExpandWidth(true)
        );

        const float labelWidth = 132f;

        Rect labelRect = new Rect(
            rect.x,
            rect.y + 3f,
            labelWidth - 6f,
            rect.height - 6f
        );

        Rect track = new Rect(
            rect.x + labelWidth,
            rect.y + 4f,
            Mathf.Max(20f, rect.width - labelWidth - 4f),
            rect.height - 8f
        );

        GUI.Label(
            labelRect,
            new GUIContent(row.label, row.tooltip),
            EditorStyles.miniLabel
        );

        DrawTimelineBackground(track);

        for (int i = 1; i < 4; i++)
        {
            float x =
                track.x +
                track.width *
                (i / 4f);

            EditorGUI.DrawRect(
                new Rect(x, track.y, 1f, track.height),
                GetTimelineGridColor()
            );
        }

        if (row.hasWindow)
        {
            float startNormalized =
                Mathf.Clamp01(
                    row.windowStart /
                    Mathf.Max(0.01f, duration)
                );

            float endNormalized =
                Mathf.Clamp01(
                    row.windowEnd /
                    Mathf.Max(0.01f, duration)
                );

            float x = track.x + track.width * startNormalized;
            float width = Mathf.Max(
                3f,
                track.width *
                Mathf.Max(0f, endNormalized - startNormalized)
            );

            EditorGUI.DrawRect(
                new Rect(
                    x,
                    track.y + 2f,
                    width,
                    track.height - 4f
                ),
                row.color
            );
        }

        foreach (float markerTime in row.markers)
        {
            float normalized =
                Mathf.Clamp01(
                    markerTime /
                    Mathf.Max(0.01f, duration)
                );

            float x =
                track.x +
                track.width *
                normalized;

            EditorGUI.DrawRect(
                new Rect(
                    x - 2f,
                    track.y + 1f,
                    4f,
                    track.height - 2f
                ),
                row.color
            );
        }

        GUI.Label(
            track,
            new GUIContent(string.Empty, row.tooltip)
        );
    }

    private static void DrawTimelineBackground(Rect rect)
    {
        EditorGUI.DrawRect(
            rect,
            EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.12f, 0.65f)
                : new Color(0.76f, 0.76f, 0.76f, 0.7f)
        );
    }

    private static Color GetTimelineGridColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.08f)
            : new Color(0f, 0f, 0f, 0.10f);
    }

    private static Color GetTimelineColor(
        Color darkSkinColor,
        Color lightSkinColor)
    {
        return EditorGUIUtility.isProSkin
            ? darkSkinColor
            : lightSkinColor;
    }

    private void DrawMechanicProgression()
    {
        FoldoutBox(
            "MECHANIC PROGRESSION",
            ref progressionExpanded,
            () =>
            {
                LevelConfig config = GetSingleConfig();

                if (config == null)
                {
                    Help("Mechanic progression is available when a single LevelConfig is selected.");
                    return;
                }

                Help(
                    "Mark only the mechanics the player meets for the first time or must master here. These tags are used by validation and the briefing draft generator."
                );

                string introduced = GetProgressionSummary(
                    config,
                    MechanicProgressionStatus.IntroducedHere
                );

                string finalChallenges = GetProgressionSummary(
                    config,
                    MechanicProgressionStatus.FinalChallenge
                );

                SummaryRow("Introduced", introduced);
                SummaryRow("Final Challenge", finalChallenges);

                EditorGUILayout.Space(4);

                showInactiveMechanics = EditorGUILayout.ToggleLeft(
                    "Show inactive mechanics",
                    showInactiveMechanics
                );

                string lastCategory = null;

                foreach (MechanicDescriptor descriptor in
                         MechanicDescriptors)
                {
                    bool active = descriptor.isActive(config);

                    if (!active && !showInactiveMechanics)
                        continue;

                    if (lastCategory != descriptor.category)
                    {
                        MiniTitle(descriptor.category);
                        lastCategory = descriptor.category;
                    }

                    DrawMechanicStatusRow(
                        descriptor,
                        active
                    );
                }
            }
        );
    }

    private void DrawMechanicStatusRow(
        MechanicDescriptor descriptor,
        bool active)
    {
        SerializedProperty property =
            serializedObject.FindProperty(
                descriptor.propertyPath
            );

        if (property == null)
        {
            EditorGUILayout.HelpBox(
                "Missing serialized property: " +
                descriptor.propertyPath,
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.BeginHorizontal();

        GUIContent label = new GUIContent(
            descriptor.label,
            descriptor.briefingText
        );

        EditorGUILayout.LabelField(
            label,
            GUILayout.Width(155f)
        );

        using (new EditorGUI.DisabledScope(!active))
        {
            property.enumValueIndex =
                EditorGUILayout.Popup(
                    property.enumValueIndex,
                    MechanicStatusLabels
                );
        }

        GUIStyle stateStyle =
            new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleRight
            };

        EditorGUILayout.LabelField(
            active ? "ACTIVE" : "OFF",
            stateStyle,
            GUILayout.Width(48f)
        );

        EditorGUILayout.EndHorizontal();

        if (!active &&
            property.enumValueIndex !=
            (int)MechanicProgressionStatus.AlreadyKnown)
        {
            Warning(
                $"{descriptor.label} is inactive but still has a progression tag."
            );
        }
    }

    private void DrawValidation()
    {
        FoldoutBox(
            "LEVEL VALIDATION",
            ref validationExpanded,
            () =>
            {
                LevelConfig config = GetSingleConfig();

                if (config == null)
                {
                    Help("Validation is available when a single LevelConfig is selected.");
                    return;
                }

                List<ValidationIssue> issues =
                    BuildValidationIssues(config);

                int errors = CountIssues(
                    issues,
                    ValidationSeverity.Error
                );

                int warnings = CountIssues(
                    issues,
                    ValidationSeverity.Warning
                );

                int suggestions = CountIssues(
                    issues,
                    ValidationSeverity.Suggestion
                );

                EditorGUILayout.BeginHorizontal();
                DrawValidationCount("ERRORS", errors);
                DrawValidationCount("WARNINGS", warnings);
                DrawValidationCount("SUGGESTIONS", suggestions);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                if (issues.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No level-design problems were detected.",
                        MessageType.Info
                    );
                    return;
                }

                DrawValidationGroup(
                    issues,
                    ValidationSeverity.Error,
                    "ERRORS"
                );

                DrawValidationGroup(
                    issues,
                    ValidationSeverity.Warning,
                    "WARNINGS"
                );

                DrawValidationGroup(
                    issues,
                    ValidationSeverity.Suggestion,
                    "DESIGN SUGGESTIONS"
                );

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField(
                    "Validation also checks duplicate mechanic introductions across all LevelConfig assets.",
                    EditorStyles.wordWrappedMiniLabel
                );
            }
        );
    }

    private static void DrawValidationCount(
        string label,
        int count)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            label,
            EditorStyles.centeredGreyMiniLabel
        );
        EditorGUILayout.LabelField(
            count.ToString(),
            new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            }
        );
        EditorGUILayout.EndVertical();
    }

    private void DrawValidationGroup(
        List<ValidationIssue> issues,
        ValidationSeverity severity,
        string title)
    {
        bool hasAny = false;

        foreach (ValidationIssue issue in issues)
        {
            if (issue.severity == severity)
            {
                hasAny = true;
                break;
            }
        }

        if (!hasAny)
            return;

        MiniTitle(title);

        foreach (ValidationIssue issue in issues)
        {
            if (issue.severity != severity)
                continue;

            MessageType messageType =
                severity == ValidationSeverity.Error
                    ? MessageType.Error
                    : severity == ValidationSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;

            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );

            EditorGUILayout.HelpBox(
                issue.message,
                messageType
            );

            if (issue.fix != null ||
                !string.IsNullOrWhiteSpace(
                    issue.propertyPath))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (!string.IsNullOrWhiteSpace(
                        issue.propertyPath) &&
                    GUILayout.Button(
                        focusedValidationPropertyPath ==
                        issue.propertyPath
                            ? "HIDE FIELD"
                            : "EDIT FIELD",
                        GUILayout.Width(100f)))
                {
                    focusedValidationPropertyPath =
                        focusedValidationPropertyPath ==
                        issue.propertyPath
                            ? null
                            : issue.propertyPath;
                }

                if (issue.fix != null &&
                    GUILayout.Button(
                        issue.fixLabel,
                        GUILayout.Width(110f)))
                {
                    ApplyValidationFix(issue);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrWhiteSpace(
                    issue.propertyPath) &&
                focusedValidationPropertyPath ==
                issue.propertyPath)
            {
                SerializedProperty focusedProperty =
                    serializedObject.FindProperty(
                        issue.propertyPath
                    );

                if (focusedProperty != null)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.PropertyField(
                        focusedProperty,
                        true
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "The linked field could not be found: " +
                        issue.propertyPath,
                        MessageType.Error
                    );
                }
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void ApplyValidationFix(
        ValidationIssue issue)
    {
        LevelConfig config = GetSingleConfig();

        if (config == null || issue.fix == null)
            return;

        serializedObject.ApplyModifiedProperties();

        Undo.RecordObject(
            config,
            "Fix Level Validation Issue"
        );

        issue.fix(config);

        EditorUtility.SetDirty(config);
        serializedObject.Update();
        GUI.changed = true;
    }

    private static int CountIssues(
        List<ValidationIssue> issues,
        ValidationSeverity severity)
    {
        int count = 0;

        foreach (ValidationIssue issue in issues)
        {
            if (issue.severity == severity)
                count++;
        }

        return count;
    }

    private List<ValidationIssue> BuildValidationIssues(
        LevelConfig config)
    {
        List<ValidationIssue> issues =
            new List<ValidationIssue>();

        bool hasThreat = HasAnyThreat(config);

        if (hasThreat && !config.HasDangerProfile)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Warning,
                    "Danger Balance Profile is not assigned. Danger tiers will use legacy hidden fallback values.",
                    null,
                    "AUTO FIX",
                    "dangerBalanceProfile"
                )
            );
        }

        if (!hasThreat)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Suggestion,
                    "This level contains no enemies, boss or traps. That is fine for a pure introduction, but confirm that the level still has meaningful pressure."
                )
            );
        }

        if (config.UsesScore &&
            !config.normalCoinEnabled &&
            !config.goldCoinEnabled &&
            !config.rareCoinEnabled)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    "This objective requires score, but every coin type is disabled.",
                    level =>
                    {
                        level.normalCoinEnabled = true;
                        level.normalCoinChance = 100f;
                    },
                    "AUTO FIX",
                    "normalCoinEnabled"
                )
            );
        }

        if (config.UsesScore &&
            config.maxCoinCount <= 0)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    "This objective requires score, but Max Coin Count is 0.",
                    level => level.maxCoinCount = 8,
                    "AUTO FIX",
                    "maxCoinCount"
                )
            );
        }

        float enabledChance =
            GetEnabledCoinChance(config);

        if (config.UsesScore &&
            enabledChance > 0f &&
            !Mathf.Approximately(enabledChance, 100f))
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Warning,
                    $"Enabled coin chances total {enabledChance:0.##}%. A total of 100% keeps the distribution predictable.",
                    NormalizeCoinChances,
                    "AUTO FIX",
                    "normalCoinChance"
                )
            );
        }

        if (config.beaconEnemyCount > 0 &&
            config.normalEnemyCount <= 0 &&
            config.projectileEnemyCount <= 0 &&
            config.hunterEnemyCount <= 0 &&
            !config.bossEnabled)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Warning,
                    "Beacon is enabled without another enemy type or boss. Its buff has no useful combat target.",
                    null,
                    "AUTO FIX",
                    "beaconEnemyCount"
                )
            );
        }

        ValidateBoss(config, issues);
        ValidateSpawnWindows(config, issues);

        int extremeCount =
            CountExtremeThreats(config);

        if (extremeCount >= 3)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Warning,
                    $"This level combines {extremeCount} D4/D5 threats. Test reaction windows carefully, especially where spawn timings overlap."
                )
            );
        }

        int openingPressure =
            CountOpeningPressure(config, 5f);

        if (openingPressure >= 3)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Warning,
                    $"{openingPressure} threat systems can become active during the first 5 seconds. The opening may not give the player enough time to read the arena."
                )
            );
        }

        float dangerAverage =
            config.GetActiveDangerAverage();

        if (dangerAverage >= 3.5f &&
            config.SafeMissionDifficulty <= 1)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Suggestion,
                    $"Estimated combat pressure is D{dangerAverage:0.0}, but the displayed mission difficulty is only {config.SafeMissionDifficulty}/5.",
                    null,
                    "AUTO FIX",
                    "missionDifficulty"
                )
            );
        }

        if (HasAnyCustomOverride(config))
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Suggestion,
                    "This level contains custom danger overrides. Future shared profile changes will not affect those overridden threats."
                )
            );
        }

        ValidateMechanicProgression(
            config,
            issues
        );

        if (!HasUsefulBriefingPages(config))
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Suggestion,
                    "The briefing has no useful extra page or still contains the default placeholder.",
                    PopulateBriefingDraft,
                    "GENERATE DRAFT",
                    "briefingPages"
                )
            );
        }

        ValidateObstaclePool(config, issues);

        return issues;
    }

    private static void ValidateBoss(
        LevelConfig config,
        List<ValidationIssue> issues)
    {
        if (!config.bossEnabled)
            return;

        if (config.EffectiveBossSpawnCondition ==
            BossSpawnCondition.Score)
        {
            if (config.bossSpawnScore >=
                config.SafeWinScore)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        "Boss Spawn Score must be lower than the mission Win Score or the boss may never appear.",
                        level =>
                        {
                            level.bossSpawnScore =
                                Mathf.Max(
                                    0,
                                    Mathf.CeilToInt(
                                        level.SafeWinScore *
                                        0.75f
                                    ) - 1
                                );
                        },
                        "AUTO FIX",
                        "bossSpawnScore"
                    )
                );
            }
        }
        else if (config.bossSpawnTime >=
                 config.SafeTimeLimit)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    "Boss Spawn Time must be lower than the mission Time Limit or the boss may never appear.",
                    level =>
                    {
                        level.bossSpawnTime =
                            Mathf.Max(
                                0f,
                                level.SafeTimeLimit *
                                0.75f
                            );
                    },
                    "AUTO FIX",
                    "bossSpawnTime"
                )
            );
        }
    }

    private static void ValidateSpawnWindows(
        LevelConfig config,
        List<ValidationIssue> issues)
    {
        ValidateWindow(
            issues,
            "Beacon Spawn Time",
            config.beaconEnemyCount > 0,
            config.beaconMinSpawnTime,
            config.beaconMaxSpawnTime,
            level =>
            {
                FitWindowToDuration(
                    ref level.beaconMinSpawnTime,
                    ref level.beaconMaxSpawnTime,
                    level.UsesTime
                        ? level.SafeTimeLimit
                        : Mathf.Max(
                            level.beaconMaxSpawnTime,
                            20f
                        )
                );
            }
        );

        ValidateWindow(
            issues,
            "Armor Spawn Time",
            config.armorEnabled,
            config.armorMinSpawnTime,
            config.armorMaxSpawnTime,
            level =>
            {
                FitWindowToDuration(
                    ref level.armorMinSpawnTime,
                    ref level.armorMaxSpawnTime,
                    level.UsesTime
                        ? level.SafeTimeLimit
                        : Mathf.Max(
                            level.armorMaxSpawnTime,
                            20f
                        )
                );
            }
        );

        ValidateWindow(
            issues,
            "Slow Spawn Time",
            config.slowEnabled,
            config.slowMinSpawnTime,
            config.slowMaxSpawnTime,
            level =>
            {
                FitWindowToDuration(
                    ref level.slowMinSpawnTime,
                    ref level.slowMaxSpawnTime,
                    level.UsesTime
                        ? level.SafeTimeLimit
                        : Mathf.Max(
                            level.slowMaxSpawnTime,
                            20f
                        )
                );
            }
        );

        if (!config.UsesTime)
            return;

        float duration = config.SafeTimeLimit;

        ValidateFirstSpawnBeforeEnd(
            issues,
            "Normal Enemy",
            config.normalEnemyCount > 0,
            config.normalEnemySpawnInterval,
            duration,
            level =>
                level.normalEnemySpawnInterval =
                    Mathf.Max(
                        0.1f,
                        level.SafeTimeLimit * 0.20f
                    )
        );

        ValidateFirstSpawnBeforeEnd(
            issues,
            "Projectile Enemy",
            config.projectileEnemyCount > 0,
            config.projectileEnemySpawnInterval,
            duration,
            level =>
                level.projectileEnemySpawnInterval =
                    Mathf.Max(
                        0.1f,
                        level.SafeTimeLimit * 0.20f
                    )
        );

        ValidateFirstSpawnBeforeEnd(
            issues,
            "Hunter Enemy",
            config.hunterEnemyCount > 0,
            config.hunterEnemySpawnInterval,
            duration,
            level =>
                level.hunterEnemySpawnInterval =
                    Mathf.Max(
                        0.1f,
                        level.SafeTimeLimit * 0.25f
                    )
        );

        ValidateFirstSpawnBeforeEnd(
            issues,
            "Beacon Enemy",
            config.beaconEnemyCount > 0,
            config.beaconMinSpawnTime,
            duration,
            level =>
            {
                level.beaconMinSpawnTime =
                    level.SafeTimeLimit * 0.30f;
                level.beaconMaxSpawnTime =
                    level.SafeTimeLimit * 0.60f;
            }
        );

        ValidateFirstSpawnBeforeEnd(
            issues,
            "Armor",
            config.armorEnabled,
            config.armorMinSpawnTime,
            duration,
            level =>
            {
                level.armorMinSpawnTime =
                    level.SafeTimeLimit * 0.30f;
                level.armorMaxSpawnTime =
                    level.SafeTimeLimit * 0.65f;
            }
        );

        ValidateFirstSpawnBeforeEnd(
            issues,
            "Slow",
            config.slowEnabled,
            config.slowMinSpawnTime,
            duration,
            level =>
            {
                level.slowMinSpawnTime =
                    level.SafeTimeLimit * 0.30f;
                level.slowMaxSpawnTime =
                    level.SafeTimeLimit * 0.65f;
            }
        );

        if (config.verticalLaserEnabled)
        {
            LaserDangerSettings settings =
                config.ResolveVerticalLaserDanger();

            if (settings.minSpawnTime >= duration)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Warning,
                        "Vertical Laser first-spawn window begins after the mission ends. Choose a faster danger tier or create a per-level override."
                    )
                );
            }
        }

        if (config.horizontalLaserEnabled)
        {
            LaserDangerSettings settings =
                config.ResolveHorizontalLaserDanger();

            if (settings.minSpawnTime >= duration)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Warning,
                        "Horizontal Laser first-spawn window begins after the mission ends. Choose a faster danger tier or create a per-level override."
                    )
                );
            }
        }

        if (config.bombTrapEnabled)
        {
            BombDangerSettings settings =
                config.ResolveBombDanger();

            if (settings.minSpawnTime >= duration)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Warning,
                        "Space Bomb first-spawn window begins after the mission ends. Choose a faster danger tier or create a per-level override."
                    )
                );
            }
        }
    }

    private static void ValidateWindow(
        List<ValidationIssue> issues,
        string displayName,
        bool active,
        float min,
        float max,
        Action<LevelConfig> fix)
    {
        if (!active || min <= max)
            return;

        issues.Add(
            new ValidationIssue(
                ValidationSeverity.Error,
                $"{displayName}: minimum value cannot be greater than maximum value.",
                fix
            )
        );
    }

    private static void ValidateFirstSpawnBeforeEnd(
        List<ValidationIssue> issues,
        string displayName,
        bool active,
        float firstSpawn,
        float duration,
        Action<LevelConfig> fix)
    {
        if (!active || firstSpawn < duration)
            return;

        issues.Add(
            new ValidationIssue(
                ValidationSeverity.Warning,
                $"{displayName} is enabled, but its first spawn occurs at or after the mission ends.",
                fix
            )
        );
    }

    private static void FitWindowToDuration(
        ref float min,
        ref float max,
        float duration)
    {
        float safeDuration =
            Mathf.Max(0.2f, duration);

        float lower = Mathf.Min(min, max);
        float upper = Mathf.Max(min, max);

        if (lower >= safeDuration)
            lower = safeDuration * 0.30f;

        if (upper >= safeDuration)
            upper = safeDuration * 0.70f;

        min = Mathf.Max(0f, lower);
        max = Mathf.Max(min, upper);
    }

    private void ValidateMechanicProgression(
        LevelConfig config,
        List<ValidationIssue> issues)
    {
        int introducedCount = 0;

        foreach (MechanicDescriptor descriptor in
                 MechanicDescriptors)
        {
            bool active =
                descriptor.isActive(config);

            MechanicProgressionStatus status =
                GetMechanicStatus(
                    config,
                    descriptor.id
                );

            if (!active &&
                status !=
                MechanicProgressionStatus.AlreadyKnown)
            {
                MechanicId id = descriptor.id;

                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Warning,
                        $"{descriptor.label} is tagged as {GetMechanicStatusLabel(status)}, but the mechanic is inactive in this level.",
                        level =>
                            SetMechanicStatus(
                                level,
                                id,
                                MechanicProgressionStatus.AlreadyKnown
                            ),
                        "AUTO FIX",
                        descriptor.propertyPath
                    )
                );

                continue;
            }

            if (!active)
                continue;

            if (status ==
                MechanicProgressionStatus.IntroducedHere)
            {
                introducedCount++;

                DangerLevel danger;

                if (TryGetThreatDanger(
                    config,
                    descriptor.id,
                    out danger) &&
                    (int)danger >=
                    (int)DangerLevel.Danger4)
                {
                    issues.Add(
                        new ValidationIssue(
                            ValidationSeverity.Warning,
                            $"{descriptor.label} is introduced at {DangerLevelUtility.GetDisplayName(danger)}. First appearances usually need a more readable D1–D2 setup."
                        )
                    );
                }

                List<LevelConfig> duplicates =
                    FindDuplicateIntroductions(
                        config,
                        descriptor
                    );

                if (duplicates.Count > 0)
                {
                    List<string> labels =
                        new List<string>();

                    foreach (LevelConfig duplicate in
                             duplicates)
                    {
                        labels.Add(
                            $"Level {duplicate.levelNumber}"
                        );
                    }

                    issues.Add(
                        new ValidationIssue(
                            ValidationSeverity.Warning,
                            $"{descriptor.label} is also marked as introduced in {string.Join(", ", labels)}.",
                            null,
                            "AUTO FIX",
                            descriptor.propertyPath
                        )
                    );
                }
            }
        }

        if (introducedCount > 2)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Suggestion,
                    $"This level introduces {introducedCount} mechanics at once. Consider limiting major introductions to one or two so the player can read each system."
                )
            );
        }
    }

    private static void ValidateObstaclePool(
        LevelConfig config,
        List<ValidationIssue> issues)
    {
        if (config.obstacleSpawnMode !=
            ObstacleSpawnMode.Random ||
            config.randomObstacleCount <= 0)
        {
            return;
        }

        bool hasEnabledPrefab = false;

        if (config.levelObstacles != null)
        {
            foreach (LevelObstacleOption option in
                     config.levelObstacles)
            {
                if (option != null &&
                    option.enabled &&
                    option.prefab != null)
                {
                    hasEnabledPrefab = true;
                    break;
                }
            }
        }

        if (!hasEnabledPrefab)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    "Random obstacle spawning is enabled, but the obstacle pool contains no enabled prefab.",
                    null,
                    "AUTO FIX",
                    "levelObstacles"
                )
            );
        }
    }

    private static int CountOpeningPressure(
        LevelConfig config,
        float openingSeconds)
    {
        int count = 0;

        if (config.normalEnemyCount > 0 &&
            config.normalEnemySpawnInterval <= openingSeconds)
            count++;

        if (config.projectileEnemyCount > 0 &&
            config.projectileEnemySpawnInterval <= openingSeconds)
            count++;

        if (config.hunterEnemyCount > 0 &&
            config.hunterEnemySpawnInterval <= openingSeconds)
            count++;

        if (config.beaconEnemyCount > 0 &&
            config.beaconMinSpawnTime <= openingSeconds)
            count++;

        if (config.bossEnabled &&
            config.EffectiveBossSpawnCondition ==
            BossSpawnCondition.Time &&
            config.bossSpawnTime <= openingSeconds)
            count++;

        if (config.verticalLaserEnabled &&
            config.ResolveVerticalLaserDanger()
                .minSpawnTime <= openingSeconds)
            count++;

        if (config.horizontalLaserEnabled &&
            config.ResolveHorizontalLaserDanger()
                .minSpawnTime <= openingSeconds)
            count++;

        if (config.bombTrapEnabled &&
            config.ResolveBombDanger()
                .minSpawnTime <= openingSeconds)
            count++;

        return count;
    }

    private static bool HasAnyThreat(
        LevelConfig config)
    {
        return
            config.normalEnemyCount > 0 ||
            config.projectileEnemyCount > 0 ||
            config.hunterEnemyCount > 0 ||
            config.beaconEnemyCount > 0 ||
            config.bossEnabled ||
            config.verticalLaserEnabled ||
            config.horizontalLaserEnabled ||
            config.bombTrapEnabled;
    }

    private static float GetEnabledCoinChance(
        LevelConfig config)
    {
        float total = 0f;

        if (config.normalCoinEnabled)
            total += config.normalCoinChance;

        if (config.goldCoinEnabled)
            total += config.goldCoinChance;

        if (config.rareCoinEnabled)
            total += config.rareCoinChance;

        return total;
    }

    private static void NormalizeCoinChances(
        LevelConfig config)
    {
        float total =
            GetEnabledCoinChance(config);

        if (total <= 0f)
        {
            config.normalCoinEnabled = true;
            config.normalCoinChance = 100f;
            config.goldCoinChance = 0f;
            config.rareCoinChance = 0f;
            return;
        }

        float multiplier = 100f / total;

        if (config.normalCoinEnabled)
            config.normalCoinChance *= multiplier;

        if (config.goldCoinEnabled)
            config.goldCoinChance *= multiplier;

        if (config.rareCoinEnabled)
            config.rareCoinChance *= multiplier;
    }

    private static bool HasActiveObstacles(
        LevelConfig config)
    {
        if (config == null)
            return false;

        bool hasEnabledPrefab = false;

        if (config.levelObstacles != null)
        {
            foreach (LevelObstacleOption option in
                     config.levelObstacles)
            {
                if (option != null &&
                    option.enabled &&
                    option.prefab != null)
                {
                    hasEnabledPrefab = true;
                    break;
                }
            }
        }

        if (!hasEnabledPrefab)
            return false;

        return config.obstacleSpawnMode ==
               ObstacleSpawnMode.Fixed ||
               config.randomObstacleCount > 0;
    }

    private void GenerateBriefingDraftWithConfirmation()
    {
        LevelConfig config = GetSingleConfig();

        if (config == null)
            return;

        serializedObject.ApplyModifiedProperties();

        if (HasCustomBriefingContent(config))
        {
            bool confirmed =
                EditorUtility.DisplayDialog(
                    "Generate Briefing Draft",
                    "Existing custom briefing text will be replaced by a new draft based on this level's current design.",
                    "Generate",
                    "Cancel"
                );

            if (!confirmed)
            {
                serializedObject.Update();
                return;
            }
        }

        Undo.RecordObject(
            config,
            "Generate Briefing Draft"
        );

        PopulateBriefingDraft(config);

        EditorUtility.SetDirty(config);
        serializedObject.Update();
        GUI.changed = true;
    }

    private static void PopulateBriefingDraft(
        LevelConfig config)
    {
        if (config == null)
            return;

        if (string.IsNullOrWhiteSpace(
            config.briefingTitle))
        {
            config.briefingTitle =
                "MISSION BRIEFING";
        }

        config.briefingModeDescription =
            config.GetDefaultModeDescription();

        switch (config.winCondition)
        {
            case WinConditionType.ReachScore:
                config.briefingObjectiveDescription =
                    $"Collect {config.SafeWinScore} points and complete the mission.";
                break;

            case WinConditionType.SurviveTime:
                config.briefingObjectiveDescription =
                    $"Survive for {FormatSecondsForEditor(config.SafeTimeLimit)}. Keep moving until the countdown reaches zero.";
                break;

            case WinConditionType.ReachScoreWithinTime:
                config.briefingObjectiveDescription =
                    $"Collect {config.SafeWinScore} points before the {FormatSecondsForEditor(config.SafeTimeLimit)} countdown reaches zero.";
                break;
        }

        List<string> pages = new List<string>();
        List<MechanicDescriptor> introduced =
            GetActiveMechanicsWithStatus(
                config,
                MechanicProgressionStatus.IntroducedHere
            );

        for (int i = 0;
             i < introduced.Count && i < 3;
             i++)
        {
            pages.Add(
                introduced[i].briefingText
            );
        }

        if (introduced.Count > 3)
        {
            List<string> remaining =
                new List<string>();

            for (int i = 3;
                 i < introduced.Count;
                 i++)
            {
                remaining.Add(
                    introduced[i].label
                );
            }

            pages.Add(
                "This mission also introduces: " +
                string.Join(", ", remaining) +
                ". Take time to read how these systems interact."
            );
        }

        List<MechanicDescriptor> finalChallenges =
            GetActiveMechanicsWithStatus(
                config,
                MechanicProgressionStatus.FinalChallenge
            );

        if (finalChallenges.Count > 0)
        {
            List<string> names =
                new List<string>();

            foreach (MechanicDescriptor descriptor in
                     finalChallenges)
            {
                names.Add(descriptor.label);
            }

            pages.Add(
                "Mastery test: " +
                string.Join(", ", names) +
                ". Expect little room for mistakes and use every tool deliberately."
            );
        }

        if (pages.Count == 0)
        {
            string enemyPage =
                BuildEnemyBriefingPage(config);

            if (!string.IsNullOrWhiteSpace(enemyPage))
                pages.Add(enemyPage);

            string hazardPage =
                BuildHazardBriefingPage(config);

            if (!string.IsNullOrWhiteSpace(hazardPage))
                pages.Add(hazardPage);

            string supportPage =
                BuildSupportBriefingPage(config);

            if (!string.IsNullOrWhiteSpace(supportPage))
                pages.Add(supportPage);
        }

        if (pages.Count == 0)
        {
            pages.Add(
                "Read the arena, keep moving and complete the objective without wasting your escape routes."
            );
        }

        config.briefingPages =
            pages.ToArray();
    }

    private static string BuildEnemyBriefingPage(
        LevelConfig config)
    {
        List<string> enemies =
            new List<string>();

        if (config.normalEnemyCount > 0)
            enemies.Add("normal enemies");

        if (config.projectileEnemyCount > 0)
            enemies.Add("projectile enemies");

        if (config.hunterEnemyCount > 0)
            enemies.Add("hunters");

        if (config.beaconEnemyCount > 0)
            enemies.Add("beacons");

        if (config.bossEnabled)
            enemies.Add("a boss encounter");

        if (enemies.Count == 0)
            return string.Empty;

        return
            "Active threats: " +
            string.Join(", ", enemies) +
            ". Keep enough space to react when their pressure overlaps.";
    }

    private static string BuildHazardBriefingPage(
        LevelConfig config)
    {
        List<string> hazards =
            new List<string>();

        if (config.verticalLaserEnabled)
            hazards.Add("vertical lasers");

        if (config.horizontalLaserEnabled)
            hazards.Add("horizontal lasers");

        if (config.bombTrapEnabled)
            hazards.Add("space bombs");

        if (hazards.Count == 0)
            return string.Empty;

        return
            "Arena hazards: " +
            string.Join(", ", hazards) +
            ". Watch their warnings and protect your next escape route.";
    }

    private static string BuildSupportBriefingPage(
        LevelConfig config)
    {
        List<string> tools =
            new List<string>();

        if (config.dashEnabled)
            tools.Add("Dash");

        if (config.cloneEnabled)
            tools.Add("Clone");

        if (config.armorEnabled)
            tools.Add("Armor");

        if (config.slowEnabled)
            tools.Add("Slow");

        if (tools.Count == 0)
            return string.Empty;

        return
            "Available tools: " +
            string.Join(", ", tools) +
            ". Use them deliberately instead of waiting until the arena is already closed around you.";
    }

    private static bool HasCustomBriefingContent(
        LevelConfig config)
    {
        if (!string.IsNullOrWhiteSpace(
                config.briefingModeDescription) ||
            !string.IsNullOrWhiteSpace(
                config.briefingObjectiveDescription))
        {
            return true;
        }

        if (config.briefingPages == null)
            return false;

        foreach (string page in
                 config.briefingPages)
        {
            if (!string.IsNullOrWhiteSpace(page) &&
                !IsPlaceholderBriefingPage(page))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUsefulBriefingPages(
        LevelConfig config)
    {
        if (config.briefingPages == null ||
            config.briefingPages.Length == 0)
        {
            return false;
        }

        foreach (string page in
                 config.briefingPages)
        {
            if (!string.IsNullOrWhiteSpace(page) &&
                !IsPlaceholderBriefingPage(page))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPlaceholderBriefingPage(
        string page)
    {
        if (string.IsNullOrWhiteSpace(page))
            return true;

        string value =
            page.Trim();

        return string.Equals(
                   value,
                   "Mission information...",
                   StringComparison.OrdinalIgnoreCase
               ) ||
               string.Equals(
                   value,
                   "Exclusive for Tester",
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private static int CountNonEmptyBriefingPages(
        string[] pages)
    {
        if (pages == null)
            return 0;

        int count = 0;

        foreach (string page in pages)
        {
            if (!string.IsNullOrWhiteSpace(page))
                count++;
        }

        return count;
    }

    private static string FormatSecondsForEditor(
        float seconds)
    {
        float safeSeconds =
            Mathf.Max(0f, seconds);

        if (Mathf.Approximately(
            safeSeconds,
            Mathf.Round(safeSeconds)))
        {
            return
                $"{Mathf.RoundToInt(safeSeconds)} seconds";
        }

        return $"{safeSeconds:0.#} seconds";
    }

    private static List<MechanicDescriptor>
        GetActiveMechanicsWithStatus(
            LevelConfig config,
            MechanicProgressionStatus status)
    {
        List<MechanicDescriptor> result =
            new List<MechanicDescriptor>();

        foreach (MechanicDescriptor descriptor in
                 MechanicDescriptors)
        {
            if (descriptor.isActive(config) &&
                GetMechanicStatus(
                    config,
                    descriptor.id) == status)
            {
                result.Add(descriptor);
            }
        }

        return result;
    }

    private static string GetProgressionSummary(
        LevelConfig config,
        MechanicProgressionStatus status)
    {
        if (config == null ||
            config.mechanicProgression == null)
        {
            return "None";
        }

        List<string> labels =
            new List<string>();

        foreach (MechanicDescriptor descriptor in
                 MechanicDescriptors)
        {
            if (!descriptor.isActive(config))
                continue;

            if (GetMechanicStatus(
                    config,
                    descriptor.id) == status)
            {
                labels.Add(descriptor.label);
            }
        }

        return labels.Count > 0
            ? string.Join(", ", labels)
            : "None";
    }

    private static string GetMechanicStatusLabel(
        MechanicProgressionStatus status)
    {
        switch (status)
        {
            case MechanicProgressionStatus.IntroducedHere:
                return "Introduced In This Level";

            case MechanicProgressionStatus.FinalChallenge:
                return "Final Challenge";

            default:
                return "Already Known";
        }
    }

    private static MechanicProgressionStatus
        GetMechanicStatus(
            LevelConfig config,
            MechanicId id)
    {
        if (config == null ||
            config.mechanicProgression == null)
        {
            return
                MechanicProgressionStatus.AlreadyKnown;
        }

        LevelMechanicProgression progression =
            config.mechanicProgression;

        switch (id)
        {
            case MechanicId.ReachScoreMode:
                return progression.reachScoreMode;
            case MechanicId.SurviveTimeMode:
                return progression.surviveTimeMode;
            case MechanicId.TimedScoreMode:
                return progression.timedScoreMode;
            case MechanicId.Dash:
                return progression.dash;
            case MechanicId.Clone:
                return progression.clone;
            case MechanicId.Combo:
                return progression.combo;
            case MechanicId.NormalCoin:
                return progression.normalCoin;
            case MechanicId.GoldCoin:
                return progression.goldCoin;
            case MechanicId.RareCoin:
                return progression.rareCoin;
            case MechanicId.StaticObstacles:
                return progression.staticObstacles;
            case MechanicId.NormalEnemy:
                return progression.normalEnemy;
            case MechanicId.ProjectileEnemy:
                return progression.projectileEnemy;
            case MechanicId.HunterEnemy:
                return progression.hunterEnemy;
            case MechanicId.Boss:
                return progression.boss;
            case MechanicId.BeaconEnemy:
                return progression.beaconEnemy;
            case MechanicId.Armor:
                return progression.armor;
            case MechanicId.Slow:
                return progression.slow;
            case MechanicId.VerticalLaser:
                return progression.verticalLaser;
            case MechanicId.HorizontalLaser:
                return progression.horizontalLaser;
            case MechanicId.SpaceBomb:
                return progression.spaceBomb;
            default:
                return
                    MechanicProgressionStatus.AlreadyKnown;
        }
    }

    private static void SetMechanicStatus(
        LevelConfig config,
        MechanicId id,
        MechanicProgressionStatus status)
    {
        if (config.mechanicProgression == null)
        {
            config.mechanicProgression =
                new LevelMechanicProgression();
        }

        LevelMechanicProgression progression =
            config.mechanicProgression;

        switch (id)
        {
            case MechanicId.ReachScoreMode:
                progression.reachScoreMode = status;
                break;
            case MechanicId.SurviveTimeMode:
                progression.surviveTimeMode = status;
                break;
            case MechanicId.TimedScoreMode:
                progression.timedScoreMode = status;
                break;
            case MechanicId.Dash:
                progression.dash = status;
                break;
            case MechanicId.Clone:
                progression.clone = status;
                break;
            case MechanicId.Combo:
                progression.combo = status;
                break;
            case MechanicId.NormalCoin:
                progression.normalCoin = status;
                break;
            case MechanicId.GoldCoin:
                progression.goldCoin = status;
                break;
            case MechanicId.RareCoin:
                progression.rareCoin = status;
                break;
            case MechanicId.StaticObstacles:
                progression.staticObstacles = status;
                break;
            case MechanicId.NormalEnemy:
                progression.normalEnemy = status;
                break;
            case MechanicId.ProjectileEnemy:
                progression.projectileEnemy = status;
                break;
            case MechanicId.HunterEnemy:
                progression.hunterEnemy = status;
                break;
            case MechanicId.Boss:
                progression.boss = status;
                break;
            case MechanicId.BeaconEnemy:
                progression.beaconEnemy = status;
                break;
            case MechanicId.Armor:
                progression.armor = status;
                break;
            case MechanicId.Slow:
                progression.slow = status;
                break;
            case MechanicId.VerticalLaser:
                progression.verticalLaser = status;
                break;
            case MechanicId.HorizontalLaser:
                progression.horizontalLaser = status;
                break;
            case MechanicId.SpaceBomb:
                progression.spaceBomb = status;
                break;
        }
    }

    private static bool TryGetThreatDanger(
        LevelConfig config,
        MechanicId id,
        out DangerLevel danger)
    {
        switch (id)
        {
            case MechanicId.NormalEnemy:
                danger = config.normalEnemyDanger;
                return true;
            case MechanicId.ProjectileEnemy:
                danger = config.projectileEnemyDanger;
                return true;
            case MechanicId.HunterEnemy:
                danger = config.hunterEnemyDanger;
                return true;
            case MechanicId.Boss:
                danger = config.bossDanger;
                return true;
            case MechanicId.BeaconEnemy:
                danger = config.beaconEnemyDanger;
                return true;
            case MechanicId.VerticalLaser:
                danger = config.verticalLaserDanger;
                return true;
            case MechanicId.HorizontalLaser:
                danger = config.horizontalLaserDanger;
                return true;
            case MechanicId.SpaceBomb:
                danger = config.bombDanger;
                return true;
            default:
                danger = DangerLevel.Danger2;
                return false;
        }
    }

    private static List<LevelConfig>
        FindDuplicateIntroductions(
            LevelConfig current,
            MechanicDescriptor descriptor)
    {
        List<LevelConfig> duplicates =
            new List<LevelConfig>();

        foreach (LevelConfig config in
                 GetAllLevelConfigs())
        {
            if (config == null ||
                config == current ||
                config.mechanicProgression == null ||
                !descriptor.isActive(config))
            {
                continue;
            }

            if (GetMechanicStatus(
                    config,
                    descriptor.id) ==
                MechanicProgressionStatus.IntroducedHere)
            {
                duplicates.Add(config);
            }
        }

        return duplicates;
    }

    private static List<LevelConfig>
        GetAllLevelConfigs()
    {
        double now =
            EditorApplication.timeSinceStartup;

        if (cachedLevelConfigs != null &&
            now - cachedLevelConfigTime < 5d)
        {
            return cachedLevelConfigs;
        }

        cachedLevelConfigTime = now;
        cachedLevelConfigs =
            new List<LevelConfig>();

        string[] guids =
            AssetDatabase.FindAssets(
                "t:LevelConfig"
            );

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            LevelConfig config =
                AssetDatabase.LoadAssetAtPath<LevelConfig>(
                    path
                );

            if (config != null)
                cachedLevelConfigs.Add(config);
        }

        return cachedLevelConfigs;
    }

    private void EnsureProgressionMetadata()
    {
        if (targets == null)
            return;

        foreach (UnityEngine.Object item in
                 targets)
        {
            LevelConfig config =
                item as LevelConfig;

            if (config == null ||
                config.mechanicProgression != null)
            {
                continue;
            }

            config.mechanicProgression =
                new LevelMechanicProgression();

            EditorUtility.SetDirty(config);
        }
    }

    private void CreateBalancedProfile(SerializedProperty profileProperty)
    {
        string defaultFolder = "Assets/Balance";

        if (!AssetDatabase.IsValidFolder(defaultFolder))
            AssetDatabase.CreateFolder("Assets", "Balance");

        string path = AssetDatabase.GenerateUniqueAssetPath(
            defaultFolder + "/Default_Danger_Balance.asset"
        );

        DangerBalanceProfile profile =
            ScriptableObject.CreateInstance<DangerBalanceProfile>();

        profile.ResetToBalancedDefaults();
        AssetDatabase.CreateAsset(profile, path);
        AssetDatabase.SaveAssets();

        if (profileProperty != null)
            profileProperty.objectReferenceValue = profile;

        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }

    private void ForceBossCondition(BossSpawnCondition condition)
    {
        SerializedProperty property =
            serializedObject.FindProperty("bossSpawnCondition");

        if (property == null || property.hasMultipleDifferentValues)
            return;

        property.enumValueIndex = (int)condition;
    }

    private void ValidateMinMax(
        string minPropertyName,
        string maxPropertyName,
        string displayName)
    {
        SerializedProperty minProperty =
            serializedObject.FindProperty(minPropertyName);

        SerializedProperty maxProperty =
            serializedObject.FindProperty(maxPropertyName);

        if (minProperty == null ||
            maxProperty == null ||
            minProperty.hasMultipleDifferentValues ||
            maxProperty.hasMultipleDifferentValues)
        {
            return;
        }

        if (GetNumericValue(minProperty) <= GetNumericValue(maxProperty))
            return;

        EditorGUILayout.HelpBox(
            displayName + ": minimum value cannot be greater than maximum value.",
            MessageType.Error
        );
    }

    private static float GetNumericValue(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return property.intValue;
            case SerializedPropertyType.Float:
                return property.floatValue;
            default:
                return 0f;
        }
    }

    private LevelConfig GetSingleConfig()
    {
        return serializedObject.isEditingMultipleObjects
            ? null
            : target as LevelConfig;
    }

    private static int CountExtremeThreats(LevelConfig config)
    {
        int count = 0;

        AddExtreme(config.normalEnemyCount > 0, config.normalEnemyDanger, ref count);
        AddExtreme(config.projectileEnemyCount > 0, config.projectileEnemyDanger, ref count);
        AddExtreme(config.hunterEnemyCount > 0, config.hunterEnemyDanger, ref count);
        AddExtreme(config.beaconEnemyCount > 0, config.beaconEnemyDanger, ref count);
        AddExtreme(config.bossEnabled, config.bossDanger, ref count);
        AddExtreme(config.verticalLaserEnabled, config.verticalLaserDanger, ref count);
        AddExtreme(config.horizontalLaserEnabled, config.horizontalLaserDanger, ref count);
        AddExtreme(config.bombTrapEnabled, config.bombDanger, ref count);

        return count;
    }

    private static void AddExtreme(
        bool active,
        DangerLevel level,
        ref int count)
    {
        if (active && (int)level >= (int)DangerLevel.Danger4)
            count++;
    }

    private static bool HasAnyCustomOverride(LevelConfig config)
    {
        return config.normalEnemyCustomOverride ||
               config.projectileEnemyCustomOverride ||
               config.hunterEnemyCustomOverride ||
               config.bossCustomOverride ||
               config.beaconEnemyCustomOverride ||
               config.verticalLaserCustomOverride ||
               config.horizontalLaserCustomOverride ||
               config.bombCustomOverride;
    }

    private static string GetWinConditionSummary(LevelConfig config)
    {
        switch (config.winCondition)
        {
            case WinConditionType.ReachScore:
                return $"Reach {config.SafeWinScore} Score";
            case WinConditionType.SurviveTime:
                return $"Survive {config.SafeTimeLimit:0.##} Seconds";
            case WinConditionType.ReachScoreWithinTime:
                return $"Reach {config.SafeWinScore} in {config.SafeTimeLimit:0.##} Seconds";
            default:
                return "Unknown";
        }
    }

    private static string GetBossSummary(LevelConfig config)
    {
        if (!config.bossEnabled)
            return "Disabled";

        string trigger = config.EffectiveBossSpawnCondition == BossSpawnCondition.Score
            ? $"at {config.SafeBossSpawnScore} score"
            : $"after {config.SafeBossSpawnTime:0.##}s";

        return $"{trigger} • {DangerLevelUtility.GetShortLabel(config.bossDanger)}";
    }

    private static string GetEnemySummary(LevelConfig config)
    {
        List<string> values = new List<string>();

        if (config.normalEnemyCount > 0)
            values.Add($"{config.normalEnemyCount} Normal {DangerLevelUtility.GetShortLabel(config.normalEnemyDanger)}");
        if (config.projectileEnemyCount > 0)
            values.Add($"{config.projectileEnemyCount} Projectile {DangerLevelUtility.GetShortLabel(config.projectileEnemyDanger)}");
        if (config.hunterEnemyCount > 0)
            values.Add($"{config.hunterEnemyCount} Hunter {DangerLevelUtility.GetShortLabel(config.hunterEnemyDanger)}");
        if (config.beaconEnemyCount > 0)
            values.Add($"{config.beaconEnemyCount} Beacon {DangerLevelUtility.GetShortLabel(config.beaconEnemyDanger)}");

        return values.Count > 0
            ? string.Join(", ", values)
            : "None";
    }

    private static string GetHazardSummary(LevelConfig config)
    {
        List<string> values = new List<string>();

        if (config.verticalLaserEnabled)
            values.Add($"Vertical {DangerLevelUtility.GetShortLabel(config.verticalLaserDanger)}");
        if (config.horizontalLaserEnabled)
            values.Add($"Horizontal {DangerLevelUtility.GetShortLabel(config.horizontalLaserDanger)}");
        if (config.bombTrapEnabled)
            values.Add($"Bomb {DangerLevelUtility.GetShortLabel(config.bombDanger)}");

        return values.Count > 0
            ? string.Join(", ", values)
            : "None";
    }

    private string GetFoldoutPrefsKey(
        string foldoutType,
        string title)
    {
        LevelConfig config = target as LevelConfig;

        string assetId = "GLOBAL";

        if (config != null)
        {
            string assetPath =
                AssetDatabase.GetAssetPath(config);

            if (!string.IsNullOrEmpty(assetPath))
            {
                string guid =
                    AssetDatabase.AssetPathToGUID(assetPath);

                if (!string.IsNullOrEmpty(guid))
                    assetId = guid;
            }
            else
            {
                assetId =
                    config.GetEntityId().ToString();
            }
        }

        return
            $"VoidRush.LevelConfigEditor." +
            $"{assetId}.{foldoutType}.{title}";
    }

    private void FoldoutBox(
        string title,
        ref bool expanded,
        Action content)
    {
        string prefsKey =
            GetFoldoutPrefsKey("Main", title);

        expanded = EditorPrefs.GetBool(
            prefsKey,
            expanded
        );

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox
        );

        EditorGUI.BeginChangeCheck();

        bool newExpanded =
            EditorGUILayout.Foldout(
                expanded,
                title,
                true,
                EditorStyles.foldoutHeader
            );

        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetBool(
                prefsKey,
                newExpanded
            );
        }

        expanded = newExpanded;

        if (expanded)
        {
            EditorGUILayout.Space(3);
            content?.Invoke();
        }

        EditorGUILayout.EndVertical();
    }

    private void NestedFoldout(
        string title,
        ref bool expanded,
        Action content)
    {
        string prefsKey =
            GetFoldoutPrefsKey("Nested", title);

        expanded = EditorPrefs.GetBool(
            prefsKey,
            expanded
        );

        EditorGUILayout.Space(4);

        EditorGUI.BeginChangeCheck();

        bool newExpanded =
            EditorGUILayout.Foldout(
                expanded,
                title,
                true
            );

        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetBool(
                prefsKey,
                newExpanded
            );
        }

        expanded = newExpanded;

        if (!expanded)
            return;

        EditorGUI.indentLevel++;
        content?.Invoke();
        EditorGUI.indentLevel--;
    }

    private static void SummaryRow(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(110f));
        EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
    }

    private static void MiniTitle(string title)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
    }

    private static void Help(string text)
    {
        EditorGUILayout.HelpBox(text, MessageType.Info);
    }

    private static void Warning(string text)
    {
        EditorGUILayout.HelpBox(text, MessageType.Warning);
    }

    private static void Space()
    {
        EditorGUILayout.Space(6);
    }

    private void PropWithLabel(
        string name,
        string label,
        string tooltip = null)
    {
        SerializedProperty property =
            serializedObject.FindProperty(name);

        if (property == null)
        {
            EditorGUILayout.HelpBox(
                "Missing serialized property: " + name,
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.PropertyField(
            property,
            new GUIContent(label, tooltip)
        );
    }

    private void Prop(string name, bool includeChildren = false)
    {
        SerializedProperty property =
            serializedObject.FindProperty(name);

        if (property == null)
        {
            EditorGUILayout.HelpBox(
                "Missing serialized property: " + name,
                MessageType.Error
            );

            return;
        }

        EditorGUILayout.PropertyField(property, includeChildren);
    }

    private bool BoolValue(string name)
    {
        SerializedProperty property =
            serializedObject.FindProperty(name);

        return property != null && property.boolValue;
    }

    private int IntValue(string name)
    {
        SerializedProperty property =
            serializedObject.FindProperty(name);

        return property != null ? property.intValue : 0;
    }


    private DangerLevel DangerValue(string name)
    {
        SerializedProperty property =
            serializedObject.FindProperty(name);

        if (property == null)
            return DangerLevel.Danger2;

        return DangerLevelUtility.Sanitize(
            (DangerLevel)property.intValue
        );
    }

    private int EnumValue(string name)
    {
        SerializedProperty property =
            serializedObject.FindProperty(name);

        return property != null ? property.enumValueIndex : 0;
    }
}
