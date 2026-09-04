using UnityEngine;

/// <summary>
/// Kept under the original class/file name so existing scene references remain
/// valid. In the PC project it now applies only desktop/GPG-PC runtime policy;
/// all Adaptive Performance, thermal and mobile render-scale work is removed.
/// </summary>
[DisallowMultipleComponent]
public class MobilePerformanceSettings : MonoBehaviour
{
    [Header("PC Runtime")]
    [SerializeField] private bool runInBackground = true;
    [SerializeField] private bool preventScreenSleep = true;

    private void Awake()
    {
        Application.runInBackground = runInBackground;
        Screen.sleepTimeout = preventScreenSleep
            ? SleepTimeout.NeverSleep
            : SleepTimeout.SystemSetting;

        RuntimePerformancePolicy.ApplyFrameRate(60);
    }
}
