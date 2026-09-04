using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-500)]
public sealed class SkinUIButtonThemeController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    private static readonly Color ButtonTextOutlineColor =
        new Color32(8, 10, 14, 235);

    private const float MinimumButtonTextOutlineWidth = 0.18f;

    private static SkinUIButtonThemeController instance;

    private Coroutine refreshRoutine;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad
    )]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject controllerObject =
            new GameObject("Skin UI Button Theme Controller");

        DontDestroyOnLoad(controllerObject);

        instance = controllerObject.AddComponent
            <SkinUIButtonThemeController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        PlayerSkinCatalog.SelectedSkinChanged -=
            HandleSelectedSkinChanged;

        PlayerSkinCatalog.SelectedSkinChanged +=
            HandleSelectedSkinChanged;
    }

    private void Start()
    {
        ScheduleRefresh();
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;

        PlayerSkinCatalog.SelectedSkinChanged -=
            HandleSelectedSkinChanged;

        instance = null;
    }

    private void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode
    )
    {
        ScheduleRefresh();
    }

    private void HandleSelectedSkinChanged()
    {
        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        if (!isActiveAndEnabled)
            return;

        if (refreshRoutine != null)
            StopCoroutine(refreshRoutine);

        refreshRoutine = StartCoroutine(
            RefreshAfterSceneSetup()
        );
    }

    private IEnumerator RefreshAfterSceneSetup()
    {
        // First frame: let scene Awake/OnEnable finish.
        yield return null;
        RefreshCurrentSceneInternal();

        // Second pass catches UI that is created in Start().
        yield return null;
        RefreshCurrentSceneInternal();

        refreshRoutine = null;
    }

    public static void RefreshCurrentScene()
    {
        EnsureInstance();
        instance.RefreshCurrentSceneInternal();
    }

    public static void ApplyButtonTheme(Button button)
    {
        if (button == null)
            return;

        EnsureInstance();
        instance.ApplyButtonIfEligible(button);
    }

    private void RefreshCurrentSceneInternal()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid() || !activeScene.isLoaded)
            return;

        GameStateManager gameStateManager =
            FindGameStateManagerInScene(activeScene);

        bool isMainMenu = string.Equals(
            activeScene.name,
            MainMenuSceneName,
            System.StringComparison.Ordinal
        );

        bool isGameplayScene = gameStateManager != null;

        if (!isMainMenu && !isGameplayScene)
            return;

        PlayerSkinCatalog skinCatalog = ResolveSkinCatalog();

        if (skinCatalog == null)
            return;

        Color themeColor =
            skinCatalog.GetSelectedUIThemeColor();

        ApplyButtons(
            activeScene,
            gameStateManager,
            isGameplayScene,
            themeColor
        );

        ApplyPanelAccentTexts(
            activeScene,
            gameStateManager,
            isGameplayScene,
            themeColor
        );

        ApplyStatsScrollbarTheme(
            activeScene,
            themeColor
        );

        ApplySliderHandleThemes(
            activeScene,
            gameStateManager,
            isGameplayScene,
            themeColor
        );

        if (isMainMenu)
        {
            ApplyMainMenuAmbientTextThemes(
                activeScene,
                themeColor
            );

            ApplyMainMenuRushTitle(
                activeScene,
                themeColor
            );

            // Skin theming must never overwrite the Continue level label.
            // That label previews the target level's gameplay NearStars color.
            MainMenu.Instance?.RefreshContinueLevelColor();
        }
    }

    private static void ApplyButtons(
        Scene activeScene,
        GameStateManager gameStateManager,
        bool isGameplayScene,
        Color themeColor
    )
    {
        Button[] buttons =
            Resources.FindObjectsOfTypeAll<Button>();

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (!IsButtonInScene(button, activeScene))
                continue;

            if (isGameplayScene &&
                IsGameplayHUDTransform(
                    button.transform,
                    gameStateManager
                ))
            {
                continue;
            }

            ApplyThemeColor(button, themeColor);
        }
    }

    private void ApplyButtonIfEligible(Button button)
    {
        if (button == null)
            return;

        Scene activeScene = SceneManager.GetActiveScene();

        if (!IsButtonInScene(button, activeScene))
            return;

        GameStateManager gameStateManager =
            FindGameStateManagerInScene(activeScene);

        bool isMainMenu = string.Equals(
            activeScene.name,
            MainMenuSceneName,
            System.StringComparison.Ordinal
        );

        bool isGameplayScene = gameStateManager != null;

        if (!isMainMenu && !isGameplayScene)
            return;

        if (isGameplayScene &&
            IsGameplayHUDTransform(
                button.transform,
                gameStateManager
            ))
        {
            return;
        }

        PlayerSkinCatalog skinCatalog = ResolveSkinCatalog();

        if (skinCatalog == null)
            return;

        ApplyThemeColor(
            button,
            skinCatalog.GetSelectedUIThemeColor()
        );
    }

    private static void ApplyPanelAccentTexts(
        Scene scene,
        GameStateManager gameStateManager,
        bool isGameplayScene,
        Color themeColor
    )
    {
        themeColor = NormalizeColor(themeColor);

        TMP_Text[] texts =
            Resources.FindObjectsOfTypeAll<TMP_Text>();

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (!IsTextInScene(text, scene))
                continue;

            if (isGameplayScene &&
                IsGameplayHUDTransform(
                    text.transform,
                    gameStateManager
                ))
            {
                continue;
            }

            // Button labels stay readable/white; only their outline is
            // strengthened by ApplyButtonTextReadability().
            if (IsInsideButton(text.transform))
                continue;

            if (IsFatefulRushTitle(text))
                continue;

            if (!IsPanelAccentText(text))
                continue;

            Color currentColor = text.color;

            text.color = new Color(
                themeColor.r,
                themeColor.g,
                themeColor.b,
                currentColor.a
            );
        }
    }

    private static bool IsPanelAccentText(TMP_Text text)
    {
        if (text == null || text.gameObject == null)
            return false;

        Transform current = text.transform;

        // Most panel headings in the project are named Title, TitleText,
        // AudioTitle, GameTitle, LevelTitleText or Header. Checking the
        // text object and its first parent also catches header containers
        // without touching ordinary labels.
        for (int depth = 0; depth < 2 && current != null; depth++)
        {
            string objectName = current.gameObject.name;

            if (ContainsIgnoreCase(objectName, "title") ||
                ContainsIgnoreCase(objectName, "header"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsInsideButton(Transform transform)
    {
        Transform current = transform;

        while (current != null)
        {
            if (current.GetComponent<Button>() != null)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static void ApplySliderHandleThemes(
        Scene scene,
        GameStateManager gameStateManager,
        bool isGameplayScene,
        Color themeColor
    )
    {
        themeColor = NormalizeColor(themeColor);

        Slider[] sliders =
            Resources.FindObjectsOfTypeAll<Slider>();

        for (int i = 0; i < sliders.Length; i++)
        {
            Slider slider = sliders[i];

            if (slider == null || slider.gameObject == null)
                continue;

            if (!slider.gameObject.scene.IsValid() ||
                slider.gameObject.scene != scene)
            {
                continue;
            }

            if (isGameplayScene &&
                IsGameplayHUDTransform(
                    slider.transform,
                    gameStateManager
                ))
            {
                continue;
            }

            if (slider.fillRect != null)
            {
                Graphic fillGraphic =
                    slider.fillRect.GetComponent<Graphic>();

                if (fillGraphic == null)
                {
                    fillGraphic =
                        slider.fillRect.GetComponentInChildren
                            <Graphic>(true);
                }

                if (fillGraphic != null)
                {
                    Color currentFillColor = fillGraphic.color;

                    fillGraphic.color = new Color(
                        themeColor.r,
                        themeColor.g,
                        themeColor.b,
                        currentFillColor.a
                    );
                }
            }

            if (slider.handleRect == null)
                continue;

            Graphic handleGraphic =
                slider.handleRect.GetComponent<Graphic>();

            if (handleGraphic == null)
            {
                handleGraphic =
                    slider.handleRect.GetComponentInChildren
                        <Graphic>(true);
            }

            if (handleGraphic == null)
                continue;

            if (slider.transition ==
                    Selectable.Transition.ColorTint &&
                slider.targetGraphic == handleGraphic)
            {
                ApplySelectableColorTintTheme(
                    slider,
                    handleGraphic,
                    themeColor
                );
            }
            else
            {
                Color currentColor = handleGraphic.color;

                handleGraphic.color = new Color(
                    themeColor.r,
                    themeColor.g,
                    themeColor.b,
                    currentColor.a
                );
            }
        }
    }

    private static void ApplyStatsScrollbarTheme(
        Scene scene,
        Color themeColor
    )
    {
        themeColor = NormalizeColor(themeColor);

        Scrollbar[] scrollbars =
            Resources.FindObjectsOfTypeAll<Scrollbar>();

        for (int i = 0; i < scrollbars.Length; i++)
        {
            Scrollbar scrollbar = scrollbars[i];

            if (scrollbar == null || scrollbar.gameObject == null)
                continue;

            if (!scrollbar.gameObject.scene.IsValid() ||
                scrollbar.gameObject.scene != scene)
            {
                continue;
            }

            if (!HasStatsAncestor(scrollbar.transform))
                continue;

            Graphic targetGraphic = scrollbar.targetGraphic;

            if (targetGraphic == null)
                targetGraphic = scrollbar.GetComponent<Graphic>();

            if (targetGraphic == null)
                continue;

            if (scrollbar.transition == Selectable.Transition.ColorTint)
            {
                ApplySelectableColorTintTheme(
                    scrollbar,
                    targetGraphic,
                    themeColor
                );
            }
            else
            {
                Color currentColor = targetGraphic.color;

                targetGraphic.color = new Color(
                    themeColor.r,
                    themeColor.g,
                    themeColor.b,
                    currentColor.a
                );
            }
        }
    }

    private static bool HasStatsAncestor(Transform transform)
    {
        Transform current = transform;

        while (current != null)
        {
            if (ContainsIgnoreCase(
                    current.gameObject.name,
                    "stats"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void ApplyMainMenuAmbientTextThemes(
        Scene scene,
        Color themeColor
    )
    {
        themeColor = NormalizeColor(themeColor);

        // The three animated ambient labels (SIGNAL LOST / THREAT UNKNOWN /
        // NO RETURN VECTOR) continuously rewrite their own TMP color in
        // MenuFloatingText.Update(). Feed the skin theme into that component
        // instead of setting TMP_Text.color only once.
        MenuFloatingText[] floatingTexts =
            Resources.FindObjectsOfTypeAll<MenuFloatingText>();

        for (int i = 0; i < floatingTexts.Length; i++)
        {
            MenuFloatingText floatingText = floatingTexts[i];

            if (floatingText == null || floatingText.gameObject == null)
                continue;

            if (!floatingText.gameObject.scene.IsValid() ||
                floatingText.gameObject.scene != scene)
            {
                continue;
            }

            floatingText.SetThemeColor(themeColor);
        }

        // Also catches decorative static ambient labels such as
        // "SIGNAL // UNSTABLE" if they are not driven by MenuFloatingText.
        TMP_Text[] texts =
            Resources.FindObjectsOfTypeAll<TMP_Text>();

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (!IsTextInScene(text, scene))
                continue;

            if (!IsMainMenuAmbientAccentText(text) &&
                !IsMainMenuVersionText(text))
            {
                continue;
            }

            // Animated texts are handled above so their cached baseColor is
            // updated as well. Reapplying here is harmless, but unnecessary.
            if (text.GetComponent<MenuFloatingText>() != null)
                continue;

            Color currentColor = text.color;

            text.color = new Color(
                themeColor.r,
                themeColor.g,
                themeColor.b,
                currentColor.a
            );
        }
    }

    private static bool IsMainMenuVersionText(TMP_Text text)
    {
        if (text == null || text.gameObject == null)
            return false;

        // Main Menu footer version label is driven by MenuVersionText.
        // Name check is kept as a fallback in case the component is moved
        // or the hierarchy is reorganized later.
        if (text.GetComponent<MenuVersionText>() != null)
            return true;

        string objectName = text.gameObject.name;

        return ContainsIgnoreCase(objectName, "versiontext") ||
               ContainsIgnoreCase(objectName, "version");
    }

    private static bool IsMainMenuAmbientAccentText(TMP_Text text)
    {
        if (text == null || text.gameObject == null)
            return false;

        Transform current = text.transform;

        while (current != null)
        {
            string objectName = current.gameObject.name;

            if (ContainsIgnoreCase(objectName, "ambienttexts") ||
                ContainsIgnoreCase(objectName, "ambienttext"))
            {
                return true;
            }

            current = current.parent;
        }

        string textObjectName = text.gameObject.name;

        if (ContainsIgnoreCase(textObjectName, "signallost") ||
            ContainsIgnoreCase(textObjectName, "threatunknown") ||
            ContainsIgnoreCase(textObjectName, "noreturnvector") ||
            ContainsIgnoreCase(textObjectName, "signalunstable"))
        {
            return true;
        }

        string content = text.text;

        return ContainsIgnoreCase(content, "SIGNAL LOST") ||
               ContainsIgnoreCase(content, "THREAT UNKNOWN") ||
               ContainsIgnoreCase(content, "NO RETURN VECTOR") ||
               content.TrimStart().StartsWith(
                   "SIGNAL //",
                   System.StringComparison.OrdinalIgnoreCase
               );
    }

    private static void ApplyMainMenuRushTitle(
        Scene scene,
        Color themeColor
    )
    {
        themeColor = NormalizeColor(themeColor);
        string themeHex = ColorUtility.ToHtmlStringRGB(themeColor);

        TMP_Text[] texts =
            Resources.FindObjectsOfTypeAll<TMP_Text>();

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (!IsTextInScene(text, scene))
                continue;

            if (!IsFatefulRushTitle(text))
                continue;

            ApplyRushRichTextColor(text, themeHex);
        }
    }

    private static bool IsFatefulRushTitle(TMP_Text text)
    {
        if (text == null || string.IsNullOrEmpty(text.text))
            return false;

        string sourceText = text.text;

        bool containsFateful =
            sourceText.IndexOf(
                "FATEFUL",
                System.StringComparison.OrdinalIgnoreCase
            ) >= 0;

        bool containsRush =
            sourceText.IndexOf(
                "RUSH",
                System.StringComparison.OrdinalIgnoreCase
            ) >= 0;

        return containsFateful && containsRush;
    }

    private static void ApplyRushRichTextColor(
        TMP_Text text,
        string themeHex
    )
    {
        if (text == null || string.IsNullOrEmpty(text.text))
            return;

        string source = text.text;

        int rushIndex = source.IndexOf(
            "RUSH",
            System.StringComparison.OrdinalIgnoreCase
        );

        if (rushIndex < 0)
            return;

        int colorTagStart = source.LastIndexOf(
            "<color=",
            rushIndex,
            System.StringComparison.OrdinalIgnoreCase
        );

        if (colorTagStart >= 0)
        {
            int colorTagEnd = source.IndexOf(
                '>',
                colorTagStart
            );

            int colorCloseTag = source.IndexOf(
                "</color>",
                rushIndex,
                System.StringComparison.OrdinalIgnoreCase
            );

            // RUSH is already inside a TMP <color> tag. Replace only
            // that opening tag so FATEFUL keeps its original color.
            if (colorTagEnd >= colorTagStart &&
                colorTagEnd < rushIndex &&
                colorCloseTag >= rushIndex)
            {
                string updatedText =
                    source.Substring(0, colorTagStart) +
                    "<color=#" + themeHex + ">" +
                    source.Substring(colorTagEnd + 1);

                if (!string.Equals(
                        updatedText,
                        source,
                        System.StringComparison.Ordinal))
                {
                    text.text = updatedText;
                }

                return;
            }
        }

        // Fallback for a title written as plain "FATEFUL RUSH".
        string themedRush =
            "<color=#" + themeHex + ">" +
            source.Substring(rushIndex, 4) +
            "</color>";

        text.text =
            source.Substring(0, rushIndex) +
            themedRush +
            source.Substring(rushIndex + 4);
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

    private static GameStateManager FindGameStateManagerInScene(
        Scene scene
    )
    {
        GameStateManager[] managers =
            Resources.FindObjectsOfTypeAll<GameStateManager>();

        for (int i = 0; i < managers.Length; i++)
        {
            GameStateManager manager = managers[i];

            if (manager == null ||
                manager.gameObject == null)
            {
                continue;
            }

            if (manager.gameObject.scene == scene)
                return manager;
        }

        return null;
    }

    private static bool IsButtonInScene(
        Button button,
        Scene scene
    )
    {
        if (button == null || button.gameObject == null)
            return false;

        if (!button.gameObject.scene.IsValid())
            return false;

        return button.gameObject.scene == scene;
    }

    private static bool IsTextInScene(
        TMP_Text text,
        Scene scene
    )
    {
        if (text == null || text.gameObject == null)
            return false;

        if (!text.gameObject.scene.IsValid())
            return false;

        return text.gameObject.scene == scene;
    }

    private static bool IsGameplayHUDTransform(
        Transform target,
        GameStateManager gameStateManager
    )
    {
        if (target == null || gameStateManager == null)
            return false;

        return IsUnderRoot(target, gameStateManager.scoreHUD) ||
               IsUnderRoot(target, gameStateManager.timeHUD) ||
               IsUnderRoot(target, gameStateManager.joystickHUD) ||
               IsUnderRoot(target, gameStateManager.dashHUD) ||
               IsUnderRoot(target, gameStateManager.cloneHUD) ||
               IsUnderRoot(target, gameStateManager.pauseButtonHUD);
    }

    private static bool IsUnderRoot(
        Transform target,
        GameObject root
    )
    {
        if (target == null || root == null)
            return false;

        Transform rootTransform = root.transform;

        return target == rootTransform ||
               target.IsChildOf(rootTransform);
    }

    private static void ApplyThemeColor(
        Button button,
        Color themeColor
    )
    {
        if (button == null)
            return;

        themeColor = NormalizeColor(themeColor);

        Graphic targetGraphic = button.targetGraphic;

        if (targetGraphic == null)
            targetGraphic = button.GetComponent<Graphic>();

        if (targetGraphic != null)
        {
            if (button.transition == Selectable.Transition.ColorTint)
            {
                ApplySelectableColorTintTheme(
                    button,
                    targetGraphic,
                    themeColor
                );
            }
            else
            {
                Color currentColor = targetGraphic.color;

                targetGraphic.color = new Color(
                    themeColor.r,
                    themeColor.g,
                    themeColor.b,
                    currentColor.a
                );
            }
        }

        ApplyButtonTextReadability(button);
    }

    private static void ApplyButtonTextReadability(Button button)
    {
        if (button == null)
            return;

        TMP_Text[] labels =
            button.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];

            if (label == null)
                continue;

            label.outlineColor = ButtonTextOutlineColor;
            label.outlineWidth = Mathf.Max(
                label.outlineWidth,
                MinimumButtonTextOutlineWidth
            );
        }
    }

    private static void ApplySelectableColorTintTheme(
        Selectable selectable,
        Graphic targetGraphic,
        Color themeColor
    )
    {
        if (selectable == null || targetGraphic == null)
            return;

        ColorBlock colors = selectable.colors;

        float normalAlpha = colors.normalColor.a;
        float highlightedAlpha = colors.highlightedColor.a;
        float pressedAlpha = colors.pressedColor.a;
        float selectedAlpha = colors.selectedColor.a;
        float disabledAlpha = colors.disabledColor.a;

        colors.normalColor =
            WithAlpha(themeColor, normalAlpha);

        colors.highlightedColor =
            WithAlpha(
                ShiftBrightness(themeColor, 1.10f),
                highlightedAlpha
            );

        colors.pressedColor =
            WithAlpha(
                ShiftBrightness(themeColor, 0.82f),
                pressedAlpha
            );

        colors.selectedColor =
            WithAlpha(
                ShiftBrightness(themeColor, 1.06f),
                selectedAlpha
            );

        colors.disabledColor =
            WithAlpha(
                ShiftBrightness(themeColor, 0.58f),
                disabledAlpha
            );

        // ColorTint applies the state color through CanvasRenderer.
        // Keep the Graphic itself neutral so the skin color is not
        // multiplied by itself.
        Color graphicColor = targetGraphic.color;
        targetGraphic.color = new Color(
            1f,
            1f,
            1f,
            graphicColor.a
        );

        selectable.colors = colors;
    }

    private static Color ShiftBrightness(
        Color color,
        float multiplier
    )
    {
        return new Color(
            Mathf.Clamp01(color.r * multiplier),
            Mathf.Clamp01(color.g * multiplier),
            Mathf.Clamp01(color.b * multiplier),
            color.a
        );
    }

    private static Color WithAlpha(
        Color color,
        float alpha
    )
    {
        color.a = alpha;
        return color;
    }

    private static Color NormalizeColor(Color color)
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

        color.r = Mathf.Clamp01(color.r);
        color.g = Mathf.Clamp01(color.g);
        color.b = Mathf.Clamp01(color.b);
        color.a = 1f;

        return color;
    }

    private static bool ContainsIgnoreCase(
        string source,
        string value
    )
    {
        if (string.IsNullOrEmpty(source) ||
            string.IsNullOrEmpty(value))
        {
            return false;
        }

        return source.IndexOf(
            value,
            System.StringComparison.OrdinalIgnoreCase
        ) >= 0;
    }
}
