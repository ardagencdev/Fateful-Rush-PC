using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GameplayMusicFade : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField, Range(0f, 1f)]
    private float gameplayMusicBaseVolume = 0.2f;

    [Header("Fade Durations")]
    [SerializeField, Min(0f)]
    private float fadeInDuration = 0.6f;

    [SerializeField, Min(0f)]
    private float fadeOutDuration = 0.4f;

    [Header("Music Transition")]
    [SerializeField, Min(0f)]
    private float clipTransitionDuration = 0.8f;

    [Header("Dynamic Tension")]
    [SerializeField, Range(0f, 0.25f)]
    [Tooltip("Maximum tension sırasında gameplay müziğine eklenecek ses seviyesi. 0.08 = yaklaşık %8 boost.")]
    private float maxTensionVolumeBoost = 0.08f;

    [SerializeField, Range(1f, 1.10f)]
    [Tooltip("Maximum tension sırasında müziğin pitch değeri. Küçük tutulması önerilir.")]
    private float maxTensionPitch = 1.045f;

    [SerializeField, Min(0.01f)]
    [Tooltip("Tension yükselirken hedef değere ne kadar hızlı yaklaşılacağı.")]
    private float tensionRiseSpeed = 1.9f;

    [SerializeField, Min(0.01f)]
    [Tooltip("Tension düşerken hedef değere ne kadar hızlı dönüleceği.")]
    private float tensionFallSpeed = 3.5f;

    public float FadeOutDuration => fadeOutDuration;
    public float CurrentTargetVolume =>
        GetBaseTargetVolume() *
        GetTensionVolumeMultiplier(SmootherStep(currentTension));

    public float CurrentTension => currentTension;

    private AudioSource source;
    private Coroutine fadeRoutine;

    // Volume fade'leri source.volume ile doğrudan kavga etmesin diye
    // 0-1 arası ayrı bir playback gain tutuyoruz.
    private float playbackGain;
    private float targetTension;
    private float currentTension;

    private void Awake()
    {
        source = GetComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
        source.pitch = 1f;

        playbackGain = 0f;
        targetTension = 0f;
        currentTension = 0f;

        GameAudioMixerController.Route(
            source,
            GameAudioMixerController.AudioBus.Music
        );
    }

    private void LateUpdate()
    {
        if (source == null)
            return;

        float speed =
            targetTension >= currentTension
                ? tensionRiseSpeed
                : tensionFallSpeed;

        currentTension = Mathf.MoveTowards(
            currentTension,
            targetTension,
            speed * Time.unscaledDeltaTime
        );

        ApplyOutput();
    }

    public void PlayClipAndFadeIn(AudioClip clip)
    {
        if (clip == null)
        {
            StopImmediately();
            return;
        }

        StopCurrentFade();

        source.Stop();
        source.clip = clip;
        source.loop = true;
        playbackGain = 0f;
        ApplyOutput();
        source.Play();

        FadeGainTo(1f, fadeInDuration);
    }

    public void TransitionToClip(AudioClip newClip)
    {
        if (newClip == null)
        {
            FadeOut();
            return;
        }

        if (source.isPlaying &&
            source.clip == newClip)
        {
            FadeGainTo(1f, fadeInDuration);
            return;
        }

        StopCurrentFade();

        fadeRoutine = StartCoroutine(
            TransitionClipRoutine(newClip)
        );
    }

    public void PlayAndFadeIn()
    {
        if (source.clip == null)
            return;

        StopCurrentFade();

        source.Stop();
        source.loop = true;
        playbackGain = 0f;
        ApplyOutput();
        source.Play();

        FadeGainTo(1f, fadeInDuration);
    }

    public void FadeIn()
    {
        if (source.clip == null)
            return;

        if (!source.isPlaying)
            source.Play();

        FadeGainTo(1f, fadeInDuration);
    }

    public void FadeOut()
    {
        if (!source.isPlaying)
            return;

        FadeGainTo(
            0f,
            fadeOutDuration,
            true
        );
    }

    public void FadeOutAndPause(float duration)
    {
        if (source == null || source.clip == null)
            return;

        StopCurrentFade();

        fadeRoutine = StartCoroutine(
            FadeOutAndPauseRoutine(duration)
        );
    }

    public void ResumeFromPause(float duration)
    {
        if (source == null || source.clip == null)
            return;

        StopCurrentFade();

        source.UnPause();

        if (!source.isPlaying)
            source.Play();

        FadeGainTo(1f, duration);
    }

    public void SetTension(
        float normalizedTension,
        bool immediate = false)
    {
        targetTension = Mathf.Clamp01(normalizedTension);

        if (!immediate)
            return;

        currentTension = targetTension;
        ApplyOutput();
    }

    public void ResetTension(bool immediate = false)
    {
        SetTension(0f, immediate);
    }

    private IEnumerator FadeOutAndPauseRoutine(float duration)
    {
        yield return FadeGainRoutine(
            0f,
            duration
        );

        if (source != null)
        {
            playbackGain = 0f;
            ApplyOutput();
            source.Pause();
        }

        fadeRoutine = null;
    }

    public void StopImmediately()
    {
        StopCurrentFade();

        playbackGain = 0f;
        targetTension = 0f;
        currentTension = 0f;

        source.Stop();
        source.clip = null;
        source.pitch = 1f;
        ApplyOutput();
    }

    public void RefreshVolume()
    {
        if (source == null)
            return;

        ApplyOutput();
    }

    private IEnumerator TransitionClipRoutine(
        AudioClip newClip)
    {
        float halfDuration = Mathf.Max(
            0.01f,
            clipTransitionDuration * 0.5f
        );

        if (source.isPlaying)
        {
            yield return FadeGainRoutine(
                0f,
                halfDuration
            );
        }

        source.Stop();
        source.clip = newClip;
        source.loop = true;
        playbackGain = 0f;
        ApplyOutput();
        source.Play();

        yield return FadeGainRoutine(
            1f,
            halfDuration
        );

        fadeRoutine = null;
    }

    private float GetBaseTargetVolume()
    {
        bool soundOn =
            PlayerPrefs.GetInt(
                "SoundOn",
                1
            ) == 1;

        bool gameplayMusicOn =
            PlayerPrefs.GetInt(
                "GameplayMusicOn",
                1
            ) == 1;

        if (!soundOn || !gameplayMusicOn)
            return 0f;

        if (GameAudioMixerController.IsReady)
            return gameplayMusicBaseVolume;

        float userMusicVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                "MusicVolume",
                1f
            )
        );

        return userMusicVolume *
               gameplayMusicBaseVolume;
    }

    private void FadeGainTo(
        float targetGain,
        float duration,
        bool stopAfterFade = false)
    {
        StopCurrentFade();

        fadeRoutine = StartCoroutine(
            FadeGainRoutineWrapper(
                targetGain,
                duration,
                stopAfterFade
            )
        );
    }

    private IEnumerator FadeGainRoutineWrapper(
        float targetGain,
        float duration,
        bool stopAfterFade)
    {
        yield return FadeGainRoutine(
            targetGain,
            duration
        );

        if (stopAfterFade)
        {
            source.Stop();
            playbackGain = 0f;
            ApplyOutput();
        }

        fadeRoutine = null;
    }

    private IEnumerator FadeGainRoutine(
        float targetGain,
        float duration)
    {
        targetGain = Mathf.Clamp01(targetGain);

        if (duration <= 0f)
        {
            playbackGain = targetGain;
            ApplyOutput();
            yield break;
        }

        float startGain = playbackGain;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                timer / duration
            );

            progress = SmootherStep(progress);

            playbackGain = Mathf.Lerp(
                startGain,
                targetGain,
                progress
            );

            ApplyOutput();
            yield return null;
        }

        playbackGain = targetGain;
        ApplyOutput();
    }

    private void ApplyOutput()
    {
        if (source == null)
            return;

        float easedTension =
            SmootherStep(currentTension);

        source.volume =
            GetBaseTargetVolume() *
            playbackGain *
            GetTensionVolumeMultiplier(easedTension);

        source.pitch = Mathf.Lerp(
            1f,
            maxTensionPitch,
            easedTension
        );
    }

    private float GetTensionVolumeMultiplier(
        float tension)
    {
        return 1f +
               Mathf.Clamp01(tension) *
               maxTensionVolumeBoost;
    }

    private static float SmootherStep(float value)
    {
        value = Mathf.Clamp01(value);

        return value * value * value *
               (value * (value * 6f - 15f) + 10f);
    }

    private void StopCurrentFade()
    {
        if (fadeRoutine == null)
            return;

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    private void OnDisable()
    {
        StopCurrentFade();
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(
            0f,
            fadeInDuration
        );

        fadeOutDuration = Mathf.Max(
            0f,
            fadeOutDuration
        );

        clipTransitionDuration = Mathf.Max(
            0f,
            clipTransitionDuration
        );

        gameplayMusicBaseVolume = Mathf.Clamp01(
            gameplayMusicBaseVolume
        );

        maxTensionVolumeBoost = Mathf.Clamp(
            maxTensionVolumeBoost,
            0f,
            0.25f
        );

        maxTensionPitch = Mathf.Clamp(
            maxTensionPitch,
            1f,
            1.10f
        );

        tensionRiseSpeed = Mathf.Max(
            0.01f,
            tensionRiseSpeed
        );

        tensionFallSpeed = Mathf.Max(
            0.01f,
            tensionFallSpeed
        );
    }
}
