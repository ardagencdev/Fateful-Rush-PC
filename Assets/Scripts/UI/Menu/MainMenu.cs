using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public static MainMenu Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "a";
    private const string CreditsSceneName = "CreditsScene";

    [Header("UI")]
    [SerializeField] private UIPanelFadeSwitcher fadeSwitcher;
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Footer Signal")]
    [Tooltip(
        "Opsiyonel. Boş bırakılırsa Canvas içindeki 'SIGNAL //' TMP otomatik bulunur."
    )]
    [SerializeField] private TMP_Text signalStatusText;

    [Header("Continue")]
    [Tooltip("LevelSelectPanel üzerindeki 40 LevelConfig kaynağı.")]
    [SerializeField] private LevelSelectPanel levelSelectPanel;

    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueLevelText;

    [SerializeField, Range(0.1f, 1f)]
    private float unavailableContinueAlpha = 0.25f;

    [Header("Dev Room")]
    [SerializeField] private LevelConfig devRoomConfig;
    [SerializeField] private GameObject devRoomButton;

    [Header("Quit")]
    [SerializeField, Min(0f)]
    private float fallbackQuitDelay = 0.35f;

    private Coroutine quitRoutine;
    private CanvasGroup continueButtonGroup;
    private CanvasGroup mainMenuCanvasGroup;
    private LevelConfig continueTargetLevel;

    private bool isStartingGame;
    private bool isQuitting;
    private bool isDevRoomButtonVisible;
    private bool isDesktopDevRoomAllowed;
    private int lastDevRoomHotkeyFrame = -1;

    public bool IsContinueAvailable =>
        continueTargetLevel != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        Time.timeScale = 1f;

        RefreshSignalStatusText();

        isDesktopDevRoomAllowed =
            IsDesktopPlatform();

        // MainMenu componenti MainMenuPanel üzerinde ve panel başka bir
        // menü açıldığında inactive oluyor. MonoBehaviour.Update() bu durumda
        // çalışmadığı için F8'i Input System'in global update callback'inden dinliyoruz.
        InputSystem.onAfterUpdate += HandleInputSystemAfterUpdate;

        FindDevRoomButtonIfNeeded();
        SetDevRoomButtonVisible(false);

        FindLevelSelectPanelIfNeeded();
        PrepareContinueButton();
        RefreshContinueState();
    }

    private void Start()
    {
        // Safety fallback: if the player finished Level 40 and closed the
        // game before reaching/finishing the Credits scene, do not lose the
        // ending. The pending flag is cleared only by the Credits CONTINUE
        // button.
        if (PlayerPrefs.GetInt(
                FatefulRushCreditsController.PendingKey,
                0
            ) == 1)
        {
            SetMainMenuInteraction(false);

            if (Application.CanStreamedLevelBeLoaded(
                    CreditsSceneName
                ))
            {
                SceneManager.LoadScene(
                    CreditsSceneName
                );
            }
            else
            {
                Debug.LogError(
                    $"[MainMenu] Credits scene bulunamadı: '{CreditsSceneName}'. " +
                    "Build Profiles > Scene List'e ekle.",
                    this
                );

                SetMainMenuInteraction(true);
            }

            return;
        }

        RestoreExtrasAfterCreditsIfNeeded();
    }

    private void OnEnable()
    {
        isStartingGame = false;
        isQuitting = false;

        SetMainMenuInteraction(true);
        RefreshContinueState();
        RefreshSignalStatusText();
    }

    private void OnDisable()
    {
        StopQuitRoutine();
    }

    private void OnDestroy()
    {
        InputSystem.onAfterUpdate -= HandleInputSystemAfterUpdate;

        if (Instance == this)
            Instance = null;

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(
                ContinueGame
            );
        }
    }

    private void RefreshSignalStatusText()
    {
        FindSignalStatusTextIfNeeded();

        if (signalStatusText == null)
            return;

        signalStatusText.text =
            SignalStatusState.DisplayText;
    }

    private void FindSignalStatusTextIfNeeded()
    {
        if (signalStatusText != null)
            return;

        Canvas canvas =
            GetComponentInParent<Canvas>(true);

        if (canvas == null &&
            mainMenuPanel != null)
        {
            canvas =
                mainMenuPanel.GetComponentInParent<Canvas>(
                    true
                );
        }

        if (canvas == null)
            return;

        TMP_Text[] texts =
            canvas.GetComponentsInChildren<TMP_Text>(
                true
            );

        for (int i = 0;
             i < texts.Length;
             i++)
        {
            TMP_Text candidate =
                texts[i];

            if (candidate == null ||
                string.IsNullOrWhiteSpace(candidate.text))
            {
                continue;
            }

            if (candidate.text.TrimStart().StartsWith(
                    "SIGNAL //",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                signalStatusText = candidate;
                return;
            }
        }
    }

    private void HandleInputSystemAfterUpdate()
    {
        if (!isDesktopDevRoomAllowed)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || !keyboard.f8Key.wasPressedThisFrame)
            return;

        // Input System ayni Unity frame'inde birden fazla update turu
        // calistirabilirse tek F8 basisi iki kez toggle edilmesin.
        if (lastDevRoomHotkeyFrame == Time.frameCount)
            return;

        lastDevRoomHotkeyFrame = Time.frameCount;

        // Referans daha sonra kaybolduysa / deserialize olmadiysa tekrar bul.
        FindDevRoomButtonIfNeeded();

        SetDevRoomButtonVisible(
            !isDevRoomButtonVisible
        );
    }

    public void ContinueGame()
    {
        if (isStartingGame || isQuitting)
            return;

        // Butona basıldığı anda kayıt durumunu bir kez daha doğrula.
        RefreshContinueState();

        if (continueTargetLevel == null ||
            continueButton == null)
        {
            return;
        }

        if (!CanLoadGameScene())
            return;

        isStartingGame = true;
        SetMainMenuInteraction(false);
        Time.timeScale = 1f;

        SelectedLevelData.SetMission(
            continueTargetLevel
        );

        LoadGameScene();
    }

    public void RefreshContinueState()
    {
        FindLevelSelectPanelIfNeeded();
        PrepareContinueButton();

        bool canContinue =
            TryFindContinueTarget(
                out continueTargetLevel
            );

        SetContinueVisualState(canContinue);
    }

    public void StartGame()
    {
        if (!isDesktopDevRoomAllowed)
            return;

        if (isStartingGame || isQuitting)
            return;

        if (devRoomConfig == null)
        {
            Debug.LogError(
                "MainMenu devRoomConfig reference is missing.",
                this
            );

            return;
        }

        if (!CanLoadGameScene())
            return;

        isStartingGame = true;
        SetMainMenuInteraction(false);
        Time.timeScale = 1f;

        SelectedLevelData.SetDevRoom(devRoomConfig);

        LoadGameScene();
    }

    public void OpenGooglePlayAchievements()
    {
        GooglePlayGamesManager.ShowAchievementsUI();
    }

    public void OpenGooglePlayLeaderboards()
    {
        GooglePlayGamesLeaderboards.ShowAllLeaderboardsUI();
    }

    public void OpenCredits()
    {
        OpenCreditsInternal(
            returnToExtras: false
        );
    }

    public void OpenCreditsFromExtras()
    {
        OpenCreditsInternal(
            returnToExtras: true
        );
    }

    private void OpenCreditsInternal(bool returnToExtras)
    {
        if (isStartingGame || isQuitting)
            return;

        if (!Application.CanStreamedLevelBeLoaded(
                CreditsSceneName
            ))
        {
            Debug.LogError(
                $"[MainMenu] Credits scene bulunamadı: '{CreditsSceneName}'. " +
                "Build Profiles > Scene List'e ekle.",
                this
            );

            return;
        }

        if (SceneTransition.Instance != null &&
            SceneTransition.Instance.IsTransitioning)
        {
            return;
        }

        if (returnToExtras)
        {
            FatefulRushCreditsController
                .MarkOpenedFromExtras();
        }

        Time.timeScale = 1f;

        if (SceneTransition.Instance != null)
        {
            SceneTransition.Instance.LoadSceneWithFade(
                CreditsSceneName
            );

            return;
        }

        SceneManager.LoadScene(
            CreditsSceneName
        );
    }

    private void RestoreExtrasAfterCreditsIfNeeded()
    {
        if (!FatefulRushCreditsController
                .HasReturnToExtrasRequest())
        {
            return;
        }

        ExtrasPanelUI extrasPanelUI =
            FindAnyObjectByType<ExtrasPanelUI>();

        if (extrasPanelUI == null)
        {
            Debug.LogError(
                "[MainMenu] Credits sonrasi ExtrasPanelUI bulunamadi. " +
                "Return request korunuyor; reference/scene yapisini kontrol et.",
                this
            );

            return;
        }

        FatefulRushCreditsController
            .ConsumeReturnToExtrasRequest();

        extrasPanelUI.RestoreAfterCredits();
    }

    public void SignInGooglePlayGames()
    {
        GooglePlayGamesManager.ManualSignIn();
    }

    public void QuitGame()
    {
        if (isQuitting || isStartingGame)
            return;

        isQuitting = true;
        SetMainMenuInteraction(false);
        Time.timeScale = 1f;

        if (SceneTransition.Instance != null)
        {
            SceneTransition.Instance.QuitGameWithFade();
            return;
        }

        StopQuitRoutine();
        quitRoutine = StartCoroutine(QuitRoutine());
    }

    private bool TryFindContinueTarget(
        out LevelConfig targetLevel
    )
    {
        targetLevel = null;

        if (levelSelectPanel == null)
            return false;

        IReadOnlyList<LevelConfig> configuredLevels =
            levelSelectPanel.GetConfiguredLevels();

        if (configuredLevels == null ||
            configuredLevels.Count == 0)
        {
            return false;
        }

        int highestCompletedLevel = 0;

        foreach (LevelConfig level in configuredLevels)
        {
            if (level == null)
                continue;

            bool isCompleted =
                PlayerPrefs.GetInt(
                    $"CompletedLevel_{level.levelNumber}",
                    0
                ) == 1;

            if (!isCompleted)
                continue;

            highestCompletedLevel = Mathf.Max(
                highestCompletedLevel,
                level.levelNumber
            );
        }

        // Oyuncu henüz hiçbir görevi tamamlamadıysa Continue kapalı kalır.
        if (highestCompletedLevel <= 0)
            return false;

        int unlockedLevel =
            PlayerPrefs.GetInt(
                "UnlockedLevel",
                1
            );

        // Eski veya bozulmuş kayıtta UnlockedLevel geride kalmışsa
        // tamamlanan en yüksek bölümün bir sonrasını esas al.
        int desiredLevelNumber = Mathf.Max(
            unlockedLevel,
            highestCompletedLevel + 1
        );

        // Level 40 tamamlandığında UnlockedLevel 41 olabilir.
        // Listede bulunan en ileri geçerli LevelConfig seçilerek taşma önlenir.
        foreach (LevelConfig level in configuredLevels)
        {
            if (level == null)
                continue;

            if (level.levelNumber > desiredLevelNumber)
                break;

            targetLevel = level;
        }

        return targetLevel != null;
    }

    private void PrepareContinueButton()
    {
        if (continueButton == null)
            return;

        if (continueButtonGroup == null)
        {
            continueButtonGroup =
                continueButton.GetComponent<CanvasGroup>();

            if (continueButtonGroup == null)
            {
                continueButtonGroup =
                    continueButton.gameObject
                        .AddComponent<CanvasGroup>();
            }
        }

        continueButton.onClick.RemoveListener(
            ContinueGame
        );
        continueButton.onClick.AddListener(
            ContinueGame
        );

        UIButtonSound buttonSound =
            continueButton.GetComponent<UIButtonSound>();

        if (buttonSound != null)
        {
            buttonSound.ConfigureAsContinue(this);
        }
    }

    private void SetContinueVisualState(
        bool canContinue
    )
    {
        // Continue her zaman tıklama alır. Kayıt yoksa ContinueGame
        // hiçbir aksiyon gerçekleştirmez; UIButtonSound Locked sesi çalar.
        if (continueButton != null)
            continueButton.interactable = true;

        if (continueButtonGroup != null)
        {
            continueButtonGroup.alpha =
                canContinue
                    ? 1f
                    : unavailableContinueAlpha;

            continueButtonGroup.interactable = true;
            continueButtonGroup.blocksRaycasts = true;
        }

        if (continueLevelText == null)
            return;

        continueLevelText.gameObject.SetActive(
            canContinue
        );

        if (canContinue &&
            continueTargetLevel != null)
        {
            continueLevelText.text =
                $"LEVEL {continueTargetLevel.levelNumber}";

            RefreshContinueLevelColor();
        }
    }

    public void RefreshContinueLevelColor()
    {
        if (continueLevelText == null ||
            continueTargetLevel == null)
        {
            return;
        }

        // Main Menu level label always previews the exact gameplay NearStars
        // color configured for the Continue target level. It must not follow
        // the selected skin UI theme.
        Color levelColor = continueTargetLevel.nearStarsColor;
        levelColor.a = 1f;
        continueLevelText.color = levelColor;
    }

    private void FindLevelSelectPanelIfNeeded()
    {
        if (levelSelectPanel != null)
            return;

        LevelSelectPanel[] candidates =
            Resources.FindObjectsOfTypeAll<LevelSelectPanel>();

        foreach (LevelSelectPanel candidate in candidates)
        {
            if (candidate == null ||
                !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            levelSelectPanel = candidate;
            return;
        }
    }


    private void SetMainMenuInteraction(bool interactable)
    {
        if (mainMenuPanel == null)
            return;

        if (mainMenuCanvasGroup == null)
        {
            mainMenuCanvasGroup =
                mainMenuPanel.GetComponent<CanvasGroup>();

            if (mainMenuCanvasGroup == null)
            {
                mainMenuCanvasGroup =
                    mainMenuPanel.AddComponent<CanvasGroup>();
            }
        }

        mainMenuCanvasGroup.interactable = interactable;
        mainMenuCanvasGroup.blocksRaycasts = interactable;
    }

    private void LoadGameScene()
    {
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

    private IEnumerator QuitRoutine()
    {
        if (fadeSwitcher != null &&
            mainMenuPanel != null)
        {
            fadeSwitcher.HidePanel(mainMenuPanel);

            if (fallbackQuitDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    fallbackQuitDelay
                );
            }
        }

        quitRoutine = null;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void FindDevRoomButtonIfNeeded()
    {
        if (devRoomButton != null)
            return;

        Transform[] sceneTransforms =
            Resources.FindObjectsOfTypeAll<Transform>();

        foreach (Transform sceneTransform in sceneTransforms)
        {
            if (sceneTransform == null)
                continue;

            GameObject candidate =
                sceneTransform.gameObject;

            if (!candidate.scene.IsValid())
                continue;

            if (candidate.name != "DevRoomButton")
                continue;

            devRoomButton = candidate;
            return;
        }

        Debug.LogWarning(
            "MainMenu could not find DevRoomButton. " +
            "Assign it in the Inspector or keep its name as DevRoomButton.",
            this
        );
    }

    private void SetDevRoomButtonVisible(
        bool visible
    )
    {
        bool shouldShow =
            isDesktopDevRoomAllowed && visible;

        isDevRoomButtonVisible = shouldShow;

        if (devRoomButton != null)
            devRoomButton.SetActive(shouldShow);
    }

    private static bool IsDesktopPlatform()
    {
        if (Application.isEditor)
            return true;

        return
            Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.LinuxPlayer ||
            Application.platform == RuntimePlatform.OSXPlayer;
    }

    private bool CanLoadGameScene()
    {
        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError(
                "MainMenu game scene name is empty.",
                this
            );

            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError(
                $"Scene '{gameSceneName}' could not be loaded. " +
                "Make sure it is included in Build Profiles.",
                this
            );

            return false;
        }

        return true;
    }

    private void StopQuitRoutine()
    {
        if (quitRoutine == null)
            return;

        StopCoroutine(quitRoutine);
        quitRoutine = null;
    }

    private void OnValidate()
    {
        fallbackQuitDelay =
            Mathf.Max(0f, fallbackQuitDelay);

        unavailableContinueAlpha =
            Mathf.Clamp(
                unavailableContinueAlpha,
                0.1f,
                1f
            );
    }
}