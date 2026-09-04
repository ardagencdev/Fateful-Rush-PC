using UnityEngine;

public enum GameLaunchMode
{
    DevRoom,
    Mission
}

public static class SelectedLevelData
{
    public static LevelConfig SelectedLevel { get; private set; }

    public static GameLaunchMode LaunchMode { get; private set; } =
        GameLaunchMode.DevRoom;

    public static bool IsLevelMode =>
        LaunchMode == GameLaunchMode.Mission;

    // Eski scriptlerle uyumluluk
    public static LevelConfig selectedLevel
    {
        get => SelectedLevel;
        set => SelectedLevel = value;
    }

    public static GameLaunchMode launchMode
    {
        get => LaunchMode;
        set => LaunchMode = value;
    }

    public static bool isLevelMode =>
        IsLevelMode;

    public static void SetDevRoom(LevelConfig config)
    {
        if (config == null)
        {
            Debug.LogWarning(
                "[SelectedLevelData] Dev Room için boş LevelConfig gönderildi."
            );
        }

        LaunchMode = GameLaunchMode.DevRoom;
        SelectedLevel = config;
    }

    public static void SetMission(LevelConfig config)
    {
        if (config == null)
        {
            Debug.LogWarning(
                "[SelectedLevelData] Mission için boş LevelConfig gönderildi."
            );
        }

        LaunchMode = GameLaunchMode.Mission;
        SelectedLevel = config;
    }

    public static void Clear()
    {
        SelectedLevel = null;
        LaunchMode = GameLaunchMode.DevRoom;
    }
}