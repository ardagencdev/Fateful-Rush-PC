using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelSelectPanel : MonoBehaviour
{
    public bool IsOpen => levelSelectPanel != null && levelSelectPanel.activeInHierarchy;

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject levelSelectPanel;
    [SerializeField] private UIPanelFadeSwitcher fadeSwitcher;

    [Header("Level Buttons")]
    [SerializeField] private RectTransform levelButtonsContainer;
    [SerializeField] private LevelButtonUI levelButtonPrefab;
    [SerializeField] private LevelConfig[] levels;

    [Header("Pagination")]
    [SerializeField, Min(1)] private int levelsPerPage = 5;
    [SerializeField] private TMP_Text pageIndicatorText;
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;

    [Header("Swipe")]
    [SerializeField, Min(1f)] private float minSwipeDistance = 80f;

    [Header("Page Animation")]
    [SerializeField, Min(0f)] private float pageSlideDistance = 700f;
    [SerializeField, Min(0.01f)] private float pageAnimDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float navigationAnimDuration = 0.18f;
    [SerializeField, Range(0.5f, 1f)] private float hiddenNavigationScale = 0.85f;

    [Header("Mission Briefing")]
    [SerializeField] private MissionBriefingPanelUI missionBriefingPanel;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "a";

    private readonly List<LevelButtonUI> createdButtons =
        new List<LevelButtonUI>();

    private int currentPageIndex;
    private int totalPageCount;

    private Vector2 dragStartPosition;
    private Vector2 containerStartPosition;

    private bool isDragging;
    private bool isLoadingLevel;

    private CanvasGroup containerGroup;
    private CanvasGroup previousPageGroup;
    private CanvasGroup nextPageGroup;

    private Vector3 previousPageBaseScale = Vector3.one;
    private Vector3 nextPageBaseScale = Vector3.one;

    private Coroutine pageRoutine;
    private Coroutine previousButtonRoutine;
    private Coroutine nextButtonRoutine;

    private UIButtonSound previousPageSound;
    private UIButtonSound nextPageSound;

    private void Awake()
    {
        PrepareContainer();
        PreparePageButtons();

        // Panel başlangıçta kapalı olabileceği için burada coroutine çalıştırmıyoruz.
        // Circular pagination: both navigation buttons stay visible.
        SetNavigationButtonInstant(
            previousPageButton,
            previousPageGroup,
            true,
            previousPageBaseScale
        );

        SetNavigationButtonInstant(
            nextPageButton,
            nextPageGroup,
            true,
            nextPageBaseScale
        );
    }

    private void OnEnable()
    {
        isLoadingLevel = false;

        // Obje aktif olduğu anda son hesaplanan sayfaya göre
        // navigation butonlarını kesin olarak düzelt.
        RefreshPageUI(false);
        SetPageButtonsInteractable(true);
    }

    private void OnDestroy()
    {
        StopAllAnimations();

        if (previousPageButton != null)
            previousPageButton.onClick.RemoveListener(PreviousPage);

        if (nextPageButton != null)
            nextPageButton.onClick.RemoveListener(NextPage);
    }

    private void Update()
    {
        if (levelSelectPanel == null ||
            !levelSelectPanel.activeSelf ||
            pageRoutine != null)
        {
            return;
        }

        if (missionBriefingPanel != null &&
            missionBriefingPanel.IsOpen)
        {
            isDragging = false;
            return;
        }

        HandlePcPageNavigation();
    }

    public void OpenPanel()
    {
        if (!ValidatePanelReferences())
            return;

        StopAllAnimations();

        CalculatePageCount();
        OpenLatestUnlockedPage();

        // Important: do not instantiate the level buttons while the whole
        // Mission Select panel is inactive. When a button is created under an
        // inactive parent, its OnEnable lifecycle is delayed until the panel
        // opens. That can make its own scale/reset logic fight the panel intro
        // transition on the very first page. Page changes do not have this
        // problem because the panel is already active.
        //
        // Clear old page objects first, activate the panel through the shared
        // transition, then create the new buttons while their parent is active.
        ClearCreatedButtons();
        ResetContainerVisuals();

        ApplyCurrentPageStarProgression();

        SwitchPanels(
            mainMenuPanel,
            levelSelectPanel
        );

        CreateCurrentPageButtons();
        RefreshPageUI(false);
        SetPageButtonsInteractable(true);
    }

    public void ClosePanel()
    {
        if (!ValidatePanelReferences())
            return;

        StopAllAnimations();
        ResetContainerVisuals();

        MainMenuStarColorRandomizer.Instance?
            .ShowMainMenuColor();

        SwitchPanels(
            levelSelectPanel,
            mainMenuPanel
        );
    }

    public bool IsMissionBriefingOpen =>
        missionBriefingPanel != null &&
        missionBriefingPanel.IsOpen;

    public void ShowMissionBriefing(LevelConfig config)
    {
        if (IsMissionBriefingOpen)
            return;

        if (config == null)
        {
            Debug.LogWarning(
                "LevelSelectPanel received a null LevelConfig.",
                this
            );
            return;
        }

        if (missionBriefingPanel == null)
        {
            Debug.LogError(
                "LevelSelectPanel missionBriefingPanel is missing.",
                this
            );
            return;
        }

        isDragging = false;

        missionBriefingPanel.Show(
            config,
            StartLevel,
            OnMissionBriefingClosed
        );
    }

    public void StartLevel(LevelConfig config)
    {
        if (isLoadingLevel)
            return;

        if (config == null)
        {
            Debug.LogWarning(
                "LevelSelectPanel received a null LevelConfig.",
                this
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError(
                "LevelSelectPanel game scene name is empty.",
                this
            );
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError(
                $"Scene '{gameSceneName}' could not be loaded. " +
                "Make sure it exists in Build Profiles.",
                this
            );
            return;
        }

        isLoadingLevel = true;
        SelectedLevelData.SetMission(config);

        if (SceneTransition.Instance != null)
        {
            SceneTransition.Instance.LoadSceneWithFade(
                gameSceneName
            );
        }
        else
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    public void RefreshButtons()
    {
        CleanupButtonList();

        foreach (LevelButtonUI button in createdButtons)
        {
            if (button != null)
                button.Refresh();
        }
    }

    public IReadOnlyList<LevelConfig> GetConfiguredLevels()
    {
        // Main menu Continue sistemi, LevelSelectPanel ile ayni
        // LevelConfig listesini kullansin; ikinci bir 40 elemanli liste tutulmasin.
        return GetValidLevels();
    }

    private void OnMissionBriefingClosed()
    {
        RefreshButtons();
        ApplyCurrentPageStarProgression();
    }

    private void ApplyCurrentPageStarProgression()
    {
        MainMenuStarColorRandomizer.Instance?
            .ShowLevelSelectionPage(
                currentPageIndex,
                totalPageCount
            );
    }

    public void NextPage()
    {
        if (!CanNavigatePages())
            return;

        int nextPageIndex =
            (currentPageIndex + 1) % totalPageCount;

        // Play only after the navigation request is accepted.
        // Rejected spam clicks therefore stay silent.
        nextPageSound?.PlayConfiguredSound();

        StartPageTransition(
            nextPageIndex,
            -1
        );
    }

    public void PreviousPage()
    {
        if (!CanNavigatePages())
            return;

        int previousPageIndex =
            (currentPageIndex - 1 + totalPageCount) % totalPageCount;

        // Play only after the navigation request is accepted.
        previousPageSound?.PlayConfiguredSound();

        StartPageTransition(
            previousPageIndex,
            1
        );
    }

    private bool CanNavigatePages()
    {
        return pageRoutine == null &&
               totalPageCount > 1 &&
               levelSelectPanel != null &&
               levelSelectPanel.activeSelf &&
               !IsMissionBriefingOpen;
    }

    private void PrepareContainer()
    {
        if (levelButtonsContainer == null)
            return;

        containerStartPosition =
            levelButtonsContainer.anchoredPosition;

        containerGroup =
            levelButtonsContainer.GetComponent<CanvasGroup>();

        if (containerGroup == null)
        {
            containerGroup =
                levelButtonsContainer.gameObject
                    .AddComponent<CanvasGroup>();
        }
    }

    private void PreparePageButtons()
    {
        if (previousPageButton != null)
        {
            previousPageSound =
                ConfigurePageNavigationSound(previousPageButton);

            previousPageButton.onClick.RemoveListener(
                PreviousPage
            );
            previousPageButton.onClick.AddListener(
                PreviousPage
            );

            previousPageBaseScale =
                previousPageButton.transform.localScale;
        }

        if (nextPageButton != null)
        {
            nextPageSound =
                ConfigurePageNavigationSound(nextPageButton);

            nextPageButton.onClick.RemoveListener(
                NextPage
            );
            nextPageButton.onClick.AddListener(
                NextPage
            );

            nextPageBaseScale =
                nextPageButton.transform.localScale;
        }

        previousPageGroup =
            GetOrAddCanvasGroup(previousPageButton);

        nextPageGroup =
            GetOrAddCanvasGroup(nextPageButton);
    }

    private static UIButtonSound ConfigurePageNavigationSound(Button button)
    {
        if (button == null)
            return null;

        UIButtonSound buttonSound =
            button.GetComponent<UIButtonSound>();

        if (buttonSound == null)
        {
            buttonSound =
                button.gameObject.AddComponent<UIButtonSound>();
        }

        buttonSound.enabled = true;

        // Page navigation owns its SFX. UIButtonSound must not also
        // auto-play from Button.onClick or the same click can sound twice.
        buttonSound.ConfigureAsPageNavigation(manualPlayback: true);

        return buttonSound;
    }

    private static CanvasGroup GetOrAddCanvasGroup(
        Button button
    )
    {
        if (button == null)
            return null;

        CanvasGroup group =
            button.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group =
                button.gameObject.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private void CalculatePageCount()
    {
        int validLevelCount = 0;

        if (levels != null)
        {
            foreach (LevelConfig level in levels)
            {
                if (level != null)
                    validLevelCount++;
            }
        }

        totalPageCount = Mathf.Max(
            1,
            Mathf.CeilToInt(
                validLevelCount /
                (float)levelsPerPage
            )
        );

        currentPageIndex = Mathf.Clamp(
            currentPageIndex,
            0,
            totalPageCount - 1
        );
    }

    private void OpenLatestUnlockedPage()
    {
        int unlockedLevel =
            PlayerPrefs.GetInt(
                "UnlockedLevel",
                1
            );

        int safeUnlockedLevel =
            Mathf.Max(1, unlockedLevel);

        currentPageIndex =
            (safeUnlockedLevel - 1) /
            levelsPerPage;

        currentPageIndex = Mathf.Clamp(
            currentPageIndex,
            0,
            totalPageCount - 1
        );
    }

    private void CreateCurrentPageButtons()
    {
        if (!ValidateButtonReferences())
            return;

        ClearCreatedButtons();

        List<LevelConfig> validLevels =
            GetValidLevels();

        int firstIndex =
            currentPageIndex * levelsPerPage;

        int lastIndex = Mathf.Min(
            firstIndex + levelsPerPage,
            validLevels.Count
        );

        for (int i = firstIndex; i < lastIndex; i++)
        {
            LevelButtonUI levelButton =
                Instantiate(
                    levelButtonPrefab,
                    levelButtonsContainer
                );

            levelButton.Setup(
                validLevels[i],
                this
            );

            createdButtons.Add(levelButton);
        }
    }

    private List<LevelConfig> GetValidLevels()
    {
        List<LevelConfig> validLevels =
            new List<LevelConfig>();

        if (levels == null)
            return validLevels;

        foreach (LevelConfig levelConfig in levels)
        {
            if (levelConfig != null)
                validLevels.Add(levelConfig);
        }

        validLevels.Sort(
            (left, right) =>
                left.levelNumber.CompareTo(
                    right.levelNumber
                )
        );

        return validLevels;
    }

    private void StartPageTransition(
        int newPageIndex,
        int direction
    )
    {
        if (!gameObject.activeInHierarchy)
            return;

        // Lock immediately, before any transition animation can start.
        SetPageButtonsInteractable(false);

        pageRoutine = StartCoroutine(
            PageTransitionRoutine(
                newPageIndex,
                direction
            )
        );
    }

    private IEnumerator PageTransitionRoutine(
        int newPageIndex,
        int direction
    )
    {
        isDragging = false;

        float timer = 0f;

        while (timer < pageAnimDuration)
        {
            timer += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                timer / pageAnimDuration
            );

            float eased = EaseOutCubic(progress);

            if (levelButtonsContainer != null)
            {
                levelButtonsContainer.anchoredPosition =
                    containerStartPosition +
                    Vector2.right *
                    direction *
                    pageSlideDistance *
                    eased;
            }

            if (containerGroup != null)
                containerGroup.alpha = 1f - eased;

            yield return null;
        }

        currentPageIndex = newPageIndex;

        ApplyCurrentPageStarProgression();
        CreateCurrentPageButtons();

        // Panel aktifken sayfa sınırına göre okları smooth değiştir.
        RefreshPageUI(true);

        if (levelButtonsContainer != null)
        {
            levelButtonsContainer.anchoredPosition =
                containerStartPosition -
                Vector2.right *
                direction *
                pageSlideDistance;
        }

        timer = 0f;

        while (timer < pageAnimDuration)
        {
            timer += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                timer / pageAnimDuration
            );

            float eased = EaseOutCubic(progress);

            if (levelButtonsContainer != null)
            {
                levelButtonsContainer.anchoredPosition =
                    Vector2.Lerp(
                        containerStartPosition -
                        Vector2.right *
                        direction *
                        pageSlideDistance,
                        containerStartPosition,
                        eased
                    );
            }

            if (containerGroup != null)
                containerGroup.alpha = eased;

            yield return null;
        }

        ResetContainerVisuals();

        pageRoutine = null;
        SetPageButtonsInteractable(true);
    }

    private void RefreshPageUI(
        bool animateNavigation
    )
    {
        if (pageIndicatorText != null)
        {
            pageIndicatorText.text =
                $"{currentPageIndex + 1} / {totalPageCount}";
        }

        // Circular pagination: when there is more than one page,
        // both arrows are always available (1 <-> last page).
        bool canGoPrevious =
            totalPageCount > 1;

        bool canGoNext =
            totalPageCount > 1;

        RefreshNavigationButton(
            previousPageButton,
            previousPageGroup,
            canGoPrevious,
            previousPageBaseScale,
            true,
            animateNavigation
        );

        RefreshNavigationButton(
            nextPageButton,
            nextPageGroup,
            canGoNext,
            nextPageBaseScale,
            false,
            animateNavigation
        );
    }

    private void RefreshNavigationButton(
        Button button,
        CanvasGroup group,
        bool show,
        Vector3 baseScale,
        bool isPrevious,
        bool animate
    )
    {
        if (button == null || group == null)
            return;

        StopNavigationRoutine(isPrevious);

        if (!animate ||
            !gameObject.activeInHierarchy ||
            levelSelectPanel == null ||
            !levelSelectPanel.activeSelf)
        {
            SetNavigationButtonInstant(
                button,
                group,
                show,
                baseScale
            );
            return;
        }

        Coroutine routine = StartCoroutine(
            NavigationButtonRoutine(
                button,
                group,
                show,
                baseScale,
                isPrevious
            )
        );

        if (isPrevious)
            previousButtonRoutine = routine;
        else
            nextButtonRoutine = routine;
    }

    private IEnumerator NavigationButtonRoutine(
        Button button,
        CanvasGroup group,
        bool show,
        Vector3 baseScale,
        bool isPrevious
    )
    {
        if (show && !button.gameObject.activeSelf)
        {
            group.alpha = 0f;
            button.transform.localScale =
                baseScale * hiddenNavigationScale;

            button.gameObject.SetActive(true);
        }

        button.interactable = false;
        group.interactable = false;
        group.blocksRaycasts = false;

        float startAlpha = group.alpha;
        float targetAlpha = show ? 1f : 0f;

        Vector3 startScale =
            button.transform.localScale;

        Vector3 targetScale =
            show
                ? baseScale
                : baseScale * hiddenNavigationScale;

        float timer = 0f;

        while (timer < navigationAnimDuration)
        {
            timer += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                timer / navigationAnimDuration
            );

            float eased =
                EaseOutCubic(progress);

            group.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                eased
            );

            button.transform.localScale =
                Vector3.Lerp(
                    startScale,
                    targetScale,
                    eased
                );

            yield return null;
        }

        group.alpha = targetAlpha;
        button.transform.localScale =
            targetScale;

        bool canInteract =
            show &&
            pageRoutine == null;

        button.interactable = canInteract;
        group.interactable = canInteract;
        group.blocksRaycasts = canInteract;

        if (!show)
            button.gameObject.SetActive(false);

        if (isPrevious)
            previousButtonRoutine = null;
        else
            nextButtonRoutine = null;
    }

    private void SetNavigationButtonInstant(
        Button button,
        CanvasGroup group,
        bool show,
        Vector3 baseScale
    )
    {
        if (button == null || group == null)
            return;

        button.gameObject.SetActive(show);
        group.alpha = show ? 1f : 0f;
        group.interactable = show;
        group.blocksRaycasts = show;
        button.interactable = show;

        button.transform.localScale =
            show
                ? baseScale
                : baseScale * hiddenNavigationScale;
    }

    private void SetPageButtonsInteractable(
        bool interactable
    )
    {
        bool canGoPrevious =
            totalPageCount > 1;

        bool canGoNext =
            totalPageCount > 1;

        if (previousPageButton != null &&
            previousPageButton.gameObject.activeSelf)
        {
            previousPageButton.interactable =
                interactable &&
                canGoPrevious;
        }

        if (previousPageGroup != null)
        {
            previousPageGroup.interactable =
                interactable &&
                canGoPrevious;

            previousPageGroup.blocksRaycasts =
                interactable &&
                canGoPrevious;
        }

        if (nextPageButton != null &&
            nextPageButton.gameObject.activeSelf)
        {
            nextPageButton.interactable =
                interactable &&
                canGoNext;
        }

        if (nextPageGroup != null)
        {
            nextPageGroup.interactable =
                interactable &&
                canGoNext;

            nextPageGroup.blocksRaycasts =
                interactable &&
                canGoNext;
        }

        if (containerGroup != null)
        {
            containerGroup.interactable =
                interactable;

            containerGroup.blocksRaycasts =
                interactable;
        }
    }

    private void HandlePcPageNavigation()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.pageDownKey.wasPressedThisFrame ||
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                NextPage();
                return;
            }

            if (Keyboard.current.pageUpKey.wasPressedThisFrame ||
                Keyboard.current.qKey.wasPressedThisFrame)
            {
                PreviousPage();
                return;
            }
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.rightShoulder.wasPressedThisFrame)
            {
                NextPage();
                return;
            }

            if (Gamepad.current.leftShoulder.wasPressedThisFrame)
            {
                PreviousPage();
                return;
            }
        }

        if (Mouse.current != null)
        {
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (wheel < -0.01f)
                NextPage();
            else if (wheel > 0.01f)
                PreviousPage();
        }

        // Mouse drag remains available as a desktop convenience.
        HandleMouseSwipe();
    }

    private void HandleMouseSwipe()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragStartPosition =
                Mouse.current.position.ReadValue();

            isDragging = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame &&
            isDragging)
        {
            Vector2 dragEndPosition =
                Mouse.current.position.ReadValue();

            isDragging = false;
            TrySwipe(dragEndPosition);
        }
    }

    private void TrySwipe(
        Vector2 dragEndPosition
    )
    {
        float swipeX =
            dragEndPosition.x -
            dragStartPosition.x;

        if (Mathf.Abs(swipeX) <
            minSwipeDistance)
        {
            return;
        }

        if (swipeX < 0f)
            NextPage();
        else
            PreviousPage();
    }

    private void ClearCreatedButtons()
    {
        foreach (LevelButtonUI button in createdButtons)
        {
            if (button == null)
                continue;

            // Destroy is deferred until the end of the frame. Disable first so
            // a panel re-open in the same frame cannot re-enable an old button
            // and restart its scale animation.
            button.gameObject.SetActive(false);
            Destroy(button.gameObject);
        }

        createdButtons.Clear();

        if (levelButtonsContainer == null)
            return;

        LevelButtonUI[] existingButtons =
            levelButtonsContainer
                .GetComponentsInChildren<LevelButtonUI>(
                    true
                );

        foreach (LevelButtonUI button in existingButtons)
        {
            if (button == null)
                continue;

            if (button.transform.parent !=
                levelButtonsContainer)
            {
                continue;
            }

            button.gameObject.SetActive(false);
            Destroy(button.gameObject);
        }
    }

    private void CleanupButtonList()
    {
        for (int i = createdButtons.Count - 1;
             i >= 0;
             i--)
        {
            if (createdButtons[i] == null)
                createdButtons.RemoveAt(i);
        }
    }

    private void ResetContainerVisuals()
    {
        if (levelButtonsContainer != null)
        {
            levelButtonsContainer.anchoredPosition =
                containerStartPosition;
        }

        if (containerGroup != null)
            containerGroup.alpha = 1f;
    }

    private void StopAllAnimations()
    {
        if (pageRoutine != null)
        {
            StopCoroutine(pageRoutine);
            pageRoutine = null;
        }

        StopNavigationRoutine(true);
        StopNavigationRoutine(false);

        isDragging = false;
    }

    private void StopNavigationRoutine(
        bool isPrevious
    )
    {
        Coroutine routine =
            isPrevious
                ? previousButtonRoutine
                : nextButtonRoutine;

        if (routine == null)
            return;

        StopCoroutine(routine);

        if (isPrevious)
            previousButtonRoutine = null;
        else
            nextButtonRoutine = null;
    }

    private void SwitchPanels(
        GameObject panelToHide,
        GameObject panelToShow
    )
    {
        if (fadeSwitcher != null)
        {
            fadeSwitcher.SwitchPanel(
                panelToHide,
                panelToShow
            );
            return;
        }

        if (panelToHide != null)
            panelToHide.SetActive(false);

        if (panelToShow != null)
            panelToShow.SetActive(true);
    }

    private bool ValidatePanelReferences()
    {
        if (mainMenuPanel == null ||
            levelSelectPanel == null)
        {
            Debug.LogError(
                "LevelSelectPanel panel references are missing.",
                this
            );
            return false;
        }

        return true;
    }

    private bool ValidateButtonReferences()
    {
        if (levelButtonsContainer == null)
        {
            Debug.LogError(
                "LevelSelectPanel levelButtonsContainer is missing.",
                this
            );
            return false;
        }

        if (levelButtonPrefab == null)
        {
            Debug.LogError(
                "LevelSelectPanel levelButtonPrefab is missing.",
                this
            );
            return false;
        }

        return true;
    }

    private void OnValidate()
    {
        levelsPerPage =
            Mathf.Max(1, levelsPerPage);

        pageAnimDuration =
            Mathf.Max(0.01f, pageAnimDuration);

        navigationAnimDuration =
            Mathf.Max(0.01f, navigationAnimDuration);

        hiddenNavigationScale =
            Mathf.Clamp(
                hiddenNavigationScale,
                0.5f,
                1f
            );

        if (levels == null)
            return;

        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] == null)
                continue;

            for (int j = i + 1;
                 j < levels.Length;
                 j++)
            {
                if (levels[j] == null)
                    continue;

                if (levels[i].levelNumber ==
                    levels[j].levelNumber)
                {
                    Debug.LogWarning(
                        "Duplicate level number found: " +
                        levels[i].levelNumber,
                        this
                    );
                }
            }
        }
    }

    private static float EaseOutCubic(
        float value
    )
    {
        float inverse =
            1f - Mathf.Clamp01(value);

        return
            1f -
            inverse *
            inverse *
            inverse;
    }
}