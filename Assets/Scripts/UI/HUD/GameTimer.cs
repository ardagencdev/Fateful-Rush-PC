using System.Collections;
using TMPro;
using UnityEngine;

public class GameTimer : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI timerText;

    [Header("Countdown Color")]
    [Tooltip("Countdown başladığında kullanılacak renk.")]
    public Color countdownStartColor = Color.white;

    [Tooltip("Süre sıfıra yaklaştığında kullanılacak renk.")]
    public Color countdownEndColor = Color.red;

    [Tooltip(
        "Renk geçişinin ilerleme eğrisi. X: geçen süre oranı, Y: renk geçiş oranı."
    )]
    public AnimationCurve countdownColorCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Low Time Warning")]
    [Min(0f)]
    [Tooltip("Timerın sallanmaya başlayacağı kalan süre.")]
    public float warningStartTime = 10f;

    [Min(0f)]
    [Tooltip("Son saniyelerdeki maksimum UI sallanma mesafesi.")]
    public float maxShakeDistance = 4f;

    [Min(0f)]
    [Tooltip("Timer sallanmasının hızı.")]
    public float shakeFrequency = 24f;

    [Tooltip(
        "Açıksa süre azaldıkça sallanma şiddeti giderek artar."
    )]
    public bool increaseShakeOverTime = true;

    [Header("Countdown SFX")]
    [Tooltip("Her tam saniye değişiminde çalacak kısa countdown sesi.")]
    public AudioClip countdownTickSound;

    [Tooltip(
        "Countdown sesi için kullanılacak AudioSource. Boşsa bu objede aranır, " +
        "bulunamazsa otomatik oluşturulur."
    )]
    public AudioSource countdownAudioSource;

    [Range(0f, 1f)]
    public float countdownTickVolume = 0.75f;

    [Range(0f, 1f)]
    [Tooltip("Son 10 saniyedeki en yüksek ses çarpanı.")]
    public float finalSecondsVolumeBoost = 0.2f;

    [Range(0f, 1f)]
    [Tooltip("Son 10 saniyedeki en yüksek pitch artışı.")]
    public float finalSecondsPitchBoost = 0.12f;

    [Header("Optimization")]
    [Min(0f)]
    public float uiRefreshInterval = 0.05f;

    private GameStateManager gameStateManager;
    private LevelConfig levelConfig;
    private RectTransform timerRectTransform;

    private float elapsedTime;
    private float uiRefreshTimer;

    private bool useCountdown;
    private int lastDisplayedSecond = -1;

    private Vector2 originalAnchoredPosition;
    private Color originalTextColor = Color.white;

    public bool IsTiming { get; private set; }

    public float ElapsedTime => elapsedTime;

    public float RemainingTime
    {
        get
        {
            if (!useCountdown ||
                levelConfig == null)
            {
                return 0f;
            }

            return Mathf.Max(
                0f,
                levelConfig.SafeTimeLimit - elapsedTime
            );
        }
    }

    private void Awake()
    {
        RefreshReferences();
        CacheInitialVisualState();
        PrepareCountdownAudioSource();
    }

    private IEnumerator Start()
    {
        ApplyLevelConfig();

        ResetTimerState();
        UpdateUI();

        yield return new WaitUntil(
            () => GameStateManager.IsGameplayStarted
        );

        StartTimer();
    }

    private void Update()
    {
        if (!GameStateManager.IsGameplayStarted)
        {
            RestoreTimerPosition();
            return;
        }

        if (!IsTiming)
        {
            RestoreTimerPosition();
            return;
        }

        if (Time.timeScale <= 0f)
        {
            RestoreTimerPosition();
            return;
        }

        UpdateElapsedTime();

        if (useCountdown)
        {
            UpdateCountdownWarning();
            TryPlayCountdownTick();
        }
        else
        {
            RestoreTimerPosition();
        }

        uiRefreshTimer +=
            Time.unscaledDeltaTime;

        if (uiRefreshInterval <= 0f ||
            uiRefreshTimer >= uiRefreshInterval)
        {
            uiRefreshTimer = 0f;
            UpdateUI();
        }
    }

    private void OnDisable()
    {
        RestoreTimerPosition();
    }

    public void StartTimer()
    {
        elapsedTime = 0f;
        uiRefreshTimer = 0f;
        lastDisplayedSecond = -1;
        IsTiming = true;

        RestoreTimerPosition();
        UpdateUI();

        if (useCountdown)
        {
            lastDisplayedSecond =
                Mathf.CeilToInt(RemainingTime);
        }
    }

    public void StopTimer()
    {
        if (!IsTiming)
            return;

        UpdateElapsedTime();

        IsTiming = false;
        RestoreTimerPosition();
        UpdateUI();
    }

    public void ResetTimer()
    {
        ResetTimerState();
        RestoreTimerVisuals();
        UpdateUI();
    }

    private void ResetTimerState()
    {
        elapsedTime = 0f;
        uiRefreshTimer = 0f;
        lastDisplayedSecond = -1;
        IsTiming = false;
    }

    private void UpdateElapsedTime()
    {
        if (gameStateManager != null)
        {
            elapsedTime =
                gameStateManager.ElapsedGameTime;

            return;
        }

        elapsedTime +=
            Time.unscaledDeltaTime;
    }

    private void ApplyLevelConfig()
    {
        LevelManager levelManager =
            FindAnyObjectByType<LevelManager>();

        levelConfig =
            levelManager != null
                ? levelManager.currentLevel
                : null;

        useCountdown =
            levelConfig != null &&
            levelConfig.UsesTime;

        if (!useCountdown)
        {
            RestoreTimerVisuals();
        }
    }

    private void UpdateUI()
    {
        if (timerText == null)
            return;

        if (useCountdown &&
            levelConfig != null)
        {
            float remainingTime = RemainingTime;

            timerText.text =
                FormatTime(remainingTime);

            UpdateCountdownColor(remainingTime);
            return;
        }

        timerText.text =
            FormatTime(elapsedTime);

        timerText.color = originalTextColor;
    }

    private void UpdateCountdownColor(float remainingTime)
    {
        if (timerText == null ||
            levelConfig == null)
        {
            return;
        }

        float duration =
            levelConfig.SafeTimeLimit;

        float elapsedRatio =
            duration <= 0f
                ? 1f
                : 1f - Mathf.Clamp01(
                    remainingTime / duration
                );

        float evaluatedRatio =
            countdownColorCurve != null
                ? countdownColorCurve.Evaluate(elapsedRatio)
                : elapsedRatio;

        timerText.color =
            Color.Lerp(
                countdownStartColor,
                countdownEndColor,
                Mathf.Clamp01(evaluatedRatio)
            );
    }

    private void UpdateCountdownWarning()
    {
        if (timerRectTransform == null)
            return;

        float remainingTime = RemainingTime;

        if (warningStartTime <= 0f ||
            remainingTime <= 0f ||
            remainingTime > warningStartTime)
        {
            RestoreTimerPosition();
            return;
        }

        float urgency =
            1f - Mathf.Clamp01(
                remainingTime / warningStartTime
            );

        float shakeStrength =
            increaseShakeOverTime
                ? Mathf.Lerp(0.35f, 1f, urgency)
                : 1f;

        float time =
            Time.unscaledTime * shakeFrequency;

        Vector2 noiseOffset = new Vector2(
            Mathf.PerlinNoise(time, 0.17f) * 2f - 1f,
            Mathf.PerlinNoise(0.43f, time) * 2f - 1f
        );

        timerRectTransform.anchoredPosition =
            originalAnchoredPosition +
            noiseOffset * maxShakeDistance * shakeStrength;
    }

    private void TryPlayCountdownTick()
    {
        if (countdownTickSound == null ||
            levelConfig == null)
        {
            return;
        }

        int displayedSecond =
            Mathf.CeilToInt(RemainingTime);

        if (displayedSecond == lastDisplayedSecond)
            return;

        bool movedToNextSecond =
            lastDisplayedSecond >= 0 &&
            displayedSecond < lastDisplayedSecond;

        lastDisplayedSecond = displayedSecond;

        if (!movedToNextSecond ||
            displayedSecond <= 0)
        {
            return;
        }

        float urgency = 0f;

        if (warningStartTime > 0f &&
            displayedSecond <= warningStartTime)
        {
            urgency =
                1f - Mathf.Clamp01(
                    displayedSecond / warningStartTime
                );
        }

        float volumeMultiplier =
            countdownTickVolume +
            finalSecondsVolumeBoost * urgency;

        float targetPitch =
            1f + finalSecondsPitchBoost * urgency;

        SoundManager soundManager = SoundManager.Instance;

        if (soundManager != null)
        {
            soundManager.PlayCustomSoundAtUI(
                countdownTickSound,
                timerRectTransform,
                Mathf.Clamp01(volumeMultiplier),
                targetPitch
            );

            return;
        }

        if (countdownAudioSource == null)
            return;

        countdownAudioSource.pitch = targetPitch;
        countdownAudioSource.volume =
            SoundManager.SFXVolume;

        countdownAudioSource.PlayOneShot(
            countdownTickSound,
            Mathf.Clamp01(volumeMultiplier)
        );
    }

    private string FormatTime(float time)
    {
        time = Mathf.Max(0f, time);

        int minutes =
            Mathf.FloorToInt(time / 60f);

        int seconds =
            Mathf.FloorToInt(time % 60f);

        return $"{minutes:00}:{seconds:00}";
    }

    private void RefreshReferences()
    {
        if (timerText == null)
        {
            timerText =
                GetComponent<TextMeshProUGUI>();
        }

        if (timerText != null)
        {
            timerRectTransform =
                timerText.rectTransform;
        }
        else
        {
            timerRectTransform =
                transform as RectTransform;
        }

        if (gameStateManager == null)
        {
            gameStateManager =
                FindAnyObjectByType<GameStateManager>();
        }

        if (timerText == null)
        {
            Debug.LogWarning(
                "GameTimer could not find a TextMeshProUGUI reference.",
                this
            );
        }

        if (gameStateManager == null)
        {
            Debug.LogWarning(
                "GameTimer could not find GameStateManager. " +
                "Timer will use its internal fallback time.",
                this
            );
        }
    }

    private void CacheInitialVisualState()
    {
        if (timerRectTransform != null)
        {
            originalAnchoredPosition =
                timerRectTransform.anchoredPosition;
        }

        if (timerText != null)
        {
            originalTextColor =
                timerText.color;
        }
    }

    private void PrepareCountdownAudioSource()
    {
        if (countdownAudioSource == null)
        {
            countdownAudioSource =
                GetComponent<AudioSource>();
        }

        if (countdownAudioSource == null)
        {
            countdownAudioSource =
                gameObject.AddComponent<AudioSource>();
        }

        countdownAudioSource.playOnAwake = false;
        countdownAudioSource.loop = false;
        SoundManager.ConfigureAsWorld3D(countdownAudioSource);
    }

    private void RestoreTimerPosition()
    {
        if (timerRectTransform == null)
            return;

        timerRectTransform.anchoredPosition =
            originalAnchoredPosition;
    }

    private void RestoreTimerVisuals()
    {
        RestoreTimerPosition();

        if (timerText != null)
        {
            timerText.color = originalTextColor;
        }

        if (countdownAudioSource != null)
        {
            countdownAudioSource.pitch = 1f;
        }
    }

    private void OnValidate()
    {
        uiRefreshInterval =
            Mathf.Max(0f, uiRefreshInterval);

        warningStartTime =
            Mathf.Max(0f, warningStartTime);

        maxShakeDistance =
            Mathf.Max(0f, maxShakeDistance);

        shakeFrequency =
            Mathf.Max(0f, shakeFrequency);

        countdownTickVolume =
            Mathf.Clamp01(countdownTickVolume);

        finalSecondsVolumeBoost =
            Mathf.Clamp01(finalSecondsVolumeBoost);

        finalSecondsPitchBoost =
            Mathf.Clamp01(finalSecondsPitchBoost);
    }
}