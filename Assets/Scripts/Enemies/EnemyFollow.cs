using System.Collections;
using UnityEngine;

public enum NormalEnemyPursuitRole
{
    Pursuer,
    Interceptor,
    Flanker
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Movement")]
    public float speed = 5f;
    public float maxSpeed = 7f;
    public float speedIncreaseRate = 0.1f;
    public float minStartSpeed = 1.5f;
    public float maxStartSpeed = 2.5f;

    [Header("Predictive Pursuit")]
    public bool predictionEnabled = true;
    public float predictionDistanceThreshold = 2.5f;
    public float predictionTime = 0.25f;
    public float maxPredictionDistance = 1.5f;

    [Header("Group Pursuit")]
    public NormalEnemyPursuitRole pursuitRole =
        NormalEnemyPursuitRole.Pursuer;

    [Min(0f)]
    public float interceptorLeadDistance = 2.6f;

    [Min(0f)]
    public float flankerLeadDistance = 0.9f;

    [Min(0f)]
    public float flankerSideOffset = 2.2f;

    [Min(0.1f)]
    public float tacticalOffsetFadeDistance = 3.4f;

    [Header("Wave Movement")]
    public float minSideMoveAmount = 0.1f;
    public float maxSideMoveAmount = 0.35f;
    public float minSideMoveSpeed = 1.5f;
    public float maxSideMoveSpeed = 3f;
    public float waveFadeDistance = 2.5f;

    [Header("Enemy Separation")]
    public bool separationEnabled = true;
    public LayerMask enemyLayer;
    public float separationRadius = 0.75f;
    public float separationStrength = 0.65f;

    [Header("Obstacle Avoidance")]
    public LayerMask obstacleLayer;
    public float obstacleProbeDistance = 0.75f;
    [Range(2, 8)] public int obstacleAvoidanceAttempts = 5;
    [Range(0f, 1f)] public float obstacleOutwardBias = 0.25f;

    [Header("Advanced Unstuck")]
    public float escapeCheckRadius = 1.2f;
    public float escapeSpeedMultiplier = 2.2f;

    [Header("Spawn Effect")]
    public float spawnEffectDuration = 0.15f;

    [Header("Boss Absorption")]
    [Min(0f)] public float bossAbsorptionSpeed = 8f;
    [Min(0f)] public float bossAbsorptionDistance = 0.3f;
    [Min(0f)] public float bossAbsorptionShrinkDuration = 0.12f;

    public bool IsBeingAbsorbed => isBeingAbsorbed;

    [Header("Stuck Fix")]
    public float stuckCheckTime = 0.5f;
    public float stuckDistance = 0.08f;
    public float unstuckDuration = 0.4f;
    public float unstuckSideForce = 1.5f;

    [Header("Close Range Smoothing")]
    public float closeRangeDistance = 0.8f;
    [Range(0.5f, 1f)]
    public float closeRangeSpeedMultiplier = 0.9f;

    [Header("Near Miss")]
    [SerializeField]
    private bool enableNearMiss = true;

    [Tooltip("Surface-to-surface distance that arms a normal-enemy near miss.")]
    [SerializeField, Min(0.05f)]
    private float nearMissDistance = 0.80f;

    [Tooltip("Enemy must separate this much after the closest point before the near miss fires.")]
    [SerializeField, Min(0f)]
    private float nearMissReleaseDistance = 0.10f;

    private float movementOffset;
    private float beaconSpeedMultiplier = 1f;
    private float sideMoveAmount;
    private float sideMoveSpeed;

    private Rigidbody2D rb;
    private Collider2D col;
    private Rigidbody2D targetRigidbody;
    private PlayerMovement playerMovement;
    private Transform originalPlayerTarget;

    private Vector3 spawnTargetScale;
    private Vector2 lastPosition;

    private bool isSpawning;
    private bool isBeingAbsorbed;
    private bool isFinishingAbsorption;
    private bool absorptionNotified;
    private BossEnemyFollow absorptionBoss;
    private Vector3 absorptionStartScale;

    private float stuckTimer;
    private float unstuckTimer;
    private int unstuckDirection = 1;
    private int obstacleAvoidanceSide = 1;
    private int flankSide = 1;

    private ContactFilter2D navigationFilter;

    private Collider2D nearMissPlayerCollider;
    private bool nearMissArmed;
    private bool nearMissTriggered;
    private bool nearMissTouchedPlayer;
    private float nearMissClosestDistance = float.PositiveInfinity;
    private Vector3 nearMissClosestPoint;

    private readonly RaycastHit2D[] avoidanceHits = new RaycastHit2D[12];
    private readonly RaycastHit2D[] absorptionPathHits = new RaycastHit2D[8];
    private readonly Collider2D[] escapeHits = new Collider2D[16];
    private readonly Collider2D[] separationHits = new Collider2D[16];

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
    }

    private void Start()
    {
        spawnTargetScale = transform.localScale;

        if (spawnTargetScale == Vector3.zero)
            spawnTargetScale = Vector3.one;

        transform.localScale = Vector3.zero;

        speed = Random.Range(minStartSpeed, maxStartSpeed);

        unstuckDirection = Random.Range(0, 2) == 0 ? -1 : 1;
        obstacleAvoidanceSide = unstuckDirection;
        unstuckTimer = 0f;

        FindPlayerIfNeeded();

        originalPlayerTarget = player;

        movementOffset = Random.Range(0f, 100f);
        sideMoveAmount = Random.Range(minSideMoveAmount, maxSideMoveAmount);

        if (Random.value < 0.5f)
            sideMoveAmount *= -1f;

        sideMoveSpeed = Random.Range(minSideMoveSpeed, maxSideMoveSpeed);

        lastPosition = rb.position;
        ResetNearMissTracking();

        StartCoroutine(SpawnEffect());
    }

    private void FixedUpdate()
    {
        if (isBeingAbsorbed)
        {
            AbsorbIntoBoss();
            return;
        }

        FindPlayerIfNeeded();
        UpdateTarget();

        if (player == null)
        {
            StopEnemy();
            return;
        }

        if (playerMovement != null && playerMovement.IsGameOver)
        {
            StopEnemy();
            return;
        }

        if (isSpawning)
        {
            StopEnemy();
            return;
        }

        TrackNearMiss();
        FollowTarget();
        IncreaseSpeed();
    }

    private void UpdateTarget()
    {
        Transform desiredTarget = VoidCloneAbility.ActiveCloneTarget != null
            ? VoidCloneAbility.ActiveCloneTarget
            : originalPlayerTarget;

        if (player == desiredTarget)
            return;

        player = desiredTarget;
        targetRigidbody = player != null
            ? player.GetComponent<Rigidbody2D>()
            : null;
    }

    private void FindPlayerIfNeeded()
    {
        if (originalPlayerTarget != null)
            return;

        GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");

        if (foundPlayer == null)
            return;

        originalPlayerTarget = foundPlayer.transform;
        player = originalPlayerTarget;

        playerMovement = foundPlayer.GetComponent<PlayerMovement>();
        targetRigidbody = foundPlayer.GetComponent<Rigidbody2D>();
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
            isBeingAbsorbed ||
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

    public bool BeginBossAbsorption(BossEnemyFollow boss)
    {
        if (boss == null || isBeingAbsorbed)
            return false;

        absorptionBoss = boss;
        isBeingAbsorbed = true;
        nearMissArmed = false;
        isFinishingAbsorption = false;
        absorptionNotified = false;

        StopAllCoroutines();
        isSpawning = false;

        if (transform.localScale == Vector3.zero)
            transform.localScale = spawnTargetScale == Vector3.zero
                ? Vector3.one
                : spawnTargetScale;

        absorptionStartScale = transform.localScale;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            lastPosition = rb.position;
        }

        stuckTimer = 0f;
        unstuckTimer = 0f;

        if (obstacleAvoidanceSide == 0)
            obstacleAvoidanceSide =
                Random.Range(0, 2) == 0 ? -1 : 1;

        if (col != null)
            col.isTrigger = true;

        return true;
    }

    private void AbsorbIntoBoss()
    {
        if (isFinishingAbsorption)
            return;

        if (absorptionBoss == null)
        {
            NotifyAbsorptionComplete();
            Destroy(gameObject);
            return;
        }

        Vector2 current = rb != null
            ? rb.position
            : (Vector2)transform.position;

        Vector2 target =
            absorptionBoss.transform.position;

        Vector2 toBoss =
            target - current;

        float distance =
            toBoss.magnitude;

        float finishDistance =
            Mathf.Max(
                0.01f,
                bossAbsorptionDistance
            );

        Vector2 targetDirection =
            toBoss.sqrMagnitude > 0.001f
                ? toBoss.normalized
                : Vector2.zero;

        // Boss cok yakinda olsa bile arada obstacle varsa shrink/finish
        // fazina GECME. Once normal Stalker obstacle steering ile dolan.
        bool directPathClear =
            distance <= finishDistance &&
            EnemyObstacleSteering2D.IsPathClear(
                col,
                targetDirection,
                navigationFilter,
                absorptionPathHits,
                distance,
                0.02f
            );

        if (distance <= finishDistance &&
            directPathClear)
        {
            StartCoroutine(
                FinishBossAbsorption()
            );
            return;
        }

        if (targetDirection.sqrMagnitude <= 0.001f)
            return;

        float absorptionStep =
            Mathf.Max(
                0f,
                bossAbsorptionSpeed
            ) *
            Time.fixedDeltaTime;

        // Player chase'de kullanilan AYNI steering sistemi.
        // Stalker Boss'a giderken de collider'i ile ileri bakar,
        // obstacle'i gorur ve uygun taraftan dolanir.
        Vector2 steeredDirection =
            EnemyObstacleSteering2D.GetSteeredDirection(
                col,
                targetDirection,
                targetDirection,
                navigationFilter,
                avoidanceHits,
                obstacleProbeDistance,
                absorptionStep,
                0.03f,
                obstacleAvoidanceAttempts,
                obstacleOutwardBias,
                ref obstacleAvoidanceSide
            );

        bool moved = false;

        if (steeredDirection.sqrMagnitude > 0.001f)
        {
            Vector2 next =
                current +
                steeredDirection.normalized *
                absorptionStep;

            if (rb != null)
                rb.MovePosition(next);
            else
                transform.position = next;

            FlipSprite(
                steeredDirection.normalized
            );

            moved = true;
        }

        HandleAbsorptionStuck(
            distance,
            moved
        );
    }

    private void HandleAbsorptionStuck(
        float distanceToBoss,
        bool attemptedMove)
    {
        if (rb == null)
            return;

        if (distanceToBoss <=
            Mathf.Max(
                0.15f,
                bossAbsorptionDistance
            ))
        {
            ResetStuckCheck();
            return;
        }

        stuckTimer +=
            Time.fixedDeltaTime;

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
            stuckDistance *
            stuckDistance;

        if (!attemptedMove ||
            movedSqrDistance < stuckSqrDistance)
        {
            Vector2 escapeDirection =
                GetEscapeDirection();

            if (escapeDirection.sqrMagnitude <= 0.001f &&
                absorptionBoss != null)
            {
                Vector2 bossDirection =
                    (Vector2)absorptionBoss.transform.position -
                    rb.position;

                if (bossDirection.sqrMagnitude > 0.001f)
                {
                    bossDirection.Normalize();

                    Vector2 sideDirection =
                        new Vector2(
                            -bossDirection.y,
                            bossDirection.x
                        ) *
                        obstacleAvoidanceSide;

                    escapeDirection =
                        sideDirection.normalized;
                }
            }

            if (escapeDirection.sqrMagnitude > 0.001f)
            {
                float escapeDistance =
                    Mathf.Max(
                        0f,
                        bossAbsorptionSpeed
                    ) *
                    escapeSpeedMultiplier *
                    Time.fixedDeltaTime;

                Vector2 steeredEscape =
                    EnemyObstacleSteering2D.GetSteeredDirection(
                        col,
                        escapeDirection,
                        absorptionBoss != null
                            ? ((Vector2)absorptionBoss.transform.position -
                               rb.position).normalized
                            : escapeDirection,
                        navigationFilter,
                        avoidanceHits,
                        obstacleProbeDistance,
                        escapeDistance,
                        0.03f,
                        obstacleAvoidanceAttempts,
                        obstacleOutwardBias,
                        ref obstacleAvoidanceSide
                    );

                if (steeredEscape.sqrMagnitude > 0.001f)
                {
                    rb.MovePosition(
                        rb.position +
                        steeredEscape.normalized *
                        escapeDistance
                    );
                }
            }

            obstacleAvoidanceSide *= -1;
            unstuckTimer = unstuckDuration;
        }

        lastPosition = rb.position;
        stuckTimer = 0f;
    }

    private IEnumerator FinishBossAbsorption()
    {
        if (isFinishingAbsorption)
            yield break;

        isFinishingAbsorption = true;

        float duration =
            Mathf.Max(
                0f,
                bossAbsorptionShrinkDuration
            );

        float timer = 0f;

        Vector3 startScale =
            transform.localScale;

        while (timer < duration &&
               absorptionBoss != null)
        {
            Vector2 current =
                rb != null
                    ? rb.position
                    : (Vector2)transform.position;

            Vector2 target =
                absorptionBoss.transform.position;

            Vector2 toBoss =
                target - current;

            float distance =
                toBoss.magnitude;

            Vector2 direction =
                toBoss.sqrMagnitude > 0.001f
                    ? toBoss.normalized
                    : Vector2.zero;

            // Moving obstacle son anda araya girerse artik transform.Lerp ile
            // obstacle'in icinden gecme. Finish iptal olur ve FixedUpdate'ta
            // tekrar normal absorption steering'e doner.
            if (distance > 0.001f &&
                !EnemyObstacleSteering2D.IsPathClear(
                    col,
                    direction,
                    navigationFilter,
                    absorptionPathHits,
                    distance,
                    0.01f
                ))
            {
                isFinishingAbsorption = false;
                absorptionStartScale =
                    transform.localScale;

                yield break;
            }

            timer += Time.deltaTime;

            float t =
                duration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        timer / duration
                    );

            float smoothT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            Vector2 nextPosition =
                Vector2.Lerp(
                    current,
                    target,
                    smoothT
                );

            if (rb != null)
                rb.MovePosition(nextPosition);
            else
                transform.position = nextPosition;

            transform.localScale =
                Vector3.Lerp(
                    startScale,
                    Vector3.zero,
                    smoothT
                );

            yield return null;
        }

        NotifyAbsorptionComplete();
        Destroy(gameObject);
    }

    private void NotifyAbsorptionComplete()
    {
        if (absorptionNotified)
            return;

        absorptionNotified = true;

        if (absorptionBoss != null)
            absorptionBoss.NotifyStalkerAbsorbed(this);
    }

    private void OnDestroy()
    {
        if (isBeingAbsorbed)
            NotifyAbsorptionComplete();
    }

    public void ConfigurePursuitRole(
        NormalEnemyPursuitRole role,
        int preferredFlankSide = 1
    )
    {
        pursuitRole = role;
        flankSide = preferredFlankSide < 0 ? -1 : 1;
    }

    private IEnumerator SpawnEffect()
    {
        isSpawning = true;

        if (spawnEffectDuration <= 0f)
        {
            transform.localScale = spawnTargetScale;
            isSpawning = false;
            yield break;
        }

        float time = 0f;

        while (time < spawnEffectDuration)
        {
            time += Time.deltaTime;

            float t = Mathf.Clamp01(time / spawnEffectDuration);

            // Hafif yumuşak spawn eğrisi.
            t = t * t * (3f - 2f * t);

            transform.localScale = Vector3.Lerp(
                Vector3.zero,
                spawnTargetScale,
                t
            );

            yield return null;
        }

        transform.localScale = spawnTargetScale;
        isSpawning = false;

        lastPosition = rb.position;
        stuckTimer = 0f;
    }

    private void FollowTarget()
    {
        Vector2 targetPosition = GetTargetPosition();
        Vector2 toTarget = targetPosition - rb.position;

        float distanceToTarget = Vector2.Distance(
            rb.position,
            player.position
        );

        if (toTarget.sqrMagnitude <= 0.001f)
        {
            ResetStuckCheck();
            return;
        }

        Vector2 targetDirection = toTarget.normalized;
        Vector2 finalDirection = targetDirection;

        finalDirection += GetWaveDirection(
            targetDirection,
            distanceToTarget
        );

        finalDirection += GetSeparationDirection()
            * separationStrength;

        if (finalDirection.sqrMagnitude <= 0.001f)
            finalDirection = targetDirection;
        else
            finalDirection.Normalize();

        if (unstuckTimer > 0f)
        {
            unstuckTimer -= Time.fixedDeltaTime;

            Vector2 sideDirection = new Vector2(
                -targetDirection.y,
                targetDirection.x
            ) * unstuckDirection;

            finalDirection = (
                finalDirection +
                sideDirection * unstuckSideForce
            ).normalized;
        }

        Move(finalDirection, targetDirection, distanceToTarget);
        HandleStuckCheck(distanceToTarget);
        FlipSprite(finalDirection);
    }

    private Vector2 GetTargetPosition()
    {
        Vector2 playerPosition = player.position;

        // Clone aggro should stay simple and predictable: all Stalkers
        // converge on the clone instead of trying to flank a decoy.
        if (player != originalPlayerTarget)
            return playerPosition;

        float distanceToPlayer = Vector2.Distance(
            rb.position,
            playerPosition
        );

        Vector2 targetVelocity =
            targetRigidbody != null
                ? targetRigidbody.linearVelocity
                : Vector2.zero;

        Vector2 predictionOffset = Vector2.zero;

        if (predictionEnabled &&
            targetRigidbody != null &&
            distanceToPlayer >= predictionDistanceThreshold)
        {
            predictionOffset =
                targetVelocity * predictionTime;

            predictionOffset = Vector2.ClampMagnitude(
                predictionOffset,
                maxPredictionDistance
            );
        }

        Vector2 tacticalOffset = Vector2.zero;

        if (pursuitRole != NormalEnemyPursuitRole.Pursuer)
        {
            Vector2 travelDirection =
                GetPlayerTravelDirection(
                    playerPosition,
                    targetVelocity
                );

            float tacticalStrength = Mathf.InverseLerp(
                closeRangeDistance,
                tacticalOffsetFadeDistance,
                distanceToPlayer
            );

            if (pursuitRole ==
                NormalEnemyPursuitRole.Interceptor)
            {
                tacticalOffset =
                    travelDirection *
                    interceptorLeadDistance *
                    tacticalStrength;
            }
            else if (pursuitRole ==
                     NormalEnemyPursuitRole.Flanker)
            {
                Vector2 sideDirection = new Vector2(
                    -travelDirection.y,
                    travelDirection.x
                ) * flankSide;

                tacticalOffset = (
                    travelDirection * flankerLeadDistance +
                    sideDirection * flankerSideOffset
                ) * tacticalStrength;
            }
        }

        return ClampTargetInsideArena(
            playerPosition +
            predictionOffset +
            tacticalOffset
        );
    }

    private Vector2 GetPlayerTravelDirection(
        Vector2 playerPosition,
        Vector2 targetVelocity
    )
    {
        if (targetVelocity.sqrMagnitude > 0.04f)
            return targetVelocity.normalized;

        if (playerMovement != null)
        {
            Vector2 inputDirection =
                playerMovement.CurrentMoveInput;

            if (inputDirection.sqrMagnitude <= 0.04f)
            {
                inputDirection =
                    playerMovement.LastMoveDirection;
            }

            if (inputDirection.sqrMagnitude > 0.04f)
                return inputDirection.normalized;
        }

        Vector2 enemyToPlayer =
            playerPosition - rb.position;

        if (enemyToPlayer.sqrMagnitude > 0.001f)
            return enemyToPlayer.normalized;

        return Vector2.right;
    }

    private static Vector2 ClampTargetInsideArena(
        Vector2 targetPosition
    )
    {
        CameraWorldBounds bounds =
            CameraWorldBounds.Instance;

        if (bounds == null)
            return targetPosition;

        const float margin = 0.25f;

        return new Vector2(
            Mathf.Clamp(
                targetPosition.x,
                bounds.MinX + margin,
                bounds.MaxX - margin
            ),
            Mathf.Clamp(
                targetPosition.y,
                bounds.MinY + margin,
                bounds.MaxY - margin
            )
        );
    }

    private Vector2 GetWaveDirection(
        Vector2 targetDirection,
        float distanceToTarget
    )
    {
        if (waveFadeDistance <= 0f)
            return Vector2.zero;

        float waveStrength = Mathf.InverseLerp(
            closeRangeDistance,
            waveFadeDistance,
            distanceToTarget
        );

        if (waveStrength <= 0f)
            return Vector2.zero;

        Vector2 sideDirection = new Vector2(
            -targetDirection.y,
            targetDirection.x
        );

        float wave = Mathf.Sin(
            (Time.time + movementOffset) * sideMoveSpeed
        );

        return sideDirection
            * wave
            * sideMoveAmount
            * waveStrength;
    }

    private Vector2 GetSeparationDirection()
    {
        if (!separationEnabled)
            return Vector2.zero;

        if (separationRadius <= 0f)
            return Vector2.zero;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
        filter.useLayerMask = true;
        filter.useTriggers = false;

        int hitCount = Physics2D.OverlapCircle(
            rb.position,
            separationRadius,
            filter,
            separationHits
        );

        if (hitCount <= 0)
            return Vector2.zero;

        Vector2 separationDirection = Vector2.zero;
        int validEnemyCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = separationHits[i];

            if (hit == null)
                continue;

            if (hit.attachedRigidbody == rb)
                continue;

            EnemyFollow otherEnemy =
                hit.GetComponentInParent<EnemyFollow>();

            if (otherEnemy == null || otherEnemy == this)
                continue;

            Vector2 awayDirection =
                rb.position -
                (Vector2)otherEnemy.transform.position;

            float sqrDistance = awayDirection.sqrMagnitude;

            if (sqrDistance <= 0.001f)
            {
                awayDirection = Random.insideUnitCircle.normalized;
                sqrDistance = 0.001f;
            }

            float distance = Mathf.Sqrt(sqrDistance);

            float proximityStrength = 1f -
                Mathf.Clamp01(distance / separationRadius);

            separationDirection +=
                awayDirection.normalized * proximityStrength;

            validEnemyCount++;
        }

        if (validEnemyCount <= 0)
            return Vector2.zero;

        separationDirection /= validEnemyCount;

        return Vector2.ClampMagnitude(
            separationDirection,
            1f
        );
    }

    private void Move(
        Vector2 direction,
        Vector2 targetDirection,
        float distanceToTarget
    )
    {
        float finalSpeed = GetEffectiveSpeed();

        if (distanceToTarget < closeRangeDistance)
        {
            float closeRangeT = Mathf.InverseLerp(
                0f,
                closeRangeDistance,
                distanceToTarget
            );

            float speedMultiplier = Mathf.Lerp(
                closeRangeSpeedMultiplier,
                1f,
                closeRangeT
            );

            finalSpeed *= speedMultiplier;
        }

        float movementDistance =
            finalSpeed * Time.fixedDeltaTime;

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
                    0.03f
                );

            rb.MovePosition(
                rb.position +
                overlapDirection * recoveryDistance
            );

            return;
        }

        Vector2 steeredDirection =
            EnemyObstacleSteering2D.GetSteeredDirection(
                col,
                direction,
                targetDirection,
                navigationFilter,
                avoidanceHits,
                obstacleProbeDistance,
                movementDistance,
                0.03f,
                obstacleAvoidanceAttempts,
                obstacleOutwardBias,
                ref obstacleAvoidanceSide
            );

        if (steeredDirection.sqrMagnitude <= 0.001f)
            return;

        EnemyObstacleSteering2D.MoveWithPhysicsSlide(
            rb,
            col,
            steeredDirection * finalSpeed,
            Time.fixedDeltaTime,
            navigationFilter,
            5
        );
    }

    private void HandleStuckCheck(float distanceToTarget)
    {
        if (distanceToTarget <= 0.15f)
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
            (rb.position - lastPosition).sqrMagnitude;

        float stuckSqrDistance =
            stuckDistance * stuckDistance;

        if (movedSqrDistance < stuckSqrDistance)
        {
            Vector2 escapeDirection = GetEscapeDirection();

            if (escapeDirection != Vector2.zero)
            {
                rb.MovePosition(
                    rb.position +
                    escapeDirection *
                    GetEffectiveSpeed() *
                    escapeSpeedMultiplier *
                    Time.fixedDeltaTime
                );

                unstuckDirection =
                    Random.Range(0, 2) == 0 ? -1 : 1;

                unstuckTimer = unstuckDuration;
            }
        }

        lastPosition = rb.position;
        stuckTimer = 0f;
    }

    private Vector2 GetEscapeDirection()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(
            EnemyObstacleSteering2D.BuildNavigationMask(obstacleLayer)
        );
        filter.useLayerMask = true;
        filter.useTriggers = false;

        int hitCount = Physics2D.OverlapCircle(
            rb.position,
            escapeCheckRadius,
            filter,
            escapeHits
        );

        if (hitCount <= 0)
            return Vector2.zero;

        Vector2 escapeDirection = Vector2.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = escapeHits[i];

            if (hit == null)
                continue;

            Vector2 closestPoint =
                hit.ClosestPoint(rb.position);

            Vector2 awayFromObstacle =
                rb.position - closestPoint;

            if (awayFromObstacle.sqrMagnitude <= 0.001f)
            {
                awayFromObstacle =
                    rb.position -
                    (Vector2)hit.bounds.center;
            }

            if (awayFromObstacle.sqrMagnitude > 0.001f)
            {
                escapeDirection +=
                    awayFromObstacle.normalized;
            }
        }

        if (escapeDirection.sqrMagnitude <= 0.001f)
            return Vector2.zero;

        return escapeDirection.normalized;
    }

    private void ResetStuckCheck()
    {
        stuckTimer = 0f;
        lastPosition = rb.position;
    }

    private void FlipSprite(Vector2 direction)
    {
        if (isSpawning)
            return;

        Vector3 scale = transform.localScale;
        float absX = Mathf.Abs(scale.x);

        if (absX <= 0.001f)
            absX = Mathf.Abs(spawnTargetScale.x);

        if (direction.x > 0.01f)
            scale.x = absX;
        else if (direction.x < -0.01f)
            scale.x = -absX;

        transform.localScale = scale;
    }


    public void SetBeaconSpeedMultiplier(float multiplier)
    {
        beaconSpeedMultiplier = Mathf.Max(1f, multiplier);
    }

    public float GetEffectiveSpeed()
    {
        return Mathf.Min(
            Mathf.Max(0f, speed) * beaconSpeedMultiplier,
            Mathf.Max(0f, maxSpeed)
        );
    }

    private void IncreaseSpeed()
    {
        speed += speedIncreaseRate * Time.fixedDeltaTime;
        speed = Mathf.Clamp(speed, 0f, maxSpeed);
    }

    private void StopEnemy()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }
}