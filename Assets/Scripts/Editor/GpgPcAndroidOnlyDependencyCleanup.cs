#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fateful Rush's Google Play Games on PC project is Android-only.
/// External Dependency Manager for Unity installs an iOS resolver assembly as
/// part of its common package. On Windows machines without Unity iOS Build
/// Support, that optional assembly can fail to load because
/// UnityEditor.iOS.Extensions.Xcode is unavailable.
///
/// The Android resolver is still required by AdMob / GPGS / Input SDK, so this
/// class disables ONLY Google.IOSResolver.dll in the Unity Editor. It does not
/// delete or disable Google.JarResolver, VersionHandler or PackageManager.
/// </summary>
public static class GpgPcAndroidOnlyDependencyCleanup
{
    private const string ResolverFileName = "Google.IOSResolver.dll";

    [MenuItem("Fateful Rush/PC/Disable Unused iOS Resolver (PC Project)")]
    public static void DisableIosResolverForPcProject()
    {
        string edmRoot = Path.Combine(
            Application.dataPath,
            "ExternalDependencyManager"
        );

        if (!Directory.Exists(edmRoot))
            return;

        string[] resolverDlls;
        try
        {
            resolverDlls = Directory.GetFiles(
                edmRoot,
                ResolverFileName,
                SearchOption.AllDirectories
            );
        }
        catch
        {
            return;
        }

        bool changedAny = false;

        for (int i = 0; i < resolverDlls.Length; i++)
        {
            string fullPath = resolverDlls[i].Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');

            if (!fullPath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                continue;

            string assetPath = "Assets" + fullPath.Substring(dataPath.Length);
            PluginImporter importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
            if (importer == null)
                continue;

            bool needsChange = importer.GetCompatibleWithEditor() ||
                               importer.GetCompatibleWithAnyPlatform();

            if (!needsChange)
                continue;

            try
            {
                importer.SetCompatibleWithAnyPlatform(false);
                importer.SetCompatibleWithEditor(false);
                importer.SaveAndReimport();
                changedAny = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[GPG PC] Could not disable unused iOS resolver at " +
                    assetPath + ". " + ex.Message
                );
            }
        }

        if (changedAny)
        {
            Debug.Log(
                "[GPG PC] Unused Google.IOSResolver.dll disabled for this " +
                "Android/PC-only project. Android dependency resolution remains active."
            );
        }
    }
}
#endif
