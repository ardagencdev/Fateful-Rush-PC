using UnityEngine;

public class PowerUpSpawner : MonoBehaviour
{
    private const float FailedSpawnRetryDelay = 0.25f;

    [Header("Power Up Prefabs")]
    public GameObject slowPowerUpPrefab;
    public GameObject armorPowerUpPrefab;

    [Header("Enabled")]
    public bool slowEnabled = true;
    public bool armorEnabled = true;

    [Header("Slow Spawn")]
    [Min(0f)]
    public float slowMinSpawnTime = 8f;

    [Min(0f)]
    public float slowMaxSpawnTime = 20f;

    [Header("Armor Spawn")]
    [Min(0f)]
    public float armorMinSpawnTime = 15f;

    [Min(0f)]
    public float armorMaxSpawnTime = 30f;

    [Header("Slow Settings")]
    [Range(0.01f, 1f)]
    public float slowMultiplier = 0.4f;

    [Min(0.1f)]
    public float slowDuration = 5f;

    [Header("Spawn Rules")]
    [Min(0f)]
    public float spawnPadding = 0.8f;

    [Min(0f)]
    public float checkRadius = 0.8f;

    [Min(0f)]
    public float playerSafeDistance = 4f;

    [Min(1)]
    public int maxAttempts = 50;

    [Header("References")]
    public PlayerMovement playerMovement;

    private float slowTimer;
    private float armorTimer;

    private bool slowSpawned;
    private bool armorSpawned;

    private int wallLayerIndex;
    private int obstacleLayerIndex;

    private ContactFilter2D spawnFilter;

    private readonly Collider2D[] hits =
        new Collider2D[32];

    private void Awake()
    {
        RefreshPlayerReference();

        wallLayerIndex =
            LayerMask.NameToLayer("Wall");

        obstacleLayerIndex =
            LayerMask.NameToLayer("Obstacle");

        RefreshSpawnFilter();
    }

    private void Start()
    {
        ResetSpawner();
    }

    private void Update()
    {
        if (!GameStateManager.IsGameplayStarted)
            return;

        if (playerMovement == null)
            RefreshPlayerReference();

        if (playerMovement != null &&
            playerMovement.IsGameOver)
        {
            return;
        }

        HandleSlowSpawn();
        HandleArmorSpawn();
    }

    public void ApplyLevelSettings(
        bool slowOn,
        bool armorOn,
        float slowMin,
        float slowMax,
        float armorMin,
        float armorMax,
        float levelSlowMultiplier,
        float levelSlowDuration)
    {
        slowEnabled = slowOn;
        armorEnabled = armorOn;

        slowMinSpawnTime =
            Mathf.Max(0f, slowMin);

        slowMaxSpawnTime =
            Mathf.Max(
                slowMinSpawnTime,
                slowMax
            );

        armorMinSpawnTime =
            Mathf.Max(0f, armorMin);

        armorMaxSpawnTime =
            Mathf.Max(
                armorMinSpawnTime,
                armorMax
            );

        slowMultiplier =
            Mathf.Clamp(
                levelSlowMultiplier,
                0.01f,
                1f
            );

        slowDuration =
            Mathf.Max(
                0.1f,
                levelSlowDuration
            );

        ResetSpawner();
    }

    public void ResetSpawner()
    {
        slowSpawned = false;
        armorSpawned = false;

        slowTimer = Random.Range(
            slowMinSpawnTime,
            slowMaxSpawnTime
        );

        armorTimer = Random.Range(
            armorMinSpawnTime,
            armorMaxSpawnTime
        );

        RefreshPlayerReference();
        RefreshSpawnFilter();
    }

    private void HandleSlowSpawn()
    {
        if (!slowEnabled || slowSpawned)
            return;

        slowTimer -= Time.deltaTime;

        if (slowTimer > 0f)
            return;

        if (TrySpawnPowerUp(
                slowPowerUpPrefab,
                true))
        {
            slowSpawned = true;
        }
        else
        {
            slowTimer =
                FailedSpawnRetryDelay;
        }
    }

    private void HandleArmorSpawn()
    {
        if (!armorEnabled || armorSpawned)
            return;

        armorTimer -= Time.deltaTime;

        if (armorTimer > 0f)
            return;

        if (TrySpawnPowerUp(
                armorPowerUpPrefab,
                false))
        {
            armorSpawned = true;
        }
        else
        {
            armorTimer =
                FailedSpawnRetryDelay;
        }
    }

    private bool TrySpawnPowerUp(
        GameObject prefab,
        bool isSlow)
    {
        if (prefab == null)
            return false;

        if (CameraWorldBounds.Instance == null)
            return false;

        if (playerMovement == null)
            RefreshPlayerReference();

        // Safety guarantee: never spawn a power-up without a valid
        // player reference, otherwise the minimum-distance rule
        // could be bypassed.
        if (playerMovement == null)
            return false;

        for (int attempt = 0;
             attempt < maxAttempts;
             attempt++)
        {
            Vector2 spawnPosition =
                CameraWorldBounds.Instance
                    .RandomPointInside(
                        spawnPadding
                    );

            if (!IsFarEnoughFromPlayer(spawnPosition))
                continue;

            if (!IsAreaClear(spawnPosition))
                continue;

            GameObject powerUp = Instantiate(
                prefab,
                spawnPosition,
                Quaternion.identity
            );

            if (powerUp == null)
                return false;

            SpawnAreaRegistry.Register(
                powerUp,
                checkRadius
            );

            if (isSlow)
                ApplySlowSettings(powerUp);

            return true;
        }

        return false;
    }

    private void ApplySlowSettings(
        GameObject powerUp)
    {
        SlowPowerUp slowPowerUp =
            powerUp.GetComponent<SlowPowerUp>();

        if (slowPowerUp == null)
        {
            Debug.LogWarning(
                "Spawned slow power-up prefab " +
                "does not contain SlowPowerUp.",
                powerUp
            );

            return;
        }

        slowPowerUp.slowMultiplier =
            slowMultiplier;

        slowPowerUp.slowDuration =
            slowDuration;
    }


    private bool IsFarEnoughFromPlayer(Vector2 position)
    {
        if (playerMovement == null)
            return false;

        Vector2 playerPosition =
            playerMovement.transform.position;

        float safeDistance =
            Mathf.Max(0f, playerSafeDistance);

        return (position - playerPosition).sqrMagnitude >=
               safeDistance * safeDistance;
    }

    private bool IsAreaClear(Vector2 position)
    {
        if (!SpawnAreaRegistry.IsAreaFree(
                position,
                checkRadius))
        {
            return false;
        }

        int hitCount = Physics2D.OverlapCircle(
            position,
            checkRadius,
            spawnFilter,
            hits
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];

            if (hit == null)
                continue;

            if (IsBlocked(hit))
                return false;
        }

        return true;
    }

    private bool IsBlocked(Collider2D hit)
    {
        GameObject hitObject = hit.gameObject;

        return hit.CompareTag("Enemy") ||
               hit.CompareTag("Coin") ||
               hit.CompareTag("Player") ||
               hit.CompareTag("PowerUp") ||
               hit.CompareTag("Bomb") ||
               hitObject.layer == wallLayerIndex ||
               hitObject.layer == obstacleLayerIndex;
    }

    private void RefreshPlayerReference()
    {
        if (playerMovement == null)
        {
            playerMovement =
                FindAnyObjectByType<PlayerMovement>();
        }
    }

    private void RefreshSpawnFilter()
    {
        spawnFilter =
            ContactFilter2D.noFilter;

        spawnFilter.useTriggers = true;
    }

    private void OnValidate()
    {
        slowMinSpawnTime =
            Mathf.Max(
                0f,
                slowMinSpawnTime
            );

        slowMaxSpawnTime =
            Mathf.Max(
                slowMinSpawnTime,
                slowMaxSpawnTime
            );

        armorMinSpawnTime =
            Mathf.Max(
                0f,
                armorMinSpawnTime
            );

        armorMaxSpawnTime =
            Mathf.Max(
                armorMinSpawnTime,
                armorMaxSpawnTime
            );

        slowMultiplier =
            Mathf.Clamp(
                slowMultiplier,
                0.01f,
                1f
            );

        slowDuration =
            Mathf.Max(
                0.1f,
                slowDuration
            );

        spawnPadding =
            Mathf.Max(
                0f,
                spawnPadding
            );

        checkRadius =
            Mathf.Max(
                0f,
                checkRadius
            );

        playerSafeDistance =
            Mathf.Max(
                0f,
                playerSafeDistance
            );

        maxAttempts =
            Mathf.Max(
                1,
                maxAttempts
            );

        if (Application.isPlaying)
            RefreshSpawnFilter();
    }
}