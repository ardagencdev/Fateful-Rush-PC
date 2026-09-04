using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("CURRENT LEVEL")]
    public LevelConfig currentLevel;

    [Header("CORE MANAGERS")]
    public PlayerCoinCollector coinCollector;
    public CoinManager coinManager;
    public ObstacleSpawner obstacleSpawner;
    public EnemySpawner enemySpawner;
    public PowerUpSpawner powerUpSpawner;
    public BeaconEnemySpawner beaconEnemySpawner;

    [Header("LASERS / TRAPS")]
    public LaserWallSpawner verticalLaserSpawner;
    public HorizontalLaserWallSpawner horizontalLaserSpawner;
    public SpaceBombSpawner bombTrapSpawner;

    [Header("PLAYER")]
    public PlayerMovement playerMovement;
    public PlayerDash playerDash;
    public PlayerArmor playerArmor;
    public GameObject dashButton;
    public VoidCloneAbility voidCloneAbility;
    public GameObject cloneButton;

    [Header("UI")]
    public ComboUI comboUI;

    [Header("BACKGROUND")]
    public StarfieldController starfieldController;

    private bool initialized;

    private void Start()
    {
        InitializeLevel();
    }

    public void InitializeLevel()
    {
        if (initialized)
            return;

        ResolveSpawnerReferences();
        ResolveStarfieldReference();
        ResolveSelectedLevel();

        if (currentLevel == null)
        {
            Debug.LogError(
                "[LevelManager] currentLevel NULL! LevelConfig atanmadı.",
                this
            );

            return;
        }

        initialized = true;

        Debug.Log(
            $"[LevelManager] Loaded Config: {currentLevel.name} | " +
            $"Mode: {SelectedLevelData.launchMode}",
            this
        );

        if (!currentLevel.HasDangerProfile)
        {
            Debug.LogWarning(
                "[LevelManager] Danger Balance Profile atanmadı. " +
                "Eski LevelConfig değerleri fallback olarak kullanılacak.",
                currentLevel
            );
        }

        ApplyLevelConfig();
    }

    private void ResolveSelectedLevel()
    {
        if (SelectedLevelData.selectedLevel != null)
            currentLevel = SelectedLevelData.selectedLevel;
    }

    private void ApplyLevelConfig()
    {
        if (currentLevel == null)
            return;

        ApplyPlayer();
        ApplyUI();
        ApplyBackground();
        ApplyCoins();
        ApplyObstacles();
        ApplyEnemies();
        ApplyBeaconEnemy();
        ApplyPowerUps();
        ApplyLasersAndTraps();
    }

    private void ApplyPlayer()
    {
        if (playerMovement != null)
        {
            playerMovement.speed =
                currentLevel.playerMoveSpeed;

            playerMovement.comboSpeedBonus =
                PlayerMovement.FixedComboSpeedBonus;

            playerMovement.comboSpeedStages =
                currentLevel.comboSpeedStages;
        }

        if (playerDash != null)
        {
            playerDash.SetDashButton(
                dashButton != null
                    ? dashButton.GetComponent<UnityEngine.UI.Button>()
                    : null
            );

            playerDash.enabled =
                currentLevel.dashEnabled;

            playerDash.dashDistance =
                currentLevel.dashDistance;

            playerDash.dashDuration =
                currentLevel.dashDuration;

            playerDash.dashCooldown =
                currentLevel.dashCooldown;
        }

        if (playerArmor != null)
        {
            playerArmor.immuneDuration =
                currentLevel.armorImmuneDuration;
        }

        SetUIVisible(
            dashButton,
            currentLevel.dashEnabled
        );

        SetUIVisible(
            cloneButton,
            currentLevel.cloneEnabled
        );

        if (voidCloneAbility != null)
        {
            voidCloneAbility.SetCloneButton(
                cloneButton != null
                    ? cloneButton.GetComponent<UnityEngine.UI.Button>()
                    : null
            );

            voidCloneAbility.enabled =
                currentLevel.cloneEnabled;

            voidCloneAbility.cloneDuration =
                currentLevel.cloneDuration;

            voidCloneAbility.SetCloneCooldown(
                currentLevel.cloneCooldown
            );

        }
    }

    private void ApplyUI()
    {
        bool comboActive =
            currentLevel.EffectiveComboEnabled;

        if (coinCollector != null)
        {
            coinCollector.comboEnabled =
                comboActive;

            coinCollector.comboTimeLimit =
                currentLevel.comboTimeLimit;

            coinCollector.comboSpeedStages =
                currentLevel.comboSpeedStages;
        }

        if (comboUI != null)
        {
            SetUIVisible(
                comboUI.gameObject,
                comboActive
            );
        }
    }

    private void ApplyBackground()
    {
        if (starfieldController == null)
        {
            Debug.LogWarning(
                "[LevelManager] StarfieldController bulunamadı. " +
                "Background level ayarları uygulanamadı.",
                this
            );

            return;
        }

        starfieldController.ApplyLevel(currentLevel);
    }

    private void ApplyCoins()
    {
        if (coinManager == null)
            return;

        bool coinsActive =
            currentLevel.CoinsEnabled;

        coinManager.enabled = coinsActive;

        if (!coinsActive)
            return;

        coinManager.spawnInterval =
            currentLevel.coinSpawnInterval;

        coinManager.maxCoinCount =
            currentLevel.maxCoinCount;

        coinManager.normalCoinChance =
            currentLevel.normalCoinEnabled
                ? currentLevel.normalCoinChance
                : 0f;

        coinManager.goldCoinChance =
            currentLevel.goldCoinEnabled
                ? currentLevel.goldCoinChance
                : 0f;

        coinManager.rareCoinChance =
            currentLevel.rareCoinEnabled
                ? currentLevel.rareCoinChance
                : 0f;

        coinManager.normalCoinValue =
            currentLevel.normalCoinValue;

        coinManager.goldCoinValue =
            currentLevel.goldCoinValue;

        coinManager.rareCoinValue =
            currentLevel.rareCoinValue;

        coinManager.ResetSpawner();
    }

    private void ApplyObstacles()
    {
        if (obstacleSpawner == null)
            return;

        obstacleSpawner.levelObstacles =
            currentLevel.levelObstacles;

        obstacleSpawner.obstacleSpawnMode =
            currentLevel.obstacleSpawnMode;

        obstacleSpawner.randomObstacleCount =
            currentLevel.randomObstacleCount;

        obstacleSpawner.ClearObstacles();
        obstacleSpawner.SpawnObstacles();
    }

    private void ApplyEnemies()
    {
        if (enemySpawner == null)
            return;

        ApplyNormalEnemySettings();
        ApplyProjectileEnemySettings();
        ApplyHunterEnemySettings();
        ApplyBossSettings();

        enemySpawner.ConfigureSpawnPacing(
            currentLevel
        );

        enemySpawner.ResetSpawner();
    }

    private void ApplyNormalEnemySettings()
    {
        enemySpawner.normalEnemyCount =
            currentLevel.normalEnemyCount;

        enemySpawner.normalEnemySpawnInterval =
            currentLevel.normalEnemySpawnInterval;

        enemySpawner.ApplyNormalDanger(
            currentLevel.ResolveNormalEnemyDanger()
        );
    }

    private void ApplyProjectileEnemySettings()
    {
        enemySpawner.projectileEnemyCount =
            currentLevel.projectileEnemyCount;

        enemySpawner.projectileEnemySpawnInterval =
            currentLevel.projectileEnemySpawnInterval;

        enemySpawner.ApplyProjectileDanger(
            currentLevel.ResolveProjectileEnemyDanger()
        );
    }

    private void ApplyHunterEnemySettings()
    {
        enemySpawner.hunterEnemyCount =
            currentLevel.hunterEnemyCount;

        enemySpawner.hunterEnemySpawnInterval =
            currentLevel.hunterEnemySpawnInterval;

        enemySpawner.ApplyHunterDanger(
            currentLevel.ResolveHunterEnemyDanger()
        );
    }

    private void ApplyBossSettings()
    {
        enemySpawner.bossEnabled =
            currentLevel.bossEnabled;

        enemySpawner.bossSpawnCondition =
            currentLevel.EffectiveBossSpawnCondition;

        enemySpawner.bossSpawnScore =
            currentLevel.SafeBossSpawnScore;

        enemySpawner.bossSpawnTime =
            currentLevel.SafeBossSpawnTime;

        enemySpawner.ApplyBossDanger(
            currentLevel.ResolveBossDanger()
        );
    }

    private void ApplyBeaconEnemy()
    {
        if (beaconEnemySpawner == null)
        {
            if (currentLevel.beaconEnemyCount > 0)
            {
                Debug.LogWarning(
                    "[LevelManager] Beacon enemy açık fakat " +
                    "BeaconEnemySpawner bulunamadı.",
                    this
                );
            }

            return;
        }

        beaconEnemySpawner.ApplyLevelSettings(
            currentLevel.beaconEnemyCount,
            currentLevel.beaconMinSpawnTime,
            currentLevel.beaconMaxSpawnTime
        );

        beaconEnemySpawner.ApplyDangerSettings(
            currentLevel.ResolveBeaconEnemyDanger()
        );
    }

    private void ApplyPowerUps()
    {
        if (powerUpSpawner == null)
        {
            if (currentLevel.slowEnabled ||
                currentLevel.armorEnabled)
            {
                Debug.LogWarning(
                    "[LevelManager] Power-up açık fakat " +
                    "PowerUpSpawner bulunamadı.",
                    this
                );
            }

            return;
        }

        powerUpSpawner.ApplyLevelSettings(
            currentLevel.slowEnabled,
            currentLevel.armorEnabled,
            currentLevel.slowMinSpawnTime,
            currentLevel.slowMaxSpawnTime,
            currentLevel.armorMinSpawnTime,
            currentLevel.armorMaxSpawnTime,
            currentLevel.slowMultiplier,
            currentLevel.slowDuration
        );
    }

    private void ApplyLasersAndTraps()
    {
        ApplyVerticalLaser();
        ApplyHorizontalLaser();
        ApplyBombTraps();
    }

    private void ApplyVerticalLaser()
    {
        if (verticalLaserSpawner == null)
        {
            if (currentLevel.verticalLaserEnabled)
            {
                Debug.LogError(
                    "[LevelManager] Vertical Laser açık fakat " +
                    "LaserWallSpawner bulunamadı.",
                    this
                );
            }

            return;
        }

        verticalLaserSpawner.gameObject.SetActive(
            currentLevel.verticalLaserEnabled
        );

        if (!currentLevel.verticalLaserEnabled)
            return;

        LaserDangerSettings settings =
            currentLevel.ResolveVerticalLaserDanger();

        verticalLaserSpawner.ApplyLevelSettings(
            settings.minSpawnTime,
            settings.maxSpawnTime,
            settings.warningDuration,
            settings.lifeTime,
            settings.width,
            settings.sizeExtra
        );
    }

    private void ApplyHorizontalLaser()
    {
        if (horizontalLaserSpawner == null)
        {
            if (currentLevel.horizontalLaserEnabled)
            {
                Debug.LogError(
                    "[LevelManager] Horizontal Laser açık fakat " +
                    "HorizontalLaserWallSpawner bulunamadı.",
                    this
                );
            }

            return;
        }

        if (!currentLevel.horizontalLaserEnabled)
        {
            horizontalLaserSpawner.StopLaserSystem();
            horizontalLaserSpawner.gameObject.SetActive(false);
            return;
        }

        horizontalLaserSpawner.gameObject.SetActive(true);

        LaserDangerSettings settings =
            currentLevel.ResolveHorizontalLaserDanger();

        horizontalLaserSpawner.ApplyLevelSettings(
            settings.minSpawnTime,
            settings.maxSpawnTime,
            settings.warningDuration,
            settings.lifeTime,
            settings.width,
            settings.sizeExtra
        );
    }

    private void ApplyBombTraps()
    {
        if (bombTrapSpawner == null)
        {
            if (currentLevel.bombTrapEnabled)
            {
                Debug.LogError(
                    "[LevelManager] Bomb Trap açık fakat " +
                    "SpaceBombSpawner bulunamadı.",
                    this
                );
            }

            return;
        }

        bombTrapSpawner.gameObject.SetActive(
            currentLevel.bombTrapEnabled
        );

        if (!currentLevel.bombTrapEnabled)
            return;

        bombTrapSpawner.ApplyDangerSettings(
            currentLevel.ResolveBombDanger()
        );
    }

    private void ResolveSpawnerReferences()
    {
        if (verticalLaserSpawner == null)
        {
            verticalLaserSpawner =
                FindAnyObjectByType<LaserWallSpawner>(
                    FindObjectsInactive.Include
                );
        }

        if (horizontalLaserSpawner == null)
        {
            horizontalLaserSpawner =
                FindAnyObjectByType<HorizontalLaserWallSpawner>(
                    FindObjectsInactive.Include
                );
        }

        if (bombTrapSpawner == null)
        {
            bombTrapSpawner =
                FindAnyObjectByType<SpaceBombSpawner>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void ResolveStarfieldReference()
    {
        if (starfieldController != null)
            return;

        starfieldController =
            FindAnyObjectByType<StarfieldController>(
                FindObjectsInactive.Include
            );
    }

    private void SetUIVisible(
        GameObject obj,
        bool visible
    )
    {
        if (obj == null)
            return;

        if (!obj.activeSelf)
            obj.SetActive(true);

        CanvasGroup group =
            obj.GetComponent<CanvasGroup>();

        if (group == null)
            group = obj.AddComponent<CanvasGroup>();

        group.alpha =
            visible ? 1f : 0f;

        group.interactable =
            visible;

        group.blocksRaycasts =
            visible;
    }
}