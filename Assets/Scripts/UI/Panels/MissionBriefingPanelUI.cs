using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MissionBriefingPanelUI : MonoBehaviour
{
    private const string CompletedLevelKeyPrefix = "CompletedLevel_";

    public static MissionBriefingPanelUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject briefingPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button startButton;

    [Header("Mission Information")]
    [SerializeField] private TMP_Text levelTitleText;
    [SerializeField] private TMP_Text modeText;
    [SerializeField] private TMP_Text pageDescriptionText;
    [SerializeField] private TMP_Text pageIndicatorText;

    [Header("Difficulty")]
    [Tooltip(
        "Soldan sağa beş yıldızın dolu Image bileşenlerini ekle. " +
        "Yıldızlar zorluk değerine göre tamamen açılır veya kapanır."
    )]
    [SerializeField] private Image[] difficultyStarFills = new Image[5];

    [Header("Best Time")]
    [SerializeField] private GameObject bestTimeGroup;
    [SerializeField] private TMP_Text bestTimeValueText;
    [SerializeField] private string noBestTimeText = "--:--.--";

    [Header("Swipe")]
    [SerializeField, Min(1f)] private float minSwipeDistance = 80f;

    [Header("Page Animation")]
    [SerializeField, Min(0f)] private float pageSlideDistance = 160f;
    [SerializeField, Min(0.01f)] private float pageAnimDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float startButtonAnimDuration = 0.18f;

    [Header("Panel Animation")]
    [SerializeField, Min(0.01f)] private float panelFadeDuration = 0.22f;
    [SerializeField, Range(0.5f, 1f)] private float panelStartScale = 0.96f;

    private readonly List<string> pages = new List<string>();

    private LevelConfig selectedLevel;
    private Action<LevelConfig> onStartRequested;
    private Action onClosed;

    private int currentPageIndex;
    private Vector2 dragStartPosition;
    private bool isDragging;

    private RectTransform descriptionRect;
    private CanvasGroup descriptionGroup;
    private Vector2 descriptionStartPosition;

    private CanvasGroup startButtonGroup;
    private RectTransform panelRect;
    private CanvasGroup panelGroup;

    private Coroutine pageRoutine;
    private Coroutine startButtonRoutine;
    private Coroutine panelRoutine;

    public bool IsOpen =>
        briefingPanel != null &&
        briefingPanel.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        PrepareDescription();
        PrepareStartButton();
        PreparePanel();
        PrepareButtons();
        HideInstant();
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartSelectedMission);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsOpen || pageRoutine != null)
            return;

        HandlePcPageNavigation();
    }

    public void Show(
        LevelConfig levelConfig,
        Action<LevelConfig> startCallback,
        Action closeCallback = null)
    {
        if (levelConfig == null)
        {
            Debug.LogWarning(
                "[MissionBriefingPanelUI] Açılacak LevelConfig boş.",
                this
            );
            return;
        }

        if (briefingPanel == null)
        {
            Debug.LogError(
                "[MissionBriefingPanelUI] Briefing Panel atanmamış.",
                this
            );
            return;
        }

        StopActiveRoutines();

        selectedLevel = levelConfig;
        onStartRequested = startCallback;
        onClosed = closeCallback;
        currentPageIndex = 0;
        isDragging = false;

        BuildPages(levelConfig);

        if (WasLevelCompleted(levelConfig))
            currentPageIndex = Mathf.Max(0, pages.Count - 1);

        RefreshMissionInformation(levelConfig);
        RefreshDifficulty(levelConfig.SafeMissionDifficulty);
        RefreshBestTime(levelConfig);

        MainMenuStarColorRandomizer.Instance?
            .ShowMissionBriefingColor();

        briefingPanel.SetActive(true);

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayMissionBriefingOpenSound(panelRect);

        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        if (panelRect != null)
            panelRect.localScale = Vector3.one * panelStartScale;

        RefreshPageInstant();

        if (panelGroup != null && panelRect != null)
            panelRoutine = StartCoroutine(PlayPanelIntro());
        else
            SetPanelInteractive(true);
    }

    public void Close()
    {
        if (briefingPanel == null ||
            !briefingPanel.activeSelf ||
            panelRoutine != null)
        {
            return;
        }

        SetPanelInteractive(false);
        panelRoutine = StartCoroutine(PlayPanelOutro());
    }

    public void HideInstant()
    {
        StopActiveRoutines();

        selectedLevel = null;
        onStartRequested = null;
        onClosed = null;
        currentPageIndex = 0;
        isDragging = false;
        pages.Clear();

        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        if (panelRect != null)
            panelRect.localScale = Vector3.one * panelStartScale;

        if (briefingPanel != null)
            briefingPanel.SetActive(false);
    }

    private void PrepareButtons()
    {
        if (startButton != null)
            startButton.onClick.AddListener(StartSelectedMission);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void PrepareDescription()
    {
        if (pageDescriptionText == null)
            return;

        descriptionRect =
            pageDescriptionText.GetComponent<RectTransform>();

        descriptionGroup =
            pageDescriptionText.GetComponent<CanvasGroup>();

        if (descriptionGroup == null)
        {
            descriptionGroup =
                pageDescriptionText.gameObject.AddComponent<CanvasGroup>();
        }

        if (descriptionRect != null)
            descriptionStartPosition = descriptionRect.anchoredPosition;
    }

    private void PrepareStartButton()
    {
        if (startButton == null)
            return;

        startButtonGroup =
            startButton.GetComponent<CanvasGroup>();

        if (startButtonGroup == null)
        {
            startButtonGroup =
                startButton.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void PreparePanel()
    {
        if (briefingPanel == null)
            return;

        panelRect = briefingPanel.GetComponent<RectTransform>();
        panelGroup = briefingPanel.GetComponent<CanvasGroup>();

        if (panelGroup == null)
            panelGroup = briefingPanel.AddComponent<CanvasGroup>();
    }

    private void BuildPages(LevelConfig levelConfig)
    {
        pages.Clear();

        // Briefing artık tek sayfadır. Görev hedefi ile levela özel kısa bilgi
        // aynı sayfada birleştirilir; böylece oyuncu başlamadan önce yalnızca
        // gerçekten gerekli bilgiyi tek bakışta görür.
        string combinedBriefing =
            levelConfig.GetEffectiveObjectiveDescription();

        if (levelConfig.briefingPages != null)
        {
            foreach (string page in levelConfig.briefingPages)
            {
                if (string.IsNullOrWhiteSpace(page))
                    continue;

                string trimmedPage = page.Trim();

                if (string.IsNullOrWhiteSpace(combinedBriefing))
                {
                    combinedBriefing = trimmedPage;
                }
                else
                {
                    combinedBriefing +=
                        "\n\n" + trimmedPage;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(combinedBriefing))
            combinedBriefing = "Complete the mission objective.";

        pages.Add(combinedBriefing.Trim());
    }

    private void RefreshMissionInformation(LevelConfig levelConfig)
    {
        if (levelTitleText != null)
        {
            string levelName = string.IsNullOrWhiteSpace(levelConfig.levelName)
                ? string.Empty
                : levelConfig.levelName.Trim();

            levelTitleText.text = string.IsNullOrEmpty(levelName)
                ? $"LEVEL {levelConfig.levelNumber}"
                : $"LEVEL {levelConfig.levelNumber} — {levelName.ToUpperInvariant()}";
        }

        if (modeText != null)
            modeText.text = levelConfig.GetEffectiveModeDescription();
    }

    private void RefreshDifficulty(int difficulty)
    {
        if (difficultyStarFills == null)
            return;

        int safeDifficulty = Mathf.Clamp(difficulty, 0, 5);

        for (int i = 0; i < difficultyStarFills.Length; i++)
        {
            Image starFill = difficultyStarFills[i];

            if (starFill == null)
                continue;

            starFill.gameObject.SetActive(i < safeDifficulty);
        }
    }

    private void RefreshBestTime(LevelConfig levelConfig)
    {
        bool showBestTime = levelConfig.CanSaveBestTime;

        if (bestTimeGroup != null)
            bestTimeGroup.SetActive(showBestTime);

        if (!showBestTime || bestTimeValueText == null)
            return;

        string key = "BestTime_Level_" + levelConfig.levelNumber;

        if (!PlayerPrefs.HasKey(key))
        {
            bestTimeValueText.text = noBestTimeText;
            return;
        }

        float bestTime = PlayerPrefs.GetFloat(key, -1f);

        bestTimeValueText.text = bestTime >= 0f
            ? FormatTime(bestTime)
            : noBestTimeText;
    }

    private void RefreshPageInstant()
    {
        if (pages.Count == 0)
            return;

        currentPageIndex = Mathf.Clamp(
            currentPageIndex,
            0,
            pages.Count - 1
        );

        if (pageDescriptionText != null)
            pageDescriptionText.text = pages[currentPageIndex];

        if (descriptionRect != null)
            descriptionRect.anchoredPosition = descriptionStartPosition;

        if (descriptionGroup != null)
            descriptionGroup.alpha = 1f;

        if (pageIndicatorText != null)
        {
            bool showPageIndicator =
                pages.Count > 1;

            pageIndicatorText.gameObject.SetActive(
                showPageIndicator
            );

            if (showPageIndicator)
            {
                pageIndicatorText.text =
                    $"{currentPageIndex + 1} / {pages.Count}";
            }
        }

        SetStartButtonInstant(
            currentPageIndex == pages.Count - 1
        );
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

        HandleMouseSwipe();
    }

    private void HandleMouseSwipe()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragStartPosition = Mouse.current.position.ReadValue();
            isDragging = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
        {
            Vector2 dragEndPosition = Mouse.current.position.ReadValue();
            isDragging = false;
            TrySwipe(dragEndPosition);
        }
    }

    private void TrySwipe(Vector2 dragEndPosition)
    {
        float swipeX = dragEndPosition.x - dragStartPosition.x;

        if (Mathf.Abs(swipeX) < minSwipeDistance)
            return;

        if (swipeX < 0f)
            NextPage();
        else
            PreviousPage();
    }

    private void NextPage()
    {
        if (currentPageIndex >= pages.Count - 1)
            return;

        int oldPage = currentPageIndex;
        currentPageIndex++;

        StartPageAnimation(oldPage, currentPageIndex, -1);
    }

    private void PreviousPage()
    {
        if (currentPageIndex <= 0)
            return;

        int oldPage = currentPageIndex;
        currentPageIndex--;

        StartPageAnimation(oldPage, currentPageIndex, 1);
    }

    private void StartPageAnimation(
        int oldPage,
        int newPage,
        int direction)
    {
        if (pageRoutine != null)
            return;

        pageRoutine = StartCoroutine(
            PageTransitionRoutine(oldPage, newPage, direction)
        );
    }

    private IEnumerator PageTransitionRoutine(
        int oldPage,
        int newPage,
        int direction)
    {
        AnimateStartButton(newPage == pages.Count - 1);

        float timer = 0f;

        while (timer < pageAnimDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / pageAnimDuration);
            float eased = EaseOutCubic(progress);

            if (descriptionRect != null)
            {
                descriptionRect.anchoredPosition =
                    descriptionStartPosition +
                    Vector2.right * direction * pageSlideDistance * eased;
            }

            if (descriptionGroup != null)
                descriptionGroup.alpha = 1f - eased;

            yield return null;
        }

        if (pageDescriptionText != null)
            pageDescriptionText.text = pages[newPage];

        if (pageIndicatorText != null)
            pageIndicatorText.text = $"{newPage + 1} / {pages.Count}";

        timer = 0f;

        while (timer < pageAnimDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / pageAnimDuration);
            float eased = EaseOutCubic(progress);

            if (descriptionRect != null)
            {
                descriptionRect.anchoredPosition =
                    descriptionStartPosition -
                    Vector2.right * direction * pageSlideDistance *
                    (1f - eased);
            }

            if (descriptionGroup != null)
                descriptionGroup.alpha = eased;

            yield return null;
        }

        if (descriptionRect != null)
            descriptionRect.anchoredPosition = descriptionStartPosition;

        if (descriptionGroup != null)
            descriptionGroup.alpha = 1f;

        pageRoutine = null;
    }

    private void AnimateStartButton(bool show)
    {
        if (startButton == null || startButtonGroup == null)
            return;

        if (startButtonRoutine != null)
            StopCoroutine(startButtonRoutine);

        startButtonRoutine = StartCoroutine(StartButtonRoutine(show));
    }

    private IEnumerator StartButtonRoutine(bool show)
    {
        if (show)
            startButton.gameObject.SetActive(true);

        float startAlpha = startButtonGroup.alpha;
        float targetAlpha = show ? 1f : 0f;
        Vector3 startScale = startButton.transform.localScale;
        Vector3 targetScale = show
            ? Vector3.one
            : Vector3.one * 0.85f;

        float timer = 0f;

        while (timer < startButtonAnimDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(
                timer / startButtonAnimDuration
            );
            float eased = EaseOutCubic(progress);

            startButtonGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                eased
            );

            startButton.transform.localScale = Vector3.Lerp(
                startScale,
                targetScale,
                eased
            );

            yield return null;
        }

        startButtonGroup.alpha = targetAlpha;
        startButton.transform.localScale = targetScale;
        startButton.interactable = show;

        if (!show)
            startButton.gameObject.SetActive(false);

        startButtonRoutine = null;
    }

    private void SetStartButtonInstant(bool show)
    {
        if (startButton == null)
            return;

        startButton.gameObject.SetActive(show);
        startButton.interactable = show;
        startButton.transform.localScale = show
            ? Vector3.one
            : Vector3.one * 0.85f;

        if (startButtonGroup != null)
            startButtonGroup.alpha = show ? 1f : 0f;
    }

    private IEnumerator PlayPanelIntro()
    {
        float timer = 0f;

        while (timer < panelFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / panelFadeDuration);
            float eased = EaseOutCubic(progress);

            panelGroup.alpha = eased;
            panelRect.localScale = Vector3.Lerp(
                Vector3.one * panelStartScale,
                Vector3.one,
                eased
            );

            yield return null;
        }

        panelGroup.alpha = 1f;
        panelRect.localScale = Vector3.one;
        SetPanelInteractive(true);
        panelRoutine = null;
    }

    private IEnumerator PlayPanelOutro()
    {
        float startAlpha =
            panelGroup != null
                ? panelGroup.alpha
                : 1f;

        Vector3 startScale =
            panelRect != null
                ? panelRect.localScale
                : Vector3.one;

        float timer = 0f;

        while (timer < panelFadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(
                    timer / panelFadeDuration
                );

            float eased = EaseOutCubic(progress);

            if (panelGroup != null)
            {
                panelGroup.alpha =
                    Mathf.Lerp(
                        startAlpha,
                        0f,
                        eased
                    );
            }

            if (panelRect != null)
            {
                panelRect.localScale =
                    Vector3.Lerp(
                        startScale,
                        Vector3.one * panelStartScale,
                        eased
                    );
            }

            yield return null;
        }

        Action closeCallback = onClosed;

        panelRoutine = null;

        HideInstant();

        closeCallback?.Invoke();
    }

    private void StartSelectedMission()
    {
        if (selectedLevel == null)
        {
            Debug.LogWarning(
                "[MissionBriefingPanelUI] Başlatılacak level bulunamadı.",
                this
            );

            return;
        }

        if (currentPageIndex != pages.Count - 1)
            return;

        if (panelRoutine != null)
            return;

        LevelConfig levelToStart = selectedLevel;
        Action<LevelConfig> callback = onStartRequested;

        SetPanelInteractive(false);

        panelRoutine = StartCoroutine(
            StartMissionRoutine(
                levelToStart,
                callback
            )
        );
    }

    private IEnumerator StartMissionRoutine(
    LevelConfig levelToStart,
    Action<LevelConfig> callback)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayStartButtonSound(startButton != null ? startButton.transform as RectTransform : panelRect);

        float startAlpha =
            panelGroup != null
                ? panelGroup.alpha
                : 1f;

        Vector3 startScale =
            panelRect != null
                ? panelRect.localScale
                : Vector3.one;

        float timer = 0f;

        while (timer < panelFadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                timer / panelFadeDuration
            );

            float eased = EaseOutCubic(progress);

            if (panelGroup != null)
            {
                panelGroup.alpha = Mathf.Lerp(
                    startAlpha,
                    0f,
                    eased
                );
            }

            if (panelRect != null)
            {
                panelRect.localScale = Vector3.Lerp(
                    startScale,
                    Vector3.one * panelStartScale,
                    eased
                );
            }

            yield return null;
        }

        panelRoutine = null;

        HideInstant();

        callback?.Invoke(levelToStart);
    }

    private void SetPanelInteractive(bool interactive)
    {
        if (panelGroup == null)
            return;

        panelGroup.interactable = interactive;
        panelGroup.blocksRaycasts = interactive;
    }

    private void StopActiveRoutines()
    {
        if (pageRoutine != null)
        {
            StopCoroutine(pageRoutine);
            pageRoutine = null;
        }

        if (startButtonRoutine != null)
        {
            StopCoroutine(startButtonRoutine);
            startButtonRoutine = null;
        }

        if (panelRoutine != null)
        {
            StopCoroutine(panelRoutine);
            panelRoutine = null;
        }
    }

    private static bool WasLevelCompleted(
        LevelConfig levelConfig)
    {
        if (levelConfig == null ||
            levelConfig.levelNumber <= 0)
        {
            return false;
        }

        return PlayerPrefs.GetInt(
            CompletedLevelKeyPrefix + levelConfig.levelNumber,
            0
        ) == 1;
    }

    private static string FormatTime(float seconds)
    {
        float safeTime = Mathf.Max(0f, seconds);
        int minutes = Mathf.FloorToInt(safeTime / 60f);
        float remainingSeconds = safeTime - minutes * 60f;

        return $"{minutes:00}:{remainingSeconds:00.00}";
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - Mathf.Clamp01(value);
        return 1f - inverse * inverse * inverse;
    }
}