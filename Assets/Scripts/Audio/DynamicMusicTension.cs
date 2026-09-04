using UnityEngine;

public class DynamicMusicTension : MonoBehaviour
{
    [Header("Progress Thresholds")]
    [SerializeField, Range(0f, 0.95f)]
    [Tooltip("Reach Score görevlerinde hedefin hangi yüzdesinden sonra tension başlayacak. 0.60 = %60.")]
    private float scoreTensionStart = 0.60f;

    [SerializeField, Range(0f, 0.95f)]
    [Tooltip("Timed görevlerde sürenin ne kadarı geçtikten sonra tension başlayacak. 0.625 = sürenin yaklaşık son %37.5'i.")]
    private float timeTensionStart = 0.625f;

    [Header("Response")]
    [SerializeField, Min(0.02f)]
    [Tooltip("Objective değeri küçük küçük değişirken bile gereksiz sık hesap yapmamak için update aralığı.")]
    private float evaluationInterval = 0.08f;

    private GameStateManager gameStateManager;
    private PlayerCoinCollector coinCollector;
    private GameplayMusicFade gameplayMusic;
    private LevelConfig levelConfig;

    private float evaluationTimer;
    private bool configured;

    public float CurrentRawTension { get; private set; }

    public void Configure(
        GameStateManager stateManager,
        PlayerCoinCollector collector,
        GameplayMusicFade music,
        LevelConfig level)
    {
        gameStateManager = stateManager;
        coinCollector = collector;
        gameplayMusic = music;
        levelConfig = level;

        evaluationTimer = 0f;
        CurrentRawTension = 0f;
        configured = true;

        gameplayMusic?.ResetTension(true);
    }

    private void Update()
    {
        if (!configured ||
            gameplayMusic == null ||
            levelConfig == null)
        {
            return;
        }

        // Gameplay intro, result screen ve sahne geçişinde müzik normal karakterine döner.
        if (!GameStateManager.IsGameplayStarted ||
            GameStateManager.IsGameplayEnded)
        {
            if (CurrentRawTension > 0f)
            {
                CurrentRawTension = 0f;
                gameplayMusic.SetTension(0f);
            }

            return;
        }

        // Pause sırasında objective ilerlemediği için mevcut tension korunur.
        // Müzik zaten GameplayMusicFade tarafından fade-out + pause edilir.
        if (Time.timeScale <= 0f)
            return;

        evaluationTimer -= Time.unscaledDeltaTime;

        if (evaluationTimer > 0f)
            return;

        evaluationTimer = Mathf.Max(
            0.02f,
            evaluationInterval
        );

        CurrentRawTension = CalculateTension();
        gameplayMusic.SetTension(CurrentRawTension);
    }

    private float CalculateTension()
    {
        switch (levelConfig.winCondition)
        {
            case WinConditionType.ReachScore:
                return CalculateScoreTension();

            case WinConditionType.SurviveTime:
                return CalculateTimeTension();

            case WinConditionType.ReachScoreWithinTime:
                return Mathf.Max(
                    CalculateScoreTension(),
                    CalculateTimeTension()
                );

            default:
                return 0f;
        }
    }

    private float CalculateScoreTension()
    {
        int currentScore =
            coinCollector != null
                ? coinCollector.Score
                : 0;

        float scoreProgress = Mathf.Clamp01(
            currentScore /
            (float)levelConfig.SafeWinScore
        );

        return NormalizePressure(
            scoreProgress,
            scoreTensionStart
        );
    }

    private float CalculateTimeTension()
    {
        float elapsed =
            gameStateManager != null
                ? gameStateManager.ElapsedGameTime
                : 0f;

        float timeProgress = Mathf.Clamp01(
            elapsed / levelConfig.SafeTimeLimit
        );

        return NormalizePressure(
            timeProgress,
            timeTensionStart
        );
    }

    private static float NormalizePressure(
        float progress,
        float startThreshold)
    {
        progress = Mathf.Clamp01(progress);
        startThreshold = Mathf.Clamp(
            startThreshold,
            0f,
            0.99f
        );

        if (progress <= startThreshold)
            return 0f;

        float normalized = Mathf.InverseLerp(
            startThreshold,
            1f,
            progress
        );

        // Basamak hissi yerine yumuşak bir pressure ramp.
        return normalized * normalized *
               (3f - 2f * normalized);
    }

    private void OnDisable()
    {
        gameplayMusic?.ResetTension(true);
        CurrentRawTension = 0f;
    }

    private void OnValidate()
    {
        scoreTensionStart = Mathf.Clamp(
            scoreTensionStart,
            0f,
            0.95f
        );

        timeTensionStart = Mathf.Clamp(
            timeTensionStart,
            0f,
            0.95f
        );

        evaluationInterval = Mathf.Max(
            0.02f,
            evaluationInterval
        );
    }
}
