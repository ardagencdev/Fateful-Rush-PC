#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

/// <summary>
/// Enables the Input SDK integration automatically once Google's Input SDK
/// package is present. If the package is removed, the define is removed too,
/// so the project cannot be left in a broken compile state.
/// </summary>
public static class GpgPcInputSdkDefineWatcher
{
    private const string Define = "PLAY_GAMES_PC_INPUT_SDK";
    private const string ProviderType =
        "Google.Android.Libraries.Play.Games.Inputmapping.InputMappingProviderCallbackHelper";

    [MenuItem("Fateful Rush/PC/Refresh Input SDK Integration")]
    public static void SyncDefine()
    {
        bool packageInstalled = AppDomain.CurrentDomain
            .GetAssemblies()
            .Any(assembly => assembly.GetType(ProviderType, false) != null);

        string defines = PlayerSettings.GetScriptingDefineSymbols(
            NamedBuildTarget.Android
        );

        var set = defines
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);

        bool changed = packageInstalled
            ? set.Add(Define)
            : set.Remove(Define);

        if (!changed)
            return;

        PlayerSettings.SetScriptingDefineSymbols(
            NamedBuildTarget.Android,
            string.Join(";", set)
        );

        UnityEngine.Debug.Log(
            packageInstalled
                ? "[GPG PC] Google Input SDK detected; integration enabled."
                : "[GPG PC] Google Input SDK not detected; integration define removed."
        );
    }
}
#endif
