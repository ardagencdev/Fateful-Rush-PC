using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class MiniBossFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Movement")]
    public float speed = 2.5f;

    [Tooltip("Normal chase sirasindaki minimal shake miktari.")]
    [Min(0f)] public float normalShakeAmount = 0.01f;

    [Tooltip("Hedef yonundeki degisimlerin ne kadar yumusak olacagi.")]
    public float directionSmoothness = 8f;

    [Header("Coordinated Pursuit")]
    public NormalEnemyPursuitRole pursuitRole =
        NormalEnemyPursuitRole.Pursuer;

    [Min(0f)] public float interceptorLeadDistance = 2.6f;
    [Min(0f)] public float predictionTime = 0.25f;
    [Min(0f)] public float maxPredictionDistance = 1.5f;
    [Min(0.1f)] public float tacticalOffsetFadeDistance = 3.4f;

    [Header("Wave Movement")]
    public float minSideMoveAmount = 0.18f;
    public float maxSideMoveAmount = 0.35f;
    public float minSideMoveSpeed = 1.5f;
    public float maxSideMoveSpeed = 3f;

    [Header("Collision")]
    public LayerMask solidLayers;
    public float castSkin = 0.05f;
    public float obstacleProbeDistance = 0.9f;
    [Range(0f, 1f)] public float obstacleOutwardBias = 0.3f;

    [Tooltip("Duz yol kapaliysa kac farkli kayma acisi denenecek.")]
    [Range(1, 8)]
    public int slideDirectionAttempts = 4;

    [Header("Advanced Unstuck")]
    public LayerMask obstacleLayer;
    public float escapeCheckRadius = 1.2f;
    public float escapeSpeedMultiplier = 2.2f;

    [Header("MiniBoss Local AOE")]
    public bool aoeEnabled = true;

    [Tooltip("MiniBoss dogduktan sonra ilk AOE'nin hazir olmasi icin gereken temel sure.")]
    [Min(0f)] public float aoeInitialCooldown = 2f;

    [Tooltip("AOE bittikten sonra MiniBoss bu sure boyunca chase yapar.")]
    [Min(0f)] public float aoeCooldown = 4.5f;

    [Tooltip("Cooldown hazir oldugunda player bu mesafeye girdiyse charge baslar. 0 ise aoeRadius kullanilir.")]
    [Min(0f)] public float aoeTriggerDistance = 5.5f;

    [Tooltip("Patlama aninda player bu radius icindeyse ve arada obstacle yoksa hasar alir.")]
    [Min(0f)] public float aoeRadius = 4.5f;

    [Tooltip("MiniBoss bu sure boyunca sabit kalir, shake artar ve sonra AOE patlar.")]
    [Min(0f)] public float aoeChargeDuration = 0.9f;

    [Min(0f)] public float aoeMaxShakeAmount = 0.12f;

    [Tooltip("0 birakilirsa obstacleLayer cover icin kullanilir.")]
    public LayerMask aoeCoverLayers;

    [Header("MiniBoss AOE Danger Preview / SFX")]
    [Tooltip("Charge boyunca 0 alphadan maksimuma cikan local red danger alani. Obstacle arkasi safe kalir.")]
    public Color dangerPreviewColor =
        new Color(1f, 0.025f, 0.025f, 0.46f);

    [Tooltip("AOE strike gerceklestigi anda MiniBoss merkezinden AOE radius sinirina yayilan parlak shockwave'in suresi.")]
    [Min(0.05f)] public float dangerStrikeWaveDuration = 0.30f;

    [Tooltip("Strike shockwave halkasinin radial genisligi.")]
    [Range(0.03f, 0.30f)] public float dangerStrikeWaveWidth = 0.11f;

    [Tooltip("Strike shockwave'in normal red danger alanina gore ekstra parlaklik/alpha gucu.")]
    [Range(0f, 3f)] public float dangerStrikeWaveBoost = 1.45f;

    [Tooltip("Shockwave bittikten sonra local red danger alaninin smooth sekilde kaybolma suresi. Bu bitene kadar MiniBoss sabit kalir.")]
    [Min(0.05f)] public float dangerPreviewFadeOutDuration = 0.45f;

    [Tooltip("MiniBoss collider kenari ile red danger alaninin baslangici arasindaki bosluk.")]
    [Min(0f)] public float dangerPreviewInnerPadding = 0.10f;

    [Tooltip("MiniBoss cevresindeki kirmizinin alpha carpani. Radius sinirina dogru alpha artar.")]
    [Range(0.05f, 1f)] public float dangerPreviewInnerAlphaMultiplier = 0.22f;

    [Tooltip("Charge sirasinda disariya ilerleyen dalga on cephesinin yumusakligi.")]
    [Range(0.03f, 0.30f)] public float dangerPreviewWaveFrontWidth = 0.12f;

    [Tooltip("Charge dalgasinin on cephesindeki ekstra parlaklik/alpha.")]
    [Range(0f, 2f)] public float dangerPreviewWaveFrontBoost = 0.75f;

    [Tooltip("MiniBoss cevresindeki red rengin koyuluk carpani.")]
    [Range(0.1f, 1f)] public float dangerPreviewInnerBrightness = 0.55f;

    [Tooltip("Cover hassasiyeti. Helper minimum 720 angular sample kullanir.")]
    [Range(360, 960)] public int dangerPreviewRayCount = 360;

    [Tooltip("Boss ile ayni BossDangerPreview shader'i. Bos birakilirsa Shader.Find ile bulunur.")]
    public Shader dangerPreviewShader;

    [Tooltip("Hareketli obstacle safe alanlarinin saniyede kac kez guncellenecegi.")]
    [Range(5f, 30f)] public float dangerPreviewVisibilityRefreshRate = 15f;

    [Tooltip("Obstacle safe-area kenarlarinin yumusakligi.")]
    [Range(0.01f, 0.35f)] public float dangerPreviewCoverFeather = 0.08f;

    [Tooltip("Danger preview'nin SpriteRenderer'larin ustunde gorunmesi icin sorting order.")]
    public int dangerPreviewSortingOrder = 20000;

    public AudioClip aoeSfx;

    [Header("Stuck Fix")]
    public float stuckCheckTime = 0.5f;
    public float stuckDistance = 0.08f;
    public float unstuckDuration = 0.5f;
    public float unstuckSideForce = 1.5f;

    [Header("Clone Targeting")]
    [Tooltip("Bu Mini Boss, Void Clone tarafindan hedef olarak secilebilir mi?")]
    public bool canTargetClone;

    private float movementOffset;
    private float sideMoveAmount;
    private float sideMoveSpeed;

    private Rigidbody2D rb;
    private Collider2D col;
    private Rigidbody2D playerBody;
    private PlayerMovement playerMovement;
    private PlayerArmor playerArmor;

    private Vector3 originalScale;
    private Vector2 lastPosition;
    private Vector2 smoothedDirection;

    private float stuckTimer;
    private float unstuckTimer;
    private int unstuckDirection = 1;

    private bool stopped;

    private ContactFilter2D navigationFilter;

    private readonly RaycastHit2D[] castHits =
        new RaycastHit2D[8];

    private readonly RaycastHit2D[] avoidanceHits =
        new RaycastHit2D[12];

    private readonly Collider2D[] escapeHits =
        new Collider2D[16];

    private float additionalInitialAoeDelay;
    private float aoeCooldownTimer;
    private bool isChargingAoe;
    private bool isAoeFadingOut;
    private float aoeChargeProgress;

    [SerializeField, HideInInspector]
    private int aoeBalanceVersion;
    private Vector2 aoeChargeCenter;
    private Coroutine aoeRoutine;
    private GameObject dangerPreviewObject;

    public bool IsChargingAoe => isChargingAoe;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        originalScale = transform.localScale;

        if (originalScale == Vector3.zero)
            originalScale = Vector3.one;

        EnemyObstacleSteering2D.ConfigureAIMovementBody(rb, true);

        ApplyAoeBalanceMigration();
        RefreshNavigationFilter();
    }

    private void Start()
    {
        FindPlayerIfNeeded();
        RefreshNavigationFilter();

        movementOffset = Random.Range(0f, 100f);

        float sideMagnitude = Random.Range(
            minSideMoveAmount,
            maxSideMoveAmount
        );

        sideMoveAmount = Random.value < 0.5f
            ? -sideMagnitude
            : sideMagnitude;

        sideMoveSpeed = Random.Range(
            minSideMoveSpeed,
            maxSideMoveSpeed
        );

        unstuckDirection =
            Random.Range(0, 2) == 0 ? -1 : 1;

        lastPosition = rb.position;

        aoeCooldownTimer = Mathf.Max(
            0f,
            aoeInitialCooldown + additionalInitialAoeDelay
        );
    }

    private void Update()
    {
        if (stopped)
            return;

        FindPlayerIfNeeded();

        if (playerMovement != null &&
            playerMovement.IsGameOver)
        {
            // Strike playeri oldururse bile shockwave + fade tamamlanir.
            if (isAoeFadingOut)
                return;

            StopMiniBoss();
            return;
        }

        if (!aoeEnabled || isChargingAoe || player == null)
            return;

        if (aoeCooldownTimer > 0f)
        {
            aoeCooldownTimer -= Time.deltaTime;
            return;
        }

        float triggerDistance = aoeTriggerDistance > 0f
            ? aoeTriggerDistance
            : aoeRadius;

        if (Vector2.Distance(
                transform.position,
                player.position) > triggerDistance)
        {
            return;
        }

        aoeRoutine = StartCoroutine(AoeStrikeRoutine());
    }

    private void FixedUpdate()
    {
        if (stopped)
            return;

        FindPlayerIfNeeded();

        if (rb == null || player == null)
            return;

        if (isAoeFadingOut)
        {
            RestoreAoeChargeCenter();
            return;
        }

        if (playerMovement != null &&
            playerMovement.IsGameOver)
        {
            StopMiniBoss();
            return;
        }

        if (isChargingAoe)
        {
            ApplyAoeChargeShake();
            return;
        }

        MoveMiniBoss();
    }

    private void RefreshNavigationFilter()
    {
        navigationFilter = new ContactFilter2D();
        navigationFilter.SetLayerMask(
            EnemyObstacleSteering2D.BuildNavigationMask(
                (LayerMask)(solidLayers.value | obstacleLayer.value)
            )
        );
        navigationFilter.useLayerMask = true;
        navigationFilter.useTriggers = false;
    }

    private void FindPlayerIfNeeded()
    {
        if (player != null)
        {
            if (playerMovement == null)
                playerMovement = player.GetComponent<PlayerMovement>();

            if (playerArmor == null)
                playerArmor = player.GetComponent<PlayerArmor>();

            if (playerBody == null)
                playerBody = player.GetComponent<Rigidbody2D>();

            return;
        }

        GameObject foundPlayer =
            GameObject.FindGameObjectWithTag("Player");

        if (foundPlayer == null)
            return;

        player = foundPlayer.transform;
        playerMovement = foundPlayer.GetComponent<PlayerMovement>();
        playerArmor = foundPlayer.GetComponent<PlayerArmor>();
        playerBody = foundPlayer.GetComponent<Rigidbody2D>();
    }

    public void ConfigurePursuitRole(NormalEnemyPursuitRole role)
    {
        pursuitRole = role == NormalEnemyPursuitRole.Flanker
            ? NormalEnemyPursuitRole.Interceptor
            : role;
    }

    public void AddInitialAoeDelay(float extraDelay)
    {
        additionalInitialAoeDelay += Mathf.Max(0f, extraDelay);
    }

    private Transform GetCurrentTarget()
    {
        if (canTargetClone &&
            VoidCloneAbility.ActiveCloneTarget != null)
        {
            return VoidCloneAbility.ActiveCloneTarget;
        }

        return player;
    }

    private void MoveMiniBoss()
    {
        Transform currentTarget = GetCurrentTarget();

        if (currentTarget == null)
        {
            ResetStuckCheck();
            return;
        }

        Vector2 targetPosition =
            GetTacticalTargetPosition(currentTarget);

        Vector2 toTarget = targetPosition - rb.position;

        if (toTarget.sqrMagnitude <= 0.001f)
        {
            ResetStuckCheck();
            return;
        }

        Vector2 targetDirection = toTarget.normalized;

        smoothedDirection = Vector2.Lerp(
            smoothedDirection == Vector2.zero
                ? targetDirection
                : smoothedDirection,
            targetDirection,
            directionSmoothness * Time.fixedDeltaTime
        ).normalized;

        FlipSprite(smoothedDirection);

        Vector2 waveSideDirection =
            GetPerpendicularDirection(smoothedDirection, 1);

        float wave = Mathf.Sin(
            (Time.time + movementOffset) * sideMoveSpeed
        ) * sideMoveAmount;

        Vector2 movementDirection =
            (smoothedDirection +
             waveSideDirection * wave).normalized;

        if (unstuckTimer > 0f)
        {
            unstuckTimer -= Time.fixedDeltaTime;

            Vector2 escapeSideDirection =
                GetPerpendicularDirection(
                    movementDirection,
                    unstuckDirection
                );

            movementDirection =
                (movementDirection +
                 escapeSideDirection * unstuckSideForce).normalized;
        }

        bool moved = MoveWithCollision(movementDirection);
        HandleStuckCheck(moved);
    }

    private Vector2 GetTacticalTargetPosition(Transform currentTarget)
    {
        Vector2 targetPosition = currentTarget.position;

        // Clone is a decoy: when a MiniBoss is assigned to it, chase it
        // directly instead of predicting the real player's route.
        if (currentTarget != player ||
            pursuitRole == NormalEnemyPursuitRole.Pursuer)
        {
            return ClampTargetInsideArena(targetPosition);
        }

        Vector2 velocity = playerBody != null
            ? playerBody.linearVelocity
            : Vector2.zero;

        if (velocity.sqrMagnitude <= 0.04f && playerMovement != null)
        {
            Vector2 moveInput = playerMovement.CurrentMoveInput;

            if (moveInput.sqrMagnitude <= 0.04f)
                moveInput = playerMovement.LastMoveDirection;

            if (moveInput.sqrMagnitude > 0.04f)
            {
                velocity = moveInput.normalized *
                           Mathf.Max(1f, playerMovement.CurrentMoveSpeed);
            }
        }

        Vector2 travelDirection = velocity.sqrMagnitude > 0.04f
            ? velocity.normalized
            : ((Vector2)player.position - rb.position).normalized;

        float distance = Vector2.Distance(
            rb.position,
            player.position
        );

        float tacticalStrength = Mathf.InverseLerp(
            0.8f,
            tacticalOffsetFadeDistance,
            distance
        );

        Vector2 prediction = Vector2.ClampMagnitude(
            velocity * predictionTime,
            maxPredictionDistance
        );

        Vector2 interceptOffset =
            travelDirection *
            interceptorLeadDistance *
            tacticalStrength;

        return ClampTargetInsideArena(
            (Vector2)player.position +
            prediction +
            interceptOffset
        );
    }

    private static Vector2 ClampTargetInsideArena(Vector2 position)
    {
        CameraWorldBounds bounds = CameraWorldBounds.Instance;

        if (bounds == null)
            return position;

        const float margin = 0.25f;

        return new Vector2(
            Mathf.Clamp(
                position.x,
                bounds.MinX + margin,
                bounds.MaxX - margin
            ),
            Mathf.Clamp(
                position.y,
                bounds.MinY + margin,
                bounds.MaxY - margin
            )
        );
    }

    private IEnumerator AoeStrikeRoutine()
    {
        if (isChargingAoe || stopped)
            yield break;

        isChargingAoe = true;
        GameAudioMixerController.SetBossDanger(this, true);
        isAoeFadingOut = false;
        aoeChargeProgress = 0f;

        aoeChargeCenter = rb != null
            ? rb.position
            : (Vector2)transform.position;

        ResetStuckCheck();
        ZeroVelocity();

        float duration = Mathf.Max(0.05f, aoeChargeDuration);
        float timer = 0f;

        // Boss'taki sistemle ayni:
        // charge basladigi anda local radius preview olusur ama alpha 0'dir.
        ShowLocalDangerPreview();
        UpdateLocalDangerPreviewAlpha(0f);

        while (timer < duration)
        {
            if (stopped ||
                (playerMovement != null && playerMovement.IsGameOver))
            {
                CancelAoeCharge();
                yield break;
            }

            timer += Time.deltaTime;

            aoeChargeProgress = Mathf.Clamp01(
                timer / duration
            );

            float visualProgress = Mathf.SmoothStep(
                0f,
                1f,
                aoeChargeProgress
            );

            UpdateLocalDangerPreviewAlpha(
                visualProgress
            );

            yield return null;
        }

        aoeChargeProgress = 1f;
        UpdateLocalDangerPreviewAlpha(1f);

        RestoreAoeChargeCenter();

        // HASAR TAM BU ANDA uygulanir.
        ExecuteLocalAoeStrike();

        // Strike anini net gosteren local shockwave.
        // MiniBoss bu asama + fade tamamen bitene kadar sabit.
        isAoeFadingOut = true;

        float strikeWaveDuration =
            Mathf.Max(
                0.05f,
                dangerStrikeWaveDuration
            );

        float strikeWaveTimer = 0f;

        UpdateLocalDangerStrikeWave(0f);

        while (strikeWaveTimer < strikeWaveDuration)
        {
            if (stopped ||
                (playerMovement != null && playerMovement.IsGameOver))
            {
                CancelAoeCharge();
                yield break;
            }

            RestoreAoeChargeCenter();

            strikeWaveTimer += Time.deltaTime;

            float strikeWaveProgress =
                Mathf.Clamp01(
                    strikeWaveTimer /
                    strikeWaveDuration
                );

            UpdateLocalDangerStrikeWave(
                Mathf.SmoothStep(
                    0f,
                    1f,
                    strikeWaveProgress
                )
            );

            yield return null;
        }

        DisableLocalDangerStrikeWave();

        float fadeDuration =
            Mathf.Max(
                0.05f,
                dangerPreviewFadeOutDuration
            );

        float fadeTimer = 0f;

        while (fadeTimer < fadeDuration)
        {
            if (stopped ||
                (playerMovement != null && playerMovement.IsGameOver))
            {
                CancelAoeCharge();
                yield break;
            }

            RestoreAoeChargeCenter();

            fadeTimer += Time.deltaTime;

            float fadeProgress =
                Mathf.Clamp01(
                    fadeTimer / fadeDuration
                );

            float opacity =
                1f -
                Mathf.SmoothStep(
                    0f,
                    1f,
                    fadeProgress
                );

            UpdateLocalDangerPreviewOpacity(
                opacity
            );

            yield return null;
        }

        UpdateLocalDangerPreviewOpacity(0f);
        HideDangerPreview();

        isAoeFadingOut = false;
        isChargingAoe = false;
        GameAudioMixerController.SetBossDanger(this, false);
        aoeChargeProgress = 0f;

        aoeCooldownTimer =
            Mathf.Max(0f, aoeCooldown);

        aoeRoutine = null;

        lastPosition = rb != null
            ? rb.position
            : (Vector2)transform.position;
    }

    private void ApplyAoeChargeShake()
    {
        if (rb == null)
            return;

        float shake = Mathf.Lerp(
            Mathf.Max(0f, normalShakeAmount),
            Mathf.Max(normalShakeAmount, aoeMaxShakeAmount),
            Mathf.SmoothStep(0f, 1f, aoeChargeProgress)
        );

        rb.MovePosition(
            aoeChargeCenter + Random.insideUnitCircle * shake
        );
    }

    private void RestoreAoeChargeCenter()
    {
        if (rb != null)
        {
            rb.position = aoeChargeCenter;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        else
        {
            transform.position = aoeChargeCenter;
        }
    }

    private void ExecuteLocalAoeStrike()
    {
        if (player == null || playerMovement == null)
            return;

        EnemyAreaStrikeUtility.PlaySound(aoeSfx, transform.position);

        CameraShake.Instance?.Shake(
            0.18f,
            0.12f
        );

        VibrationManager.Instance?.VibrateMiniBossAoe();

        EnemyAreaStrikeUtility.ExecuteStrike(
            transform,
            player,
            playerMovement,
            playerArmor,
            GetAoeCoverLayers(),
            true,
            aoeRadius,
            "MINI BOSS"
        );
    }

    private void ShowLocalDangerPreview()
    {
        HideDangerPreview();

        Vector2 origin = rb != null
            ? aoeChargeCenter
            : (Vector2)transform.position;

        float miniBossVisualRadius = 0.25f;

        if (col != null)
        {
            Bounds bounds = col.bounds;

            miniBossVisualRadius = Mathf.Max(
                bounds.extents.x,
                bounds.extents.y
            );
        }

        // Shake olsa bile red danger MiniBoss sprite/collider ustune binmez.
        float innerRadius =
            miniBossVisualRadius +
            Mathf.Max(
                0f,
                dangerPreviewInnerPadding
            ) +
            Mathf.Max(
                0f,
                aoeMaxShakeAmount
            );

        dangerPreviewObject =
            EnemyDangerPreviewMesh.CreatePreview(
                origin,
                true,
                aoeRadius,
                GetAoeCoverLayers(),
                dangerPreviewColor,
                dangerPreviewRayCount,
                dangerPreviewSortingOrder,
                innerRadius,
                14,
                0,
                dangerPreviewInnerAlphaMultiplier,
                dangerPreviewWaveFrontWidth,
                dangerPreviewWaveFrontBoost,
                dangerPreviewInnerBrightness,
                dangerPreviewShader,
                dangerPreviewVisibilityRefreshRate,
                dangerPreviewCoverFeather
            );
    }

    private void UpdateLocalDangerPreviewAlpha(
        float normalizedAlpha)
    {
        EnemyDangerPreviewMesh.SetPreviewAlpha(
            dangerPreviewObject,
            dangerPreviewColor,
            normalizedAlpha
        );
    }

    private void UpdateLocalDangerPreviewOpacity(
        float normalizedOpacity)
    {
        EnemyDangerPreviewMesh.SetPreviewOpacity(
            dangerPreviewObject,
            normalizedOpacity
        );
    }

    private void UpdateLocalDangerStrikeWave(
        float normalizedProgress)
    {
        EnemyDangerPreviewMesh.SetStrikeWave(
            dangerPreviewObject,
            normalizedProgress,
            dangerStrikeWaveWidth,
            dangerStrikeWaveBoost
        );
    }

    private void DisableLocalDangerStrikeWave()
    {
        EnemyDangerPreviewMesh.SetStrikeWave(
            dangerPreviewObject,
            -1f,
            dangerStrikeWaveWidth,
            dangerStrikeWaveBoost
        );
    }

    private void HideDangerPreview()
    {
        EnemyDangerPreviewMesh.DestroyPreview(
            ref dangerPreviewObject
        );
    }

    private LayerMask GetAoeCoverLayers()
    {
        return aoeCoverLayers.value != 0
            ? aoeCoverLayers
            : obstacleLayer;
    }

    private void CancelAoeCharge()
    {
        HideDangerPreview();
        RestoreAoeChargeCenter();
        isAoeFadingOut = false;
        isChargingAoe = false;
        GameAudioMixerController.SetBossDanger(this, false);
        aoeChargeProgress = 0f;
        aoeRoutine = null;
    }

    private bool MoveWithCollision(Vector2 direction)
    {
        float movementDistance = speed * Time.fixedDeltaTime;

        if (EnemyObstacleSteering2D.TryGetOverlapRecovery(
                col,
                navigationFilter,
                out Vector2 overlapDirection,
                out float penetrationDepth))
        {
            float recoveryDistance =
                EnemyObstacleSteering2D.GetOverlapRecoveryDistance(
                    penetrationDepth,
                    movementDistance,
                    castSkin
                );

            rb.MovePosition(
                rb.position +
                overlapDirection * recoveryDistance
            );

            return true;
        }

        if (direction.sqrMagnitude <= 0.001f)
            return false;

        Vector2 steeredDirection =
            EnemyObstacleSteering2D.GetSteeredDirection(
                col,
                direction,
                direction,
                navigationFilter,
                avoidanceHits,
                obstacleProbeDistance,
                movementDistance,
                castSkin,
                slideDirectionAttempts,
                obstacleOutwardBias,
                ref unstuckDirection
            );

        if (steeredDirection.sqrMagnitude <= 0.001f)
            return false;

        Vector2 intendedMovement =
            steeredDirection * movementDistance;

        Vector2 finalMovement =
            intendedMovement +
            Random.insideUnitCircle * Mathf.Max(0f, normalShakeAmount);

        if (EnemyObstacleSteering2D.MoveDisplacementWithPhysicsSlide(
                rb,
                col,
                finalMovement,
                Time.fixedDeltaTime,
                navigationFilter,
                6))
        {
            return true;
        }

        if (TrySlideAroundObstacle(
                steeredDirection,
                intendedMovement.magnitude))
        {
            return true;
        }

        unstuckDirection *= -1;
        return false;
    }

    private bool TrySlideAroundObstacle(
        Vector2 forwardDirection,
        float movementDistance)
    {
        if (movementDistance <= 0f)
            return false;

        Vector2 leftDirection =
            GetPerpendicularDirection(forwardDirection, 1);

        Vector2 rightDirection =
            GetPerpendicularDirection(forwardDirection, -1);

        for (int attempt = 0;
             attempt < slideDirectionAttempts;
             attempt++)
        {
            float blend =
                (attempt + 1f) / slideDirectionAttempts;

            Vector2 preferredSide =
                unstuckDirection > 0
                    ? leftDirection
                    : rightDirection;

            Vector2 oppositeSide =
                unstuckDirection > 0
                    ? rightDirection
                    : leftDirection;

            Vector2 firstDirection =
                Vector2.Lerp(
                    forwardDirection,
                    preferredSide,
                    blend
                ).normalized;

            Vector2 firstMovement =
                firstDirection * movementDistance;

            if (CanMove(firstMovement))
            {
                rb.MovePosition(rb.position + firstMovement);
                return true;
            }

            Vector2 secondDirection =
                Vector2.Lerp(
                    forwardDirection,
                    oppositeSide,
                    blend
                ).normalized;

            Vector2 secondMovement =
                secondDirection * movementDistance;

            if (CanMove(secondMovement))
            {
                rb.MovePosition(rb.position + secondMovement);
                unstuckDirection *= -1;
                return true;
            }
        }

        return false;
    }

    private bool CanMove(Vector2 movement)
    {
        if (col == null || movement.sqrMagnitude <= 0.001f)
            return true;

        int hitCount = col.Cast(
            movement.normalized,
            navigationFilter,
            castHits,
            movement.magnitude + Mathf.Max(castSkin, 0f)
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = castHits[i].collider;

            if (hitCollider == null || hitCollider == col)
                continue;

            return false;
        }

        return true;
    }

    private void HandleStuckCheck(bool attemptedMove)
    {
        stuckTimer += Time.fixedDeltaTime;

        float effectiveStuckCheckTime = Mathf.Min(
            Mathf.Max(0.05f, stuckCheckTime),
            0.25f
        );

        if (stuckTimer < effectiveStuckCheckTime)
            return;

        float movedDistanceSqr =
            (rb.position - lastPosition).sqrMagnitude;

        float requiredDistanceSqr =
            stuckDistance * stuckDistance;

        if (movedDistanceSqr < requiredDistanceSqr)
        {
            Vector2 escapeDirection = GetEscapeDirection();

            if (escapeDirection.sqrMagnitude <= 0.001f)
            {
                Transform currentTarget = GetCurrentTarget();

                Vector2 targetDirection =
                    currentTarget != null
                        ? ((Vector2)currentTarget.position - rb.position).normalized
                        : Vector2.right;

                Vector2 sideDirection =
                    GetPerpendicularDirection(
                        targetDirection,
                        unstuckDirection
                    );

                escapeDirection =
                    (sideDirection +
                     Random.insideUnitCircle * 0.35f).normalized;
            }

            Vector2 escapeMovement =
                escapeDirection *
                speed *
                escapeSpeedMultiplier *
                Time.fixedDeltaTime;

            if (CanMove(escapeMovement))
                rb.MovePosition(rb.position + escapeMovement);
            else
                unstuckDirection *= -1;

            unstuckTimer = unstuckDuration;
        }

        lastPosition = rb.position;
        stuckTimer = 0f;
    }

    private void ResetStuckCheck()
    {
        stuckTimer = 0f;

        if (rb != null)
            lastPosition = rb.position;
    }

    private Vector2 GetEscapeDirection()
    {
        ContactFilter2D obstacleFilter =
            new ContactFilter2D();

        obstacleFilter.SetLayerMask(
            obstacleLayer | solidLayers
        );

        obstacleFilter.useLayerMask = true;
        obstacleFilter.useTriggers = true;

        int hitCount = Physics2D.OverlapCircle(
            rb.position,
            escapeCheckRadius,
            obstacleFilter,
            escapeHits
        );

        if (hitCount <= 0)
            return Vector2.zero;

        Vector2 escapeDirection = Vector2.zero;
        int validHitCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = escapeHits[i];

            if (hit == null || hit == col)
                continue;

            Vector2 closestPoint = hit.ClosestPoint(rb.position);
            Vector2 awayFromObstacle = rb.position - closestPoint;

            if (awayFromObstacle.sqrMagnitude <= 0.001f)
            {
                awayFromObstacle =
                    rb.position - (Vector2)hit.bounds.center;
            }

            if (awayFromObstacle.sqrMagnitude <= 0.001f)
                continue;

            float distance = awayFromObstacle.magnitude;
            float weight = 1f / Mathf.Max(distance, 0.05f);

            escapeDirection +=
                awayFromObstacle.normalized * weight;

            validHitCount++;
        }

        if (validHitCount <= 0 ||
            escapeDirection.sqrMagnitude <= 0.001f)
        {
            return Vector2.zero;
        }

        return escapeDirection.normalized;
    }

    private Vector2 GetPerpendicularDirection(
        Vector2 direction,
        int side)
    {
        return new Vector2(-direction.y, direction.x) * side;
    }

    private void FlipSprite(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) <= 0.01f)
            return;

        Vector3 scale = transform.localScale;
        float absoluteX = Mathf.Abs(originalScale.x);

        scale.x = direction.x > 0f
            ? absoluteX
            : -absoluteX;

        transform.localScale = scale;
    }

    private void ZeroVelocity()
    {
        if (rb == null)
            return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    public void StopForGameEnd()
    {
        StopMiniBoss();
    }

    private void StopMiniBoss()
    {
        if (stopped)
            return;

        stopped = true;

        if (aoeRoutine != null)
        {
            StopCoroutine(aoeRoutine);
            aoeRoutine = null;
        }

        HideDangerPreview();

        if (isChargingAoe || isAoeFadingOut)
            RestoreAoeChargeCenter();

        isAoeFadingOut = false;
        isChargingAoe = false;
        GameAudioMixerController.SetBossDanger(this, false);
        ZeroVelocity();
        enabled = false;
    }

    private void OnDisable()
    {
        if (aoeRoutine != null)
        {
            StopCoroutine(aoeRoutine);
            aoeRoutine = null;
        }

        HideDangerPreview();
        isAoeFadingOut = false;
        isChargingAoe = false;
        GameAudioMixerController.SetBossDanger(this, false);
    }

    private void ApplyAoeBalanceMigration()
    {
        // Existing prefab'daki eski 3.25 / 4.5 degerlerini sadece BIR KEZ
        // yeni local AOE menziline tasir. Sonrasinda Inspector'dan serbestce
        // daha kucuk veya buyuk deger verebilirsin.
        if (aoeBalanceVersion >= 1)
            return;

        if (aoeRadius <= 3.30f)
            aoeRadius = 4.5f;

        if (aoeTriggerDistance <= 4.55f)
            aoeTriggerDistance = 5.5f;

        aoeBalanceVersion = 1;
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        normalShakeAmount = Mathf.Max(0f, normalShakeAmount);
        aoeInitialCooldown = Mathf.Max(0f, aoeInitialCooldown);
        aoeCooldown = Mathf.Max(0f, aoeCooldown);
        aoeTriggerDistance = Mathf.Max(0f, aoeTriggerDistance);
        aoeRadius = Mathf.Max(0f, aoeRadius);
        aoeChargeDuration = Mathf.Max(0f, aoeChargeDuration);
        aoeMaxShakeAmount = Mathf.Max(normalShakeAmount, aoeMaxShakeAmount);
        dangerStrikeWaveDuration =
            Mathf.Max(0.05f, dangerStrikeWaveDuration);
        dangerStrikeWaveWidth =
            Mathf.Clamp(dangerStrikeWaveWidth, 0.03f, 0.30f);
        dangerStrikeWaveBoost =
            Mathf.Clamp(dangerStrikeWaveBoost, 0f, 3f);
        dangerPreviewFadeOutDuration =
            Mathf.Max(0.05f, dangerPreviewFadeOutDuration);
        dangerPreviewInnerPadding =
            Mathf.Max(0f, dangerPreviewInnerPadding);
        dangerPreviewInnerAlphaMultiplier =
            Mathf.Clamp01(dangerPreviewInnerAlphaMultiplier);
        dangerPreviewWaveFrontWidth =
            Mathf.Clamp(dangerPreviewWaveFrontWidth, 0.03f, 0.30f);
        dangerPreviewWaveFrontBoost =
            Mathf.Clamp(dangerPreviewWaveFrontBoost, 0f, 2f);
        dangerPreviewInnerBrightness =
            Mathf.Clamp(dangerPreviewInnerBrightness, 0.1f, 1f);
        dangerPreviewRayCount =
            Mathf.Clamp(dangerPreviewRayCount, 360, 960);
        dangerPreviewVisibilityRefreshRate =
            Mathf.Clamp(dangerPreviewVisibilityRefreshRate, 5f, 30f);
        dangerPreviewCoverFeather =
            Mathf.Clamp(dangerPreviewCoverFeather, 0.01f, 0.35f);

        ApplyAoeBalanceMigration();

        slideDirectionAttempts = Mathf.Clamp(slideDirectionAttempts, 1, 8);
    }

    private void OnDrawGizmosSelected()
    {
        if (aoeRadius <= 0f)
            return;

        Gizmos.DrawWireSphere(transform.position, aoeRadius);
    }
}
