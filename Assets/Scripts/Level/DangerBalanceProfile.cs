using System;
using UnityEngine;

public enum DangerLevel
{
    Danger1 = 1,
    Danger2 = 2,
    Danger3 = 3,
    Danger4 = 4,
    Danger5 = 5
}

public static class DangerLevelUtility
{
    public const int TierCount = 5;

    public static int ToIndex(DangerLevel level)
    {
        return Mathf.Clamp((int)level - 1, 0, TierCount - 1);
    }

    public static DangerLevel Sanitize(DangerLevel level)
    {
        return (DangerLevel)Mathf.Clamp((int)level, 1, TierCount);
    }

    public static string GetShortLabel(DangerLevel level)
    {
        return $"D{(int)Sanitize(level)}";
    }

    public static string GetDisplayName(DangerLevel level)
    {
        switch (Sanitize(level))
        {
            case DangerLevel.Danger1:
                return "D1 - INTRODUCTION";
            case DangerLevel.Danger2:
                return "D2 - STANDARD";
            case DangerLevel.Danger3:
                return "D3 - PRESSURE";
            case DangerLevel.Danger4:
                return "D4 - SEVERE";
            case DangerLevel.Danger5:
                return "D5 - EXTREME";
            default:
                return "D2 - STANDARD";
        }
    }

    public static string GetDescription(DangerLevel level)
    {
        switch (Sanitize(level))
        {
            case DangerLevel.Danger1:
                return "Readable first-use tuning that still creates a real threat.";
            case DangerLevel.Danger2:
                return "Reliable baseline pressure for early and mid-game encounters.";
            case DangerLevel.Danger3:
                return "Sustained pressure for mechanics the player already understands.";
            case DangerLevel.Danger4:
                return "Severe late-game tuning with shorter reaction windows.";
            case DangerLevel.Danger5:
                return "Extreme endgame tuning for low counts and final challenges.";
            default:
                return string.Empty;
        }
    }
}

[Serializable]
public class NormalEnemyDangerSettings
{
    [Min(0f)] public float minStartSpeed = 1.5f;
    [Min(0f)] public float maxStartSpeed = 2.5f;
    [Min(0f)] public float maxSpeed = 7f;
    [Min(0f)] public float speedIncreaseRate = 0.1f;

    public bool predictionEnabled = true;
    [Min(0f)] public float predictionDistanceThreshold = 2.5f;
    [Min(0f)] public float predictionTime = 0.25f;
    [Min(0f)] public float maxPredictionDistance = 1.5f;

    public bool separationEnabled = true;
    [Min(0f)] public float separationRadius = 0.75f;
    [Min(0f)] public float separationStrength = 0.65f;

    public NormalEnemyDangerSettings Clone()
    {
        return (NormalEnemyDangerSettings)MemberwiseClone();
    }

    public void Sanitize()
    {
        minStartSpeed = Mathf.Max(0f, minStartSpeed);
        maxStartSpeed = Mathf.Max(minStartSpeed, maxStartSpeed);
        maxSpeed = Mathf.Max(maxStartSpeed, maxSpeed);
        speedIncreaseRate = Mathf.Max(0f, speedIncreaseRate);
        predictionDistanceThreshold = Mathf.Max(0f, predictionDistanceThreshold);
        predictionTime = Mathf.Max(0f, predictionTime);
        maxPredictionDistance = Mathf.Max(0f, maxPredictionDistance);
        separationRadius = Mathf.Max(0f, separationRadius);
        separationStrength = Mathf.Max(0f, separationStrength);
    }
}

[Serializable]
public class ProjectileEnemyDangerSettings
{
    [Min(0f)] public float moveSpeed = 3f;
    [Min(0f)] public float stoppingDistance = 7f;
    [Min(0f)] public float retreatDistance = 4f;
    [Min(0.01f)] public float fireRate = 1.5f;
    [Min(0f)] public float projectileSpeed = 6f;

    [Min(1)] public int shotsPerBurst = 3;
    [Min(0.05f)] public float reloadDuration = 3f;
    [Min(0f)] public float reloadRetreatDistance = 9f;
    [Min(0.1f)] public float reloadMoveSpeedMultiplier = 1.4f;

    public bool strafeEnabled = true;
    [Min(0f)] public float strafeSpeedMultiplier = 0.65f;
    [Min(0f)] public float strafeDirectionChangeMinTime = 1.5f;
    [Min(0f)] public float strafeDirectionChangeMaxTime = 3f;
    [Min(0f)] public float strafeDistanceTolerance = 0.6f;

    public bool predictiveAimEnabled = true;
    [Min(0f)] public float predictionTime = 0.25f;
    [Min(0f)] public float maxPredictionDistance = 1.5f;
    [Min(0f)] public float predictionDistanceThreshold = 2.5f;

    public bool separationEnabled = true;
    [Min(0f)] public float separationRadius = 0.9f;
    [Min(0f)] public float separationStrength = 0.45f;

    public ProjectileEnemyDangerSettings Clone()
    {
        return (ProjectileEnemyDangerSettings)MemberwiseClone();
    }

    public void Sanitize()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        retreatDistance = Mathf.Clamp(retreatDistance, 0f, stoppingDistance);
        fireRate = Mathf.Max(0.01f, fireRate);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        if (shotsPerBurst <= 0)
            shotsPerBurst = 3;

        if (reloadDuration <= 0f)
            reloadDuration = 3f;

        if (reloadRetreatDistance <= 0f)
            reloadRetreatDistance = Mathf.Max(stoppingDistance, 9f);
        else
            reloadRetreatDistance = Mathf.Max(stoppingDistance, reloadRetreatDistance);

        if (reloadMoveSpeedMultiplier <= 0f)
            reloadMoveSpeedMultiplier = 1.4f;
        strafeSpeedMultiplier = Mathf.Max(0f, strafeSpeedMultiplier);
        strafeDirectionChangeMinTime = Mathf.Max(0f, strafeDirectionChangeMinTime);
        strafeDirectionChangeMaxTime = Mathf.Max(
            strafeDirectionChangeMinTime,
            strafeDirectionChangeMaxTime
        );
        strafeDistanceTolerance = Mathf.Max(0f, strafeDistanceTolerance);
        predictionTime = Mathf.Max(0f, predictionTime);
        maxPredictionDistance = Mathf.Max(0f, maxPredictionDistance);
        predictionDistanceThreshold = Mathf.Max(0f, predictionDistanceThreshold);
        separationRadius = Mathf.Max(0f, separationRadius);
        separationStrength = Mathf.Max(0f, separationStrength);
    }
}

[Serializable]
public class HunterEnemyDangerSettings
{
    [Min(0f)] public float prepareDistance = 6f;
    [Min(0f)] public float repositionTime = 1.2f;
    [Min(0f)] public float recoveryTime = 1.2f;
    [Min(0f)] public float warningDuration = 1f;
    [Min(0f)] public float chargeSpeed = 15f;
    [Min(0.01f)] public float maxChargeTime = 0.65f;
    [Min(0f)] public float stunDuration = 1f;

    public HunterEnemyDangerSettings Clone()
    {
        return (HunterEnemyDangerSettings)MemberwiseClone();
    }

    public void Sanitize()
    {
        prepareDistance = Mathf.Max(0f, prepareDistance);
        repositionTime = Mathf.Max(0f, repositionTime);
        recoveryTime = Mathf.Max(0f, recoveryTime);
        warningDuration = Mathf.Max(0f, warningDuration);
        chargeSpeed = Mathf.Max(0f, chargeSpeed);
        maxChargeTime = Mathf.Max(0.01f, maxChargeTime);
        stunDuration = Mathf.Max(0f, stunDuration);
    }
}

[Serializable]
public class BossDangerSettings
{
    [Min(0f)] public float speed = 1.2f;
    [Min(0f)] public float directionSmoothness = 7f;
    public bool canSplit = true;
    [Min(0f)] public float splitDelay = 0.8f;
    [Min(0f)] public float splitDistance = 1.2f;
    [Min(0f)] public float miniBossSpeed = 2.5f;

    public BossDangerSettings Clone()
    {
        return (BossDangerSettings)MemberwiseClone();
    }

    public void Sanitize()
    {
        speed = Mathf.Max(0f, speed);
        directionSmoothness = Mathf.Max(0f, directionSmoothness);
        splitDelay = Mathf.Max(0f, splitDelay);
        splitDistance = Mathf.Max(0f, splitDistance);
        miniBossSpeed = Mathf.Max(0f, miniBossSpeed);
    }
}

[Serializable]
public class BeaconEnemyDangerSettings
{
    [Min(0f)] public float activationDelay = 3f;
    [Min(0.05f)] public float pulseInterval = 2f;
    [Min(0.05f)] public float retargetInterval = 0.5f;
    [Min(0f)] public float targetStopDistance = 1.5f;

    [Min(0.1f)] public float buffDuration = 15f;
    [Min(0.1f)] public float buffSizeMultiplier = 1.25f;
    [Min(0.1f)] public float normalSpeedMultiplier = 1.35f;
    [Min(0.1f)] public float normalMaxSpeedMultiplier = 1.25f;
    [Min(0.1f)] public float projectileMoveMultiplier = 1.2f;
    [Min(0.1f)] public float projectileShotMultiplier = 1.25f;
    [Min(0.1f)] public float projectileFireMultiplier = 1.25f;
    [Min(0.1f)] public float hunterRepositionMultiplier = 0.8f;
    [Min(0.1f)] public float hunterWarningMultiplier = 0.8f;
    [Min(0.1f)] public float hunterChargeMultiplier = 1.25f;
    [Min(0.1f)] public float hunterStunMultiplier = 0.8f;

    [Min(0f)] public float moveSpeed = 3f;
    [Min(0f)] public float safeDistanceFromPlayer = 5f;
    [Min(0f)] public float wanderStrength = 0.6f;
    [Min(0f)] public float respawnDelay = 6f;

    public BeaconEnemyDangerSettings Clone()
    {
        return (BeaconEnemyDangerSettings)MemberwiseClone();
    }

    public void Sanitize()
    {
        activationDelay = Mathf.Max(0f, activationDelay);
        pulseInterval = Mathf.Max(0.05f, pulseInterval);
        retargetInterval = Mathf.Max(0.05f, retargetInterval);
        targetStopDistance = Mathf.Max(0f, targetStopDistance);
        buffDuration = Mathf.Max(0.1f, buffDuration);
        buffSizeMultiplier = Mathf.Max(0.1f, buffSizeMultiplier);
        normalSpeedMultiplier = Mathf.Max(0.1f, normalSpeedMultiplier);
        normalMaxSpeedMultiplier = Mathf.Max(0.1f, normalMaxSpeedMultiplier);
        projectileMoveMultiplier = Mathf.Max(0.1f, projectileMoveMultiplier);
        projectileShotMultiplier = Mathf.Max(0.1f, projectileShotMultiplier);
        projectileFireMultiplier = Mathf.Max(0.1f, projectileFireMultiplier);
        hunterRepositionMultiplier = Mathf.Max(0.1f, hunterRepositionMultiplier);
        hunterWarningMultiplier = Mathf.Max(0.1f, hunterWarningMultiplier);
        hunterChargeMultiplier = Mathf.Max(0.1f, hunterChargeMultiplier);
        hunterStunMultiplier = Mathf.Max(0.1f, hunterStunMultiplier);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        safeDistanceFromPlayer = Mathf.Max(0f, safeDistanceFromPlayer);
        wanderStrength = Mathf.Max(0f, wanderStrength);
        normalMaxSpeedMultiplier = 1f;
        respawnDelay = Mathf.Max(0f, respawnDelay);
    }
}

[Serializable]
public class LaserDangerSettings
{
    [Min(0f)] public float minSpawnTime = 8f;
    [Min(0f)] public float maxSpawnTime = 25f;
    [Min(0f)] public float warningDuration = 2f;
    [Min(0.1f)] public float lifeTime = 1.5f;
    [Min(0.01f)] public float width = 0.5f;
    [Min(0f)] public float sizeExtra = 1f;

    public LaserDangerSettings Clone()
    {
        return (LaserDangerSettings)MemberwiseClone();
    }

    public void Sanitize()
    {
        minSpawnTime = Mathf.Max(0f, minSpawnTime);
        maxSpawnTime = Mathf.Max(minSpawnTime, maxSpawnTime);
        warningDuration = Mathf.Max(0f, warningDuration);
        lifeTime = Mathf.Max(0.1f, lifeTime);
        width = Mathf.Max(0.01f, width);
        sizeExtra = Mathf.Max(0f, sizeExtra);
    }
}

[Serializable]
public class BombDangerSettings
{
    [Min(0f)] public float minSpawnTime = 6f;
    [Min(0f)] public float maxSpawnTime = 14f;
    [Min(0)] public int maxBombCount = 3;
    [Min(0f)] public float spawnSafeTime = 0.35f;

    public BombDangerSettings Clone()
    {
        return (BombDangerSettings)MemberwiseClone();
    }

    public void Sanitize()
    {
        minSpawnTime = Mathf.Max(0f, minSpawnTime);
        maxSpawnTime = Mathf.Max(minSpawnTime, maxSpawnTime);
        maxBombCount = Mathf.Max(0, maxBombCount);
        spawnSafeTime = Mathf.Max(0f, spawnSafeTime);
    }
}

[CreateAssetMenu(
    fileName = "Default_Danger_Balance",
    menuName = "Fateful Rush/Balance/Danger Balance Profile"
)]
public class DangerBalanceProfile : ScriptableObject
{
    [SerializeField, Min(1)]
    private int profileVersion = 3;

    [SerializeField]
    private NormalEnemyDangerSettings[] normalEnemyLevels;

    [SerializeField]
    private ProjectileEnemyDangerSettings[] projectileEnemyLevels;

    [SerializeField]
    private HunterEnemyDangerSettings[] hunterEnemyLevels;

    [SerializeField]
    private BossDangerSettings[] bossLevels;

    [SerializeField]
    private BeaconEnemyDangerSettings[] beaconEnemyLevels;

    [SerializeField]
    private LaserDangerSettings[] verticalLaserLevels;

    [SerializeField]
    private LaserDangerSettings[] horizontalLaserLevels;

    [SerializeField]
    private BombDangerSettings[] bombLevels;

    public int ProfileVersion => profileVersion;

    public NormalEnemyDangerSettings GetNormalEnemy(DangerLevel level)
    {
        return GetTier(normalEnemyLevels, level, CreateDefaultNormalEnemyLevels);
    }

    public ProjectileEnemyDangerSettings GetProjectileEnemy(DangerLevel level)
    {
        return GetTier(projectileEnemyLevels, level, CreateDefaultProjectileEnemyLevels);
    }

    public HunterEnemyDangerSettings GetHunterEnemy(DangerLevel level)
    {
        return GetTier(hunterEnemyLevels, level, CreateDefaultHunterEnemyLevels);
    }

    public BossDangerSettings GetBoss(DangerLevel level)
    {
        return GetTier(bossLevels, level, CreateDefaultBossLevels);
    }

    public BeaconEnemyDangerSettings GetBeaconEnemy(DangerLevel level)
    {
        return GetTier(beaconEnemyLevels, level, CreateDefaultBeaconEnemyLevels);
    }

    public LaserDangerSettings GetVerticalLaser(DangerLevel level)
    {
        return GetTier(verticalLaserLevels, level, CreateDefaultVerticalLaserLevels);
    }

    public LaserDangerSettings GetHorizontalLaser(DangerLevel level)
    {
        return GetTier(horizontalLaserLevels, level, CreateDefaultHorizontalLaserLevels);
    }

    public BombDangerSettings GetBomb(DangerLevel level)
    {
        return GetTier(bombLevels, level, CreateDefaultBombLevels);
    }

    public void ResetToBalancedDefaults()
    {
        profileVersion = 3;
        normalEnemyLevels = CreateDefaultNormalEnemyLevels();
        projectileEnemyLevels = CreateDefaultProjectileEnemyLevels();
        hunterEnemyLevels = CreateDefaultHunterEnemyLevels();
        bossLevels = CreateDefaultBossLevels();
        beaconEnemyLevels = CreateDefaultBeaconEnemyLevels();
        verticalLaserLevels = CreateDefaultVerticalLaserLevels();
        horizontalLaserLevels = CreateDefaultHorizontalLaserLevels();
        bombLevels = CreateDefaultBombLevels();
    }

    private void OnEnable()
    {
        EnsureValidArrays();
        UpgradeProfileIfNeeded();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureValidArrays();
        UpgradeProfileIfNeeded();
        SanitizeAll();
    }
#endif

    private void EnsureValidArrays()
    {
        normalEnemyLevels = EnsureArray(normalEnemyLevels, CreateDefaultNormalEnemyLevels);
        projectileEnemyLevels = EnsureArray(projectileEnemyLevels, CreateDefaultProjectileEnemyLevels);
        hunterEnemyLevels = EnsureArray(hunterEnemyLevels, CreateDefaultHunterEnemyLevels);
        bossLevels = EnsureArray(bossLevels, CreateDefaultBossLevels);
        beaconEnemyLevels = EnsureArray(beaconEnemyLevels, CreateDefaultBeaconEnemyLevels);
        verticalLaserLevels = EnsureArray(verticalLaserLevels, CreateDefaultVerticalLaserLevels);
        horizontalLaserLevels = EnsureArray(horizontalLaserLevels, CreateDefaultHorizontalLaserLevels);
        bombLevels = EnsureArray(bombLevels, CreateDefaultBombLevels);
    }

    private void UpgradeProfileIfNeeded()
    {
        if (profileVersion >= 3)
            return;

        ProjectileEnemyDangerSettings[] defaults =
            CreateDefaultProjectileEnemyLevels();

        if (projectileEnemyLevels != null)
        {
            int count = Mathf.Min(
                projectileEnemyLevels.Length,
                defaults.Length
            );

            for (int i = 0; i < count; i++)
            {
                ProjectileEnemyDangerSettings current =
                    projectileEnemyLevels[i];

                ProjectileEnemyDangerSettings fallback =
                    defaults[i];

                if (current == null || fallback == null)
                    continue;

                current.shotsPerBurst =
                    fallback.shotsPerBurst;

                current.reloadDuration =
                    fallback.reloadDuration;

                current.reloadRetreatDistance =
                    fallback.reloadRetreatDistance;

                current.reloadMoveSpeedMultiplier =
                    fallback.reloadMoveSpeedMultiplier;
            }
        }

        profileVersion = 3;
    }

    private void SanitizeAll()
    {
        SanitizeArray(normalEnemyLevels, item => item.Sanitize());
        SanitizeArray(projectileEnemyLevels, item => item.Sanitize());
        SanitizeArray(hunterEnemyLevels, item => item.Sanitize());
        SanitizeArray(bossLevels, item => item.Sanitize());
        SanitizeArray(beaconEnemyLevels, item => item.Sanitize());
        SanitizeArray(verticalLaserLevels, item => item.Sanitize());
        SanitizeArray(horizontalLaserLevels, item => item.Sanitize());
        SanitizeArray(bombLevels, item => item.Sanitize());
    }

    private static T GetTier<T>(
        T[] values,
        DangerLevel level,
        Func<T[]> createDefaults)
        where T : class
    {
        int index = DangerLevelUtility.ToIndex(level);

        if (values != null &&
            values.Length == DangerLevelUtility.TierCount &&
            values[index] != null)
        {
            return values[index];
        }

        return createDefaults()[index];
    }

    private static T[] EnsureArray<T>(
        T[] current,
        Func<T[]> createDefaults)
        where T : class
    {
        T[] defaults = createDefaults();

        if (current == null || current.Length != DangerLevelUtility.TierCount)
        {
            T[] rebuilt = defaults;

            if (current != null)
            {
                int copyCount = Mathf.Min(current.Length, rebuilt.Length);

                for (int i = 0; i < copyCount; i++)
                {
                    if (current[i] != null)
                        rebuilt[i] = current[i];
                }
            }

            return rebuilt;
        }

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i] == null)
                current[i] = defaults[i];
        }

        return current;
    }

    private static void SanitizeArray<T>(T[] values, Action<T> sanitize)
        where T : class
    {
        if (values == null)
            return;

        foreach (T value in values)
        {
            if (value != null)
                sanitize(value);
        }
    }

    private static NormalEnemyDangerSettings[] CreateDefaultNormalEnemyLevels()
    {
        return new[]
        {
            new NormalEnemyDangerSettings { minStartSpeed = 2.5f, maxStartSpeed = 3.3f, maxSpeed = 5.7f, speedIncreaseRate = 0.16f, predictionEnabled = true, predictionDistanceThreshold = 3.2f, predictionTime = 0.34f, maxPredictionDistance = 2.3f, separationEnabled = true, separationRadius = 0.82f, separationStrength = 0.72f },
            new NormalEnemyDangerSettings { minStartSpeed = 2.9f, maxStartSpeed = 3.8f, maxSpeed = 6.0f, speedIncreaseRate = 0.18f, predictionEnabled = true, predictionDistanceThreshold = 3.6f, predictionTime = 0.39f, maxPredictionDistance = 2.8f, separationEnabled = true, separationRadius = 0.88f, separationStrength = 0.79f },
            new NormalEnemyDangerSettings { minStartSpeed = 3.3f, maxStartSpeed = 4.3f, maxSpeed = 6.2f, speedIncreaseRate = 0.21f, predictionEnabled = true, predictionDistanceThreshold = 4.0f, predictionTime = 0.44f, maxPredictionDistance = 3.3f, separationEnabled = true, separationRadius = 0.94f, separationStrength = 0.87f },
            new NormalEnemyDangerSettings { minStartSpeed = 3.7f, maxStartSpeed = 4.8f, maxSpeed = 6.4f, speedIncreaseRate = 0.24f, predictionEnabled = true, predictionDistanceThreshold = 4.4f, predictionTime = 0.50f, maxPredictionDistance = 3.9f, separationEnabled = true, separationRadius = 1.00f, separationStrength = 0.96f },
            new NormalEnemyDangerSettings { minStartSpeed = 4.1f, maxStartSpeed = 5.2f, maxSpeed = 6.55f, speedIncreaseRate = 0.27f, predictionEnabled = true, predictionDistanceThreshold = 4.8f, predictionTime = 0.56f, maxPredictionDistance = 4.5f, separationEnabled = true, separationRadius = 1.05f, separationStrength = 1.04f }
        };
    }

    private static ProjectileEnemyDangerSettings[] CreateDefaultProjectileEnemyLevels()
    {
        return new[]
        {
            new ProjectileEnemyDangerSettings
            {
                moveSpeed = 3.0f,
                stoppingDistance = 7.0f,
                retreatDistance = 4.0f,
                fireRate = 1.65f,
                projectileSpeed = 6.2f,
                shotsPerBurst = 3,
                reloadDuration = 3.25f,
                reloadRetreatDistance = 9.5f,
                reloadMoveSpeedMultiplier = 1.35f,
                strafeEnabled = true,
                strafeSpeedMultiplier = 0.60f,
                strafeDirectionChangeMinTime = 1.6f,
                strafeDirectionChangeMaxTime = 2.8f,
                strafeDistanceTolerance = 0.60f,
                predictiveAimEnabled = false,
                predictionTime = 0.22f,
                maxPredictionDistance = 1.3f,
                predictionDistanceThreshold = 2.5f,
                separationEnabled = true,
                separationRadius = 0.90f,
                separationStrength = 0.45f
            },
            new ProjectileEnemyDangerSettings
            {
                moveSpeed = 3.3f,
                stoppingDistance = 6.8f,
                retreatDistance = 3.9f,
                fireRate = 1.42f,
                projectileSpeed = 6.9f,
                shotsPerBurst = 3,
                reloadDuration = 3.0f,
                reloadRetreatDistance = 9.2f,
                reloadMoveSpeedMultiplier = 1.40f,
                strafeEnabled = true,
                strafeSpeedMultiplier = 0.70f,
                strafeDirectionChangeMinTime = 1.3f,
                strafeDirectionChangeMaxTime = 2.4f,
                strafeDistanceTolerance = 0.60f,
                predictiveAimEnabled = true,
                predictionTime = 0.28f,
                maxPredictionDistance = 1.9f,
                predictionDistanceThreshold = 2.8f,
                separationEnabled = true,
                separationRadius = 0.95f,
                separationStrength = 0.52f
            },
            new ProjectileEnemyDangerSettings
            {
                moveSpeed = 3.6f,
                stoppingDistance = 6.6f,
                retreatDistance = 3.8f,
                fireRate = 1.20f,
                projectileSpeed = 7.7f,
                shotsPerBurst = 4,
                reloadDuration = 2.75f,
                reloadRetreatDistance = 9.0f,
                reloadMoveSpeedMultiplier = 1.45f,
                strafeEnabled = true,
                strafeSpeedMultiplier = 0.80f,
                strafeDirectionChangeMinTime = 1.0f,
                strafeDirectionChangeMaxTime = 2.0f,
                strafeDistanceTolerance = 0.55f,
                predictiveAimEnabled = true,
                predictionTime = 0.35f,
                maxPredictionDistance = 2.6f,
                predictionDistanceThreshold = 3.2f,
                separationEnabled = true,
                separationRadius = 1.00f,
                separationStrength = 0.60f
            },
            new ProjectileEnemyDangerSettings
            {
                moveSpeed = 4.0f,
                stoppingDistance = 6.3f,
                retreatDistance = 3.6f,
                fireRate = 0.98f,
                projectileSpeed = 8.7f,
                shotsPerBurst = 4,
                reloadDuration = 2.45f,
                reloadRetreatDistance = 8.7f,
                reloadMoveSpeedMultiplier = 1.50f,
                strafeEnabled = true,
                strafeSpeedMultiplier = 0.90f,
                strafeDirectionChangeMinTime = 0.8f,
                strafeDirectionChangeMaxTime = 1.6f,
                strafeDistanceTolerance = 0.50f,
                predictiveAimEnabled = true,
                predictionTime = 0.42f,
                maxPredictionDistance = 3.3f,
                predictionDistanceThreshold = 3.6f,
                separationEnabled = true,
                separationRadius = 1.05f,
                separationStrength = 0.68f
            },
            new ProjectileEnemyDangerSettings
            {
                moveSpeed = 4.4f,
                stoppingDistance = 6.0f,
                retreatDistance = 3.4f,
                fireRate = 0.82f,
                projectileSpeed = 9.8f,
                shotsPerBurst = 5,
                reloadDuration = 2.20f,
                reloadRetreatDistance = 8.5f,
                reloadMoveSpeedMultiplier = 1.55f,
                strafeEnabled = true,
                strafeSpeedMultiplier = 1.00f,
                strafeDirectionChangeMinTime = 0.65f,
                strafeDirectionChangeMaxTime = 1.3f,
                strafeDistanceTolerance = 0.45f,
                predictiveAimEnabled = true,
                predictionTime = 0.50f,
                maxPredictionDistance = 4.0f,
                predictionDistanceThreshold = 4.0f,
                separationEnabled = true,
                separationRadius = 1.10f,
                separationStrength = 0.78f
            }
        };
    }

    private static HunterEnemyDangerSettings[] CreateDefaultHunterEnemyLevels()
    {
        return new[]
        {
            new HunterEnemyDangerSettings { prepareDistance = 6.0f, repositionTime = 1.15f, recoveryTime = 1.10f, warningDuration = 1.05f, chargeSpeed = 15.5f, maxChargeTime = 0.68f, stunDuration = 1.05f },
            new HunterEnemyDangerSettings { prepareDistance = 5.9f, repositionTime = 1.00f, recoveryTime = 0.95f, warningDuration = 0.92f, chargeSpeed = 16.8f, maxChargeTime = 0.74f, stunDuration = 0.92f },
            new HunterEnemyDangerSettings { prepareDistance = 5.7f, repositionTime = 0.88f, recoveryTime = 0.82f, warningDuration = 0.80f, chargeSpeed = 18.2f, maxChargeTime = 0.82f, stunDuration = 0.80f },
            new HunterEnemyDangerSettings { prepareDistance = 5.5f, repositionTime = 0.75f, recoveryTime = 0.70f, warningDuration = 0.68f, chargeSpeed = 20.0f, maxChargeTime = 0.90f, stunDuration = 0.68f },
            new HunterEnemyDangerSettings { prepareDistance = 5.3f, repositionTime = 0.64f, recoveryTime = 0.58f, warningDuration = 0.58f, chargeSpeed = 22.0f, maxChargeTime = 0.98f, stunDuration = 0.58f }
        };
    }

    private static BossDangerSettings[] CreateDefaultBossLevels()
    {
        return new[]
        {
            new BossDangerSettings { speed = 4.5f, directionSmoothness = 7.5f, canSplit = false, splitDelay = 0.90f, splitDistance = 1.20f, miniBossSpeed = 3.4f },
            new BossDangerSettings { speed = 4.9f, directionSmoothness = 8.2f, canSplit = true, splitDelay = 0.78f, splitDistance = 1.30f, miniBossSpeed = 3.8f },
            new BossDangerSettings { speed = 5.3f, directionSmoothness = 9.0f, canSplit = true, splitDelay = 0.65f, splitDistance = 1.45f, miniBossSpeed = 4.3f },
            new BossDangerSettings { speed = 5.7f, directionSmoothness = 9.8f, canSplit = true, splitDelay = 0.52f, splitDistance = 1.60f, miniBossSpeed = 4.8f },
            new BossDangerSettings { speed = 6.0f, directionSmoothness = 10.8f, canSplit = true, splitDelay = 0.42f, splitDistance = 1.75f, miniBossSpeed = 5.3f }
        };
    }

    private static BeaconEnemyDangerSettings[] CreateDefaultBeaconEnemyLevels()
    {
        return new[]
        {
            new BeaconEnemyDangerSettings { activationDelay = 3.5f, pulseInterval = 2.4f, retargetInterval = 0.65f, targetStopDistance = 1.7f, buffDuration = 10f, buffSizeMultiplier = 1.10f, normalSpeedMultiplier = 1.10f, normalMaxSpeedMultiplier = 1f, projectileMoveMultiplier = 1.08f, projectileShotMultiplier = 1.10f, projectileFireMultiplier = 1.10f, hunterRepositionMultiplier = 0.92f, hunterWarningMultiplier = 0.92f, hunterChargeMultiplier = 1.10f, hunterStunMultiplier = 0.92f, moveSpeed = 2.7f, safeDistanceFromPlayer = 6.0f, wanderStrength = 0.50f, respawnDelay = 8f },
            new BeaconEnemyDangerSettings { activationDelay = 3.0f, pulseInterval = 2.0f, retargetInterval = 0.55f, targetStopDistance = 1.6f, buffDuration = 12f, buffSizeMultiplier = 1.15f, normalSpeedMultiplier = 1.15f, normalMaxSpeedMultiplier = 1f, projectileMoveMultiplier = 1.12f, projectileShotMultiplier = 1.15f, projectileFireMultiplier = 1.15f, hunterRepositionMultiplier = 0.88f, hunterWarningMultiplier = 0.88f, hunterChargeMultiplier = 1.15f, hunterStunMultiplier = 0.88f, moveSpeed = 3.0f, safeDistanceFromPlayer = 5.8f, wanderStrength = 0.60f, respawnDelay = 7f },
            new BeaconEnemyDangerSettings { activationDelay = 2.5f, pulseInterval = 1.6f, retargetInterval = 0.45f, targetStopDistance = 1.5f, buffDuration = 14f, buffSizeMultiplier = 1.20f, normalSpeedMultiplier = 1.20f, normalMaxSpeedMultiplier = 1f, projectileMoveMultiplier = 1.16f, projectileShotMultiplier = 1.22f, projectileFireMultiplier = 1.22f, hunterRepositionMultiplier = 0.82f, hunterWarningMultiplier = 0.82f, hunterChargeMultiplier = 1.22f, hunterStunMultiplier = 0.82f, moveSpeed = 3.3f, safeDistanceFromPlayer = 5.6f, wanderStrength = 0.70f, respawnDelay = 6f },
            new BeaconEnemyDangerSettings { activationDelay = 2.0f, pulseInterval = 1.25f, retargetInterval = 0.35f, targetStopDistance = 1.4f, buffDuration = 16f, buffSizeMultiplier = 1.25f, normalSpeedMultiplier = 1.27f, normalMaxSpeedMultiplier = 1f, projectileMoveMultiplier = 1.22f, projectileShotMultiplier = 1.30f, projectileFireMultiplier = 1.30f, hunterRepositionMultiplier = 0.76f, hunterWarningMultiplier = 0.76f, hunterChargeMultiplier = 1.30f, hunterStunMultiplier = 0.76f, moveSpeed = 3.6f, safeDistanceFromPlayer = 5.4f, wanderStrength = 0.80f, respawnDelay = 5f },
            new BeaconEnemyDangerSettings { activationDelay = 1.5f, pulseInterval = 0.95f, retargetInterval = 0.28f, targetStopDistance = 1.3f, buffDuration = 18f, buffSizeMultiplier = 1.30f, normalSpeedMultiplier = 1.35f, normalMaxSpeedMultiplier = 1f, projectileMoveMultiplier = 1.30f, projectileShotMultiplier = 1.40f, projectileFireMultiplier = 1.40f, hunterRepositionMultiplier = 0.68f, hunterWarningMultiplier = 0.68f, hunterChargeMultiplier = 1.40f, hunterStunMultiplier = 0.68f, moveSpeed = 4.0f, safeDistanceFromPlayer = 5.2f, wanderStrength = 0.90f, respawnDelay = 4f }
        };
    }

    private static LaserDangerSettings[] CreateDefaultVerticalLaserLevels()
    {
        return CreateDefaultLaserLevels();
    }

    private static LaserDangerSettings[] CreateDefaultHorizontalLaserLevels()
    {
        return CreateDefaultLaserLevels();
    }

    private static LaserDangerSettings[] CreateDefaultLaserLevels()
    {
        return new[]
        {
            new LaserDangerSettings
            {
                minSpawnTime = 10.0f,
                maxSpawnTime = 18.0f,
                warningDuration = 2.20f,
                lifeTime = 1.40f,
                width = 0.50f,
                sizeExtra = 1.00f
            },
            new LaserDangerSettings
            {
                minSpawnTime = 8.0f,
                maxSpawnTime = 15.0f,
                warningDuration = 1.85f,
                lifeTime = 1.55f,
                width = 0.56f,
                sizeExtra = 1.05f
            },
            new LaserDangerSettings
            {
                minSpawnTime = 6.5f,
                maxSpawnTime = 12.0f,
                warningDuration = 1.55f,
                lifeTime = 1.70f,
                width = 0.62f,
                sizeExtra = 1.10f
            },
            new LaserDangerSettings
            {
                minSpawnTime = 5.0f,
                maxSpawnTime = 9.5f,
                warningDuration = 1.30f,
                lifeTime = 1.90f,
                width = 0.70f,
                sizeExtra = 1.18f
            },
            new LaserDangerSettings
            {
                minSpawnTime = 4.0f,
                maxSpawnTime = 7.5f,
                warningDuration = 1.05f,
                lifeTime = 2.10f,
                width = 0.78f,
                sizeExtra = 1.25f
            }
        };
    }

    private static BombDangerSettings[] CreateDefaultBombLevels()
    {
        return new[]
        {
            new BombDangerSettings { minSpawnTime = 7.5f, maxSpawnTime = 12.0f, maxBombCount = 2, spawnSafeTime = 0.65f },
            new BombDangerSettings { minSpawnTime = 6.0f, maxSpawnTime = 10.0f, maxBombCount = 3, spawnSafeTime = 0.52f },
            new BombDangerSettings { minSpawnTime = 4.8f, maxSpawnTime = 8.0f, maxBombCount = 4, spawnSafeTime = 0.42f },
            new BombDangerSettings { minSpawnTime = 3.8f, maxSpawnTime = 6.5f, maxBombCount = 4, spawnSafeTime = 0.34f },
            new BombDangerSettings { minSpawnTime = 3.0f, maxSpawnTime = 5.5f, maxBombCount = 5, spawnSafeTime = 0.28f }
        };
    }
}
