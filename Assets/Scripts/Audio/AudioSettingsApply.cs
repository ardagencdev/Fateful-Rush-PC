using UnityEngine;

public class AudioSettingsApply : MonoBehaviour
{
    public enum SoundType
    {
        Music,
        SFX
    }

    [Header("Settings")]
    [SerializeField] private SoundType soundType;

    [Header("References")]
    [SerializeField] private AudioSource audioSource;

    private float baseVolume = 1f;
    private bool isManagedByDedicatedController;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            return;

        baseVolume = Mathf.Clamp01(audioSource.volume);

        // These systems already own their AudioSource volume and include
        // their own authored/base gain. Applying the raw slider value here
        // would overwrite that gain and cause volume jumps.
        isManagedByDedicatedController =
            audioSource.GetComponent<GameplayMusicFade>() != null ||
            audioSource.GetComponent<MenuMusicApply>() != null ||
            audioSource.GetComponent<SoundManager>() != null ||
            audioSource.GetComponent<LaserWall>() != null;

        GameAudioMixerController.Route(
            audioSource,
            soundType == SoundType.Music
                ? GameAudioMixerController.AudioBus.Music
                : GameAudioMixerController.AudioBus.GameplaySFX
        );
    }

    private void Start()
    {
        Apply();
    }

    public void Apply()
    {
        if (audioSource == null)
        {
            Debug.LogWarning(
                $"{gameObject.name} üzerinde AudioSource yok.",
                this
            );

            return;
        }

        if (isManagedByDedicatedController)
            return;

        bool soundOn =
            PlayerPrefs.GetInt("SoundOn", 1) == 1;

        float userVolume;

        if (GameAudioMixerController.IsReady)
        {
            // Slider gain'i mixer parent bus'unda uygulanir.
            userVolume = 1f;
        }
        else
        {
            userVolume = soundType == SoundType.Music
                ? PlayerPrefs.GetFloat("MusicVolume", 1f)
                : PlayerPrefs.GetFloat("SFXVolume", 1f);

            userVolume = Mathf.Clamp01(userVolume);
        }

        audioSource.volume = soundOn
            ? baseVolume * userVolume
            : 0f;
    }
}
