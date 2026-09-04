using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UIPanelAnimation : MonoBehaviour
{
    [Header("Polished Intro")]
    [SerializeField, Min(0.05f)]
    private float introDuration = 0.24f;

    [SerializeField, Range(0.85f, 1f)]
    private float introStartScale = 0.95f;

    private CanvasGroup canvasGroup;
    private Coroutine animationRoutine;
    private bool suppressNextEnableAnimation;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        StopAnimation();

        if (suppressNextEnableAnimation)
        {
            suppressNextEnableAnimation = false;
            return;
        }

        animationRoutine = StartCoroutine(AnimatePanel());
    }

    public void SuppressNextEnableAnimation()
    {
        suppressNextEnableAnimation = true;
    }

    private void OnDisable()
    {
        StopAnimation();
    }

    private IEnumerator AnimatePanel()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, introDuration);

        Vector3 fromScale = Vector3.one * introStartScale;
        Vector3 targetScale = Vector3.one;

        transform.localScale = fromScale;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float alphaEase = EaseOutCubic(t);
            float scaleEase = EaseOutBackSubtle(t);

            canvasGroup.alpha = alphaEase;
            transform.localScale = Vector3.LerpUnclamped(
                fromScale,
                targetScale,
                scaleEase
            );

            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        transform.localScale = targetScale;

        animationRoutine = null;
    }

    private void StopAnimation()
    {
        if (animationRoutine == null)
            return;

        StopCoroutine(animationRoutine);
        animationRoutine = null;
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseOutBackSubtle(float t)
    {
        t = Mathf.Clamp01(t);

        const float c1 = 0.8f;
        const float c3 = c1 + 1f;
        float x = t - 1f;

        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private void OnValidate()
    {
        introDuration = Mathf.Max(0.05f, introDuration);
        introStartScale = Mathf.Clamp(introStartScale, 0.85f, 1f);
    }
}
