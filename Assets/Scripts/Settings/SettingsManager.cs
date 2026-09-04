using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    private const string SoundKey = "SoundOn";
    private const string MenuMusicKey = "MenuMusicOn";
    private const string MusicVolumeKey = "MusicVolume";
    private const string SFXVolumeKey = "SFXVolume";
    private const string VibrationKey = "VibrationEnabled";
    private const string FPSKey = "FPSMode";
    private const string HUDOpacityKey = "HUDOpacity";
    private const string LanguageKey = "Language";

    public const float MinimumHUDOpacity = 0.25f;
    public const float MaximumHUDOpacity = 1f;

    private const int DefaultFPS = 60;
    private const string DefaultLanguage = "EN";

    [Header("HUD Opacity")]
    [SerializeField]
    private CanvasGroup hudCanvasGroup;

    private MenuMusicApply cachedMenuMusic;
    private GameplayMusicFade[] cachedGameplayMusic;
    private SoundManager cachedSoundManager;
    private AudioSettingsApply[] cachedAudioAppliers;
    private bool prefsDirty;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        CacheAudioReferences();
    }

    private void Start()
    {
        ApplyAllSettings();
    }

    public void ApplyAllSettings()
    {
        ApplySound();
        ApplyMenuMusic();
        ApplyGameplayMusic();
        ApplySFX();
        ApplyVibration();
        ApplyFPS();
        ApplyHUDOpacity();
        ApplyHUDPosition();
    }

    #region Sound

    public void SetSound(bool value)
    {
        PlayerPrefs.SetInt(
            SoundKey,
            value ? 1 : 0
        );

        PlayerPrefs.Save();

        ApplySound();
        ApplyMenuMusic();
        ApplyGameplayMusic();
        ApplySFX();
        ApplyAudioAppliers();
    }

    public bool GetSound()
    {
        return PlayerPrefs.GetInt(
            SoundKey,
            1
        ) == 1;
    }

    private void ApplySound()
    {
        AudioListener.volume =
            GetSound() ? 1f : 0f;
    }

    #endregion

    #region Menu Music

    public void SetMenuMusic(bool value)
    {
        PlayerPrefs.SetInt(
            MenuMusicKey,
            value ? 1 : 0
        );

        PlayerPrefs.Save();

        ApplyMenuMusic();
    }

    public bool GetMenuMusic()
    {
        return PlayerPrefs.GetInt(
            MenuMusicKey,
            1
        ) == 1;
    }

    public void SetMusicVolume(float value)
    {
        value = Mathf.Clamp01(value);

        PlayerPrefs.SetFloat(
            MusicVolumeKey,
            value
        );

        prefsDirty = true;

        GameAudioMixerController.Instance?
            .SetMusicVolume(value);

        ApplyMenuMusic();
        ApplyGameplayMusic();
        ApplyAudioAppliers();
    }

    public float GetMusicVolume()
    {
        return Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                MusicVolumeKey,
                1f
            )
        );
    }

    private void ApplyMenuMusic()
    {
        if (cachedMenuMusic == null)
        {
            cachedMenuMusic =
                FindAnyObjectByType<MenuMusicApply>();
        }

        if (cachedMenuMusic != null)
            cachedMenuMusic.ApplyMusicVolume();
    }

    private void ApplyGameplayMusic()
    {
        if (cachedGameplayMusic == null ||
            cachedGameplayMusic.Length == 0)
        {
            cachedGameplayMusic =
                UnityFindCompat.FindObjectsByType<GameplayMusicFade>(
                    FindObjectsInactive.Exclude
                );
        }

        for (int i = 0; i < cachedGameplayMusic.Length; i++)
        {
            GameplayMusicFade gameplayMusic =
                cachedGameplayMusic[i];

            if (gameplayMusic != null)
                gameplayMusic.RefreshVolume();
        }
    }

    #endregion

    #region SFX

    public void SetSFXVolume(float value)
    {
        value = Mathf.Clamp01(value);

        PlayerPrefs.SetFloat(
            SFXVolumeKey,
            value
        );

        prefsDirty = true;

        GameAudioMixerController.Instance?
            .SetSFXVolume(value);

        ApplySFX();
        ApplyAudioAppliers();
    }

    public float GetSFXVolume()
    {
        return Mathf.Clamp01(
            PlayerPrefs.GetFloat(
                SFXVolumeKey,
                1f
            )
        );
    }

    private void ApplySFX()
    {
        if (cachedSoundManager == null)
        {
            cachedSoundManager =
                SoundManager.Instance != null
                    ? SoundManager.Instance
                    : FindAnyObjectByType<SoundManager>();
        }

        if (cachedSoundManager != null)
            cachedSoundManager.ApplySFXVolume();
    }

    private void ApplyAudioAppliers()
    {
        if (cachedAudioAppliers == null)
        {
            cachedAudioAppliers =
                UnityFindCompat.FindObjectsByType<AudioSettingsApply>(
                FindObjectsInactive.Exclude
            );
        }

        for (int i = 0; i < cachedAudioAppliers.Length; i++)
        {
            AudioSettingsApply applier =
                cachedAudioAppliers[i];

            if (applier != null)
                applier.Apply();
        }
    }

    private void CacheAudioReferences()
    {
        cachedMenuMusic =
            FindAnyObjectByType<MenuMusicApply>();

        cachedGameplayMusic =
            UnityFindCompat.FindObjectsByType<GameplayMusicFade>(
                FindObjectsInactive.Exclude
            );

        cachedSoundManager =
            SoundManager.Instance != null
                ? SoundManager.Instance
                : FindAnyObjectByType<SoundManager>();

        cachedAudioAppliers =
            UnityFindCompat.FindObjectsByType<AudioSettingsApply>(
                FindObjectsInactive.Exclude
            );
    }

    private void FlushPendingPrefs()
    {
        if (!prefsDirty)
            return;

        PlayerPrefs.Save();
        prefsDirty = false;
    }

    #endregion

    #region Vibration

    public void SetVibration(bool value)
    {
        PlayerPrefs.SetInt(
            VibrationKey,
            value ? 1 : 0
        );

        PlayerPrefs.Save();

        ApplyVibration();
    }

    public bool GetVibration()
    {
        return PlayerPrefs.GetInt(
            VibrationKey,
            1
        ) == 1;
    }

    private void ApplyVibration()
    {
        if (VibrationManager.Instance != null)
        {
            VibrationManager.Instance.SetVibration(
                GetVibration()
            );
        }
    }

    #endregion

    #region HUD Position

    public void SetHUDPositionLeft()
    {
        if (ControlLayoutManager.Instance != null)
        {
            ControlLayoutManager.Instance.SetHUDPositionLeft();
        }
        else
        {
            // Android-equivalent layout: visible HUD left = old joystick right.
            SaveJoystickSideFallback(
                ControlLayoutManager.JoystickSide.Right
            );
        }
    }

    public void SetHUDPositionRight()
    {
        if (ControlLayoutManager.Instance != null)
        {
            ControlLayoutManager.Instance.SetHUDPositionRight();
        }
        else
        {
            // Android-equivalent layout: visible HUD right = old joystick left.
            SaveJoystickSideFallback(
                ControlLayoutManager.JoystickSide.Left
            );
        }
    }

    public ControlLayoutManager.HudPosition GetHUDPosition()
    {
        int savedSide = GetJoystickSide();

        return savedSide == (int)ControlLayoutManager.JoystickSide.Right
            ? ControlLayoutManager.HudPosition.Left
            : ControlLayoutManager.HudPosition.Right;
    }

    private void ApplyHUDPosition()
    {
        if (ControlLayoutManager.Instance != null)
        {
            ControlLayoutManager.Instance.ApplySavedLayout();
        }
    }

    private static void SaveJoystickSideFallback(
        ControlLayoutManager.JoystickSide side)
    {
        PlayerPrefs.SetInt(
            "JoystickSide",
            (int)side
        );

        PlayerPrefs.Save();
    }

    // Keep existing scene UnityEvent bindings compatible.
    public void SetJoystickLeft()
    {
        if (ControlLayoutManager.Instance != null)
            ControlLayoutManager.Instance.SetJoystickLeft();
        else
            SaveJoystickSideFallback(ControlLayoutManager.JoystickSide.Left);
    }

    public void SetJoystickRight()
    {
        if (ControlLayoutManager.Instance != null)
            ControlLayoutManager.Instance.SetJoystickRight();
        else
            SaveJoystickSideFallback(ControlLayoutManager.JoystickSide.Right);
    }

    public int GetJoystickSide()
    {
        return PlayerPrefs.GetInt(
            "JoystickSide",
            (int)ControlLayoutManager.JoystickSide.Right
        );
    }

    #endregion

    #region FPS

    public void SetFPS30()
    {
        SetFPS(30);
    }

    public void SetFPS60()
    {
        SetFPS(60);
    }

    private void SetFPS(int fps)
    {
        int validatedFPS =
            fps == 30 ? 30 : 60;

        PlayerPrefs.SetInt(
            FPSKey,
            validatedFPS
        );

        PlayerPrefs.Save();

        ApplyFPS();
    }

    public int GetFPS()
    {
        int savedFPS = PlayerPrefs.GetInt(
            FPSKey,
            DefaultFPS
        );

        return savedFPS == 30 ? 30 : 60;
    }

    private void ApplyFPS()
    {
        RuntimePerformancePolicy.ApplyFrameRate(GetFPS());
    }

    #endregion

    #region HUD Opacity

    public void SetHUDOpacity(float value)
    {
        value = Mathf.Clamp(
            value,
            MinimumHUDOpacity,
            MaximumHUDOpacity
        );

        PlayerPrefs.SetFloat(
            HUDOpacityKey,
            value
        );

        prefsDirty = true;

        ApplyHUDOpacity();
    }

    public float GetHUDOpacity()
    {
        return Mathf.Clamp(
            PlayerPrefs.GetFloat(
                HUDOpacityKey,
                MaximumHUDOpacity
            ),
            MinimumHUDOpacity,
            MaximumHUDOpacity
        );
    }

    private void ApplyHUDOpacity()
    {
        if (hudCanvasGroup != null)
        {
            hudCanvasGroup.alpha =
                GetHUDOpacity();
        }
    }

    #endregion

    #region Language

    public void SetLanguage(string languageCode)
    {
        string validatedLanguage =
            ValidateLanguageCode(languageCode);

        PlayerPrefs.SetString(
            LanguageKey,
            validatedLanguage
        );

        PlayerPrefs.Save();
    }

    public string GetLanguage()
    {
        string savedLanguage =
            PlayerPrefs.GetString(
                LanguageKey,
                DefaultLanguage
            );

        return ValidateLanguageCode(savedLanguage);
    }

    private static string ValidateLanguageCode(
        string languageCode
    )
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return DefaultLanguage;

        string normalizedCode =
            languageCode.Trim().ToUpperInvariant();

        return normalizedCode switch
        {
            "TR" => "TR",
            "RU" => "RU",
            "CN" => "CN",
            _ => DefaultLanguage
        };
    }

    #endregion

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            FlushPendingPrefs();
    }

    private void OnApplicationQuit()
    {
        FlushPendingPrefs();
    }

    private void OnDisable()
    {
        FlushPendingPrefs();
    }

    private void OnDestroy()
    {
        FlushPendingPrefs();

        if (Instance == this)
            Instance = null;
    }
}