using UnityEngine;

/// <summary>
/// PC compatibility implementation. Google Play Games on PC has no phone
/// vibration path, but the public API is intentionally preserved because many
/// gameplay scripts already call it through optional feedback hooks.
/// </summary>
public sealed class VibrationManager : MonoBehaviour
{
    public static VibrationManager Instance { get; private set; }

    public bool CanVibrate => false;
    public bool IsEnabled => false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetEnabled(bool enabled) { }
    public void SetVibration(bool enabled) { }
    public void SetVibrationEnabled(bool enabled) { }
    public void Cancel() { }
    public void VibrateUI() { }
    public void VibrateCoin() { }
    public void VibrateDash() { }
    public void VibrateNearMiss(float intensity01 = 1f) { }
    public void VibrateClone() { }
    public void VibratePowerUp() { }
    public void VibrateArmorBreak() { }
    public void VibrateBossAoe() { }
    public void VibrateMiniBossAoe() { }
    public void VibrateBossSplit() { }
    public void VibrateSpaceBomb() { }
    public void VibrateSuccess() { }
    public void VibrateFailure() { }
    public void VibrateLight() { }
    public void VibrateMedium() { }
    public void VibrateHeavy() { }
}
