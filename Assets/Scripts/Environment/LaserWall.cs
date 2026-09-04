using System.Collections;
using UnityEngine;

public class LaserWall : MonoBehaviour
{
    [Header("Lifetime")]
    public float lifeTime = 1.5f;

    [Header("Sound")]
    public AudioClip laserLoopSound;

    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("Laser yok olmadan hemen önce sesin smooth şekilde kapanma süresi.")]
    [Min(0f)]
    public float fadeOutDuration = 0.25f;

    [Header("Near Miss")]
    [SerializeField]
    private bool enableNearMiss = true;

    [Tooltip("Surface-to-surface distance that counts as narrowly surviving the laser.")]
    [SerializeField, Min(0.05f)]
    private float nearMissDistance = 0.65f;

    [SerializeField, Min(0f)]
    private float nearMissReleaseDistance = 0.10f;

    private const float NearMissReliabilityPadding = 0.12f;

    private AudioSource audioSource;
    private bool soundWasPaused;
    private Coroutine lifetimeRoutine;

    private Collider2D[] laserColliders;
    private Collider2D[] playerColliders;
    private PlayerMovement nearMissPlayer;
    private bool nearMissArmed;
    private bool nearMissTouchedPlayer;
    private bool nearMissTriggered;
    private float nearMissClosestDistance = float.PositiveInfinity;
    private Vector3 nearMissClosestPoint;

    private void Start()
    {
        SetupAudio();
        SetupNearMissTracking();

        if (lifeTime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        lifetimeRoutine = StartCoroutine(LifetimeRoutine());
    }

    private void Update()
    {
        TrackNearMiss();
    }

    private void SetupAudio()
    {
        if (laserLoopSound == null)
            return;

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.clip = laserLoopSound;
        audioSource.volume =
            Mathf.Clamp01(volume) * SoundManager.SFXVolume;

        audioSource.loop = false;
        audioSource.spatialBlend = 1f;

        GameAudioMixerController.Route(
            audioSource,
            GameAudioMixerController.AudioBus.GameplaySFX
        );

        audioSource.Play();
    }

    private IEnumerator LifetimeRoutine()
    {
        float safeLifeTime = Mathf.Max(0f, lifeTime);
        float safeFadeDuration = Mathf.Clamp(
            fadeOutDuration,
            0f,
            safeLifeTime
        );

        float waitBeforeFade =
            Mathf.Max(0f, safeLifeTime - safeFadeDuration);

        if (waitBeforeFade > 0f)
            yield return new WaitForSeconds(waitBeforeFade);

        if (audioSource != null &&
            audioSource.isPlaying &&
            safeFadeDuration > 0f)
        {
            float startVolume = audioSource.volume;
            float elapsed = 0f;

            while (elapsed < safeFadeDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(
                    elapsed / safeFadeDuration
                );

                float smoothT = t * t * (3f - 2f * t);

                audioSource.volume =
                    Mathf.Lerp(
                        startVolume,
                        0f,
                        smoothT
                    );

                yield return null;
            }

            audioSource.volume = 0f;
        }
        else if (safeFadeDuration > 0f)
        {
            yield return new WaitForSeconds(safeFadeDuration);
        }

        TryCompleteNearMiss();

        Destroy(gameObject);
        lifetimeRoutine = null;
    }

    private void SetupNearMissTracking()
    {
        nearMissArmed = false;
        nearMissTouchedPlayer = false;
        nearMissTriggered = false;
        nearMissClosestDistance = float.PositiveInfinity;
        nearMissClosestPoint = transform.position;

        laserColliders =
            GetComponentsInChildren<Collider2D>(true);

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
            return;

        nearMissPlayer =
            playerObject.GetComponent<PlayerMovement>();

        playerColliders =
            playerObject.GetComponentsInChildren<Collider2D>(true);
    }

    private void TrackNearMiss()
    {
        if (!enableNearMiss ||
            nearMissTriggered ||
            nearMissTouchedPlayer ||
            nearMissPlayer == null ||
            nearMissPlayer.IsGameOver ||
            Time.timeScale <= 0f)
        {
            return;
        }

        if (laserColliders == null ||
            laserColliders.Length == 0 ||
            playerColliders == null ||
            playerColliders.Length == 0)
        {
            return;
        }

        float frameClosest = float.PositiveInfinity;
        Vector3 frameClosestPoint = transform.position;

        for (int i = 0; i < laserColliders.Length; i++)
        {
            Collider2D laserCollider = laserColliders[i];

            if (laserCollider == null ||
                !laserCollider.enabled)
            {
                continue;
            }

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider2D playerCollider = playerColliders[j];

                if (playerCollider == null ||
                    !playerCollider.enabled)
                {
                    continue;
                }

                ColliderDistance2D separation =
                    laserCollider.Distance(playerCollider);

                float distance = separation.distance;

                if (distance <= 0f)
                {
                    nearMissTouchedPlayer = true;
                    return;
                }

                if (distance < frameClosest)
                {
                    frameClosest = distance;
                    frameClosestPoint = separation.pointA;
                }
            }
        }

        if (float.IsPositiveInfinity(frameClosest))
            return;

        float detectionDistance =
            nearMissDistance + NearMissReliabilityPadding;

        if (frameClosest <= detectionDistance)
        {
            nearMissArmed = true;

            if (frameClosest < nearMissClosestDistance)
            {
                nearMissClosestDistance = frameClosest;
                nearMissClosestPoint = frameClosestPoint;
            }

            return;
        }

        if (nearMissArmed &&
            frameClosest >=
            detectionDistance + nearMissReleaseDistance)
        {
            TriggerNearMiss();
        }
    }

    private void TryCompleteNearMiss()
    {
        if (!nearMissArmed ||
            nearMissTriggered ||
            nearMissTouchedPlayer ||
            nearMissPlayer == null ||
            nearMissPlayer.IsGameOver)
        {
            return;
        }

        TriggerNearMiss();
    }

    private void TriggerNearMiss()
    {
        if (nearMissTriggered)
            return;

        float closeness =
            NearMissFeedback.GetCloseness01(
                nearMissClosestDistance,
                nearMissDistance + NearMissReliabilityPadding
            );

        nearMissTriggered = NearMissFeedback.TryTrigger(
            nearMissClosestPoint,
            closeness
        );
    }

    public void FreezeLaser()
    {
        soundWasPaused = false;

        if (audioSource != null)
            audioSource.Stop();

        enabled = false;
    }

    public void PauseLaserSound()
    {
        if (audioSource == null || !audioSource.isPlaying)
            return;

        audioSource.Pause();
        soundWasPaused = true;
    }

    public void ResumeLaserSound()
    {
        if (audioSource == null || !soundWasPaused)
            return;

        audioSource.UnPause();
        soundWasPaused = false;
    }

    private void OnDisable()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        soundWasPaused = false;
    }

    private void OnValidate()
    {
        lifeTime = Mathf.Max(0f, lifeTime);
        volume = Mathf.Clamp01(volume);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        nearMissDistance = Mathf.Max(0.05f, nearMissDistance);
        nearMissReleaseDistance = Mathf.Max(0f, nearMissReleaseDistance);
    }
}
