using System.Collections;
using UnityEngine;

public class LaserWallSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject laserWarningPrefab;
    public GameObject laserWallPrefab;

    [Header("Spawn Time")]
    public float minSpawnTime = 8f;
    public float maxSpawnTime = 25f;

    [Header("Warning")]
    public float warningDuration = 2f;

    [Header("Laser")]
    public float laserLifeTime = 1.5f;
    public float laserWidth = 0.5f;
    public float edgePadding = 0.4f;
    public float heightExtra = 1f;

    [Header("Game State")]
    public PlayerMovement playerMovement;

    private Coroutine spawnCoroutine;
    private GameObject activeWarning;
    private GameObject activeLaser;
    private bool stopped;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = FindAnyObjectByType<PlayerMovement>();
    }

    private void Start()
    {
        StartLaserSystem();
    }

    public void ApplyLevelSettings(float minTime, float maxTime, float warningTime, float lifeTime, float width, float extraHeight)
    {
        minSpawnTime = Mathf.Max(0f, minTime);
        maxSpawnTime = Mathf.Max(minSpawnTime, maxTime);
        warningDuration = Mathf.Max(0f, warningTime);
        laserLifeTime = Mathf.Max(0.1f, lifeTime);
        laserWidth = Mathf.Max(0.01f, width);
        heightExtra = Mathf.Max(0f, extraHeight);

        RestartLaserSystem();
    }

    private void Update()
    {
        if (stopped) return;

        if (playerMovement != null && playerMovement.IsGameOver)
            StopLaserSystem();
    }

    private void StartLaserSystem()
    {
        stopped = false;

        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);

        spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    private void RestartLaserSystem()
    {
        DestroyActiveWarning();

        if (activeLaser != null)
        {
            Destroy(activeLaser);
            activeLaser = null;
        }

        StartLaserSystem();
    }

    private IEnumerator SpawnRoutine()
    {
        while (!stopped)
        {
            yield return new WaitUntil(
                () =>
                    stopped ||
                    GameStateManager.IsGameplayStarted
            );

            if (stopped || IsGameOver())
                yield break;

            float waitTime = Random.Range(
                minSpawnTime,
                maxSpawnTime
            );

            float elapsedTime = 0f;

            while (elapsedTime < waitTime)
            {
                if (stopped || IsGameOver())
                    yield break;

                if (GameStateManager.IsGameplayStarted)
                {
                    elapsedTime += Time.deltaTime;
                }

                yield return null;
            }

            if (stopped ||
                IsGameOver() ||
                !GameStateManager.IsGameplayStarted)
            {
                continue;
            }

            yield return SpawnVerticalLaser();
        }

        spawnCoroutine = null;
    }

    private IEnumerator SpawnVerticalLaser()
    {
        if (!GameStateManager.IsGameplayStarted)
            yield break;

        CameraWorldBounds bounds = CameraWorldBounds.Instance;
        if (bounds == null) yield break;

        float xPos = Random.Range(bounds.MinX + edgePadding, bounds.MaxX - edgePadding);

        Vector3 position = new Vector3(xPos, bounds.Center.y, 0f);
        Vector3 scale = new Vector3(laserWidth, bounds.Height + heightExtra, 1f);

        activeWarning = Instantiate(laserWarningPrefab, position, Quaternion.identity);
        activeWarning.transform.localScale = scale;

        yield return PlayWarning();

        DestroyActiveWarning();

        if (IsGameOver())
        {
            StopLaserSystem();
            yield break;
        }

        activeLaser = Instantiate(laserWallPrefab, position, Quaternion.identity);
        activeLaser.transform.localScale = scale;

        LaserWall laserWall = activeLaser.GetComponent<LaserWall>();
        if (laserWall != null)
            laserWall.lifeTime = laserLifeTime;
    }

    private IEnumerator PlayWarning()
    {
        if (activeWarning == null)
        {
            yield return new WaitForSeconds(warningDuration);
            yield break;
        }

        LaserWarning warning = activeWarning.GetComponent<LaserWarning>();

        if (warning != null)
        {
            warning.blinkDuration = warningDuration;
            yield return warning.PlayWarning();
        }
        else
        {
            yield return new WaitForSeconds(warningDuration);
        }
    }

    private void DestroyActiveWarning()
    {
        if (activeWarning == null) return;

        Destroy(activeWarning);
        activeWarning = null;
    }

    private bool IsGameOver()
    {
        return playerMovement != null && playerMovement.IsGameOver;
    }

    public void StopLaserSystem()
    {
        if (stopped) return;

        stopped = true;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        DestroyActiveWarning();

        if (activeLaser != null)
        {
            Destroy(activeLaser);
            activeLaser = null;
        }

        enabled = false;
    }
}
