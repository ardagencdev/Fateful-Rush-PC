#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FatefulRushPCURPGlobalSettingsFix
{
    private const string RootFolder = "Assets/PCOnly";
    private const string AssetPath = RootFolder + "/PC_URPGlobalSettings.asset";
    private const string GlobalSettingsTypeName =
        "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings";

    [MenuItem("Fateful Rush/PC/Fix URP Global Settings (Unity 6000.3)")]
    public static void Fix()
    {
        try
        {
            EnsureFolder("Assets", "PCOnly");

            var oldSettings =
                EditorGraphicsSettings.GetRenderPipelineGlobalSettingsAsset(
                    typeof(UniversalRenderPipeline));

            string oldPath = oldSettings != null
                ? AssetDatabase.GetAssetPath(oldSettings)
                : "(none)";

            var globalSettingsType = FindType(GlobalSettingsTypeName);
            if (globalSettingsType == null)
            {
                EditorUtility.DisplayDialog(
                    "Fateful Rush - PC URP Fix",
                    "UniversalRenderPipelineGlobalSettings tipi bulunamadi.\n\n" +
                    "URP package'inin kurulu oldugunu kontrol et.",
                    "OK");
                return;
            }

            if (!typeof(RenderPipelineGlobalSettings).IsAssignableFrom(globalSettingsType))
            {
                EditorUtility.DisplayDialog(
                    "Fateful Rush - PC URP Fix",
                    "Bulunan URP Global Settings tipi RenderPipelineGlobalSettings degil.",
                    "OK");
                return;
            }

            // If our PC-only asset already exists and has the correct type, reuse it.
            var pcSettings = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(AssetPath);

            if (pcSettings != null && pcSettings.GetType() != globalSettingsType)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "Fateful Rush - PC URP Fix",
                    "Assets/PCOnly/PC_URPGlobalSettings.asset zaten var ama tipi beklenenden farkli.\n\n" +
                    "Sadece bu PCOnly asset'ini silip temizden olusturayim mi?",
                    "Evet",
                    "Iptal");

                if (!replace)
                    return;

                AssetDatabase.DeleteAsset(AssetPath);
                pcSettings = null;
            }

            if (pcSettings == null)
            {
                var created = ScriptableObject.CreateInstance(globalSettingsType)
                              as RenderPipelineGlobalSettings;

                if (created == null)
                    throw new Exception("URP Global Settings instance olusturulamadi.");

                created.name = "PC_URPGlobalSettings";

                AssetDatabase.CreateAsset(created, AssetPath);

                // Unity 6: populate the clean asset with the graphics settings
                // supported by this exact Editor/URP version.
                EditorGraphicsSettings.PopulateRenderPipelineGraphicsSettings(created);

                EditorUtility.SetDirty(created);
                AssetDatabase.SaveAssetIfDirty(created);

                pcSettings = created;
            }
            else
            {
                // Make sure an existing PC-only asset is populated for this exact version too.
                EditorGraphicsSettings.PopulateRenderPipelineGraphicsSettings(pcSettings);
                EditorUtility.SetDirty(pcSettings);
                AssetDatabase.SaveAssetIfDirty(pcSettings);
            }

            // Associate the clean PC-only Global Settings with URP.
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset(
                typeof(UniversalRenderPipeline),
                pcSettings);

            EditorUtility.SetDirty(pcSettings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var activeSettings =
                EditorGraphicsSettings.GetRenderPipelineGlobalSettingsAsset(
                    typeof(UniversalRenderPipeline));

            bool success = activeSettings == pcSettings;

            Debug.Log(
                "[Fateful Rush PC URP Fix]\n" +
                "Previous URP Global Settings: " + oldPath + "\n" +
                "PC URP Global Settings: " + AssetPath + "\n" +
                "Assigned successfully: " + success);

            Selection.activeObject = pcSettings;
            EditorGUIUtility.PingObject(pcSettings);

            if (success)
            {
                EditorUtility.DisplayDialog(
                    "Fateful Rush - PC URP Fix",
                    "Tamamlandi.\n\n" +
                    "Aktif URP Global Settings artik:\n" +
                    AssetPath + "\n\n" +
                    "Console'u Clear yap. Eski warning cache'de gorunurse Unity'yi bir kez kapatip ac.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Fateful Rush - PC URP Fix",
                    "Asset olusturuldu ama aktif URP Global Settings olarak dogrulanamadi.\n\n" +
                    "Console'daki logu kontrol et.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog(
                "Fateful Rush - PC URP Fix",
                "Fix sirasinda hata olustu:\n\n" + ex.Message,
                "OK");
        }
    }

    private static Type FindType(string fullName)
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(a => a.GetType(fullName, false))
            .FirstOrDefault(t => t != null);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
