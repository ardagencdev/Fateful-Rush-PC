using UnityEngine;

/// <summary>
/// Persistent Main Menu footer state.
///
/// Default / first launch:
/// SIGNAL // UNSTABLE
///
/// Successful numbered level completion:
/// SIGNAL // STABLE
///
/// Failed numbered level attempt:
/// SIGNAL // UNSTABLE
///
/// The state persists between scenes and game launches.
/// </summary>
public static class SignalStatusState
{
    private const string StableKey =
        "FatefulRush_SignalStable";

    public static bool IsStable =>
        PlayerPrefs.GetInt(
            StableKey,
            0
        ) == 1;

    public static string DisplayText =>
        IsStable
            ? "SIGNAL // STABLE"
            : "SIGNAL // UNSTABLE";

    public static void MarkStable()
    {
        SetStable(true);
    }

    public static void MarkUnstable()
    {
        SetStable(false);
    }

    private static void SetStable(bool stable)
    {
        PlayerPrefs.SetInt(
            StableKey,
            stable ? 1 : 0
        );

        PlayerPrefs.Save();
    }
}
