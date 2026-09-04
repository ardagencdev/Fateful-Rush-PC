using System.Collections;
using UnityEngine;

public class MenuFadeIn : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private CanvasGroup canvasGroup;

    [SerializeField, Min(0f)]
    private float fadeDuration = 1.2f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        RefreshReferences();

        if (canvasGroup == null)
        {
            Debug.LogWarning(
                "MenuFadeIn could not find a CanvasGroup.",
                this
            );

            enabled = false;
            return;
        }

        SetCanvasState(0f, false);
    }

    private void Start()
    {
        fadeRoutine = StartCoroutine(FadeInRoutine());
    }

    private void OnDisable()
    {
        StopFadeRoutine();
    }

    private IEnumerator FadeInRoutine()
    {
        if (fadeDuration <= 0f)
        {
            SetCanvasState(1f, true);
            fadeRoutine = null;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsedTime / fadeDuration
            );

            canvasGroup.alpha = progress;

            yield return null;
        }

        SetCanvasState(1f, true);

        fadeRoutine = null;
    }

    private void SetCanvasState(
        float alpha,
        bool interactive)
    {
        canvasGroup.alpha = Mathf.Clamp01(alpha);
        canvasGroup.interactable = interactive;
        canvasGroup.blocksRaycasts = interactive;
    }

    private void RefreshReferences()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void StopFadeRoutine()
    {
        if (fadeRoutine == null)
            return;

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);
    }
}