using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class UISliderDynamicSound : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Sound")]
    [SerializeField] private AudioClip sliderMoveSound;

    [Header("Movement Sensitivity")]
    [Tooltip("Bu hiz ve altinda ses cok hafif olur.")]
    [SerializeField] private float slowSpeed = 0.15f;

    [Tooltip("Bu hizda ses maksimum hissiyata ulasir.")]
    [SerializeField] private float fastSpeed = 4f;

    [SerializeField] private float speedSmoothing = 14f;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float slowVolume = 0.10f;

    [Range(0f, 1f)]
    [SerializeField] private float fastVolume = 0.55f;

    [Header("Pitch")]
    [SerializeField] private float slowPitch = 0.96f;
    [SerializeField] private float fastPitch = 1.12f;

    [Header("Trigger")]
    [Tooltip("Slider uzerinde bu kadar mesafe gidilmeden yeni ses uretilmez.")]
    [Range(0.001f, 0.1f)]
    [SerializeField] private float minimumTravel = 0.012f;

    [Tooltip("Yavas harekette iki ses arasindaki minimum sure.")]
    [SerializeField] private float slowInterval = 0.075f;

    [Tooltip("Hizli harekette iki ses arasindaki minimum sure.")]
    [SerializeField] private float fastInterval = 0.028f;

    [Header("Stereo Position")]
    [Range(0f, 1f)]
    [SerializeField] private float stereoPanAmount = 0.25f;

    private Slider slider;
    private RectTransform rectTransform;
    private AudioSource audioSource;

    private bool interacting;

    private float lastNormalizedValue;
    private float lastSampleTime;
    private float lastPlayTime = -10f;

    private float accumulatedTravel;
    private float smoothedSpeed;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        rectTransform = transform as RectTransform;

        CreateAudioSource();
    }

    private void Update()
    {
        if (!interacting || slider == null)
            return;

        float currentTime = Time.unscaledTime;

        float deltaTime =
            Mathf.Max(
                currentTime - lastSampleTime,
                0.0001f
            );

        float currentValue =
            GetNormalizedSliderValue();

        float movement =
            Mathf.Abs(
                currentValue - lastNormalizedValue
            );

        lastNormalizedValue = currentValue;
        lastSampleTime = currentTime;

        if (movement <= 0f)
        {
            smoothedSpeed = Mathf.Lerp(
                smoothedSpeed,
                0f,
                1f - Mathf.Exp(
                    -speedSmoothing * deltaTime
                )
            );

            return;
        }

        float instantSpeed =
            movement / deltaTime;

        float smoothing =
            1f - Mathf.Exp(
                -speedSmoothing * deltaTime
            );

        smoothedSpeed = Mathf.Lerp(
            smoothedSpeed,
            instantSpeed,
            smoothing
        );

        accumulatedTravel += movement;

        float intensity =
            Mathf.InverseLerp(
                slowSpeed,
                fastSpeed,
                smoothedSpeed
            );

        float interval =
            Mathf.Lerp(
                slowInterval,
                fastInterval,
                intensity
            );

        bool enoughDistance =
            accumulatedTravel >= minimumTravel;

        bool enoughTime =
            currentTime - lastPlayTime >= interval;

        if (!enoughDistance || !enoughTime)
            return;

        PlaySliderSound(intensity);

        accumulatedTravel = 0f;
        lastPlayTime = currentTime;
    }

    public void OnPointerDown(
        PointerEventData eventData)
    {
        interacting = true;

        lastNormalizedValue =
            GetNormalizedSliderValue();

        lastSampleTime =
            Time.unscaledTime;

        accumulatedTravel = 0f;
        smoothedSpeed = 0f;
    }

    public void OnPointerUp(
        PointerEventData eventData)
    {
        interacting = false;

        accumulatedTravel = 0f;
        smoothedSpeed = 0f;
    }

    private void PlaySliderSound(float intensity)
    {
        if (sliderMoveSound == null ||
            audioSource == null)
        {
            return;
        }

        // Slider feedback follows the same SFX volume setting as every
        // other UI/gameplay sound. When the AudioMixer is ready,
        // SoundManager.SFXVolume returns unity gain because the mixer bus
        // already applies the user's SFX slider value. Without the mixer,
        // it falls back to the saved PlayerPrefs value.
        float sfxVolume = SoundManager.SFXVolume;

        if (sfxVolume <= 0f)
            return;

        float volume =
            Mathf.Lerp(
                slowVolume,
                fastVolume,
                intensity
            ) * sfxVolume;

        float pitch =
            Mathf.Lerp(
                slowPitch,
                fastPitch,
                intensity
            );

        audioSource.pitch = pitch;
        audioSource.panStereo =
            CalculateStereoPan();

        audioSource.PlayOneShot(
            sliderMoveSound,
            volume
        );
    }

    private float GetNormalizedSliderValue()
    {
        if (slider == null)
            return 0f;

        return Mathf.InverseLerp(
            slider.minValue,
            slider.maxValue,
            slider.value
        );
    }

    private void CreateAudioSource()
    {
        audioSource =
            gameObject.GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource =
                gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;

        // UI feedback.
        audioSource.spatialBlend = 0f;

        audioSource.dopplerLevel = 0f;
        audioSource.volume = 1f;
        audioSource.ignoreListenerPause = true;

        // UISFX lives under the SFX mixer bus, so the slider feedback
        // follows the SFX volume in real time without touching its authored
        // slow/fast loudness curve.
        GameAudioMixerController.Route(
            audioSource,
            GameAudioMixerController.AudioBus.UISFX
        );
    }

    private float CalculateStereoPan()
    {
        if (rectTransform == null ||
            Screen.width <= 0)
        {
            return 0f;
        }

        Canvas canvas =
            rectTransform.GetComponentInParent<Canvas>();

        Camera camera = null;

        if (canvas != null &&
            canvas.renderMode !=
            RenderMode.ScreenSpaceOverlay)
        {
            camera = canvas.worldCamera;
        }

        Vector3 worldCenter =
            rectTransform.TransformPoint(
                rectTransform.rect.center
            );

        Vector2 screenPoint =
            RectTransformUtility.WorldToScreenPoint(
                camera,
                worldCenter
            );

        float normalizedX =
            screenPoint.x / Screen.width;

        float pan =
            Mathf.Lerp(
                -1f,
                1f,
                normalizedX
            );

        return Mathf.Clamp(
            pan * stereoPanAmount,
            -1f,
            1f
        );
    }

    private void OnDisable()
    {
        interacting = false;
        accumulatedTravel = 0f;
        smoothedSpeed = 0f;
    }
}