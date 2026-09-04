using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MenuMusicApply : MonoBehaviour
{
    [Header("Menu Music")]
    [SerializeField]
    private AudioClip[] menuMusics;

    [Tooltip(
        "Oyun ilk açıldığında çalacak parçanın listedeki index değeri."
    )]
    [SerializeField, Min(0)]
    private int firstTrackIndex;

    [SerializeField, Range(0f, 1f)]
    private float menuMusicBaseVolume = 0.2f;

    [Header("Transitions")]
    [SerializeField, Min(0f)]
    private float fadeInDuration = 1.5f;

    [SerializeField, Min(0.1f)]
    private float crossfadeDuration = 3f;

    [SerializeField, Min(0f)]
    private float fadeOutDuration = 0.8f;

    public float FadeOutDuration => fadeOutDuration;

    private AudioSource sourceA;
    private AudioSource sourceB;

    private AudioSource activeSource;
    private AudioSource standbySource;

    private Coroutine playlistRoutine;
    private Coroutine volumeRoutine;

    private readonly List<int> shuffledPlaylist = new();

    private int playlistPosition;
    private int lastPlayedIndex = -1;

    private float masterVolume;
    private float activeGain = 1f;
    private float standbyGain;

    private bool isStoppingMusic;

    private void Awake()
    {
        PrepareAudioSources();
    }

    private void Start()
    {
        StartMenuPlaylist();
    }

    private void PrepareAudioSources()
    {
        AudioSource[] sources =
            GetComponents<AudioSource>();

        sourceA = sources[0];

        if (sources.Length >= 2)
        {
            sourceB = sources[1];
        }
        else
        {
            sourceB =
                gameObject.AddComponent<AudioSource>();

            CopyAudioSourceSettings(
                sourceA,
                sourceB
            );
        }

        ConfigureAudioSource(sourceA);
        ConfigureAudioSource(sourceB);

        activeSource = sourceA;
        standbySource = sourceB;

        masterVolume = 0f;
        activeGain = 1f;
        standbyGain = 0f;

        ApplySourceVolumes();
    }

    private static void ConfigureAudioSource(
        AudioSource source
    )
    {
        source.playOnAwake = false;
        source.loop = false;
        source.mute = false;
        source.spatialBlend = 0f;
        source.volume = 0f;

        GameAudioMixerController.Route(
            source,
            GameAudioMixerController.AudioBus.Music
        );
    }

    private static void CopyAudioSourceSettings(
        AudioSource source,
        AudioSource target
    )
    {
        target.outputAudioMixerGroup =
            source.outputAudioMixerGroup;

        target.priority = source.priority;
        target.pitch = source.pitch;
        target.panStereo = source.panStereo;
        target.spatialBlend = source.spatialBlend;

        target.bypassEffects =
            source.bypassEffects;

        target.bypassListenerEffects =
            source.bypassListenerEffects;

        target.bypassReverbZones =
            source.bypassReverbZones;
    }

    private void StartMenuPlaylist()
    {
        StopActiveRoutines();

        sourceA.Stop();
        sourceB.Stop();

        isStoppingMusic = false;

        AudioClip firstClip =
            GetFirstValidTrack(
                out int firstIndex
            );

        if (firstClip == null)
        {
            Debug.LogWarning(
                "MenuMusicApply üzerinde geçerli bir menü müziği bulunamadı.",
                this
            );

            return;
        }

        lastPlayedIndex = firstIndex;

        activeSource.clip = firstClip;
        activeSource.volume = 0f;
        activeSource.Play();

        activeGain = 1f;
        standbyGain = 0f;
        masterVolume = 0f;

        ApplySourceVolumes();

        StartMasterVolumeFade(
            GetTargetVolume(),
            fadeInDuration
        );

        playlistRoutine =
            StartCoroutine(
                MenuPlaylistRoutine()
            );
    }

    private IEnumerator MenuPlaylistRoutine()
    {
        while (activeSource != null &&
               activeSource.clip != null)
        {
            float transitionDuration =
                GetSafeCrossfadeDuration(
                    activeSource.clip
                );

            float remainingTime =
                Mathf.Max(
                    0f,
                    activeSource.clip.length -
                    activeSource.time -
                    transitionDuration
                );

            yield return new WaitForSecondsRealtime(
                remainingTime
            );

            AudioClip nextClip =
                GetNextShuffledMusic();

            if (nextClip == null)
            {
                playlistRoutine = null;
                yield break;
            }

            standbySource.clip = nextClip;
            standbySource.volume = 0f;
            standbySource.Play();

            standbyGain = 0f;

            yield return CrossfadeRoutine(
                transitionDuration
            );

            activeSource.Stop();
            activeSource.clip = null;

            SwapSources();

            activeGain = 1f;
            standbyGain = 0f;

            ApplySourceVolumes();
        }

        playlistRoutine = null;
    }

    private IEnumerator CrossfadeRoutine(
        float duration
    )
    {
        duration = Mathf.Max(0.01f, duration);

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(
                    timer / duration
                );

            /*
             * Equal-power crossfade:
             * Ortada iki müzik de duyulurken ses seviyesinin
             * aniden düşmesini engeller.
             */
            activeGain = Mathf.Cos(
                progress *
                Mathf.PI *
                0.5f
            );

            standbyGain = Mathf.Sin(
                progress *
                Mathf.PI *
                0.5f
            );

            ApplySourceVolumes();

            yield return null;
        }

        activeGain = 0f;
        standbyGain = 1f;

        ApplySourceVolumes();
    }

    private void SwapSources()
    {
        AudioSource previousActive =
            activeSource;

        activeSource = standbySource;
        standbySource = previousActive;
    }

    private AudioClip GetFirstValidTrack(
        out int selectedIndex
    )
    {
        selectedIndex = -1;

        if (menuMusics == null ||
            menuMusics.Length == 0)
        {
            return null;
        }

        int safeFirstIndex =
            Mathf.Clamp(
                firstTrackIndex,
                0,
                menuMusics.Length - 1
            );

        if (menuMusics[safeFirstIndex] != null)
        {
            selectedIndex = safeFirstIndex;
            return menuMusics[safeFirstIndex];
        }

        for (int i = 0;
             i < menuMusics.Length;
             i++)
        {
            if (menuMusics[i] == null)
                continue;

            selectedIndex = i;
            return menuMusics[i];
        }

        return null;
    }

    private AudioClip GetNextShuffledMusic()
    {
        if (menuMusics == null ||
            menuMusics.Length == 0)
        {
            return null;
        }

        if (shuffledPlaylist.Count == 0 ||
            playlistPosition >=
            shuffledPlaylist.Count)
        {
            BuildNewShufflePlaylist();
        }

        if (shuffledPlaylist.Count == 0)
            return null;

        int index =
            shuffledPlaylist[
                playlistPosition
            ];

        playlistPosition++;
        lastPlayedIndex = index;

        return menuMusics[index];
    }

    private void BuildNewShufflePlaylist()
    {
        shuffledPlaylist.Clear();

        if (menuMusics == null)
            return;

        for (int i = 0;
             i < menuMusics.Length;
             i++)
        {
            if (menuMusics[i] != null)
                shuffledPlaylist.Add(i);
        }

        for (int i = 0;
             i < shuffledPlaylist.Count;
             i++)
        {
            int randomIndex =
                Random.Range(
                    i,
                    shuffledPlaylist.Count
                );

            (
                shuffledPlaylist[i],
                shuffledPlaylist[randomIndex]
            ) =
            (
                shuffledPlaylist[randomIndex],
                shuffledPlaylist[i]
            );
        }

        if (shuffledPlaylist.Count > 1 &&
            shuffledPlaylist[0] ==
            lastPlayedIndex)
        {
            int swapIndex =
                Random.Range(
                    1,
                    shuffledPlaylist.Count
                );

            (
                shuffledPlaylist[0],
                shuffledPlaylist[swapIndex]
            ) =
            (
                shuffledPlaylist[swapIndex],
                shuffledPlaylist[0]
            );
        }

        playlistPosition = 0;
    }

    private float GetSafeCrossfadeDuration(
        AudioClip clip
    )
    {
        if (clip == null)
            return 0.01f;

        return Mathf.Clamp(
            crossfadeDuration,
            0.01f,
            Mathf.Max(
                0.01f,
                clip.length * 0.5f
            )
        );
    }

    public void ApplyMusicVolume()
    {
        sourceA.mute = false;
        sourceB.mute = false;

        RefreshVolume();
    }

    public void RefreshVolume()
    {
        if (isStoppingMusic)
            return;

        StartMasterVolumeFade(
            GetTargetVolume(),
            0.2f
        );
    }

    public void FadeOutMusic()
    {
        if (isStoppingMusic)
            return;

        isStoppingMusic = true;

        if (playlistRoutine != null)
        {
            StopCoroutine(playlistRoutine);
            playlistRoutine = null;
        }

        StartMasterVolumeFade(
            0f,
            fadeOutDuration,
            true
        );
    }

    private void StartMasterVolumeFade(
        float targetVolume,
        float duration,
        bool stopAfter = false
    )
    {
        if (volumeRoutine != null)
        {
            StopCoroutine(volumeRoutine);
            volumeRoutine = null;
        }

        volumeRoutine =
            StartCoroutine(
                MasterVolumeFadeRoutine(
                    targetVolume,
                    duration,
                    stopAfter
                )
            );
    }

    private IEnumerator MasterVolumeFadeRoutine(
        float targetVolume,
        float duration,
        bool stopAfter
    )
    {
        duration = Mathf.Max(
            0.01f,
            duration
        );

        float startVolume =
            masterVolume;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(
                    timer / duration
                );

            progress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress
                );

            masterVolume =
                Mathf.Lerp(
                    startVolume,
                    targetVolume,
                    progress
                );

            ApplySourceVolumes();

            yield return null;
        }

        masterVolume = targetVolume;
        ApplySourceVolumes();

        if (stopAfter)
        {
            sourceA.Stop();
            sourceB.Stop();

            sourceA.clip = null;
            sourceB.clip = null;
        }

        volumeRoutine = null;
    }

    private void ApplySourceVolumes()
    {
        if (activeSource != null)
        {
            activeSource.volume =
                masterVolume *
                activeGain;
        }

        if (standbySource != null)
        {
            standbySource.volume =
                masterVolume *
                standbyGain;
        }
    }

    private float GetTargetVolume()
    {
        bool soundOn =
            PlayerPrefs.GetInt(
                "SoundOn",
                1
            ) == 1;

        bool menuMusicOn =
            PlayerPrefs.GetInt(
                "MenuMusicOn",
                1
            ) == 1;

        if (!soundOn || !menuMusicOn)
            return 0f;

        if (GameAudioMixerController.IsReady)
            return menuMusicBaseVolume;

        float musicVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                "MusicVolume",
                1f
            )
        );

        return musicVolume *
               menuMusicBaseVolume;
    }

    private void StopActiveRoutines()
    {
        if (playlistRoutine != null)
        {
            StopCoroutine(playlistRoutine);
            playlistRoutine = null;
        }

        if (volumeRoutine != null)
        {
            StopCoroutine(volumeRoutine);
            volumeRoutine = null;
        }
    }

    private void OnDisable()
    {
        StopActiveRoutines();

        if (sourceA != null)
        {
            sourceA.Stop();
            sourceA.volume = 0f;
        }

        if (sourceB != null)
        {
            sourceB.Stop();
            sourceB.volume = 0f;
        }
    }

    private void OnValidate()
    {
        firstTrackIndex =
            Mathf.Max(0, firstTrackIndex);

        menuMusicBaseVolume =
            Mathf.Clamp01(
                menuMusicBaseVolume
            );

        fadeInDuration =
            Mathf.Max(
                0f,
                fadeInDuration
            );

        crossfadeDuration =
            Mathf.Max(
                0.1f,
                crossfadeDuration
            );

        fadeOutDuration =
            Mathf.Max(
                0f,
                fadeOutDuration
            );
    }
}