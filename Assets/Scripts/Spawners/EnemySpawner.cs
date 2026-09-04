using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    private const float FailedSpawnRetryDelay = 0.25f;

    [Header("References")]
    public Transform player;
    public PlayerMovement playerMovement;

    [Header("Prefabs")]
    public GameObject normalEnemyPrefab;
    public GameObject projectileEnemyPrefab;
    public GameObject hunterEnemyPrefab;
    public GameObject bossPrefab;

    [Header("Normal Enemy Spawn")]
    [Min(0)]
    public int normalEnemyCount;

    [Min(0f)]
    public float normalEnemySpawnInterval = 2.5f;

    [Header("Projectile Enemy Spawn")]
    [Min(0)]
    public int projectileEnemyCount;

    [Min(0f)]
    public float projectileEnemySpawnInterval = 5f;

    [Header("Hunter Enemy Spawn")]
    [Min(0)]
    public int hunterEnemyCount;

    [Min(0f)]
    public float hunterEnemySpawnInterval = 8f;

    [Header("Normal Enemy Settings")]
    [Min(0f)]
    public float normalMinStartSpeed = 1.5f;

    [Min(0f)]
    public float normalMaxStartSpeed = 2.5f;

    [Min(0f)]
    public float normalMaxSpeed = 7f;

    [Min(0f)]
    public float normalSpeedIncreaseRate = 0.1f;

    [Header("Normal Enemy AI")]
    public bool normalPredictionEnabled = true;

    [Min(0f)]
    public float normalPredictionDistanceThreshold = 2.5f;

    [Min(0f)]
    public float normalPredictionTime = 0.25f;

    [Min(0f)]
    public float normalMaxPredictionDistance = 1.5f;

    public bool normalSeparationEnabled = true;

    [Min(0f)]
    public float normalSeparationRadius = 0.75f;

    [Min(0f)]
    public float normalSeparationStrength = 0.65f;

    [Header("Projectile Enemy Settings")]
    [Min(0f)]
    public float projectileMoveSpeed = 3f;

    [Min(0f)]
    public float projectileStoppingDistance = 7f;

    [Min(0f)]
    public float projectileRetreatDistance = 4f;

    [Min(0.01f)]
    public float projectileFireRate = 1.5f;

    [Min(0f)]
    public float projectileSpeed = 6f;

    [Header("Projectile Enemy Burst / Reload")]
    [Min(1)]
    public int projectileShotsPerBurst = 3;

    [Min(0.05f)]
    public float projectileReloadDuration = 3f;

    [Min(0f)]
    public float projectileReloadRetreatDistance = 9f;

    [Min(0.1f)]
    public float projectileReloadMoveSpeedMultiplier = 1.4f;

    [Header("Projectile Enemy AI")]
    public bool projectileStrafeEnabled = true;

    [Min(0f)]
    public float projectileStrafeSpeedMultiplier = 0.65f;

    [Min(0f)]
    public float projectileStrafeDirectionChangeMinTime = 1.5f;

    [Min(0f)]
    public float projectileStrafeDirectionChangeMaxTime = 3f;

    [Min(0f)]
    public float projectileStrafeDistanceTolerance = 0.6f;

    public bool projectilePredictiveAimEnabled = true;

    [Min(0f)]
    public float projectilePredictionTime = 0.25f;

    [Min(0f)]
    public float projectileMaxPredictionDistance = 1.5f;

    [Min(0f)]
    public float projectilePredictionDistanceThreshold = 2.5f;

    public bool projectileSeparationEnabled = true;

    [Min(0f)]
    public float projectileSeparationRadius = 0.9f;

    [Min(0f)]
    public float projectileSeparationStrength = 0.45f;

    [Header("Hunter Enemy Settings")]
    [Min(0f)]
    public float hunterPrepareDistance = 6f;

    [Min(0f)]
    public float hunterRepositionTime = 1.2f;

    [Min(0f)]
    public float hunterRecoveryTime = 1.2f;

    [Min(0f)]
    public float hunterWarningDuration = 1f;

    [Min(0f)]
    public float hunterChargeSpeed = 15f;

    [Min(0.01f)]
    public float hunterMaxChargeTime = 0.65f;

    [Min(0f)]
    public float hunterStunDuration = 1f;

    [Header("Boss Settings")]
    public bool bossEnabled;

    public BossSpawnCondition bossSpawnCondition =
        BossSpawnCondition.Score;

    [Min(0)]
    public int bossSpawnScore = 75;

    [Min(0f)]
    public float bossSpawnTime = 30f;

    [Min(0f)]
    public float bossSpeed = 1.2f;

    [Min(0f)]
    public float bossDirectionSmoothness = 7f;

    public bool bossCanSplit = true;

    [Min(0f)]
    public float bossSplitDelay = 0.8f;

    [Min(0f)]
    public float bossSplitDistance = 1.2f;

    [Min(0f)]
    public float miniBossSpeed = 2.5f;

    [Header("Spawn Area")]
    [Min(0f)]
    public float minDistanceFromPlayer = 3f;

    [Min(0f)]
    public float edgeOffset = 0.8f;

    [Header("Fair Spawn Protection")]
    [Tooltip("No enemy may appear closer than this, even if an old scene value is lower.")]
    [Min(0f)] public float absolutePlayerSafeDistance = 4.5f;
    [Tooltip("Reject edge spawns inside the player's immediate forward path.")]
    [Min(0f)] public float forwardSpawnBlockDistance = 8f;
    [Range(-1f, 1f)] public float forwardSpawnDotThreshold = 0.3f;
    [Min(0f)] public float playerPredictionTime = 0.75f;
    [Min(0f)] public float predictedPositionSafeDistance = 4f;

    [Header("Obstacle Check")]
    public LayerMask obstacleLayer;

    [Min(0f)]
    public float spawnCheckRadius = 0.7f;

    [Min(1)]
    public int maxSpawnAttempts = 30;

    private float normalSpawnTimer;
    private float projectileSpawnTimer;
    private float hunterSpawnTimer;

    private float normalFirstSpawnDelay;
    private float projectileFirstSpawnDelay;
    private float hunterFirstSpawnDelay;

    private bool bossSpawned;

    private int spawnedNormal;
    private int spawnedProjectile;
    private int spawnedHunter;

    private ContactFilter2D obstacleFilter;
    private ContactFilter2D fullSpawnFilter;

    private Rigidbody2D playerBody;
    private int wallLayerIndex;
    private int obstacleLayerIndex;

    private readonly Collider2D[] spawnCheckHits =
        new Collider2D[16];

    private readonly List<GameObject> activeEnemies =
        new List<GameObject>(32);

    private void Awake()
    {
        wallLayerIndex = LayerMask.NameToLayer("Wall");
        obstacleLayerIndex = LayerMask.NameToLayer("Obstacle");

        fullSpawnFilter = ContactFilter2D.noFilter;
        fullSpawnFilter.useTriggers = true;

        RefreshPlayerReferences();
        RefreshObstacleFilter();

        normalFirstSpawnDelay = normalEnemySpawnInterval;
        projectileFirstSpawnDelay = projectileEnemySpawnInterval;
        hunterFirstSpawnDelay = hunterEnemySpawnInterval;
    }

    private void Update()
    {
        if (!GameStateManager.IsGameplayStarted)
            return;

        if (playerMovement == null)
        {
            RefreshPlayerReferences();

            if (playerMovement == null)
                return;
        }

        if (playerMovement.IsGameOver)
            return;

        HandleNormalEnemySpawn();
        HandleProjectileEnemySpawn();
        HandleHunterEnemySpawn();
    }

    public void ApplyNormalDanger(
        NormalEnemyDangerSettings settings)
    {
        if (settings == null)
            return;

        settings.Sanitize();

        normalMinStartSpeed = settings.minStartSpeed;
        normalMaxStartSpeed = settings.maxStartSpeed;
        normalMaxSpeed = settings.maxSpeed;
        normalSpeedIncreaseRate = settings.speedIncreaseRate;
        normalPredictionEnabled = settings.predictionEnabled;
        normalPredictionDistanceThreshold = settings.predictionDistanceThreshold;
        normalPredictionTime = settings.predictionTime;
        normalMaxPredictionDistance = settings.maxPredictionDistance;
        normalSeparationEnabled = settings.separationEnabled;
        normalSeparationRadius = settings.separationRadius;
        normalSeparationStrength = settings.separationStrength;
    }

    public void ApplyProjectileDanger(
        ProjectileEnemyDangerSettings settings)
    {
        if (settings == null)
            return;

        settings.Sanitize();

        projectileMoveSpeed = settings.moveSpeed;
        projectileStoppingDistance = settings.stoppingDistance;
        projectileRetreatDistance = settings.retreatDistance;
        projectileFireRate = settings.fireRate;
        projectileSpeed = settings.projectileSpeed;
        projectileShotsPerBurst = settings.shotsPerBurst;
        projectileReloadDuration = settings.reloadDuration;
        projectileReloadRetreatDistance = settings.reloadRetreatDistance;
        projectileReloadMoveSpeedMultiplier = settings.reloadMoveSpeedMultiplier;
        projectileStrafeEnabled = settings.strafeEnabled;
        projectileStrafeSpeedMultiplier = settings.strafeSpeedMultiplier;
        projectileStrafeDirectionChangeMinTime = settings.strafeDirectionChangeMinTime;
        projectileStrafeDirectionChangeMaxTime = settings.strafeDirectionChangeMaxTime;
        projectileStrafeDistanceTolerance = settings.strafeDistanceTolerance;
        projectilePredictiveAimEnabled = settings.predictiveAimEnabled;
        projectilePredictionTime = settings.predictionTime;
        projectileMaxPredictionDistance = settings.maxPredictionDistance;
        projectilePredictionDistanceThreshold = settings.predictionDistanceThreshold;
        projectileSeparationEnabled = settings.separationEnabled;
        projectileSeparationRadius = settings.separationRadius;
        projectileSeparationStrength = settings.separationStrength;
    }

    public void ApplyHunterDanger(
        HunterEnemyDangerSettings settings)
    {
        if (settings == null)
            return;

        settings.Sanitize();

        hunterPrepareDistance = settings.prepareDistance;
        hunterRepositionTime = settings.repositionTime;
        hunterRecoveryTime = settings.recoveryTime;
        hunterWarningDuration = settings.warningDuration;
        hunterChargeSpeed = settings.chargeSpeed;
        hunterMaxChargeTime = settings.maxChargeTime;
        hunterStunDuration = settings.stunDuration;
    }

    public void ApplyBossDanger(BossDangerSettings settings)
    {
        if (settings == null)
            return;

        settings.Sanitize();

        bossSpeed = settings.speed;
        bossDirectionSmoothness = settings.directionSmoothness;
        bossCanSplit = settings.canSplit;
        bossSplitDelay = settings.splitDelay;
        bossSplitDistance = settings.splitDistance;
        miniBossSpeed = settings.miniBossSpeed;
    }

    public void ConfigureSpawnPacing(LevelConfig level)
    {
        normalFirstSpawnDelay = normalEnemySpawnInterval;
        projectileFirstSpawnDelay = projectileEnemySpawnInterval;
        hunterFirstSpawnDelay = hunterEnemySpawnInterval;

        if (level == null ||
            (level.winCondition != WinConditionType.SurviveTime &&
             level.winCondition != WinConditionType.ReachScoreWithinTime))
        {
            return;
        }

        float missionTime =
            Mathf.Max(0.1f, level.SafeTimeLimit);

        // Time-based missions need pressure almost immediately. These values
        // affect only the first spawn; the configured repeat intervals remain intact.
        normalFirstSpawnDelay = Mathf.Min(
            normalEnemySpawnInterval,
            Mathf.Clamp(
                missionTime * 0.04f,
                1.25f,
                2.75f
            )
        );

        projectileFirstSpawnDelay = Mathf.Min(
            projectileEnemySpawnInterval,
            Mathf.Clamp(
                missionTime * 0.07f,
                2.5f,
                4.5f
            )
        );

        hunterFirstSpawnDelay = Mathf.Min(
            hunterEnemySpawnInterval,
            Mathf.Clamp(
                missionTime * 0.11f,
                4f,
                7f
            )
        );
    }

    public void ResetSpawner()
    {
        normalSpawnTimer = 0f;
        projectileSpawnTimer = 0f;
        hunterSpawnTimer = 0f;

        bossSpawned = false;

        spawnedNormal = 0;
        spawnedProjectile = 0;
        spawnedHunter = 0;

        /*
         * Sahnedeki canlı enemy referanslarını unutmuyoruz.
         * Yalnızca daha önce yok edilmiş olanları temizliyoruz.
         */
        RemoveDestroyedEnemies();

        RefreshPlayerReferences();
        RefreshObstacleFilter();
    }

    private void HandleNormalEnemySpawn()
    {
        // Once the Boss phase begins, Stalkers are intentionally removed
        // from the encounter and no new Stalker is allowed to spawn.
        if (bossSpawned)
            return;

        if (spawnedNormal >= normalEnemyCount)
            return;

        if (normalEnemyPrefab == null)
            return;

        float requiredDelay =
            spawnedNormal == 0
                ? normalFirstSpawnDelay
                : normalEnemySpawnInterval;

        normalSpawnTimer += Time.deltaTime;

        if (normalSpawnTimer < requiredDelay)
            return;

        if (TrySpawnNormalEnemy())
        {
            normalSpawnTimer = 0f;
        }
        else
        {
            normalSpawnTimer = Mathf.Max(
                0f,
                requiredDelay - FailedSpawnRetryDelay
            );
        }
    }

    private void HandleProjectileEnemySpawn()
    {
        if (spawnedProjectile >= projectileEnemyCount)
            return;

        if (projectileEnemyPrefab == null)
            return;

        float requiredDelay =
            spawnedProjectile == 0
                ? projectileFirstSpawnDelay
                : projectileEnemySpawnInterval;

        projectileSpawnTimer += Time.deltaTime;

        if (projectileSpawnTimer < requiredDelay)
            return;

        if (TrySpawnProjectileEnemy())
        {
            projectileSpawnTimer = 0f;
        }
        else
        {
            projectileSpawnTimer = Mathf.Max(
                0f,
                requiredDelay - FailedSpawnRetryDelay
            );
        }
    }

    private void HandleHunterEnemySpawn()
    {
        if (spawnedHunter >= hunterEnemyCount)
            return;

        if (hunterEnemyPrefab == null)
            return;

        float requiredDelay =
            spawnedHunter == 0
                ? hunterFirstSpawnDelay
                : hunterEnemySpawnInterval;

        hunterSpawnTimer += Time.deltaTime;

        if (hunterSpawnTimer < requiredDelay)
            return;

        if (TrySpawnHunterEnemy())
        {
            hunterSpawnTimer = 0f;
        }
        else
        {
            hunterSpawnTimer = Mathf.Max(
                0f,
                requiredDelay - FailedSpawnRetryDelay
            );
        }
    }

    private bool TrySpawnNormalEnemy()
    {
        if (bossSpawned)
            return false;

        if (!TryCreateEnemy(
                normalEnemyPrefab,
                out GameObject enemy))
        {
            return false;
        }

        spawnedNormal++;
        ConfigureSpawnedEnemy(enemy);
        ConfigureNormalPursuitRole(
            enemy,
            spawnedNormal
        );

        return true;
    }

    private void ConfigureNormalPursuitRole(
        GameObject enemy,
        int spawnSequence
    )
    {
        if (enemy == null)
            return;

        EnemyFollow normal =
            enemy.GetComponent<EnemyFollow>();

        if (normal == null)
            return;

        if (spawnSequence <= 1)
        {
            normal.ConfigurePursuitRole(
                NormalEnemyPursuitRole.Pursuer
            );

            return;
        }

        int flankSide =
            Random.value < 0.5f ? -1 : 1;

        if (normalEnemyCount >= 3)
        {
            NormalEnemyPursuitRole role =
                spawnSequence == 2
                    ? NormalEnemyPursuitRole.Interceptor
                    : NormalEnemyPursuitRole.Flanker;

            normal.ConfigurePursuitRole(
                role,
                flankSide
            );

            return;
        }

        // With only two Stalkers, alternate between cutting the player's
        // route and approaching from the side so runs do not feel identical.
        NormalEnemyPursuitRole secondRole =
            Random.value < 0.5f
                ? NormalEnemyPursuitRole.Interceptor
                : NormalEnemyPursuitRole.Flanker;

        normal.ConfigurePursuitRole(
            secondRole,
            flankSide
        );
    }

    private bool TrySpawnProjectileEnemy()
    {
        if (!TryCreateEnemy(
                projectileEnemyPrefab,
                out GameObject enemy))
        {
            return false;
        }

        spawnedProjectile++;
        ConfigureSpawnedEnemy(enemy);

        return true;
    }

    private bool TrySpawnHunterEnemy()
    {
        if (!TryCreateEnemy(
                hunterEnemyPrefab,
                out GameObject enemy))
        {
            return false;
        }

        spawnedHunter++;
        ConfigureSpawnedEnemy(enemy);

        return true;
    }

    private bool TryCreateEnemy(
        GameObject enemyPrefab,
        out GameObject enemy)
    {
        enemy = null;

        if (enemyPrefab == null)
            return false;

        if (!TryGetSafeSpawnPosition(
                out Vector2 spawnPosition))
        {
            return false;
        }

        enemy = Instantiate(
            enemyPrefab,
            spawnPosition,
            Quaternion.identity
        );

        activeEnemies.Add(enemy);
        return true;
    }

    private void ConfigureSpawnedEnemy(GameObject enemy)
    {
        if (enemy == null)
            return;

        ApplyEnemySettings(enemy);
        AssignPlayer(enemy);
        RefreshBuffTarget(enemy);
    }

    private void ApplyEnemySettings(GameObject enemy)
    {
        EnemyFollow normal =
            enemy.GetComponent<EnemyFollow>();

        if (normal != null)
        {
            normal.minStartSpeed =
                normalMinStartSpeed;

            normal.maxStartSpeed =
                normalMaxStartSpeed;

            normal.maxSpeed =
                normalMaxSpeed;

            normal.speedIncreaseRate =
                normalSpeedIncreaseRate;

            normal.predictionEnabled =
                normalPredictionEnabled;

            normal.predictionDistanceThreshold =
                normalPredictionDistanceThreshold;

            normal.predictionTime =
                normalPredictionTime;

            normal.maxPredictionDistance =
                normalMaxPredictionDistance;

            normal.separationEnabled =
                normalSeparationEnabled;

            normal.separationRadius =
                normalSeparationRadius;

            normal.separationStrength =
                normalSeparationStrength;
        }

        ProjectileEnemyFollow projectile =
            enemy.GetComponent<ProjectileEnemyFollow>();

        if (projectile != null)
        {
            projectile.moveSpeed =
                projectileMoveSpeed;

            projectile.stoppingDistance =
                projectileStoppingDistance;

            projectile.retreatDistance =
                projectileRetreatDistance;

            projectile.fireRate =
                projectileFireRate;

            projectile.projectileSpeed =
                projectileSpeed;

            projectile.shotsPerBurst =
                projectileShotsPerBurst;

            projectile.reloadDuration =
                projectileReloadDuration;

            projectile.reloadRetreatDistance =
                projectileReloadRetreatDistance;

            projectile.reloadMoveSpeedMultiplier =
                projectileReloadMoveSpeedMultiplier;

            projectile.strafeEnabled =
                projectileStrafeEnabled;

            projectile.strafeSpeedMultiplier =
                projectileStrafeSpeedMultiplier;

            projectile.strafeDirectionChangeMinTime =
                projectileStrafeDirectionChangeMinTime;

            projectile.strafeDirectionChangeMaxTime =
                projectileStrafeDirectionChangeMaxTime;

            projectile.strafeDistanceTolerance =
                projectileStrafeDistanceTolerance;

            projectile.predictiveAimEnabled =
                projectilePredictiveAimEnabled;

            projectile.predictionTime =
                projectilePredictionTime;

            projectile.maxPredictionDistance =
                projectileMaxPredictionDistance;

            projectile.predictionDistanceThreshold =
                projectilePredictionDistanceThreshold;

            projectile.separationEnabled =
                projectileSeparationEnabled;

            projectile.separationRadius =
                projectileSeparationRadius;

            projectile.separationStrength =
                projectileSeparationStrength;
        }

        HunterEnemyFollow hunter =
            enemy.GetComponent<HunterEnemyFollow>();

        if (hunter != null)
        {
            hunter.prepareDistance =
                hunterPrepareDistance;

            hunter.repositionTime =
                hunterRepositionTime;

            hunter.recoveryTime =
                hunterRecoveryTime;

            hunter.warningDuration =
                hunterWarningDuration;

            hunter.chargeSpeed =
                hunterChargeSpeed;

            hunter.maxChargeTime =
                hunterMaxChargeTime;

            hunter.stunDuration =
                hunterStunDuration;
        }

        BossEnemyFollow boss =
            enemy.GetComponent<BossEnemyFollow>();

        if (boss != null)
        {
            boss.speed =
                bossSpeed;

            boss.directionSmoothness =
                bossDirectionSmoothness;

            boss.canSplit =
                bossCanSplit;

            boss.splitDelay =
                bossSplitDelay;

            boss.splitDistance =
                bossSplitDistance;

            boss.miniBossSpeed =
                miniBossSpeed;
        }
    }

    private void AssignPlayer(GameObject enemy)
    {
        EnemyFollow normal =
            enemy.GetComponent<EnemyFollow>();

        if (normal != null)
            normal.player = player;

        ProjectileEnemyFollow projectile =
            enemy.GetComponent<ProjectileEnemyFollow>();

        if (projectile != null)
            projectile.player = player;

        HunterEnemyFollow hunter =
            enemy.GetComponent<HunterEnemyFollow>();

        if (hunter != null)
        {
            hunter.player = player;
            hunter.playerMovement = playerMovement;
        }

        BossEnemyFollow boss =
            enemy.GetComponent<BossEnemyFollow>();

        if (boss != null)
            boss.player = player;
    }

    private static void RefreshBuffTarget(
        GameObject enemy)
    {
        EnemyBuffTarget buffTarget =
            enemy.GetComponent<EnemyBuffTarget>();

        if (buffTarget != null)
            buffTarget.RefreshBaseValues();
    }

    private bool TryGetSafeSpawnPosition(
        out Vector2 spawnPosition)
    {
        spawnPosition = Vector2.zero;

        if (CameraWorldBounds.Instance == null)
            return false;

        if (player == null)
        {
            RefreshPlayerReferences();

            if (player == null)
                return false;
        }

        for (int attempt = 0;
             attempt < maxSpawnAttempts;
             attempt++)
        {
            spawnPosition = GetRandomEdgePosition();

            if (IsSafePosition(spawnPosition))
                return true;
        }

        return false;
    }

    private bool IsSafePosition(Vector2 spawnPosition)
    {
        if (player == null)
            return false;

        Vector2 playerPosition = player.position;
        float safeDistance = Mathf.Max(minDistanceFromPlayer, absolutePlayerSafeDistance);

        float safeDistanceSquared = safeDistance * safeDistance;

        if ((spawnPosition - playerPosition).sqrMagnitude < safeDistanceSquared)
            return false;

        Vector2 movementDirection = Vector2.zero;
        Vector2 playerVelocity = Vector2.zero;

        if (playerMovement != null)
        {
            movementDirection = playerMovement.CurrentMoveInput;
            if (movementDirection.sqrMagnitude <= 0.01f)
                movementDirection = playerMovement.LastMoveDirection;

            if (playerBody != null)
                playerVelocity = playerBody.linearVelocity;
        }

        if (movementDirection.sqrMagnitude > 0.01f)
        {
            movementDirection.Normalize();
            Vector2 toSpawn = spawnPosition - playerPosition;
            float distance = toSpawn.magnitude;

            if (distance <= forwardSpawnBlockDistance && distance > 0.01f &&
                Vector2.Dot(movementDirection, toSpawn / distance) >= forwardSpawnDotThreshold)
            {
                return false;
            }
        }

        Vector2 predictedPosition =
            playerPosition + playerVelocity * playerPredictionTime;

        float predictedSafeDistanceSquared =
            predictedPositionSafeDistance * predictedPositionSafeDistance;

        if ((spawnPosition - predictedPosition).sqrMagnitude <
            predictedSafeDistanceSquared)
        {
            return false;
        }

        // noFilter makes the safety check independent of a scene LayerMask mistake.
        int hitCount = Physics2D.OverlapCircle(
            spawnPosition,
            spawnCheckRadius,
            fullSpawnFilter,
            spawnCheckHits
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = spawnCheckHits[i];
            if (hit == null)
                continue;

            if (hit.CompareTag("Player") || hit.CompareTag("Enemy") ||
                hit.CompareTag("Coin") || hit.CompareTag("PowerUp") ||
                hit.CompareTag("Bomb"))
            {
                return false;
            }

            int layer = hit.gameObject.layer;

            if (layer == wallLayerIndex ||
                layer == obstacleLayerIndex)
            {
                return false;
            }
        }

        return true;
    }

    private Vector2 GetRandomEdgePosition()
    {
        CameraWorldBounds bounds =
            CameraWorldBounds.Instance;

        float minX = bounds.MinX + edgeOffset;
        float maxX = bounds.MaxX - edgeOffset;
        float minY = bounds.MinY + edgeOffset;
        float maxY = bounds.MaxY - edgeOffset;

        int side = Random.Range(0, 4);

        switch (side)
        {
            case 0:
                return new Vector2(
                    Random.Range(minX, maxX),
                    maxY
                );

            case 1:
                return new Vector2(
                    Random.Range(minX, maxX),
                    minY
                );

            case 2:
                return new Vector2(
                    minX,
                    Random.Range(minY, maxY)
                );

            default:
                return new Vector2(
                    maxX,
                    Random.Range(minY, maxY)
                );
        }
    }

    public void TrySpawnBoss(int currentScore)
    {
        if (bossSpawnCondition !=
            BossSpawnCondition.Score)
        {
            return;
        }

        if (currentScore < bossSpawnScore)
            return;

        TrySpawnBossInternal();
    }

    public void TrySpawnBossByTime(
        float elapsedTime)
    {
        if (bossSpawnCondition !=
            BossSpawnCondition.Time)
        {
            return;
        }

        if (elapsedTime < bossSpawnTime)
            return;

        TrySpawnBossInternal();
    }

    private void TrySpawnBossInternal()
    {
        if (!GameStateManager.IsGameplayStarted)
            return;

        if (!bossEnabled)
            return;

        if (bossSpawned)
            return;

        if (bossPrefab == null)
            return;

        if (!TryGetSafeSpawnPosition(
                out Vector2 spawnPosition))
        {
            return;
        }

        GameObject boss = Instantiate(
            bossPrefab,
            spawnPosition,
            Quaternion.identity
        );

        if (boss == null)
            return;

        bossSpawned = true;
        StatsManager.AddBossEncounter();
        activeEnemies.Add(boss);

        ConfigureSpawnedEnemy(boss);

    }

    private void RefreshPlayerReferences()
    {
        if (playerMovement == null)
        {
            playerMovement =
                FindAnyObjectByType<PlayerMovement>();
        }

        if (playerMovement != null)
        {
            player = playerMovement.transform;
            playerBody = playerMovement.GetComponent<Rigidbody2D>();
        }
        else
        {
            playerBody = null;
        }
    }

    private void RefreshObstacleFilter()
    {
        obstacleFilter = ContactFilter2D.noFilter;
        obstacleFilter.SetLayerMask(obstacleLayer);
        obstacleFilter.useTriggers = true;
    }

    private void RemoveDestroyedEnemies()
    {
        for (int i = activeEnemies.Count - 1;
             i >= 0;
             i--)
        {
            if (activeEnemies[i] == null)
                activeEnemies.RemoveAt(i);
        }
    }

    private void OnValidate()
    {
        normalEnemyCount =
            Mathf.Max(0, normalEnemyCount);

        projectileEnemyCount =
            Mathf.Max(0, projectileEnemyCount);

        hunterEnemyCount =
            Mathf.Max(0, hunterEnemyCount);

        normalEnemySpawnInterval =
            Mathf.Max(
                0f,
                normalEnemySpawnInterval
            );

        projectileEnemySpawnInterval =
            Mathf.Max(
                0f,
                projectileEnemySpawnInterval
            );

        hunterEnemySpawnInterval =
            Mathf.Max(
                0f,
                hunterEnemySpawnInterval
            );

        normalMinStartSpeed =
            Mathf.Max(0f, normalMinStartSpeed);

        normalMaxStartSpeed =
            Mathf.Max(
                normalMinStartSpeed,
                normalMaxStartSpeed
            );

        normalMaxSpeed =
            Mathf.Max(0f, normalMaxSpeed);

        normalSpeedIncreaseRate =
            Mathf.Max(
                0f,
                normalSpeedIncreaseRate
            );

        normalPredictionDistanceThreshold =
            Mathf.Max(0f, normalPredictionDistanceThreshold);

        normalPredictionTime =
            Mathf.Max(0f, normalPredictionTime);

        normalMaxPredictionDistance =
            Mathf.Max(0f, normalMaxPredictionDistance);

        normalSeparationRadius =
            Mathf.Max(0f, normalSeparationRadius);

        normalSeparationStrength =
            Mathf.Max(0f, normalSeparationStrength);

        projectileMoveSpeed =
            Mathf.Max(0f, projectileMoveSpeed);

        projectileStoppingDistance =
            Mathf.Max(
                0f,
                projectileStoppingDistance
            );

        projectileRetreatDistance =
            Mathf.Max(
                0f,
                projectileRetreatDistance
            );

        projectileFireRate =
            Mathf.Max(0.01f, projectileFireRate);

        projectileSpeed =
            Mathf.Max(0f, projectileSpeed);

        projectileStrafeSpeedMultiplier =
            Mathf.Max(0f, projectileStrafeSpeedMultiplier);

        projectileStrafeDirectionChangeMinTime =
            Mathf.Max(0f, projectileStrafeDirectionChangeMinTime);

        projectileStrafeDirectionChangeMaxTime =
            Mathf.Max(
                projectileStrafeDirectionChangeMinTime,
                projectileStrafeDirectionChangeMaxTime
            );

        projectileStrafeDistanceTolerance =
            Mathf.Max(0f, projectileStrafeDistanceTolerance);

        projectilePredictionTime =
            Mathf.Max(0f, projectilePredictionTime);

        projectileMaxPredictionDistance =
            Mathf.Max(0f, projectileMaxPredictionDistance);

        projectilePredictionDistanceThreshold =
            Mathf.Max(0f, projectilePredictionDistanceThreshold);

        projectileSeparationRadius =
            Mathf.Max(0f, projectileSeparationRadius);

        projectileSeparationStrength =
            Mathf.Max(0f, projectileSeparationStrength);

        hunterPrepareDistance =
            Mathf.Max(0f, hunterPrepareDistance);

        hunterRepositionTime =
            Mathf.Max(0f, hunterRepositionTime);

        hunterRecoveryTime =
            Mathf.Max(0f, hunterRecoveryTime);

        hunterWarningDuration =
            Mathf.Max(0f, hunterWarningDuration);

        hunterChargeSpeed =
            Mathf.Max(0f, hunterChargeSpeed);

        hunterMaxChargeTime =
            Mathf.Max(0.01f, hunterMaxChargeTime);

        hunterStunDuration =
            Mathf.Max(0f, hunterStunDuration);

        bossSpawnScore =
            Mathf.Max(0, bossSpawnScore);

        bossSpawnTime =
            Mathf.Max(0f, bossSpawnTime);

        bossSpeed =
            Mathf.Max(0f, bossSpeed);

        bossDirectionSmoothness =
            Mathf.Max(0f, bossDirectionSmoothness);

        bossSplitDelay =
            Mathf.Max(0f, bossSplitDelay);

        bossSplitDistance =
            Mathf.Max(0f, bossSplitDistance);

        miniBossSpeed =
            Mathf.Max(0f, miniBossSpeed);

        minDistanceFromPlayer =
            Mathf.Max(
                0f,
                minDistanceFromPlayer
            );

        edgeOffset =
            Mathf.Max(0f, edgeOffset);

        spawnCheckRadius =
            Mathf.Max(
                0f,
                spawnCheckRadius
            );

        maxSpawnAttempts =
            Mathf.Max(1, maxSpawnAttempts);

        absolutePlayerSafeDistance = Mathf.Max(0f, absolutePlayerSafeDistance);
        forwardSpawnBlockDistance = Mathf.Max(0f, forwardSpawnBlockDistance);
        playerPredictionTime = Mathf.Max(0f, playerPredictionTime);
        predictedPositionSafeDistance = Mathf.Max(0f, predictedPositionSafeDistance);

        if (Application.isPlaying)
            RefreshObstacleFilter();
    }
}