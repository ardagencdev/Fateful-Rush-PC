using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PlayerSkinCatalog",
    menuName = "Void Rush/Player Skin Catalog"
)]
public class PlayerSkinCatalog : ScriptableObject
{
    public static PlayerSkinCatalog LoadedInstance { get; private set; }

    public static event Action SelectedSkinChanged;

    public const string SelectedSkinKey =
        "SelectedPlayerSkinId";

    private const string DebugAllSkinsUnlockedKey =
        "DebugAllPlayerSkinsUnlocked";

    private const string CompletedLevelKeyPrefix =
        "CompletedLevel_";

    private const string LegacySilverSkinId =
        "silver";

    private const int CurrentArmorColorVersion = 2;
    private const int CurrentDarkVisualColorVersion = 3;
    private const int CurrentUIThemeColorVersion = 1;

    // Canonical 11-skin progression. Keeping this in the runtime catalog makes
    // unlock checks/result rewards self-healing even if a scene still references
    // an older serialized catalog asset.
    public static int GetCurrentRequiredCompletedLevel(string skinId)
    {
        if (string.IsNullOrWhiteSpace(skinId))
            return -1;

        string normalizedId = skinId
            .Trim()
            .ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        switch (normalizedId)
        {
            case "white":
                return 0;
            case "blue":
                return 4;
            case "orange":
                return 8;
            case "red":
                return 12;
            case "green":
                return 16;
            case "pink":
            case "deeppink":
            case "hotpink":
                return 20;
            case "yellow":
                return 24;
            case "cyan":
            case "lightblue":
                return 28;
            case "purple":
                return 32;
            case "dark":
            case "black":
                return 36;
            case "gold":
            case "golden":
                return 40;
            default:
                return -1;
        }
    }

    [Serializable]
    public class SkinEntry
    {
        [Tooltip("Kalıcı kayıt için benzersiz kimlik. Örnek: white, red, golden.")]
        public string id;

        public string displayName;
        public Sprite playerSprite;

        [ColorUsage(true, true)]
        public Color dashTrailColor = Color.white;

        [ColorUsage(true, true)]
        public Color armorVisualColor = Color.white;

        [HideInInspector]
        public int armorVisualColorVersion;

        [ColorUsage(false, false)]
        [Tooltip(
            "Menu, panel title and non-HUD button accent color for this skin."
        )]
        public Color uiThemeColor = Color.white;

        [HideInInspector]
        public int uiThemeColorVersion;

        [Min(0)]
        [Tooltip(
            "0 ise başlangıçtan açık. 1-40 ise o bölüm tamamlanınca açılır."
        )]
        public int requiredCompletedLevel;
    }

    [SerializeField]
    private List<SkinEntry> skins =
        new List<SkinEntry>();

    [SerializeField, Min(0)]
    [Tooltip("Normalde WhitePlayer girdisinin indexi 0 olmalı.")]
    private int defaultSkinIndex;

    public IReadOnlyList<SkinEntry> Skins => skins;

    public SkinEntry DefaultSkin
    {
        get
        {
            if (skins == null || skins.Count == 0)
                return null;

            int safeIndex = Mathf.Clamp(
                defaultSkinIndex,
                0,
                skins.Count - 1
            );

            return skins[safeIndex];
        }
    }

    public static bool AreAllSkinsDebugUnlocked
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return PlayerPrefs.GetInt(
                DebugAllSkinsUnlockedKey,
                0
            ) == 1;
#else
            return false;
#endif
        }
    }

    private void OnEnable()
    {
        LoadedInstance = this;
        EnsureCurrentSkinOrder();
        EnsureCurrentUnlockProgression();
        MigrateLegacySilverSelection();
        EnsureArmorVisualColors();
        EnsureDarkSkinVisualColors();
        EnsureUIThemeColors();
    }

    public SkinEntry GetSelectedSkin()
    {
        SkinEntry fallback = GetFallbackSkin();

        if (fallback == null)
            return null;

        string savedId = PlayerPrefs.GetString(
            SelectedSkinKey,
            fallback.id
        );

        SkinEntry savedSkin = FindById(savedId);

        if (savedSkin != null && IsUnlocked(savedSkin))
            return savedSkin;

        SaveSelectedSkin(fallback);
        return fallback;
    }

    public bool TrySelectSkin(SkinEntry skin)
    {
        if (skin == null || !IsUnlocked(skin))
            return false;

        SaveSelectedSkin(skin);
        return true;
    }

    public bool IsSelected(SkinEntry skin)
    {
        if (skin == null)
            return false;

        SkinEntry selected = GetSelectedSkin();

        return selected != null &&
               string.Equals(
                   selected.id,
                   skin.id,
                   StringComparison.Ordinal
               );
    }

    public bool IsUnlocked(SkinEntry skin)
    {
        if (skin == null)
            return false;

        if (AreAllSkinsDebugUnlocked)
            return true;

        if (skin.requiredCompletedLevel <= 0)
            return true;

        return PlayerPrefs.GetInt(
            CompletedLevelKeyPrefix +
            skin.requiredCompletedLevel,
            0
        ) == 1;
    }

    public SkinEntry FindById(string skinId)
    {
        if (skins == null ||
            string.IsNullOrWhiteSpace(skinId))
        {
            return null;
        }

        for (int i = 0; i < skins.Count; i++)
        {
            SkinEntry skin = skins[i];

            if (skin == null)
                continue;

            if (string.Equals(
                    skin.id,
                    skinId,
                    StringComparison.Ordinal))
            {
                return skin;
            }
        }

        return null;
    }

    public SkinEntry FindSkinUnlockedAtLevel(int completedLevelNumber)
    {
        if (completedLevelNumber <= 0 || skins == null)
            return null;

        EnsureCurrentUnlockProgression();

        for (int i = 0; i < skins.Count; i++)
        {
            SkinEntry skin = skins[i];

            if (skin != null &&
                skin.requiredCompletedLevel == completedLevelNumber)
            {
                return skin;
            }
        }

        return null;
    }

    public string GetRequirementText(SkinEntry skin)
    {
        if (skin == null)
            return string.Empty;

        if (IsUnlocked(skin))
        {
            return IsSelected(skin)
                ? "EQUIPPED"
                : "UNLOCKED";
        }

        return $"COMPLETE LEVEL {skin.requiredCompletedLevel}";
    }

    public static void SetDebugAllSkinsUnlocked(
        bool unlocked
    )
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (unlocked)
        {
            PlayerPrefs.SetInt(
                DebugAllSkinsUnlockedKey,
                1
            );
        }
        else
        {
            PlayerPrefs.DeleteKey(
                DebugAllSkinsUnlockedKey
            );
        }

        PlayerPrefs.Save();
#endif
    }

    public static bool ToggleDebugAllSkinsUnlocked()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool newState =
            !AreAllSkinsDebugUnlocked;

        SetDebugAllSkinsUnlocked(newState);

        return newState;
#else
        return false;
#endif
    }

    public Color GetSelectedUIThemeColor()
    {
        SkinEntry selectedSkin = GetSelectedSkin();

        return selectedSkin != null
            ? GetUIThemeColor(selectedSkin)
            : Color.white;
    }

    public static Color GetUIThemeColor(SkinEntry skin)
    {
        if (skin == null)
            return Color.white;

        if (skin.uiThemeColorVersion >= CurrentUIThemeColorVersion)
            return NormalizeUIThemeColor(skin.uiThemeColor);

        return GetDefaultUIThemeColor(skin);
    }

    public static void ClearSavedSelection()
    {
        bool hadSelection = PlayerPrefs.HasKey(SelectedSkinKey);

        PlayerPrefs.DeleteKey(SelectedSkinKey);
        PlayerPrefs.Save();
        FatefulRushCloudSave.RequestUpload();

        if (hadSelection)
            SelectedSkinChanged?.Invoke();
    }

    private void MigrateLegacySilverSelection()
    {
        if (!PlayerPrefs.HasKey(SelectedSkinKey))
            return;

        string savedId = PlayerPrefs.GetString(
            SelectedSkinKey,
            string.Empty
        );

        if (!string.Equals(
                savedId,
                LegacySilverSkinId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SkinEntry purpleSkin = FindById("purple");

        if (purpleSkin == null)
        {
            PlayerPrefs.DeleteKey(SelectedSkinKey);
            PlayerPrefs.Save();
            return;
        }

        PlayerPrefs.SetString(
            SelectedSkinKey,
            purpleSkin.id
        );

        PlayerPrefs.Save();
    }

    private SkinEntry GetFallbackSkin()
    {
        SkinEntry defaultSkin = DefaultSkin;

        if (defaultSkin != null &&
            IsUnlocked(defaultSkin))
        {
            return defaultSkin;
        }

        if (skins == null)
            return null;

        for (int i = 0; i < skins.Count; i++)
        {
            if (IsUnlocked(skins[i]))
                return skins[i];
        }

        return null;
    }

    private static void SaveSelectedSkin(SkinEntry skin)
    {
        if (skin == null ||
            string.IsNullOrWhiteSpace(skin.id))
        {
            return;
        }

        string previousSkinId = PlayerPrefs.GetString(
            SelectedSkinKey,
            string.Empty
        );

        bool changed = !string.Equals(
            previousSkinId,
            skin.id,
            StringComparison.Ordinal
        );

        PlayerPrefs.SetString(
            SelectedSkinKey,
            skin.id
        );

        PlayerPrefs.Save();

        if (changed)
        {
            FatefulRushCloudSave.RequestUpload();

            GooglePlayGamesManager.NotifySkinEquipped(
                skin.id
            );

            SelectedSkinChanged?.Invoke();
        }
    }

    private void EnsureCurrentSkinOrder()
    {
        if (skins == null || skins.Count <= 1)
            return;

        skins.Sort((left, right) =>
        {
            int leftOrder = GetCurrentSkinOrder(left?.id);
            int rightOrder = GetCurrentSkinOrder(right?.id);

            int orderComparison = leftOrder.CompareTo(rightOrder);

            if (orderComparison != 0)
                return orderComparison;

            string leftId = left?.id ?? string.Empty;
            string rightId = right?.id ?? string.Empty;

            return string.Compare(
                leftId,
                rightId,
                StringComparison.OrdinalIgnoreCase
            );
        });

        // White remains the canonical default at index 0.
        defaultSkinIndex = 0;
    }

    private static int GetCurrentSkinOrder(string skinId)
    {
        if (string.IsNullOrWhiteSpace(skinId))
            return int.MaxValue;

        string normalizedId = skinId
            .Trim()
            .ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        switch (normalizedId)
        {
            case "white":
                return 0;
            case "blue":
                return 1;
            case "orange":
                return 2;
            case "red":
                return 3;
            case "green":
                return 4;
            case "pink":
            case "deeppink":
            case "hotpink":
                return 5;
            case "yellow":
                return 6;
            case "cyan":
            case "lightblue":
                return 7;
            case "purple":
                return 8;
            case "dark":
            case "black":
                return 9;
            case "gold":
            case "golden":
                return 10;
            default:
                return 1000;
        }
    }

    private void EnsureCurrentUnlockProgression()
    {
        if (skins == null)
            return;

        for (int i = 0; i < skins.Count; i++)
        {
            SkinEntry skin = skins[i];

            if (skin == null)
                continue;

            int requiredLevel =
                GetCurrentRequiredCompletedLevel(skin.id);

            if (requiredLevel >= 0)
                skin.requiredCompletedLevel = requiredLevel;
        }
    }

    private void EnsureArmorVisualColors()
    {
        if (skins == null)
            return;

        for (int i = 0; i < skins.Count; i++)
        {
            SkinEntry skin = skins[i];

            if (skin == null ||
                skin.armorVisualColorVersion >=
                CurrentArmorColorVersion)
            {
                continue;
            }

            skin.armorVisualColor =
                GetDefaultArmorVisualColor(skin);

            skin.armorVisualColorVersion =
                CurrentArmorColorVersion;
        }
    }

    private void EnsureDarkSkinVisualColors()
    {
        if (skins == null)
            return;

        for (int i = 0; i < skins.Count; i++)
        {
            SkinEntry skin = skins[i];

            if (skin == null ||
                !IsDarkSkinId(skin.id) ||
                skin.armorVisualColorVersion >=
                CurrentDarkVisualColorVersion)
            {
                continue;
            }

            // Dark is intentionally a deep crimson instead of sharing
            // Red skin's bright red treatment.
            skin.dashTrailColor =
                new Color(0.72f, 0.02f, 0.07f, 0f);

            skin.armorVisualColor =
                new Color(0.58f, 0.03f, 0.08f, 1f);

            skin.armorVisualColorVersion =
                CurrentDarkVisualColorVersion;
        }
    }

    private void EnsureUIThemeColors()
    {
        if (skins == null)
            return;

        for (int i = 0; i < skins.Count; i++)
        {
            SkinEntry skin = skins[i];

            if (skin == null ||
                skin.uiThemeColorVersion >=
                CurrentUIThemeColorVersion)
            {
                continue;
            }

            skin.uiThemeColor =
                GetDefaultUIThemeColor(skin);

            skin.uiThemeColorVersion =
                CurrentUIThemeColorVersion;
        }
    }

    private static Color GetDefaultUIThemeColor(
        SkinEntry skin
    )
    {
        if (skin == null)
            return Color.white;

        if (IsDarkSkinId(skin.id))
            return new Color32(145, 8, 24, 255);

        if (IsPinkSkinId(skin.id))
            return new Color32(255, 20, 147, 255);

        return NormalizeUIThemeColor(
            NormalizeHdrColor(skin.armorVisualColor)
        );
    }

    private static bool IsDarkSkinId(string skinId)
    {
        if (string.IsNullOrWhiteSpace(skinId))
            return false;

        string normalizedId = skinId
            .Trim()
            .ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        return normalizedId == "dark" ||
               normalizedId == "black";
    }

    private static bool IsPinkSkinId(string skinId)
    {
        if (string.IsNullOrWhiteSpace(skinId))
            return false;

        string normalizedId = skinId
            .Trim()
            .ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        return normalizedId == "pink" ||
               normalizedId == "deeppink" ||
               normalizedId == "hotpink";
    }

    private static Color GetDefaultArmorVisualColor(
        SkinEntry skin
    )
    {
        if (skin == null)
            return Color.white;

        string normalizedId =
            string.IsNullOrWhiteSpace(skin.id)
                ? string.Empty
                : skin.id
                    .Trim()
                    .ToLowerInvariant()
                    .Replace("_", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace(" ", string.Empty);

        switch (normalizedId)
        {
            case "white":
                return new Color32(220, 232, 240, 255);

            case "blue":
                return new Color32(40, 170, 232, 255);

            case "cyan":
            case "lightblue":
                return new Color32(49, 233, 241, 255);

            case "yellow":
                return new Color32(254, 236, 7, 255);

            case "orange":
                return new Color32(255, 166, 6, 255);

            case "green":
                return new Color32(85, 255, 100, 255);

            case "red":
                return new Color32(245, 30, 34, 255);

            case "pink":
            case "deeppink":
            case "hotpink":
                return new Color32(255, 30, 160, 255);

            case "purple":
                return new Color32(170, 81, 209, 255);

            case "dark":
            case "black":
                return new Color32(148, 8, 20, 255);

            case "gold":
            case "golden":
                return new Color32(238, 196, 85, 255);

            default:
                return NormalizeHdrColor(
                    skin.dashTrailColor
                );
        }
    }

    private static Color NormalizeUIThemeColor(Color color)
    {
        color.r = Mathf.Clamp01(color.r);
        color.g = Mathf.Clamp01(color.g);
        color.b = Mathf.Clamp01(color.b);
        color.a = 1f;
        return color;
    }

    private static Color NormalizeHdrColor(Color color)
    {
        float highestChannel = Mathf.Max(
            color.r,
            color.g,
            color.b
        );

        if (highestChannel > 1f)
        {
            color.r /= highestChannel;
            color.g /= highestChannel;
            color.b /= highestChannel;
        }

        color.a = 1f;
        return color;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (skins == null)
            return;

        EnsureCurrentSkinOrder();
        EnsureCurrentUnlockProgression();
        EnsureArmorVisualColors();
        EnsureDarkSkinVisualColors();
        EnsureUIThemeColors();

        defaultSkinIndex = Mathf.Clamp(
            defaultSkinIndex,
            0,
            Mathf.Max(0, skins.Count - 1)
        );

        HashSet<string> usedIds =
            new HashSet<string>();

        HashSet<int> usedUnlockLevels =
            new HashSet<int>();

        for (int i = 0; i < skins.Count; i++)
        {
            SkinEntry skin = skins[i];

            if (skin == null)
                continue;

            skin.requiredCompletedLevel =
                Mathf.Max(
                    0,
                    skin.requiredCompletedLevel
                );

            if (string.IsNullOrWhiteSpace(skin.id))
            {
                Debug.LogWarning(
                    $"Player Skin Catalog: Skin {i} için ID boş.",
                    this
                );
            }
            else if (!usedIds.Add(skin.id))
            {
                Debug.LogWarning(
                    $"Player Skin Catalog: Tekrarlanan skin ID: {skin.id}",
                    this
                );
            }

            if (skin.requiredCompletedLevel > 0 &&
                !usedUnlockLevels.Add(
                    skin.requiredCompletedLevel))
            {
                Debug.LogWarning(
                    "Player Skin Catalog: Birden fazla skin aynı " +
                    $"level görevine bağlı: {skin.requiredCompletedLevel}",
                    this
                );
            }
        }

        SkinEntry defaultSkin = DefaultSkin;

        if (defaultSkin != null &&
            defaultSkin.requiredCompletedLevel != 0)
        {
            Debug.LogWarning(
                "Player Skin Catalog: Varsayılan skin başlangıçtan " +
                "açık olmalı. Required Completed Level değerini 0 yap.",
                this
            );
        }
    }
#endif
}
