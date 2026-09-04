using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BeaconEnemy : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public PlayerMovement playerMovement;
    public SoundManager soundManager;

    [Header("Activation")]
    public float activationDelay = 3f;
    public GameObject activationWavePrefab;
    public GameObject loopWavePrefab;
    public float pulseInterval = 1f;

    [Header("Targeting")]
    public float retargetInterval = 0.5f;
    public float targetStopDistance = 1.5f;

    [Header("Spawner Buff Settings")]
    public float buffDuration = 15f;
    public float buffSizeMultiplier = 1.25f;
    public float projectileMoveMultiplier = 1.2f;
    public float projectileShotMultiplier = 1.25f;
    public float projectileFireMultiplier = 1.25f;
    public float hunterRepositionMultiplier = 0.8f;
    public float hunterWarningMultiplier = 0.8f;
    public float hunterChargeMultiplier = 1.25f;
    public float hunterStunMultiplier = 0.8f;

    [Header("Buff Values (Normal)")]
    public float normalSpeedMultiplier = 1.3f;
    public float normalMaxSpeedMultiplier = 1.2f;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float safeDistanceFromPlayer = 5f;
    public float wanderStrength = 0.6f;
    public float boundsPadding = 0.6f;

    [Header("Collision")]
    public LayerMask solidLayers;
    public float castSkin = 0.05f;
    public float obstacleProbeDistance = 0.8f;
    [Range(2, 8)] public int obstacleAvoidanceAttempts = 5;
    [Range(0f, 1f)] public float obstacleOutwardBias = 0.3f;

    [Header("Spawn Effect")]
    public float spawnEffectDuration = 0.15f;

    [Header("Stuck Fix")]
    public float stuckCheckTime = 0.5f;
    public float stuckDistance = 0.06f;
    public float unstuckDuration = 0.45f;
    public float unstuckSideForce = 1.5f;
    public float escapeCheckRadius = 1.2f;
    public float escapeSpeedMultiplier = 2.2f;

    [Header("Optimization")]
    public float targetCacheRefreshInterval = 1.5f;

    private Rigidbody2D rb;
    private Collider2D col;
    private Transform currentTarget;

    private Vector3 targetScale;
    private Vector2 wanderDir;
    private Vector2 lastPosition;

    private bool active;
    private bool dead;
    private bool isSpawning;

    private float retargetTimer;
    private float stuckTimer;
    private float unstuckTimer;
    private int unstuckDirection = 1;

    private readonly HashSet<EnemyBuffTarget> appliedBuffTargets =
        new HashSet<EnemyBuffTarget>();
    private BeaconEnemySpawner spawnerOwner;
    private bool spawnerNotified;

    private ContactFilter2D solidFilter;
    private ContactFilter2D escapeFilter;

    private WaitForSeconds activationWait;
    private WaitForSeconds pulseWait;

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[4];
    private readonly RaycastHit2D[] avoidanceHits = new RaycastHit2D[12];
    private readonly Collider2D[] escapeHits = new Collider2D[8];

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        EnemyObstacleSteering2D.ConfigureAIMovementBody(rb, true);

        solidFilter = new ContactFilter2D();
        solidFilter.SetLayerMask(
            EnemyObstacleSteering2D.BuildNavigationMask(solidLayers)
        );
        solidFilter.useTriggers = false;

        escapeFilter = new ContactFilter2D();
        escapeFilter.SetLayerMask(
            EnemyObstacleSteering2D.BuildNavigationMask(solidLayers)
        );
        escapeFilter.useTriggers = true;

        targetScale = transform.localScale;
        transform.localScale = Vector3.zero;

        wanderDir = Random.insideUnitCircle.normalized;
        if (wanderDir.sqrMagnitude <= 0.001f)
            wanderDir = Vector2.right;

        unstuckDirection = Random.Range(0, 2) == 0 ? -1 : 1;
    }

    private void Start()
    {
        if (player == null)
        {
            PlayerMovement pm = FindAnyObjectByType<PlayerMovement>();

            if (pm != null)
            {
                player = pm.transform;
                playerMovement = pm;
            }
        }

        if (soundManager == null)
            soundManager = FindAnyObjectByType<SoundManager>();

        lastPosition = rb.position;

        RuntimeObjectPool.Prewarm(activationWavePrefab, 1);
        RuntimeObjectPool.Prewarm(loopWavePrefab, 2);

        activationWait = new WaitForSeconds(Mathf.Max(0f, activationDelay));
        pulseWait = new WaitForSeconds(Mathf.Max(0.05f, pulseInterval));

        StartCoroutine(SpawnEffect());
        StartCoroutine(ActivationRoutine());
    }

    private void FixedUpdate()
    {
        if (dead) return;
        if (isSpawning) return;
        if (playerMovement != null && playerMovement.IsGameOver) return;
        if (player == null) return;

        HandleRetarget();
        MoveLogic();
        HandleStuckCheck();
    }

    private IEnumerator SpawnEffect()
    {
        isSpawning = true;

        float time = 0f;

        while (time < spawnEffectDuration)
        {
            time += Time.deltaTime;
            float t = time / spawnEffectDuration;

            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);

            yield return null;
        }

        transform.localScale = targetScale;
        isSpawning = false;
    }

    private IEnumerator ActivationRoutine()
    {
        yield return activationWait;

        if (dead) yield break;

        active = true;

        if (soundManager != null)
            soundManager.PlayBeaconActivationWaveSound(transform.position);

        if (activationWavePrefab != null)
        {
            GameObject wave = RuntimeObjectPool.Spawn(
                activationWavePrefab,
                transform.position,
                Quaternion.identity
            );
            BeaconPulseWave pulse = wave.GetComponent<BeaconPulseWave>();

            if (pulse != null)
                pulse.Initialize(this, false);
        }

        StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        while (active && !dead)
        {
            yield return pulseWait;

            if (dead) yield break;
            if (loopWavePrefab == null) continue;

            if (soundManager != null)
                soundManager.PlayBeaconLoopWaveSound(transform.position);

            GameObject wave = RuntimeObjectPool.Spawn(
                loopWavePrefab,
                transform.position,
                Quaternion.identity
            );
            BeaconPulseWave pulse = wave.GetComponent<BeaconPulseWave>();

            if (pulse != null)
                pulse.Initialize(this, true);
        }
    }

    private void HandleRetarget()
    {
        retargetTimer -= Time.fixedDeltaTime;

        if (retargetTimer > 0f) return;

        retargetTimer = retargetInterval;
        PickTarget();
    }

    private void PickTarget()
    {
        currentTarget = null;

        float bestSqrDistance = Mathf.Infinity;
        Vector2 beaconPos = rb.position;

        foreach (EnemyBuffTarget target in EnemyBuffTarget.ActiveTargets)
        {
            if (target == null) continue;
            if (target.IsBuffed) continue;
            if (!target.CanReceiveBeaconBuff) continue;

            float sqrDistance =
                ((Vector2)target.transform.position - beaconPos).sqrMagnitude;

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                currentTarget = target.transform;
            }
        }
    }

    private void MoveLogic()
    {
        Vector2 pos = rb.position;
        Vector2 dir;

        float playerSqrDistance = ((Vector2)player.position - pos).sqrMagnitude;
        float safeSqrDistance = safeDistanceFromPlayer * safeDistanceFromPlayer;

        if (playerSqrDistance < safeSqrDistance)
        {
            dir = (pos - (Vector2)player.position).normalized;
        }
        else if (unstuckTimer > 0f)
        {
            unstuckTimer -= Time.fixedDeltaTime;

            Vector2 baseDir = currentTarget != null
                ? ((Vector2)currentTarget.position - pos).normalized
                : wanderDir.normalized;

            Vector2 sideDir = new Vector2(-baseDir.y, baseDir.x) * unstuckDirection;
            dir = (baseDir + sideDir * unstuckSideForce).normalized;
        }
        else if (currentTarget != null)
        {
            float targetSqrDistance = ((Vector2)currentTarget.position - pos).sqrMagnitude;
            float stopSqrDistance = targetStopDistance * targetStopDistance;

            dir = targetSqrDistance > stopSqrDistance
                ? ((Vector2)currentTarget.position - pos).normalized
                : Vector2.zero;
        }
        else
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;

            if (randomDir.sqrMagnitude <= 0.001f)
                randomDir = wanderDir;

            wanderDir = Vector2.Lerp(wanderDir, randomDir, wanderStrength * Time.fixedDeltaTime);
            dir = wanderDir.normalized;
        }

        MoveWithCollision(dir);
    }

    private void MoveWithCollision(Vector2 dir)
    {
        Vector2 pos = rb.position;
        float movementDistance = moveSpeed * Time.fixedDeltaTime;

        if (EnemyObstacleSteering2D.TryGetOverlapRecovery(
                col,
                solidFilter,
                out Vector2 overlapDirection,
                out float penetrationDepth))
        {
            float recoveryDistance =
                EnemyObstacleSteering2D.GetOverlapRecoveryDistance(
                    penetrationDepth,
                    movementDistance,
                    castSkin
                );

            Vector2 recoveryPosition = ClampToBounds(
                pos + overlapDirection * recoveryDistance
            );

            rb.MovePosition(recoveryPosition);
            return;
        }

        if (dir.sqrMagnitude <= 0.001f)
            return;

        Vector2 steeredDirection =
            EnemyObstacleSteering2D.GetSteeredDirection(
                col,
                dir,
                dir,
                solidFilter,
                avoidanceHits,
                obstacleProbeDistance,
                movementDistance,
                castSkin,
                obstacleAvoidanceAttempts,
                obstacleOutwardBias,
                ref unstuckDirection
            );

        if (steeredDirection.sqrMagnitude <= 0.001f)
            return;

        Vector2 nextPos =
            EnemyObstacleSteering2D.CalculatePhysicsSlideTarget(
                rb,
                col,
                steeredDirection * moveSpeed,
                Time.fixedDeltaTime,
                solidFilter,
                5
            );

        nextPos = ClampToBounds(nextPos);

        Vector2 movement = nextPos - pos;

        if (movement.sqrMagnitude > 0.000001f)
        {
            rb.MovePosition(nextPos);
            return;
        }

        Vector2 sideDir =
            new Vector2(-steeredDirection.y, steeredDirection.x) *
            unstuckDirection;

        Vector2 sideMove =
            sideDir.normalized * movementDistance;

        if (CanMove(sideMove))
        {
            rb.MovePosition(pos + sideMove);
            return;
        }

        unstuckDirection *= -1;
    }

    private bool CanMove(Vector2 movement)
    {
        if (col == null) return true;
        if (movement.sqrMagnitude <= 0.001f) return true;

        int hitCount = col.Cast(
            movement.normalized,
            solidFilter,
            castHits,
            movement.magnitude + castSkin
        );

        return hitCount == 0;
    }

    private void HandleStuckCheck()
    {
        stuckTimer += Time.fixedDeltaTime;

        float effectiveStuckCheckTime = Mathf.Min(
            Mathf.Max(0.05f, stuckCheckTime),
            0.25f
        );

        if (stuckTimer < effectiveStuckCheckTime) return;

        float movedSqrDistance = (rb.position - lastPosition).sqrMagnitude;
        float stuckSqrDistance = stuckDistance * stuckDistance;

        if (movedSqrDistance < stuckSqrDistance)
        {
            Vector2 escapeDirection = GetEscapeDirection();

            if (escapeDirection == Vector2.zero)
            {
                unstuckDirection *= -1;
                escapeDirection = new Vector2(unstuckDirection, Random.Range(-0.5f, 0.5f)).normalized;
            }

            Vector2 movement = escapeDirection * moveSpeed * escapeSpeedMultiplier * Time.fixedDeltaTime;

            if (CanMove(movement))
                rb.MovePosition(rb.position + movement);

            unstuckTimer = unstuckDuration;
        }

        lastPosition = rb.position;
        stuckTimer = 0f;
    }

    private Vector2 GetEscapeDirection()
    {
        int hitCount = Physics2D.OverlapCircle(
            rb.position,
            escapeCheckRadius,
            escapeFilter,
            escapeHits
        );

        if (hitCount == 0)
            return Vector2.zero;

        Vector2 escapeDirection = Vector2.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = escapeHits[i];
            if (hit == null) continue;

            Vector2 awayFromObstacle = rb.position - (Vector2)hit.ClosestPoint(rb.position);

            if (awayFromObstacle.sqrMagnitude > 0.001f)
                escapeDirection += awayFromObstacle.normalized;
        }

        return escapeDirection.normalized;
    }

    private Vector2 ClampToBounds(Vector2 pos)
    {
        if (CameraWorldBounds.Instance == null) return pos;

        pos.x = Mathf.Clamp(pos.x, CameraWorldBounds.Instance.MinX + boundsPadding, CameraWorldBounds.Instance.MaxX - boundsPadding);
        pos.y = Mathf.Clamp(pos.y, CameraWorldBounds.Instance.MinY + boundsPadding, CameraWorldBounds.Instance.MaxY - boundsPadding);

        return pos;
    }

    public void SetSpawnerOwner(BeaconEnemySpawner owner)
    {
        spawnerOwner = owner;
    }

    public void ApplyBuffToTarget(GameObject targetObject)
    {
        if (targetObject == null || dead)
            return;

        EnemyBuffTarget target = targetObject.GetComponent<EnemyBuffTarget>();

        if (target == null)
            target = targetObject.GetComponentInParent<EnemyBuffTarget>();

        if (target == null)
            target = targetObject.AddComponent<EnemyBuffTarget>();

        if (!target.CanReceiveBeaconBuff || target.IsBuffed)
            return;

        target.buffDuration = buffDuration;
        target.ApplyBeaconBuff(
            this,
            buffSizeMultiplier,
            normalSpeedMultiplier,
            1f,
            projectileMoveMultiplier,
            projectileShotMultiplier,
            projectileFireMultiplier,
            hunterRepositionMultiplier,
            hunterWarningMultiplier,
            hunterChargeMultiplier,
            hunterStunMultiplier
        );

        if (target.BuffSource == this)
            appliedBuffTargets.Add(target);
    }

    private void ReleaseAppliedBuffs()
    {
        foreach (EnemyBuffTarget target in appliedBuffTargets)
        {
            if (target != null)
                target.RemoveBeaconBuff(this);
        }

        appliedBuffTargets.Clear();
    }

    private void NotifySpawnerOnce()
    {
        if (spawnerNotified)
            return;

        spawnerNotified = true;
        if (spawnerOwner != null)
            spawnerOwner.NotifyBeaconDestroyed(this);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDieFromDash(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDieFromDash(other.gameObject);
    }

    private void TryDieFromDash(GameObject other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerDash dash = other.GetComponent<PlayerDash>();
        if (dash == null)
            dash = other.GetComponentInParent<PlayerDash>();

        TryDieFromDash(dash);
    }

    public bool TryDieFromDash(PlayerDash dash)
    {
        if (dead || dash == null || !dash.IsDashing)
            return false;

        Die();
        return true;
    }

    private void Die()
    {
        if (dead) return;

        dead = true;
        active = false;

        StatsManager.AddBeaconDestroyed();

        ReleaseAppliedBuffs();
        NotifySpawnerOnce();

        if (soundManager != null)
            soundManager.PlayBeaconDeathSound(transform.position);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        ReleaseAppliedBuffs();
        NotifySpawnerOnce();
    }
}