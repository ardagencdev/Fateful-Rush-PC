using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// One-time ending message shown on Main Menu after the player's first
/// completion of Level 40.
///
/// SETUP:
/// 1) Create a TMP text in the MainMenu scene.
/// 2) Put this component on that TMP object.
/// 3) Assign the TMP text field below.
/// 4) Keep the object ACTIVE in the hierarchy. This script controls visibility
///    using CanvasGroup, so it can detect the pending Level 40 completion.
/// </summary>
[DisallowMultipleComponent]
public sealed class CompletionThankYouUI : MonoBehaviour
{
    public const string PendingKey =
        "FatefulRush_Level40_ThankYou_Pending";

    [Header("Reference")]
    [SerializeField]
    private TMP_Text thankYouText;

    [Header("Message")]
    [SerializeField, TextArea(2, 5)]
    private string message =
        "THANK YOU FOR PLAYING\n" +
        "YOU HAVE COMPLETED FATEFUL RUSH";

    [Header("Timing")]
    [SerializeField, Min(0f)]
    private float startDelay = 0.7f;

    [SerializeField, Min(0.05f)]
    private float fadeInDuration = 0.45f;

    [SerializeField, Min(0f)]
    private float visibleDuration = 4.0f;

    [SerializeField, Min(0.05f)]
    private float fadeOutDuration = 0.6f;

    [Header("Scale")]
    [SerializeField, Range(0.5f, 1f)]
    private float startScale = 0.92f;

    [SerializeField, Range(1f, 1.2f)]
    private float overshootScale = 1.03f;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 baseScale = Vector3.one;
    private Coroutine routine;

    private void Awake()
    {
        if (thankYouText == null)
            thankYouText = GetComponent<TMP_Text>();

        if (thankYouText == null)
        {
            Debug.LogError(
                "[CompletionThankYouUI] TMP_Text reference is missing.",
                this
            );

            enabled = false;
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        rectTransform = thankYouText.rectTransform;
        baseScale = rectTransform.localScale;

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        HideImmediate();
    }

    private void Start()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) != 1)
            return;

        // Consume the pending message now. Level 40 completion itself is
        // already persistent, so this prevents the ending message from
        // appearing on every future Main Menu visit.
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();

        routine = StartCoroutine(
            ShowRoutine()
        );
    }

    private IEnumerator ShowRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                startDelay
            );
        }

        thankYouText.text = message;

        // Make sure this ending message is above the normal Main Menu UI.
        transform.SetAsLastSibling();

        canvasGroup.alpha = 0f;
        rectTransform.localScale =
            baseScale * startScale;

        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / fadeInDuration
                );

            float eased =
                1f - Mathf.Pow(1f - t, 3f);

            canvasGroup.alpha = eased;

            float scale =
                Mathf.Lerp(
                    startScale,
                    overshootScale,
                    eased
                );

            rectTransform.localScale =
                baseScale * scale;

            yield return null;
        }

        canvasGroup.alpha = 1f;
        rectTransform.localScale =
            baseScale * overshootScale;

        // Small settle so it feels deliberate rather than popping.
        elapsed = 0f;
        const float settleDuration = 0.18f;

        while (elapsed < settleDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / settleDuration
                );

            rectTransform.localScale =
                baseScale *
                Mathf.Lerp(
                    overshootScale,
                    1f,
                    t
                );

            yield return null;
        }

        rectTransform.localScale = baseScale;

        if (visibleDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(
                visibleDuration
            );
        }

        elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / fadeOutDuration
                );

            canvasGroup.alpha =
                1f - t;

            yield return null;
        }

        HideImmediate();
        routine = null;
    }

    private void HideImmediate()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (rectTransform != null)
            rectTransform.localScale = baseScale;
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private void OnValidate()
    {
        startDelay =
            Mathf.Max(0f, startDelay);

        fadeInDuration =
            Mathf.Max(0.05f, fadeInDuration);

        visibleDuration =
            Mathf.Max(0f, visibleDuration);

        fadeOutDuration =
            Mathf.Max(0.05f, fadeOutDuration);

        startScale =
            Mathf.Clamp(startScale, 0.5f, 1f);

        overshootScale =
            Mathf.Clamp(
                overshootScale,
                1f,
                1.2f
            );
    }
}
