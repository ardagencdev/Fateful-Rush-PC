#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Google Play Games Plugin for Unity currently contains vendor-owned source
/// that still references Unity APIs marked obsolete in newer Unity 6 releases
/// (for example IUserProfile / UserScope and some editor-only APIs).
///
/// Fateful Rush does not own those calls and changing Google's implementation
/// would be riskier than simply scoping CS0618 suppression to the vendor files.
/// This patcher therefore suppresses CS0618 ONLY inside
/// Assets/GooglePlayGames/com.google.play.games/**/*.cs.
///
/// Project/gameplay scripts continue to report obsolete API warnings normally.
/// </summary>
public static class GpgsEditorWarningCleaner
{
    private const string LegacyMarker =
        "// FATEFUL_RUSH_GPGS_EDITOR_CS0618_SCOPE";

    private const string Marker =
        "// FATEFUL_RUSH_GPGS_VENDOR_CS0618_SCOPE";

    private const string DisableLine =
        "#pragma warning disable 0618";

    private const string RestoreLine =
        "#pragma warning restore 0618";

    [MenuItem("Fateful Rush/PC/Clean Google Play Games Vendor Warnings")]
    public static void CleanWarnings()
    {
        string packageRoot = Path.Combine(
            Application.dataPath,
            "GooglePlayGames",
            "com.google.play.games"
        );

        if (!Directory.Exists(packageRoot))
            return;

        string[] files = Directory.GetFiles(
            packageRoot,
            "*.cs",
            SearchOption.AllDirectories
        );

        bool changedAny = false;

        for (int i = 0; i < files.Length; i++)
        {
            string filePath = files[i];
            string contents;

            try
            {
                contents = File.ReadAllText(filePath);
            }
            catch
            {
                continue;
            }

            // Older Fateful Rush package versions already patched the Editor
            // folder using LegacyMarker. Do not wrap those files a second time.
            if (contents.Contains(Marker) || contents.Contains(LegacyMarker))
                continue;

            // Do not touch files that do not currently contain obsolete usage.
            // This keeps the vendor diff as small as possible.
            if (!contents.Contains("[Obsolete") &&
                !contents.Contains("System.Obsolete") &&
                !MightReferenceObsoleteUnitySocialApi(contents))
            {
                continue;
            }

            string newline = contents.Contains("\r\n") ? "\r\n" : "\n";
            string patched =
                Marker + newline +
                DisableLine + newline +
                contents.TrimEnd('\r', '\n') + newline +
                RestoreLine + newline;

            try
            {
                File.WriteAllText(filePath, patched, new UTF8Encoding(false));
                changedAny = true;
            }
            catch
            {
                // Vendor folder can be read-only in unusual package layouts.
                // Silently leave it untouched rather than generating new noise.
            }
        }

        if (!changedAny)
            return;

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log(
            "[GPG PC] Scoped Google Play Games vendor CS0618 warnings cleaned."
        );
    }

    private static bool MightReferenceObsoleteUnitySocialApi(string contents)
    {
        return contents.Contains("IUserProfile") ||
               contents.Contains("UserScope") ||
               contents.Contains("SetApplicationIdentifier(BuildTargetGroup") ||
               contents.Contains("GPGSProjectSettings");
    }
}
#endif
