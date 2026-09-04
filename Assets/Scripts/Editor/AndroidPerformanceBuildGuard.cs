#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Dedicated Google Play Games on PC build guard. This project still emits
/// an Android App Bundle, but the artifact is tuned for desktop execution.
/// </summary>
public sealed class AndroidPerformanceBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => -2000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        ApplyGpgPcSettings();
        EnsureInputSdkReady();
    }

    [MenuItem("Fateful Rush/PC/Apply GPG PC Build Settings")]
    public static void ApplyGpgPcSettings()
    {
        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.Android,
            ScriptingImplementation.IL2CPP
        );

        // Dedicated PC artifact: native x86-64 only. This avoids ARM
        // translation and also prevents shipping an unused ARM player binary.
        PlayerSettings.Android.targetArchitectures =
            AndroidArchitecture.X86_64;

        // Unity 6.3 supports Android API 25+ as the minimum; use 25 for the PC build.
        PlayerSettings.Android.minSdkVersion =
            AndroidSdkVersions.AndroidApiLevel25;

        // Google Play Games on PC recommends Vulkan first, with GLES as the
        // supported compatibility fallback.
        PlayerSettings.SetUseDefaultGraphicsAPIs(
            BuildTarget.Android,
            false
        );
        PlayerSettings.SetGraphicsAPIs(
            BuildTarget.Android,
            new[]
            {
                GraphicsDeviceType.Vulkan,
                GraphicsDeviceType.OpenGLES3
            }
        );

        // Desktop GPUs natively handle DXT far better than ASTC/ETC runtime
        // transcoding. This is a PC-only project, so use the desktop-friendly
        // Android build texture subtarget.
        EditorUserBuildSettings.androidBuildSubtarget =
            MobileTextureSubtarget.DXT;

        // Google Play delivery should remain AAB for this project.
        EditorUserBuildSettings.buildAppBundle = true;

        // 3:2 through 21:9 explicitly covers Google's recommended landscape
        // aspect ratios: 3:2, 16:10, 16:9 and 21:9.
        PlayerSettings.Android.minAspectRatio = 1.5f;
        PlayerSettings.Android.maxAspectRatio = 2.33f;
        PlayerSettings.Android.resizeableActivity = true;
        PlayerSettings.Android.runWithoutFocus = true;

        PlayerSettings.Android.optimizedFramePacing = false;
        PlayerSettings.Android.applicationEntry =
            AndroidApplicationEntry.Activity;
        PlayerSettings.gcIncremental = true;
        PlayerSettings.runInBackground = true;

        // Google Play requires API 36 for app updates from Aug 31, 2026.
        // Parse by name so this script stays source-compatible if Unity's enum
        // surface differs between Unity 6 patch releases.
        if (Enum.TryParse(
                "AndroidApiLevel36",
                out AndroidSdkVersions api36))
        {
            PlayerSettings.Android.targetSdkVersion = api36;
        }
        else
        {
            Debug.LogWarning(
                "[GPG PC] Unity could not expose Android API 36 in " +
                "AndroidSdkVersions. Install/update the Android SDK/Unity " +
                "patch and set Target API Level 36 manually before upload."
            );
        }

        Debug.Log(
            "[GPG PC] Applied: IL2CPP, x86-64, min API 25, Vulkan->GLES3, DXT, AAB, " +
            "3:2..21:9, resizable activity, background run and PC pacing."
        );
    }
    private static void EnsureInputSdkReady()
    {
        const string providerType =
            "Google.Android.Libraries.Play.Games.Inputmapping." +
            "InputMappingProviderCallbackHelper";

        bool packageInstalled = false;
        foreach (System.Reflection.Assembly assembly in
                 AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetType(providerType, false) != null)
            {
                packageInstalled = true;
                break;
            }
        }

        string defines = PlayerSettings.GetScriptingDefineSymbols(
            NamedBuildTarget.Android
        );
        string[] defineArray = defines.Split(
            new[] { ';' },
            StringSplitOptions.RemoveEmptyEntries
        );
        bool integrationEnabled =
            Array.IndexOf(defineArray, "PLAY_GAMES_PC_INPUT_SDK") >= 0;

        if (!packageInstalled || !integrationEnabled)
        {
            throw new BuildFailedException(
                "Google Play Games on PC Input SDK is not ready. " +
                "Run Fateful Rush > PC > Install Google Input SDK 1.1.1-beta, " +
                "wait for Unity to finish compiling, then run Validate GPG PC Setup."
            );
        }
    }

}
#endif
