using UnityEngine;

/// <summary>
/// Google Play Games leaderboard IDs for Fateful Rush.
///
/// These are raw Play Games IDs, so this file does not depend on the
/// auto-generated GPGSIds.cs resource file.
/// </summary>
public static class FatefulRushLeaderboardIds
{
    public const string Level01 = "CgkIsrqqqssOEAIQKg";
    public const string Level02 = "CgkIsrqqqssOEAIQKw";
    public const string Level03 = "CgkIsrqqqssOEAIQLA";
    public const string Level04 = "CgkIsrqqqssOEAIQLQ";
    public const string Level05 = "CgkIsrqqqssOEAIQLg";
    public const string Level06 = "CgkIsrqqqssOEAIQLw";
    public const string Level07 = "CgkIsrqqqssOEAIQMA";
    public const string Level08 = "CgkIsrqqqssOEAIQMQ";
    public const string Level09 = "CgkIsrqqqssOEAIQMg";

    public const string Level11 = "CgkIsrqqqssOEAIQMw";
    public const string Level12 = "CgkIsrqqqssOEAIQNA";

    public const string Level14 = "CgkIsrqqqssOEAIQNQ";
    public const string Level15 = "CgkIsrqqqssOEAIQNg";
    public const string Level16 = "CgkIsrqqqssOEAIQNw";

    public const string Level18 = "CgkIsrqqqssOEAIQOA";
    public const string Level19 = "CgkIsrqqqssOEAIQOQ";
    public const string Level20 = "CgkIsrqqqssOEAIQOg";

    public const string Level22 = "CgkIsrqqqssOEAIQOw";
    public const string Level23 = "CgkIsrqqqssOEAIQPA";

    public const string Level25 = "CgkIsrqqqssOEAIQPQ";

    public const string Level27 = "CgkIsrqqqssOEAIQPg";
    public const string Level28 = "CgkIsrqqqssOEAIQPw";

    public const string Level30 = "CgkIsrqqqssOEAIQQA";

    public const string Level32 = "CgkIsrqqqssOEAIQQQ";
    public const string Level33 = "CgkIsrqqqssOEAIQQg";

    public const string Level35 = "CgkIsrqqqssOEAIQQw";
    public const string Level36 = "CgkIsrqqqssOEAIQRA";

    public const string Level38 = "CgkIsrqqqssOEAIQRQ";
    public const string Level39 = "CgkIsrqqqssOEAIQRg";

    public static bool TryGetId(
        int levelNumber,
        out string leaderboardId)
    {
        switch (levelNumber)
        {
            case 1:  leaderboardId = Level01; return true;
            case 2:  leaderboardId = Level02; return true;
            case 3:  leaderboardId = Level03; return true;
            case 4:  leaderboardId = Level04; return true;
            case 5:  leaderboardId = Level05; return true;
            case 6:  leaderboardId = Level06; return true;
            case 7:  leaderboardId = Level07; return true;
            case 8:  leaderboardId = Level08; return true;
            case 9:  leaderboardId = Level09; return true;

            case 11: leaderboardId = Level11; return true;
            case 12: leaderboardId = Level12; return true;

            case 14: leaderboardId = Level14; return true;
            case 15: leaderboardId = Level15; return true;
            case 16: leaderboardId = Level16; return true;

            case 18: leaderboardId = Level18; return true;
            case 19: leaderboardId = Level19; return true;
            case 20: leaderboardId = Level20; return true;

            case 22: leaderboardId = Level22; return true;
            case 23: leaderboardId = Level23; return true;

            case 25: leaderboardId = Level25; return true;

            case 27: leaderboardId = Level27; return true;
            case 28: leaderboardId = Level28; return true;

            case 30: leaderboardId = Level30; return true;

            case 32: leaderboardId = Level32; return true;
            case 33: leaderboardId = Level33; return true;

            case 35: leaderboardId = Level35; return true;
            case 36: leaderboardId = Level36; return true;

            case 38: leaderboardId = Level38; return true;
            case 39: leaderboardId = Level39; return true;

            default:
                leaderboardId = null;
                return false;
        }
    }
}
