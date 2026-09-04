using System.Collections.Generic;
using UnityEngine;

public class BeaconEnemySpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject beaconEnemyPrefab;

    [Header("Level Control")]
    [Tooltip("Maximum number of Beacon enemies alive at the same time.")]
    [Min(0)] public int maxBeaconCount = 1;

    [Header("Spawn Time")]
    [Min(0f)] public float minSpawnTime = 1f;
    [Min(0f)] public float maxSpawnTime = 2f;
    [Tooltip("Delay before a destroyed Beacon is replaced.")]
    [Min(0f)] public float respawnDelay = 6f;

    [Header("Beacon Buff")]
    [Min(0.1f)] public float buffDuration = 15f;
    [Min(0.1f)] public float buffSizeMultiplier = 1.25f;
    [Min(0.1f)] public float normalSpeedMultiplier = 1.2f;
    [HideInInspector] public float normalMaxSpeedMultiplier = 1f;
    [Min(0.1f)] public float projectileMoveMultiplier = 1.2f;
    [Min(0.1f)] public float projectileShotMultiplier = 1.25f;
    [Min(0.1f)] public float projectileFireMultiplier = 1.25f;
    [Min(0.1f)] public float hunterRepositionMultiplier = 0.8f;
    [Min(0.1f)] public float hunterWarningMultiplier = 0.8f;
    [Min(0.1f)] public float hunterChargeMultiplier = 1.25f;
    [Min(0.1f)] public float hunterStunMultiplier = 0.8f;

    [Header("Beacon Behaviour")]
    [Min(0f)] public float activationDelay = 3f;
    [Min(0.05f)] public float pulseInterval = 2f;
    [Min(0.05f)] public float retargetInterval = 0.5f;
    [Min(0f)] public float targetStopDistance = 1.5f;
    [Min(0f)] public float moveSpeed = 3f;
    [Min(0f)] public float safeDistanceFromPlayer = 5f;
    [Min(0f)] public float wanderStrength = 0.6f;

    [Header("Spawn Rules")]
    [Min(0f)] public float spawnPadding = 1f;
    [Min(0f)] public float checkRadius = 0.8f;
    [Min(0f)] public float minDistanceFromPlayer = 5f;
    [Min(1)] public int maxAttempts = 50;

    [Header("References")]
    public Transform player;
    public PlayerMovement playerMovement;

    private readonly HashSet<BeaconEnemy> activeBeacons = new HashSet<BeaconEnemy>();
    private float timer;
    private bool initialized;
    private int wallLayerIndex;
    private int obstacleLayerIndex;
    private ContactFilter2D spawnFilter;
    private readonly Collider2D[] spawnHits = new Collider2D[32];

    private void Awake()
    {
        RefreshPlayerReferences();
        wallLayerIndex = LayerMask.NameToLayer("Wall");
        obstacleLayerIndex = LayerMask.NameToLayer("Obstacle");
        spawnFilter = ContactFilter2D.noFilter;
        spawnFilter.useTriggers = true;
    }

    private void Start()
    {
        if (!initialized)
            InitializeSpawner();
    }

    private void Update()
    {
        if (!initialized || !GameStateManager.IsGameplayStarted || maxBeaconCount <= 0)
            return;

        if (playerMovement != null && playerMovement.IsGameOver)
            return;

        CleanupActiveBeacons();
        if (activeBeacons.Count >= maxBeaconCount)
            return;

        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        if (TrySpawnBeacon())
            ResetInitialTimer();
        else
            timer = 0.3f;
    }

    public void ApplyLevelSettings(int count, float minTime, float maxTime)
    {
        maxBeaconCount = Mathf.Max(0, count);
        minSpawnTime = Mathf.Max(0f, minTime);
        maxSpawnTime = Mathf.Max(minSpawnTime, maxTime);
        InitializeSpawner();
    }

    public void ApplyDangerSettings(BeaconEnemyDangerSettings settings)
    {
        if (settings == null)
            return;

        settings.Sanitize();
        activationDelay = settings.activationDelay;
        pulseInterval = settings.pulseInterval;
        retargetInterval = settings.retargetInterval;
        targetStopDistance = settings.targetStopDistance;
        buffDuration = settings.buffDuration;
        buffSizeMultiplier = settings.buffSizeMultiplier;
        normalSpeedMultiplier = settings.normalSpeedMultiplier;
        normalMaxSpeedMultiplier = 1f;
        projectileMoveMultiplier = settings.projectileMoveMultiplier;
        projectileShotMultiplier = settings.projectileShotMultiplier;
        projectileFireMultiplier = settings.projectileFireMultiplier;
        hunterRepositionMultiplier = settings.hunterRepositionMultiplier;
        hunterWarningMultiplier = settings.hunterWarningMultiplier;
        hunterChargeMultiplier = settings.hunterChargeMultiplier;
        hunterStunMultiplier = settings.hunterStunMultiplier;
        moveSpeed = settings.moveSpeed;
        safeDistanceFromPlayer = settings.safeDistanceFromPlayer;
        wanderStrength = settings.wanderStrength;
        respawnDelay = settings.respawnDelay;
    }

    public void NotifyBeaconDestroyed(BeaconEnemy beacon)
    {
        if (beacon != null)
            activeBeacons.Remove(beacon);

        if (!initialized || maxBeaconCount <= 0)
            return;

        timer = respawnDelay;
    }

    private void InitializeSpawner()
    {
        initialized = true;
        CleanupActiveBeacons();
        enabled = maxBeaconCount > 0;
        if (!enabled)
            return;

        RefreshPlayerReferences();
        ResetInitialTimer();
    }

    private void ResetInitialTimer()
    {
        timer = Random.Range(minSpawnTime, maxSpawnTime);
    }

    private void CleanupActiveBeacons()
    {
        activeBeacons.RemoveWhere(beacon => beacon == null);
    }

    private bool TrySpawnBeacon()
    {
        if (beaconEnemyPrefab == null || CameraWorldBounds.Instance == null)
            return false;

        if (!TryGetSpawnPosition(out Vector2 spawnPosition))
            return false;

        GameObject beaconObject = Instantiate(beaconEnemyPrefab, spawnPosition, Quaternion.identity);
        BeaconEnemy beaconEnemy = beaconObject.GetComponent<BeaconEnemy>();

        if (beaconEnemy == null)
        {
            Debug.LogError($"Beacon prefabında BeaconEnemy componenti bulunamadı: {beaconEnemyPrefab.name}", beaconEnemyPrefab);
            Destroy(beaconObject);
            return false;
        }

        ConfigureBeacon(beaconEnemy);
        beaconEnemy.SetSpawnerOwner(this);
        activeBeacons.Add(beaconEnemy);
        return true;
    }

    private void ConfigureBeacon(BeaconEnemy beaconEnemy)
    {
        beaconEnemy.player = player;
        beaconEnemy.playerMovement = playerMovement;
        beaconEnemy.activationDelay = activationDelay;
        beaconEnemy.pulseInterval = pulseInterval;
        beaconEnemy.retargetInterval = retargetInterval;
        beaconEnemy.targetStopDistance = targetStopDistance;
        beaconEnemy.moveSpeed = moveSpeed;
        beaconEnemy.safeDistanceFromPlayer = safeDistanceFromPlayer;
        beaconEnemy.wanderStrength = wanderStrength;
        beaconEnemy.buffDuration = buffDuration;
        beaconEnemy.buffSizeMultiplier = buffSizeMultiplier;
        beaconEnemy.normalSpeedMultiplier = normalSpeedMultiplier;
        beaconEnemy.normalMaxSpeedMultiplier = 1f;
        beaconEnemy.projectileMoveMultiplier = projectileMoveMultiplier;
        beaconEnemy.projectileShotMultiplier = projectileShotMultiplier;
        beaconEnemy.projectileFireMultiplier = projectileFireMultiplier;
        beaconEnemy.hunterRepositionMultiplier = hunterRepositionMultiplier;
        beaconEnemy.hunterWarningMultiplier = hunterWarningMultiplier;
        beaconEnemy.hunterChargeMultiplier = hunterChargeMultiplier;
        beaconEnemy.hunterStunMultiplier = hunterStunMultiplier;
    }

    private bool TryGetSpawnPosition(out Vector2 spawnPosition)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            spawnPosition = CameraWorldBounds.Instance.RandomPointInside(spawnPadding);
            if (IsValidPosition(spawnPosition))
                return true;
        }

        spawnPosition = Vector2.zero;
        return false;
    }

    private bool IsValidPosition(Vector2 position)
    {
        if (player != null && Vector2.Distance(player.position, position) < minDistanceFromPlayer)
            return false;

        int hitCount = Physics2D.OverlapCircle(position, checkRadius, spawnFilter, spawnHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = spawnHits[i];
            if (hit != null && BlocksBeaconSpawn(hit))
                return false;
        }

        return true;
    }

    private bool BlocksBeaconSpawn(Collider2D hit)
    {
        if (hit.CompareTag("Player") || hit.CompareTag("Enemy") ||
            hit.CompareTag("Coin") || hit.CompareTag("PowerUp") || hit.CompareTag("Bomb"))
            return true;

        int layer = hit.gameObject.layer;
        return layer == wallLayerIndex || layer == obstacleLayerIndex;
    }

    private void RefreshPlayerReferences()
    {
        if (playerMovement == null)
            playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (player == null && playerMovement != null)
            player = playerMovement.transform;
    }

    private void OnValidate()
    {
        maxBeaconCount = Mathf.Max(0, maxBeaconCount);
        minSpawnTime = Mathf.Max(0f, minSpawnTime);
        maxSpawnTime = Mathf.Max(minSpawnTime, maxSpawnTime);
        respawnDelay = Mathf.Max(0f, respawnDelay);
        buffDuration = Mathf.Max(0.1f, buffDuration);
        buffSizeMultiplier = Mathf.Max(0.1f, buffSizeMultiplier);
        normalSpeedMultiplier = Mathf.Max(1f, normalSpeedMultiplier);
        normalMaxSpeedMultiplier = 1f;
        projectileMoveMultiplier = Mathf.Max(0.1f, projectileMoveMultiplier);
        projectileShotMultiplier = Mathf.Max(0.1f, projectileShotMultiplier);
        projectileFireMultiplier = Mathf.Max(0.1f, projectileFireMultiplier);
        hunterRepositionMultiplier = Mathf.Max(0.1f, hunterRepositionMultiplier);
        hunterWarningMultiplier = Mathf.Max(0.1f, hunterWarningMultiplier);
        hunterChargeMultiplier = Mathf.Max(0.1f, hunterChargeMultiplier);
        hunterStunMultiplier = Mathf.Max(0.1f, hunterStunMultiplier);
        activationDelay = Mathf.Max(0f, activationDelay);
        pulseInterval = Mathf.Max(0.05f, pulseInterval);
        retargetInterval = Mathf.Max(0.05f, retargetInterval);
        targetStopDistance = Mathf.Max(0f, targetStopDistance);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        safeDistanceFromPlayer = Mathf.Max(0f, safeDistanceFromPlayer);
        wanderStrength = Mathf.Max(0f, wanderStrength);
        spawnPadding = Mathf.Max(0f, spawnPadding);
        checkRadius = Mathf.Max(0f, checkRadius);
        minDistanceFromPlayer = Mathf.Max(0f, minDistanceFromPlayer);
        maxAttempts = Mathf.Max(1, maxAttempts);
    }
}
