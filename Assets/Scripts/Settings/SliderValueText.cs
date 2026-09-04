using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SliderValueText : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("References")]
    [SerializeField]
    private Slider slider;

    [SerializeField]
    private TMP_Text valueText;

    [Header("Fade")]
    [Min(0f)]
    [SerializeField]
    private float fadeInDuration = 0.12f;

    [Min(0f)]
    [SerializeField]
    private float fadeOutDuration = 0.25f;

    [Min(0f)]
    [SerializeField]
    private float hideDelay = 0.45f;

    private CanvasGroup canvasGroup;
    private Coroutine fadeRoutine;

    private bool isPointerDown;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponent<Slider>();

        SetupValueText();

        if (slider != null)
            slider.onValueChanged.AddListener(UpdateText);

        HideInstant();
    }

    private void OnEnable()
    {
        isPointerDown = false;

        if (slider != null)
            UpdateTextValue();

        HideInstant();
    }

    private void OnDisable()
    {
        isPointerDown = false;
        StopFadeRoutine();
        HideInstant();
    }

    private void OnDestroy()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(UpdateText);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        Show();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        HideAfterDelay();
    }

    private void SetupValueText()
    {
        if (valueText == null)
            return;

        canvasGroup =
            valueText.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup =
                valueText.gameObject
                    .AddComponent<CanvasGroup>();
        }
    }

    private void UpdateText(float value)
    {
        UpdateTextValue();

        if (!gameObject.activeInHierarchy)
            return;

        Show();

        /*
         * Mouse veya parmakla sürüklenmiyorsa
         * yazı kısa süre sonra otomatik kapanır.
         *
         * Bu sayede klavye, controller veya kod
         * üzerinden yapılan değişikliklerde yazı
         * ekranda sürekli açık kalmaz.
         */
        if (!isPointerDown)
            HideAfterDelay();
    }

    private void UpdateTextValue()
    {
        if (valueText == null || slider == null)
            return;

        int percentage = Mathf.RoundToInt(
           slider.value * 100f
                  );

        valueText.SetText("{0}%", percentage);
    }

    private void Show()
    {
        if (valueText == null || canvasGroup == null)
            return;

        StopFadeRoutine();

        valueText.gameObject.SetActive(true);

        fadeRoutine = StartCoroutine(
            FadeRoutine(
                1f,
                fadeInDuration
            )
        );
    }

    private void HideAfterDelay()
    {
        if (valueText == null || canvasGroup == null)
            return;

        StopFadeRoutine();

        fadeRoutine = StartCoroutine(
            HideDelayRoutine()
        );
    }

    private IEnumerator HideDelayRoutine()
    {
        if (hideDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                hideDelay
            );
        }

        yield return FadeRoutine(
            0f,
            fadeOutDuration
        );

        if (valueText != null)
            valueText.gameObject.SetActive(false);

        fadeRoutine = null;
    }

    private IEnumerator FadeRoutine(
        float targetAlpha,
        float duration
    )
    {
        if (canvasGroup == null)
            yield break;

        float startAlpha = canvasGroup.alpha;

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                timer / duration
            );

            /*
             * Smoothstep:
             * Fade başlangıcını ve bitişini yumuşatır.
             */
            t = t * t * (3f - 2f * t);

            canvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                t
            );

            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private void HideInstant()
    {
        if (valueText == null)
            return;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        valueText.gameObject.SetActive(false);
    }

    private void StopFadeRoutine()
    {
        if (fadeRoutine == null)
            return;

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }
}