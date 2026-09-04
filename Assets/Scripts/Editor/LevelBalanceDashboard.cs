#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class LevelBalanceDashboard : EditorWindow
{
    private enum DashboardTab
    {
        Overview,
        Mechanics,
        Enemies,
        Hazards,
        Changes
    }

    private enum RowFilter
    {
        AllLevels,
        WarningsOnly,
        ChangesOnly
    }

    private struct PressureBreakdown
    {
        public float total;
        public float enemies;
        public float hazards;
        public float support;
        public float objective;
        public float exposureSeconds;
        public float exposureMultiplier;
    }

    private sealed class LevelRow
    {
        public LevelConfig config;
        public PressureBreakdown pressureBreakdown;
        public float pressure;
        public float previousDelta;
        public float previousDeltaPercent;
        public float nextDelta;
        public float nextDeltaPercent;
        public string changeSummary;
        public string newMechanics;
        public string missionChange;
        public string warning;
        public bool onboarding;
        public WarningLevel warningLevel;
    }

    private enum WarningLevel
    {
        None,
        Notice,
        Warning,
        Spike
    }

    private static readonly string[] TabLabels =
    {
        "OVERVIEW",
        "MECHANICS",
        "ENEMIES",
        "HAZARDS",
        "CHANGES"
    };

    private static readonly string[] FilterLabels =
    {
        "All Levels",
        "Warnings Only",
        "Changes Only"
    };

    private readonly List<LevelConfig> levels = new List<LevelConfig>(40);
    private readonly List<LevelRow> rows = new List<LevelRow>(40);

    private DashboardTab tab;
    private RowFilter filter;
    private Vector2 scroll;
    private string search = string.Empty;
    private bool showDeltaPercent;
    private double nextAutoRefresh;
    private int lastAssetHash;

    private GUIStyle headerStyle;
    private GUIStyle cellStyle;
    private GUIStyle centeredCellStyle;
    private GUIStyle levelButtonStyle;
    private GUIStyle miniBadgeStyle;

    [MenuItem("Tools/Fateful Rush/Level Balance Dashboard", priority = 120)]
    public static void Open()
    {
        LevelBalanceDashboard window = GetWindow<LevelBalanceDashboard>();
        window.titleContent = new GUIContent("Level Balance");
        window.minSize = new Vector2(940f, 430f);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnExternalChange;
        EditorApplication.projectChanged += OnExternalChange;
        EditorApplication.hierarchyChanged += OnExternalChange;
        RefreshData(true);
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnExternalChange;
        EditorApplication.projectChanged -= OnExternalChange;
        EditorApplication.hierarchyChanged -= OnExternalChange;
    }

    private void OnInspectorUpdate()
    {
        // LevelConfig inspectors apply serialized changes immediately. A light periodic rebuild keeps
        // this window live without storing any duplicate balance data.
        if (EditorApplication.timeSinceStartup >= nextAutoRefresh)
        {
            nextAutoRefresh = EditorApplication.timeSinceStartup + 0.35d;
            RefreshData(false);
        }
    }

    private void OnExternalChange()
    {
        RefreshData(true);
    }

    private void RefreshData(bool forceAssetScan)
    {
        if (forceAssetScan || levels.Count == 0)
            ScanLevelAssets();

        int hash = ComputeAssetHash();
        if (!forceAssetScan && hash == lastAssetHash)
            return;

        lastAssetHash = hash;
        RebuildRows();
        Repaint();
    }

    private void ScanLevelAssets()
    {
        levels.Clear();

        string[] guids = AssetDatabase.FindAssets(
            "t:LevelConfig",
            new[] { "Assets/LevelConfigs" }
        );

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);

            if (config == null || config.levelNumber < 1 || config.levelNumber > 40)
                continue;

            levels.Add(config);
        }

        levels.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber));
    }

    private int ComputeAssetHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + levels.Count;

            foreach (LevelConfig config in levels)
            {
                if (config == null)
                    continue;

                // JsonUtility is editor-only here and gives us a simple serialized-content fingerprint.
                string json = EditorJsonUtility.ToJson(config, false);
                hash = hash * 31 + json.GetHashCode();
            }

            return hash;
        }
    }

    private void RebuildRows()
    {
        rows.Clear();

        for (int i = 0; i < levels.Count; i++)
        {
            LevelConfig current = levels[i];
            LevelConfig previous = i > 0 ? levels[i - 1] : null;
            PressureBreakdown pressure = CalculatePressureBreakdown(current);

            LevelRow row = new LevelRow
            {
                config = current,
                pressureBreakdown = pressure,
                pressure = pressure.total,
                changeSummary = BuildChangeSummary(previous, current),
                newMechanics = BuildIntroducedMechanics(previous, current),
                missionChange = BuildMissionChange(previous, current)
            };

            if (i > 0)
            {
                LevelRow previousRow = rows[i - 1];
                row.previousDelta = row.pressure - previousRow.pressure;
                row.previousDeltaPercent = previousRow.pressure > 0.01f
                    ? row.previousDelta / previousRow.pressure * 100f
                    : 0f;
            }

            rows.Add(row);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (i + 1 < rows.Count)
            {
                rows[i].nextDelta = rows[i + 1].pressure - rows[i].pressure;
                rows[i].nextDeltaPercent = rows[i].pressure > 0.01f
                    ? rows[i].nextDelta / rows[i].pressure * 100f
                    : 0f;
            }

            EvaluateWarning(rows[i], i > 0 ? rows[i - 1] : null);
        }
    }

    private void EvaluateWarning(LevelRow row, LevelRow previous)
    {
        row.warningLevel = WarningLevel.None;
        row.warning = "";
        row.onboarding = false;

        if (previous == null)
        {
            row.onboarding = CountCsvItems(row.newMechanics) > 0;
            return;
        }

        float absoluteDelta = row.previousDelta;
        float percent = row.previousDeltaPercent;
        int newlyIntroduced = CountCsvItems(row.newMechanics);
        bool hasStableBaseline = previous.pressure >= 8f;

        // Pressure v2 deliberately requires a meaningful absolute increase. Percentage-only
        // jumps on tiny early-game values no longer create noisy warnings.
        if (absoluteDelta >= 14f && (!hasStableBaseline || percent >= 22f))
        {
            row.warningLevel = WarningLevel.Spike;
            row.warning = "Large pressure spike";
        }
        else if (absoluteDelta >= 9f && (!hasStableBaseline || percent >= 16f))
        {
            row.warningLevel = WarningLevel.Warning;
            row.warning = "Noticeable pressure jump";
        }
        else if (absoluteDelta >= 6f && (!hasStableBaseline || percent >= 12f))
        {
            row.warningLevel = WarningLevel.Notice;
            row.warning = "Check transition";
        }

        // One or two introductions are treated as normal onboarding. Three deserves a check;
        // four or more is dense enough to warrant a warning even if raw pressure barely moves.
        if (newlyIntroduced >= 4 && row.warningLevel < WarningLevel.Warning)
        {
            row.warningLevel = WarningLevel.Warning;
            row.warning = $"{newlyIntroduced} mechanics introduced together";
        }
        else if (newlyIntroduced == 3 && row.warningLevel < WarningLevel.Notice)
        {
            row.warningLevel = WarningLevel.Notice;
            row.warning = "3 mechanics introduced together";
        }

        if (newlyIntroduced > 0 && absoluteDelta >= 8f && row.warningLevel < WarningLevel.Warning)
        {
            row.warningLevel = WarningLevel.Warning;
            row.warning = "New mechanic + pressure jump";
        }

        if (HasHighDangerFirstAppearance(previous.config, row.config) &&
            row.warningLevel < WarningLevel.Warning)
        {
            row.warningLevel = WarningLevel.Warning;
            row.warning = "New threat starts at D4-D5";
        }

        if (Mathf.Abs(row.config.missionDifficulty - previous.config.missionDifficulty) >= 2 &&
            row.warningLevel < WarningLevel.Notice)
        {
            row.warningLevel = WarningLevel.Notice;
            row.warning = "Inspector difficulty jumps by 2+ stars";
        }

        row.onboarding = newlyIntroduced > 0 && row.warningLevel == WarningLevel.None;
    }

    private void OnGUI()
    {
        EnsureStyles();
        DrawToolbar();
        DrawLegend();

        if (levels.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No LevelConfig assets numbered 1-40 were found under Assets/LevelConfigs.",
                MessageType.Warning
            );
            return;
        }

        if (levels.Count != 40)
        {
            EditorGUILayout.HelpBox(
                $"Found {levels.Count}/40 numbered LevelConfig assets. Missing levels will not appear until their config asset exists.",
                MessageType.Warning
            );
        }

        switch (tab)
        {
            case DashboardTab.Overview:
                DrawOverview();
                break;
            case DashboardTab.Mechanics:
                DrawMechanics();
                break;
            case DashboardTab.Enemies:
                DrawEnemies();
                break;
            case DashboardTab.Hazards:
                DrawHazards();
                break;
            case DashboardTab.Changes:
                DrawChanges();
                break;
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.Space(5f);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            tab = (DashboardTab)GUILayout.Toolbar(
                (int)tab,
                TabLabels,
                EditorStyles.toolbarButton,
                GUILayout.Width(505f)
            );

            GUILayout.Space(8f);

            filter = (RowFilter)EditorGUILayout.Popup(
                (int)filter,
                FilterLabels,
                EditorStyles.toolbarPopup,
                GUILayout.Width(125f)
            );

            showDeltaPercent = GUILayout.Toggle(
                showDeltaPercent,
                "Δ %",
                EditorStyles.toolbarButton,
                GUILayout.Width(48f)
            );

            GUILayout.FlexibleSpace();

            GUILayout.Label("Search", GUILayout.Width(43f));
            search = GUILayout.TextField(search ?? string.Empty, EditorStyles.toolbarSearchField, GUILayout.Width(150f));

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                ScanLevelAssets();
                lastAssetHash = 0;
                RefreshData(true);
            }
        }
    }

    private void DrawLegend()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUILayout.Label("Pressure v2 compares threat intensity across mission modes using estimated exposure time. Hover PRESSURE for details.", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            DrawLegendBadge("ONBOARD", new Color(0.24f, 0.52f, 0.82f), 72f);
            DrawLegendBadge("OK", new Color(0.30f, 0.70f, 0.38f));
            DrawLegendBadge("CHECK", new Color(0.85f, 0.72f, 0.22f));
            DrawLegendBadge("WARNING", new Color(0.93f, 0.49f, 0.18f));
            DrawLegendBadge("SPIKE", new Color(0.85f, 0.25f, 0.22f));
        }
    }

    private void DrawOverview()
    {
        const float deltaWidth = 104f;
        float contentWidth = 1760f;
        scroll = EditorGUILayout.BeginScrollView(scroll, true, true);
        DrawHeaderRow(contentWidth,
            ("LEVEL", 64f), ("MODE", 112f), ("OBJECTIVE", 150f), ("★", 42f),
            ("PRESSURE", 82f), ("ENEMY", 70f), ("HAZARD", 70f), ("SUPPORT", 70f), ("EXPOSURE", 82f),
            ("Δ PREV", deltaWidth), ("Δ NEXT", deltaWidth),
            ("MISSION CHANGE", 175f), ("INTRODUCED", 195f), ("STATUS", 240f), ("NAME", 210f));

        foreach (LevelRow row in FilteredRows())
        {
            Rect rowRect = BeginTableRow(contentWidth, row);
            DrawLevelButton(row.config, 64f);
            DrawCell(ModeLabel(row.config), 112f);
            DrawCell(ObjectiveLabel(row.config), 150f);
            DrawCell(new string('★', Mathf.Clamp(row.config.missionDifficulty, 0, 5)), 42f, true);
            DrawPressureCell(row, 82f);
            DrawCell(row.pressureBreakdown.enemies.ToString("0.0"), 70f, true, "Enemy contribution before exposure scaling.");
            DrawCell(row.pressureBreakdown.hazards.ToString("0.0"), 70f, true, "Hazard / boss / obstacle contribution before exposure scaling.");
            DrawCell(row.pressureBreakdown.support > 0.01f ? $"-{row.pressureBreakdown.support:0.0}" : "—", 70f, true, "Estimated player-support credit subtracted from pressure.");
            DrawCell($"{row.pressureBreakdown.exposureSeconds:0.#}s", 82f, true, $"Estimated time exposed to this level's threat mix. Multiplier: ×{row.pressureBreakdown.exposureMultiplier:0.00}");
            DrawDeltaCell(row.previousDelta, row.previousDeltaPercent, row.config.levelNumber == 1, deltaWidth);
            DrawDeltaCell(row.nextDelta, row.nextDeltaPercent, row.config.levelNumber == 40, deltaWidth);
            DrawCell(string.IsNullOrEmpty(row.missionChange) ? "—" : row.missionChange, 175f);
            DrawCell(string.IsNullOrEmpty(row.newMechanics) ? "—" : row.newMechanics, 195f);
            DrawStatusCell(row, 240f);
            DrawCell(row.config.levelName, 210f);
            EndTableRow(rowRect, row.config);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawMechanics()
    {
        float contentWidth = 1360f;
        scroll = EditorGUILayout.BeginScrollView(scroll, true, true);
        DrawHeaderRow(contentWidth,
            ("LEVEL", 64f), ("DASH", 58f), ("CLONE", 62f), ("COMBO", 62f),
            ("NORMAL", 66f), ("GOLD", 58f), ("RARE", 58f),
            ("OBST.", 68f), ("ARMOR", 62f), ("SLOW", 58f),
            ("COIN RATE", 82f), ("MAX COINS", 78f), ("PLAYER SPD", 82f),
            ("INTRODUCED", 190f), ("STATUS", 235f));

        foreach (LevelRow row in FilteredRows())
        {
            LevelConfig c = row.config;
            Rect rowRect = BeginTableRow(contentWidth, row);
            DrawLevelButton(c, 64f);
            DrawBoolCell(c.dashEnabled, 58f);
            DrawBoolCell(c.cloneEnabled, 62f);
            DrawBoolCell(c.EffectiveComboEnabled, 62f);
            DrawBoolCell(c.UsesScore && c.normalCoinEnabled, 66f);
            DrawBoolCell(c.UsesScore && c.goldCoinEnabled, 58f);
            DrawBoolCell(c.UsesScore && c.rareCoinEnabled, 58f);
            DrawCell(GetObstacleCount(c).ToString(), 68f, true);
            DrawBoolCell(c.armorEnabled, 62f);
            DrawBoolCell(c.slowEnabled, 58f);
            DrawCell(c.UsesScore ? $"{c.coinSpawnInterval:0.00}s" : "—", 82f, true);
            DrawCell(c.UsesScore ? c.maxCoinCount.ToString() : "—", 78f, true);
            DrawCell(c.playerMoveSpeed.ToString("0.0"), 82f, true);
            DrawCell(string.IsNullOrEmpty(row.newMechanics) ? "—" : row.newMechanics, 190f);
            DrawStatusCell(row, 235f);
            EndTableRow(rowRect, c);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawEnemies()
    {
        float contentWidth = 1475f;
        scroll = EditorGUILayout.BeginScrollView(scroll, true, true);
        DrawHeaderRow(contentWidth,
            ("LEVEL", 64f),
            ("STALKER", 118f), ("SPAWN", 68f),
            ("PROJECTILE", 125f), ("SPAWN", 68f),
            ("HUNTER", 118f), ("SPAWN", 68f),
            ("BEACON", 115f), ("BOSS", 135f),
            ("AVG DANGER", 88f), ("PRESSURE", 82f), ("Δ PREV", 104f), ("STATUS", 235f));

        foreach (LevelRow row in FilteredRows())
        {
            LevelConfig c = row.config;
            Rect rowRect = BeginTableRow(contentWidth, row);
            DrawLevelButton(c, 64f);
            DrawThreatCell(c.normalEnemyCount, c.normalEnemyDanger, 118f);
            DrawCell(c.normalEnemyCount > 0 ? $"{c.normalEnemySpawnInterval:0.0}s" : "—", 68f, true);
            DrawThreatCell(c.projectileEnemyCount, c.projectileEnemyDanger, 125f);
            DrawCell(c.projectileEnemyCount > 0 ? $"{c.projectileEnemySpawnInterval:0.0}s" : "—", 68f, true);
            DrawThreatCell(c.hunterEnemyCount, c.hunterEnemyDanger, 118f);
            DrawCell(c.hunterEnemyCount > 0 ? $"{c.hunterEnemySpawnInterval:0.0}s" : "—", 68f, true);
            DrawThreatCell(c.beaconEnemyCount, c.beaconEnemyDanger, 115f);
            DrawCell(c.bossEnabled ? $"ON {DangerLevelUtility.GetShortLabel(c.bossDanger)}" : "—", 135f, true);
            DrawCell(c.GetActiveDangerAverage() > 0f ? c.GetActiveDangerAverage().ToString("0.00") : "—", 88f, true);
            DrawPressureCell(row, 82f);
            DrawDeltaCell(row.previousDelta, row.previousDeltaPercent, c.levelNumber == 1, 104f);
            DrawStatusCell(row, 235f);
            EndTableRow(rowRect, c);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawHazards()
    {
        float contentWidth = 1360f;
        scroll = EditorGUILayout.BeginScrollView(scroll, true, true);
        DrawHeaderRow(contentWidth,
            ("LEVEL", 64f), ("V-LASER", 105f), ("H-LASER", 105f), ("BOMB", 105f),
            ("OBST. MODE", 100f), ("OBSTACLES", 82f),
            ("ARMOR", 62f), ("SLOW", 58f), ("BOSS", 110f), ("BEACON", 105f),
            ("PRESSURE", 82f), ("Δ PREV", 104f), ("STATUS", 235f));

        foreach (LevelRow row in FilteredRows())
        {
            LevelConfig c = row.config;
            Rect rowRect = BeginTableRow(contentWidth, row);
            DrawLevelButton(c, 64f);
            DrawDangerToggleCell(c.verticalLaserEnabled, c.verticalLaserDanger, 105f);
            DrawDangerToggleCell(c.horizontalLaserEnabled, c.horizontalLaserDanger, 105f);
            DrawDangerToggleCell(c.bombTrapEnabled, c.bombDanger, 105f);
            DrawCell(c.obstacleSpawnMode.ToString(), 100f, true);
            DrawCell(GetObstacleCount(c).ToString(), 82f, true);
            DrawBoolCell(c.armorEnabled, 62f);
            DrawBoolCell(c.slowEnabled, 58f);
            DrawDangerToggleCell(c.bossEnabled, c.bossDanger, 110f);
            DrawThreatCell(c.beaconEnemyCount, c.beaconEnemyDanger, 105f);
            DrawPressureCell(row, 82f);
            DrawDeltaCell(row.previousDelta, row.previousDeltaPercent, c.levelNumber == 1, 104f);
            DrawStatusCell(row, 235f);
            EndTableRow(rowRect, c);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawChanges()
    {
        float contentWidth = 1450f;
        scroll = EditorGUILayout.BeginScrollView(scroll, true, true);
        DrawHeaderRow(contentWidth,
            ("LEVEL", 64f), ("MODE", 112f), ("PRESSURE", 82f), ("Δ PREV", 104f),
            ("MISSION CHANGE", 175f), ("INTRODUCED", 210f),
            ("CHANGES FROM PREVIOUS LEVEL", 485f), ("STATUS", 220f));

        foreach (LevelRow row in FilteredRows())
        {
            Rect rowRect = BeginTableRow(contentWidth, row);
            DrawLevelButton(row.config, 64f);
            DrawCell(ModeLabel(row.config), 112f);
            DrawPressureCell(row, 82f);
            DrawDeltaCell(row.previousDelta, row.previousDeltaPercent, row.config.levelNumber == 1, 104f);
            DrawCell(string.IsNullOrEmpty(row.missionChange) ? "—" : row.missionChange, 175f);
            DrawCell(string.IsNullOrEmpty(row.newMechanics) ? "—" : row.newMechanics, 210f);
            DrawCell(string.IsNullOrEmpty(row.changeSummary) ? "No tracked change" : row.changeSummary, 485f);
            DrawStatusCell(row, 220f);
            EndTableRow(rowRect, row.config);
        }

        EditorGUILayout.EndScrollView();
    }

    private IEnumerable<LevelRow> FilteredRows()
    {
        IEnumerable<LevelRow> result = rows;

        if (filter == RowFilter.WarningsOnly)
            result = result.Where(r => r.warningLevel != WarningLevel.None);
        else if (filter == RowFilter.ChangesOnly)
            result = result.Where(r => !string.IsNullOrEmpty(r.changeSummary));

        if (!string.IsNullOrWhiteSpace(search))
        {
            string needle = search.Trim();
            result = result.Where(r =>
                r.config.levelNumber.ToString().IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (r.config.levelName ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (r.newMechanics ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (r.missionChange ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (r.changeSummary ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (r.warning ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        return result;
    }

    private void DrawHeaderRow(float width, params (string label, float width)[] columns)
    {
        using (new EditorGUILayout.HorizontalScope(GUILayout.Width(width)))
        {
            foreach ((string label, float columnWidth) in columns)
                GUILayout.Label(label, headerStyle, GUILayout.Width(columnWidth), GUILayout.Height(25f));
        }
    }

    private Rect BeginTableRow(float width, LevelRow row)
    {
        Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Width(width), GUILayout.Height(24f));

        if (Event.current.type == EventType.Repaint)
        {
            Color background = GetRowColor(row.warningLevel, row.onboarding);
            EditorGUI.DrawRect(rect, background);
        }

        return rect;
    }

    private void EndTableRow(Rect rowRect, LevelConfig config)
    {
        EditorGUILayout.EndHorizontal();

        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.clickCount == 2 && rowRect.Contains(e.mousePosition))
        {
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            e.Use();
        }
    }

    private void DrawLevelButton(LevelConfig config, float width)
    {
        if (GUILayout.Button($"L{config.levelNumber:00}", levelButtonStyle, GUILayout.Width(width), GUILayout.Height(22f)))
        {
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }
    }

    private void DrawCell(string text, float width, bool centered = false, string tooltip = null)
    {
        GUILayout.Label(
            new GUIContent(string.IsNullOrEmpty(text) ? "—" : text, tooltip ?? string.Empty),
            centered ? centeredCellStyle : cellStyle,
            GUILayout.Width(width), GUILayout.Height(22f)
        );
    }

    private void DrawPressureCell(LevelRow row, float width)
    {
        DrawCell(
            row.pressure.ToString("0.0"),
            width,
            true,
            BuildPressureTooltip(row)
        );
    }

    private static string BuildPressureTooltip(LevelRow row)
    {
        PressureBreakdown p = row.pressureBreakdown;
        return
            $"Estimated Pressure v2\n" +
            $"Enemies: {p.enemies:0.0}\n" +
            $"Hazards / Boss / Obstacles: {p.hazards:0.0}\n" +
            $"Exposure: {p.exposureSeconds:0.#}s × {p.exposureMultiplier:0.00}\n" +
            $"Objective urgency: +{p.objective:0.0}\n" +
            $"Player support: -{p.support:0.0}\n" +
            $"Total: {p.total:0.0}";
    }

    private void DrawBoolCell(bool value, float width)
    {
        Color previous = GUI.color;
        GUI.color = value ? new Color(0.65f, 1f, 0.70f) : new Color(0.65f, 0.65f, 0.65f);
        DrawCell(value ? "ON" : "—", width, true);
        GUI.color = previous;
    }

    private void DrawThreatCell(int count, DangerLevel danger, float width)
    {
        DrawCell(count > 0 ? $"{count} × {DangerLevelUtility.GetShortLabel(danger)}" : "—", width, true);
    }

    private void DrawDangerToggleCell(bool enabled, DangerLevel danger, float width)
    {
        DrawCell(enabled ? $"ON · {DangerLevelUtility.GetShortLabel(danger)}" : "—", width, true);
    }

    private void DrawDeltaCell(float delta, float percent, bool unavailable, float width)
    {
        if (unavailable)
        {
            DrawCell("—", width, true);
            return;
        }

        Color previous = GUI.color;
        if (delta >= 14f)
            GUI.color = new Color(1f, 0.48f, 0.44f);
        else if (delta >= 9f)
            GUI.color = new Color(1f, 0.68f, 0.30f);
        else if (delta > 0.1f)
            GUI.color = new Color(1f, 0.88f, 0.45f);
        else if (delta < -0.1f)
            GUI.color = new Color(0.62f, 0.85f, 1f);

        string text = showDeltaPercent
            ? $"{delta:+0.0;-0.0;0.0} ({percent:+0;-0;0}%)"
            : $"{delta:+0.0;-0.0;0.0}";

        DrawCell(text, width, true, $"Pressure change: {delta:+0.0;-0.0;0.0} ({percent:+0.0;-0.0;0.0}%)");
        GUI.color = previous;
    }

    private void DrawStatusCell(LevelRow row, float width)
    {
        string text;
        switch (row.warningLevel)
        {
            case WarningLevel.Spike:
                text = "SPIKE · " + row.warning;
                break;
            case WarningLevel.Warning:
                text = "WARNING · " + row.warning;
                break;
            case WarningLevel.Notice:
                text = "CHECK · " + row.warning;
                break;
            default:
                text = row.onboarding
                    ? "ONBOARDING · " + (string.IsNullOrEmpty(row.newMechanics) ? "new mechanic" : row.newMechanics)
                    : "OK";
                break;
        }

        DrawCell(text, width, false, row.warningLevel == WarningLevel.None
            ? (row.onboarding ? "Planned mechanic introduction without a balance warning." : "No automatic balance warning.")
            : row.warning);
    }

    private void DrawLegendBadge(string text, Color color, float width = 58f)
    {
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = color;
        GUILayout.Label(text, miniBadgeStyle, GUILayout.Width(width), GUILayout.Height(18f));
        GUI.backgroundColor = old;
    }

    private void EnsureStyles()
    {
        if (headerStyle != null)
            return;

        headerStyle = new GUIStyle(EditorStyles.toolbarButton)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 10,
            clipping = TextClipping.Clip
        };

        cellStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(5, 4, 0, 0),
            clipping = TextClipping.Clip,
            fontSize = 10
        };

        centeredCellStyle = new GUIStyle(cellStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(1, 1, 0, 0)
        };

        levelButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        miniBadgeStyle = new GUIStyle(EditorStyles.miniButton)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 9
        };
    }

    private static Color GetRowColor(WarningLevel warning, bool onboarding)
    {
        bool pro = EditorGUIUtility.isProSkin;

        switch (warning)
        {
            case WarningLevel.Spike:
                return pro ? new Color(0.32f, 0.10f, 0.09f, 0.72f) : new Color(1f, 0.82f, 0.80f, 0.72f);
            case WarningLevel.Warning:
                return pro ? new Color(0.30f, 0.18f, 0.07f, 0.68f) : new Color(1f, 0.90f, 0.73f, 0.68f);
            case WarningLevel.Notice:
                return pro ? new Color(0.25f, 0.23f, 0.08f, 0.55f) : new Color(1f, 0.96f, 0.78f, 0.60f);
            default:
                if (onboarding)
                    return pro ? new Color(0.08f, 0.18f, 0.30f, 0.58f) : new Color(0.80f, 0.90f, 1f, 0.68f);

                return pro ? new Color(0.17f, 0.17f, 0.17f, 0.42f) : new Color(0.94f, 0.94f, 0.94f, 0.65f);
        }
    }

    private static string ModeLabel(LevelConfig c)
    {
        switch (c.winCondition)
        {
            case WinConditionType.ReachScore:
                return "Reach Score";
            case WinConditionType.SurviveTime:
                return "Survive Time";
            case WinConditionType.ReachScoreWithinTime:
                return "Timed Score";
            default:
                return c.winCondition.ToString();
        }
    }

    private static string ObjectiveLabel(LevelConfig c)
    {
        switch (c.winCondition)
        {
            case WinConditionType.ReachScore:
                return $"Score {c.winScore}";
            case WinConditionType.SurviveTime:
                return $"Survive {c.timeLimit:0.#}s";
            case WinConditionType.ReachScoreWithinTime:
                return $"{c.winScore} pts / {c.timeLimit:0.#}s";
            default:
                return "—";
        }
    }

    private static int GetObstacleCount(LevelConfig c)
    {
        if (c.levelObstacles == null || c.levelObstacles.Length == 0)
            return 0;

        int enabled = 0;
        foreach (LevelObstacleOption option in c.levelObstacles)
        {
            if (option != null && option.enabled && option.prefab != null)
                enabled++;
        }

        if (c.obstacleSpawnMode == ObstacleSpawnMode.Random)
            return Mathf.Min(enabled, Mathf.Max(0, c.randomObstacleCount));

        return enabled;
    }

    private static PressureBreakdown CalculatePressureBreakdown(LevelConfig c)
    {
        PressureBreakdown result = new PressureBreakdown();
        if (c == null)
            return result;

        result.enemies += EnemyPressure(c.normalEnemyCount, c.normalEnemyDanger, c.normalEnemySpawnInterval, 1.40f);
        result.enemies += EnemyPressure(c.projectileEnemyCount, c.projectileEnemyDanger, c.projectileEnemySpawnInterval, 2.05f);
        result.enemies += EnemyPressure(c.hunterEnemyCount, c.hunterEnemyDanger, c.hunterEnemySpawnInterval, 2.25f);
        result.enemies += EnemyPressure(c.beaconEnemyCount, c.beaconEnemyDanger, Mathf.Max(1f, c.beaconMinSpawnTime), 1.75f);

        if (c.bossEnabled)
            result.hazards += 5.5f + (int)DangerLevelUtility.Sanitize(c.bossDanger) * 1.65f;

        if (c.verticalLaserEnabled)
            result.hazards += 1.8f + (int)DangerLevelUtility.Sanitize(c.verticalLaserDanger) * 0.85f;

        if (c.horizontalLaserEnabled)
            result.hazards += 1.8f + (int)DangerLevelUtility.Sanitize(c.horizontalLaserDanger) * 0.85f;

        if (c.bombTrapEnabled)
            result.hazards += 2.1f + (int)DangerLevelUtility.Sanitize(c.bombDanger) * 1.00f;

        result.hazards += GetObstacleCount(c) * 0.42f;

        result.exposureSeconds = EstimateExposureSeconds(c);
        result.exposureMultiplier = Mathf.Lerp(
            0.92f,
            1.20f,
            Mathf.InverseLerp(25f, 75f, result.exposureSeconds)
        );

        if (c.dashEnabled) result.support += 0.50f;
        if (c.cloneEnabled) result.support += 1.60f;
        if (c.armorEnabled) result.support += 2.10f;
        if (c.slowEnabled) result.support += 2.20f;
        if (c.EffectiveComboEnabled) result.support += 0.55f;

        result.objective = CalculateObjectivePressure(c);

        float raw =
            (result.enemies + result.hazards) * result.exposureMultiplier +
            result.objective -
            result.support;

        // Late-game configs naturally stack many systems. Compress only the high end so a single
        // extra mechanic does not produce an exaggerated numerical jump while ordering is preserved.
        if (raw > 50f)
            raw = 50f + (raw - 50f) * 0.72f;

        result.total = Mathf.Max(0f, raw);
        return result;
    }

    private static float EnemyPressure(int count, DangerLevel danger, float spawnInterval, float typeWeight)
    {
        if (count <= 0)
            return 0f;

        float dangerWeight = 1f + ((int)DangerLevelUtility.Sanitize(danger) - 1) * 0.28f;
        float cadenceWeight = Mathf.Clamp(
            1f + (4.5f - Mathf.Max(0.25f, spawnInterval)) * 0.04f,
            0.88f,
            1.18f
        );

        return count * typeWeight * dangerWeight * cadenceWeight;
    }

    private static float EstimateExposureSeconds(LevelConfig c)
    {
        if (c.winCondition == WinConditionType.SurviveTime)
            return Mathf.Max(1f, c.timeLimit);

        float scoreCompletion = EstimateScoreCompletionSeconds(c);

        if (c.winCondition == WinConditionType.ReachScoreWithinTime)
            return Mathf.Min(Mathf.Max(1f, c.timeLimit), Mathf.Max(12f, scoreCompletion));

        return Mathf.Clamp(scoreCompletion, 12f, 95f);
    }

    private static float EstimateScoreCompletionSeconds(LevelConfig c)
    {
        if (c == null || !c.UsesScore)
            return 0f;

        float expectedCoinValue = ExpectedCoinValue(c);
        float theoreticalScoreRate = expectedCoinValue / Mathf.Max(0.15f, c.coinSpawnInterval);

        // Not every spawned coin is collected immediately. A shared collection-efficiency factor
        // lets Reach Score and Timed Score live on the same exposure scale as Survive Time.
        const float expectedCollectionEfficiency = 0.78f;
        float expectedScoreRate = theoreticalScoreRate * expectedCollectionEfficiency;

        return c.winScore / Mathf.Max(0.10f, expectedScoreRate);
    }

    private static float CalculateObjectivePressure(LevelConfig c)
    {
        if (c == null || c.winCondition != WinConditionType.ReachScoreWithinTime)
            return 0f;

        float estimatedCompletion = EstimateScoreCompletionSeconds(c);
        float urgency = estimatedCompletion / Mathf.Max(1f, c.timeLimit);

        // Timed score becomes meaningfully harder only when the expected completion time starts
        // consuming most of the available clock. Keep this contribution modest and capped.
        return Mathf.Clamp((urgency - 0.60f) * 5f, 0f, 3.5f);
    }

    private static float ExpectedCoinValue(LevelConfig c)
    {
        if (!c.UsesScore)
            return 1f;

        float weight = 0f;
        float value = 0f;

        if (c.normalCoinEnabled)
        {
            weight += Mathf.Max(0f, c.normalCoinChance);
            value += Mathf.Max(0f, c.normalCoinChance) * Mathf.Max(1, c.normalCoinValue);
        }

        if (c.goldCoinEnabled)
        {
            weight += Mathf.Max(0f, c.goldCoinChance);
            value += Mathf.Max(0f, c.goldCoinChance) * Mathf.Max(1, c.goldCoinValue);
        }

        if (c.rareCoinEnabled)
        {
            weight += Mathf.Max(0f, c.rareCoinChance);
            value += Mathf.Max(0f, c.rareCoinChance) * Mathf.Max(1, c.rareCoinValue);
        }

        return weight > 0.01f ? value / weight : 1f;
    }

    private static string BuildIntroducedMechanics(LevelConfig previous, LevelConfig current)
    {
        if (current == null)
            return "";

        List<string> result = new List<string>();
        LevelMechanicProgression progression = current.mechanicProgression;

        if (progression != null)
        {
            AddProgressionIntroduction(result, progression.dash, "Dash");
            AddProgressionIntroduction(result, progression.clone, "Clone");
            AddProgressionIntroduction(result, progression.combo, "Combo");
            AddProgressionIntroduction(result, progression.normalCoin, "Normal Coin");
            AddProgressionIntroduction(result, progression.goldCoin, "Gold Coin");
            AddProgressionIntroduction(result, progression.rareCoin, "Rare Coin");
            AddProgressionIntroduction(result, progression.staticObstacles, "Obstacles");
            AddProgressionIntroduction(result, progression.normalEnemy, "Stalker");
            AddProgressionIntroduction(result, progression.projectileEnemy, "Projectile");
            AddProgressionIntroduction(result, progression.hunterEnemy, "Hunter");
            AddProgressionIntroduction(result, progression.boss, "Boss");
            AddProgressionIntroduction(result, progression.beaconEnemy, "Beacon");
            AddProgressionIntroduction(result, progression.armor, "Armor");
            AddProgressionIntroduction(result, progression.slow, "Slow");
            AddProgressionIntroduction(result, progression.verticalLaser, "V-Laser");
            AddProgressionIntroduction(result, progression.horizontalLaser, "H-Laser");
            AddProgressionIntroduction(result, progression.spaceBomb, "Bomb");
        }
        else
        {
            // Compatibility fallback for a hand-created config that has no progression metadata.
            AddNew(result, previous == null || !previous.dashEnabled, current.dashEnabled, "Dash");
            AddNew(result, previous == null || !previous.cloneEnabled, current.cloneEnabled, "Clone");
            AddNew(result, previous == null || !previous.EffectiveComboEnabled, current.EffectiveComboEnabled, "Combo");
            AddNew(result, previous == null || !HasActiveObstacles(previous), HasActiveObstacles(current), "Obstacles");
            AddNew(result, previous == null || previous.normalEnemyCount <= 0, current.normalEnemyCount > 0, "Stalker");
            AddNew(result, previous == null || previous.projectileEnemyCount <= 0, current.projectileEnemyCount > 0, "Projectile");
            AddNew(result, previous == null || previous.hunterEnemyCount <= 0, current.hunterEnemyCount > 0, "Hunter");
            AddNew(result, previous == null || !previous.bossEnabled, current.bossEnabled, "Boss");
            AddNew(result, previous == null || previous.beaconEnemyCount <= 0, current.beaconEnemyCount > 0, "Beacon");
            AddNew(result, previous == null || !previous.armorEnabled, current.armorEnabled, "Armor");
            AddNew(result, previous == null || !previous.slowEnabled, current.slowEnabled, "Slow");
            AddNew(result, previous == null || !previous.verticalLaserEnabled, current.verticalLaserEnabled, "V-Laser");
            AddNew(result, previous == null || !previous.horizontalLaserEnabled, current.horizontalLaserEnabled, "H-Laser");
            AddNew(result, previous == null || !previous.bombTrapEnabled, current.bombTrapEnabled, "Bomb");
        }

        return string.Join(", ", result);
    }

    private static string BuildMissionChange(LevelConfig previous, LevelConfig current)
    {
        if (current == null)
            return "";

        if (previous == null)
            return "Start · " + ModeLabel(current);

        if (previous.winCondition == current.winCondition)
            return "";

        return $"{ModeLabel(previous)} → {ModeLabel(current)}";
    }

    private static void AddProgressionIntroduction(
        List<string> result,
        MechanicProgressionStatus status,
        string label)
    {
        if (status == MechanicProgressionStatus.IntroducedHere)
            result.Add(label);
    }

    private static void AddNew(List<string> result, bool wasInactive, bool isActive, string label)
    {
        if (wasInactive && isActive)
            result.Add(label);
    }

    private static string BuildChangeSummary(LevelConfig previous, LevelConfig current)
    {
        if (previous == null || current == null)
            return "Starting configuration";

        List<string> changes = new List<string>();

        if (previous.winCondition != current.winCondition)
            changes.Add($"Mode {ModeLabel(previous)} → {ModeLabel(current)}");

        if (current.UsesScore && (!previous.UsesScore || previous.winScore != current.winScore))
            changes.Add($"Score {previous.winScore} → {current.winScore}");

        if (current.UsesTime && (!previous.UsesTime || !Approximately(previous.timeLimit, current.timeLimit)))
            changes.Add($"Time {previous.timeLimit:0.#}s → {current.timeLimit:0.#}s");

        AddToggleChange(changes, "Dash", previous.dashEnabled, current.dashEnabled);
        AddToggleChange(changes, "Clone", previous.cloneEnabled, current.cloneEnabled);
        AddToggleChange(changes, "Combo", previous.EffectiveComboEnabled, current.EffectiveComboEnabled);
        AddToggleChange(changes, "Armor", previous.armorEnabled, current.armorEnabled);
        AddToggleChange(changes, "Slow", previous.slowEnabled, current.slowEnabled);
        AddToggleChange(changes, "V-Laser", previous.verticalLaserEnabled, current.verticalLaserEnabled);
        AddToggleChange(changes, "H-Laser", previous.horizontalLaserEnabled, current.horizontalLaserEnabled);
        AddToggleChange(changes, "Bomb", previous.bombTrapEnabled, current.bombTrapEnabled);
        AddToggleChange(changes, "Boss", previous.bossEnabled, current.bossEnabled);

        AddThreatChange(changes, "Stalker", previous.normalEnemyCount, previous.normalEnemyDanger, current.normalEnemyCount, current.normalEnemyDanger);
        AddThreatChange(changes, "Projectile", previous.projectileEnemyCount, previous.projectileEnemyDanger, current.projectileEnemyCount, current.projectileEnemyDanger);
        AddThreatChange(changes, "Hunter", previous.hunterEnemyCount, previous.hunterEnemyDanger, current.hunterEnemyCount, current.hunterEnemyDanger);
        AddThreatChange(changes, "Beacon", previous.beaconEnemyCount, previous.beaconEnemyDanger, current.beaconEnemyCount, current.beaconEnemyDanger);

        AddDangerChange(changes, "Boss", previous.bossEnabled, previous.bossDanger, current.bossEnabled, current.bossDanger);
        AddDangerChange(changes, "V-Laser", previous.verticalLaserEnabled, previous.verticalLaserDanger, current.verticalLaserEnabled, current.verticalLaserDanger);
        AddDangerChange(changes, "H-Laser", previous.horizontalLaserEnabled, previous.horizontalLaserDanger, current.horizontalLaserEnabled, current.horizontalLaserDanger);
        AddDangerChange(changes, "Bomb", previous.bombTrapEnabled, previous.bombDanger, current.bombTrapEnabled, current.bombDanger);

        int prevObstacles = GetObstacleCount(previous);
        int currentObstacles = GetObstacleCount(current);
        if (prevObstacles != currentObstacles || previous.obstacleSpawnMode != current.obstacleSpawnMode)
            changes.Add($"Obstacles {prevObstacles}/{previous.obstacleSpawnMode} → {currentObstacles}/{current.obstacleSpawnMode}");

        if (current.UsesScore && previous.UsesScore && !Approximately(previous.coinSpawnInterval, current.coinSpawnInterval))
            changes.Add($"Coin rate {previous.coinSpawnInterval:0.00}s → {current.coinSpawnInterval:0.00}s");

        if (!Approximately(previous.playerMoveSpeed, current.playerMoveSpeed))
            changes.Add($"Player speed {previous.playerMoveSpeed:0.0} → {current.playerMoveSpeed:0.0}");

        if (previous.missionDifficulty != current.missionDifficulty)
            changes.Add($"Difficulty ★{previous.missionDifficulty} → ★{current.missionDifficulty}");

        return string.Join("  •  ", changes);
    }

    private static void AddToggleChange(List<string> changes, string label, bool previous, bool current)
    {
        if (previous != current)
            changes.Add($"{label} {(current ? "ON" : "OFF")}");
    }

    private static void AddThreatChange(
        List<string> changes,
        string label,
        int previousCount,
        DangerLevel previousDanger,
        int currentCount,
        DangerLevel currentDanger)
    {
        if (previousCount == currentCount && (previousCount <= 0 || previousDanger == currentDanger))
            return;

        string before = previousCount > 0 ? $"{previousCount}×{DangerLevelUtility.GetShortLabel(previousDanger)}" : "OFF";
        string after = currentCount > 0 ? $"{currentCount}×{DangerLevelUtility.GetShortLabel(currentDanger)}" : "OFF";
        changes.Add($"{label} {before} → {after}");
    }

    private static void AddDangerChange(
        List<string> changes,
        string label,
        bool previousEnabled,
        DangerLevel previousDanger,
        bool currentEnabled,
        DangerLevel currentDanger)
    {
        if (!previousEnabled || !currentEnabled || previousDanger == currentDanger)
            return;

        changes.Add($"{label} {DangerLevelUtility.GetShortLabel(previousDanger)} → {DangerLevelUtility.GetShortLabel(currentDanger)}");
    }

    private static bool HasHighDangerFirstAppearance(LevelConfig previous, LevelConfig current)
    {
        if (previous == null || current == null)
            return false;

        LevelMechanicProgression p = current.mechanicProgression;
        if (p != null)
        {
            return IsIntroducedHighThreat(p.normalEnemy, current.normalEnemyCount > 0, current.normalEnemyDanger) ||
                   IsIntroducedHighThreat(p.projectileEnemy, current.projectileEnemyCount > 0, current.projectileEnemyDanger) ||
                   IsIntroducedHighThreat(p.hunterEnemy, current.hunterEnemyCount > 0, current.hunterEnemyDanger) ||
                   IsIntroducedHighThreat(p.beaconEnemy, current.beaconEnemyCount > 0, current.beaconEnemyDanger) ||
                   IsIntroducedHighThreat(p.boss, current.bossEnabled, current.bossDanger) ||
                   IsIntroducedHighThreat(p.verticalLaser, current.verticalLaserEnabled, current.verticalLaserDanger) ||
                   IsIntroducedHighThreat(p.horizontalLaser, current.horizontalLaserEnabled, current.horizontalLaserDanger) ||
                   IsIntroducedHighThreat(p.spaceBomb, current.bombTrapEnabled, current.bombDanger);
        }

        return IsNewHighThreat(previous.normalEnemyCount > 0, current.normalEnemyCount > 0, current.normalEnemyDanger) ||
               IsNewHighThreat(previous.projectileEnemyCount > 0, current.projectileEnemyCount > 0, current.projectileEnemyDanger) ||
               IsNewHighThreat(previous.hunterEnemyCount > 0, current.hunterEnemyCount > 0, current.hunterEnemyDanger) ||
               IsNewHighThreat(previous.beaconEnemyCount > 0, current.beaconEnemyCount > 0, current.beaconEnemyDanger) ||
               IsNewHighThreat(previous.bossEnabled, current.bossEnabled, current.bossDanger) ||
               IsNewHighThreat(previous.verticalLaserEnabled, current.verticalLaserEnabled, current.verticalLaserDanger) ||
               IsNewHighThreat(previous.horizontalLaserEnabled, current.horizontalLaserEnabled, current.horizontalLaserDanger) ||
               IsNewHighThreat(previous.bombTrapEnabled, current.bombTrapEnabled, current.bombDanger);
    }

    private static bool IsIntroducedHighThreat(
        MechanicProgressionStatus status,
        bool active,
        DangerLevel danger)
    {
        return status == MechanicProgressionStatus.IntroducedHere &&
               active &&
               (int)DangerLevelUtility.Sanitize(danger) >= 4;
    }

    private static bool IsNewHighThreat(bool previousActive, bool currentActive, DangerLevel danger)
    {
        return !previousActive && currentActive && (int)DangerLevelUtility.Sanitize(danger) >= 4;
    }

    private static bool HasActiveObstacles(LevelConfig c)
    {
        return GetObstacleCount(c) > 0;
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.001f;
    }

    private static int CountCsvItems(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
#endif
