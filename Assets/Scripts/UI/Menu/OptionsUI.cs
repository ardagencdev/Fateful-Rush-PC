using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class OptionsUI : MonoBehaviour
{
    private const string SoundEnabledKey = "SoundOn";
    private const string VibrationEnabledKey = "VibrationEnabled";
    private const string FPSModeKey = "FPSMode";

    private const int DefaultSoundState = 1;
    private const int DefaultVibrationState = 1;
    private const int DefaultFPS = 60;

    private const float SelectedAlpha = 1f;
    private const float UnselectedAlpha = 0.35f;

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private UIPanelFadeSwitcher fadeSwitcher;

    [Header("Audio Buttons")]
    [SerializeField] private Button soundOnButton;
    [SerializeField] private Button soundOffButton;
    [SerializeField] private Button menuMusicOnButton;
    [SerializeField] private Button menuMusicOffButton;

    [Header("Audio Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Value Texts")]
    [SerializeField] private TMP_Text musicValueText;
    [SerializeField] private TMP_Text sfxValueText;
    [SerializeField] private TMP_Text hudOpacityValueText;

    [Header("Game Buttons")]
    [SerializeField] private Button vibrationOnButton;
    [SerializeField] private Button vibrationOffButton;
    [SerializeField] private Button fps30Button;
    [SerializeField] private Button fps60Button;
    [FormerlySerializedAs("joystickLeftButton")]
    [SerializeField] private Button hudPositionLeftButton;

    [FormerlySerializedAs("joystickRightButton")]
    [SerializeField] private Button hudPositionRightButton;

    [Header("Gameplay UI")]
    [SerializeField] private Slider hudOpacitySlider;

    private readonly Dictionary<Button, CanvasGroup> buttonCanvasGroups =
        new Dictionary<Button, CanvasGroup>();

    private SettingsManager settings;
    private VibrationManager vibrationManager;
    private ControlLayoutManager controlLayoutManager;

    private void Awake()
    {
        RefreshReferences();
        ConfigureNormalizedSlider(musicSlider);
        ConfigureNormalizedSlider(sfxSlider);
        ConfigureHUDOpacitySlider(hudOpacitySlider);
        CacheButtonCanvasGroups();
        ConfigureHUDPositionRow();
        HideMobileOnlyControls();
    }

    private void Start()
    {
        if (settings != null)
            settings.ApplyAllSettings();

        LoadSettingsToUI();
        SetInitialPanelState();
    }

    public void OpenOptions()
    {
        MainMenuStarColorRandomizer.Instance?
            .ShowOptionsColor();

        SwitchPanels(
            mainMenuPanel,
            optionsPanel
        );
    }

    public void CloseOptions()
    {
        MainMenuStarColorRandomizer.Instance?
            .ShowMainMenuColor();

        SwitchPanels(
            optionsPanel,
            mainMenuPanel
        );
    }

    public void SoundOn()
    {
        SetMasterSound(true);
    }

    public void SoundOff()
    {
        SetMasterSound(false);
    }

    public void MenuMusicOn()
    {
        RefreshReferences();

        if (settings != null)
            settings.SetMenuMusic(true);

        RefreshButtonStates();
    }

    public void MenuMusicOff()
    {
        RefreshReferences();

        if (settings != null)
            settings.SetMenuMusic(false);

        RefreshButtonStates();
    }

    public void ChangeMusicVolume(float value)
    {
        value = Mathf.Clamp01(value);

        RefreshReferences();

        if (settings != null)
            settings.SetMusicVolume(value);

        UpdatePercentText(musicValueText, value);
    }

    public void ChangeSFXVolume(float value)
    {
        value = Mathf.Clamp01(value);

        RefreshReferences();

        if (settings != null)
            settings.SetSFXVolume(value);

        UpdatePercentText(sfxValueText, value);
    }

    public void VibrationOn()
    {
        SetVibration(true);
    }

    public void VibrationOff()
    {
        SetVibration(false);
    }

    public void SetFPS30()
    {
        SetFPS(30);
    }

    public void SetFPS60()
    {
        SetFPS(60);
    }

    public void SetHUDPositionLeft()
    {
        RefreshReferences();

        if (settings != null)
            settings.SetHUDPositionLeft();
        else if (controlLayoutManager != null)
            controlLayoutManager.SetHUDPositionLeft();

        RefreshButtonStates();
    }

    public void SetHUDPositionRight()
    {
        RefreshReferences();

        if (settings != null)
            settings.SetHUDPositionRight();
        else if (controlLayoutManager != null)
            controlLayoutManager.SetHUDPositionRight();

        RefreshButtonStates();
    }

    // Existing scene Button OnClick bindings can keep their old method names.
    public void SetJoystickLeft() => SetHUDPositionLeft();
    public void SetJoystickRight() => SetHUDPositionRight();

    public void ChangeHUDOpacity(float value)
    {
        value = Mathf.Clamp(
            value,
            SettingsManager.MinimumHUDOpacity,
            SettingsManager.MaximumHUDOpacity
        );

        RefreshReferences();

        if (settings != null)
            settings.SetHUDOpacity(value);

        UpdatePercentText(hudOpacityValueText, value);
    }

    public bool IsOptionsOpen()
    {
        return optionsPanel != null &&
               optionsPanel.activeSelf;
    }

    public bool HandleEscapeBack()
    {
        if (!IsOptionsOpen())
            return false;

        CloseOptions();
        return true;
    }

    private void SetMasterSound(bool enabled)
    {
        RefreshReferences();

        if (settings != null)
        {
            settings.SetSound(enabled);
        }
        else
        {
            PlayerPrefs.SetInt(
                SoundEnabledKey,
                enabled ? 1 : 0
            );

            PlayerPrefs.Save();
            AudioListener.volume = enabled ? 1f : 0f;
        }

        RefreshButtonStates();
    }

    private void SetVibration(bool enabled)
    {
        RefreshReferences();

        if (settings != null)
        {
            settings.SetVibration(enabled);
        }
        else
        {
            PlayerPrefs.SetInt(
                VibrationEnabledKey,
                enabled ? 1 : 0
            );

            PlayerPrefs.Save();

            if (vibrationManager != null)
                vibrationManager.SetVibration(enabled);
        }

        RefreshButtonStates();
    }

    private void SetFPS(int targetFPS)
    {
        RefreshReferences();

        if (settings != null)
        {
            if (targetFPS == 30)
                settings.SetFPS30();
            else
                settings.SetFPS60();
        }
        else
        {
            int validatedFPS = targetFPS == 30 ? 30 : 60;
            PlayerPrefs.SetInt(FPSModeKey, validatedFPS);
            PlayerPrefs.Save();
            RuntimePerformancePolicy.ApplyFrameRate(validatedFPS);
        }

        RefreshButtonStates();
    }

    private int GetSavedFPS()
    {
        RefreshReferences();

        if (settings != null)
            return settings.GetFPS();

        int savedFPS = PlayerPrefs.GetInt(
            FPSModeKey,
            DefaultFPS
        );

        return savedFPS == 30 ? 30 : 60;
    }

    private void LoadSettingsToUI()
    {
        RefreshReferences();

        if (settings != null)
        {
            SetSliderValue(
                musicSlider,
                musicValueText,
                settings.GetMusicVolume()
            );

            SetSliderValue(
                sfxSlider,
                sfxValueText,
                settings.GetSFXVolume()
            );

            SetSliderValue(
                hudOpacitySlider,
                hudOpacityValueText,
                settings.GetHUDOpacity()
            );
        }

        RefreshButtonStates();
    }

    private void SetSliderValue(
        Slider slider,
        TMP_Text valueText,
        float value)
    {
        value = Mathf.Clamp01(value);

        if (slider != null)
            slider.SetValueWithoutNotify(value);

        UpdatePercentText(valueText, value);
    }

    private void RefreshButtonStates()
    {
        RefreshReferences();

        bool soundEnabled =
            settings != null
                ? settings.GetSound()
                : PlayerPrefs.GetInt(
                    SoundEnabledKey,
                    DefaultSoundState
                ) == 1;

        SetButtonState(
            soundOnButton,
            soundEnabled
        );

        SetButtonState(
            soundOffButton,
            !soundEnabled
        );

        if (settings != null)
        {
            bool menuMusicEnabled =
                settings.GetMenuMusic();

            SetButtonState(
                menuMusicOnButton,
                menuMusicEnabled
            );

            SetButtonState(
                menuMusicOffButton,
                !menuMusicEnabled
            );
        }

        bool vibrationEnabled =
            settings != null
                ? settings.GetVibration()
                : PlayerPrefs.GetInt(
                    VibrationEnabledKey,
                    DefaultVibrationState
                ) == 1;

        SetButtonState(
            vibrationOnButton,
            vibrationEnabled
        );

        SetButtonState(
            vibrationOffButton,
            !vibrationEnabled
        );

        int fps = GetSavedFPS();

        SetButtonState(
            fps30Button,
            fps == 30
        );

        SetButtonState(
            fps60Button,
            fps == 60
        );

        ControlLayoutManager.HudPosition hudPosition =
            settings != null
                ? settings.GetHUDPosition()
                : controlLayoutManager != null
                    ? controlLayoutManager.GetSavedPosition()
                    : ControlLayoutManager.HudPosition.Left;

        bool hudIsLeft =
            hudPosition == ControlLayoutManager.HudPosition.Left;

        SetButtonState(
            hudPositionLeftButton,
            hudIsLeft
        );

        SetButtonState(
            hudPositionRightButton,
            !hudIsLeft
        );
    }

    private void SetButtonState(
        Button button,
        bool selected)
    {
        if (button == null)
            return;

        CanvasGroup canvasGroup =
            GetButtonCanvasGroup(button);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = selected
                ? SelectedAlpha
                : UnselectedAlpha;
        }

        UIButtonEffect buttonEffect =
            button.GetComponent<UIButtonEffect>();

        if (buttonEffect != null)
            buttonEffect.SetSelected(selected);
    }

    private CanvasGroup GetButtonCanvasGroup(
        Button button)
    {
        if (buttonCanvasGroups.TryGetValue(
                button,
                out CanvasGroup cachedGroup))
        {
            return cachedGroup;
        }

        CanvasGroup canvasGroup =
            button.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup =
                button.gameObject.AddComponent<CanvasGroup>();
        }

        buttonCanvasGroups[button] = canvasGroup;

        return canvasGroup;
    }


    private void ConfigureHUDPositionRow()
    {
        Transform row = FindHUDPositionRow();

        if (row != null)
        {
            row.gameObject.SetActive(true);
        }

        SetButtonObjectActive(hudPositionLeftButton, true);
        SetButtonObjectActive(hudPositionRightButton, true);
    }

    private Transform FindHUDPositionRow()
    {
        Transform current =
            hudPositionLeftButton != null
                ? hudPositionLeftButton.transform
                : hudPositionRightButton != null
                    ? hudPositionRightButton.transform
                    : null;

        Transform fallback = current != null
            ? current.parent
            : null;

        for (int i = 0; current != null && i < 6; i++)
        {
            string objectName = current.gameObject.name;

            if (objectName.IndexOf(
                    "joystickrow",
                    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf(
                    "hudposition",
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return current;
            }

            current = current.parent;
        }

        return fallback;
    }

    private static bool IsInsideButton(
        Transform target,
        Button button)
    {
        if (target == null || button == null)
            return false;

        return target == button.transform ||
               target.IsChildOf(button.transform);
    }

    private void HideMobileOnlyControls()
    {
        SetButtonObjectActive(vibrationOnButton, false);
        SetButtonObjectActive(vibrationOffButton, false);
        SetButtonObjectActive(fps30Button, false);
        SetButtonObjectActive(fps60Button, false);
        SetButtonObjectActive(hudPositionLeftButton, true);
        SetButtonObjectActive(hudPositionRightButton, true);
    }

    private static void SetButtonObjectActive(Button button, bool active)
    {
        if (button != null)
            button.gameObject.SetActive(active);
    }

    private void CacheButtonCanvasGroups()
    {
        CacheButtonCanvasGroup(soundOnButton);
        CacheButtonCanvasGroup(soundOffButton);
        CacheButtonCanvasGroup(menuMusicOnButton);
        CacheButtonCanvasGroup(menuMusicOffButton);
        CacheButtonCanvasGroup(vibrationOnButton);
        CacheButtonCanvasGroup(vibrationOffButton);
        CacheButtonCanvasGroup(fps30Button);
        CacheButtonCanvasGroup(fps60Button);
        CacheButtonCanvasGroup(hudPositionLeftButton);
        CacheButtonCanvasGroup(hudPositionRightButton);
    }

    private void CacheButtonCanvasGroup(Button button)
    {
        if (button != null)
            GetButtonCanvasGroup(button);
    }

    private void SetInitialPanelState()
    {
        if (fadeSwitcher != null)
        {
            fadeSwitcher.SetInstant(
                mainMenuPanel,
                true
            );

            fadeSwitcher.SetInstant(
                optionsPanel,
                false
            );

            return;
        }

        SetPanel(mainMenuPanel, true);
        SetPanel(optionsPanel, false);
    }

    private void SwitchPanels(
        GameObject fromPanel,
        GameObject toPanel)
    {
        if (fadeSwitcher != null)
        {
            fadeSwitcher.SwitchPanel(
                fromPanel,
                toPanel
            );

            return;
        }

        SetPanel(fromPanel, false);
        SetPanel(toPanel, true);
    }

    private void RefreshReferences()
    {
        if (fadeSwitcher == null)
        {
            fadeSwitcher =
                GetComponent<UIPanelFadeSwitcher>();
        }

        if (settings == null)
        {
            settings =
                FindAnyObjectByType<SettingsManager>();
        }

        if (vibrationManager == null)
        {
            vibrationManager =
                FindAnyObjectByType<VibrationManager>();
        }

        if (controlLayoutManager == null)
        {
            controlLayoutManager =
                FindAnyObjectByType<ControlLayoutManager>();
        }
    }


    private static void ConfigureNormalizedSlider(Slider slider)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    private static void ConfigureHUDOpacitySlider(Slider slider)
    {
        if (slider == null)
            return;

        slider.minValue = SettingsManager.MinimumHUDOpacity;
        slider.maxValue = SettingsManager.MaximumHUDOpacity;
        slider.wholeNumbers = false;

        if (slider.value < slider.minValue)
        {
            slider.SetValueWithoutNotify(slider.minValue);
        }
    }

    private static void UpdatePercentText(
        TMP_Text text,
        float value)
    {
        if (text == null)
            return;

        int percentage = Mathf.RoundToInt(
            Mathf.Clamp01(value) * 100f
        );

        text.SetText("{0}%", percentage);
    }

    private static void SetPanel(
        GameObject panel,
        bool state)
    {
        if (panel != null)
            panel.SetActive(state);
    }
}