using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameQuit : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string SoundEnabledKey = "SoundOn";
    private const string MusicVolumeKey = "MusicVolume";

    [Header("Pause UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private UIPanelFadeSwitcher fadeSwitcher;
    [SerializeField] private OptionsUI optionsUI;

    [Header("Audio")]
    [SerializeField] private AudioSource gameplayMusicSource;

    private GameplayMusicFade gameplayMusicController;

    [SerializeField, Min(0f)]
    private float musicFadeDuration = 0.25f;

    private Coroutine musicFadeRoutine;
    private Coroutine pauseTransitionRoutine;
    private float defaultGameplayMusicVolume = 1f;
    private CanvasGroup pausePanelCanvasGroup;

    private bool pauseMenuModalOpen;
    private Selectable[] pausePanelSelectables;
    private bool[] pausePanelSelectableStates;
    private GraphicRaycaster[] pausePanelRaycasters;
    private bool[] pausePanelRaycasterStates;

    public bool IsPaused { get; private set; }
    public bool IsPauseMenuModalOpen => pauseMenuModalOpen;

    private void Awake()
    {
        Time.timeScale = 1f;
        IsPaused = false;

        RefreshReferences();
        CachePausePanelCanvasGroup();

        if (gameplayMusicSource != null)
        {
            defaultGameplayMusicVolume =
                gameplayMusicSource.volume;
        }

        if (pausePanel != null)
        {
            if (fadeSwitcher != null && fadeSwitcher.isActiveAndEnabled)
                fadeSwitcher.SetInstant(pausePanel, false);
            else
                pausePanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!GameStateManager.IsGameplayStarted)
            return;

        bool keyboardPause =
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame;

        bool gamepadPause =
            Gamepad.current != null &&
            (Gamepad.current.startButton.wasPressedThisFrame ||
             (IsPaused && Gamepad.current.buttonEast.wasPressedThisFrame));

        if (!keyboardPause && !gamepadPause)
            return;

        // A confirmation dialog opened from Pause is a true modal.
        // Escape must never resume gameplay behind it.
        if (pauseMenuModalOpen)
            return;

        if (IsPaused &&
            optionsUI != null &&
            optionsUI.HandleEscapeBack())
        {
            return;
        }

        TogglePause();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused ||
            !RuntimePerformancePolicy.IsGooglePlayGamesOnPC ||
            !GameStateManager.IsGameplayStarted ||
            GameStateManager.IsGameplayEnded ||
            IsPaused)
        {
            return;
        }

        // Google Play Games' overlay pauses the Android activity while it is
        // visible. Freeze gameplay instead of letting hazards keep moving
        // behind the overlay; the player resumes explicitly from Pause.
        PauseGame();
    }

    private void OnDestroy()
    {
        StopMusicFade();
        StopPauseTransition();
        GameAudioMixerController.SetPaused(false);
    }

    public void PauseGame()
    {
        if (!GameStateManager.IsGameplayStarted || IsPaused || pauseTransitionRoutine != null)
            return;

        IsPaused = true;
        Time.timeScale = 0f;

        GameAudioMixerController.SetPaused(true);

        SoundManager.Instance?.PlayPremiumInterfaceSound(
            pausePanel != null
                ? pausePanel.transform as RectTransform
                : transform as RectTransform
        );

        FadeGameplayMusicOut();
        SetPausePanelInteraction(true);

        if (pausePanel != null)
        {
            if (fadeSwitcher != null && fadeSwitcher.isActiveAndEnabled)
                fadeSwitcher.ShowPanel(pausePanel);
            else
                pausePanel.SetActive(true);

            SelectFirstPauseControl();
        }
    }

    private void SelectFirstPauseControl()
    {
        if (pausePanel == null)
            return;

        Selectable[] selectables =
            pausePanel.GetComponentsInChildren<Selectable>(true);

        for (int i = 0; i < selectables.Length; i++)
        {
            Selectable selectable = selectables[i];
            if (selectable == null ||
                !selectable.gameObject.activeInHierarchy ||
                !selectable.IsInteractable())
            {
                continue;
            }

            selectable.Select();
            return;
        }
    }

    public void ResumeGame()
    {
        if (!IsPaused || pauseTransitionRoutine != null || pauseMenuModalOpen)
            return;

        pauseTransitionRoutine = StartCoroutine(ResumeGameRoutine());
    }

    private IEnumerator ResumeGameRoutine()
    {
        SoundManager.Instance?.PlayPremiumInterfaceSound(
            pausePanel != null
                ? pausePanel.transform as RectTransform
                : transform as RectTransform
        );

        // Keep gameplay frozen while the pause panel finishes its outro.
        if (pausePanel != null)
        {
            if (fadeSwitcher != null && fadeSwitcher.isActiveAndEnabled)
            {
                fadeSwitcher.HidePanel(pausePanel);

                while (fadeSwitcher.IsTransitioning)
                    yield return null;
            }
            else
            {
                pausePanel.SetActive(false);
            }
        }

        IsPaused = false;
        GameAudioMixerController.SetPaused(false);

        if (TimeSlowController.Instance != null)
        {
            TimeSlowController.Instance.ResumeAfterPause();
        }
        else
        {
            Time.timeScale = 1f;
        }

        FadeGameplayMusicIn();
        pauseTransitionRoutine = null;
    }

    public void TogglePause()
    {
        if (pauseMenuModalOpen)
            return;

        if (IsPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void RestartGame()
    {
        if (pauseMenuModalOpen)
            return;

        PrepareForSceneChange();

        int activeSceneIndex =
            SceneManager.GetActiveScene().buildIndex;

        if (SceneTransition.Instance != null)
        {
            SceneTransition.Instance.LoadSceneWithFade(
                activeSceneIndex
            );
        }
        else
        {
            SceneManager.LoadScene(activeSceneIndex);
        }
    }

    public void BackToMainMenu()
    {
        if (IsPaused)
        {
            GameResultUI resultUI =
                FindAnyObjectByType<GameResultUI>(
                    FindObjectsInactive.Include
                );

            if (resultUI != null &&
                resultUI.ShowPauseMenuConfirmation())
            {
                return;
            }
        }

        LoadMainMenuImmediately();
    }

    private void LoadMainMenuImmediately()
    {
        PrepareForSceneChange();

        if (SceneTransition.Instance != null)
        {
            // Pause/GameResult fallback yolunda da ayni polish: once ekran
            // tamamen siyaha kapanir, attempt reklami o anda acilir. Reklam
            // kapaninca SceneTransition MainMenu yuklemesine devam eder.
            SceneTransition.Instance.LoadSceneWithFade(
                MainMenuSceneName,
                continueTransition =>
                    FatefulRushAdManager
                        .TryShowAttemptAdBeforeReturningToMenu(
                            continueTransition
                        )
            );

            return;
        }

        // SceneTransition bulunamayan nadir fallback'te fade yapamayiz; yine
        // de reklam varsa scene yuklemeden once tamamlanmasini bekle.
        bool adStarted =
            FatefulRushAdManager.TryShowAttemptAdBeforeReturningToMenu(
                ContinueLoadMainMenuFallbackAfterAd
            );

        if (!adStarted)
            ContinueLoadMainMenuFallbackAfterAd();
    }

    private void ContinueLoadMainMenuFallbackAfterAd()
    {
        if (this == null)
            return;

        SceneManager.LoadScene(MainMenuSceneName);
    }

    public void ExitToMenu()
    {
        BackToMainMenu();
    }

    public void QuitGame()
    {
        StopMusicFade();
        Time.timeScale = 1f;
        IsPaused = false;

        if (SceneTransition.Instance != null)
        {
            SceneTransition.Instance.QuitGameWithFade();
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void FadeGameplayMusicOut()
    {
        if (gameplayMusicController != null)
        {
            StopMusicFade();
            gameplayMusicController.FadeOutAndPause(
                musicFadeDuration
            );
            return;
        }

        if (gameplayMusicSource == null)
            return;

        StopMusicFade();

        if (musicFadeDuration <= 0f)
        {
            gameplayMusicSource.volume = 0f;
            gameplayMusicSource.Pause();
            return;
        }

        musicFadeRoutine =
            StartCoroutine(FadeMusicOutRoutine());
    }

    private void FadeGameplayMusicIn()
    {
        if (gameplayMusicController != null)
        {
            StopMusicFade();
            gameplayMusicController.ResumeFromPause(
                musicFadeDuration
            );
            return;
        }

        if (gameplayMusicSource == null)
            return;

        StopMusicFade();

        float targetVolume =
            GetTargetGameplayMusicVolume();

        gameplayMusicSource.UnPause();

        if (musicFadeDuration <= 0f)
        {
            gameplayMusicSource.volume = targetVolume;
            return;
        }

        musicFadeRoutine =
            StartCoroutine(
                FadeMusicInRoutine(targetVolume)
            );
    }

    private IEnumerator FadeMusicOutRoutine()
    {
        float startVolume =
            gameplayMusicSource.volume;

        float elapsedTime = 0f;

        while (elapsedTime < musicFadeDuration)
        {
            if (gameplayMusicSource == null)
                yield break;

            elapsedTime += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsedTime / musicFadeDuration
            );

            gameplayMusicSource.volume = Mathf.Lerp(
                startVolume,
                0f,
                progress
            );

            yield return null;
        }

        if (gameplayMusicSource != null)
        {
            gameplayMusicSource.volume = 0f;
            gameplayMusicSource.Pause();
        }

        musicFadeRoutine = null;
    }

    private IEnumerator FadeMusicInRoutine(
        float targetVolume)
    {
        float startVolume =
            gameplayMusicSource.volume;

        float elapsedTime = 0f;

        while (elapsedTime < musicFadeDuration)
        {
            if (gameplayMusicSource == null)
                yield break;

            elapsedTime += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsedTime / musicFadeDuration
            );

            gameplayMusicSource.volume = Mathf.Lerp(
                startVolume,
                targetVolume,
                progress
            );

            yield return null;
        }

        if (gameplayMusicSource != null)
        {
            gameplayMusicSource.volume =
                targetVolume;
        }

        musicFadeRoutine = null;
    }

    private float GetTargetGameplayMusicVolume()
    {
        if (gameplayMusicController != null)
            return gameplayMusicController.CurrentTargetVolume;

        bool soundEnabled =
            PlayerPrefs.GetInt(SoundEnabledKey, 1) == 1;

        if (!soundEnabled)
            return 0f;

        return Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                MusicVolumeKey,
                defaultGameplayMusicVolume
            )
        );
    }

    public void SetPauseMenuModalState(bool open)
    {
        pauseMenuModalOpen = open;
        SetPausePanelInteraction(!open);
    }

    public void SetPausePanelInteraction(bool enabled)
    {
        CachePausePanelCanvasGroup();

        if (pausePanel == null)
            return;

        if (!enabled)
        {
            CachePausePanelInputStates();

            if (pausePanelCanvasGroup != null)
            {
                pausePanelCanvasGroup.interactable = false;
                pausePanelCanvasGroup.blocksRaycasts = false;
            }

            // CanvasGroup is normally enough, but the pause UI contains
            // nested canvases / UI components. Disable them explicitly so
            // no child button can receive a click through the modal.
            if (pausePanelSelectables != null)
            {
                for (int i = 0; i < pausePanelSelectables.Length; i++)
                {
                    if (pausePanelSelectables[i] != null)
                        pausePanelSelectables[i].interactable = false;
                }
            }

            if (pausePanelRaycasters != null)
            {
                for (int i = 0; i < pausePanelRaycasters.Length; i++)
                {
                    if (pausePanelRaycasters[i] != null)
                        pausePanelRaycasters[i].enabled = false;
                }
            }

            return;
        }

        if (pausePanelCanvasGroup != null)
        {
            pausePanelCanvasGroup.interactable = true;
            pausePanelCanvasGroup.blocksRaycasts = true;
        }

        RestorePausePanelInputStates();
    }

    private void CachePausePanelInputStates()
    {
        if (pausePanel == null)
            return;

        pausePanelSelectables =
            pausePanel.GetComponentsInChildren<Selectable>(true);

        pausePanelSelectableStates =
            new bool[pausePanelSelectables.Length];

        for (int i = 0; i < pausePanelSelectables.Length; i++)
        {
            pausePanelSelectableStates[i] =
                pausePanelSelectables[i] != null &&
                pausePanelSelectables[i].interactable;
        }

        pausePanelRaycasters =
            pausePanel.GetComponentsInChildren<GraphicRaycaster>(true);

        pausePanelRaycasterStates =
            new bool[pausePanelRaycasters.Length];

        for (int i = 0; i < pausePanelRaycasters.Length; i++)
        {
            pausePanelRaycasterStates[i] =
                pausePanelRaycasters[i] != null &&
                pausePanelRaycasters[i].enabled;
        }
    }

    private void RestorePausePanelInputStates()
    {
        if (pausePanelSelectables != null &&
            pausePanelSelectableStates != null)
        {
            int count = Mathf.Min(
                pausePanelSelectables.Length,
                pausePanelSelectableStates.Length
            );

            for (int i = 0; i < count; i++)
            {
                if (pausePanelSelectables[i] != null)
                {
                    pausePanelSelectables[i].interactable =
                        pausePanelSelectableStates[i];
                }
            }
        }

        if (pausePanelRaycasters != null &&
            pausePanelRaycasterStates != null)
        {
            int count = Mathf.Min(
                pausePanelRaycasters.Length,
                pausePanelRaycasterStates.Length
            );

            for (int i = 0; i < count; i++)
            {
                if (pausePanelRaycasters[i] != null)
                {
                    pausePanelRaycasters[i].enabled =
                        pausePanelRaycasterStates[i];
                }
            }
        }

        pausePanelSelectables = null;
        pausePanelSelectableStates = null;
        pausePanelRaycasters = null;
        pausePanelRaycasterStates = null;
    }

    private void CachePausePanelCanvasGroup()
    {
        if (pausePanel == null || pausePanelCanvasGroup != null)
            return;

        pausePanelCanvasGroup = pausePanel.GetComponent<CanvasGroup>();

        if (pausePanelCanvasGroup == null)
            pausePanelCanvasGroup = pausePanel.AddComponent<CanvasGroup>();
    }

    private void PrepareForSceneChange()
    {
        StopMusicFade();
        StopPauseTransition();

        IsPaused = false;
        pauseMenuModalOpen = false;
        Time.timeScale = 1f;
        GameAudioMixerController.SetPaused(false);
        SetPausePanelInteraction(true);

        if (pausePanel != null)
        {
            if (fadeSwitcher != null && fadeSwitcher.isActiveAndEnabled)
                fadeSwitcher.SetInstant(pausePanel, false);
            else
                pausePanel.SetActive(false);
        }
    }

    private void StopPauseTransition()
    {
        if (pauseTransitionRoutine == null)
            return;

        StopCoroutine(pauseTransitionRoutine);
        pauseTransitionRoutine = null;
    }

    private void StopMusicFade()
    {
        if (musicFadeRoutine == null)
            return;

        StopCoroutine(musicFadeRoutine);
        musicFadeRoutine = null;
    }

    private void RefreshReferences()
    {
        if (optionsUI == null)
        {
            optionsUI =
                FindAnyObjectByType<OptionsUI>();
        }

        if (gameplayMusicController == null &&
            gameplayMusicSource != null)
        {
            gameplayMusicController =
                gameplayMusicSource.GetComponent<GameplayMusicFade>();
        }

        if (gameplayMusicController == null)
        {
            gameplayMusicController =
                FindAnyObjectByType<GameplayMusicFade>();
        }

        if (gameplayMusicSource == null &&
            gameplayMusicController != null)
        {
            gameplayMusicSource =
                gameplayMusicController.GetComponent<AudioSource>();
        }
    }

    private void OnValidate()
    {
        musicFadeDuration =
            Mathf.Max(0f, musicFadeDuration);
    }
}