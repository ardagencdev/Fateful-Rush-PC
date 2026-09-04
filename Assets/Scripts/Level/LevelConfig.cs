using UnityEngine;

[System.Serializable]
public class LevelObstacleOption
{
    public GameObject prefab;
    public bool enabled = true;
}

public enum ObstacleSpawnMode
{
    Fixed,
    Random
}

public enum WinConditionType
{
    ReachScore,
    SurviveTime,
    ReachScoreWithinTime
}

public enum BossSpawnCondition
{
    Score,
    Time
}


public enum MechanicProgressionStatus
{
    AlreadyKnown,
    IntroducedHere,
    FinalChallenge
}

[System.Serializable]
public class LevelMechanicProgression
{
    [Header("MISSION MODES")]
    public MechanicProgressionStatus reachScoreMode =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus surviveTimeMode =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus timedScoreMode =
        MechanicProgressionStatus.AlreadyKnown;

    [Header("PLAYER")]
    public MechanicProgressionStatus dash =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus clone =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus combo =
        MechanicProgressionStatus.AlreadyKnown;

    [Header("COINS / ARENA")]
    public MechanicProgressionStatus normalCoin =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus goldCoin =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus rareCoin =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus staticObstacles =
        MechanicProgressionStatus.AlreadyKnown;

    [Header("ENEMIES")]
    public MechanicProgressionStatus normalEnemy =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus projectileEnemy =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus hunterEnemy =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus boss =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus beaconEnemy =
        MechanicProgressionStatus.AlreadyKnown;

    [Header("POWER UPS")]
    public MechanicProgressionStatus armor =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus slow =
        MechanicProgressionStatus.AlreadyKnown;

    [Header("TRAPS")]
    public MechanicProgressionStatus verticalLaser =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus horizontalLaser =
        MechanicProgressionStatus.AlreadyKnown;

    public MechanicProgressionStatus spaceBomb =
        MechanicProgressionStatus.AlreadyKnown;
}

[System.Serializable]
public class ComboSpeedStage
{
    [Min(2)]
    public int comboMultiplier = 2;

    [Min(1)]
    public int coinsRequired = 3;

    [Min(1f)]
    public float playerSpeedMultiplier = 1.03f;
}

[CreateAssetMenu(
    fileName = "LevelConfig",
    menuName = "Fateful Rush/Level Config"
)]
public class LevelConfig : ScriptableObject
{
    [Header("LEVEL INFO")]
    [Min(0)]
    public int levelNumber = 1;

    public string levelName = "Level 1";

    [Header("LEVEL DESIGN METADATA")]
    [Tooltip(
        "Editor-only progression notes used to track when mechanics are introduced, " +
        "reused or tested as final challenges. Runtime gameplay does not depend on these values."
    )]
    public LevelMechanicProgression mechanicProgression =
        new LevelMechanicProgression();

    [Header("MISSION BRIEFING")]
    [Tooltip(
        "Main Menu içindeki mission briefing panelinde gösterilecek başlık. " +
        "Boş bırakılırsa level adı kullanılır."
    )]
    public string briefingTitle = "MISSION BRIEFING";

    [Tooltip(
        "Bölüm zorluğu. 0 ile 5 arasında tam yıldız olarak gösterilir."
    )]
    [Range(0, 5)]
    public int missionDifficulty = 1;

    [Tooltip(
        "Oyun modu için özel açıklama. Boş bırakılırsa seçilen win condition'a " +
        "göre otomatik açıklama oluşturulur."
    )]
    [TextArea(2, 5)]
    public string briefingModeDescription = "";

    [Tooltip(
        "Kazanma hedefi için özel açıklama. Boş bırakılırsa win score ve time " +
        "limit değerlerinden otomatik açıklama oluşturulur."
    )]
    [TextArea(2, 5)]
    public string briefingObjectiveDescription = "";

    [Tooltip(
        "İlk otomatik bilgi sayfasından sonra gösterilecek ek briefing sayfaları. " +
        "Yeni enemy, trap ve taktik açıklamalarını buraya yazabilirsin."
    )]
    [TextArea(4, 10)]
    public string[] briefingPages =
    {
        "Mission information..."
    };

    [Header("MUSIC")]
    [Tooltip("Bu level boyunca çalacak gameplay müziği.")]
    public AudioClip gameplayMusic;

    [Header("WIN CONDITION")]
    public WinConditionType winCondition =
        WinConditionType.ReachScore;

    [Min(1)]
    public int winScore = 15;

    [Min(0.1f)]
    public float timeLimit = 35f;

    [Header("HUD")]
    public bool showGameTimerHUD = false;

    [Header("PLAYER")]
    [Min(0f)]
    public float playerMoveSpeed = 7f;

    [Header("PLAYER ABILITIES")]
    public bool dashEnabled = true;
    public bool cloneEnabled = false;

    [Header("DASH")]
    [Min(0f)]
    public float dashDistance = 2.5f;

    [Min(0.01f)]
    public float dashDuration = 0.12f;

    [Min(0f)]
    public float dashCooldown = 2f;

    [Header("CLONE")]
    [Min(0.01f)]
    public float cloneDuration = 3f;

    [Min(0f)]
    public float cloneCooldown = 8f;

    [Header("ARMOR")]
    [Min(0f)]
    public float armorImmuneDuration = 0.8f;

    [Header("SLOW POWER UP")]
    [Range(0f, 1f)]
    public float slowMultiplier = 0.4f;

    [Min(0.01f)]
    public float slowDuration = 5f;

    [Header("UI")]
    public bool comboEnabled = true;

    [Min(0.01f)]
    public float comboTimeLimit = 2f;

    [Tooltip(
        "Combo level, kaç coin chain ile açılacağı ve player speed çarpanı. " +
        "Boş bırakırsan eski 2x/3x sistemi çalışır."
    )]
    public ComboSpeedStage[] comboSpeedStages =
    {
        new ComboSpeedStage
        {
            comboMultiplier = 2,
            coinsRequired = 3,
            playerSpeedMultiplier = 1.03f
        },
        new ComboSpeedStage
        {
            comboMultiplier = 3,
            coinsRequired = 7,
            playerSpeedMultiplier = 1.06f
        },
        new ComboSpeedStage
        {
            comboMultiplier = 4,
            coinsRequired = 14,
            playerSpeedMultiplier = 1.09f
        },
        new ComboSpeedStage
        {
            comboMultiplier = 5,
            coinsRequired = 18,
            playerSpeedMultiplier = 1.12f
        },
        new ComboSpeedStage
        {
            comboMultiplier = 6,
            coinsRequired = 22,
            playerSpeedMultiplier = 1.15f
        }
    };

    [Header("BACKGROUND / STARFIELD")]
    public bool randomizeNearStarsColor = false;

    [ColorUsage(false, true)]
    public Color nearStarsColor = Color.white;

    [Tooltip(
        "MidStars ve NearStars hareket hızını birlikte çarpar. " +
        "FarStars sabit kalır, SparkleStars hareket etmez."
    )]
    [Min(0f)]
    public float nearStarsSpeedMultiplier = 1f;

    [Tooltip(
        "MidStars ve NearStars boyutunu birlikte çarpar. " +
        "SparkleStars'a daha hafif uygulanır."
    )]
    [Min(0f)]
    public float nearStarsSizeMultiplier = 1f;

    [Tooltip(
        "MidStars, NearStars ve SparkleStars emission yoğunluğunu çarpar. " +
        "Mobil performans için 0.75-1.25 aralığında tutulması önerilir."
    )]
    [Range(0.75f, 1.25f)]
    public float starfieldDensityMultiplier = 1f;

    [Header("COINS")]
    [Min(0.01f)]
    public float coinSpawnInterval = 1f;

    [Min(0)]
    public int maxCoinCount = 8;

    [Range(0f, 100f)]
    public float normalCoinChance = 70f;

    [Range(0f, 100f)]
    public float goldCoinChance = 25f;

    [Range(0f, 100f)]
    public float rareCoinChance = 5f;

    [Min(1)]
    public int normalCoinValue = 1;

    [Min(1)]
    public int goldCoinValue = 3;

    [Min(1)]
    public int rareCoinValue = 5;

    public bool normalCoinEnabled = true;
    public bool goldCoinEnabled = true;
    public bool rareCoinEnabled = true;

    [Header("OBSTACLES")]
    public ObstacleSpawnMode obstacleSpawnMode =
        ObstacleSpawnMode.Fixed;

    public LevelObstacleOption[] levelObstacles;

    [Header("RANDOM OBSTACLES")]
    [Min(0)]
    public int randomObstacleCount = 5;

    [Header("DANGER BALANCE")]
    [Tooltip(
        "Enemy ve trap danger seviyelerinin gerçek değerlerini tutan ortak profil. " +
        "Profil atanmazsa eski LevelConfig değerleri güvenli fallback olarak kullanılır."
    )]
    public DangerBalanceProfile dangerBalanceProfile;

    [Header("NORMAL ENEMY")]
    [Min(0)]
    public int normalEnemyCount = 0;

    [Min(0.01f)]
    public float normalEnemySpawnInterval = 2.5f;

    public DangerLevel normalEnemyDanger =
        DangerLevel.Danger2;

    public bool normalEnemyCustomOverride;

    public NormalEnemyDangerSettings normalEnemyOverride =
        new NormalEnemyDangerSettings();

    [Header("PROJECTILE ENEMY")]
    [Min(0)]
    public int projectileEnemyCount = 0;

    [Min(0.01f)]
    public float projectileEnemySpawnInterval = 5f;

    public DangerLevel projectileEnemyDanger =
        DangerLevel.Danger2;

    public bool projectileEnemyCustomOverride;

    public ProjectileEnemyDangerSettings projectileEnemyOverride =
        new ProjectileEnemyDangerSettings();

    [Header("HUNTER ENEMY")]
    [Min(0)]
    public int hunterEnemyCount = 0;

    [Min(0.01f)]
    public float hunterEnemySpawnInterval = 8f;

    public DangerLevel hunterEnemyDanger =
        DangerLevel.Danger2;

    public bool hunterEnemyCustomOverride;

    public HunterEnemyDangerSettings hunterEnemyOverride =
        new HunterEnemyDangerSettings();

    [Header("BOSS")]
    public bool bossEnabled = false;

    public BossSpawnCondition bossSpawnCondition =
        BossSpawnCondition.Score;

    [Min(0)]
    public int bossSpawnScore = 75;

    [Min(0f)]
    public float bossSpawnTime = 30f;

    public DangerLevel bossDanger =
        DangerLevel.Danger2;

    public bool bossCustomOverride;

    public BossDangerSettings bossOverride =
        new BossDangerSettings();

    [Header("BEACON ENEMY")]
    [Min(0)]
    public int beaconEnemyCount = 0;

    [Min(0f)]
    public float beaconMinSpawnTime = 10f;

    [Min(0f)]
    public float beaconMaxSpawnTime = 20f;

    public DangerLevel beaconEnemyDanger =
        DangerLevel.Danger2;

    public bool beaconEnemyCustomOverride;

    public BeaconEnemyDangerSettings beaconEnemyOverride =
        new BeaconEnemyDangerSettings();

    // Eski LevelConfig assetlerinin davranışını koruyan gizli fallback alanları.
    // Yeni LevelConfigEditor bunları göstermez; yalnızca profil eksikse kullanılırlar.
    [SerializeField, HideInInspector]
    private float normalMinStartSpeed = 1.5f;

    [SerializeField, HideInInspector]
    private float normalMaxStartSpeed = 2.5f;

    [SerializeField, HideInInspector]
    private float normalMaxSpeed = 7f;

    [SerializeField, HideInInspector]
    private float normalSpeedIncreaseRate = 0.1f;

    [SerializeField, HideInInspector]
    private bool normalPredictionEnabled = true;

    [SerializeField, HideInInspector]
    private float normalPredictionDistanceThreshold = 2.5f;

    [SerializeField, HideInInspector]
    private float normalPredictionTime = 0.25f;

    [SerializeField, HideInInspector]
    private float normalMaxPredictionDistance = 1.5f;

    [SerializeField, HideInInspector]
    private bool normalSeparationEnabled = true;

    [SerializeField, HideInInspector]
    private float normalSeparationRadius = 0.75f;

    [SerializeField, HideInInspector]
    private float normalSeparationStrength = 0.65f;

    [SerializeField, HideInInspector]
    private float projectileMoveSpeed = 3f;

    [SerializeField, HideInInspector]
    private float projectileStoppingDistance = 7f;

    [SerializeField, HideInInspector]
    private float projectileRetreatDistance = 4f;

    [SerializeField, HideInInspector]
    private float projectileFireRate = 1.5f;

    [SerializeField, HideInInspector]
    private float projectileSpeed = 6f;

    [SerializeField, HideInInspector]
    private float hunterRepositionTime = 1.2f;

    [SerializeField, HideInInspector]
    private float hunterWarningDuration = 1f;

    [SerializeField, HideInInspector]
    private float hunterChargeSpeed = 15f;

    [SerializeField, HideInInspector]
    private float hunterStunDuration = 1f;

    [SerializeField, HideInInspector]
    private float bossSpeed = 1.2f;

    [SerializeField, HideInInspector]
    private bool bossCanSplit = true;

    [SerializeField, HideInInspector]
    private float bossSplitDelay = 0.8f;

    [SerializeField, HideInInspector]
    private float bossSplitDistance = 1.2f;

    [SerializeField, HideInInspector]
    private float miniBossSpeed = 2.5f;

    [SerializeField, HideInInspector]
    private float beaconBuffDuration = 15f;

    [SerializeField, HideInInspector]
    private float beaconBuffSizeMultiplier = 1.25f;

    [SerializeField, HideInInspector]
    private float beaconNormalSpeedMultiplier = 1.35f;

    [SerializeField, HideInInspector]
    private float beaconNormalMaxSpeedMultiplier = 1f;

    [SerializeField, HideInInspector]
    private float beaconProjectileMoveMultiplier = 1.2f;

    [SerializeField, HideInInspector]
    private float beaconProjectileShotMultiplier = 1.25f;

    [SerializeField, HideInInspector]
    private float beaconProjectileFireMultiplier = 1.25f;

    [SerializeField, HideInInspector]
    private float beaconHunterRepositionMultiplier = 0.8f;

    [SerializeField, HideInInspector]
    private float beaconHunterWarningMultiplier = 0.8f;

    [SerializeField, HideInInspector]
    private float beaconHunterChargeMultiplier = 1.25f;

    [SerializeField, HideInInspector]
    private float beaconHunterStunMultiplier = 0.8f;

    [SerializeField, HideInInspector]
    private float beaconRespawnDelay = 6f;

    [Header("POWER UPS")]
    public bool armorEnabled = false;
    public bool slowEnabled = false;

    [Min(0f)]
    public float armorMinSpawnTime = 15f;

    [Min(0f)]
    public float armorMaxSpawnTime = 30f;

    [Min(0f)]
    public float slowMinSpawnTime = 8f;

    [Min(0f)]
    public float slowMaxSpawnTime = 20f;

    [Header("VERTICAL LASER")]
    public bool verticalLaserEnabled = false;

    public DangerLevel verticalLaserDanger =
        DangerLevel.Danger2;

    public bool verticalLaserCustomOverride;

    public LaserDangerSettings verticalLaserOverride =
        new LaserDangerSettings();

    [Header("HORIZONTAL LASER")]
    public bool horizontalLaserEnabled = false;

    public DangerLevel horizontalLaserDanger =
        DangerLevel.Danger2;

    public bool horizontalLaserCustomOverride;

    public LaserDangerSettings horizontalLaserOverride =
        new LaserDangerSettings();

    [Header("BOMBS")]
    public bool bombTrapEnabled = false;

    public DangerLevel bombDanger =
        DangerLevel.Danger2;

    public bool bombCustomOverride;

    public BombDangerSettings bombOverride =
        new BombDangerSettings();

    [SerializeField, HideInInspector]
    private float verticalLaserMinSpawnTime = 8f;

    [SerializeField, HideInInspector]
    private float verticalLaserMaxSpawnTime = 25f;

    [SerializeField, HideInInspector]
    private float verticalLaserWarningDuration = 2f;

    [SerializeField, HideInInspector]
    private float verticalLaserLifeTime = 1.5f;

    [SerializeField, HideInInspector]
    private float verticalLaserWidth = 0.5f;

    [SerializeField, HideInInspector]
    private float verticalLaserHeightExtra = 1f;

    [SerializeField, HideInInspector]
    private float horizontalLaserMinSpawnTime = 8f;

    [SerializeField, HideInInspector]
    private float horizontalLaserMaxSpawnTime = 25f;

    [SerializeField, HideInInspector]
    private float horizontalLaserWarningDuration = 2f;

    [SerializeField, HideInInspector]
    private float horizontalLaserLifeTime = 1.5f;

    [SerializeField, HideInInspector]
    private float horizontalLaserWidth = 0.5f;

    [SerializeField, HideInInspector]
    private float horizontalLaserWidthExtra = 1f;

    [SerializeField, HideInInspector]
    private float bombMinSpawnTime = 6f;

    [SerializeField, HideInInspector]
    private float bombMaxSpawnTime = 14f;

    [SerializeField, HideInInspector]
    private int maxBombCount = 3;



    public bool HasDangerProfile =>
        dangerBalanceProfile != null;

    public NormalEnemyDangerSettings ResolveNormalEnemyDanger()
    {
        NormalEnemyDangerSettings result;

        if (normalEnemyCustomOverride && normalEnemyOverride != null)
        {
            result = normalEnemyOverride.Clone();
        }
        else if (dangerBalanceProfile != null)
        {
            result = dangerBalanceProfile
                .GetNormalEnemy(normalEnemyDanger)
                .Clone();
        }
        else
        {
            result = new NormalEnemyDangerSettings
            {
                minStartSpeed = normalMinStartSpeed,
                maxStartSpeed = normalMaxStartSpeed,
                maxSpeed = normalMaxSpeed,
                speedIncreaseRate = normalSpeedIncreaseRate,
                predictionEnabled = normalPredictionEnabled,
                predictionDistanceThreshold = normalPredictionDistanceThreshold,
                predictionTime = normalPredictionTime,
                maxPredictionDistance = normalMaxPredictionDistance,
                separationEnabled = normalSeparationEnabled,
                separationRadius = normalSeparationRadius,
                separationStrength = normalSeparationStrength
            };
        }

        result.Sanitize();
        return result;
    }

    public ProjectileEnemyDangerSettings ResolveProjectileEnemyDanger()
    {
        ProjectileEnemyDangerSettings result;

        if (projectileEnemyCustomOverride && projectileEnemyOverride != null)
        {
            result = projectileEnemyOverride.Clone();
        }
        else if (dangerBalanceProfile != null)
        {
            result = dangerBalanceProfile
                .GetProjectileEnemy(projectileEnemyDanger)
                .Clone();
        }
        else
        {
            result = new ProjectileEnemyDangerSettings
            {
                moveSpeed = projectileMoveSpeed,
                stoppingDistance = projectileStoppingDistance,
                retreatDistance = projectileRetreatDistance,
                fireRate = projectileFireRate,
                projectileSpeed = projectileSpeed
            };
        }

        result.Sanitize();
        return result;
    }

    public HunterEnemyDangerSettings ResolveHunterEnemyDanger()
    {
        HunterEnemyDangerSettings result;

        if (hunterEnemyCustomOverride && hunterEnemyOverride != null)
        {
            result = hunterEnemyOverride.Clone();
        }
        else if (dangerBalanceProfile != null)
        {
            result = dangerBalanceProfile
                .GetHunterEnemy(hunterEnemyDanger)
                .Clone();
        }
        else
        {
            result = new HunterEnemyDangerSettings
            {
                repositionTime = hunterRepositionTime,
                warningDuration = hunterWarningDuration,
                chargeSpeed = hunterChargeSpeed,
                stunDuration = hunterStunDuration
            };
        }

        result.Sanitize();
        return result;
    }

    public BossDangerSettings ResolveBossDanger()
    {
        BossDangerSettings result;

        if (bossCustomOverride && bossOverride != null)
        {
            result = bossOverride.Clone();
        }
        else if (dangerBalanceProfile != null)
        {
            result = dangerBalanceProfile
                .GetBoss(bossDanger)
                .Clone();
        }
        else
        {
            result = new BossDangerSettings
            {
                speed = bossSpeed,
                canSplit = bossCanSplit,
                splitDelay = bossSplitDelay,
                splitDistance = bossSplitDistance,
                miniBossSpeed = miniBossSpeed
            };
        }

        result.Sanitize();
        return result;
    }

    public BeaconEnemyDangerSettings ResolveBeaconEnemyDanger()
    {
        BeaconEnemyDangerSettings result;

        if (beaconEnemyCustomOverride && beaconEnemyOverride != null)
        {
            result = beaconEnemyOverride.Clone();
        }
        else if (dangerBalanceProfile != null)
        {
            result = dangerBalanceProfile
                .GetBeaconEnemy(beaconEnemyDanger)
                .Clone();
        }
        else
        {
            result = new BeaconEnemyDangerSettings
            {
                buffDuration = beaconBuffDuration,
                buffSizeMultiplier = beaconBuffSizeMultiplier,
                normalSpeedMultiplier = beaconNormalSpeedMultiplier,
                normalMaxSpeedMultiplier = beaconNormalMaxSpeedMultiplier,
                projectileMoveMultiplier = beaconProjectileMoveMultiplier,
                projectileShotMultiplier = beaconProjectileShotMultiplier,
                projectileFireMultiplier = beaconProjectileFireMultiplier,
                hunterRepositionMultiplier = beaconHunterRepositionMultiplier,
                hunterWarningMultiplier = beaconHunterWarningMultiplier,
                hunterChargeMultiplier = beaconHunterChargeMultiplier,
                hunterStunMultiplier = beaconHunterStunMultiplier,
                respawnDelay = beaconRespawnDelay
            };
        }

        result.Sanitize();
        return result;
    }

    public LaserDangerSettings ResolveVerticalLaserDanger()
    {
        LaserDangerSettings result;

        if (verticalLaserCustomOverride && verticalLaserOverride != null)
        {
            result = verticalLaserOverride.Clone();
        }
        else if (dangerBalanceProfile != null)
        {
            result = dangerBalanceProfile
                .GetVerticalLaser(verticalLaserDanger)
                .Clone();
        }
        else
        {
            result = new LaserDangerSettings
            {
                minSpawnTime = verticalLaserMinSpawnTime,
                maxSpawnTime = verticalLaserMaxSpawnTime,
                warningDuration = verticalLaserWarningDuration,
                lifeTime = verticalLaserLifeTime,
                width = verticalLaserWidth,
                sizeExtra = verticalLaserHeightExtra
            };
        }

        result.Sanitize();
        return result;
    }

    public LaserDangerSettings ResolveHorizontalLaserDanger()
    {
        LaserDangerSettings result;

        if (horizontalLaserCustomOverride && horizontalLaserOverride != null)
        {
            result = horizontalLaserOverride.Clone();
        }
        else if (dangerBalanceProfile != null)
        {
            result = dangerBalanceProfile
                .GetHorizontalLaser(horizontalLaserDanger)
                .Clone();
        }
        else
        {
            result = new LaserDangerSettings
            {
                minSpawnTime = horizontalLaserMinSpawnTime,
                maxSpawnTime = horizontalLaserMaxSpawnTime,
                warningDuration = horizontalLaserWarningDuration,
                lifeTime = horizontalLaserLifeTime,
                width = horizontalLaserWidth,
                sizeExtra = horizontalLaserWidthExtra
            };
        }

        result.Sanitize();
        return result;
    }

    public BombDangerSettings ResolveBombDanger()
    {
        BombDangerSettings result;

        if (bombCustomOverride && bombOverride != null)
        {
            result = bombOverride.Clone();
        }
        else if (dangerBalanceProfile != null)
        {
            result = dangerBalanceProfile
                .GetBomb(bombDanger)
                .Clone();
        }
        else
        {
            result = new BombDangerSettings
            {
                minSpawnTime = bombMinSpawnTime,
                maxSpawnTime = bombMaxSpawnTime,
                maxBombCount = maxBombCount,
                spawnSafeTime = 0.35f
            };
        }

        result.Sanitize();
        return result;
    }

    public float GetActiveDangerAverage()
    {
        int total = 0;
        int count = 0;

        AddDanger(normalEnemyCount > 0, normalEnemyDanger, ref total, ref count);
        AddDanger(projectileEnemyCount > 0, projectileEnemyDanger, ref total, ref count);
        AddDanger(hunterEnemyCount > 0, hunterEnemyDanger, ref total, ref count);
        AddDanger(beaconEnemyCount > 0, beaconEnemyDanger, ref total, ref count);
        AddDanger(bossEnabled, bossDanger, ref total, ref count);
        AddDanger(verticalLaserEnabled, verticalLaserDanger, ref total, ref count);
        AddDanger(horizontalLaserEnabled, horizontalLaserDanger, ref total, ref count);
        AddDanger(bombTrapEnabled, bombDanger, ref total, ref count);

        return count > 0
            ? (float)total / count
            : 0f;
    }

    private static void AddDanger(
        bool active,
        DangerLevel level,
        ref int total,
        ref int count)
    {
        if (!active)
            return;

        total += (int)DangerLevelUtility.Sanitize(level);
        count++;
    }

    /*
     * Runtime sistemleri bu ortak değerleri kullanır. Böylece win condition
     * kontrolleri farklı scriptlerde birbirinden kopmaz.
     */
    public bool UsesScore =>
        winCondition == WinConditionType.ReachScore ||
        winCondition == WinConditionType.ReachScoreWithinTime;

    public bool UsesTime =>
        winCondition == WinConditionType.SurviveTime ||
        winCondition == WinConditionType.ReachScoreWithinTime;

    public bool IsPureSurvivalMode =>
        winCondition == WinConditionType.SurviveTime;

    public bool IsTimedScoreMode =>
        winCondition == WinConditionType.ReachScoreWithinTime;

    public bool CoinsEnabled => UsesScore;

    public bool EffectiveComboEnabled =>
        UsesScore && comboEnabled;

    public bool ScoreHUDEnabled => UsesScore;

    public bool TimerHUDEnabled => UsesTime;

    public bool CanSaveBestTime => UsesScore;

    public BossSpawnCondition EffectiveBossSpawnCondition
    {
        get
        {
            switch (winCondition)
            {
                case WinConditionType.ReachScore:
                    return BossSpawnCondition.Score;

                case WinConditionType.SurviveTime:
                    return BossSpawnCondition.Time;

                case WinConditionType.ReachScoreWithinTime:
                    return bossSpawnCondition;

                default:
                    return BossSpawnCondition.Score;
            }
        }
    }

    public int SafeWinScore => Mathf.Max(1, winScore);

    public float SafeTimeLimit => Mathf.Max(0.1f, timeLimit);

    public int SafeBossSpawnScore =>
        Mathf.Clamp(bossSpawnScore, 0, SafeWinScore);

    public float SafeBossSpawnTime =>
        Mathf.Clamp(bossSpawnTime, 0f, SafeTimeLimit);

    public int SafeMissionDifficulty =>
        Mathf.Clamp(missionDifficulty, 0, 5);

    public string EffectiveBriefingTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(briefingTitle))
                return briefingTitle.Trim();

            if (!string.IsNullOrWhiteSpace(levelName))
                return levelName.Trim();

            return $"LEVEL {levelNumber}";
        }
    }

    public string GetDefaultModeDescription()
    {
        switch (winCondition)
        {
            case WinConditionType.ReachScore:
                return "SCORE MISSION";

            case WinConditionType.SurviveTime:
                return "SURVIVAL MISSION";

            case WinConditionType.ReachScoreWithinTime:
                return "TIMED SCORE MISSION";

            default:
                return "MISSION";
        }
    }

    public string GetEffectiveModeDescription()
    {
        if (!string.IsNullOrWhiteSpace(briefingModeDescription))
            return briefingModeDescription.Trim();

        return GetDefaultModeDescription();
    }

    public string GetDefaultObjectiveDescription()
    {
        switch (winCondition)
        {
            case WinConditionType.ReachScore:
                return $"Collect {SafeWinScore} points to complete the mission.";

            case WinConditionType.SurviveTime:
                return $"Stay alive for {FormatSeconds(SafeTimeLimit)}. Coins and combos are disabled in this mission.";

            case WinConditionType.ReachScoreWithinTime:
                return $"Collect {SafeWinScore} points before the {FormatSeconds(SafeTimeLimit)} countdown reaches zero.";

            default:
                return "Complete the mission objective.";
        }
    }

    public string GetEffectiveObjectiveDescription()
    {
        if (!string.IsNullOrWhiteSpace(briefingObjectiveDescription))
            return briefingObjectiveDescription.Trim();

        return GetDefaultObjectiveDescription();
    }

    private static string FormatSeconds(float seconds)
    {
        float safeSeconds = Mathf.Max(0f, seconds);

        if (Mathf.Approximately(safeSeconds, Mathf.Round(safeSeconds)))
            return $"{Mathf.RoundToInt(safeSeconds)} seconds";

        return $"{safeSeconds:0.#} seconds";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (mechanicProgression == null)
            mechanicProgression = new LevelMechanicProgression();

        levelNumber = Mathf.Max(0, levelNumber);
        winScore = Mathf.Max(1, winScore);
        timeLimit = Mathf.Max(0.1f, timeLimit);

        missionDifficulty = Mathf.Clamp(
            missionDifficulty,
            0,
            5
        );

        bossSpawnScore = Mathf.Max(0, bossSpawnScore);
        bossSpawnTime = Mathf.Max(0f, bossSpawnTime);

        if (winCondition == WinConditionType.ReachScore)
        {
            bossSpawnCondition = BossSpawnCondition.Score;
        }
        else if (winCondition == WinConditionType.SurviveTime)
        {
            bossSpawnCondition = BossSpawnCondition.Time;
        }

        normalEnemyDanger = DangerLevelUtility.Sanitize(normalEnemyDanger);
        projectileEnemyDanger = DangerLevelUtility.Sanitize(projectileEnemyDanger);
        hunterEnemyDanger = DangerLevelUtility.Sanitize(hunterEnemyDanger);
        bossDanger = DangerLevelUtility.Sanitize(bossDanger);
        beaconEnemyDanger = DangerLevelUtility.Sanitize(beaconEnemyDanger);
        verticalLaserDanger = DangerLevelUtility.Sanitize(verticalLaserDanger);
        horizontalLaserDanger = DangerLevelUtility.Sanitize(horizontalLaserDanger);
        bombDanger = DangerLevelUtility.Sanitize(bombDanger);

        if (normalEnemyOverride == null)
            normalEnemyOverride = new NormalEnemyDangerSettings();
        if (projectileEnemyOverride == null)
            projectileEnemyOverride = new ProjectileEnemyDangerSettings();
        if (hunterEnemyOverride == null)
            hunterEnemyOverride = new HunterEnemyDangerSettings();
        if (bossOverride == null)
            bossOverride = new BossDangerSettings();
        if (beaconEnemyOverride == null)
            beaconEnemyOverride = new BeaconEnemyDangerSettings();
        if (verticalLaserOverride == null)
            verticalLaserOverride = new LaserDangerSettings();
        if (horizontalLaserOverride == null)
            horizontalLaserOverride = new LaserDangerSettings();
        if (bombOverride == null)
            bombOverride = new BombDangerSettings();

        normalEnemyOverride.Sanitize();
        projectileEnemyOverride.Sanitize();
        hunterEnemyOverride.Sanitize();
        bossOverride.Sanitize();
        beaconEnemyOverride.Sanitize();
        verticalLaserOverride.Sanitize();
        horizontalLaserOverride.Sanitize();
        bombOverride.Sanitize();

        normalEnemySpawnInterval = Mathf.Max(0.01f, normalEnemySpawnInterval);
        projectileEnemySpawnInterval = Mathf.Max(0.01f, projectileEnemySpawnInterval);
        hunterEnemySpawnInterval = Mathf.Max(0.01f, hunterEnemySpawnInterval);
        beaconMinSpawnTime = Mathf.Max(0f, beaconMinSpawnTime);
        beaconMaxSpawnTime = Mathf.Max(beaconMinSpawnTime, beaconMaxSpawnTime);

        // Eski serialized alan tutuluyor fakat artık win condition ile senkron.
        showGameTimerHUD = UsesTime;
    }
#endif
}