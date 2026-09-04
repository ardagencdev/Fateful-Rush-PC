using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ProjectileEnemyFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float stoppingDistance = 7f;
    public float retreatDistance = 4f;

    [Header("Combat Strafe")]
    public bool strafeEnabled = true;
    public float strafeSpeedMultiplier = 0.65f;
    public float strafeDirectionChangeMinTime = 1.5f;
    public float strafeDirectionChangeMaxTime = 3f;
    public float strafeDistanceTolerance = 0.6f;

    [Header("Predictive Aim")]
    public bool predictiveAimEnabled = true;
    public float predictionTime = 0.3f;
    public float maxPredictionDistance = 2f;
    public float predictionDistanceThreshold = 2.5f;

    [Header("Enemy Separation")]
    public bool separationEnabled = true;
    public LayerMask enemyLayer;
    public float separationRadius = 0.9f;
    public float separationStrength = 0.5f;

    [Header("Movement Wave")]
    public float minSideMoveAmount = 0.05f;
    public float maxSideMoveAmount = 0.18f;
    public float minSideMoveSpeed = 1.5f;
    public float maxSideMoveSpeed = 3f;

    [Header("Obstacle Avoidance")]
    public LayerMask obstacleLayer;
    public float obstacleProbeDistance = 0.8f;
    [Range(2, 8)] public int obstacleAvoidanceAttempts = 5;
    [Range(0f, 1f)] public float obstacleOutwardBias = 0.3f;

    [Header("Advanced Unstuck")]
    public float escapeCheckRadius = 1.2f;
    public float escapeSpeedMultiplier = 2.2f;

    [Header("Attack")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float fireRate = 1.5f;
    public float projectileSpeed = 6f;

    [Header("Shot Recoil")]
    [Min(0f)] public float shotRecoilDistance = 0.035f;
    [Min(0f)] public float shotRecoilPause = 0.02f;
    [Min(1f)] public float finalShotRecoilMultiplier = 1.12f;

    [Header("Arena Bounds")]
    [SerializeField, Min(0f)]
    private float arenaEdgePadding = 0.03f;

    [Header("Attack Timing Desync")]
    [SerializeField, Range(0f, 0.30f)]
    private float fireIntervalJitter = 0.10f;

    [SerializeField, Range(0f, 0.20f)]
    private float reloadDurationJitter = 0.08f;

    [SerializeField, Min(0f)]
    private float initialFireDelayMin = 0.15f;

    [SerializeField, Min(0f)]
    private float initialFireDelayMax = 0.85f;

    [SerializeField, Min(0f)]
    private float postReloadFireDelayMin = 0.12f;

    [SerializeField, Min(0f)]
    private float postReloadFireDelayMax = 0.38f;

    [Header("Final Burst Shot")]
    [Tooltip("Danger 1-2 use 3 shots per burst, so their final shot fires this many projectiles.")]
    [Min(1)] public int lowDangerFinalShotProjectileCount = 2;
    [Tooltip("Danger 3-5 use 4+ shots per burst, so their final shot fires this many projectiles.")]
    [Min(1)] public int highDangerFinalShotProjectileCount = 3;
    [Tooltip("Burst sizes at or above this value use the high-danger final-shot projectile count.")]
    [Min(1)] public int highDangerBurstThreshold = 4;
    [Range(0f, 20f)] public float finalShotAngleOffset = 6f;

    [Header("Burst / Reload")]
    [Min(1)] public int shotsPerBurst = 3;
    [Min(0.05f)] public float reloadDuration = 3f;
    [Min(0f)] public float reloadRetreatDistance = 9f;
    [Min(0.1f)] public float reloadMoveSpeedMultiplier = 1.4f;

    [Header("Reload Feedback")]
    public AudioClip reloadSound;
    [Range(0.5f, 1f)] public float reloadVolumeMinMultiplier = 0.82f;
    [Range(0.5f, 1f)] public float reloadVolumeMaxMultiplier = 1f;
    [Range(0.9f, 1.1f)] public float reloadPitchMin = 0.97f;
    [Range(0.9f, 1.1f)] public float reloadPitchMax = 1.03f;
    [Min(0.01f)] public float reloadSfxFadeOutDuration = 0.22f;
    [Range(0.05f, 1f)] public float reloadBlinkMinAlpha = 0.2f;
    [Min(0.1f)] public float reloadBlinkFrequency = 4.5f;

    [Header("Projectile Pool")]
    public int poolSize = 12;

    [Header("Stuck Fix")]
    public float stuckCheckTime = 0.5f;
    public float stuckDistance = 0.05f;
    public float unstuckDuration = 0.4f;
    public float unstuckSideForce = 1.8f;

    [Header("Sound")]
    public AudioClip fireSound;

    [Header("SFX Variation")]
    [SerializeField, Range(0f, 0.08f)]
    private float firePitchJitter = 0.015f;

    [SerializeField, Range(0f, 0.08f)]
    private float fireVolumeJitter = 0.01f;

    [Header("Spawn Effect")]
    public float spawnEffectDuration = 0.15f;

    [Header("Near Miss")]
    [SerializeField]
    private bool enableNearMiss = true;

    [Tooltip("Surface-to-surface distance that arms a projectile-enemy body near miss.")]
    [SerializeField, Min(0.05f)]
    private float nearMissDistance = 0.80f;

    [Tooltip("Enemy must separate this much after the closest point before the near miss fires.")]
    [SerializeField, Min(0f)]
    private float nearMissReleaseDistance = 0.10f;

    private readonly List<EnemyProjectile> ownedProjectiles =
        new List<EnemyProjectile>();

    private readonly RaycastHit2D[] avoidanceHits =
        new RaycastHit2D[12];

    private readonly Collider2D[] escapeHits =
        new Collider2D[16];

    private readonly Collider2D[] separationHits =
        new Collider2D[16];

    private Rigidbody2D rb;
    private Collider2D col;
    private Rigidbody2D targetRigidbody;
    private AudioSource audioSource;
    private AudioSource reloadAudioSource;
    private PlayerMovement playerMovement;
    private SpriteRenderer[] reloadRenderers;
    private float[] reloadRendererBaseAlphas;

    private Vector3 spawnTargetScale;
    private Vector2 lastPosition;

    private float fireCooldown;
    private float reloadTimer;
    private float reloadVisualTime;
    private float activeReloadDuration;
    private float activeReloadSfxVolume;
    private float shotRecoilPauseTimer;
    private float perEnemyFireCadenceMultiplier = 1f;
    private float perEnemyReloadCadenceMultiplier = 1f;
    private Vector2 pendingShotRecoil;
    private int shotsFiredInBurst;

    private float movementOffset;
    private float sideMoveAmount;
    private float sideMoveSpeed;

    private float stuckTimer;
    private float unstuckTimer;

    private float strafeDirectionTimer;
    private int strafeDirection = 1;
    private int unstuckDirection = 1;
    private int obstacleAvoidanceSide = 1;

    private bool isSpawning;
    private bool isReloading;
    private bool stopped;
    private bool attemptedMovementThisFrame;

    public bool IsReloading => isReloading;

    private Transform cachedCurrentTarget;
    private ContactFilter2D navigationFilter;

    private Collider2D nearMissPlayerCollider;
    private bool nearMissArmed;
    private bool nearMissTriggered;
    private bool nearMissTouchedPlayer;
    private float nearMissClosestDistance = float.PositiveInfinity;
    private Vector3 nearMissClosestPoint;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        EnemyObstacleSteering2D.ConfigureAIMovementBody(rb, true);

        navigationFilter = new ContactFilter2D();
        navigationFilter.SetLayerMask(
            EnemyObstacleSteering2D.BuildNavigationMask(obstacleLayer)
        );
        navigationFilter.useLayerMask = true;
        navigationFilter.useTriggers = false;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        SoundManager.ConfigureAsWorld3D(audioSource);
        // User SFX volume is applied once through AudioSource.volume.
        // Keeping the initial source gain at 1 prevents accidental double scaling.
        audioSource.volume = 1f;

        reloadAudioSource = gameObject.AddComponent<AudioSource>();
        reloadAudioSource.playOnAwake = false;
        reloadAudioSource.loop = false;
        SoundManager.ConfigureAsWorld3D(reloadAudioSource);
        reloadAudioSource.volume = 1f;

        CacheReloadRenderers();
    }

    private void Start()
    {
        spawnTargetScale = transform.localScale;

        if (spawnTargetScale == Vector3.zero)
            spawnTargetScale = Vector3.one;

        transform.localScale = Vector3.zero;

        if (shotsPerBurst <= 0)
            shotsPerBurst = 3;

        if (reloadDuration <= 0f)
            reloadDuration = 3f;

        if (reloadRetreatDistance <= 0f)
            reloadRetreatDistance = Mathf.Max(stoppingDistance, 9f);
        else
            reloadRetreatDistance = Mathf.Max(stoppingDistance, reloadRetreatDistance);

        if (reloadMoveSpeedMultiplier <= 0f)
            reloadMoveSpeedMultiplier = 1.4f;

        if (reloadBlinkMinAlpha <= 0f)
            reloadBlinkMinAlpha = 0.2f;

        reloadBlinkMinAlpha = Mathf.Clamp(reloadBlinkMinAlpha, 0.05f, 1f);

        if (reloadBlinkFrequency <= 0f)
            reloadBlinkFrequency = 4.5f;

        // Keep recoil intentionally subtle even if an older prefab serialized
        // the previous, much stronger values.
        shotRecoilDistance = Mathf.Clamp(shotRecoilDistance, 0f, 0.05f);
        shotRecoilPause = Mathf.Clamp(shotRecoilPause, 0f, 0.03f);
        finalShotRecoilMultiplier = Mathf.Clamp(finalShotRecoilMultiplier, 1f, 1.18f);
        lowDangerFinalShotProjectileCount = Mathf.Max(1, lowDangerFinalShotProjectileCount);
        highDangerFinalShotProjectileCount = Mathf.Max(1, highDangerFinalShotProjectileCount);
        highDangerBurstThreshold = Mathf.Max(1, highDangerBurstThreshold);
        finalShotAngleOffset = Mathf.Clamp(finalShotAngleOffset, 0f, 20f);
        arenaEdgePadding = Mathf.Max(0f, arenaEdgePadding);

        fireIntervalJitter = Mathf.Clamp(fireIntervalJitter, 0f, 0.30f);
        reloadDurationJitter = Mathf.Clamp(reloadDurationJitter, 0f, 0.20f);
        initialFireDelayMin = Mathf.Max(0f, initialFireDelayMin);
        initialFireDelayMax = Mathf.Max(initialFireDelayMin, initialFireDelayMax);
        postReloadFireDelayMin = Mathf.Max(0f, postReloadFireDelayMin);
        postReloadFireDelayMax = Mathf.Max(postReloadFireDelayMin, postReloadFireDelayMax);

        perEnemyFireCadenceMultiplier = Random.Range(0.94f, 1.06f);
        perEnemyReloadCadenceMultiplier = Random.Range(0.95f, 1.05f);

        reloadVolumeMinMultiplier = Mathf.Clamp(reloadVolumeMinMultiplier, 0.5f, 1f);
        reloadVolumeMaxMultiplier = Mathf.Clamp(reloadVolumeMaxMultiplier, reloadVolumeMinMultiplier, 1f);
        reloadPitchMin = Mathf.Clamp(reloadPitchMin, 0.9f, 1.1f);
        reloadPitchMax = Mathf.Clamp(reloadPitchMax, reloadPitchMin, 1.1f);
        reloadSfxFadeOutDuration = Mathf.Max(0.01f, reloadSfxFadeOutDuration);

        fireCooldown =
            Random.Range(initialFireDelayMin, initialFireDelayMax) +
            GetNextFireInterval() * Random.Range(0.10f, 0.55f);

        movementOffset = Random.Range(0f, 100f);

        sideMoveAmount = Random.Range(
            minSideMoveAmount,
            maxSideMoveAmount
        );

        if (Random.value < 0.5f)
            sideMoveAmount *= -1f;

        sideMoveSpeed = Random.Range(
            minSideMoveSpeed,
            maxSideMoveSpeed
        );

        strafeDirection =
            Random.Range(0, 2) == 0 ? -1 : 1;

        unstuckDirection =
            Random.Range(0, 2) == 0 ? -1 : 1;

        obstacleAvoidanceSide = unstuckDirection;

        ResetStrafeTimer();

        lastPosition = rb.position;

        FindPlayerIfNeeded();
        RuntimeObjectPool.Prewarm(
            projectilePrefab,
            Mathf.Max(1, poolSize)
        );

        StartCoroutine(SpawnEffect());
    }

    private void FixedUpdate()
    {
        FindPlayerIfNeeded();

        if (isSpawning || stopped)
            return;

        Transform currentTarget = GetCurrentTarget();

        if (currentTarget == null)
        {
            StopMovementOnly();
            return;
        }

        UpdateCachedTarget(currentTarget);

        if (playerMovement != null &&
            playerMovement.IsGameOver)
        {
            StopEnemy();
            return;
        }

        attemptedMovementThisFrame = false;
        EnforceArenaBounds();

        TrackNearMiss();

        if (ApplyPendingShotRecoil())
        {
            EnforceArenaBounds();
            FlipSprite(currentTarget);
            return;
        }

        if (shotRecoilPauseTimer > 0f)
        {
            shotRecoilPauseTimer -= Time.fixedDeltaTime;
            ResetStuckCheck();
            FlipSprite(currentTarget);
            return;
        }

        HandleStrafeDirectionTimer();
        HandleMovement(currentTarget);
        HandleStuckCheck(currentTarget);
        EnforceArenaBounds();
        FlipSprite(currentTarget);
    }

    private void Update()
    {
        FindPlayerIfNeeded();

        if (isSpawning || stopped)
            return;

        UpdateReloadState();

        Transform currentTarget = GetCurrentTarget();

        if (currentTarget == null)
            return;

        UpdateCachedTarget(currentTarget);

        if (playerMovement != null &&
            playerMovement.IsGameOver)
        {
            return;
        }

        HandleAttack(currentTarget);
    }

    private Transform GetCurrentTarget()
    {
        if (VoidCloneAbility.ActiveCloneTarget != null)
            return VoidCloneAbility.ActiveCloneTarget;

        return player;
    }

    private void UpdateCachedTarget(Transform currentTarget)
    {
        if (cachedCurrentTarget == currentTarget)
            return;

        cachedCurrentTarget = currentTarget;

        targetRigidbody = currentTarget != null
            ? currentTarget.GetComponent<Rigidbody2D>()
            : null;
    }

    private void FindPlayerIfNeeded()
    {
        if (player != null)
        {
            if (playerMovement == null)
                playerMovement =
                    player.GetComponent<PlayerMovement>();

            return;
        }

        GameObject foundPlayer =
            GameObject.FindGameObjectWithTag("Player");

        if (foundPlayer == null)
            return;

        player = foundPlayer.transform;

        playerMovement =
            foundPlayer.GetComponent<PlayerMovement>();
    }

    private void ResetNearMissTracking()
    {
        nearMissPlayerCollider = null;
        nearMissArmed = false;
        nearMissTriggered = false;
        nearMissTouchedPlayer = false;
        nearMissClosestDistance = float.PositiveInfinity;
        nearMissClosestPoint = transform.position;
    }

    private void CacheNearMissPlayerCollider()
    {
        if (playerMovement == null)
            return;

        nearMissPlayerCollider =
            playerMovement.GetComponent<Collider2D>();

        if (nearMissPlayerCollider == null)
        {
            nearMissPlayerCollider =
                playerMovement.GetComponentInChildren<Collider2D>();
        }
    }

    private void TrackNearMiss()
    {
        if (!enableNearMiss ||
            nearMissTriggered ||
            nearMissTouchedPlayer ||
            playerMovement == null ||
            playerMovement.IsGameOver ||
            col == null ||
            !col.enabled)
        {
            return;
        }

        if (nearMissPlayerCollider == null)
            CacheNearMissPlayerCollider();

        if (nearMissPlayerCollider == null ||
            !nearMissPlayerCollider.enabled)
        {
            return;
        }

        ColliderDistance2D separation =
            col.Distance(nearMissPlayerCollider);

        float surfaceDistance = separation.distance;

        if (surfaceDistance <= 0f)
        {
            nearMissTouchedPlayer = true;
            return;
        }

        if (surfaceDistance <= nearMissDistance)
        {
            nearMissArmed = true;

            if (surfaceDistance < nearMissClosestDistance)
            {
                nearMissClosestDistance = surfaceDistance;
                nearMissClosestPoint = separation.pointA;
            }

            return;
        }

        if (!nearMissArmed)
            return;

        bool released =
            surfaceDistance >=
            nearMissDistance + nearMissReleaseDistance;

        if (released)
            TriggerNearMiss();
    }

    private void TriggerNearMiss()
    {
        if (nearMissTriggered ||
            !nearMissArmed ||
            nearMissTouchedPlayer)
        {
            return;
        }

        float closeness =
            NearMissFeedback.GetCloseness01(
                nearMissClosestDistance,
                nearMissDistance
            );

        nearMissTriggered = NearMissFeedback.TryTrigger(
            nearMissClosestPoint,
            closeness
        );
    }

    private GameObject GetProjectileFromPool(Vector3 position)
    {
        return RuntimeObjectPool.Spawn(
            projectilePrefab,
            position,
            Quaternion.identity
        );
    }

    private void RegisterActiveProjectile(EnemyProjectile projectile)
    {
        if (projectile == null)
            return;

        projectile.SetPoolOwner(this);

        if (!ownedProjectiles.Contains(projectile))
            ownedProjectiles.Add(projectile);
    }

    public void NotifyProjectileReturned(EnemyProjectile projectile)
    {
        if (projectile == null)
            return;

        ownedProjectiles.Remove(projectile);
    }

    public void ReturnProjectileToPool(GameObject projectile)
    {
        if (projectile == null)
            return;

        EnemyProjectile projectileScript =
            projectile.GetComponent<EnemyProjectile>();

        if (projectileScript != null)
        {
            projectileScript.ReturnToPool();
            return;
        }

        RuntimeObjectPool.Release(projectile);
    }

    private void HandleMovement(Transform currentTarget)
    {
        Vector2 targetPosition =
            currentTarget.position;

        Vector2 toTarget =
            targetPosition - rb.position;

        if (toTarget.sqrMagnitude <= 0.001f)
        {
            ResetStuckCheck();
            return;
        }

        float distance = toTarget.magnitude;
        Vector2 targetDirection = toTarget.normalized;

        Vector2 desiredDirection = Vector2.zero;
        float speedMultiplier = 1f;
        bool retreatingForReload = false;

        if (isReloading)
        {
            float safeReloadDistance =
                Mathf.Max(stoppingDistance, reloadRetreatDistance);

            if (distance < safeReloadDistance)
            {
                desiredDirection = -targetDirection;
                speedMultiplier = reloadMoveSpeedMultiplier;
                retreatingForReload = true;
            }
            else if (strafeEnabled)
            {
                Vector2 sideDirection =
                    new Vector2(
                        -targetDirection.y,
                        targetDirection.x
                    ) * strafeDirection;

                desiredDirection = sideDirection;
                speedMultiplier =
                    strafeSpeedMultiplier *
                    reloadMoveSpeedMultiplier;
            }
            else
            {
                ResetStuckCheck();
                return;
            }
        }
        else if (distance > stoppingDistance)
        {
            desiredDirection = targetDirection;
        }
        else if (distance < retreatDistance)
        {
            desiredDirection = -targetDirection;
        }
        else if (strafeEnabled)
        {
            desiredDirection =
                GetStrafeDirection(
                    targetDirection,
                    distance
                );

            speedMultiplier = strafeSpeedMultiplier;
        }
        else
        {
            ResetStuckCheck();
            return;
        }

        Vector2 waveDirection =
            GetWaveDirection(targetDirection);

        Vector2 separationDirection =
            GetSeparationDirection();

        Vector2 finalDirection =
            desiredDirection +
            waveDirection +
            separationDirection * separationStrength;

        if (finalDirection.sqrMagnitude <= 0.001f)
            finalDirection = desiredDirection;

        finalDirection.Normalize();

        if (unstuckTimer > 0f)
        {
            unstuckTimer -= Time.fixedDeltaTime;

            Vector2 sideDirection =
                new Vector2(
                    -targetDirection.y,
                    targetDirection.x
                ) * unstuckDirection;

            finalDirection =
                (
                    finalDirection +
                    sideDirection * unstuckSideForce
                ).normalized;
        }

        float movementDistance =
            moveSpeed *
            speedMultiplier *
            Time.fixedDeltaTime;

        if (retreatingForReload)
        {
            float safeReloadDistance =
                Mathf.Max(stoppingDistance, reloadRetreatDistance);

            float missingDistance =
                safeReloadDistance - distance;

            movementDistance =
                Mathf.Min(
                    movementDistance,
                    missingDistance
                );
        }
        else if (distance > stoppingDistance)
        {
            float excessDistance =
                distance - stoppingDistance;

            movementDistance =
                Mathf.Min(
                    movementDistance,
                    excessDistance
                );
        }
        else if (distance < retreatDistance)
        {
            float missingDistance =
                retreatDistance - distance;

            movementDistance =
                Mathf.Min(
                    movementDistance,
                    missingDistance
                );
        }

        Move(finalDirection, desiredDirection, movementDistance);
    }

    private Vector2 GetStrafeDirection(
        Vector2 targetDirection,
        float distance
    )
    {
        Vector2 sideDirection =
            new Vector2(
                -targetDirection.y,
                targetDirection.x
            ) * strafeDirection;

        float idealDistance =
            (stoppingDistance + retreatDistance) * 0.5f;

        float distanceDifference =
            distance - idealDistance;

        Vector2 distanceCorrection =
            Vector2.zero;

        if (Mathf.Abs(distanceDifference) >
            strafeDistanceTolerance)
        {
            float correctionStrength =
                Mathf.InverseLerp(
                    strafeDistanceTolerance,
                    Mathf.Max(
                        stoppingDistance - retreatDistance,
                        strafeDistanceTolerance + 0.01f
                    ),
                    Mathf.Abs(distanceDifference)
                );

            if (distanceDifference > 0f)
            {
                distanceCorrection =
                    targetDirection * correctionStrength;
            }
            else
            {
                distanceCorrection =
                    -targetDirection * correctionStrength;
            }
        }

        Vector2 finalDirection =
            sideDirection + distanceCorrection;

        if (finalDirection.sqrMagnitude <= 0.001f)
            return sideDirection;

        return finalDirection.normalized;
    }

    private Vector2 GetWaveDirection(
        Vector2 targetDirection
    )
    {
        Vector2 sideDirection =
            new Vector2(
                -targetDirection.y,
                targetDirection.x
            );

        float wave =
            Mathf.Sin(
                (Time.time + movementOffset) *
                sideMoveSpeed
            );

        return sideDirection *
               wave *
               sideMoveAmount;
    }

    private Vector2 GetSeparationDirection()
    {
        if (!separationEnabled)
            return Vector2.zero;

        if (separationRadius <= 0f)
            return Vector2.zero;

        ContactFilter2D filter =
            new ContactFilter2D();

        filter.SetLayerMask(enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = false;

        int hitCount =
            Physics2D.OverlapCircle(
                rb.position,
                separationRadius,
                filter,
                separationHits
            );

        if (hitCount <= 0)
            return Vector2.zero;

        Vector2 separationDirection =
            Vector2.zero;

        int validCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit =
                separationHits[i];

            if (hit == null)
                continue;

            if (hit.attachedRigidbody == rb)
                continue;

            ProjectileEnemyFollow other =
                hit.GetComponentInParent<
                    ProjectileEnemyFollow
                >();

            if (other == null || other == this)
                continue;

            Vector2 awayDirection =
                rb.position -
                (Vector2)other.transform.position;

            float sqrDistance =
                awayDirection.sqrMagnitude;

            if (sqrDistance <= 0.001f)
            {
                awayDirection =
                    Random.insideUnitCircle.normalized;

                sqrDistance = 0.001f;
            }

            float distance =
                Mathf.Sqrt(sqrDistance);

            float proximityStrength =
                1f -
                Mathf.Clamp01(
                    distance / separationRadius
                );

            separationDirection +=
                awayDirection.normalized *
                proximityStrength;

            validCount++;
        }

        if (validCount <= 0)
            return Vector2.zero;

        separationDirection /= validCount;

        return Vector2.ClampMagnitude(
            separationDirection,
            1f
        );
    }

    private void Move(
        Vector2 direction,
        Vector2 tacticalDirection,
        float distance
    )
    {
        if (distance <= 0f)
            return;

        attemptedMovementThisFrame = true;

        if (EnemyObstacleSteering2D.TryGetOverlapRecovery(
                col,
                navigationFilter,
                out Vector2 overlapDirection,
                out float penetrationDepth))
        {
            float recoveryDistance =
                EnemyObstacleSteering2D.GetOverlapRecoveryDistance(
                    penetrationDepth,
                    distance,
                    0.03f
                );

            Vector2 recoveryTarget =
                rb.position +
                overlapDirection * recoveryDistance;

            rb.MovePosition(
                ClampPositionInsideArena(recoveryTarget)
            );

            return;
        }

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Vector2 steeredDirection =
            EnemyObstacleSteering2D.GetSteeredDirection(
                col,
                direction,
                tacticalDirection,
                navigationFilter,
                avoidanceHits,
                obstacleProbeDistance,
                distance,
                0.03f,
                obstacleAvoidanceAttempts,
                obstacleOutwardBias,
                ref obstacleAvoidanceSide
            );

        if (steeredDirection.sqrMagnitude <= 0.001f)
            return;

        Vector2 desiredDisplacement =
            steeredDirection.normalized * distance;

        desiredDisplacement =
            ClampDisplacementToArena(desiredDisplacement);

        if (desiredDisplacement.sqrMagnitude <= 0.000001f)
            return;

        EnemyObstacleSteering2D.MoveDisplacementWithPhysicsSlide(
            rb,
            col,
            desiredDisplacement,
            Time.fixedDeltaTime,
            navigationFilter,
            5
        );
    }

    private Vector2 ClampDisplacementToArena(Vector2 displacement)
    {
        Vector2 desiredPosition = rb.position + displacement;
        Vector2 clampedPosition = ClampPositionInsideArena(desiredPosition);
        return clampedPosition - rb.position;
    }

    private Vector2 ClampPositionInsideArena(Vector2 desiredPosition)
    {
        CameraWorldBounds bounds = CameraWorldBounds.Instance;

        if (bounds == null || col == null)
            return desiredPosition;

        Bounds colliderBounds = col.bounds;
        Vector2 centerOffset =
            (Vector2)colliderBounds.center - rb.position;

        Vector2 extents = colliderBounds.extents;
        float padding = Mathf.Max(0f, arenaEdgePadding);

        float minX = bounds.MinX + extents.x + padding - centerOffset.x;
        float maxX = bounds.MaxX - extents.x - padding - centerOffset.x;
        float minY = bounds.MinY + extents.y + padding - centerOffset.y;
        float maxY = bounds.MaxY - extents.y - padding - centerOffset.y;

        if (minX > maxX)
        {
            float centerX = (bounds.MinX + bounds.MaxX) * 0.5f - centerOffset.x;
            minX = centerX;
            maxX = centerX;
        }

        if (minY > maxY)
        {
            float centerY = (bounds.MinY + bounds.MaxY) * 0.5f - centerOffset.y;
            minY = centerY;
            maxY = centerY;
        }

        return new Vector2(
            Mathf.Clamp(desiredPosition.x, minX, maxX),
            Mathf.Clamp(desiredPosition.y, minY, maxY)
        );
    }

    private void EnforceArenaBounds()
    {
        if (rb == null || isSpawning)
            return;

        Vector2 clampedPosition = ClampPositionInsideArena(rb.position);

        if ((clampedPosition - rb.position).sqrMagnitude <= 0.000001f)
            return;

        rb.position = clampedPosition;
        rb.linearVelocity = Vector2.zero;
        ResetStuckCheck();
    }

    private void HandleStrafeDirectionTimer()
    {
        if (!strafeEnabled)
            return;

        strafeDirectionTimer -=
            Time.fixedDeltaTime;

        if (strafeDirectionTimer > 0f)
            return;

        strafeDirection *= -1;
        ResetStrafeTimer();
    }

    private void ResetStrafeTimer()
    {
        strafeDirectionTimer =
            Random.Range(
                strafeDirectionChangeMinTime,
                strafeDirectionChangeMaxTime
            );
    }

    private void HandleAttack(
        Transform currentTarget
    )
    {
        if (isReloading)
            return;

        fireCooldown -= Time.deltaTime;

        if (fireCooldown > 0f)
            return;

        if (!CanSeeTarget(currentTarget))
        {
            fireCooldown = GetBlockedShotRetryDelay();
            return;
        }

        bool isFinalShot =
            shotsFiredInBurst + 1 >= Mathf.Max(1, shotsPerBurst);

        if (!ShootProjectile(currentTarget, isFinalShot))
        {
            fireCooldown = GetBlockedShotRetryDelay();
            return;
        }

        shotsFiredInBurst++;

        if (shotsFiredInBurst >= Mathf.Max(1, shotsPerBurst))
        {
            BeginReload();
            return;
        }

        fireCooldown = GetNextFireInterval();
    }

    private float GetNextFireInterval()
    {
        float jitter = Mathf.Clamp(fireIntervalJitter, 0f, 0.30f);
        float randomMultiplier = Random.Range(1f - jitter, 1f + jitter);

        return Mathf.Max(
            0.05f,
            fireRate * perEnemyFireCadenceMultiplier * randomMultiplier
        );
    }

    private float GetNextReloadDuration()
    {
        float jitter = Mathf.Clamp(reloadDurationJitter, 0f, 0.20f);
        float randomMultiplier = Random.Range(1f - jitter, 1f + jitter);

        return Mathf.Max(
            0.05f,
            reloadDuration * perEnemyReloadCadenceMultiplier * randomMultiplier
        );
    }

    private float GetBlockedShotRetryDelay()
    {
        // Randomized retry prevents two enemies that regain line of sight on
        // the same frame from immediately snapping back into sync.
        return Random.Range(0.12f, 0.28f) * perEnemyFireCadenceMultiplier;
    }

    private bool CanSeeTarget(
        Transform currentTarget
    )
    {
        if (firePoint == null ||
            currentTarget == null)
        {
            return false;
        }

        Vector2 targetPosition =
            GetAimPosition(currentTarget);

        Vector2 direction =
            targetPosition -
            (Vector2)firePoint.position;

        float distance =
            direction.magnitude;

        if (distance <= 0.001f)
            return true;

        RaycastHit2D hit =
            Physics2D.Raycast(
                firePoint.position,
                direction.normalized,
                distance,
                obstacleLayer
            );

        return hit.collider == null;
    }

    private Vector2 GetAimPosition(
        Transform currentTarget
    )
    {
        Vector2 targetPosition =
            currentTarget.position;

        if (!predictiveAimEnabled)
            return targetPosition;

        if (targetRigidbody == null)
            return targetPosition;

        float distance =
            Vector2.Distance(
                firePoint != null
                    ? firePoint.position
                    : transform.position,
                targetPosition
            );

        if (distance <
            predictionDistanceThreshold)
        {
            return targetPosition;
        }

        Vector2 predictionOffset =
            targetRigidbody.linearVelocity *
            predictionTime;

        predictionOffset =
            Vector2.ClampMagnitude(
                predictionOffset,
                maxPredictionDistance
            );

        return targetPosition +
               predictionOffset;
    }

    private bool ShootProjectile(
        Transform currentTarget,
        bool isFinalShot
    )
    {
        if (projectilePrefab == null ||
            firePoint == null ||
            currentTarget == null)
        {
            return false;
        }

        Vector2 aimPosition =
            GetAimPosition(currentTarget);

        Vector2 baseDirection =
            aimPosition -
            (Vector2)firePoint.position;

        if (baseDirection.sqrMagnitude <= 0.001f)
            return false;

        baseDirection.Normalize();

        bool firedAnyProjectile;

        if (isFinalShot)
        {
            int finalProjectileCount = GetFinalShotProjectileCount();
            firedAnyProjectile = FireFinalShotSpread(baseDirection, finalProjectileCount);
        }
        else
        {
            firedAnyProjectile =
                LaunchProjectile(baseDirection);
        }

        if (!firedAnyProjectile)
            return false;

        PlayFireSound();

        float recoilMultiplier =
            isFinalShot
                ? Mathf.Max(1f, finalShotRecoilMultiplier)
                : 1f;

        QueueShotRecoil(
            -baseDirection,
            recoilMultiplier
        );

        return true;
    }


    private int GetFinalShotProjectileCount()
    {
        int threshold = Mathf.Max(1, highDangerBurstThreshold);

        return shotsPerBurst >= threshold
            ? Mathf.Max(1, highDangerFinalShotProjectileCount)
            : Mathf.Max(1, lowDangerFinalShotProjectileCount);
    }

    private bool FireFinalShotSpread(
        Vector2 baseDirection,
        int projectileCount
    )
    {
        projectileCount = Mathf.Max(1, projectileCount);

        if (projectileCount == 1)
            return LaunchProjectile(baseDirection);

        float angleOffset =
            Mathf.Clamp(finalShotAngleOffset, 0f, 20f);

        bool firedAnyProjectile = false;

        if (projectileCount == 2)
        {
            firedAnyProjectile |=
                LaunchProjectile(
                    RotateDirection(baseDirection, -angleOffset)
                );

            firedAnyProjectile |=
                LaunchProjectile(
                    RotateDirection(baseDirection, angleOffset)
                );

            return firedAnyProjectile;
        }

        // Keep the spread symmetric. For 3 projectiles this becomes
        // -angleOffset, 0, +angleOffset. Higher counts are distributed
        // evenly across the same total spread.
        for (int i = 0; i < projectileCount; i++)
        {
            float t =
                projectileCount <= 1
                    ? 0.5f
                    : i / (float)(projectileCount - 1);

            float angle =
                Mathf.Lerp(-angleOffset, angleOffset, t);

            firedAnyProjectile |=
                LaunchProjectile(
                    RotateDirection(baseDirection, angle)
                );
        }

        return firedAnyProjectile;
    }

    private bool LaunchProjectile(Vector2 direction)
    {
        GameObject projectile =
            GetProjectileFromPool(firePoint.position);

        if (projectile == null)
            return false;

        if (direction.sqrMagnitude <= 0.001f)
        {
            ReturnProjectileToPool(projectile);
            return false;
        }

        direction.Normalize();

        EnemyProjectile projectileScript =
            projectile.GetComponent<EnemyProjectile>();

        if (projectileScript != null)
        {
            RegisterActiveProjectile(projectileScript);

            projectileScript.Launch(
                direction,
                projectileSpeed,
                playerMovement
            );
        }
        else
        {
            Rigidbody2D projectileRb =
                projectile.GetComponent<Rigidbody2D>();

            if (projectileRb != null)
            {
                projectileRb.linearVelocity =
                    direction * projectileSpeed;
            }
        }

        return true;
    }

    private static Vector2 RotateDirection(
        Vector2 direction,
        float degrees
    )
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos
        ).normalized;
    }

    private void PlayFireSound()
    {
        if (fireSound == null || audioSource == null)
            return;

        audioSource.volume = SoundManager.SFXVolume;
        audioSource.pitch = SoundManager.GetVariedPitch(
            1f,
            firePitchJitter
        );

        audioSource.PlayOneShot(
            fireSound,
            SoundManager.GetVariedVolumeMultiplier(
                1f,
                fireVolumeJitter
            )
        );
    }

    private void QueueShotRecoil(
        Vector2 recoilDirection,
        float multiplier
    )
    {
        if (shotRecoilDistance <= 0f ||
            recoilDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float distance =
            shotRecoilDistance * Mathf.Max(1f, multiplier);

        pendingShotRecoil +=
            recoilDirection.normalized * distance;

        pendingShotRecoil = Vector2.ClampMagnitude(
            pendingShotRecoil,
            shotRecoilDistance * 2f
        );
    }

    private bool ApplyPendingShotRecoil()
    {
        if (pendingShotRecoil.sqrMagnitude <= 0.000001f)
            return false;

        Vector2 displacement =
            ClampDisplacementToArena(pendingShotRecoil);
        pendingShotRecoil = Vector2.zero;

        if (displacement.sqrMagnitude <= 0.000001f)
        {
            shotRecoilPauseTimer =
                Mathf.Max(shotRecoilPauseTimer, shotRecoilPause);

            ResetStuckCheck();
            return true;
        }

        EnemyObstacleSteering2D.MoveDisplacementWithPhysicsSlide(
            rb,
            col,
            displacement,
            Time.fixedDeltaTime,
            navigationFilter,
            3
        );

        shotRecoilPauseTimer =
            Mathf.Max(shotRecoilPauseTimer, shotRecoilPause);

        ResetStuckCheck();
        return true;
    }

    private void BeginReload()
    {
        if (isReloading || stopped)
            return;

        isReloading = true;
        activeReloadDuration = GetNextReloadDuration();
        reloadTimer = activeReloadDuration;
        reloadVisualTime = 0f;
        fireCooldown = 0f;

        PlayReloadSound();
        UpdateReloadVisual();
    }

    private void UpdateReloadState()
    {
        if (!isReloading)
            return;

        reloadTimer -= Time.deltaTime;
        reloadVisualTime += Time.deltaTime;
        UpdateReloadVisual();
        UpdateReloadSoundFade();

        if (reloadTimer > 0f)
            return;

        FinishReload();
    }

    private void FinishReload()
    {
        isReloading = false;
        reloadTimer = 0f;
        reloadVisualTime = 0f;
        activeReloadDuration = 0f;
        shotsFiredInBurst = 0;

        RestoreReloadVisuals();
        StopReloadSound();

        float minDelay = Mathf.Max(0f, postReloadFireDelayMin);
        float maxDelay = Mathf.Max(minDelay, postReloadFireDelayMax);

        fireCooldown =
            Random.Range(minDelay, maxDelay) +
            GetNextFireInterval() * Random.Range(0.10f, 0.25f);
    }

    private void PlayReloadSound()
    {
        if (reloadSound == null || reloadAudioSource == null)
            return;

        float minVolume = Mathf.Clamp(
            reloadVolumeMinMultiplier,
            0.5f,
            1f
        );

        float maxVolume = Mathf.Clamp(
            reloadVolumeMaxMultiplier,
            minVolume,
            1f
        );

        float minPitch = Mathf.Clamp(
            reloadPitchMin,
            0.9f,
            1.1f
        );

        float maxPitch = Mathf.Clamp(
            reloadPitchMax,
            minPitch,
            1.1f
        );

        float randomVolumeMultiplier =
            Random.Range(minVolume, maxVolume);

        float randomPitch =
            Random.Range(minPitch, maxPitch);

        activeReloadSfxVolume =
            SoundManager.SFXVolume * randomVolumeMultiplier;

        reloadAudioSource.Stop();
        reloadAudioSource.clip = reloadSound;
        reloadAudioSource.loop = false;
        reloadAudioSource.pitch = randomPitch;
        reloadAudioSource.volume = activeReloadSfxVolume;
        reloadAudioSource.Play();
    }

    private void UpdateReloadSoundFade()
    {
        if (reloadAudioSource == null ||
            !reloadAudioSource.isPlaying)
        {
            return;
        }

        float fadeDuration = Mathf.Max(
            0.01f,
            Mathf.Min(
                reloadSfxFadeOutDuration,
                activeReloadDuration > 0f
                    ? activeReloadDuration
                    : reloadDuration
            )
        );

        if (reloadTimer > fadeDuration)
        {
            reloadAudioSource.volume = activeReloadSfxVolume;
            return;
        }

        float t = Mathf.Clamp01(reloadTimer / fadeDuration);
        t = t * t * (3f - 2f * t);

        reloadAudioSource.volume =
            activeReloadSfxVolume * t;
    }

    private void StopReloadSound()
    {
        if (reloadAudioSource == null)
            return;

        reloadAudioSource.Stop();
        reloadAudioSource.clip = null;
        reloadAudioSource.volume = 1f;
        reloadAudioSource.pitch = 1f;
        activeReloadSfxVolume = 0f;
    }

    private void CacheReloadRenderers()
    {
        reloadRenderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        if (reloadRenderers == null || reloadRenderers.Length == 0)
        {
            reloadRendererBaseAlphas = null;
            return;
        }

        reloadRendererBaseAlphas =
            new float[reloadRenderers.Length];

        for (int i = 0; i < reloadRenderers.Length; i++)
        {
            SpriteRenderer renderer = reloadRenderers[i];

            reloadRendererBaseAlphas[i] =
                renderer != null
                    ? renderer.color.a
                    : 1f;
        }
    }

    private void UpdateReloadVisual()
    {
        if (reloadRenderers == null ||
            reloadRendererBaseAlphas == null)
        {
            return;
        }

        float frequency = Mathf.Max(0.1f, reloadBlinkFrequency);
        float pulse =
            0.5f -
            0.5f * Mathf.Cos(
                reloadVisualTime * frequency * Mathf.PI * 2f
            );

        pulse = Mathf.SmoothStep(0f, 1f, pulse);

        float alphaMultiplier = Mathf.Lerp(
            Mathf.Clamp(reloadBlinkMinAlpha, 0.05f, 1f),
            1f,
            pulse
        );

        for (int i = 0; i < reloadRenderers.Length; i++)
        {
            SpriteRenderer renderer = reloadRenderers[i];

            if (renderer == null)
                continue;

            Color color = renderer.color;
            color.a = reloadRendererBaseAlphas[i] * alphaMultiplier;
            renderer.color = color;
        }
    }

    private void RestoreReloadVisuals()
    {
        if (reloadRenderers == null ||
            reloadRendererBaseAlphas == null)
        {
            return;
        }

        int count = Mathf.Min(
            reloadRenderers.Length,
            reloadRendererBaseAlphas.Length
        );

        for (int i = 0; i < count; i++)
        {
            SpriteRenderer renderer = reloadRenderers[i];

            if (renderer == null)
                continue;

            Color color = renderer.color;
            color.a = reloadRendererBaseAlphas[i];
            renderer.color = color;
        }
    }

    private void HandleStuckCheck(
        Transform currentTarget
    )
    {
        if (!attemptedMovementThisFrame)
        {
            ResetStuckCheck();
            return;
        }

        if (currentTarget == null)
        {
            ResetStuckCheck();
            return;
        }

        stuckTimer += Time.fixedDeltaTime;

        float effectiveStuckCheckTime = Mathf.Min(
            Mathf.Max(0.05f, stuckCheckTime),
            0.25f
        );

        if (stuckTimer < effectiveStuckCheckTime)
            return;

        float movedSqrDistance =
            (rb.position - lastPosition)
            .sqrMagnitude;

        float stuckSqrDistance =
            stuckDistance * stuckDistance;

        if (movedSqrDistance <
            stuckSqrDistance)
        {
            Vector2 escapeDirection =
                GetEscapeDirection();

            if (escapeDirection != Vector2.zero)
            {
                Vector2 escapeTarget =
                    rb.position +
                    escapeDirection *
                    moveSpeed *
                    escapeSpeedMultiplier *
                    Time.fixedDeltaTime;

                rb.MovePosition(
                    ClampPositionInsideArena(escapeTarget)
                );

                unstuckDirection =
                    Random.Range(0, 2) == 0
                        ? -1
                        : 1;

                unstuckTimer =
                    unstuckDuration;
            }
        }

        lastPosition = rb.position;
        stuckTimer = 0f;
    }

    private Vector2 GetEscapeDirection()
    {
        ContactFilter2D filter =
            new ContactFilter2D();

        filter.SetLayerMask(
            EnemyObstacleSteering2D.BuildNavigationMask(obstacleLayer)
        );
        filter.useLayerMask = true;
        filter.useTriggers = false;

        int hitCount =
            Physics2D.OverlapCircle(
                rb.position,
                escapeCheckRadius,
                filter,
                escapeHits
            );

        if (hitCount <= 0)
            return Vector2.zero;

        Vector2 escapeDirection =
            Vector2.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit =
                escapeHits[i];

            if (hit == null)
                continue;

            Vector2 closestPoint =
                hit.ClosestPoint(rb.position);

            Vector2 awayFromObstacle =
                rb.position -
                closestPoint;

            if (awayFromObstacle.sqrMagnitude <=
                0.001f)
            {
                awayFromObstacle =
                    rb.position -
                    (Vector2)hit.bounds.center;
            }

            if (awayFromObstacle.sqrMagnitude >
                0.001f)
            {
                escapeDirection +=
                    awayFromObstacle.normalized;
            }
        }

        if (escapeDirection.sqrMagnitude <=
            0.001f)
        {
            return Vector2.zero;
        }

        return escapeDirection.normalized;
    }

    private void ResetStuckCheck()
    {
        stuckTimer = 0f;
        lastPosition = rb.position;
    }

    private void FlipSprite(
        Transform currentTarget
    )
    {
        if (currentTarget == null ||
            isSpawning)
        {
            return;
        }

        Vector2 direction =
            currentTarget.position -
            transform.position;

        Vector3 scale =
            transform.localScale;

        float absX =
            Mathf.Abs(scale.x);

        if (absX <= 0.001f)
        {
            absX =
                Mathf.Abs(
                    spawnTargetScale.x
                );
        }

        if (direction.x > 0.01f)
            scale.x = absX;
        else if (direction.x < -0.01f)
            scale.x = -absX;

        transform.localScale = scale;
    }

    private void StopMovementOnly()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private void StopEnemy()
    {
        if (stopped)
            return;

        stopped = true;
        isReloading = false;
        reloadTimer = 0f;
        reloadVisualTime = 0f;
        activeReloadDuration = 0f;
        shotRecoilPauseTimer = 0f;
        pendingShotRecoil = Vector2.zero;

        RestoreReloadVisuals();
        StopReloadSound();
        StopAllCoroutines();

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        if (audioSource != null)
            audioSource.Stop();

        DisableActiveProjectiles();

        enabled = false;
    }

    private void DisableActiveProjectiles()
    {
        while (ownedProjectiles.Count > 0)
        {
            int lastIndex = ownedProjectiles.Count - 1;
            EnemyProjectile projectile = ownedProjectiles[lastIndex];
            ownedProjectiles.RemoveAt(lastIndex);

            if (projectile == null)
                continue;

            projectile.SetPoolOwner(null);

            if (projectile.gameObject.activeSelf)
                projectile.ReturnToPool();
        }
    }

    private IEnumerator SpawnEffect()
    {
        isSpawning = true;

        if (spawnEffectDuration <= 0f)
        {
            transform.localScale =
                spawnTargetScale;

            isSpawning = false;
            EnforceArenaBounds();
            yield break;
        }

        float time = 0f;

        while (time <
               spawnEffectDuration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time /
                    spawnEffectDuration
                );

            t = t * t *
                (3f - 2f * t);

            transform.localScale =
                Vector3.Lerp(
                    Vector3.zero,
                    spawnTargetScale,
                    t
                );

            yield return null;
        }

        transform.localScale =
            spawnTargetScale;

        isSpawning = false;

        EnforceArenaBounds();
        ResetStuckCheck();
    }

    private void OnDestroy()
    {
        RestoreReloadVisuals();
        StopReloadSound();
        DisableActiveProjectiles();
    }

    private void OnValidate()
    {
        shotRecoilDistance = Mathf.Clamp(shotRecoilDistance, 0f, 0.05f);
        shotRecoilPause = Mathf.Clamp(shotRecoilPause, 0f, 0.03f);
        finalShotRecoilMultiplier = Mathf.Clamp(finalShotRecoilMultiplier, 1f, 1.18f);
        lowDangerFinalShotProjectileCount = Mathf.Max(1, lowDangerFinalShotProjectileCount);
        highDangerFinalShotProjectileCount = Mathf.Max(1, highDangerFinalShotProjectileCount);
        highDangerBurstThreshold = Mathf.Max(1, highDangerBurstThreshold);
        finalShotAngleOffset = Mathf.Clamp(finalShotAngleOffset, 0f, 20f);
        arenaEdgePadding = Mathf.Max(0f, arenaEdgePadding);

        fireIntervalJitter = Mathf.Clamp(fireIntervalJitter, 0f, 0.30f);
        reloadDurationJitter = Mathf.Clamp(reloadDurationJitter, 0f, 0.20f);
        initialFireDelayMin = Mathf.Max(0f, initialFireDelayMin);
        initialFireDelayMax = Mathf.Max(initialFireDelayMin, initialFireDelayMax);
        postReloadFireDelayMin = Mathf.Max(0f, postReloadFireDelayMin);
        postReloadFireDelayMax = Mathf.Max(postReloadFireDelayMin, postReloadFireDelayMax);

        shotsPerBurst = Mathf.Max(1, shotsPerBurst);
        reloadDuration = Mathf.Max(0.05f, reloadDuration);
        reloadMoveSpeedMultiplier = Mathf.Max(0.1f, reloadMoveSpeedMultiplier);
        reloadBlinkMinAlpha = Mathf.Clamp(reloadBlinkMinAlpha, 0.05f, 1f);
        reloadBlinkFrequency = Mathf.Max(0.1f, reloadBlinkFrequency);
    }
}