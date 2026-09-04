using System.Collections;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dedicated Fateful Rush ending credits controller.
///
/// Flow:
/// Level 40 first completion
/// -> Result Menu confirmation CONFIRM
/// -> existing SceneTransition fades gameplay + gameplay music out
/// -> CreditsScene loads
/// -> CreditsScene's existing MenuMusicApply starts the normal menu playlist
///    with its own smooth fade-in
/// -> credits auto-scroll from top to bottom
/// -> player can drag/swipe/mouse-wheel to move faster
/// -> CONTINUE at the very bottom
/// -> existing SceneTransition fades CreditsScene + menu music out
/// -> MainMenu loads and its MenuMusicApply fades the menu playlist in.
///
/// Header colors follow the exact live NearStars color when
/// MainMenuStarColorRandomizer exists in CreditsScene.
/// </summary>
[DisallowMultipleComponent]
public sealed class FatefulRushCreditsController :
    MonoBehaviour,
    IBeginDragHandler,
    IEndDragHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IScrollHandler
{
    public const string PendingKey =
        "FatefulRush_Level40_Credits_Pending";

    private const string OpenedFromExtrasKey =
        "FatefulRush_Credits_Opened_From_Extras";

    private const string ReturnToExtrasKey =
        "FatefulRush_Credits_Return_To_Extras";

    private const string DefaultMainMenuSceneName =
        "MainMenu";

    private const string HeaderObjectPrefix =
        "Header_";

    private const string FinalThankYouObjectName =
        "ThankYouForPlaying";

    [Header("References")]
    [SerializeField]
    private ScrollRect scrollRect;

    [SerializeField]
    private RectTransform content;

    [SerializeField]
    private Button continueButton;

    [Header("Auto Scroll")]
    [Tooltip("Credits'in otomatik aşağı ilerleme hızı (pixel/saniye).")]
    [SerializeField, Min(1f)]
    private float autoScrollPixelsPerSecond = 42f;

    [Tooltip("Scene açıldıktan sonra otomatik akış başlamadan önce bekleme.")]
    [SerializeField, Min(0f)]
    private float initialDelay = 1f;

    [Tooltip("Oyuncu manuel scroll yaptıktan sonra otomatik akışın tekrar başlaması için bekleme.")]
    [SerializeField, Min(0f)]
    private float resumeAfterManualScrollDelay = 0.45f;

    [Header("Header Theme")]
    [Tooltip(
        "Header_CreatedBy, Header_Playtesting, Header_SpecialThanks " +
        "ve ThankYouForPlaying otomatik olarak NearStars rengine boyanır."
    )]
    [SerializeField]
    private bool colorHeadersFromNearStars = true;

    [SerializeField]
    private string mainMenuSceneName =
        DefaultMainMenuSceneName;

    private bool isDragging;
    private bool pointerHeld;
    private bool isLeavingScene;

    private float autoScrollStartTime;
    private float resumeAutoScrollAt;

    private Coroutine initializeRoutine;

    /// <summary>
    /// Credits, Main Menu'deki Extras panelinden manuel olarak acildiginda cagrilir.
    /// Level 40 ending akisi PendingKey ile ayri tutulur.
    /// </summary>
    public static void MarkOpenedFromExtras()
    {
        PlayerPrefs.SetInt(
            OpenedFromExtrasKey,
            1
        );

        PlayerPrefs.Save();
    }

    /// <summary>
    /// Credits CONTINUE sonrasi MainMenu yuklendiginde Extras'in geri acilmasi gerekip
    /// gerekmedigini bildirir. Istek sadece gercekten tuketildiginde silinmelidir.
    /// </summary>
    public static bool HasReturnToExtrasRequest()
    {
        return PlayerPrefs.GetInt(
            ReturnToExtrasKey,
            0
        ) == 1;
    }

    public static void ConsumeReturnToExtrasRequest()
    {
        PlayerPrefs.DeleteKey(
            ReturnToExtrasKey
        );

        PlayerPrefs.Save();
    }

    private void Awake()
    {
        ResolveReferences();

        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType =
            ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity =
            Mathf.Max(scrollRect.scrollSensitivity, 18f);

        continueButton.onClick.AddListener(
            ContinueToMainMenu
        );
    }

    private void Start()
    {
        initializeRoutine =
            StartCoroutine(
                InitializeCredits()
            );
    }

    private void ResolveReferences()
    {
        if (scrollRect == null)
        {
            scrollRect =
                GetComponentInChildren<ScrollRect>(
                    true
                );
        }

        if (content == null &&
            scrollRect != null)
        {
            content = scrollRect.content;
        }

        if (continueButton == null &&
            content != null)
        {
            Button[] buttons =
                content.GetComponentsInChildren<Button>(
                    true
                );

            for (int i = 0;
                 i < buttons.Length;
                 i++)
            {
                Button candidate =
                    buttons[i];

                if (candidate == null)
                    continue;

                if (string.Equals(
                        candidate.gameObject.name,
                        "ContinueButton",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continueButton = candidate;
                    break;
                }
            }
        }
    }

    private bool ValidateReferences()
    {
        if (scrollRect == null)
        {
            Debug.LogError(
                "[FatefulRushCreditsController] Scroll Rect atanmamış.",
                this
            );

            return false;
        }

        if (content == null)
        {
            Debug.LogError(
                "[FatefulRushCreditsController] Content atanmamış.",
                this
            );

            return false;
        }

        if (continueButton == null)
        {
            Debug.LogError(
                "[FatefulRushCreditsController] Continue Button atanmamış.",
                this
            );

            return false;
        }

        return true;
    }

    private IEnumerator InitializeCredits()
    {
        // Let VerticalLayoutGroup + ContentSizeFitter finish calculating
        // the long credits content before setting the initial position.
        yield return null;

        Canvas.ForceUpdateCanvases();

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            content
        );

        Canvas.ForceUpdateCanvases();

        // Exact top of the credits.
        scrollRect.verticalNormalizedPosition = 1f;
        scrollRect.velocity = Vector2.zero;

        if (colorHeadersFromNearStars)
        {
            ApplyNearStarsColorToHeaders();
        }

        autoScrollStartTime =
            Time.unscaledTime +
            initialDelay;

        resumeAutoScrollAt =
            autoScrollStartTime;

        initializeRoutine = null;
    }

    private void Update()
    {
        if (isLeavingScene ||
            scrollRect == null ||
            content == null)
        {
            return;
        }

        if (HandlePcCreditsInput())
            return;

        if (isDragging ||
            pointerHeld ||
            Time.unscaledTime < autoScrollStartTime ||
            Time.unscaledTime < resumeAutoScrollAt)
        {
            return;
        }

        float scrollableHeight =
            GetScrollableHeight();

        if (scrollableHeight <= 0.5f)
            return;

        float current =
            scrollRect.verticalNormalizedPosition;

        if (current <= 0f)
        {
            scrollRect.verticalNormalizedPosition = 0f;
            return;
        }

        float normalizedStep =
            autoScrollPixelsPerSecond *
            Time.unscaledDeltaTime /
            scrollableHeight;

        scrollRect.verticalNormalizedPosition =
            Mathf.Max(
                0f,
                current - normalizedStep
            );
    }

    private bool HandlePcCreditsInput()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;

        bool backPressed =
            (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);

        if (backPressed)
        {
            ContinueToMainMenu();
            return true;
        }

        if (keyboard != null)
        {
            if (keyboard.homeKey.wasPressedThisFrame)
            {
                scrollRect.verticalNormalizedPosition = 1f;
                DelayAutoScrollResume();
                return true;
            }

            if (keyboard.endKey.wasPressedThisFrame)
            {
                scrollRect.verticalNormalizedPosition = 0f;
                DelayAutoScrollResume();
                return true;
            }
        }

        float direction = 0f;
        bool largeStep = false;

        if (keyboard != null)
        {
            if (keyboard.downArrowKey.wasPressedThisFrame)
                direction = -1f;
            else if (keyboard.upArrowKey.wasPressedThisFrame)
                direction = 1f;
            else if (keyboard.pageDownKey.wasPressedThisFrame)
            {
                direction = -1f;
                largeStep = true;
            }
            else if (keyboard.pageUpKey.wasPressedThisFrame)
            {
                direction = 1f;
                largeStep = true;
            }
        }

        if (Mathf.Approximately(direction, 0f) && gamepad != null)
        {
            if (gamepad.dpad.down.wasPressedThisFrame)
                direction = -1f;
            else if (gamepad.dpad.up.wasPressedThisFrame)
                direction = 1f;
            else if (gamepad.rightShoulder.wasPressedThisFrame)
            {
                direction = -1f;
                largeStep = true;
            }
            else if (gamepad.leftShoulder.wasPressedThisFrame)
            {
                direction = 1f;
                largeStep = true;
            }
        }

        if (Mathf.Approximately(direction, 0f))
            return false;

        float scrollableHeight = GetScrollableHeight();
        if (scrollableHeight <= 0.5f)
            return true;

        float pixelStep = largeStep ? 420f : 110f;
        float normalizedStep = pixelStep / scrollableHeight;

        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
            scrollRect.verticalNormalizedPosition +
            direction * normalizedStep
        );

        scrollRect.velocity = Vector2.zero;
        DelayAutoScrollResume();
        return true;
    }

    /// <summary>
    /// Called automatically by the Button listener.
    /// The button is physically at the bottom of Content, so it naturally
    /// becomes available only after the player reaches the end (or scrolls
    /// there manually).
    /// </summary>
    public void ContinueToMainMenu()
    {
        if (isLeavingScene)
            return;

        string destination =
            string.IsNullOrWhiteSpace(
                mainMenuSceneName
            )
                ? DefaultMainMenuSceneName
                : mainMenuSceneName.Trim();

        if (!Application.CanStreamedLevelBeLoaded(
                destination
            ))
        {
            Debug.LogError(
                "[FatefulRushCreditsController] " +
                $"Main Menu scene Build Profiles > Scene List içinde yok: " +
                $"'{destination}'.",
                this
            );

            return;
        }

        if (SceneTransition.Instance != null &&
            SceneTransition.Instance.IsTransitioning)
        {
            return;
        }

        isLeavingScene = true;
        continueButton.interactable = false;

        // Credits'in hangi akisla acildigini transition baslamadan once kaydet.
        // Level 40 ending her zaman Main Menu'ye doner. Extras'tan manuel
        // acildiysa MainMenu yuklendikten sonra Extras paneli geri acilir.
        bool isLevel40Ending =
            PlayerPrefs.GetInt(
                PendingKey,
                0
            ) == 1;

        bool openedFromExtras =
            PlayerPrefs.GetInt(
                OpenedFromExtrasKey,
                0
            ) == 1;

        // Clear only when a valid transition is actually starting.
        // If the player quits halfway through Level 40 credits, PendingKey stays
        // and MainMenu's fallback can show CreditsScene again next launch.
        PlayerPrefs.DeleteKey(
            PendingKey
        );

        PlayerPrefs.DeleteKey(
            OpenedFromExtrasKey
        );

        if (!isLevel40Ending && openedFromExtras)
        {
            PlayerPrefs.SetInt(
                ReturnToExtrasKey,
                1
            );
        }
        else
        {
            // Level 40 ending must never accidentally restore Extras, even if
            // an old manual-Credits marker survived an interrupted session.
            PlayerPrefs.DeleteKey(
                ReturnToExtrasKey
            );
        }

        PlayerPrefs.Save();
        Time.timeScale = 1f;

        if (SceneTransition.Instance != null)
        {
            // SceneTransition already fades MenuMusicApply and the screen
            // together, then MainMenu's MenuMusicApply fades in on load.
            SceneTransition.Instance.LoadSceneWithFade(
                destination
            );

            return;
        }

        // Emergency fallback only. Normal builds should always have
        // SceneTransition because it bootstraps itself.
        SceneManager.LoadScene(
            destination
        );
    }

    private void ApplyNearStarsColorToHeaders()
    {
        Color themeColor =
            ResolveNearStarsThemeColor();

        // NearStars uses transparency; TMP headers should remain fully opaque.
        themeColor.a = 1f;

        TMP_Text[] texts =
            content.GetComponentsInChildren<TMP_Text>(
                true
            );

        for (int i = 0;
             i < texts.Length;
             i++)
        {
            TMP_Text text =
                texts[i];

            if (text == null)
                continue;

            string objectName =
                text.gameObject.name;

            bool isHeader =
                objectName.StartsWith(
                    HeaderObjectPrefix,
                    StringComparison.OrdinalIgnoreCase
                );

            bool isFinalThankYou =
                string.Equals(
                    objectName,
                    FinalThankYouObjectName,
                    StringComparison.OrdinalIgnoreCase
                );

            if (!isHeader &&
                !isFinalThankYou)
            {
                continue;
            }

            text.color = themeColor;
        }
    }

    private static Color ResolveNearStarsThemeColor()
    {
        MainMenuStarColorRandomizer randomizer =
            MainMenuStarColorRandomizer.Instance;

        if (randomizer != null)
        {
            return randomizer.CurrentColor;
        }

        // Safety fallback: this produces the same RGB theme source used by
        // MainMenuStarColorRandomizer even if that object was accidentally
        // omitted from CreditsScene.
        PlayerSkinCatalog catalog =
            ResolveSkinCatalog();

        PlayerSkinCatalog.SkinEntry selectedSkin =
            catalog != null
                ? catalog.GetSelectedSkin()
                : null;

        Color color =
            selectedSkin != null
                ? PlayerSkinCatalog.GetUIThemeColor(
                    selectedSkin
                )
                : Color.white;

        float highestChannel =
            Mathf.Max(
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

    private static PlayerSkinCatalog ResolveSkinCatalog()
    {
        if (PlayerSkinCatalog.LoadedInstance != null)
        {
            return PlayerSkinCatalog.LoadedInstance;
        }

        PlayerSkinCatalog[] catalogs =
            Resources.FindObjectsOfTypeAll<PlayerSkinCatalog>();

        if (catalogs == null ||
            catalogs.Length == 0)
        {
            return null;
        }

        for (int i = 0;
             i < catalogs.Length;
             i++)
        {
            PlayerSkinCatalog catalog =
                catalogs[i];

            if (catalog == null)
                continue;

            if (string.Equals(
                    catalog.name,
                    "PlayerSkinCatalog",
                    StringComparison.Ordinal))
            {
                return catalog;
            }
        }

        return catalogs[0];
    }

    public void OnBeginDrag(
        PointerEventData eventData)
    {
        isDragging = true;

        if (scrollRect != null)
            scrollRect.velocity = Vector2.zero;
    }

    public void OnEndDrag(
        PointerEventData eventData)
    {
        isDragging = false;
        DelayAutoScrollResume();
    }

    public void OnPointerDown(
        PointerEventData eventData)
    {
        pointerHeld = true;
    }

    public void OnPointerUp(
        PointerEventData eventData)
    {
        pointerHeld = false;
        DelayAutoScrollResume();
    }

    public void OnScroll(
        PointerEventData eventData)
    {
        DelayAutoScrollResume();
    }

    private void DelayAutoScrollResume()
    {
        resumeAutoScrollAt =
            Time.unscaledTime +
            resumeAfterManualScrollDelay;
    }

    private float GetScrollableHeight()
    {
        RectTransform viewport =
            scrollRect.viewport != null
                ? scrollRect.viewport
                : scrollRect.GetComponent<RectTransform>();

        if (viewport == null)
            return 0f;

        return Mathf.Max(
            0f,
            content.rect.height -
            viewport.rect.height
        );
    }

    private void OnDestroy()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(
                ContinueToMainMenu
            );
        }

        if (initializeRoutine != null)
        {
            StopCoroutine(
                initializeRoutine
            );

            initializeRoutine = null;
        }
    }

    private void OnValidate()
    {
        autoScrollPixelsPerSecond =
            Mathf.Max(
                1f,
                autoScrollPixelsPerSecond
            );

        initialDelay =
            Mathf.Max(
                0f,
                initialDelay
            );

        resumeAfterManualScrollDelay =
            Mathf.Max(
                0f,
                resumeAfterManualScrollDelay
            );

        if (string.IsNullOrWhiteSpace(
                mainMenuSceneName
            ))
        {
            mainMenuSceneName =
                DefaultMainMenuSceneName;
        }
    }
}
