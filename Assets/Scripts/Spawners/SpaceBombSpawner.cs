using System.Collections.Generic;
using UnityEngine;

public class SpaceBombSpawner : MonoBehaviour
{
    private const float FailedSpawnRetryDelay = 0.25f;
    private const float ForwardBlockWidth = 3.5f;

    [Header("Prefab")]
    public GameObject bombPrefab;

    [Header("Spawn Time")]
    [Min(0f)]
    public float minSpawnTime = 6f;

    [Min(0f)]
    public float maxSpawnTime = 14f;

    [Header("Spawn Rules")]
    [Min(0)]
    public int maxBombCount = 3;

    [Min(0f)]
    public float bombSpawnSafeTime = 0.35f;

    [Min(0f)]
    public float spawnPadding = 1.5f;

    [Min(0f)]
    public float checkRadius = 1f;

    [Min(1)]
    public int maxAttempts = 50;

    [Min(0f)]
    public float playerSafeDistance = 2.5f;

    [Min(0f)]
    public float playerForwardSafeDistance = 7f;

    [Range(0f, 1f)]
    public float forwardDotLimit = 0.45f;

    [Header("Game State")]
    public PlayerMovement playerMovement;

    private float timer;
    private float nextSpawnTime;

    private int wallLayerIndex;
    private int obstacleLayerIndex;

    private ContactFilter2D spawnFilter;

    private readonly Collider2D[] hits = new Collider2D[32];
    private readonly List<GameObject> activeBombs = new List<GameObject>(8);

    private void Awake()
    {
        RefreshPlayerReference();

        wallLayerIndex = LayerMask.NameToLayer("Wall");
        obstacleLayerIndex = LayerMask.NameToLayer("Obstacle");

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

        if (playerMovement != null && playerMovement.IsGameOver)
            return;

        CleanupActiveBombs();

        if (activeBombs.Count >= maxBombCount)
            return;

        timer += Time.deltaTime;

        if (timer < nextSpawnTime)
            return;

        if (TrySpawnBomb())
        {
            timer = 0f;
            SetNextSpawnTime();
        }
        else
        {
            timer = 0f;
            nextSpawnTime = FailedSpawnRetryDelay;
        }
    }

    public void ApplyDangerSettings(
        BombDangerSettings settings)
    {
        if (settings == null)
            return;

        settings.Sanitize();

        minSpawnTime = settings.minSpawnTime;
        maxSpawnTime = settings.maxSpawnTime;
        maxBombCount = settings.maxBombCount;
        bombSpawnSafeTime = settings.spawnSafeTime;

        ResetSpawner();
    }

    public void ApplyLevelSettings(
        float minTime,
        float maxTime,
        int maxCount)
    {
        minSpawnTime = Mathf.Max(0f, minTime);
        maxSpawnTime = Mathf.Max(minSpawnTime, maxTime);
        maxBombCount = Mathf.Max(0, maxCount);

        ResetSpawner();
    }

    public void ResetSpawner()
    {
        timer = 0f;

        CleanupActiveBombs();
        RefreshPlayerReference();
        RefreshSpawnFilter();

        SetNextSpawnTime();
    }

    private bool TrySpawnBomb()
    {
        if (bombPrefab == null)
            return false;

        if (CameraWorldBounds.Instance == null)
            return false;

        if (!TryGetValidSpawnPosition(out Vector2 spawnPosition))
            return false;

        GameObject bomb = Instantiate(
            bombPrefab,
            spawnPosition,
            Quaternion.identity
        );

        if (bomb == null)
            return false;

        SpaceBomb spaceBomb =
            bomb.GetComponent<SpaceBomb>();

        if (spaceBomb != null)
        {
            spaceBomb.spawnSafeTime =
                bombSpawnSafeTime;
        }

        activeBombs.Add(bomb);

        SpawnAreaRegistry.Register(
            bomb,
            checkRadius
        );

        SoundManager.Instance?.PlaySpaceBombSpawnSound(bomb.transform.position);

        return true;
    }

    private void CleanupActiveBombs()
    {
        for (int i = activeBombs.Count - 1; i >= 0; i--)
        {
            if (activeBombs[i] == null)
                activeBombs.RemoveAt(i);
        }
    }

    private bool TryGetValidSpawnPosition(out Vector2 spawnPosition)
    {
        if (CameraWorldBounds.Instance == null)
        {
            spawnPosition = Vector2.zero;
            return false;
        }

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            spawnPosition =
                CameraWorldBounds.Instance.RandomPointInside(spawnPadding);

            if (IsAreaClear(spawnPosition))
                return true;
        }

        spawnPosition = Vector2.zero;
        return false;
    }

    private bool IsAreaClear(Vector2 position)
    {
        if (!SpawnAreaRegistry.IsAreaFree(position, checkRadius))
            return false;

        if (!IsSafeFromPlayer(position))
            return false;

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

    private bool IsSafeFromPlayer(Vector2 spawnPosition)
    {
        if (playerMovement == null)
            return true;

        Vector2 playerPosition =
            playerMovement.transform.position;

        Vector2 toSpawn =
            spawnPosition - playerPosition;

        float distanceSqr =
            toSpawn.sqrMagnitude;

        float safeDistanceSqr =
            playerSafeDistance * playerSafeDistance;

        if (distanceSqr < safeDistanceSqr)
            return false;

        Vector2 moveDirection =
            playerMovement.LastMoveDirection;

        if (moveDirection.sqrMagnitude <= 0.001f)
            return true;

        float distance =
            Mathf.Sqrt(distanceSqr);

        if (distance <= Mathf.Epsilon)
            return false;

        Vector2 normalizedMoveDirection =
            moveDirection.normalized;

        Vector2 normalizedSpawnDirection =
            toSpawn / distance;

        float forwardDot = Vector2.Dot(
            normalizedSpawnDirection,
            normalizedMoveDirection
        );

        if (forwardDot < forwardDotLimit)
            return true;

        float forwardAmount = Vector2.Dot(
            toSpawn,
            normalizedMoveDirection
        );

        if (forwardAmount <= 0f ||
            forwardAmount >= playerForwardSafeDistance)
        {
            return true;
        }

        Vector2 closestPointOnForwardLine =
            playerPosition +
            normalizedMoveDirection * forwardAmount;

        float sideDistance =
            Vector2.Distance(
                spawnPosition,
                closestPointOnForwardLine
            );

        return sideDistance >= ForwardBlockWidth;
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

    private void SetNextSpawnTime()
    {
        nextSpawnTime =
            Random.Range(
                minSpawnTime,
                maxSpawnTime
            );
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
        spawnFilter = ContactFilter2D.noFilter;
        spawnFilter.useTriggers = true;
    }

    private void OnValidate()
    {
        minSpawnTime =
            Mathf.Max(0f, minSpawnTime);

        maxSpawnTime =
            Mathf.Max(minSpawnTime, maxSpawnTime);

        maxBombCount =
            Mathf.Max(0, maxBombCount);

        bombSpawnSafeTime =
            Mathf.Max(0f, bombSpawnSafeTime);

        spawnPadding =
            Mathf.Max(0f, spawnPadding);

        checkRadius =
            Mathf.Max(0f, checkRadius);

        maxAttempts =
            Mathf.Max(1, maxAttempts);

        playerSafeDistance =
            Mathf.Max(0f, playerSafeDistance);

        playerForwardSafeDistance =
            Mathf.Max(0f, playerForwardSafeDistance);

        forwardDotLimit =
            Mathf.Clamp01(forwardDotLimit);

        if (Application.isPlaying)
            RefreshSpawnFilter();
    }
}