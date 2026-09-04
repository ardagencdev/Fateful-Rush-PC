using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class IntroController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField]
    private string nextSceneName = "MainMenu";

    [Header("Sequence Timing")]
    [SerializeField, Min(0f)]
    private float openingBlackDuration = 0.30f;

    [SerializeField, Min(0f)]
    private float studioFadeInDuration = 0.35f;

    [SerializeField, Min(0f)]
    private float studioHoldDuration = 0.55f;

    [SerializeField, Min(0f)]
    private float studioFadeOutDuration = 0.30f;

    [SerializeField, Min(0f)]
    private float transitionGapDuration = 0.12f;

    [SerializeField, Min(0f)]
    private float glowPreRevealDuration = 0.20f;

    [SerializeField, Min(0f)]
    private float gameLogoRevealDuration = 0.45f;

    [SerializeField, Min(0f)]
    private float gameLogoHoldDuration = 1.15f;

    [SerializeField, Min(0f)]
    private float finalFadeOutDuration = 0.55f;

    [SerializeField, Min(0f)]
    private float minimumSkipDelay = 0.60f;

    [Header("Studio Logo - YoungDev Studios")]
    [SerializeField]
    private CanvasGroup studioLogoGroup;

    [SerializeField]
    private RectTransform studioLogoTransform;

    [SerializeField, Min(0f)]
    private float studioStartScale = 0.985f;

    [SerializeField, Min(0f)]
    private float studioEndScale = 1f;

    [Header("Fateful Rush Logo")]
    [SerializeField]
    private CanvasGroup logoGroup;

    [SerializeField]
    private RectTransform logoTransform;

    [SerializeField, Min(0f)]
    private float logoStartScale = 0.96f;

    [SerializeField, Min(0f)]
    private float logoEndScale = 1f;

    [SerializeField, Min(0f)]
    private float logoFadeOutScale = 0.985f;

    [Header("Cosmic Backdrop - Optional")]
    [Tooltip("Very subtle stars / nebula layer behind the Fateful Rush logo. Leave empty if unused.")]
    [SerializeField]
    private CanvasGroup gameBackdropGroup;

    [SerializeField]
    private RectTransform gameBackdropTransform;

    [SerializeField, Range(0f, 1f)]
    private float backdropMaxAlpha = 0.32f;

    [SerializeField, Min(0f)]
    private float backdropStartScale = 1.035f;

    [SerializeField, Min(0f)]
    private float backdropEndScale = 1f;

    [Header("Divine / Void Glow - Optional")]
    [SerializeField]
    private CanvasGroup glowGroup;

    [SerializeField]
    private RectTransform glowTransform;

    [SerializeField, Range(0f, 1f)]
    private float glowPreRevealAlpha = 0.16f;

    [SerializeField, Range(0f, 1f)]
    private float glowMaxAlpha = 0.38f;

    [SerializeField, Min(0f)]
    private float glowPreRevealStartScale = 1.24f;

    [SerializeField, Min(0f)]
    private float glowRevealScale = 0.93f;

    [SerializeField, Min(0f)]
    private float glowEndScale = 1.16f;

    [SerializeField, Min(0f)]
    private float glowFadeOutScale = 1.28f;

    [Header("Intro SFX")]
    [Tooltip("Played once immediately when the Intro scene starts.")]
    [SerializeField]
    private AudioSource introAudioSource;

    [SerializeField]
    private AudioClip introSound;

    [SerializeField, Range(0f, 2f)]
    private float introSoundVolume = 1f;

    [SerializeField]
    private bool fadeOutSoundWhenLeaving = true;

    [Header("Optional")]
    [SerializeField]
    private CanvasGroup tapToSkipGroup;

    private Coroutine introRoutine;
    private Coroutine loadingRoutine;

    private bool isLoading;
    private bool canSkip;
    private bool introSoundPlayed;

    private float introStartTime;
    private GameObject spatialIntroAudioObject;

    private void Awake()
    {
        RefreshReferences();
        ConfigureIntroAudioSource();
        ConfigureCanvasGroups();
        ResetIntroVisuals();
    }

    private void Start()
    {
        Time.timeScale = 1f;

        introStartTime = Time.unscaledTime;
        canSkip = false;
        introSoundPlayed = false;

        // Intro SFX starts immediately when the Intro scene begins.
        PlayIntroSound();

        introRoutine = StartCoroutine(IntroRoutine());
    }

    private void Update()
    {
        if (isLoading)
            return;

        if (!canSkip)
        {
            canSkip =
                Time.unscaledTime - introStartTime >=
                minimumSkipDelay;

            if (!canSkip)
                return;
        }

        if (WasSkipInputPressed())
            SkipIntro();
    }

    private void OnDisable()
    {
        StopActiveRoutines();
    }

    private IEnumerator IntroRoutine()
    {
        ResetIntroVisuals();

        if (openingBlackDuration > 0f)
            yield return WaitRealtime(openingBlackDuration);

        // 1) YoungDev Studios: clean, quiet and understated.
        yield return FadeStudioLogo(0f, 1f, studioFadeInDuration);

        if (studioHoldDuration > 0f)
            yield return WaitRealtime(studioHoldDuration);

        yield return FadeStudioLogo(1f, 0f, studioFadeOutDuration);

        if (transitionGapDuration > 0f)
            yield return WaitRealtime(transitionGapDuration);

        // 2) A tiny inward glow contraction before the game identity appears.
        if (glowPreRevealDuration > 0f)
            yield return PreRevealGlowRoutine();
        else
            ApplyPreRevealGlowState();

        // 3) Main Fateful Rush logo reveal.
        if (gameLogoRevealDuration > 0f)
            yield return GameLogoRevealRoutine();
        else
            ApplyGameLogoVisibleState();

        canSkip = true;

        if (gameLogoHoldDuration > 0f)
            yield return WaitRealtime(gameLogoHoldDuration);

        introRoutine = null;
        BeginLoadingSequence();
    }

    private IEnumerator FadeStudioLogo(
        float fromAlpha,
        float toAlpha,
        float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(studioLogoGroup, toAlpha);

            if (studioLogoTransform != null)
            {
                studioLogoTransform.localScale =
                    Vector3.one * studioEndScale;
            }

            yield break;
        }

        float elapsed = 0f;
        float startScale =
            studioLogoTransform != null
                ? studioLogoTransform.localScale.x
                : studioStartScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Smooth01(progress);

            SetAlpha(
                studioLogoGroup,
                Mathf.Lerp(fromAlpha, toAlpha, eased)
            );

            if (studioLogoTransform != null)
            {
                float scale = Mathf.Lerp(
                    startScale,
                    studioEndScale,
                    eased
                );

                studioLogoTransform.localScale =
                    Vector3.one * scale;
            }

            yield return null;
        }

        SetAlpha(studioLogoGroup, toAlpha);
    }

    private IEnumerator PreRevealGlowRoutine()
    {
        float elapsed = 0f;

        while (elapsed < glowPreRevealDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / glowPreRevealDuration
            );

            float eased = EaseInOut(progress);

            SetAlpha(
                glowGroup,
                Mathf.Lerp(0f, glowPreRevealAlpha, eased)
            );

            if (glowTransform != null)
            {
                float scale = Mathf.Lerp(
                    glowPreRevealStartScale,
                    glowRevealScale,
                    eased
                );

                glowTransform.localScale =
                    Vector3.one * scale;
            }

            yield return null;
        }

        ApplyPreRevealGlowState();
    }

    private IEnumerator GameLogoRevealRoutine()
    {
        float elapsed = 0f;

        while (elapsed < gameLogoRevealDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / gameLogoRevealDuration
            );

            float alphaProgress = Smooth01(progress);
            float motionProgress = EaseOutCubic(progress);

            SetAlpha(logoGroup, alphaProgress);

            SetAlpha(
                gameBackdropGroup,
                Mathf.Lerp(
                    0f,
                    backdropMaxAlpha,
                    alphaProgress
                )
            );

            SetAlpha(
                glowGroup,
                Mathf.Lerp(
                    glowPreRevealAlpha,
                    glowMaxAlpha,
                    alphaProgress
                )
            );

            if (logoTransform != null)
            {
                float scale = Mathf.Lerp(
                    logoStartScale,
                    logoEndScale,
                    motionProgress
                );

                logoTransform.localScale =
                    Vector3.one * scale;
            }

            if (gameBackdropTransform != null)
            {
                float scale = Mathf.Lerp(
                    backdropStartScale,
                    backdropEndScale,
                    Smooth01(progress)
                );

                gameBackdropTransform.localScale =
                    Vector3.one * scale;
            }

            if (glowTransform != null)
            {
                float scale = Mathf.Lerp(
                    glowRevealScale,
                    glowEndScale,
                    motionProgress
                );

                glowTransform.localScale =
                    Vector3.one * scale;
            }

            if (tapToSkipGroup != null)
            {
                float skipProgress = Mathf.Clamp01(
                    (progress - 0.55f) / 0.45f
                );

                SetAlpha(
                    tapToSkipGroup,
                    Smooth01(skipProgress)
                );
            }

            yield return null;
        }

        ApplyGameLogoVisibleState();
    }

    private void SkipIntro()
    {
        if (isLoading || !canSkip)
            return;

        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        BeginLoadingSequence();
    }

    private void BeginLoadingSequence()
    {
        if (isLoading)
            return;

        isLoading = true;
        loadingRoutine = StartCoroutine(LoadNextSceneRoutine());
    }

    private IEnumerator LoadNextSceneRoutine()
    {
        float studioStartAlpha = GetAlpha(studioLogoGroup, 0f);
        float logoStartAlpha = GetAlpha(logoGroup, 0f);
        float backdropStartAlpha = GetAlpha(gameBackdropGroup, 0f);
        float glowStartAlpha = GetAlpha(glowGroup, 0f);
        float skipStartAlpha = GetAlpha(tapToSkipGroup, 0f);

        Vector3 studioStartScaleValue =
            studioLogoTransform != null
                ? studioLogoTransform.localScale
                : Vector3.one;

        Vector3 logoStartScaleValue =
            logoTransform != null
                ? logoTransform.localScale
                : Vector3.one;

        Vector3 backdropStartScaleValue =
            gameBackdropTransform != null
                ? gameBackdropTransform.localScale
                : Vector3.one;

        Vector3 glowStartScaleValue =
            glowTransform != null
                ? glowTransform.localScale
                : Vector3.one;

        float soundStartVolume =
            introAudioSource != null
                ? introAudioSource.volume
                : 0f;

        if (finalFadeOutDuration <= 0f)
        {
            ApplyFullyHiddenState();
            StopIntroSoundImmediately();
            LoadNextScene();
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < finalFadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / finalFadeOutDuration
            );

            float eased = EaseInOut(progress);

            SetAlpha(
                studioLogoGroup,
                Mathf.Lerp(studioStartAlpha, 0f, eased)
            );

            SetAlpha(
                logoGroup,
                Mathf.Lerp(logoStartAlpha, 0f, eased)
            );

            SetAlpha(
                gameBackdropGroup,
                Mathf.Lerp(backdropStartAlpha, 0f, eased)
            );

            SetAlpha(
                glowGroup,
                Mathf.Lerp(glowStartAlpha, 0f, eased)
            );

            SetAlpha(
                tapToSkipGroup,
                Mathf.Lerp(skipStartAlpha, 0f, eased)
            );

            if (studioLogoTransform != null)
            {
                studioLogoTransform.localScale =
                    Vector3.LerpUnclamped(
                        studioStartScaleValue,
                        Vector3.one * studioEndScale,
                        eased
                    );
            }

            if (logoTransform != null)
            {
                logoTransform.localScale =
                    Vector3.LerpUnclamped(
                        logoStartScaleValue,
                        Vector3.one * logoFadeOutScale,
                        eased
                    );
            }

            if (gameBackdropTransform != null)
            {
                gameBackdropTransform.localScale =
                    Vector3.LerpUnclamped(
                        backdropStartScaleValue,
                        Vector3.one,
                        eased
                    );
            }

            if (glowTransform != null)
            {
                glowTransform.localScale =
                    Vector3.LerpUnclamped(
                        glowStartScaleValue,
                        Vector3.one * glowFadeOutScale,
                        eased
                    );
            }

            if (fadeOutSoundWhenLeaving &&
                introAudioSource != null &&
                introAudioSource.isPlaying)
            {
                introAudioSource.volume = Mathf.Lerp(
                    soundStartVolume,
                    0f,
                    eased
                );
            }

            yield return null;
        }

        ApplyFullyHiddenState();
        StopIntroSoundImmediately();

        loadingRoutine = null;
        LoadNextScene();
    }

    private void PlayIntroSound()
    {
        if (introSoundPlayed)
            return;

        introSoundPlayed = true;

        if (introAudioSource == null || introSound == null)
            return;

        introAudioSource.Stop();
        introAudioSource.clip = introSound;
        introAudioSource.loop = false;
        introAudioSource.playOnAwake = false;
        introAudioSource.ignoreListenerPause = true;
        introAudioSource.volume =
            Mathf.Clamp01(SoundManager.SFXVolume) *
            Mathf.Max(0f, introSoundVolume);

        introAudioSource.Play();
    }

    private void StopIntroSoundImmediately()
    {
        if (introAudioSource == null)
            return;

        if (fadeOutSoundWhenLeaving)
        {
            introAudioSource.volume = 0f;
            introAudioSource.Stop();
        }
    }

    private void LoadNextScene()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError(
                "[IntroController] Next Scene Name is empty.",
                this
            );

            isLoading = false;
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError(
                $"[IntroController] Scene could not be loaded. " +
                $"Check Build Profiles: '{nextSceneName}'",
                this
            );

            isLoading = false;
            return;
        }

        if (SceneTransition.Instance != null)
        {
            SceneTransition.Instance.LoadSceneWithFade(nextSceneName);
            return;
        }

        Debug.LogWarning(
            "[IntroController] SceneTransition was not found. " +
            "Loading the scene directly.",
            this
        );

        SceneManager.LoadScene(nextSceneName);
    }

    private void ResetIntroVisuals()
    {
        SetAlpha(studioLogoGroup, 0f);
        SetAlpha(logoGroup, 0f);
        SetAlpha(gameBackdropGroup, 0f);
        SetAlpha(glowGroup, 0f);
        SetAlpha(tapToSkipGroup, 0f);

        if (studioLogoTransform != null)
        {
            studioLogoTransform.localScale =
                Vector3.one * studioStartScale;
        }

        if (logoTransform != null)
        {
            logoTransform.localScale =
                Vector3.one * logoStartScale;
        }

        if (gameBackdropTransform != null)
        {
            gameBackdropTransform.localScale =
                Vector3.one * backdropStartScale;
        }

        if (glowTransform != null)
        {
            glowTransform.localScale =
                Vector3.one * glowPreRevealStartScale;
        }
    }

    private void ApplyPreRevealGlowState()
    {
        SetAlpha(glowGroup, glowPreRevealAlpha);

        if (glowTransform != null)
        {
            glowTransform.localScale =
                Vector3.one * glowRevealScale;
        }
    }

    private void ApplyGameLogoVisibleState()
    {
        SetAlpha(logoGroup, 1f);
        SetAlpha(gameBackdropGroup, backdropMaxAlpha);
        SetAlpha(glowGroup, glowMaxAlpha);
        SetAlpha(tapToSkipGroup, 1f);

        if (logoTransform != null)
        {
            logoTransform.localScale =
                Vector3.one * logoEndScale;
        }

        if (gameBackdropTransform != null)
        {
            gameBackdropTransform.localScale =
                Vector3.one * backdropEndScale;
        }

        if (glowTransform != null)
        {
            glowTransform.localScale =
                Vector3.one * glowEndScale;
        }
    }

    private void ApplyFullyHiddenState()
    {
        SetAlpha(studioLogoGroup, 0f);
        SetAlpha(logoGroup, 0f);
        SetAlpha(gameBackdropGroup, 0f);
        SetAlpha(glowGroup, 0f);
        SetAlpha(tapToSkipGroup, 0f);
    }

    private void RefreshReferences()
    {
        if (introAudioSource == null)
            introAudioSource = GetComponent<AudioSource>();
    }

    private void ConfigureIntroAudioSource()
    {
        if (introAudioSource == null)
            return;

        AudioSource templateSource = introAudioSource;

        spatialIntroAudioObject =
            new GameObject("IntroSpatialSFX");

        introAudioSource =
            spatialIntroAudioObject.AddComponent<AudioSource>();

        introAudioSource.outputAudioMixerGroup =
            templateSource.outputAudioMixerGroup;

        introAudioSource.priority = templateSource.priority;
        introAudioSource.bypassEffects = templateSource.bypassEffects;
        introAudioSource.bypassListenerEffects = templateSource.bypassListenerEffects;
        introAudioSource.bypassReverbZones = templateSource.bypassReverbZones;
        introAudioSource.ignoreListenerPause = true;
        introAudioSource.ignoreListenerVolume = templateSource.ignoreListenerVolume;

        introAudioSource.playOnAwake = false;
        introAudioSource.loop = false;
        introAudioSource.Stop();

        SoundManager.ConfigureAsWorld3D(introAudioSource);
        PositionIntroAudioAtListenerCenter();
    }

    private void PositionIntroAudioAtListenerCenter()
    {
        if (introAudioSource == null)
            return;

        AudioListener listener =
            FindAnyObjectByType<AudioListener>();

        if (listener != null)
        {
            Transform listenerTransform = listener.transform;
            introAudioSource.transform.position =
                listenerTransform.position +
                listenerTransform.forward * 1.5f;

            return;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            introAudioSource.transform.position =
                mainCamera.transform.position +
                mainCamera.transform.forward * 1.5f;
        }
    }

    private void OnDestroy()
    {
        if (spatialIntroAudioObject != null)
            Destroy(spatialIntroAudioObject);
    }

    private void ConfigureCanvasGroups()
    {
        ConfigureCanvasGroup(studioLogoGroup);
        ConfigureCanvasGroup(logoGroup);
        ConfigureCanvasGroup(gameBackdropGroup);
        ConfigureCanvasGroup(glowGroup);
        ConfigureCanvasGroup(tapToSkipGroup);
    }

    private static void ConfigureCanvasGroup(CanvasGroup group)
    {
        if (group == null)
            return;

        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void StopActiveRoutines()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        if (loadingRoutine != null)
        {
            StopCoroutine(loadingRoutine);
            loadingRoutine = null;
        }
    }

    private bool WasSkipInputPressed()
    {
        bool mousePressed =
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame;

        bool keyboardPressed =
            Keyboard.current != null &&
            Keyboard.current.anyKey.wasPressedThisFrame;

        bool gamepadPressed =
            Gamepad.current != null &&
            (Gamepad.current.buttonSouth.wasPressedThisFrame ||
             Gamepad.current.startButton.wasPressedThisFrame);

        return mousePressed || keyboardPressed || gamepadPressed;
    }

    private static WaitForSecondsRealtime WaitRealtime(float duration)
    {
        return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
    }

    private static void SetAlpha(CanvasGroup group, float value)
    {
        if (group != null)
            group.alpha = Mathf.Clamp01(value);
    }

    private static float GetAlpha(CanvasGroup group, float fallback)
    {
        return group != null
            ? group.alpha
            : Mathf.Clamp01(fallback);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float EaseOutCubic(float value)
    {
        value = Mathf.Clamp01(value);
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInOut(float value)
    {
        value = Mathf.Clamp01(value);

        return value < 0.5f
            ? 4f * value * value * value
            : 1f - Mathf.Pow(-2f * value + 2f, 3f) / 2f;
    }

    private void OnValidate()
    {
        openingBlackDuration = Mathf.Max(0f, openingBlackDuration);
        studioFadeInDuration = Mathf.Max(0f, studioFadeInDuration);
        studioHoldDuration = Mathf.Max(0f, studioHoldDuration);
        studioFadeOutDuration = Mathf.Max(0f, studioFadeOutDuration);
        transitionGapDuration = Mathf.Max(0f, transitionGapDuration);
        glowPreRevealDuration = Mathf.Max(0f, glowPreRevealDuration);
        gameLogoRevealDuration = Mathf.Max(0f, gameLogoRevealDuration);
        gameLogoHoldDuration = Mathf.Max(0f, gameLogoHoldDuration);
        finalFadeOutDuration = Mathf.Max(0f, finalFadeOutDuration);
        minimumSkipDelay = Mathf.Max(0f, minimumSkipDelay);

        studioStartScale = Mathf.Max(0f, studioStartScale);
        studioEndScale = Mathf.Max(0f, studioEndScale);
        logoStartScale = Mathf.Max(0f, logoStartScale);
        logoEndScale = Mathf.Max(0f, logoEndScale);
        logoFadeOutScale = Mathf.Max(0f, logoFadeOutScale);
        backdropStartScale = Mathf.Max(0f, backdropStartScale);
        backdropEndScale = Mathf.Max(0f, backdropEndScale);
        glowPreRevealStartScale = Mathf.Max(0f, glowPreRevealStartScale);
        glowRevealScale = Mathf.Max(0f, glowRevealScale);
        glowEndScale = Mathf.Max(0f, glowEndScale);
        glowFadeOutScale = Mathf.Max(0f, glowFadeOutScale);
        introSoundVolume = Mathf.Max(0f, introSoundVolume);
    }
}