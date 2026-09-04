using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public PlayerCoinCollector coinCollector;
    public PlayerDash playerDash;
    public GameStateManager gameStateManager;

    [Header("Movement")]
    [Min(0f)]
    public float speed = 7f;

    public const float FixedComboSpeedBonus = 1.15f;

    [HideInInspector]
    public float comboSpeedBonus = FixedComboSpeedBonus;

    [Tooltip(
        "LevelConfig'den gelir. Bos ise eski " +
        "comboSpeedBonus sistemi kullanilir."
    )]
    public ComboSpeedStage[] comboSpeedStages;

    [Header("Near Miss Boost")]
    [Tooltip("Near Miss sonrasi uygulanan sabit hareket hizi carpani. 1.05 = %5 boost.")]
    [SerializeField, Range(1f, 1.20f)]
    private float nearMissSpeedMultiplier = 1.05f;

    [Tooltip("Near Miss hareket bonusunun gercek zaman cinsinden suresi.")]
    [SerializeField, Min(0.05f)]
    private float nearMissBoostDuration = 1f;

    [Header("Movement Feel")]
    [Min(0f)]
    public float acceleration = 70f;

    [Min(0f)]
    public float deceleration = 100f;

    [Tooltip(
        "Kucuk yon duzeltmelerinin ne kadar hizli " +
        "donecegini belirler. Void Clone da bu degeri kullanir."
    )]
    [Min(0f)]
    public float turnAcceleration = 240f;

    [Header("Responsive Turning")]
    [Tooltip(
        "Bu acinin ustundeki sert donusler gecikmesiz uygulanir."
    )]
    [Range(0f, 180f)]
    public float instantSharpTurnAngle = 60f;

    [Range(1f, 2f)]
    public float sharpTurnBoost = 1.5f;

    [Header("Analog Input")]
    [Range(0.3f, 1f)]
    public float lowInputAccelerationMultiplier = 0.75f;

    [Range(1f, 2f)]
    public float highInputAccelerationMultiplier = 1.15f;

    [Range(0f, 0.5f)]
    public float minInputToMove = 0.03f;

    private Rigidbody2D rb;
    private Collider2D mainCollider;
    private DeathFadeEffect deathFade;
    private SpecialSkinVisuals specialSkinVisuals;

    private Vector2 moveInput;
    private Vector2 currentVelocity;

    private Vector3 originalScale;
    private int facingDirection = 1;

    private float nearMissBoostEndTime = -100f;

    public Vector2 LastMoveDirection { get; private set; } =
        Vector2.right;

    public Vector2 CurrentVelocity => currentVelocity;
    public Vector2 CurrentMoveInput => moveInput;

    public Vector2 VisualMoveDirection
    {
        get
        {
            if (currentVelocity.sqrMagnitude > 0.0001f)
                return currentVelocity.normalized;

            if (playerDash != null &&
                playerDash.IsDashing &&
                LastMoveDirection.sqrMagnitude > 0.0001f)
            {
                return LastMoveDirection.normalized;
            }

            if (moveInput.sqrMagnitude > 0.0001f)
                return moveInput.normalized;

            return Vector2.zero;
        }
    }

    public float CurrentMoveSpeed => GetCurrentSpeed();

    public bool IsGameOver { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCollider = GetComponent<Collider2D>();
        deathFade = GetComponent<DeathFadeEffect>();
        specialSkinVisuals = GetComponent<SpecialSkinVisuals>();

        if (coinCollector == null)
            coinCollector = GetComponent<PlayerCoinCollector>();

        if (playerDash == null)
            playerDash = GetComponent<PlayerDash>();

        if (gameStateManager == null)
        {
            gameStateManager =
                FindAnyObjectByType<GameStateManager>();
        }

        originalScale = transform.localScale;

        rb.interpolation =
            RigidbodyInterpolation2D.Interpolate;

        // A previous game-end path may have disabled Rigidbody2D simulation.
        // Every freshly loaded Player must always start with working physics.
        EnsureGameplayPhysicsReady();
    }

    private void OnEnable()
    {
        if (!IsGameOver)
            EnsureGameplayPhysicsReady();
    }

    private void FixedUpdate()
    {
        if (GameStateManager.IsGameplayStarted &&
            !IsGameOver &&
            ((rb != null && !rb.simulated) ||
             (mainCollider != null && !mainCollider.enabled)))
        {
            Debug.LogWarning(
                "[PlayerMovement] Gameplay sırasında Player physics kapalı bulundu. " +
                "Runtime state otomatik onarılıyor.",
                this
            );

            EnsureGameplayPhysicsReady();
        }

        if (Time.timeScale == 0f)
        {
            StopMovement();
            return;
        }

        float deltaTime = GetPlayerDeltaTime();

        if (!GameStateManager.IsGameplayStarted ||
            IsGameOver)
        {
            SmoothStop(deltaTime);
            return;
        }

        // PlayerDash moves the Rigidbody2D itself.
        if (playerDash != null &&
            playerDash.IsDashing)
        {
            currentVelocity = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        currentVelocity = CalculateNextVelocity(
            GetCurrentSpeed(),
            deltaTime
        );

        rb.linearVelocity = Vector2.zero;

        if (currentVelocity.sqrMagnitude <= 0.0001f)
        {
            currentVelocity = Vector2.zero;
            return;
        }

        rb.MovePosition(
            rb.position +
            currentVelocity * deltaTime
        );
    }

    private Vector2 CalculateNextVelocity(
        float maximumSpeed,
        float deltaTime
    )
    {
        float inputMagnitude = moveInput.magnitude;
        float currentMagnitude = currentVelocity.magnitude;

        if (inputMagnitude <= 0.001f)
        {
            float stoppedMagnitude = Mathf.MoveTowards(
                currentMagnitude,
                0f,
                deceleration * deltaTime
            );

            if (stoppedMagnitude <= 0.001f ||
                currentMagnitude <= 0.001f)
            {
                return Vector2.zero;
            }

            return currentVelocity.normalized *
                   stoppedMagnitude;
        }

        Vector2 desiredDirection =
            moveInput / inputMagnitude;

        float targetMagnitude =
            maximumSpeed * inputMagnitude;

        float speedChangeRate =
            targetMagnitude >= currentMagnitude
                ? GetAdaptiveAccelerationRate(
                    inputMagnitude
                )
                : deceleration;

        float newMagnitude = Mathf.MoveTowards(
            currentMagnitude,
            targetMagnitude,
            speedChangeRate * deltaTime
        );

        Vector2 newDirection =
            GetResponsiveDirection(
                desiredDirection,
                currentMagnitude,
                deltaTime
            );

        return newDirection * newMagnitude;
    }

    private float GetAdaptiveAccelerationRate(
        float inputMagnitude
    )
    {
        return acceleration * Mathf.Lerp(
            lowInputAccelerationMultiplier,
            highInputAccelerationMultiplier,
            inputMagnitude
        );
    }

    private Vector2 GetResponsiveDirection(
        Vector2 desiredDirection,
        float currentMagnitude,
        float deltaTime
    )
    {
        if (currentMagnitude <= 0.001f ||
            currentVelocity.sqrMagnitude <= 0.0001f)
        {
            return desiredDirection;
        }

        Vector2 currentDirection =
            currentVelocity.normalized;

        float turnAngle = Vector2.Angle(
            currentDirection,
            desiredDirection
        );

        if (turnAngle <= 0.01f)
            return desiredDirection;

        if (instantSharpTurnAngle <= 0f ||
            turnAngle >= instantSharpTurnAngle)
        {
            return desiredDirection;
        }

        float turnAmount = Mathf.InverseLerp(
            0f,
            Mathf.Max(1f, instantSharpTurnAngle),
            turnAngle
        );

        float responsiveTurnAcceleration =
            turnAcceleration * Mathf.Lerp(
                1f,
                sharpTurnBoost,
                turnAmount
            );

        // Converts lateral acceleration into a stable angular speed.
        float maximumDegreesDelta =
            responsiveTurnAcceleration /
            Mathf.Max(0.5f, currentMagnitude) *
            Mathf.Rad2Deg *
            deltaTime;

        return RotateDirectionTowards(
            currentDirection,
            desiredDirection,
            maximumDegreesDelta
        );
    }

    private static Vector2 RotateDirectionTowards(
        Vector2 currentDirection,
        Vector2 targetDirection,
        float maximumDegreesDelta
    )
    {
        float currentAngle = Mathf.Atan2(
            currentDirection.y,
            currentDirection.x
        ) * Mathf.Rad2Deg;

        float targetAngle = Mathf.Atan2(
            targetDirection.y,
            targetDirection.x
        ) * Mathf.Rad2Deg;

        float newAngle = Mathf.MoveTowardsAngle(
            currentAngle,
            targetAngle,
            maximumDegreesDelta
        );

        float angleInRadians =
            newAngle * Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Cos(angleInRadians),
            Mathf.Sin(angleInRadians)
        );
    }

    private float GetCurrentSpeed()
    {
        float currentSpeed = speed;

        if (coinCollector != null)
        {
            bool hasConfiguredStages =
                comboSpeedStages != null &&
                comboSpeedStages.Length > 0;

            if (hasConfiguredStages)
            {
                float multiplier =
                    GetComboSpeedMultiplier(
                        coinCollector.Combo
                    );

                if (multiplier > 0f)
                    currentSpeed *= multiplier;
            }
            else if (coinCollector.Combo >= 3)
            {
                // Legacy fallback when no combo stages are configured.
                currentSpeed += comboSpeedBonus;
            }
        }

        if (IsNearMissBoostActive)
            currentSpeed *= nearMissSpeedMultiplier;

        return currentSpeed;
    }

    public bool IsNearMissBoostActive =>
        !IsGameOver &&
        Time.unscaledTime < nearMissBoostEndTime;

    public void ApplyNearMissBoost()
    {
        if (IsGameOver ||
            !GameStateManager.IsGameplayStarted ||
            GameStateManager.IsGameplayEnded)
        {
            return;
        }

        // Near Miss'ler hizi stacklemez. Yeni Near Miss sadece sureyi yeniler.
        nearMissBoostEndTime =
            Time.unscaledTime + nearMissBoostDuration;
    }

    private void ClearNearMissBoost()
    {
        nearMissBoostEndTime = -100f;
    }

    private float GetComboSpeedMultiplier(
        int currentCombo
    )
    {
        if (currentCombo <= 1 ||
            comboSpeedStages == null ||
            comboSpeedStages.Length == 0)
        {
            return 0f;
        }

        float bestMultiplier = 0f;
        int bestCombo = 1;

        for (int i = 0;
             i < comboSpeedStages.Length;
             i++)
        {
            ComboSpeedStage stage =
                comboSpeedStages[i];

            if (stage == null)
                continue;

            if (stage.comboMultiplier <= bestCombo)
                continue;

            if (currentCombo < stage.comboMultiplier)
                continue;

            bestCombo = stage.comboMultiplier;

            bestMultiplier = Mathf.Max(
                1f,
                stage.playerSpeedMultiplier
            );
        }

        return bestMultiplier;
    }

    public void SetMoveInput(Vector2 input)
    {
        if (Time.timeScale == 0f ||
            !GameStateManager.IsGameplayStarted ||
            IsGameOver)
        {
            moveInput = Vector2.zero;
            return;
        }

        input = Vector2.ClampMagnitude(input, 1f);

        if (input.magnitude < minInputToMove)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = input;
        LastMoveDirection = moveInput.normalized;

        UpdateFacing(moveInput.x);
    }

    private void SmoothStop(float deltaTime)
    {
        moveInput = Vector2.zero;

        float currentMagnitude =
            currentVelocity.magnitude;

        float stoppedMagnitude = Mathf.MoveTowards(
            currentMagnitude,
            0f,
            deceleration * deltaTime
        );

        currentVelocity =
            stoppedMagnitude > 0.001f &&
            currentMagnitude > 0.001f
                ? currentVelocity.normalized *
                  stoppedMagnitude
                : Vector2.zero;

        rb.linearVelocity = Vector2.zero;
    }

    private void UpdateFacing(float horizontalInput)
    {
        if (horizontalInput > 0.05f &&
            facingDirection != 1)
        {
            facingDirection = 1;

            transform.localScale = new Vector3(
                Mathf.Abs(originalScale.x),
                originalScale.y,
                originalScale.z
            );
        }
        else if (horizontalInput < -0.05f &&
                 facingDirection != -1)
        {
            facingDirection = -1;

            transform.localScale = new Vector3(
                -Mathf.Abs(originalScale.x),
                originalScale.y,
                originalScale.z
            );
        }
    }

    public void StopMovement()
    {
        moveInput = Vector2.zero;
        currentVelocity = Vector2.zero;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    public void PrepareForGameplay()
    {
        IsGameOver = false;
        ClearNearMissBoost();
        StopMovement();
        EnsureGameplayPhysicsReady();
    }

    public void EnsureGameplayPhysicsReady()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (mainCollider == null)
            mainCollider = GetComponent<Collider2D>();

        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (mainCollider != null)
            mainCollider.enabled = true;
    }

    public void SetGameOver(bool value)
    {
        IsGameOver = value;

        if (value)
        {
            ClearNearMissBoost();
            StopMovement();
        }
    }

    public void GameOver(
        string deathCause = "UNKNOWN"
    )
    {
        if (IsGameOver || GameStateManager.IsGameplayEnded)
            return;

        if (gameStateManager == null)
        {
            gameStateManager =
                FindAnyObjectByType<GameStateManager>(
                    FindObjectsInactive.Include
                );
        }

        IsGameOver = true;
        ClearNearMissBoost();
        StopMovement();

        LastDeathInfo.Cause =
            string.IsNullOrWhiteSpace(deathCause)
                ? "UNKNOWN"
                : deathCause;

        int finalScore =
            coinCollector != null
                ? coinCollector.Score
                : 0;

        if (specialSkinVisuals == null)
            specialSkinVisuals = GetComponent<SpecialSkinVisuals>();

        // Death VFX is optional. Never let a skin/fade effect prevent the
        // authoritative GameStateManager -> GameResultUI path from running.
        try
        {
            specialSkinVisuals?.PlayDeathEffect();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        try
        {
            deathFade?.Play();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (gameStateManager != null)
        {
            try
            {
                gameStateManager.GameOver(
                    finalScore,
                    LastDeathInfo.Cause
                );
                return;
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    "[PlayerMovement] GameStateManager.GameOver threw an exception. " +
                    "Emergency result fallback will be used.",
                    this
                );
                Debug.LogException(exception, this);
            }
        }

        ShowEmergencyGameOverFallback(
            finalScore,
            LastDeathInfo.Cause
        );
    }

    private void ShowEmergencyGameOverFallback(
        int finalScore,
        string cause)
    {
        // Last-resort protection for a broken/missing GameStateManager. The run
        // must never continue invisibly after the Player has already died.
        Time.timeScale = 0f;

        GameResultUI emergencyResultUI =
            FindAnyObjectByType<GameResultUI>(
                FindObjectsInactive.Include
            );

        if (emergencyResultUI != null)
        {
            emergencyResultUI.ShowLose(
                finalScore,
                gameStateManager != null
                    ? gameStateManager.ElapsedGameTime
                    : 0f,
                cause
            );
            return;
        }

        Debug.LogError(
            "[PlayerMovement] GameStateManager ve GameResultUI kullanılamıyor. " +
            "Run güvenli şekilde donduruldu.",
            this
        );
    }

    private float GetPlayerDeltaTime()
    {
        // Keeps player speed unchanged during global slow motion.
        if (Time.timeScale <= 0f)
            return Time.fixedDeltaTime;

        return Time.fixedDeltaTime /
               Time.timeScale;
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        comboSpeedBonus = FixedComboSpeedBonus;
        nearMissSpeedMultiplier =
            Mathf.Clamp(nearMissSpeedMultiplier, 1f, 1.20f);
        nearMissBoostDuration =
            Mathf.Max(0.05f, nearMissBoostDuration);

        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        turnAcceleration =
            Mathf.Max(0f, turnAcceleration);
    }
}
