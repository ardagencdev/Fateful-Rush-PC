using System.Collections.Generic;
using UnityEngine;

public class CoinManager : MonoBehaviour
{
    [Header("Coin Prefabs")]
    public GameObject normalCoin;
    public GameObject goldCoin;
    public GameObject rareCoin;

    [Header("Spawn Settings")]
    [Min(0f)]
    public float spawnInterval = 1f;

    [Min(0)]
    public int maxCoinCount = 8;

    [Header("Coin Chances")]
    [Range(0f, 100f)]
    public float normalCoinChance = 70f;

    [Range(0f, 100f)]
    public float goldCoinChance = 25f;

    [Range(0f, 100f)]
    public float rareCoinChance = 5f;

    [Header("Coin Values")]
    [Min(1)]
    public int normalCoinValue = 1;

    [Min(1)]
    public int goldCoinValue = 3;

    [Min(1)]
    public int rareCoinValue = 5;

    [Header("Spawn Rules")]
    [Min(0f)]
    public float spawnPadding = 0.8f;

    [Min(0f)]
    public float checkRadius = 0.6f;

    [Min(0f)]
    public float playerSafeDistance = 2.5f;

    [Min(1)]
    public int maxTry = 100;

    [Header("References")]
    public PlayerMovement playerMovement;

    private float timer;

    private Transform playerTransform;

    private int obstacleLayerIndex;
    private int wallLayerIndex;

    private ContactFilter2D spawnFilter;

    private readonly List<GameObject> activeCoins =
        new List<GameObject>(32);

    private readonly Collider2D[] spawnCheckHits =
        new Collider2D[32];

    private void Awake()
    {
        RefreshPlayerReference();

        obstacleLayerIndex =
            LayerMask.NameToLayer("Obstacle");

        wallLayerIndex =
            LayerMask.NameToLayer("Wall");

        spawnFilter = ContactFilter2D.noFilter;
        spawnFilter.useTriggers = true;
    }

    private void Start()
    {
        int prewarmCount = Mathf.Max(1, maxCoinCount);

        RuntimeObjectPool.Prewarm(normalCoin, prewarmCount);
        RuntimeObjectPool.Prewarm(goldCoin, prewarmCount);
        RuntimeObjectPool.Prewarm(rareCoin, prewarmCount);
    }

    private void Update()
    {
        if (!GameStateManager.IsGameplayStarted)
            return;

        if (playerMovement != null &&
            playerMovement.IsGameOver)
        {
            return;
        }

        HandleCoinSpawn();
    }

    public void ResetSpawner()
    {
        timer = 0f;

        /*
         * Sahnedeki canlı coinleri listeden silmiyoruz.
         * Yalnızca yok edilmiş coin referanslarını
         * temizliyoruz. Böylece maxCoinCount aşılmaz.
         */
        RemoveDestroyedCoins();

        RefreshPlayerReference();
    }

    private void HandleCoinSpawn()
    {
        if (maxCoinCount <= 0)
            return;

        timer += Time.deltaTime;

        if (timer < spawnInterval)
            return;

        timer = 0f;

        if (GetActiveCoinCount() >= maxCoinCount)
            return;

        TrySpawnCoin();
    }

    private int GetActiveCoinCount()
    {
        RemoveDestroyedCoins();
        return activeCoins.Count;
    }

    private void RemoveDestroyedCoins()
    {
        for (int i = activeCoins.Count - 1;
             i >= 0;
             i--)
        {
            if (activeCoins[i] == null ||
                !activeCoins[i].activeSelf)
            {
                activeCoins.RemoveAt(i);
            }
        }
    }

    private bool TrySpawnCoin()
    {
        if (CameraWorldBounds.Instance == null)
            return false;

        GameObject coinPrefab = GetRandomCoin();

        if (coinPrefab == null)
            return false;

        if (!TryGetValidSpawnPosition(
                out Vector2 spawnPosition))
        {
            return false;
        }

        GameObject coinObject = RuntimeObjectPool.Spawn(
            coinPrefab,
            spawnPosition,
            Quaternion.identity
        );

        Coin coin =
            coinObject.GetComponent<Coin>();

        if (coin == null)
        {
            Debug.LogError(
                $"Coin prefabında Coin componenti bulunamadı: {coinPrefab.name}",
                coinPrefab
            );

            RuntimeObjectPool.Release(coinObject);
            return false;
        }

        ApplyCoinValue(
            coin,
            coinPrefab
        );

        activeCoins.Add(coinObject);

        SpawnAreaRegistry.Register(
            coinObject,
            checkRadius
        );

        return true;
    }

    private void ApplyCoinValue(
        Coin coin,
        GameObject prefab
    )
    {
        if (prefab == normalCoin)
        {
            coin.Configure(CoinType.Normal, normalCoinValue);
            return;
        }

        if (prefab == goldCoin)
        {
            coin.Configure(CoinType.Gold, goldCoinValue);
            return;
        }

        if (prefab == rareCoin)
            coin.Configure(CoinType.Rare, rareCoinValue);
    }

    private bool TryGetValidSpawnPosition(
        out Vector2 spawnPosition
    )
    {
        CameraWorldBounds bounds =
            CameraWorldBounds.Instance;

        if (bounds == null)
        {
            spawnPosition = Vector2.zero;
            return false;
        }

        for (int attempt = 0;
             attempt < maxTry;
             attempt++)
        {
            spawnPosition =
                bounds.RandomPointInside(
                    spawnPadding
                );

            if (IsValidPosition(spawnPosition))
                return true;
        }

        spawnPosition = Vector2.zero;
        return false;
    }

    private bool IsValidPosition(Vector2 position)
    {
        if (!SpawnAreaRegistry.IsAreaFree(
                position,
                checkRadius))
        {
            return false;
        }

        if (playerTransform != null)
        {
            float safeDistanceSquared =
                playerSafeDistance *
                playerSafeDistance;

            Vector2 playerPosition =
                playerTransform.position;

            if ((position - playerPosition)
                    .sqrMagnitude <
                safeDistanceSquared)
            {
                return false;
            }
        }

        int hitCount = Physics2D.OverlapCircle(
            position,
            checkRadius,
            spawnFilter,
            spawnCheckHits
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit =
                spawnCheckHits[i];

            if (hit == null)
                continue;

            if (BlocksCoinSpawn(hit))
                return false;
        }

        return true;
    }

    private bool BlocksCoinSpawn(Collider2D hit)
    {
        if (hit.CompareTag("Coin"))
            return true;

        if (hit.CompareTag("Enemy"))
            return true;

        if (hit.CompareTag("Player"))
            return true;

        if (hit.CompareTag("PowerUp"))
            return true;

        if (hit.CompareTag("Bomb"))
            return true;

        int objectLayer =
            hit.gameObject.layer;

        if (objectLayer == obstacleLayerIndex)
            return true;

        if (objectLayer == wallLayerIndex)
            return true;

        return false;
    }

    private GameObject GetRandomCoin()
    {
        float normalChance =
            normalCoin != null
                ? normalCoinChance
                : 0f;

        float goldChance =
            goldCoin != null
                ? goldCoinChance
                : 0f;

        float rareChance =
            rareCoin != null
                ? rareCoinChance
                : 0f;

        float totalChance =
            normalChance +
            goldChance +
            rareChance;

        if (totalChance <= 0f)
            return null;

        float randomValue =
            Random.Range(0f, totalChance);

        if (randomValue < normalChance)
            return normalCoin;

        randomValue -= normalChance;

        if (randomValue < goldChance)
            return goldCoin;

        return rareCoin;
    }

    private void RefreshPlayerReference()
    {
        if (playerMovement == null)
        {
            playerMovement =
                FindAnyObjectByType<PlayerMovement>();
        }

        playerTransform =
            playerMovement != null
                ? playerMovement.transform
                : null;
    }

    private void OnValidate()
    {
        spawnInterval =
            Mathf.Max(0f, spawnInterval);

        maxCoinCount =
            Mathf.Max(0, maxCoinCount);

        normalCoinValue =
            Mathf.Max(1, normalCoinValue);

        goldCoinValue =
            Mathf.Max(1, goldCoinValue);

        rareCoinValue =
            Mathf.Max(1, rareCoinValue);

        spawnPadding =
            Mathf.Max(0f, spawnPadding);

        checkRadius =
            Mathf.Max(0f, checkRadius);

        playerSafeDistance =
            Mathf.Max(0f, playerSafeDistance);

        maxTry =
            Mathf.Max(1, maxTry);
    }
}