using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatsPanelUI : MonoBehaviour
{
    public bool IsOpen => statsPanel != null && statsPanel.activeInHierarchy;

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private GameObject resetConfirmationPanel;

    [Header("Fade")]
    [SerializeField] private UIPanelFadeSwitcher fadeSwitcher;

    [Header("Stats Scroll")]
    [SerializeField] private ScrollRect statsScrollRect;


    [Header("Text")]
    [SerializeField] private TextMeshProUGUI statsText;

    private readonly StringBuilder builder = new StringBuilder(4096);

    private void Awake()
    {
        if (fadeSwitcher == null)
            fadeSwitcher = GetComponent<UIPanelFadeSwitcher>();

        ConfigurePcScrollRect();

        if (resetConfirmationPanel != null)
        {
            if (fadeSwitcher != null)
                fadeSwitcher.SetInstant(resetConfirmationPanel, false);
            else
                resetConfirmationPanel.SetActive(false);
        }
    }

    public void OpenStats()
    {
        if (mainMenuPanel == null || statsPanel == null)
            return;

        HideResetConfirmation();
        RefreshStats();

        MainMenuStarColorRandomizer.Instance?.ShowStatsColor();

        Switch(mainMenuPanel, statsPanel);
        ResetScrollToTop();
    }

    public void CloseStats()
    {
        if (mainMenuPanel == null || statsPanel == null)
            return;

        HideResetConfirmation();
        MainMenuStarColorRandomizer.Instance?.ShowMainMenuColor();
        Switch(statsPanel, mainMenuPanel);
    }

    public bool HandleEscapeBack()
    {
        if (!IsOpen)
            return false;

        if (resetConfirmationPanel != null &&
            resetConfirmationPanel.activeInHierarchy)
        {
            HideResetConfirmation();
            return true;
        }

        CloseStats();
        return true;
    }

    public void ShowResetConfirmation()
    {
        if (resetConfirmationPanel == null)
            return;

        if (fadeSwitcher != null)
        {
            fadeSwitcher.ShowPanel(resetConfirmationPanel);
            return;
        }

        resetConfirmationPanel.SetActive(true);
    }

    public void HideResetConfirmation()
    {
        if (resetConfirmationPanel == null)
            return;

        if (fadeSwitcher != null && resetConfirmationPanel.activeSelf)
        {
            fadeSwitcher.HidePanel(resetConfirmationPanel);
            return;
        }

        resetConfirmationPanel.SetActive(false);
    }

    public void ConfirmResetStats()
    {
        StatsManager.ResetAllStats();

        RefreshStats();
        HideResetConfirmation();
        ResetScrollToTop();
    }

    private void Switch(GameObject fromPanel, GameObject toPanel)
    {
        if (fadeSwitcher != null)
        {
            fadeSwitcher.SwitchPanel(fromPanel, toPanel);
            return;
        }

        if (fromPanel != null)
            fromPanel.SetActive(false);

        if (toPanel != null)
            toPanel.SetActive(true);
    }

    private void ConfigurePcScrollRect()
    {
        if (statsScrollRect == null)
            return;

        statsScrollRect.vertical = true;
        statsScrollRect.scrollSensitivity =
            Mathf.Max(statsScrollRect.scrollSensitivity, 18f);

        // Preserve the game's intentional clean UI: no visible scrollbar,
        // but mouse-wheel and drag scrolling remain fully active on PC.
        Scrollbar verticalScrollbar = statsScrollRect.verticalScrollbar;
        statsScrollRect.verticalScrollbar = null;
        if (verticalScrollbar != null)
            verticalScrollbar.gameObject.SetActive(false);
    }

    private void RefreshStats()
    {
        if (statsText == null)
            return;

        builder.Clear();

        AppendGeneral();
        AppendProgression();
        AppendModes();
        AppendPerformance();
        AppendNearMiss();
        AppendCollectibles();
        AppendAbilities();
        AppendDangerMastery();
        AppendDeathAnalysis();
        AppendBestTimes();

        statsText.text = builder.ToString().TrimEnd();
    }

    private void AppendGeneral()
    {
        int runs = StatsManager.GetTotalRuns();
        int wins = StatsManager.GetTotalWins();
        int deaths = StatsManager.GetTotalDeaths();

        builder.AppendLine("GENERAL");
        AppendStat("Total Runs", runs);
        AppendStat("Total Wins", wins);
        AppendStat("Total Deaths", deaths);
        AppendStat("Win Rate", FormatPercent(wins, runs));
        AppendStat("Current Win Streak", StatsManager.GetCurrentWinStreak());
        AppendStat("Best Win Streak", StatsManager.GetBestWinStreak());
        AppendStat("Total Play Time", FormatTime(StatsManager.GetTotalPlayTime()));
        AppendStat("Average Run Time", FormatTime(StatsManager.GetAverageRunTime()));
        AppendStat("Longest Run", FormatTime(StatsManager.GetLongestRunTime()));
        AppendSpacer();
    }

    private void AppendProgression()
    {
        int completed = StatsManager.GetCompletedLevelCount();
        int highest = StatsManager.GetHighestCompletedLevel();
        float completion = completed / (float)StatsManager.LastLevelNumber * 100f;

        builder.AppendLine("PROGRESSION");
        AppendStat("Levels Completed", $"{completed} / {StatsManager.LastLevelNumber}");
        AppendStat("Completion", $"{completion:F1}%");
        AppendStat("Highest Level Completed", highest > 0 ? highest.ToString() : "-");

        PlayerSkinCatalog catalog = PlayerSkinCatalog.LoadedInstance;

        if (catalog != null && catalog.Skins != null)
        {
            int unlocked = 0;
            int total = catalog.Skins.Count;

            for (int i = 0; i < total; i++)
            {
                PlayerSkinCatalog.SkinEntry skin = catalog.Skins[i];
                if (skin != null && catalog.IsUnlocked(skin))
                    unlocked++;
            }

            AppendStat("Skins Unlocked", $"{unlocked} / {total}");
        }

        AppendSpacer();
    }

    private void AppendModes()
    {
        builder.AppendLine("MISSION MODES");
        AppendMode("Score Missions", WinConditionType.ReachScore);
        AppendMode("Survival Missions", WinConditionType.SurviveTime);
        AppendMode("Timed Score Missions", WinConditionType.ReachScoreWithinTime);
        AppendSpacer();
    }

    private void AppendMode(string label, WinConditionType mode)
    {
        int runs = StatsManager.GetModeRuns(mode);
        int wins = StatsManager.GetModeWins(mode);
        builder.Append(label)
            .Append(": ")
            .Append(wins)
            .Append(" / ")
            .Append(runs)
            .Append(" wins");

        if (runs > 0)
        {
            builder.Append("  (")
                .Append((wins / (float)runs * 100f).ToString("F1"))
                .Append("%)");
        }

        builder.AppendLine();
    }

    private void AppendPerformance()
    {
        builder.AppendLine("PERFORMANCE");
        AppendStat("Total Score Earned", StatsManager.GetTotalScore());
        AppendStat("Best Run Score", StatsManager.GetBestRunScore());
        AppendStat("Average Score / Score Run", StatsManager.GetAverageScorePerScoreRun().ToString("F1"));
        AppendStat("Highest Combo", $"x{StatsManager.GetHighestCombo()}");
        AppendStat("Longest Combo Chain", $"{StatsManager.GetLongestComboChain()} coins");
        AppendStat("6x Combo Reached", StatsManager.GetMaxComboReachedCount());
        AppendStat("Combo Bonus Points", StatsManager.GetComboBonusScore());
        AppendSpacer();
    }

    private void AppendNearMiss()
    {
        builder.AppendLine("NEAR MISS");
        AppendStat("Total Near Misses", StatsManager.GetNearMisses());
        AppendStat("Best Near Miss Streak", $"x{StatsManager.GetBestNearMissStreak()}");
        AppendSpacer();
    }

    private void AppendCollectibles()
    {
        builder.AppendLine("COLLECTIBLES");
        AppendStat("Total Coins", StatsManager.GetTotalCoins());
        AppendStat("Normal Coins", StatsManager.GetNormalCoins());
        AppendStat("Gold Coins", StatsManager.GetGoldCoins());
        AppendStat("Rare Coins", StatsManager.GetRareCoins());
        AppendStat("Most Coins in a Run", StatsManager.GetMostCoinsInRun());
        AppendStat("Base Coin Value Collected", StatsManager.GetTotalCoinValue());
        AppendStat("Magnet Coins", StatsManager.GetMagnetCoins());
        AppendSpacer();
    }

    private void AppendAbilities()
    {
        int armorUses = StatsManager.GetArmorBuffUses();
        int armorSaves = StatsManager.GetArmorSaves();

        builder.AppendLine("ABILITIES & POWER-UPS");
        AppendStat("Dash Uses", StatsManager.GetDashUses());
        AppendStat("Clone Uses", StatsManager.GetCloneUses());
        AppendStat("Slow Buff Uses", StatsManager.GetSlowBuffUses());
        AppendStat("Armor Buff Uses", armorUses);
        AppendStat("Armor Saves", armorSaves);
        AppendStat("Armor Save Rate", FormatPercent(armorSaves, armorUses));
        AppendSpacer();
    }

    private void AppendDangerMastery()
    {
        builder.AppendLine("DANGER MASTERY");
        AppendStat("Beacons Destroyed", StatsManager.GetBeaconsDestroyed());
        AppendStat("Hunters Stunned", StatsManager.GetHuntersStunned());
        AppendStat("Boss Encounters", StatsManager.GetBossEncounters());
        AppendStat("Boss Splits Triggered", StatsManager.GetBossSplits());
        AppendStat("Boss AOE Evades", StatsManager.GetBossAoeEvades());
        AppendStat("Mini-Boss AOE Evades", StatsManager.GetMiniBossAoeEvades());
        AppendSpacer();
    }

    private void AppendDeathAnalysis()
    {
        builder.AppendLine("DEATH ANALYSIS");

        int nemesisCount;
        string nemesis = StatsManager.GetNemesis(out nemesisCount);

        AppendStat(
            "Nemesis",
            nemesisCount > 0
                ? $"{nemesis} ({nemesisCount})"
                : "NONE"
        );

        bool hasAny = false;

        for (int i = 0; i < StatsManager.TrackedDeathCauses.Length; i++)
        {
            string cause = StatsManager.TrackedDeathCauses[i];
            int count = StatsManager.GetDeathCauseCount(cause);

            if (count <= 0)
                continue;

            AppendStat(FormatCauseLabel(cause), count);
            hasAny = true;
        }

        if (!hasAny)
            builder.AppendLine("No recorded death causes yet.");

        AppendSpacer();
    }

    private void AppendBestTimes()
    {
        builder.AppendLine("LEVEL BEST TIMES");

        bool hasAny = false;

        for (int level = StatsManager.FirstLevelNumber;
             level <= StatsManager.LastLevelNumber;
             level++)
        {
            float best = StatsManager.GetLevelBestTime(level);

            if (best <= 0f)
                continue;

            builder.Append("Level ")
                .Append(level.ToString("00"))
                .Append(": ")
                .AppendLine(FormatPreciseTime(best));

            hasAny = true;
        }

        float devRoom = StatsManager.GetDevRoomBestTime();
        if (devRoom > 0f)
        {
            builder.Append("Dev Room: ")
                .AppendLine(FormatPreciseTime(devRoom));
            hasAny = true;
        }

        if (!hasAny)
            builder.AppendLine("No best-time records yet.");
    }

    private static string FormatCauseLabel(string cause)
    {
        switch (cause)
        {
            case "LASER BULLET": return "Laser Bullet Deaths";
            case "LASER WALL": return "Laser Wall Deaths";
            case "MINI BOSS": return "Mini-Boss Deaths";
            case "SPACE BOMB": return "Space Bomb Deaths";
            case "TIME EXPIRED": return "Time Expired";
            case "UNKNOWN": return "Unknown Deaths";
            default: return cause.Substring(0, 1) + cause.Substring(1).ToLowerInvariant() + " Deaths";
        }
    }

    private void AppendStat(string label, int value)
    {
        AppendStat(label, value.ToString("N0"));
    }

    private void AppendStat(string label, string value)
    {
        builder.Append(label)
            .Append(": ")
            .AppendLine(value);
    }

    private void AppendSpacer()
    {
        builder.AppendLine();
    }

    private void ResetScrollToTop()
    {
        if (statsScrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        if (statsScrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(statsScrollRect.content);
        }

        statsScrollRect.StopMovement();
        statsScrollRect.verticalNormalizedPosition = 1f;
    }

    private static string FormatPercent(int numerator, int denominator)
    {
        if (denominator <= 0)
            return "0.0%";

        return $"{numerator / (float)denominator * 100f:F1}%";
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        int hours = totalSeconds / 3600;
        int minutes = totalSeconds % 3600 / 60;
        int secs = totalSeconds % 60;

        if (hours > 0)
            return $"{hours}h {minutes}m {secs}s";

        return $"{minutes}m {secs}s";
    }

    private static string FormatPreciseTime(float seconds)
    {
        float safe = Mathf.Max(0f, seconds);
        int minutes = Mathf.FloorToInt(safe / 60f);
        float remaining = safe - minutes * 60f;

        if (minutes > 0)
            return $"{minutes}:{remaining:00.00}";

        return $"{remaining:0.00}s";
    }
}
