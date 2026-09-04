#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FatefulRushObstacleBalanceApplier
{
    private static readonly Dictionary<int, string[]> Plan = new Dictionary<int, string[]>
    {
        { 1, Array.Empty<string>() }, { 2, Array.Empty<string>() }, { 3, Array.Empty<string>() },
        { 4, new[] { "Asteroid" } },
        { 5, new[] { "Saturnish", "WideAsteroid" } },
        { 6, new[] { "Satellite", "Asteroid" } },
        { 7, new[] { "SpearAsteroid", "Saturnish" } },
        { 8, new[] { "BlackHole", "WideAsteroid" } },
        { 9, new[] { "SUN", "Satellite" } },
        { 10, new[] { "BlueWall", "Asteroid", "Saturnish" } },
        { 11, new[] { "WideAsteroid", "SpearAsteroid" } },
        { 12, new[] { "BlackHole", "Satellite", "SUN" } },
        { 13, new[] { "BlueWall", "Saturnish", "Asteroid" } },
        { 14, new[] { "WideAsteroid", "SpearAsteroid", "Satellite" } },
        { 15, new[] { "BlackHole", "SUN", "BlueWall" } },
        { 16, new[] { "Asteroid", "Saturnish", "Satellite", "WideAsteroid" } },
        { 17, new[] { "BlackHole", "SpearAsteroid", "BlueWall" } },
        { 18, new[] { "SpearAsteroid", "SUN", "WideAsteroid" } },
        { 19, new[] { "BlackHole", "Satellite", "Saturnish" } },
        { 20, new[] { "Asteroid", "BlueWall", "SUN", "WideAsteroid" } },
        { 21, new[] { "Wreck", "Satellite", "WideAsteroid" } },
        { 22, new[] { "WreckedAirlock", "Asteroid", "BlueWall", "Saturnish" } },
        { 23, new[] { "WreckedThing", "SpearAsteroid", "WideAsteroid" } },
        { 24, new[] { "BrokenSatellite", "BlackHole", "SUN", "Wreck" } },
        { 25, new[] { "WreckedObservation", "Satellite", "SpearAsteroid", "WideAsteroid" } },
        { 26, new[] { "WreckedAirlock", "Asteroid", "BlueWall", "Wreck" } },
        { 27, new[] { "WreckedThing", "BrokenSatellite", "Saturnish", "SpearAsteroid" } },
        { 28, new[] { "WreckedObservation", "BlueWall", "Satellite" } },
        { 29, new[] { "Wreck", "BlackHole", "BrokenSatellite", "SUN" } },
        { 30, new[] { "WreckedThing", "WreckedObservation", "WideAsteroid" } },
        { 31, new[] { "WreckedAirlock", "Asteroid", "BlackHole", "Wreck" } },
        { 32, new[] { "WreckedThing", "BrokenSatellite", "Satellite", "SpearAsteroid" } },
        { 33, new[] { "WreckedObservation", "WreckedAirlock", "BlueWall", "Saturnish", "WideAsteroid" } },
        { 34, new[] { "Wreck", "BrokenSatellite", "Asteroid", "SUN" } },
        { 35, new[] { "WreckedThing", "WreckedObservation", "SpearAsteroid", "WideAsteroid" } },
        { 36, new[] { "Wreck", "BrokenSatellite", "BlackHole", "Saturnish" } },
        { 37, new[] { "WreckedAirlock", "WreckedObservation", "WreckedThing", "Asteroid" } },
        { 38, new[] { "Wreck", "WreckedObservation", "BrokenSatellite", "BlueWall", "Satellite" } },
        { 39, new[] { "WreckedAirlock", "WreckedThing", "Saturnish", "SpearAsteroid" } },
        { 40, new[] { "Wreck", "WreckedObservation", "WreckedThing", "BrokenSatellite", "BlackHole" } }
    };

    [MenuItem("Tools/Fateful Rush/Apply Final 40-Level Obstacle Balance")]
    public static void ApplyFinalObstacleBalance()
    {
        List<LevelConfig> levels = LoadCampaignLevels();
        int changed = 0;

        foreach (LevelConfig level in levels)
        {
            HashSet<string> desired = new HashSet<string>(Plan[level.levelNumber], StringComparer.OrdinalIgnoreCase);
            Undo.RecordObject(level, "Apply Fateful Rush obstacle balance");

            if (level.levelObstacles != null)
            {
                foreach (LevelObstacleOption option in level.levelObstacles)
                {
                    if (option == null || option.prefab == null)
                        continue;
                    option.enabled = desired.Contains(option.prefab.name.Trim());
                }
            }

            level.obstacleSpawnMode = ObstacleSpawnMode.Fixed;
            level.randomObstacleCount = Mathf.Min(5, desired.Count);
            EditorUtility.SetDirty(level);
            changed++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Fateful Rush final obstacle plan applied to {changed} levels. Exact campaign sets restored; hard cap is 5.");
    }

    [MenuItem("Tools/Fateful Rush/Validate Final 40-Level Obstacle Balance")]
    public static void ValidateFinalObstacleBalance()
    {
        int issues = 0;
        HashSet<string> darkNames = new HashSet<string>(
            new[] { "BrokenSatellite", "Wreck", "WreckedAirlock", "WreckedObservation", "WreckedThing" },
            StringComparer.OrdinalIgnoreCase);

        foreach (LevelConfig level in LoadCampaignLevels())
        {
            HashSet<string> expected = new HashSet<string>(Plan[level.levelNumber], StringComparer.OrdinalIgnoreCase);
            HashSet<string> actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (level.levelObstacles != null)
            {
                foreach (LevelObstacleOption option in level.levelObstacles)
                    if (option != null && option.prefab != null && option.enabled)
                        actual.Add(option.prefab.name.Trim());
            }

            if (!expected.SetEquals(actual) || level.obstacleSpawnMode != ObstacleSpawnMode.Fixed)
            {
                issues++;
                Debug.LogError($"Level {level.levelNumber}: obstacle set differs from final plan.", level);
            }

            if (actual.Count > 5)
            {
                issues++;
                Debug.LogError($"Level {level.levelNumber}: {actual.Count} obstacles exceed the cap.", level);
            }

            if (level.levelNumber <= 20 && actual.Overlaps(darkNames))
            {
                issues++;
                Debug.LogError($"Level {level.levelNumber}: wrecked obstacle appears before Level 21.", level);
            }
        }

        if (issues == 0)
            Debug.Log("Fateful Rush obstacle validation passed: exact 40-level sets, early clean atmosphere and 5-obstacle cap confirmed.");
        else
            Debug.LogWarning($"Fateful Rush obstacle validation finished with {issues} issue(s).");
    }

    private static List<LevelConfig> LoadCampaignLevels()
    {
        return AssetDatabase.FindAssets("t:LevelConfig")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<LevelConfig>)
            .Where(level => level != null && level.levelNumber >= 1 && level.levelNumber <= 40)
            .GroupBy(level => level.levelNumber)
            .Select(group => group.First())
            .OrderBy(level => level.levelNumber)
            .ToList();
    }
}
#endif
