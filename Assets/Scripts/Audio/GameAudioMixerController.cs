using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
public sealed class GameAudioMixerController : MonoBehaviour
{
    public enum AudioBus
    {
        Music,
        GameplaySFX,
        UISFX,
        CriticalSFX
    }

    private const string MixerResourcePath = "GameAudioMixer";

    private const string MusicVolumeParameter = "MusicVolume";
    private const string SFXVolumeParameter = "SFXVolume";

    private const string MusicGroupName = "MusicContent";
    private const string GameplayGroupName = "GameplaySFX";
    private const string UIGroupName = "UISFX";
    private const string CriticalGroupName = "CriticalSFX";

    private const string NormalSnapshotName = "Normal";
    private const string PausedSnapshotName = "Paused";
    private const string BossDangerSnapshotName = "BossDanger";
    private const string SlowMotionSnapshotName = "SlowMotion";
    private const string BossDangerSlowSnapshotName = "BossDangerSlow";

    private const float MinimumDecibels = -80f;

    public static GameAudioMixerController Instance { get; private set; }

    public static bool IsReady =>
        Instance != null &&
        Instance.mixer != null &&
        Instance.musicGroup != null &&
        Instance.gameplayGroup != null &&
        Instance.uiGroup != null &&
        Instance.criticalGroup != null;

    private AudioMixer mixer;

    private AudioMixerGroup musicGroup;
    private AudioMixerGroup gameplayGroup;
    private AudioMixerGroup uiGroup;
    private AudioMixerGroup criticalGroup;

    private AudioMixerSnapshot normalSnapshot;
    private AudioMixerSnapshot pausedSnapshot;
    private AudioMixerSnapshot bossDangerSnapshot;
    private AudioMixerSnapshot slowMotionSnapshot;
    private AudioMixerSnapshot bossDangerSlowSnapshot;

    private readonly HashSet<Object> bossDangerOwners =
        new HashSet<Object>();

    private bool isPaused;
    private bool isSlowMotionActive;
    private bool warnedMissingMixer;
    private bool warnedMissingMusicParameter;
    private bool warnedMissingSFXParameter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        GameObject controllerObject =
            new GameObject("GameAudioMixerController");

        DontDestroyOnLoad(controllerObject);
        controllerObject.AddComponent<GameAudioMixerController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadMixerAndRouting();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        // Unity recommends changing exposed AudioMixer parameters from Start
        // or later rather than Awake/OnEnable.
        ApplySavedVolumes();
        RefreshSnapshot(0f);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode loadMode)
    {
        if (loadMode != LoadSceneMode.Single)
            return;

        // AudioMixerController survives scene changes. Gameplay-only states
        // must never leak into a restarted level or the main menu.
        ResetTransientState(0f);
    }

    public AudioMixerGroup GetGroup(AudioBus bus)
    {
        return bus switch
        {
            AudioBus.Music => musicGroup,
            AudioBus.UISFX => uiGroup,
            AudioBus.CriticalSFX => criticalGroup,
            _ => gameplayGroup
        };
    }

    public void RouteSource(AudioSource source, AudioBus bus)
    {
        if (source == null || !IsReady)
            return;

        AudioMixerGroup group = GetGroup(bus);

        if (group != null)
            source.outputAudioMixerGroup = group;
    }

    public static void Route(
        AudioSource source,
        AudioBus bus)
    {
        Instance?.RouteSource(source, bus);
    }

    public void ApplySavedVolumes()
    {
        if (mixer == null)
            return;

        SetMusicVolume(
            PlayerPrefs.GetFloat("MusicVolume", 1f)
        );

        SetSFXVolume(
            PlayerPrefs.GetFloat("SFXVolume", 1f)
        );
    }

    public void SetMusicVolume(float normalizedVolume)
    {
        if (mixer == null)
            return;

        bool applied = mixer.SetFloat(
            MusicVolumeParameter,
            LinearToDecibels(normalizedVolume)
        );

        if (!applied && !warnedMissingMusicParameter)
        {
            warnedMissingMusicParameter = true;
            Debug.LogWarning(
                $"Audio Mixer exposed parameter '{MusicVolumeParameter}' bulunamadi. " +
                "Music grubunun Volume parametresini expose edip adini birebir MusicVolume yap."
            );
        }
    }

    public void SetSFXVolume(float normalizedVolume)
    {
        if (mixer == null)
            return;

        bool applied = mixer.SetFloat(
            SFXVolumeParameter,
            LinearToDecibels(normalizedVolume)
        );

        if (!applied && !warnedMissingSFXParameter)
        {
            warnedMissingSFXParameter = true;
            Debug.LogWarning(
                $"Audio Mixer exposed parameter '{SFXVolumeParameter}' bulunamadi. " +
                "SFX grubunun Volume parametresini expose edip adini birebir SFXVolume yap."
            );
        }
    }

    public static void ResetTransientState(
        float transitionDuration = 0f)
    {
        if (Instance == null)
            return;

        Instance.isPaused = false;
        Instance.isSlowMotionActive = false;
        Instance.bossDangerOwners.Clear();
        Instance.RefreshSnapshot(transitionDuration);
    }

    public static void SetPaused(bool paused)
    {
        if (Instance == null)
            return;

        Instance.isPaused = paused;
        Instance.RefreshSnapshot(
            paused ? 0.12f : 0.18f
        );
    }

    public static void SetSlowMotion(bool active)
    {
        if (Instance == null)
            return;

        Instance.isSlowMotionActive = active;
        Instance.RefreshSnapshot(
            active ? 0.18f : 0.25f
        );
    }

    public static void SetBossDanger(
        Object owner,
        bool active)
    {
        if (Instance == null || owner == null)
            return;

        if (active)
            Instance.bossDangerOwners.Add(owner);
        else
            Instance.bossDangerOwners.Remove(owner);

        Instance.RefreshSnapshot(
            active ? 0.15f : 0.22f
        );
    }

    private void LoadMixerAndRouting()
    {
        mixer = Resources.Load<AudioMixer>(
            MixerResourcePath
        );

        if (mixer == null)
        {
            WarnMissingMixerOnce();
            return;
        }

        // Snapshot transitions must continue while Time.timeScale == 0.
        mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;

        musicGroup = FindGroupExact(MusicGroupName);
        gameplayGroup = FindGroupExact(GameplayGroupName);
        uiGroup = FindGroupExact(UIGroupName);
        criticalGroup = FindGroupExact(CriticalGroupName);

        normalSnapshot = mixer.FindSnapshot(NormalSnapshotName);
        pausedSnapshot = mixer.FindSnapshot(PausedSnapshotName);
        bossDangerSnapshot = mixer.FindSnapshot(BossDangerSnapshotName);
        slowMotionSnapshot = mixer.FindSnapshot(SlowMotionSnapshotName);
        bossDangerSlowSnapshot = mixer.FindSnapshot(BossDangerSlowSnapshotName);

        ValidateSetup();
    }

    private AudioMixerGroup FindGroupExact(string groupName)
    {
        if (mixer == null)
            return null;

        AudioMixerGroup[] groups =
            mixer.FindMatchingGroups(groupName);

        for (int i = 0; i < groups.Length; i++)
        {
            AudioMixerGroup group = groups[i];

            if (group != null && group.name == groupName)
                return group;
        }

        return null;
    }

    private void RefreshSnapshot(float transitionDuration)
    {
        if (mixer == null)
            return;

        RemoveDestroyedDangerOwners();

        AudioMixerSnapshot targetSnapshot =
            GetHighestPrioritySnapshot();

        if (targetSnapshot == null)
            return;

        targetSnapshot.TransitionTo(
            Mathf.Max(0f, transitionDuration)
        );
    }

    private AudioMixerSnapshot GetHighestPrioritySnapshot()
    {
        if (isPaused && pausedSnapshot != null)
            return pausedSnapshot;

        bool bossDangerActive =
            bossDangerOwners.Count > 0;

        if (bossDangerActive &&
            isSlowMotionActive &&
            bossDangerSlowSnapshot != null)
        {
            return bossDangerSlowSnapshot;
        }

        if (bossDangerActive &&
            bossDangerSnapshot != null)
        {
            return bossDangerSnapshot;
        }

        if (isSlowMotionActive &&
            slowMotionSnapshot != null)
        {
            return slowMotionSnapshot;
        }

        return normalSnapshot;
    }

    private void RemoveDestroyedDangerOwners()
    {
        if (bossDangerOwners.Count == 0)
            return;

        bossDangerOwners.RemoveWhere(
            owner => owner == null
        );
    }

    private static float LinearToDecibels(float value)
    {
        value = Mathf.Clamp01(value);

        if (value <= 0.0001f)
            return MinimumDecibels;

        return Mathf.Clamp(
            20f * Mathf.Log10(value),
            MinimumDecibels,
            0f
        );
    }

    private void ValidateSetup()
    {
        if (musicGroup == null ||
            gameplayGroup == null ||
            uiGroup == null ||
            criticalGroup == null)
        {
            Debug.LogWarning(
                "GameAudioMixer group yapisi eksik. Beklenen gruplar: " +
                "MusicContent, GameplaySFX, UISFX, CriticalSFX."
            );
        }

        if (normalSnapshot == null ||
            pausedSnapshot == null ||
            bossDangerSnapshot == null ||
            slowMotionSnapshot == null ||
            bossDangerSlowSnapshot == null)
        {
            Debug.LogWarning(
                "GameAudioMixer snapshot yapisi eksik. Beklenen snapshotlar: " +
                "Normal, Paused, BossDanger, SlowMotion, BossDangerSlow."
            );
        }
    }

    private void WarnMissingMixerOnce()
    {
        if (warnedMissingMixer)
            return;

        warnedMissingMixer = true;

        Debug.LogWarning(
            "Assets/Resources/GameAudioMixer.mixer bulunamadi. " +
            "Mixer kurulana kadar mevcut AudioSource volume sistemi fallback olarak calismaya devam edecek."
        );
    }
}
