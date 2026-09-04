#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the Fateful Rush player-skin catalog on the current 11-skin layout.
///
/// This replaces the old Green-only auto setup while intentionally keeping the
/// original file/class name so the existing .meta file can be overwritten safely.
///
/// Progression order:
/// WHITE      -> default
/// BLUE       -> Level 4
/// ORANGE     -> Level 8
/// RED        -> Level 12
/// GREEN      -> Level 16
/// PINK       -> Level 20
/// YELLOW     -> Level 24
/// LIGHT BLUE -> Level 28
/// PURPLE     -> Level 32
/// DARK       -> Level 36
/// GOLDEN     -> Level 40
/// </summary>
[InitializeOnLoad]
public static class PlayerSkinGreenAutoSetup
{
    private const int ArmorColorVersion = 2;
    private const int UIThemeColorVersion = 1;

    private sealed class SkinPreset
    {
        public readonly string id;
        public readonly string displayName;
        public readonly int requiredCompletedLevel;
        public readonly string[] spriteFileNames;

        public SkinPreset(
            string id,
            string displayName,
            int requiredCompletedLevel,
            params string[] spriteFileNames)
        {
            this.id = id;
            this.displayName = displayName;
            this.requiredCompletedLevel = requiredCompletedLevel;
            this.spriteFileNames = spriteFileNames;
        }
    }

    private sealed class SkinSnapshot
    {
        public Sprite playerSprite;
        public Color dashTrailColor;
        public Color armorVisualColor;
        public int armorVisualColorVersion;
        public Color uiThemeColor;
        public int uiThemeColorVersion;
    }

    private static readonly SkinPreset[] Presets =
    {
        new SkinPreset("white", "WHITE", 0, "WhitePlayer"),
        new SkinPreset("blue", "BLUE", 4, "BluePlayer"),
        new SkinPreset("orange", "ORANGE", 8, "OrangePlayer"),
        new SkinPreset("red", "RED", 12, "RedPlayer"),
        new SkinPreset("green", "GREEN", 16, "GreenPlayer"),
        new SkinPreset("pink", "PINK", 20, "PinkPlayer"),
        new SkinPreset("yellow", "YELLOW", 24, "YellowPlayer"),

        // Keep the stable ID "cyan" so old saves that selected Cyan continue
        // to work, but use the new LightBluePlayer sprite and display name.
        new SkinPreset(
            "cyan",
            "LIGHT BLUE",
            28,
            "LightBluePlayer",
            "CyanPlayer"
        ),

        new SkinPreset("purple", "PURPLE", 32, "PurplePlayer"),
        new SkinPreset("dark", "DARK", 36, "DarkPlayer"),
        new SkinPreset("golden", "GOLDEN", 40, "GoldenPlayer")
    };

    static PlayerSkinGreenAutoSetup()
    {
        EditorApplication.delayCall += SyncAllCatalogsSilently;
    }

    [MenuItem("Fateful Rush/Player Skins/Sync 11-Skin Catalog")]
    private static void SyncAllCatalogsFromMenu()
    {
        int changedCatalogs = SyncAllCatalogs();

        if (changedCatalogs > 0)
        {
            Debug.Log(
                $"Player skins synced: {changedCatalogs} catalog(s) updated to the 11-skin layout. " +
                "Unlocks: 4 / 8 / 12 / 16 / 20 / 24 / 28 / 32 / 36 / 40."
            );
        }
        else
        {
            Debug.Log(
                "Player skin catalog is already using the current 11-skin layout."
            );
        }
    }

    private static void SyncAllCatalogsSilently()
    {
        SyncAllCatalogs();
    }

    private static int SyncAllCatalogs()
    {
        string[] catalogGuids =
            AssetDatabase.FindAssets("t:PlayerSkinCatalog");

        if (catalogGuids == null || catalogGuids.Length == 0)
        {
            catalogGuids = AssetDatabase.FindAssets(
                "PlayerSkinCatalog t:ScriptableObject"
            );
        }

        int changedCatalogs = 0;

        for (int i = 0; i < catalogGuids.Length; i++)
        {
            string catalogPath =
                AssetDatabase.GUIDToAssetPath(catalogGuids[i]);

            PlayerSkinCatalog catalog =
                AssetDatabase.LoadAssetAtPath<PlayerSkinCatalog>(
                    catalogPath
                );

            if (catalog == null)
                continue;

            if (SyncCatalog(catalog))
                changedCatalogs++;
        }

        if (changedCatalogs > 0)
            AssetDatabase.SaveAssets();

        return changedCatalogs;
    }

    private static bool SyncCatalog(PlayerSkinCatalog catalog)
    {
        Dictionary<string, SkinSnapshot> existing =
            CaptureExistingSkins(catalog);

        SerializedObject serializedCatalog =
            new SerializedObject(catalog);

        serializedCatalog.Update();

        SerializedProperty skinsProperty =
            serializedCatalog.FindProperty("skins");

        SerializedProperty defaultSkinIndexProperty =
            serializedCatalog.FindProperty("defaultSkinIndex");

        if (skinsProperty == null)
            return false;

        skinsProperty.arraySize = Presets.Length;

        for (int i = 0; i < Presets.Length; i++)
        {
            SkinPreset preset = Presets[i];

            existing.TryGetValue(
                preset.id,
                out SkinSnapshot snapshot
            );

            SerializedProperty entry =
                skinsProperty.GetArrayElementAtIndex(i);

            SetString(entry, "id", preset.id);
            SetString(entry, "displayName", preset.displayName);
            SetInt(
                entry,
                "requiredCompletedLevel",
                preset.requiredCompletedLevel
            );

            Sprite preferredSprite =
                FindFirstSprite(preset.spriteFileNames);

            Sprite spriteToUse = preferredSprite != null
                ? preferredSprite
                : snapshot != null
                    ? snapshot.playerSprite
                    : null;

            SetObject(entry, "playerSprite", spriteToUse);

            if (string.Equals(
                    preset.id,
                    "pink",
                    StringComparison.OrdinalIgnoreCase))
            {
                ApplyPinkColors(entry);
            }
            else if (snapshot != null)
            {
                SetColor(
                    entry,
                    "dashTrailColor",
                    snapshot.dashTrailColor
                );

                SetColor(
                    entry,
                    "armorVisualColor",
                    snapshot.armorVisualColor
                );

                SetInt(
                    entry,
                    "armorVisualColorVersion",
                    snapshot.armorVisualColorVersion
                );

                SetColor(
                    entry,
                    "uiThemeColor",
                    snapshot.uiThemeColor
                );

                SetInt(
                    entry,
                    "uiThemeColorVersion",
                    snapshot.uiThemeColorVersion
                );
            }
            else
            {
                ApplyFallbackColors(entry, preset.id);
            }
        }

        if (defaultSkinIndexProperty != null)
            defaultSkinIndexProperty.intValue = 0;

        bool changed =
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

        if (changed)
            EditorUtility.SetDirty(catalog);

        return changed;
    }

    private static Dictionary<string, SkinSnapshot>
        CaptureExistingSkins(PlayerSkinCatalog catalog)
    {
        Dictionary<string, SkinSnapshot> result =
            new Dictionary<string, SkinSnapshot>(
                StringComparer.OrdinalIgnoreCase
            );

        if (catalog == null || catalog.Skins == null)
            return result;

        for (int i = 0; i < catalog.Skins.Count; i++)
        {
            PlayerSkinCatalog.SkinEntry skin =
                catalog.Skins[i];

            if (skin == null ||
                string.IsNullOrWhiteSpace(skin.id))
            {
                continue;
            }

            result[skin.id] = new SkinSnapshot
            {
                playerSprite = skin.playerSprite,
                dashTrailColor = skin.dashTrailColor,
                armorVisualColor = skin.armorVisualColor,
                armorVisualColorVersion =
                    skin.armorVisualColorVersion,
                uiThemeColor = skin.uiThemeColor,
                uiThemeColorVersion = skin.uiThemeColorVersion
            };
        }

        return result;
    }

    private static void ApplyPinkColors(
        SerializedProperty entry)
    {
        // Matches the approved hot/deep-pink direction of PinkPlayer.
        // HDR intensity follows the existing skin catalog's trail/armor style.
        Color pinkDash = new Color(
            1.4980392f,
            0.11749328f,
            0.8633987f,
            0f
        );

        Color pinkArmor = new Color(
            1.4980392f,
            0.17623991f,
            0.93994623f,
            1f
        );

        Color pinkUI = new Color32(
            255,
            20,
            147,
            255
        );

        SetColor(entry, "dashTrailColor", pinkDash);
        SetColor(entry, "armorVisualColor", pinkArmor);
        SetInt(
            entry,
            "armorVisualColorVersion",
            ArmorColorVersion
        );

        SetColor(entry, "uiThemeColor", pinkUI);
        SetInt(
            entry,
            "uiThemeColorVersion",
            UIThemeColorVersion
        );
    }

    private static void ApplyFallbackColors(
        SerializedProperty entry,
        string skinId)
    {
        // This path is only used if an old catalog is missing one of the
        // built-in entries. Pink has its dedicated treatment above.
        Color color = GetFallbackColor(skinId);

        SetColor(
            entry,
            "dashTrailColor",
            new Color(
                color.r * 1.4980392f,
                color.g * 1.4980392f,
                color.b * 1.4980392f,
                0f
            )
        );

        SetColor(
            entry,
            "armorVisualColor",
            new Color(
                color.r * 1.4980392f,
                color.g * 1.4980392f,
                color.b * 1.4980392f,
                1f
            )
        );

        SetInt(
            entry,
            "armorVisualColorVersion",
            ArmorColorVersion
        );

        SetColor(entry, "uiThemeColor", color);
        SetInt(
            entry,
            "uiThemeColorVersion",
            UIThemeColorVersion
        );
    }

    private static Color GetFallbackColor(string skinId)
    {
        switch (skinId)
        {
            case "blue":
                return new Color32(65, 105, 225, 255);

            case "orange":
                return new Color32(255, 155, 55, 255);

            case "purple":
                return new Color32(138, 43, 226, 255);

            case "green":
                return new Color32(85, 255, 100, 255);

            case "yellow":
                return new Color32(255, 250, 120, 255);

            case "cyan":
                return new Color32(49, 233, 241, 255);

            case "red":
                return new Color32(245, 30, 34, 255);

            case "dark":
                return new Color32(145, 8, 24, 255);

            case "golden":
                return new Color32(238, 196, 85, 255);

            default:
                return Color.white;
        }
    }

    private static Sprite FindFirstSprite(
        params string[] fileNames)
    {
        if (fileNames == null)
            return null;

        for (int i = 0; i < fileNames.Length; i++)
        {
            Sprite sprite = FindSprite(fileNames[i]);

            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private static Sprite FindSprite(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        string[] assetGuids =
            AssetDatabase.FindAssets(fileName);

        for (int i = 0; i < assetGuids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(assetGuids[i]);

            if (string.IsNullOrWhiteSpace(path))
                continue;

            string candidateFileName =
                Path.GetFileNameWithoutExtension(path);

            if (!string.Equals(
                    candidateFileName,
                    fileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Sprite directSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (directSprite != null)
                return directSprite;

            UnityEngine.Object[] subAssets =
                AssetDatabase.LoadAllAssetsAtPath(path);

            for (int j = 0; j < subAssets.Length; j++)
            {
                if (subAssets[j] is Sprite subSprite)
                    return subSprite;
            }
        }

        return null;
    }

    private static void SetString(
        SerializedProperty parent,
        string propertyName,
        string value)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property != null)
            property.stringValue = value;
    }

    private static void SetInt(
        SerializedProperty parent,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property != null)
            property.intValue = value;
    }

    private static void SetColor(
        SerializedProperty parent,
        string propertyName,
        Color value)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property != null)
            property.colorValue = value;
    }

    private static void SetObject(
        SerializedProperty parent,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }
}
#endif
