using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class OptionsCategorySwitcher : MonoBehaviour
{
    private const string SavedCategoryKey = "OptionsLastCategory";

    private enum OptionsCategory
    {
        Audio = 0,
        Game = 1
    }

    [Header("Pages")]
    [Tooltip("Swipe sadece bu alanın içinde başlar.")]
    [SerializeField] private RectTransform swipeArea;
    [SerializeField] private RectTransform audioPage;
    [SerializeField] private RectTransform gamePage;

    [Header("Category Button")]
    [SerializeField] private Button switchCategoryButton;
    [SerializeField] private TMP_Text switchCategoryButtonText;
    [SerializeField] private string audioButtonLabel = "AUDIO";
    [SerializeField] private string gameButtonLabel = "GAME";

    [Header("Page Indicator")]
    [SerializeField] private TMP_Text pageIndicatorText;
    [SerializeField] private string audioPageIndicator = "1 / 2";
    [SerializeField] private string gamePageIndicator = "2 / 2";

    [Header("Swipe")]
    [SerializeField] private bool swipeEnabled = true;
    [SerializeField, Min(1f)] private float minSwipeDistance = 90f;
    [SerializeField, Range(0f, 1f)]
    private float maxVerticalToHorizontalRatio = 0.75f;

    [Header("Transition")]
    [SerializeField, Min(1f)] private float slideDistance = 850f;
    [SerializeField, Min(0.01f)] private float transitionDuration = 0.22f;

    private readonly List<RaycastResult> raycastResults =
        new List<RaycastResult>();

    private OptionsCategory currentCategory;
    private CanvasGroup audioCanvasGroup;
    private CanvasGroup gameCanvasGroup;
    private Canvas rootCanvas;
    private Coroutine transitionRoutine;

    private Vector2 pointerStartPosition;
    private bool isTrackingPointer;

    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();

        audioCanvasGroup = GetOrAddCanvasGroup(audioPage);
        gameCanvasGroup = GetOrAddCanvasGroup(gamePage);

        if (switchCategoryButton != null)
        {
            switchCategoryButton.onClick.RemoveListener(
                ToggleCategoryFromButton
            );

            switchCategoryButton.onClick.AddListener(
                ToggleCategoryFromButton
            );
        }
    }

    private void OnEnable()
    {
        StopTransition();

        currentCategory = LoadSavedCategory();
        ApplyCategoryInstant(currentCategory);
    }

    private void OnDisable()
    {
        StopTransition();
        isTrackingPointer = false;
    }

    private void OnDestroy()
    {
        if (switchCategoryButton != null)
        {
            switchCategoryButton.onClick.RemoveListener(
                ToggleCategoryFromButton
            );
        }
    }

    private void Update()
    {
        if (transitionRoutine != null)
            return;

        HandlePcNavigation();
    }

    public void ShowAudio()
    {
        SwitchToCategory(OptionsCategory.Audio);
    }

    public void ShowGame()
    {
        SwitchToCategory(OptionsCategory.Game);
    }

    public void ToggleCategory()
    {
        ToggleCategoryFromButton();
    }

    private void ToggleCategoryFromButton()
    {
        OptionsCategory targetCategory =
            currentCategory == OptionsCategory.Audio
                ? OptionsCategory.Game
                : OptionsCategory.Audio;

        SwitchToCategory(targetCategory);
    }

    private void HandlePcNavigation()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.pageUpKey.wasPressedThisFrame ||
                Keyboard.current.qKey.wasPressedThisFrame)
            {
                ShowAudio();
                return;
            }

            if (Keyboard.current.pageDownKey.wasPressedThisFrame ||
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                ShowGame();
                return;
            }
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftShoulder.wasPressedThisFrame)
            {
                ShowAudio();
                return;
            }

            if (Gamepad.current.rightShoulder.wasPressedThisFrame)
            {
                ShowGame();
                return;
            }
        }

        if (Mouse.current != null)
        {
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (wheel > 0.01f)
                ShowAudio();
            else if (wheel < -0.01f)
                ShowGame();
        }

        if (swipeEnabled)
            HandleMouseSwipe();
    }

    private void HandleMouseSwipe()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            BeginPointerTracking(
                Mouse.current.position.ReadValue()
            );
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndPointerTracking(
                Mouse.current.position.ReadValue()
            );
        }
    }

    private void BeginPointerTracking(Vector2 screenPosition)
    {
        isTrackingPointer = false;

        if (!IsInsideSwipeArea(screenPosition))
            return;

        // Slider veya buton üstünden başlayan hareketler swipe sayılmaz.
        // Böylece ses seviyesi sürüklerken sayfa yanlışlıkla değişmez.
        if (IsPointerOverSelectable(screenPosition))
            return;

        pointerStartPosition = screenPosition;
        isTrackingPointer = true;
    }

    private void EndPointerTracking(Vector2 screenPosition)
    {
        if (!isTrackingPointer)
            return;

        isTrackingPointer = false;

        Vector2 swipeDelta =
            screenPosition - pointerStartPosition;

        float horizontalDistance =
            Mathf.Abs(swipeDelta.x);

        float verticalDistance =
            Mathf.Abs(swipeDelta.y);

        if (horizontalDistance < minSwipeDistance)
            return;

        if (verticalDistance >
            horizontalDistance * maxVerticalToHorizontalRatio)
        {
            return;
        }

        // There are only two pages, so a valid horizontal swipe in
        // either direction switches to the other page. This makes
        // page 2 reachable with both left and right swipes.
        OptionsCategory targetCategory =
            currentCategory == OptionsCategory.Audio
                ? OptionsCategory.Game
                : OptionsCategory.Audio;

        // Keep the slide animation consistent with the swipe direction.
        int swipeDirection = swipeDelta.x < 0f ? 1 : -1;

        SwitchToCategory(
            targetCategory,
            swipeDirection
        );
    }

    private void SwitchToCategory(
        OptionsCategory targetCategory,
        int? directionOverride = null)
    {
        if (transitionRoutine != null ||
            targetCategory == currentCategory)
        {
            return;
        }

        if (switchCategoryButton != null)
            switchCategoryButton.interactable = false;

        OptionsCategory previousCategory =
            currentCategory;

        currentCategory = targetCategory;
        SaveCurrentCategory();
        UpdateSwitchButtonText();
        UpdatePageIndicator();

        int direction = directionOverride ??
            (targetCategory == OptionsCategory.Game
                ? 1
                : -1);

        transitionRoutine = StartCoroutine(
            AnimateCategorySwitch(
                previousCategory,
                targetCategory,
                direction
            )
        );
    }

    private IEnumerator AnimateCategorySwitch(
        OptionsCategory previousCategory,
        OptionsCategory targetCategory,
        int direction)
    {
        RectTransform previousPage =
            GetPage(previousCategory);

        RectTransform targetPage =
            GetPage(targetCategory);

        CanvasGroup previousGroup =
            GetCanvasGroup(previousCategory);

        CanvasGroup targetGroup =
            GetCanvasGroup(targetCategory);

        if (previousPage == null || targetPage == null ||
            previousGroup == null || targetGroup == null)
        {
            ApplyCategoryInstant(targetCategory);
            transitionRoutine = null;

            if (switchCategoryButton != null)
                switchCategoryButton.interactable = true;

            yield break;
        }

        previousPage.gameObject.SetActive(true);
        targetPage.gameObject.SetActive(true);

        previousPage.anchoredPosition = Vector2.zero;
        targetPage.anchoredPosition =
            Vector2.right * slideDistance * direction;

        SetCanvasGroupState(previousGroup, 1f, false);
        SetCanvasGroupState(targetGroup, 0f, false);

        float timer = 0f;
        float safeDuration =
            Mathf.Max(0.01f, transitionDuration);

        Vector2 previousTargetPosition =
            Vector2.left * slideDistance * direction;

        while (timer < safeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(timer / safeDuration);

            float easedProgress =
                Mathf.SmoothStep(0f, 1f, progress);

            previousPage.anchoredPosition =
                Vector2.LerpUnclamped(
                    Vector2.zero,
                    previousTargetPosition,
                    easedProgress
                );

            targetPage.anchoredPosition =
                Vector2.LerpUnclamped(
                    Vector2.right * slideDistance * direction,
                    Vector2.zero,
                    easedProgress
                );

            previousGroup.alpha =
                1f - easedProgress;

            targetGroup.alpha =
                easedProgress;

            yield return null;
        }

        previousPage.anchoredPosition = Vector2.zero;
        targetPage.anchoredPosition = Vector2.zero;

        SetCanvasGroupState(previousGroup, 0f, false);
        previousPage.gameObject.SetActive(false);

        SetCanvasGroupState(targetGroup, 1f, true);
        targetPage.gameObject.SetActive(true);

        transitionRoutine = null;

        if (switchCategoryButton != null)
            switchCategoryButton.interactable = true;
    }

    private void ApplyCategoryInstant(
        OptionsCategory category)
    {
        bool showAudio =
            category == OptionsCategory.Audio;

        ApplyPageInstant(
            audioPage,
            audioCanvasGroup,
            showAudio
        );

        ApplyPageInstant(
            gamePage,
            gameCanvasGroup,
            !showAudio
        );

        UpdateSwitchButtonText();
        UpdatePageIndicator();
    }

    private static void ApplyPageInstant(
        RectTransform page,
        CanvasGroup canvasGroup,
        bool visible)
    {
        if (page == null || canvasGroup == null)
            return;

        page.anchoredPosition = Vector2.zero;
        page.gameObject.SetActive(visible);

        SetCanvasGroupState(
            canvasGroup,
            visible ? 1f : 0f,
            visible
        );
    }

    private void UpdateSwitchButtonText()
    {
        if (switchCategoryButtonText == null)
            return;

        switchCategoryButtonText.text =
            currentCategory == OptionsCategory.Audio
                ? gameButtonLabel
                : audioButtonLabel;
    }

    private void UpdatePageIndicator()
    {
        if (pageIndicatorText == null)
            return;

        pageIndicatorText.text =
            currentCategory == OptionsCategory.Audio
                ? audioPageIndicator
                : gamePageIndicator;
    }

    private OptionsCategory LoadSavedCategory()
    {
        int savedValue = PlayerPrefs.GetInt(
            SavedCategoryKey,
            (int)OptionsCategory.Audio
        );

        return savedValue == (int)OptionsCategory.Game
            ? OptionsCategory.Game
            : OptionsCategory.Audio;
    }

    private void SaveCurrentCategory()
    {
        PlayerPrefs.SetInt(
            SavedCategoryKey,
            (int)currentCategory
        );

        PlayerPrefs.Save();
    }

    private bool IsInsideSwipeArea(
        Vector2 screenPosition)
    {
        if (swipeArea == null)
            return true;

        return RectTransformUtility
            .RectangleContainsScreenPoint(
                swipeArea,
                screenPosition,
                GetUICamera()
            );
    }

    private bool IsPointerOverSelectable(
        Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData pointerData =
            new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

        raycastResults.Clear();
        EventSystem.current.RaycastAll(
            pointerData,
            raycastResults
        );

        foreach (RaycastResult result in raycastResults)
        {
            if (result.gameObject == null)
                continue;

            Selectable selectable =
                result.gameObject
                    .GetComponentInParent<Selectable>();

            if (selectable != null &&
                selectable.IsInteractable())
            {
                return true;
            }
        }

        return false;
    }

    private Camera GetUICamera()
    {
        if (rootCanvas == null ||
            rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return rootCanvas.worldCamera;
    }

    private RectTransform GetPage(
        OptionsCategory category)
    {
        return category == OptionsCategory.Audio
            ? audioPage
            : gamePage;
    }

    private CanvasGroup GetCanvasGroup(
        OptionsCategory category)
    {
        return category == OptionsCategory.Audio
            ? audioCanvasGroup
            : gameCanvasGroup;
    }

    private static CanvasGroup GetOrAddCanvasGroup(
        RectTransform page)
    {
        if (page == null)
            return null;

        CanvasGroup canvasGroup =
            page.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = page.gameObject.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    private static void SetCanvasGroupState(
        CanvasGroup canvasGroup,
        float alpha,
        bool interactable)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = alpha;
        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = interactable;
    }

    private void StopTransition()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (switchCategoryButton != null)
            switchCategoryButton.interactable = true;
    }

    private void OnValidate()
    {
        minSwipeDistance =
            Mathf.Max(1f, minSwipeDistance);

        slideDistance =
            Mathf.Max(1f, slideDistance);

        transitionDuration =
            Mathf.Max(0.01f, transitionDuration);

        maxVerticalToHorizontalRatio =
            Mathf.Clamp01(
                maxVerticalToHorizontalRatio
            );
    }
}