using System.Collections;
using UnityEngine;

public class SpaceBomb : MonoBehaviour
{
    [Header("Explosion")]
    public GameObject explosionEffectPrefab;
    public AudioClip explosionSound;

    [Header("Spawn Safety")]
    public float spawnSafeTime = 0.35f;

    private bool triggered;
    private Collider2D bombCollider;

    private void Awake()
    {
        bombCollider = GetComponent<Collider2D>();

        SetColliderEnabled(false);
    }

    private IEnumerator Start()
    {
        float safeTime = Mathf.Max(0f, spawnSafeTime);

        if (safeTime > 0f)
            yield return new WaitForSeconds(safeTime);

        if (!triggered)
            SetColliderEnabled(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Safe fallback if enabling the bomb collider while the Player is
        // already overlapping it does not produce Enter on a specific device.
        TryTrigger(other);
    }

    private void TryTrigger(Collider2D other)
    {
        if (triggered ||
            !GameStateManager.IsGameplayStarted ||
            GameStateManager.IsGameplayEnded)
        {
            return;
        }

        if (other == null || !other.CompareTag("Player"))
            return;

        triggered = true;
        SetColliderEnabled(false);

        try
        {
            StatsManager.AddSpaceBombTrigger();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        PlayerArmor armor =
            other.GetComponentInParent<PlayerArmor>();

        PlayerMovement player =
            other.GetComponentInParent<PlayerMovement>();

        bool isImmune =
            armor != null && armor.IsImmune;

        bool willBreakArmor =
            !isImmune &&
            armor != null &&
            armor.HasArmor;

        bool lethalHit =
            !isImmune &&
            !willBreakArmor;

        // A lethal bomb hit is special: its explosion should remain visible and
        // audible after GameOver. Still, VFX/audio/haptics are optional: an
        // exception there must never prevent armor/death handling.
        try
        {
            Explode(lethalHit);
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "[SpaceBomb] Explosion feedback failed. Hit resolution continues.",
                this
            );
            Debug.LogException(exception, this);
        }

        if (isImmune)
            return;

        if (willBreakArmor)
        {
            armor.BreakArmor();
            return;
        }

        if (player != null)
        {
            player.GameOver("SPACE BOMB");
            return;
        }

        GameStateManager gameStateManager =
            FindAnyObjectByType<GameStateManager>(
                FindObjectsInactive.Include
            );

        if (gameStateManager != null)
            gameStateManager.GameOver(0, "SPACE BOMB");
    }

    private void Explode(bool persistThroughGameEnd)
    {
        CameraShake.Instance?.Shake(
            0.14f,
            0.10f
        );

        VibrationManager.Instance?.VibrateSpaceBomb();

        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(
                explosionEffectPrefab,
                transform.position,
                Quaternion.identity
            );

            if (persistThroughGameEnd)
                ConfigurePersistentExplosionVisual(effect);
        }

        if (explosionSound != null)
        {
            if (persistThroughGameEnd)
            {
                PlayPersistentExplosionSound();
            }
            else if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayCriticalSoundAtWorld(
                    explosionSound,
                    transform.position
                );
            }
            else
            {
                PlayMixerRoutedExplosionFallback();
            }
        }

        Destroy(gameObject);
    }


    private void ConfigurePersistentExplosionVisual(GameObject effect)
    {
        if (effect == null)
            return;

        ParticleSystem[] particleSystems =
            effect.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem system = particleSystems[i];

            if (system == null)
                continue;

            ParticleSystem.MainModule main = system.main;
            main.useUnscaledTime = true;
        }

        Animator[] animators =
            effect.GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
                animators[i].updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }

    private void PlayPersistentExplosionSound()
    {
        if (explosionSound == null)
            return;

        GameObject audioObject =
            new GameObject("SpaceBomb_LethalExplosionAudio");

        audioObject.transform.position = transform.position;

        GameEndPersistentAudio persistence =
            audioObject.AddComponent<GameEndPersistentAudio>();

        AudioSource source =
            audioObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.clip = explosionSound;
        source.volume = SoundManager.SFXVolume;
        source.pitch = 1f;
        source.ignoreListenerPause = false;

        SoundManager.ConfigureAsWorld3D(source);
        GameAudioMixerController.Route(
            source,
            GameAudioMixerController.AudioBus.CriticalSFX
        );

        source.Play();
        persistence.DestroyAfterRealtime(explosionSound.length + 0.25f);
    }

    private void PlayMixerRoutedExplosionFallback()
    {
        if (explosionSound == null)
            return;

        GameObject audioObject =
            new GameObject("SpaceBomb_ExplosionAudio");

        audioObject.transform.position = transform.position;

        AudioSource source =
            audioObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.clip = explosionSound;
        source.volume = SoundManager.SFXVolume;
        source.pitch = 1f;

        // Keep the emergency path on the same mixer routing as the normal
        // SoundManager path so Slow/Boss/Pause snapshots still behave.
        SoundManager.ConfigureAsWorld3D(source);
        GameAudioMixerController.Route(
            source,
            GameAudioMixerController.AudioBus.CriticalSFX
        );

        source.Play();

        Destroy(
            audioObject,
            explosionSound.length + 0.15f
        );
    }

    private void SetColliderEnabled(bool enabledState)
    {
        if (bombCollider != null)
            bombCollider.enabled = enabledState;
    }

    private void OnValidate()
    {
        spawnSafeTime = Mathf.Max(0f, spawnSafeTime);
    }
}
