using UnityEngine;

public class PlayerCollisionHandler : MonoBehaviour
{
    private const string UnknownCause = "UNKNOWN";
    private const string LaserWallCause = "LASER WALL";
    private const string BossCause = "BOSS";
    private const string MiniBossCause = "MINI BOSS";
    private const string HunterCause = "HUNTER";
    private const string BlasterCause = "BLASTER";
    private const string LaserBulletCause = "LASER BULLET";
    private const string StalkerCause = "STALKER";

    [Header("References")]
    public PlayerMovement playerMovement;
    public PlayerArmor playerArmor;
    public PlayerCoinCollector coinCollector;
    public GameStateManager gameStateManager;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerArmor == null)
            playerArmor = GetComponent<PlayerArmor>();

        if (coinCollector == null)
            coinCollector = GetComponent<PlayerCoinCollector>();

        if (gameStateManager == null)
            gameStateManager = FindAnyObjectByType<GameStateManager>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsGameOver())
            return;

        if (!collision.gameObject.CompareTag("Enemy"))
            return;

        HandleHit(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsGameOver())
            return;

        if (!other.CompareTag("Enemy") &&
            !other.CompareTag("Laser"))
        {
            return;
        }

        HandleHit(other.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (IsGameOver() ||
            !collision.gameObject.CompareTag("Enemy"))
        {
            return;
        }

        // Fallback for rare missed Enter callbacks. HandleHit already filters
        // reloading Blasters, absorbed Stalkers, immunity and armor.
        HandleHit(collision.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (IsGameOver() ||
            (!other.CompareTag("Enemy") &&
             !other.CompareTag("Laser")))
        {
            return;
        }

        // Trigger Stay protects both Enemy and Laser hazards if a mobile hitch
        // or collider reactivation ever causes the initial Enter to be missed.
        HandleHit(other.gameObject);
    }

    private void HandleHit(GameObject danger)
    {
        if (danger == null)
            return;

        EnemyFollow absorbingStalker =
            danger.GetComponentInParent<EnemyFollow>();

        // Stalkers being pulled into the Boss are no longer active threats.
        // They may pass through the player while travelling to the Boss.
        if (absorbingStalker != null &&
            absorbingStalker.IsBeingAbsorbed)
        {
            return;
        }

        // A Blaster that is reloading is temporarily harmless on body contact.
        // Keep its collider/movement active, but do not kill the player or
        // consume armor until the reload has fully finished.
        ProjectileEnemyFollow blaster =
            danger.GetComponentInParent<ProjectileEnemyFollow>();

        if (blaster != null && blaster.IsReloading)
            return;

        // EnemyProjectile owns player-projectile collision handling.
        // Skipping it here prevents armor from returning a pooled projectile
        // while this component simultaneously destroys the same object.
        if (danger.GetComponentInParent<EnemyProjectile>() != null)
            return;

        if (playerArmor != null && playerArmor.IsImmune)
            return;

        if (playerArmor != null && playerArmor.HasArmor)
        {
            playerArmor.BreakArmor();

            bool isLaser =
                danger.CompareTag("Laser") ||
                danger.GetComponentInParent<LaserWall>() != null;

            if (!isLaser)
            {
                StatsManager.AddArmorEnemyKill();
                RemoveDanger(danger);
            }

            return;
        }

        string deathCause = GetDeathCause(danger);
        int finalScore = coinCollector != null
            ? coinCollector.Score
            : 0;

        if (playerMovement != null)
        {
            playerMovement.GameOver(deathCause);
            return;
        }

        LastDeathInfo.Cause = deathCause;

        if (gameStateManager == null)
        {
            gameStateManager =
                FindAnyObjectByType<GameStateManager>(
                    FindObjectsInactive.Include
                );
        }

        if (gameStateManager != null)
            gameStateManager.GameOver(finalScore, deathCause);
    }

    private void RemoveDanger(GameObject danger)
    {
        GameObject rootDanger = GetDangerRoot(danger);

        if (rootDanger == null)
            return;

        DeathFadeEffect fade =
            rootDanger.GetComponent<DeathFadeEffect>();

        if (fade != null)
        {
            fade.Play();
            return;
        }

        Destroy(rootDanger);
    }

    private string GetDeathCause(GameObject danger)
    {
        if (danger == null)
            return UnknownCause;

        if (danger.CompareTag("Laser") ||
            danger.GetComponentInParent<LaserWall>() != null)
        {
            return LaserWallCause;
        }

        if (danger.GetComponentInParent<BossEnemyFollow>() != null)
            return BossCause;

        if (danger.GetComponentInParent<MiniBossFollow>() != null)
            return MiniBossCause;

        if (danger.GetComponentInParent<HunterEnemyFollow>() != null)
            return HunterCause;

        if (danger.GetComponentInParent<ProjectileEnemyFollow>() != null)
            return BlasterCause;

        if (danger.GetComponentInParent<EnemyProjectile>() != null)
            return LaserBulletCause;

        if (danger.GetComponentInParent<EnemyFollow>() != null)
            return StalkerCause;

        string cleanName = danger.name
            .Replace("(Clone)", "")
            .Trim();

        return string.IsNullOrWhiteSpace(cleanName)
            ? UnknownCause
            : cleanName.ToUpperInvariant();
    }

    private GameObject GetDangerRoot(GameObject danger)
    {
        if (danger == null)
            return null;

        BossEnemyFollow boss =
            danger.GetComponentInParent<BossEnemyFollow>();

        if (boss != null)
            return boss.gameObject;

        MiniBossFollow miniBoss =
            danger.GetComponentInParent<MiniBossFollow>();

        if (miniBoss != null)
            return miniBoss.gameObject;

        HunterEnemyFollow hunter =
            danger.GetComponentInParent<HunterEnemyFollow>();

        if (hunter != null)
            return hunter.gameObject;

        ProjectileEnemyFollow projectileEnemy =
            danger.GetComponentInParent<ProjectileEnemyFollow>();

        if (projectileEnemy != null)
            return projectileEnemy.gameObject;

        EnemyProjectile projectile =
            danger.GetComponentInParent<EnemyProjectile>();

        if (projectile != null)
            return projectile.gameObject;

        EnemyFollow enemy =
            danger.GetComponentInParent<EnemyFollow>();

        if (enemy != null)
            return enemy.gameObject;

        LaserWall laserWall =
            danger.GetComponentInParent<LaserWall>();

        if (laserWall != null)
            return laserWall.gameObject;

        return danger;
    }

    private bool IsGameOver()
    {
        return !GameStateManager.IsGameplayStarted ||
               GameStateManager.IsGameplayEnded ||
               (playerMovement != null &&
                playerMovement.IsGameOver);
    }
}
