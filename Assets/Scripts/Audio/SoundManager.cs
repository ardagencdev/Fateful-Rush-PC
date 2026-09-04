using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("SFX Source")]
    [Tooltip("Reference/template source. Positional one-shots are played through the internal 3D pool.")]
    public AudioSource sfxSource;

    [Header("3D SFX")]
    [SerializeField, Min(1)]
    private int spatialPoolSize = 20;

    [SerializeField, Min(1)]
    private int spatialPoolMaxSize = 32;

    [SerializeField, Min(0.01f)]
    private float spatialMinDistance = 25f;

    [SerializeField, Min(0.02f)]
    private float spatialMaxDistance = 60f;

    [SerializeField]
    private AudioRolloffMode spatialRolloffMode = AudioRolloffMode.Linear;

    [SerializeField, Range(0f, 360f)]
    private float spatialSpread = 0f;

    [Tooltip("Virtual distance in front of the AudioListener for centered/global SFX.")]
    [SerializeField, Min(0.1f)]
    private float centeredVirtualDepth = 1.5f;

    [Header("UI 3D Placement")]
    [Tooltip("How far left/right a screen-space UI sound can be placed around the listener.")]
    [SerializeField, Min(0.1f)]
    private float uiHorizontalExtent = 6f;

    [Tooltip("How far up/down a screen-space UI sound can be placed around the listener.")]
    [SerializeField, Min(0.1f)]
    private float uiVerticalExtent = 3.5f;

    [Tooltip("Virtual distance in front of the AudioListener used for UI SFX.")]
    [SerializeField, Min(0.1f)]
    private float uiVirtualDepth = 1.5f;

    [Header("Core Sounds")]
    public AudioClip coinSound;
    public AudioClip loseSound;
    public AudioClip winSound;

    [Header("Power Up Sounds")]
    public AudioClip armorCollectSound;
    public AudioClip armorBreakSound;
    public AudioClip slowCollectSound;

    [Header("Player Sounds")]
    public AudioClip dashSound;
    public AudioClip voidCloneSound;


    [Header("SFX Variation")]
    [Tooltip("Adds very small random pitch/volume changes to frequently repeated gameplay SFX. Critical warnings and win/lose cues stay fixed.")]
    [SerializeField]
    private bool enableSfxVariation = true;

    [Tooltip("Optional extra clips for the regular coin sound. The original coinSound remains part of the pool.")]
    [SerializeField]
    private AudioClip[] coinSoundVariants;

    [Tooltip("Optional extra clips for dash. The original dashSound remains part of the pool.")]
    [SerializeField]
    private AudioClip[] dashSoundVariants;

    [Tooltip("Optional extra clips for armor pickup. The original clip remains part of the pool.")]
    [SerializeField]
    private AudioClip[] armorCollectSoundVariants;

    [Tooltip("Optional extra clips for slow pickup. The original clip remains part of the pool.")]
    [SerializeField]
    private AudioClip[] slowCollectSoundVariants;

    [Tooltip("Optional extra clips for clone activation. The original clip remains part of the pool.")]
    [SerializeField]
    private AudioClip[] voidCloneSoundVariants;

    [SerializeField, Range(0f, 0.08f)]
    private float coinPitchJitter = 0.025f;

    [SerializeField, Range(0f, 0.08f)]
    private float dashPitchJitter = 0.018f;

    [SerializeField, Range(0f, 0.08f)]
    private float pickupPitchJitter = 0.012f;

    [SerializeField, Range(0f, 0.08f)]
    private float clonePitchJitter = 0.015f;

    [SerializeField, Range(0f, 0.08f)]
    private float frequentSfxVolumeJitter = 0.012f;

    [Header("Prestige Skin Coin Sounds")]
    [Tooltip("Used only while the DARK skin is equipped. Falls back to the normal coin sound if empty.")]
    public AudioClip darkCoinSound;

    [Tooltip("Used only while the GOLDEN skin is equipped. Falls back to the normal coin sound if empty.")]
    public AudioClip goldenCoinSound;

    [Header("UI Sounds")]
    public AudioClip menuButtonSound;
    public AudioClip backButtonSound;
    public AudioClip startButtonSound;
    public AudioClip lockedLevelSound;

    [FormerlySerializedAs("tutorialOpenSound")]
    public AudioClip missionBriefingOpenSound;

    public AudioClip premiumInterfaceSound;
    public AudioClip missionSelectSound;
    public AudioClip optionButtonSound;

    [FormerlySerializedAs("nextButtonSound")]
    [Tooltip("Uses the AudioClip that was previously assigned as Next Page SFX. Now used by the Skin Equip button.")]
    public AudioClip skinEquipSound;

    [Tooltip("Shared page-navigation SFX used by both Next and Previous page buttons.")]
    public AudioClip previousButtonSound;
    public AudioClip exitButtonSound;
    public AudioClip restartButtonSound;

    [Header("Gameplay Event Sounds")]
    public AudioClip spaceBombSpawnSound;
    public AudioClip bossAoeWarningSound;
    public AudioClip bossSplitSound;
    public AudioClip laserWarningSound;
    public AudioClip comboStageSound;
    public AudioClip newSkinUnlockedSound;

    [Header("Near Miss")]
    [Tooltip("Optional override. If empty, the bundled Resources/Audio/NearMissWhoosh clip is loaded automatically.")]
    public AudioClip nearMissSound;

    [Range(0f, 1f)]
    public float nearMissVolume = 0.52f;

    [SerializeField, Range(0f, 0.05f)]
    private float nearMissPitchJitter = 0.012f;

    [Header("Gameplay Event Volumes")]
    [Range(0f, 1f)] public float spaceBombSpawnVolume = 0.9f;
    [Range(0f, 1f)] public float bossAoeWarningVolume = 1f;
    [Range(0f, 1f)] public float bossSplitVolume = 1f;
    [Range(0f, 1f)] public float laserWarningVolume = 0.55f;
    [Range(0f, 1f)] public float comboStageVolume = 0.9f;
    [Range(0f, 1f)] public float newSkinUnlockedVolume = 1f;

    [Header("Beacon Enemy Sounds")]
    public AudioClip beaconActivationWaveSound;
    public AudioClip beaconLoopWaveSound;
    public AudioClip beaconDeathSound;

    [Range(0f, 1f)] public float beaconActivationVolume = 1f;
    [Range(0f, 1f)] public float beaconLoopVolume = 0.25f;
    [Range(0f, 2f)] public float beaconDeathVolume = 1.4f;

    private readonly List<AudioSource> spatialSources =
        new List<AudioSource>();

    private int spatialSourceCursor;
    private AudioListener cachedListener;

    private int lastCoinVariantIndex = -1;
    private int lastDashVariantIndex = -1;
    private int lastArmorCollectVariantIndex = -1;
    private int lastSlowCollectVariantIndex = -1;
    private int lastCloneVariantIndex = -1;

    public float ArmorBreakSoundDuration =>
        armorBreakSound != null ? armorBreakSound.length : 0f;

    public float WinSoundDuration =>
        winSound != null ? winSound.length : 0f;

    public static float SFXVolume
    {
        get
        {
            bool soundOn = PlayerPrefs.GetInt("SoundOn", 1) == 1;

            if (!soundOn)
                return 0f;

            // Mixer kuruluysa kullanici SFX slider gain'i AudioMixer
            // uzerinden uygulanir. Source seviyesinde tekrar carpip volume'u
            // iki kez dusurmemek icin burada unity gain doneriz.
            if (GameAudioMixerController.IsReady)
                return 1f;

            return Mathf.Clamp01(
                PlayerPrefs.GetFloat("SFXVolume", 1f)
            );
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Birden fazla SoundManager bulundu. Fazladan olan siliniyor.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();

        if (sfxSource != null)
            ConfigureWorldAudioSource(sfxSource);

        if (nearMissSound == null)
        {
            nearMissSound =
                Resources.Load<AudioClip>(
                    "Audio/NearMissWhoosh"
                );
        }

        PrepareSpatialPool();
    }

    private void Start()
    {
        ApplySFXVolume();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void StopAllSfx()
    {
        if (sfxSource != null)
            sfxSource.Stop();

        for (int i = 0; i < spatialSources.Count; i++)
        {
            AudioSource source = spatialSources[i];

            if (source != null)
                source.Stop();
        }
    }

    public void ApplySFXVolume()
    {
        float volume = SFXVolume;

        if (sfxSource != null)
            sfxSource.volume = volume;

        for (int i = 0; i < spatialSources.Count; i++)
        {
            if (spatialSources[i] != null)
                spatialSources[i].volume = volume;
        }
    }

    // ---------------------------------------------------------------------
    // Core / player / pickup SFX
    // ---------------------------------------------------------------------

    public void PlayCoinSound()
    {
        PlayVariedCenteredSound(
            coinSound,
            coinSoundVariants,
            ref lastCoinVariantIndex,
            coinPitchJitter,
            frequentSfxVolumeJitter
        );
    }

    public void PlayCoinSound(Vector3 worldPosition)
    {
        PlayVariedWorldSound(
            coinSound,
            coinSoundVariants,
            ref lastCoinVariantIndex,
            worldPosition,
            coinPitchJitter,
            frequentSfxVolumeJitter
        );
    }

    public void PlayCoinSound(string skinId)
    {
        AudioClip clip = GetCoinClipForSkin(skinId);
        bool regularCoin = clip == coinSound;

        PlayVariedCenteredSound(
            clip,
            regularCoin ? coinSoundVariants : null,
            ref lastCoinVariantIndex,
            coinPitchJitter,
            frequentSfxVolumeJitter
        );
    }

    public void PlayCoinSound(string skinId, Vector3 worldPosition)
    {
        AudioClip clip = GetCoinClipForSkin(skinId);
        bool regularCoin = clip == coinSound;

        PlayVariedWorldSound(
            clip,
            regularCoin ? coinSoundVariants : null,
            ref lastCoinVariantIndex,
            worldPosition,
            coinPitchJitter,
            frequentSfxVolumeJitter
        );
    }

    public void PlayLoseSound() => PlayCenteredCriticalSound(loseSound);
    public void PlayWinSound() => PlayCenteredCriticalSound(winSound);

    public void PlayArmorCollectSound()
    {
        PlayVariedCenteredSound(
            armorCollectSound,
            armorCollectSoundVariants,
            ref lastArmorCollectVariantIndex,
            pickupPitchJitter,
            frequentSfxVolumeJitter
        );
    }

    public void PlayArmorCollectSound(Vector3 worldPosition)
    {
        PlayVariedWorldSound(
            armorCollectSound,
            armorCollectSoundVariants,
            ref lastArmorCollectVariantIndex,
            worldPosition,
            pickupPitchJitter,
            frequentSfxVolumeJitter
        );
    }

    public void PlayArmorBreakSound() => PlayCenteredCriticalSound(armorBreakSound);
    public void PlayArmorBreakSound(Vector3 worldPosition) =>
        PlayWorldCriticalSound(armorBreakSound, worldPosition);

    public void PlaySlowCollectSound()
    {
        PlayVariedCenteredSound(
            slowCollectSound,
            slowCollectSoundVariants,
            ref lastSlowCollectVariantIndex,
            pickupPitchJitter,
            frequentSfxVolumeJitter
        );
    }

    public void PlaySlowCollectSound(Vector3 worldPosition)
    {
        PlayVariedWorldSound(
            slowCollectSound,
            slowCollectSoundVariants,
            ref lastSlowCollectVariantIndex,
            worldPosition,
            pickupPitchJitter,
            frequentSfxVolumeJitter
        );
    }

    public void PlayDashSound()
    {
        PlayVariedCenteredSound(
            dashSound,
            dashSoundVariants,
            ref lastDashVariantIndex,
            dashPitchJitter,
            frequentSfxVolumeJitter
        );
    }

    public void PlayDashSound(Vector3 worldPosition)
    {
        PlayVariedWorldSound(
            dashSound,
            dashSoundVariants,
            ref lastDashVariantIndex,
            worldPosition,
            dashPitchJitter,
            frequentSfxVolumeJitter
        );
    }

    public void PlayVoidCloneSound()
    {
        PlayVariedCenteredSound(
            voidCloneSound,
            voidCloneSoundVariants,
            ref lastCloneVariantIndex,
            clonePitchJitter,
            frequentSfxVolumeJitter
        );
    }

    public void PlayVoidCloneSound(Vector3 worldPosition)
    {
        PlayVariedWorldSound(
            voidCloneSound,
            voidCloneSoundVariants,
            ref lastCloneVariantIndex,
            worldPosition,
            clonePitchJitter,
            frequentSfxVolumeJitter
        );
    }

    // ---------------------------------------------------------------------
    // UI SFX. No-argument versions remain for compatibility and are still
    // true 3D sounds, positioned directly in front of the listener.
    // ---------------------------------------------------------------------

    public void PlayMissionBriefingOpenSound() =>
        PlayCenteredUISound(missionBriefingOpenSound);

    public void PlayMissionBriefingOpenSound(RectTransform sourceRect) =>
        PlayUISound(missionBriefingOpenSound, sourceRect);

    public void PlayPremiumInterfaceSound() =>
        PlayCenteredUISound(premiumInterfaceSound);

    public void PlayPremiumInterfaceSound(RectTransform sourceRect) =>
        PlayUISound(premiumInterfaceSound, sourceRect);

    public void PlayMissionSelectSound() =>
        PlayCenteredUISound(missionSelectSound);

    public void PlayMissionSelectSound(RectTransform sourceRect) =>
        PlayUISound(missionSelectSound, sourceRect);

    public void PlayStartButtonSound() =>
        PlayCenteredUISound(startButtonSound);

    public void PlayStartButtonSound(RectTransform sourceRect) =>
        PlayUISound(startButtonSound, sourceRect);

    public void PlayLockedLevelSound() =>
        PlayCenteredUISound(lockedLevelSound);

    public void PlayLockedLevelSound(RectTransform sourceRect) =>
        PlayUISound(lockedLevelSound, sourceRect);

    public void PlayMenuButtonSound() =>
        PlayCenteredUISound(menuButtonSound);

    public void PlayMenuButtonSound(RectTransform sourceRect) =>
        PlayUISound(menuButtonSound, sourceRect);

    public void PlayBackButtonSound() =>
        PlayCenteredUISound(backButtonSound);

    public void PlayBackButtonSound(RectTransform sourceRect) =>
        PlayUISound(backButtonSound, sourceRect);

    public void PlayOptionButtonSound() =>
        PlayCenteredUISound(optionButtonSound);

    public void PlayOptionButtonSound(RectTransform sourceRect) =>
        PlayUISound(optionButtonSound, sourceRect);

    public void PlayNextButtonSound() =>
        PlayCenteredUISound(previousButtonSound);

    public void PlayNextButtonSound(RectTransform sourceRect) =>
        PlayUISound(previousButtonSound, sourceRect);

    public void PlayPreviousButtonSound() =>
        PlayCenteredUISound(previousButtonSound);

    public void PlayPreviousButtonSound(RectTransform sourceRect) =>
        PlayUISound(previousButtonSound, sourceRect);

    public void PlaySkinEquipSound() =>
        PlayCenteredUISound(skinEquipSound);

    public void PlaySkinEquipSound(RectTransform sourceRect) =>
        PlayUISound(skinEquipSound, sourceRect);

    public void PlayExitButtonSound() =>
        PlayCenteredUISound(exitButtonSound);

    public void PlayExitButtonSound(RectTransform sourceRect) =>
        PlayUISound(exitButtonSound, sourceRect);

    public void PlayRestartButtonSound() =>
        PlayCenteredUISound(restartButtonSound);

    public void PlayRestartButtonSound(RectTransform sourceRect) =>
        PlayUISound(restartButtonSound, sourceRect);

    // ---------------------------------------------------------------------
    // Gameplay events
    // ---------------------------------------------------------------------

    public void PlaySpaceBombSpawnSound() =>
        PlayCenteredCriticalSound(spaceBombSpawnSound, spaceBombSpawnVolume);

    public void PlaySpaceBombSpawnSound(Vector3 worldPosition) =>
        PlayWorldCriticalSound(spaceBombSpawnSound, worldPosition, spaceBombSpawnVolume);

    public void PlayBossAoeWarningSound() =>
        PlayCenteredCriticalSound(bossAoeWarningSound, bossAoeWarningVolume);

    public void PlayBossAoeWarningSound(Vector3 worldPosition) =>
        PlayWorldCriticalSound(bossAoeWarningSound, worldPosition, bossAoeWarningVolume);

    public void PlayBossSplitSound() =>
        PlayCenteredCriticalSound(bossSplitSound, bossSplitVolume);

    public void PlayBossSplitSound(Vector3 worldPosition) =>
        PlayWorldCriticalSound(bossSplitSound, worldPosition, bossSplitVolume);

    public void PlayLaserWarningSound() =>
        PlayCenteredCriticalSound(laserWarningSound, laserWarningVolume);

    public void PlayLaserWarningSound(Vector3 worldPosition) =>
        PlayWorldCriticalSound(laserWarningSound, worldPosition, laserWarningVolume);

    public void PlayComboStageSound() =>
        PlayCenteredUISound(comboStageSound, comboStageVolume);

    public void PlayComboStageSound(RectTransform sourceRect) =>
        PlayUISound(comboStageSound, sourceRect, comboStageVolume);

    public void PlayNewSkinUnlockedSound() =>
        PlayCenteredUISound(newSkinUnlockedSound, newSkinUnlockedVolume);

    public void PlayNewSkinUnlockedSound(RectTransform sourceRect) =>
        PlayUISound(newSkinUnlockedSound, sourceRect, newSkinUnlockedVolume);

    public void PlayNearMissSound(
        Vector3 worldPosition,
        float closeness01 = 1f)
    {
        if (nearMissSound == null)
            return;

        float closeness =
            Mathf.Clamp01(closeness01);

        float volume =
            nearMissVolume *
            Mathf.Lerp(0.82f, 1f, closeness);

        float basePitch =
            Mathf.Lerp(0.985f, 1.015f, closeness);

        float pitch =
            enableSfxVariation
                ? GetVariedPitch(
                    basePitch,
                    nearMissPitchJitter
                )
                : basePitch;

        PlayWorldCriticalSound(
            nearMissSound,
            worldPosition,
            volume,
            pitch
        );
    }

    public void PlayBeaconActivationWaveSound() =>
        PlayCenteredSound(beaconActivationWaveSound, beaconActivationVolume);

    public void PlayBeaconActivationWaveSound(Vector3 worldPosition) =>
        PlayWorldSound(beaconActivationWaveSound, worldPosition, beaconActivationVolume);

    public void PlayBeaconLoopWaveSound() =>
        PlayCenteredSound(beaconLoopWaveSound, beaconLoopVolume);

    public void PlayBeaconLoopWaveSound(Vector3 worldPosition) =>
        PlayWorldSound(beaconLoopWaveSound, worldPosition, beaconLoopVolume);

    public void PlayBeaconDeathSound() =>
        PlayCenteredSound(beaconDeathSound, beaconDeathVolume);

    public void PlayBeaconDeathSound(Vector3 worldPosition) =>
        PlayWorldSound(beaconDeathSound, worldPosition, beaconDeathVolume);

    // ---------------------------------------------------------------------
    // Generic API used by gameplay scripts and custom UI sounds.
    // ---------------------------------------------------------------------

    public void PlayCustomSound(AudioClip customClip)
    {
        PlayCenteredSound(customClip);
    }

    public void PlayCustomSoundAtWorld(
        AudioClip customClip,
        Vector3 worldPosition,
        float volumeMultiplier = 1f,
        float pitch = 1f)
    {
        PlayWorldSound(
            customClip,
            worldPosition,
            volumeMultiplier,
            pitch
        );
    }

    public void PlayCustomSoundAtUI(
        AudioClip customClip,
        RectTransform sourceRect,
        float volumeMultiplier = 1f,
        float pitch = 1f)
    {
        PlayUISound(
            customClip,
            sourceRect,
            volumeMultiplier,
            pitch
        );
    }

    public void PlayCriticalSoundAtWorld(
        AudioClip customClip,
        Vector3 worldPosition,
        float volumeMultiplier = 1f,
        float pitch = 1f)
    {
        PlayWorldCriticalSound(
            customClip,
            worldPosition,
            volumeMultiplier,
            pitch
        );
    }

    public static void ConfigureAsWorld3D(AudioSource source)
    {
        if (source == null)
            return;

        if (Instance != null)
        {
            Instance.ConfigureWorldAudioSource(source);
            return;
        }

        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.spread = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 25f;
        source.maxDistance = 60f;

        GameAudioMixerController.Route(
            source,
            GameAudioMixerController.AudioBus.GameplaySFX
        );
    }

    public void ConfigureWorldAudioSource(AudioSource source)
    {
        ConfigureWorldAudioSource(
            source,
            GameAudioMixerController.AudioBus.GameplaySFX
        );
    }

    public void ConfigureWorldAudioSource(
        AudioSource source,
        GameAudioMixerController.AudioBus bus)
    {
        if (source == null)
            return;

        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.spread = spatialSpread;
        source.rolloffMode = spatialRolloffMode;
        source.minDistance = spatialMinDistance;
        source.maxDistance = spatialMaxDistance;

        GameAudioMixerController.Route(source, bus);
    }

    public static float GetVariedPitch(float basePitch, float jitterAmount)
    {
        float jitter = Mathf.Max(0f, jitterAmount);

        if (jitter <= 0f)
            return basePitch;

        return Mathf.Clamp(
            basePitch + Random.Range(-jitter, jitter),
            -3f,
            3f
        );
    }

    public static float GetVariedVolumeMultiplier(
        float baseMultiplier,
        float jitterAmount)
    {
        float jitter = Mathf.Max(0f, jitterAmount);

        if (jitter <= 0f)
            return Mathf.Max(0f, baseMultiplier);

        return Mathf.Max(
            0f,
            baseMultiplier * (1f + Random.Range(-jitter, jitter))
        );
    }

    private void PlayVariedCenteredSound(
        AudioClip primaryClip,
        AudioClip[] extraClips,
        ref int lastVariantIndex,
        float pitchJitter,
        float volumeJitter)
    {
        AudioClip clip = SelectVariationClip(
            primaryClip,
            extraClips,
            ref lastVariantIndex
        );

        float pitch = enableSfxVariation
            ? GetVariedPitch(1f, pitchJitter)
            : 1f;

        float volume = enableSfxVariation
            ? GetVariedVolumeMultiplier(1f, volumeJitter)
            : 1f;

        PlayCenteredSound(clip, volume, pitch);
    }

    private void PlayVariedWorldSound(
        AudioClip primaryClip,
        AudioClip[] extraClips,
        ref int lastVariantIndex,
        Vector3 worldPosition,
        float pitchJitter,
        float volumeJitter)
    {
        AudioClip clip = SelectVariationClip(
            primaryClip,
            extraClips,
            ref lastVariantIndex
        );

        float pitch = enableSfxVariation
            ? GetVariedPitch(1f, pitchJitter)
            : 1f;

        float volume = enableSfxVariation
            ? GetVariedVolumeMultiplier(1f, volumeJitter)
            : 1f;

        PlayWorldSound(clip, worldPosition, volume, pitch);
    }

    private AudioClip SelectVariationClip(
        AudioClip primaryClip,
        AudioClip[] extraClips,
        ref int lastVariantIndex)
    {
        if (!enableSfxVariation ||
            extraClips == null ||
            extraClips.Length == 0)
        {
            lastVariantIndex = -1;
            return primaryClip;
        }

        int validExtraCount = 0;

        for (int i = 0; i < extraClips.Length; i++)
        {
            if (extraClips[i] != null)
                validExtraCount++;
        }

        int totalCount = (primaryClip != null ? 1 : 0) + validExtraCount;

        if (totalCount <= 0)
            return null;

        if (totalCount == 1)
        {
            lastVariantIndex = 0;

            if (primaryClip != null)
                return primaryClip;

            for (int i = 0; i < extraClips.Length; i++)
            {
                if (extraClips[i] != null)
                    return extraClips[i];
            }
        }

        int selectedIndex;
        int safety = 0;

        do
        {
            selectedIndex = Random.Range(0, totalCount);
            safety++;
        }
        while (selectedIndex == lastVariantIndex && safety < 8);

        lastVariantIndex = selectedIndex;

        if (primaryClip != null)
        {
            if (selectedIndex == 0)
                return primaryClip;

            selectedIndex--;
        }

        for (int i = 0; i < extraClips.Length; i++)
        {
            AudioClip candidate = extraClips[i];

            if (candidate == null)
                continue;

            if (selectedIndex == 0)
                return candidate;

            selectedIndex--;
        }

        return primaryClip;
    }

    private AudioClip GetCoinClipForSkin(string skinId)
    {
        string normalizedSkinId =
            string.IsNullOrWhiteSpace(skinId)
                ? string.Empty
                : skinId.Trim().ToLowerInvariant();

        if (normalizedSkinId == "dark" && darkCoinSound != null)
            return darkCoinSound;

        if (normalizedSkinId == "golden" && goldenCoinSound != null)
            return goldenCoinSound;

        return coinSound;
    }

    private void PlayCenteredSound(
        AudioClip clip,
        float volumeMultiplier = 1f,
        float pitch = 1f,
        GameAudioMixerController.AudioBus bus =
            GameAudioMixerController.AudioBus.GameplaySFX)
    {
        PlaySpatialSound(
            clip,
            GetCenteredWorldPosition(),
            volumeMultiplier,
            pitch,
            bus
        );
    }

    private void PlayCenteredUISound(
        AudioClip clip,
        float volumeMultiplier = 1f,
        float pitch = 1f)
    {
        PlayCenteredSound(
            clip,
            volumeMultiplier,
            pitch,
            GameAudioMixerController.AudioBus.UISFX
        );
    }

    private void PlayCenteredCriticalSound(
        AudioClip clip,
        float volumeMultiplier = 1f,
        float pitch = 1f)
    {
        PlayCenteredSound(
            clip,
            volumeMultiplier,
            pitch,
            GameAudioMixerController.AudioBus.CriticalSFX
        );
    }

    private void PlayWorldCriticalSound(
        AudioClip clip,
        Vector3 worldPosition,
        float volumeMultiplier = 1f,
        float pitch = 1f)
    {
        PlayWorldSound(
            clip,
            worldPosition,
            volumeMultiplier,
            pitch,
            GameAudioMixerController.AudioBus.CriticalSFX
        );
    }

    private void PlayWorldSound(
        AudioClip clip,
        Vector3 worldPosition,
        float volumeMultiplier = 1f,
        float pitch = 1f,
        GameAudioMixerController.AudioBus bus =
            GameAudioMixerController.AudioBus.GameplaySFX)
    {
        PlaySpatialSound(
            clip,
            worldPosition,
            volumeMultiplier,
            pitch,
            bus
        );
    }

    private void PlayUISound(
        AudioClip clip,
        RectTransform sourceRect,
        float volumeMultiplier = 1f,
        float pitch = 1f)
    {
        Vector3 position = sourceRect != null
            ? GetUIWorldPosition(sourceRect)
            : GetCenteredWorldPosition();

        PlaySpatialSound(
            clip,
            position,
            volumeMultiplier,
            pitch,
            GameAudioMixerController.AudioBus.UISFX
        );
    }

    private void PlaySpatialSound(
        AudioClip clip,
        Vector3 position,
        float volumeMultiplier,
        float pitch,
        GameAudioMixerController.AudioBus bus)
    {
        if (clip == null)
            return;

        float sfxVolume = SFXVolume;

        if (sfxVolume <= 0f)
            return;

        AudioSource source = GetAvailableSpatialSource();

        if (source == null)
            return;

        source.transform.position = position;
        source.volume = sfxVolume;
        source.pitch = Mathf.Clamp(pitch, -3f, 3f);

        GameAudioMixerController.Route(source, bus);

        source.PlayOneShot(clip, Mathf.Max(0f, volumeMultiplier));
    }

    private void PrepareSpatialPool()
    {
        int targetCount = Mathf.Max(1, spatialPoolSize);

        while (spatialSources.Count < targetCount)
            spatialSources.Add(CreateSpatialSource(spatialSources.Count));
    }

    private AudioSource GetAvailableSpatialSource()
    {
        PrepareSpatialPool();

        int count = spatialSources.Count;

        for (int i = 0; i < count; i++)
        {
            int index = (spatialSourceCursor + i) % count;
            AudioSource candidate = spatialSources[index];

            if (candidate != null && !candidate.isPlaying)
            {
                spatialSourceCursor = (index + 1) % Mathf.Max(1, count);
                return candidate;
            }
        }

        if (spatialSources.Count < Mathf.Max(1, spatialPoolMaxSize))
        {
            AudioSource created = CreateSpatialSource(spatialSources.Count);
            spatialSources.Add(created);
            spatialSourceCursor = 0;
            return created;
        }

        int fallbackIndex = spatialSourceCursor % Mathf.Max(1, spatialSources.Count);
        spatialSourceCursor = (fallbackIndex + 1) % Mathf.Max(1, spatialSources.Count);

        AudioSource fallback = spatialSources[fallbackIndex];

        if (fallback != null)
            fallback.Stop();

        return fallback;
    }

    private AudioSource CreateSpatialSource(int index)
    {
        GameObject sourceObject = new GameObject($"SpatialSFX_{index:00}");
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.volume = SFXVolume;

        ConfigureWorldAudioSource(source);
        CopyTemplateSettings(source);

        return source;
    }

    private void CopyTemplateSettings(AudioSource target)
    {
        if (target == null || sfxSource == null)
            return;

        target.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
        target.priority = sfxSource.priority;
        target.bypassEffects = sfxSource.bypassEffects;
        target.bypassListenerEffects = sfxSource.bypassListenerEffects;
        target.bypassReverbZones = sfxSource.bypassReverbZones;
        target.ignoreListenerPause = sfxSource.ignoreListenerPause;
        target.ignoreListenerVolume = sfxSource.ignoreListenerVolume;
    }

    private Vector3 GetCenteredWorldPosition()
    {
        Transform listenerTransform = GetListenerTransform();

        if (listenerTransform == null)
            return transform.position;

        return listenerTransform.position +
               listenerTransform.forward * centeredVirtualDepth;
    }

    private Vector3 GetUIWorldPosition(RectTransform sourceRect)
    {
        Transform listenerTransform = GetListenerTransform();

        if (listenerTransform == null || sourceRect == null)
            return GetCenteredWorldPosition();

        Canvas canvas = sourceRect.GetComponentInParent<Canvas>();
        Camera eventCamera = null;

        if (canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera;

            if (eventCamera == null)
                eventCamera = Camera.main;
        }

        Vector3 rectWorldCenter =
            sourceRect.TransformPoint(sourceRect.rect.center);

        Vector2 screenPoint =
            RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                rectWorldCenter
            );

        float safeWidth = Mathf.Max(1f, Screen.width);
        float safeHeight = Mathf.Max(1f, Screen.height);

        float normalizedX = Mathf.Clamp(
            (screenPoint.x / safeWidth - 0.5f) * 2f,
            -1f,
            1f
        );

        float normalizedY = Mathf.Clamp(
            (screenPoint.y / safeHeight - 0.5f) * 2f,
            -1f,
            1f
        );

        return listenerTransform.position +
               listenerTransform.forward * uiVirtualDepth +
               listenerTransform.right * normalizedX * uiHorizontalExtent +
               listenerTransform.up * normalizedY * uiVerticalExtent;
    }

    private Transform GetListenerTransform()
    {
        if (cachedListener == null || !cachedListener.isActiveAndEnabled)
            cachedListener = FindAnyObjectByType<AudioListener>();

        if (cachedListener != null)
            return cachedListener.transform;

        Camera mainCamera = Camera.main;

        return mainCamera != null
            ? mainCamera.transform
            : null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignClip(
            ref restartButtonSound,
            "Assets/SFX/UI/RestartButtonSFX.wav"
        );

        AutoAssignClip(
            ref spaceBombSpawnSound,
            "Assets/SFX/Traps/SpaceBombSpawnSFX.wav"
        );

        AutoAssignClip(
            ref bossAoeWarningSound,
            "Assets/SFX/Enemy/BOSSAOEWarningSFX.wav"
        );

        AutoAssignClip(
            ref bossSplitSound,
            "Assets/SFX/Enemy/BOSSSplitSFX.wav"
        );

        AutoAssignClip(
            ref laserWarningSound,
            "Assets/SFX/Environment/LaserWarningSFX.wav"
        );

        AutoAssignClip(
            ref comboStageSound,
            "Assets/SFX/UI/ComboStageSFX.wav"
        );

        AutoAssignClip(
            ref newSkinUnlockedSound,
            "Assets/SFX/GameState/NewSkinUnlockedSFX.wav"
        );

        spaceBombSpawnVolume = Mathf.Clamp01(spaceBombSpawnVolume);
        bossAoeWarningVolume = Mathf.Clamp01(bossAoeWarningVolume);
        bossSplitVolume = Mathf.Clamp01(bossSplitVolume);
        laserWarningVolume = Mathf.Clamp01(laserWarningVolume);
        comboStageVolume = Mathf.Clamp01(comboStageVolume);
        newSkinUnlockedVolume = Mathf.Clamp01(newSkinUnlockedVolume);

        coinPitchJitter = Mathf.Clamp(coinPitchJitter, 0f, 0.08f);
        dashPitchJitter = Mathf.Clamp(dashPitchJitter, 0f, 0.08f);
        pickupPitchJitter = Mathf.Clamp(pickupPitchJitter, 0f, 0.08f);
        clonePitchJitter = Mathf.Clamp(clonePitchJitter, 0f, 0.08f);
        frequentSfxVolumeJitter = Mathf.Clamp(frequentSfxVolumeJitter, 0f, 0.08f);

        spatialPoolSize = Mathf.Max(1, spatialPoolSize);
        spatialPoolMaxSize = Mathf.Max(spatialPoolSize, spatialPoolMaxSize);
        spatialMinDistance = Mathf.Max(0.01f, spatialMinDistance);
        spatialMaxDistance = Mathf.Max(spatialMinDistance + 0.01f, spatialMaxDistance);
        centeredVirtualDepth = Mathf.Max(0.1f, centeredVirtualDepth);
        uiHorizontalExtent = Mathf.Max(0.1f, uiHorizontalExtent);
        uiVerticalExtent = Mathf.Max(0.1f, uiVerticalExtent);
        uiVirtualDepth = Mathf.Max(0.1f, uiVirtualDepth);
    }

    private static void AutoAssignClip(
        ref AudioClip target,
        string assetPath)
    {
        if (target != null)
            return;

        target =
            UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                assetPath
            );
    }
#endif
}
