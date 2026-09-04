using System.Collections;
using UnityEngine;

public class HorizontalLaserWallSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject laserWarningPrefab;
    public GameObject laserWallPrefab;

    [Header("Spawn Time")]
    public float minSpawnTime = 8f;
    public float maxSpawnTime = 25f;

    [Header("Warning")]
    public float warningDuration = 2f;

    [Header("Laser Settings")]
    public float laserLifeTime = 1.5f;
    public float laserWidth = 0.5f;
    public float edgePadding = 0.4f;
    public float widthExtra = 1f;

    [Header("Game State")]
    public PlayerMovement playerMovement;

    private Coroutine spawnCoroutine;
    private GameObject activeWarning;
    private GameObject activeLaser;

    private bool systemActive;
    private bool settingsApplied;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = FindAnyObjectByType<PlayerMovement>();
    }

    private void OnDisable()
    {
        StopLaserSystem();
    }

    private void Update()
    {
        if (!systemActive)
            return;

        if (playerMovement != null &&
            playerMovement.IsGameOver)
        {
            StopLaserSystem();
        }
    }

    public void ApplyLevelSettings(
        float minTime,
        float maxTime,
        float warningTime,
        float lifeTime,
        float width,
        float extraWidth
    )
    {
        minSpawnTime = Mathf.Max(0f, minTime);
        maxSpawnTime = Mathf.Max(minSpawnTime, maxTime);

        warningDuration = Mathf.Max(0f, warningTime);
        laserLifeTime = Mathf.Max(0.1f, lifeTime);
        laserWidth = Mathf.Max(0.01f, width);
        widthExtra = Mathf.Max(0f, extraWidth);

        settingsApplied = true;

        StartLaserSystem();
    }

    public void StartLaserSystem()
    {
        if (!isActiveAndEnabled)
            return;

        if (!settingsApplied)
        {
            Debug.LogWarning(
                "[HorizontalLaser] Level settings uygulanmadan sistem başlatılmak istendi.",
                this
            );

            return;
        }

        if (!ValidateReferences())
            return;

        StopLaserSystem();

        systemActive = true;
        spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    public void StopLaserSystem()
    {
        systemActive = false;

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
    }

    private IEnumerator SpawnRoutine()
    {
        while (systemActive)
        {
            yield return new WaitUntil(
                () =>
                    !systemActive ||
                    GameStateManager.IsGameplayStarted
            );

            if (!systemActive || IsGameOver())
                yield break;

            float waitTime = Random.Range(
                minSpawnTime,
                maxSpawnTime
            );

            float elapsedTime = 0f;

            while (elapsedTime < waitTime)
            {
                if (!systemActive || IsGameOver())
                    yield break;

                if (GameStateManager.IsGameplayStarted)
                {
                    elapsedTime += Time.deltaTime;
                }

                yield return null;
            }

            if (!systemActive ||
                IsGameOver() ||
                !GameStateManager.IsGameplayStarted)
            {
                continue;
            }

            yield return SpawnHorizontalLaser();
        }

        spawnCoroutine = null;
    }

    

    private IEnumerator SpawnHorizontalLaser()
    {
        if (!GameStateManager.IsGameplayStarted)
            yield break;

        CameraWorldBounds bounds =
            CameraWorldBounds.Instance;

        if (bounds == null)
        {
            Debug.LogWarning(
                "[HorizontalLaser] CameraWorldBounds.Instance bulunamadı.",
                this
            );

            yield break;
        }

        float minimumY =
            bounds.MinY + edgePadding;

        float maximumY =
            bounds.MaxY - edgePadding;

        if (minimumY > maximumY)
        {
            minimumY = bounds.Center.y;
            maximumY = bounds.Center.y;
        }

        float yPos =
            Random.Range(minimumY, maximumY);

        Vector3 position =
            new Vector3(
                bounds.Center.x,
                yPos,
                0f
            );

        Vector3 scale =
            new Vector3(
                laserWidth,
                bounds.Width + widthExtra,
                1f
            );

        Quaternion rotation =
            Quaternion.Euler(0f, 0f, 90f);

        activeWarning =
            Instantiate(
                laserWarningPrefab,
                position,
                rotation
            );

        activeWarning.transform.localScale =
            scale;

        yield return PlayWarning();

        DestroyActiveWarning();

        if (!systemActive || IsGameOver())
            yield break;

        activeLaser =
            Instantiate(
                laserWallPrefab,
                position,
                rotation
            );

        activeLaser.transform.localScale =
            scale;

        LaserWall laserWall =
            activeLaser.GetComponent<LaserWall>();

        if (laserWall != null)
        {
            laserWall.lifeTime =
                laserLifeTime;
        }
        else
        {
            Debug.LogWarning(
                "[HorizontalLaser] Laser prefab üzerinde LaserWall componenti yok.",
                activeLaser
            );
        }
    }

    private IEnumerator PlayWarning()
    {
        if (activeWarning == null)
        {
            yield return new WaitForSeconds(
                warningDuration
            );

            yield break;
        }

        LaserWarning warning =
            activeWarning.GetComponent<LaserWarning>();

        if (warning != null)
        {
            warning.blinkDuration =
                warningDuration;

            yield return warning.PlayWarning();
        }
        else
        {
            Debug.LogWarning(
                "[HorizontalLaser] Warning prefab üzerinde LaserWarning componenti yok.",
                activeWarning
            );

            yield return new WaitForSeconds(
                warningDuration
            );
        }
    }

    private void DestroyActiveWarning()
    {
        if (activeWarning == null)
            return;

        Destroy(activeWarning);
        activeWarning = null;
    }

    private bool IsGameOver()
    {
        return playerMovement != null &&
               playerMovement.IsGameOver;
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (laserWarningPrefab == null)
        {
            Debug.LogError(
                "[HorizontalLaser] Laser Warning Prefab atanmamış.",
                this
            );

            valid = false;
        }

        if (laserWallPrefab == null)
        {
            Debug.LogError(
                "[HorizontalLaser] Laser Wall Prefab atanmamış.",
                this
            );

            valid = false;
        }

        if (playerMovement == null)
            playerMovement = FindAnyObjectByType<PlayerMovement>();

        return valid;
    }
}