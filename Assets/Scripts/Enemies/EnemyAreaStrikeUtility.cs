using UnityEngine;

public static class EnemyAreaStrikeUtility
{
    public static bool ExecuteStrike(
        Transform attacker,
        Transform player,
        PlayerMovement playerMovement,
        PlayerArmor playerArmor,
        LayerMask coverLayers,
        bool useRadius,
        float radius,
        string deathCause)
    {
        if (GameStateManager.IsGameplayEnded ||
            attacker == null ||
            player == null ||
            playerMovement == null ||
            playerMovement.IsGameOver)
        {
            return false;
        }

        if (playerArmor != null &&
            playerArmor.IsImmune)
        {
            return false;
        }

        Vector2 origin = attacker.position;
        Vector2 playerPosition = player.position;

        if (useRadius)
        {
            float safeRadius = Mathf.Max(0f, radius);

            if ((playerPosition - origin).sqrMagnitude >
                safeRadius * safeRadius)
            {
                RecordAoeEvade(deathCause);
                return false;
            }
        }

        if (IsProtectedByCover(
                origin,
                playerPosition,
                coverLayers))
        {
            RecordAoeEvade(deathCause);
            return false;
        }

        if (playerArmor != null &&
            playerArmor.HasArmor)
        {
            playerArmor.BreakArmor();
            return true;
        }

        playerMovement.GameOver(deathCause);
        return true;
    }


    private static void RecordAoeEvade(string deathCause)
    {
        if (string.IsNullOrWhiteSpace(deathCause))
            return;

        if (deathCause == "BOSS")
        {
            StatsManager.AddBossAoeEvade();
            return;
        }

        if (deathCause == "MINI BOSS")
            StatsManager.AddMiniBossAoeEvade();
    }

    public static bool IsProtectedByCover(
        Vector2 origin,
        Vector2 playerPosition,
        LayerMask coverLayers)
    {
        if (coverLayers.value == 0)
            return false;

        Vector2 toPlayer =
            playerPosition - origin;

        float distance = toPlayer.magnitude;

        if (distance <= 0.001f)
            return false;

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            toPlayer / distance,
            distance,
            coverLayers
        );

        return hit.collider != null;
    }

    public static GameObject SpawnEffect(
        GameObject effectPrefab,
        Vector3 position,
        float scaleMultiplier)
    {
        if (effectPrefab == null)
            return null;

        GameObject effect = Object.Instantiate(
            effectPrefab,
            position,
            Quaternion.identity
        );

        if (effect == null)
            return null;

        float safeScale = Mathf.Max(0f, scaleMultiplier);

        effect.transform.localScale *= safeScale;
        return effect;
    }

    public static void PlaySound(AudioClip clip)
    {
        if (GameStateManager.IsGameplayEnded ||
            clip == null)
        {
            return;
        }

        SoundManager.Instance?.PlayCustomSound(clip);
    }

    public static void PlaySound(
        AudioClip clip,
        Vector3 worldPosition)
    {
        if (GameStateManager.IsGameplayEnded ||
            clip == null)
        {
            return;
        }

        SoundManager.Instance?.PlayCustomSoundAtWorld(
            clip,
            worldPosition
        );
    }
}
