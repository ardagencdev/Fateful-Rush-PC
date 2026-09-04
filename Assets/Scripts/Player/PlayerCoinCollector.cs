using TMPro;
using UnityEngine;

public class PlayerCoinCollector : MonoBehaviour
{
    public static PlayerCoinCollector Instance { get; private set; }
    [Header("References")]
    public PlayerMovement playerMovement;
    public EnemySpawner enemySpawner;
    public SoundManager soundManager;
    public ScoreUIEffect scoreUIEffect;
    public ComboUI comboUI;
    public GameStateManager gameStateManager;

    [SerializeField]
    private SpecialSkinVisuals specialSkinVisuals;

    [SerializeField]
    private PlayerSkinApplier playerSkinApplier;

    [Header("UI")]
    public TextMeshProUGUI scoreText;

    [Header("Combo Magnet")]
    [Tooltip("5x veya 6x comboda yakındaki coinleri oyuncuya doğru yumuşakça çeker.")]
    public bool comboMagnetEnabled = true;

    [Min(0.1f)]
    [Tooltip("5x comboda magnetin dünya birimi cinsinden menzili.")]
    public float combo5MagnetRadius = 1.65f;

    [Min(0.1f)]
    [Tooltip("6x comboda magnetin dünya birimi cinsinden menzili.")]
    public float combo6MagnetRadius = 2.15f;

    [Min(0.1f)]
    [Tooltip("5x comboda coinlerin oyuncuya yaklaşabileceği maksimum hız.")]
    public float combo5MagnetMaxSpeed = 7.2f;

    [Min(0.1f)]
    [Tooltip("6x comboda coinlerin oyuncuya yaklaşabileceği maksimum hız.")]
    public float combo6MagnetMaxSpeed = 9f;

    [Range(0.04f, 0.5f)]
    [Tooltip("Coin hareketinin ne kadar yumuşak olacağını belirler. Küçük değer daha hızlı tepki verir.")]
    public float comboMagnetSmoothTime = 0.16f;

    [Range(0f, 0.95f)]
    [Tooltip("Magnet menzilinin dış kenarında çekim çok hafif başlar. 0.35 = ilk girişte maksimum hızın %35'i.")]
    public float comboMagnetEdgeSpeedFactor = 0.35f;

    [Header("Combo")]
    public bool comboEnabled = true;

    [Min(0.01f)]
    public float comboTimeLimit = 1.5f;

    [Min(1)]
    public int coinsForCombo2 = 2;

    [Min(1)]
    public int coinsForCombo3 = 5;

    [Tooltip(
        "LevelConfig'den gelir. Boşsa eski coinsForCombo2/3 sistemi kullanılır."
    )]
    public ComboSpeedStage[] comboSpeedStages;

    private int score;
    private int coinsCollectedThisRun;
    private int combo = 1;
    private int comboChain;
    private float comboTimer;

    public int Score => score;
    public int CoinsCollectedThisRun => coinsCollectedThisRun;
    public int Combo => combo;

    private void Awake()
    {
        Instance = this;

        if (playerMovement == null)
        {
            playerMovement =
                GetComponent<PlayerMovement>();
        }

        if (gameStateManager == null)
        {
            gameStateManager =
                FindAnyObjectByType<GameStateManager>();
        }

        if (specialSkinVisuals == null)
        {
            specialSkinVisuals =
                GetComponent<SpecialSkinVisuals>();
        }

        if (playerSkinApplier == null)
        {
            playerSkinApplier =
                GetComponent<PlayerSkinApplier>();
        }

        UpdateScoreUI();

        if (comboUI != null)
            comboUI.UpdateCombo(1);
    }

    private void Update()
    {
        if (IsGameOver())
            return;

        if (!comboEnabled)
            return;

        if (comboChain <= 0)
            return;

        comboTimer += Time.deltaTime;

        float normalizedTime =
            1f - (comboTimer / comboTimeLimit);

        normalizedTime =
            Mathf.Clamp01(normalizedTime);

        if (comboUI != null)
        {
            comboUI.UpdateTimerBar(
                normalizedTime,
                combo
            );
        }

        if (comboTimer >= comboTimeLimit)
            ResetCombo();
    }

    private void OnTriggerEnter2D(
        Collider2D other)
    {
        if (!CanCollectCoin())
            return;

        if (!other.CompareTag("Coin"))
            return;

        CollectCoin(other);
    }

    private void OnTriggerStay2D(
        Collider2D other)
    {
        if (!CanCollectCoin())
            return;

        if (!other.CompareTag("Coin"))
            return;

        // Mobile frame hitches or a just-restored Rigidbody2D can occasionally
        // miss the initial Enter callback. Stay gives coin collection a safe
        // second chance; Coin.TryBeginCollection still prevents duplicates.
        CollectCoin(other);
    }

    private void CollectCoin(
        Collider2D coinCollider)
    {
        if (coinCollider == null)
            return;

        Coin coin =
            coinCollider.GetComponentInParent<Coin>();

        if (coin != null)
        {
            if (!coin.TryBeginCollection())
                return;
        }
        else
        {
            if (!coinCollider.enabled)
                return;

            DisableFallbackCoinPhysics(coinCollider);
        }

        RunOptional(
            () => VibrationManager.Instance?.VibrateCoin(),
            "coin vibration"
        );

        int currentCombo =
            UpdateCombo();

        int coinValue =
            coin != null
                ? Mathf.Max(1, coin.value)
                : 1;

        int gainedScore =
            coinValue * currentCombo;

        score += gainedScore;
        coinsCollectedThisRun++;

        try
        {
            StatsManager.AddScore(
                gainedScore,
                coinValue
            );

            if (coin != null)
            {
                StatsManager.AddCoin(
                    coinValue,
                    coin.Type
                );

                if (coin.WasMagnetAffected)
                    StatsManager.AddMagnetCoin();
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "[PlayerCoinCollector] Stat/achievement update failed. " +
                "Coin collection and gameplay will continue.",
                this
            );
            Debug.LogException(exception, this);
        }

        RunOptional(UpdateScoreUI, "update score UI");

        // Score-objective progression is core gameplay. Check it before any
        // boss/UI/VFX/audio feedback so those optional systems cannot swallow
        // a win when this coin reaches the target score.
        if (gameStateManager == null)
        {
            gameStateManager =
                FindAnyObjectByType<GameStateManager>(
                    FindObjectsInactive.Include
                );
        }

        if (gameStateManager != null)
        {
            try
            {
                gameStateManager.CheckScoreObjective(score);
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    "[PlayerCoinCollector] Score-objective check failed.",
                    this
                );
                Debug.LogException(exception, this);
            }
        }

        if (!GameStateManager.IsGameplayEnded)
        {
            RunOptional(
                () => enemySpawner?.TrySpawnBoss(score),
                "boss spawn check after coin"
            );
        }

        RunOptional(
            () =>
            {
                if (comboUI != null && comboEnabled)
                {
                    comboUI.ShowCombo(
                        gainedScore,
                        currentCombo
                    );
                }
            },
            "combo UI feedback"
        );

        RunOptional(
            () => scoreUIEffect?.PlayPop(),
            "score pop effect"
        );

        RunOptional(
            () => PlaySpecialSkinCoinEffect(
                coin,
                coinCollider,
                coinValue
            ),
            "special-skin coin effect"
        );

        // Releasing/hiding the collected coin is important enough to have its
        // own fallback. Even a broken collect animation cannot leave an
        // already-counted, non-interactable coin stuck in the scene.
        try
        {
            PlayCollectEffect(coin, coinCollider);
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "[PlayerCoinCollector] Coin collect effect failed. " +
                "Releasing the coin with a safe fallback.",
                this
            );
            Debug.LogException(exception, this);

            GameObject coinObject =
                coin != null
                    ? coin.gameObject
                    : coinCollider != null
                        ? coinCollider.transform.root.gameObject
                        : null;

            if (coinObject != null)
                RuntimeObjectPool.Release(coinObject);
        }

        RunOptional(
            () =>
            {
                if (soundManager == null)
                    return;

                string activeSkinId =
                    specialSkinVisuals != null
                        ? specialSkinVisuals.ActiveSkinId
                        : playerSkinApplier != null &&
                          playerSkinApplier.CurrentSkin != null
                            ? playerSkinApplier.CurrentSkin.id
                            : string.Empty;

                Vector3 coinSoundPosition =
                    coinCollider != null
                        ? coinCollider.transform.position
                        : transform.position;

                soundManager.PlayCoinSound(
                    activeSkinId,
                    coinSoundPosition
                );
            },
            "coin collect sound"
        );
    }

    private int UpdateCombo()
    {
        if (!comboEnabled)
        {
            combo = 1;
            comboChain = 0;
            comboTimer = 0f;

            return 1;
        }

        int previousCombo = combo;

        comboTimer = 0f;
        comboChain++;

        combo =
            GetComboFromChain();

        RunOptional(
            () => StatsManager.RecordComboProgress(
                combo,
                comboChain,
                combo > previousCombo
            ),
            "combo stat/achievement update"
        );

        return combo;
    }

    private int GetComboFromChain()
    {
        if (comboSpeedStages != null &&
            comboSpeedStages.Length > 0)
        {
            int result = 1;

            for (int i = 0;
                 i < comboSpeedStages.Length;
                 i++)
            {
                ComboSpeedStage stage =
                    comboSpeedStages[i];

                if (stage == null)
                    continue;

                if (stage.comboMultiplier < 2)
                    continue;

                if (stage.coinsRequired < 1)
                    continue;

                if (comboChain >=
                    stage.coinsRequired)
                {
                    result = Mathf.Max(
                        result,
                        stage.comboMultiplier
                    );
                }
            }

            return Mathf.Max(1, result);
        }

        int fallbackResult = 1;

        if (comboChain >= coinsForCombo3)
        {
            fallbackResult = 3;
        }
        else if (comboChain >= coinsForCombo2)
        {
            fallbackResult = 2;
        }

        return Mathf.Max(1, fallbackResult);
    }

    private void ResetCombo()
    {
        combo = 1;
        comboChain = 0;
        comboTimer = 0f;

        if (comboUI == null)
            return;

        comboUI.ResetCombo();

        comboUI.UpdateTimerBar(
            0f,
            combo
        );
    }


    private void PlaySpecialSkinCoinEffect(
        Coin coin,
        Collider2D coinCollider,
        int coinValue)
    {
        if (specialSkinVisuals == null)
        {
            specialSkinVisuals =
                GetComponent<SpecialSkinVisuals>();
        }

        if (specialSkinVisuals == null)
            return;

        Vector3 burstPosition;

        if (coin != null)
        {
            burstPosition = coin.transform.position;
        }
        else if (coinCollider != null)
        {
            burstPosition = coinCollider.bounds.center;
        }
        else
        {
            return;
        }

        float coinWorldSize = GetCoinWorldSize(
            coin,
            coinCollider
        );

        specialSkinVisuals.PlayCoinCollectBurst(
            burstPosition,
            coinValue,
            coinWorldSize
        );
    }

    private static float GetCoinWorldSize(
        Coin coin,
        Collider2D coinCollider)
    {
        SpriteRenderer coinRenderer = null;

        if (coin != null)
        {
            coinRenderer =
                coin.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (coinRenderer == null && coinCollider != null)
        {
            coinRenderer =
                coinCollider.GetComponentInParent<SpriteRenderer>();

            if (coinRenderer == null)
            {
                coinRenderer =
                    coinCollider.GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        if (coinRenderer != null)
        {
            Vector3 size = coinRenderer.bounds.size;
            float worldSize = Mathf.Max(size.x, size.y);

            if (worldSize > 0.001f)
                return worldSize;
        }

        if (coinCollider != null)
        {
            Vector3 size = coinCollider.bounds.size;
            float worldSize = Mathf.Max(size.x, size.y);

            if (worldSize > 0.001f)
                return worldSize;
        }

        return 0.6f;
    }

    private void PlayCollectEffect(
        Coin coin,
        Collider2D coinCollider)
    {
        SpawnScaleEffect coinEffect = null;

        if (coin != null)
        {
            // Efekt coin prefabındaki Visual child objesinde bulunuyor.
            coinEffect =
                coin.GetComponentInChildren<SpawnScaleEffect>(true);
        }

        if (coinEffect == null && coinCollider != null)
        {
            coinEffect =
                coinCollider.GetComponentInChildren<SpawnScaleEffect>(true);
        }

        if (coinEffect != null)
        {
            coinEffect.Collect();
            return;
        }

        GameObject coinObject =
            coin != null
                ? coin.gameObject
                : coinCollider.transform.root.gameObject;

        Destroy(coinObject);
    }

    private static void DisableFallbackCoinPhysics(
        Collider2D coinCollider)
    {
        Transform root = coinCollider.transform.root;

        Collider2D[] colliders =
            root.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Rigidbody2D[] rigidbodies =
            root.GetComponentsInChildren<Rigidbody2D>(true);

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody2D body = rigidbodies[i];

            if (body != null)
                body.simulated = false;
        }
    }

    public bool TryGetComboMagnetSettings(
        Vector3 coinPosition,
        out float maxSpeed,
        out float smoothTime)
    {
        maxSpeed = 0f;
        smoothTime = comboMagnetSmoothTime;

        if (!comboMagnetEnabled ||
            !comboEnabled ||
            IsGameOver() ||
            !GameStateManager.IsGameplayStarted)
        {
            return false;
        }

        float radius;
        float baseMaxSpeed;

        if (combo >= 6)
        {
            radius = combo6MagnetRadius;
            baseMaxSpeed = combo6MagnetMaxSpeed;
        }
        else if (combo >= 5)
        {
            radius = combo5MagnetRadius;
            baseMaxSpeed = combo5MagnetMaxSpeed;
        }
        else
        {
            return false;
        }

        Vector2 delta =
            (Vector2)transform.position -
            (Vector2)coinPosition;

        float radiusSquared = radius * radius;
        float distanceSquared = delta.sqrMagnitude;

        if (distanceSquared > radiusSquared)
            return false;

        float distance = Mathf.Sqrt(distanceSquared);
        float closeness =
            1f - Mathf.Clamp01(distance / radius);

        // Menzilin kenarında hafif, oyuncuya yaklaştıkça daha güçlü çekim.
        float strength = Mathf.SmoothStep(
            comboMagnetEdgeSpeedFactor,
            1f,
            closeness
        );

        maxSpeed = Mathf.Max(
            0.1f,
            baseMaxSpeed * strength
        );

        smoothTime = comboMagnetSmoothTime;
        return true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


    private void RunOptional(
        System.Action action,
        string operation)
    {
        if (action == null)
            return;

        try
        {
            action();
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                $"[PlayerCoinCollector] Optional {operation} failed. " +
                "Coin collection/gameplay continues.",
                this
            );
            Debug.LogException(exception, this);
        }
    }

    private bool CanCollectCoin()
    {
        return !IsGameOver() &&
               GameStateManager.IsGameplayStarted &&
               !GameStateManager.IsGameplayEnded &&
               Time.timeScale > 0f;
    }

    private bool IsGameOver()
    {
        return playerMovement != null &&
               playerMovement.IsGameOver;
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text =
                $"Score: {score}";
        }
    }

    private void OnValidate()
    {
        comboTimeLimit =
            Mathf.Max(
                0.01f,
                comboTimeLimit
            );

        coinsForCombo2 =
            Mathf.Max(
                1,
                coinsForCombo2
            );

        coinsForCombo3 =
            Mathf.Max(
                coinsForCombo2,
                coinsForCombo3
            );

        combo5MagnetRadius =
            Mathf.Max(0.1f, combo5MagnetRadius);

        combo6MagnetRadius =
            Mathf.Max(combo5MagnetRadius, combo6MagnetRadius);

        combo5MagnetMaxSpeed =
            Mathf.Max(0.1f, combo5MagnetMaxSpeed);

        combo6MagnetMaxSpeed =
            Mathf.Max(combo5MagnetMaxSpeed, combo6MagnetMaxSpeed);

        comboMagnetSmoothTime =
            Mathf.Clamp(comboMagnetSmoothTime, 0.04f, 0.5f);

        comboMagnetEdgeSpeedFactor =
            Mathf.Clamp(comboMagnetEdgeSpeedFactor, 0f, 0.95f);
    }
}

