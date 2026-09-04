using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class WinConditionIntroUI : MonoBehaviour
{
    private const int OverlaySortingOrder = 10000;

    private Canvas overlayCanvas;
    private CanvasGroup overlayGroup;
    private RectTransform contentTransform;
    private TextMeshProUGUI levelText;
    private TextMeshProUGUI objectiveText;
    private TextMeshProUGUI mechanicText;
    private TextMeshProUGUI inputHintText;

    public IEnumerator PlayAndWait(
        LevelConfig level,
        float totalDuration,
        float fadeDuration,
        bool allowSkip)
    {
        if (level == null)
            yield break;

        DestroyOverlay();
        CreateOverlay();

        levelText.text =
            BuildLevelText(level);

        objectiveText.text =
            BuildObjectiveText(level);

        string introducedMechanics =
            BuildIntroducedMechanicsText(level);

        bool hasIntroducedMechanic =
            !string.IsNullOrWhiteSpace(introducedMechanics);

        mechanicText.text = introducedMechanics;
        mechanicText.gameObject.SetActive(hasIntroducedMechanic);

        inputHintText.rectTransform.anchoredPosition =
            new Vector2(
                0f,
                hasIntroducedMechanic ? -180f : -142f
            );

        inputHintText.text =
            "CLICK OR PRESS SPACE / ENTER TO START";

        float safeTotalDuration =
            Mathf.Max(0.5f, totalDuration);

        float safeFadeDuration =
            Mathf.Clamp(
                fadeDuration,
                0f,
                safeTotalDuration * 0.45f
            );

        overlayGroup.alpha = 0f;

        contentTransform.localScale =
            Vector3.one * 0.92f;

        float elapsedTotal = 0f;
        bool skipRequested = false;

        if (safeFadeDuration > 0f)
        {
            float fadeElapsed = 0f;

            while (fadeElapsed < safeFadeDuration)
            {
                float deltaTime =
                    Time.unscaledDeltaTime;

                fadeElapsed += deltaTime;
                elapsedTotal += deltaTime;

                float progress =
                    Mathf.Clamp01(
                        fadeElapsed /
                        safeFadeDuration
                    );

                float easedProgress =
                    EaseOutCubic(progress);

                overlayGroup.alpha =
                    easedProgress;

                contentTransform.localScale =
                    Vector3.one * Mathf.Lerp(
                        0.92f,
                        1f,
                        easedProgress
                    );

                if (allowSkip &&
                    WasStartInputPressed())
                {
                    skipRequested = true;
                    break;
                }

                yield return null;
            }
        }
        else
        {
            overlayGroup.alpha = 1f;

            contentTransform.localScale =
                Vector3.one;
        }

        if (!skipRequested)
        {
            overlayGroup.alpha = 1f;

            contentTransform.localScale =
                Vector3.one;

            float automaticFadeStart =
                Mathf.Max(
                    elapsedTotal,
                    safeTotalDuration -
                    safeFadeDuration
                );

            while (elapsedTotal < automaticFadeStart)
            {
                float deltaTime =
                    Time.unscaledDeltaTime;

                elapsedTotal += deltaTime;

                if (allowSkip &&
                    WasStartInputPressed())
                {
                    skipRequested = true;
                    break;
                }

                yield return null;
            }
        }

        float currentAlpha =
            overlayGroup != null
                ? overlayGroup.alpha
                : 0f;

        if (overlayGroup != null &&
            currentAlpha > 0f)
        {
            float actualFadeOutDuration =
                safeFadeDuration > 0f
                    ? Mathf.Max(
                        0.12f,
                        safeFadeDuration *
                        currentAlpha
                    )
                    : 0.12f;

            float fadeOutElapsed = 0f;

            Vector3 startScale =
                contentTransform.localScale;

            while (fadeOutElapsed <
                   actualFadeOutDuration)
            {
                fadeOutElapsed +=
                    Time.unscaledDeltaTime;

                float progress =
                    Mathf.Clamp01(
                        fadeOutElapsed /
                        actualFadeOutDuration
                    );

                float easedProgress =
                    EaseInCubic(progress);

                overlayGroup.alpha =
                    Mathf.Lerp(
                        currentAlpha,
                        0f,
                        easedProgress
                    );

                contentTransform.localScale =
                    Vector3.Lerp(
                        startScale,
                        Vector3.one * 1.04f,
                        easedProgress
                    );

                yield return null;
            }
        }

        DestroyOverlay();
    }

    private void CreateOverlay()
    {
        GameObject canvasObject =
            new GameObject(
                "Win Condition Intro Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

        overlayCanvas =
            canvasObject.GetComponent<Canvas>();

        overlayCanvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        overlayCanvas.sortingOrder =
            OverlaySortingOrder;

        CanvasScaler canvasScaler =
            canvasObject.GetComponent<CanvasScaler>();

        canvasScaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        canvasScaler.referenceResolution =
            Screen.width >= Screen.height
                ? new Vector2(1920f, 1080f)
                : new Vector2(1080f, 1920f);

        canvasScaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        canvasScaler.matchWidthOrHeight = 0.5f;

        GameObject overlayObject =
            CreateUIObject(
                "Overlay",
                canvasObject.transform
            );

        RectTransform overlayRect =
            overlayObject.GetComponent<RectTransform>();

        StretchToParent(overlayRect);

        Image dimBackground =
            overlayObject.AddComponent<Image>();

        dimBackground.color =
            new Color(0f, 0f, 0f, 0.22f);

        dimBackground.raycastTarget = false;

        overlayGroup =
            overlayObject.AddComponent<CanvasGroup>();

        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;

        GameObject contentObject =
            CreateUIObject(
                "Content",
                overlayObject.transform
            );

        contentTransform =
            contentObject.GetComponent<RectTransform>();

        contentTransform.anchorMin =
            new Vector2(0.5f, 0.5f);

        contentTransform.anchorMax =
            new Vector2(0.5f, 0.5f);

        contentTransform.pivot =
            new Vector2(0.5f, 0.5f);

        contentTransform.anchoredPosition =
            Vector2.zero;

        contentTransform.sizeDelta =
            new Vector2(1500f, 600f);

        TMP_FontAsset sceneFont =
            FindSceneFont();

        Color skinAccentColor =
            GetSelectedSkinAccentColor();

        levelText =
            CreateText(
                "Level",
                contentTransform,
                sceneFont,
                string.Empty,
                25f,
                new Vector2(0f, 180f),
                new Vector2(1450f, 55f)
            );

        levelText.color =
            SetAlpha(skinAccentColor, 0.82f);

        levelText.characterSpacing = 4.5f;
        levelText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI titleText =
            CreateText(
                "Title",
                contentTransform,
                sceneFont,
                "WIN CONDITION",
                34f,
                new Vector2(0f, 122f),
                new Vector2(1450f, 70f)
            );

        titleText.color =
            new Color(1f, 1f, 1f, 0.72f);

        titleText.characterSpacing = 5f;

        objectiveText =
            CreateText(
                "Objective",
                contentTransform,
                sceneFont,
                string.Empty,
                68f,
                new Vector2(0f, 5f),
                new Vector2(1500f, 170f)
            );

        objectiveText.enableAutoSizing = true;
        objectiveText.fontSizeMin = 34f;
        objectiveText.fontSizeMax = 68f;
        objectiveText.fontStyle = FontStyles.Bold;

        mechanicText =
            CreateText(
                "New Mechanic",
                contentTransform,
                sceneFont,
                string.Empty,
                26f,
                new Vector2(0f, -108f),
                new Vector2(1450f, 50f)
            );

        mechanicText.enableAutoSizing = true;
        mechanicText.fontSizeMin = 20f;
        mechanicText.fontSizeMax = 26f;
        mechanicText.fontStyle = FontStyles.Bold;
        mechanicText.characterSpacing = 2.2f;
        mechanicText.color =
            SetAlpha(skinAccentColor, 0.92f);

        inputHintText =
            CreateText(
                "Input Hint",
                contentTransform,
                sceneFont,
                string.Empty,
                23f,
                new Vector2(0f, -180f),
                new Vector2(1450f, 60f)
            );

        inputHintText.color =
            new Color(1f, 1f, 1f, 0.52f);

        inputHintText.characterSpacing = 2.5f;
    }

    private static Color GetSelectedSkinAccentColor()
    {
        PlayerSkinCatalog catalog =
            ResolveSkinCatalog();

        if (catalog != null)
            return catalog.GetSelectedUIThemeColor();

        // Catalog bulunamazsa eski briefing morunu koru.
        return new Color(0.78f, 0.72f, 1f, 1f);
    }

    private static PlayerSkinCatalog ResolveSkinCatalog()
    {
        if (PlayerSkinCatalog.LoadedInstance != null)
            return PlayerSkinCatalog.LoadedInstance;

        PlayerSkinCatalog[] catalogs =
            Resources.FindObjectsOfTypeAll<PlayerSkinCatalog>();

        if (catalogs == null || catalogs.Length == 0)
            return null;

        for (int i = 0; i < catalogs.Length; i++)
        {
            PlayerSkinCatalog catalog = catalogs[i];

            if (catalog != null &&
                string.Equals(
                    catalog.name,
                    "PlayerSkinCatalog",
                    System.StringComparison.Ordinal
                ))
            {
                return catalog;
            }
        }

        return catalogs[0];
    }

    private static Color SetAlpha(
        Color color,
        float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        TMP_FontAsset font,
        string content,
        float fontSize,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject textObject =
            CreateUIObject(
                objectName,
                parent
            );

        RectTransform rectTransform =
            textObject.GetComponent<RectTransform>();

        rectTransform.anchorMin =
            new Vector2(0.5f, 0.5f);

        rectTransform.anchorMax =
            new Vector2(0.5f, 0.5f);

        rectTransform.pivot =
            new Vector2(0.5f, 0.5f);

        rectTransform.anchoredPosition =
            anchoredPosition;

        rectTransform.sizeDelta = size;

        TextMeshProUGUI text =
            textObject.AddComponent<TextMeshProUGUI>();

        if (font != null)
            text.font = font;

        text.text = content;
        text.fontSize = fontSize;

        text.alignment =
            TextAlignmentOptions.Center;

        text.textWrappingMode =
            TextWrappingModes.Normal;

        text.overflowMode =
            TextOverflowModes.Overflow;

        text.color = Color.white;
        text.raycastTarget = false;
        text.outlineWidth = 0.16f;

        text.outlineColor =
            new Color(0f, 0f, 0f, 0.9f);

        return text;
    }

    private static GameObject CreateUIObject(
        string objectName,
        Transform parent)
    {
        GameObject result =
            new GameObject(
                objectName,
                typeof(RectTransform)
            );

        result.transform.SetParent(
            parent,
            false
        );

        return result;
    }

    private static void StretchToParent(
        RectTransform target)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;

        target.pivot =
            new Vector2(0.5f, 0.5f);

        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }

    private static TMP_FontAsset FindSceneFont()
    {
        TextMeshProUGUI[] texts =
            UnityFindCompat.FindObjectsByType<TextMeshProUGUI>(
                FindObjectsInactive.Include
            );

        for (int i = 0;
             i < texts.Length;
             i++)
        {
            if (texts[i] != null &&
                texts[i].font != null)
            {
                return texts[i].font;
            }
        }

        return null;
    }

    private static string BuildLevelText(
        LevelConfig level)
    {
        if (level == null)
            return string.Empty;

        string levelName = string.IsNullOrWhiteSpace(level.levelName)
            ? string.Empty
            : level.levelName.Trim().ToUpperInvariant();

        if (level.levelNumber > 0)
        {
            return string.IsNullOrEmpty(levelName)
                ? $"LEVEL {level.levelNumber}"
                : $"LEVEL {level.levelNumber} — {levelName}";
        }

        if (!string.IsNullOrEmpty(levelName))
            return levelName;

        return "MISSION";
    }

    private static string BuildObjectiveText(
        LevelConfig level)
    {
        switch (level.winCondition)
        {
            case WinConditionType.ReachScore:
                return
                    $"{level.SafeWinScore} " +
                    "POINTS TO WIN";

            case WinConditionType.SurviveTime:
                return
                    "SURVIVE FOR " +
                    FormatTime(level.SafeTimeLimit);

            case WinConditionType.ReachScoreWithinTime:
                return
                    $"{level.SafeWinScore} POINTS\n" +
                    $"IN {FormatTime(level.SafeTimeLimit)}";

            default:
                return "COMPLETE THE MISSION";
        }
    }

    private static string BuildIntroducedMechanicsText(
        LevelConfig level)
    {
        if (level == null ||
            level.mechanicProgression == null)
        {
            return string.Empty;
        }

        LevelMechanicProgression progression =
            level.mechanicProgression;

        List<string> introduced =
            new List<string>();

        AddIfIntroduced(
            introduced,
            progression.reachScoreMode,
            "SCORE MODE"
        );

        AddIfIntroduced(
            introduced,
            progression.surviveTimeMode,
            "SURVIVAL MODE"
        );

        AddIfIntroduced(
            introduced,
            progression.timedScoreMode,
            "TIMED SCORE"
        );

        AddIfIntroduced(
            introduced,
            progression.dash,
            "DASH"
        );

        AddIfIntroduced(
            introduced,
            progression.clone,
            "CLONE"
        );

        AddIfIntroduced(
            introduced,
            progression.combo,
            "COMBO"
        );

        AddIfIntroduced(
            introduced,
            progression.normalCoin,
            "COINS"
        );

        AddIfIntroduced(
            introduced,
            progression.goldCoin,
            "GOLD COINS"
        );

        AddIfIntroduced(
            introduced,
            progression.rareCoin,
            "RARE COINS"
        );

        AddIfIntroduced(
            introduced,
            progression.staticObstacles,
            "OBSTACLES"
        );

        AddIfIntroduced(
            introduced,
            progression.normalEnemy,
            "STALKER"
        );

        AddIfIntroduced(
            introduced,
            progression.projectileEnemy,
            "BLASTER"
        );

        AddIfIntroduced(
            introduced,
            progression.hunterEnemy,
            "HUNTER"
        );

        AddIfIntroduced(
            introduced,
            progression.boss,
            "BOSS"
        );

        AddIfIntroduced(
            introduced,
            progression.beaconEnemy,
            "BEACON"
        );

        AddIfIntroduced(
            introduced,
            progression.armor,
            "ARMOR"
        );

        AddIfIntroduced(
            introduced,
            progression.slow,
            "SLOW"
        );

        AddIfIntroduced(
            introduced,
            progression.verticalLaser,
            "VERTICAL LASER"
        );

        AddIfIntroduced(
            introduced,
            progression.horizontalLaser,
            "HORIZONTAL LASER"
        );

        AddIfIntroduced(
            introduced,
            progression.spaceBomb,
            "SPACE BOMB"
        );

        if (introduced.Count == 0)
            return string.Empty;

        return "NEW  •  " +
               string.Join("  •  ", introduced);
    }

    private static void AddIfIntroduced(
        List<string> target,
        MechanicProgressionStatus status,
        string label)
    {
        if (status ==
            MechanicProgressionStatus.IntroducedHere)
        {
            target.Add(label);
        }
    }

    private static string FormatTime(float seconds)
    {
        float safeSeconds =
            Mathf.Max(0f, seconds);

        bool isWholeNumber =
            Mathf.Approximately(
                safeSeconds,
                Mathf.Round(safeSeconds)
            );

        string value =
            isWholeNumber
                ? Mathf.RoundToInt(safeSeconds)
                    .ToString()
                : safeSeconds.ToString("0.#");

        bool isSingleSecond =
            Mathf.Approximately(
                safeSeconds,
                1f
            );

        return value +
               (isSingleSecond
                   ? " SECOND"
                   : " SECONDS");
    }

    private static bool WasStartInputPressed()
    {
        if (Mouse.current != null &&
            Mouse.current
                .leftButton
                .wasPressedThisFrame)
        {
            return true;
        }

        if (Keyboard.current != null &&
            (Keyboard.current
                 .spaceKey
                 .wasPressedThisFrame ||
             Keyboard.current
                 .enterKey
                 .wasPressedThisFrame ||
             Keyboard.current
                 .numpadEnterKey
                 .wasPressedThisFrame))
        {
            return true;
        }

        if (Gamepad.current != null &&
            Gamepad.current
                .buttonSouth
                .wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;

        return 1f -
               inverse *
               inverse *
               inverse;
    }

    private static float EaseInCubic(float value)
    {
        return value * value * value;
    }

    private void DestroyOverlay()
    {
        if (overlayCanvas != null)
        {
            Destroy(overlayCanvas.gameObject);
        }

        overlayCanvas = null;
        overlayGroup = null;
        contentTransform = null;
        levelText = null;
        objectiveText = null;
        mechanicText = null;
        inputHintText = null;
    }

    private void OnDisable()
    {
        DestroyOverlay();
    }
}