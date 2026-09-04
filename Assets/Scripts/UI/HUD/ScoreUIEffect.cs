using System.Collections;
using TMPro;
using UnityEngine;

public class ScoreUIEffect : MonoBehaviour
{
    [Header("Reference")]
    public TextMeshProUGUI scoreText;

    [Header("Pop Settings")]
    [Min(0f)]
    public float popScale = 1.25f;

    [Min(0f)]
    public float duration = 0.08f;

    private Coroutine activeRoutine;

    private Transform scoreTransform;
    private Vector3 originalScale;

    private void Awake()
    {
        RefreshReferences();

        if (scoreText == null)
        {
            Debug.LogWarning(
                "ScoreUIEffect could not find a TextMeshProUGUI reference.",
                this
            );

            enabled = false;
            return;
        }

        scoreTransform = scoreText.transform;
        originalScale = scoreTransform.localScale;
    }

    private void OnDisable()
    {
        StopActiveRoutine();
        ResetScale();
    }

    public void PlayPop()
    {
        if (!isActiveAndEnabled || scoreTransform == null)
            return;

        StopActiveRoutine();

        activeRoutine = StartCoroutine(PopEffect());
    }

    private IEnumerator PopEffect()
    {
        Vector3 enlargedScale =
            originalScale * popScale;

        if (duration <= 0f)
        {
            ResetScale();
            activeRoutine = null;
            yield break;
        }

        // Efekt yeniden tetiklendiyse mevcut scale'den devam eder.
        yield return ScaleRoutine(
            scoreTransform.localScale,
            enlargedScale,
            duration,
            EasingType.SmoothStep
        );

        yield return ScaleRoutine(
            enlargedScale,
            originalScale,
            duration,
            EasingType.EaseIn
        );

        ResetScale();
        activeRoutine = null;
    }

    private IEnumerator ScaleRoutine(
        Vector3 startScale,
        Vector3 targetScale,
        float animationDuration,
        EasingType easingType)
    {
        float elapsedTime = 0f;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float normalizedTime = Mathf.Clamp01(
                elapsedTime / animationDuration
            );

            float easedTime = ApplyEasing(
                normalizedTime,
                easingType
            );

            scoreTransform.localScale = Vector3.LerpUnclamped(
                startScale,
                targetScale,
                easedTime
            );

            yield return null;
        }

        scoreTransform.localScale = targetScale;
    }

    private static float ApplyEasing(
        float value,
        EasingType easingType)
    {
        switch (easingType)
        {
            case EasingType.EaseIn:
                return value * value;

            case EasingType.SmoothStep:
            default:
                return value * value * (3f - 2f * value);
        }
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine == null)
            return;

        StopCoroutine(activeRoutine);
        activeRoutine = null;
    }

    private void ResetScale()
    {
        if (scoreTransform != null)
            scoreTransform.localScale = originalScale;
    }

    private void RefreshReferences()
    {
        if (scoreText == null)
            scoreText = GetComponent<TextMeshProUGUI>();

        if (scoreText == null)
            scoreText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void OnValidate()
    {
        popScale = Mathf.Max(0f, popScale);
        duration = Mathf.Max(0f, duration);
    }

    private enum EasingType
    {
        SmoothStep,
        EaseIn
    }
}