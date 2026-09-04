#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Human-readable PC release validation. This doesn't replace an actual
/// runtime test on Google Play Games on PC, but catches accidental project
/// setting regressions before the build is uploaded.
/// </summary>
public static class GpgPcBuildValidator
{
    private const string InputSdkDefine = "PLAY_GAMES_PC_INPUT_SDK";
    private const string InputSdkProviderType =
        "Google.Android.Libraries.Play.Games.Inputmapping." +
        "InputMappingProviderCallbackHelper";

    [MenuItem("Fateful Rush/PC/Validate GPG PC Setup")]
    public static void ValidateMenu()
    {
        StringBuilder report = new StringBuilder(1024);
        bool valid = Validate(report);

        string header = valid
            ? "GPG PC setup is ready."
            : "GPG PC setup needs attention.";

        Debug.Log("[GPG PC] " + header + "\n" + report);
        EditorUtility.DisplayDialog(
            "Fateful Rush - GPG PC Validation",
            header + "\n\n" + report,
            "OK"
        );
    }

    public static bool Validate(StringBuilder report)
    {
        bool valid = true;

        valid &= Check(
            PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) ==
                ScriptingImplementation.IL2CPP,
            "IL2CPP scripting backend",
            report
        );

        valid &= Check(
            PlayerSettings.Android.targetArchitectures ==
                AndroidArchitecture.X86_64,
            "x86-64 only architecture",
            report
        );

        valid &= Check(
            PlayerSettings.Android.minSdkVersion >=
                AndroidSdkVersions.AndroidApiLevel25,
            "minimum Android API 25",
            report
        );

        bool hasApi36Enum = Enum.TryParse(
            "AndroidApiLevel36",
            out AndroidSdkVersions api36
        );
        valid &= Check(
            hasApi36Enum &&
            PlayerSettings.Android.targetSdkVersion == api36,
            "target Android API 36",
            report
        );

        GraphicsDeviceType[] apis =
            PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        valid &= Check(
            apis.Length > 0 && apis[0] == GraphicsDeviceType.Vulkan,
            "Vulkan is first graphics API",
            report
        );

        valid &= Check(
            EditorUserBuildSettings.androidBuildSubtarget ==
                MobileTextureSubtarget.DXT,
            "DXT PC texture compression",
            report
        );

        valid &= Check(
            EditorUserBuildSettings.buildAppBundle,
            "Android App Bundle (AAB)",
            report
        );

        valid &= Check(
            PlayerSettings.Android.minAspectRatio <= 1.5f &&
            PlayerSettings.Android.maxAspectRatio >= 2.33f,
            "3:2 through 21:9 aspect range",
            report
        );

        valid &= Check(
            PlayerSettings.Android.resizeableActivity,
            "resizable Android activity",
            report
        );

        valid &= Check(
            !PlayerSettings.Android.optimizedFramePacing,
            "Optimize Frame Pacing disabled for GPG PC",
            report
        );

        valid &= Check(
            PlayerSettings.Android.runWithoutFocus,
            "Android run without focus enabled",
            report
        );

        valid &= Check(
            SourceManifestDeclaresPcFeature(),
            "source manifest declares android.hardware.type.pc (raw mouse / hover)",
            report
        );

        bool inputSdkInstalled = AppDomain.CurrentDomain
            .GetAssemblies()
            .Any(a => a.GetType(InputSdkProviderType, false) != null);
        valid &= Check(
            inputSdkInstalled,
            "Google Input SDK package detected",
            report
        );

        string defines = PlayerSettings.GetScriptingDefineSymbols(
            NamedBuildTarget.Android
        );
        bool inputDefine = defines
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Contains(InputSdkDefine);
        valid &= Check(
            inputDefine,
            "Fateful Rush Input SDK integration enabled",
            report
        );

        return valid;
    }

    private static bool SourceManifestDeclaresPcFeature()
    {
        const string path = "Assets/Plugins/Android/AndroidManifest.xml";

        if (!File.Exists(path))
            return false;

        string manifest = File.ReadAllText(path);

        return manifest.Contains("android.hardware.type.pc") &&
               manifest.Contains("android:required=\"false\"");
    }

    private static bool Check(
        bool condition,
        string label,
        StringBuilder report)
    {
        report.Append(condition ? "[OK] " : "[MISSING] ");
        report.AppendLine(label);
        return condition;
    }
}
#endif
