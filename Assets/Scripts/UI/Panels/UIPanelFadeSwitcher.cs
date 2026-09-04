using System.Collections;
using UnityEngine;

/// <summary>
/// Shared unscaled-time transition controller for menu panels and modal overlays.
/// Full panel switches use a subtle depth cross-fade; Show/Hide use a compact modal pop.
/// </summary>
public class UIPanelFadeSwitcher : MonoBehaviour
{
    [Header("Full Panel Switch")]
    [SerializeField, Min(0.05f)]
    private float switchDuration = 0.28f;

    [SerializeField, Range(0.95f, 1.05f)]
    private float incomingStartScale = 1.018f;

    [SerializeField, Range(0.95f, 1f)]
    private float outgoingEndScale = 0.985f;

    [SerializeField, Range(0f, 0.5f)]
    private float incomingDelayNormalized = 0.14f;

    [Header("Popup / Overlay")]
    [SerializeField, Min(0.05f)]
    private float overlayDuration = 0.22f;

    [SerializeField, Range(0.8f, 1f)]
    private float overlayStartScale = 0.95f;

    [SerializeField, Range(0.85f, 1f)]
    private float overlayEndScale = 0.97f;

    private Coroutine routine;

    private GameObject trackedFirst;
    private GameObject trackedSecond;
    private bool trackedFirstFinalState;
    private bool trackedSecondFinalState;

    public bool IsTransitioning => routine != null;

    public void SwitchPanel(GameObject fromPanel, GameObject toPanel)
    {
        if (fromPanel == null && toPanel == null)
            return;

        if (fromPanel == toPanel)
        {
            SetInstant(toPanel, true);
            return;
        }

        CompleteCurrentTransition();

        TrackFinalStates(
            fromPanel,
            false,
            toPanel,
            true
        );

        routine = StartCoroutine(
            ManagedRoutine(
                SwitchRoutine(fromPanel, toPanel)
            )
        );
    }

    public void ShowPanel(GameObject panel)
    {
        if (panel == null)
            return;

        CompleteCurrentTransition();
        TrackFinalStates(panel, true, null, false);

        routine = StartCoroutine(
            ManagedRoutine(ShowRoutine(panel))
        );
    }

    public void HidePanel(GameObject panel)
    {
        if (panel == null)
            return;

        CompleteCurrentTransition();
        TrackFinalStates(panel, false, null, false);

        routine = StartCoroutine(
            ManagedRoutine(HideRoutine(panel))
        );
    }

    // Kept for existing callers that want to yield directly.
    public IEnumerator HidePanelRoutine(GameObject panel)
    {
        if (panel == null)
            yield break;

        yield return HideRoutine(panel);
    }

    public void SetInstant(GameObject panel, bool state)
    {
        if (panel == null)
            return;

        CompleteCurrentTransition();
        ApplyFinalState(panel, state);
    }

    private IEnumerator ManagedRoutine(IEnumerator animation)
    {
        yield return animation;

        routine = null;
        ClearTracking();
    }

    private IEnumerator SwitchRoutine(
        GameObject fromPanel,
        GameObject toPanel)
    {
        if (fromPanel == null)
        {
            yield return ShowFullPanelRoutine(toPanel);
            yield break;
        }

        if (toPanel == null)
        {
            yield return HideFullPanelRoutine(fromPanel);
            yield break;
        }

        CanvasGroup fromGroup = GetCanvasGroup(fromPanel);
        CanvasGroup toGroup = GetCanvasGroup(toPanel);

        bool fromWasActive = fromPanel.activeSelf;

        if (!fromWasActive)
        {
            ApplyFinalState(fromPanel, false);
            yield return ShowFullPanelRoutine(toPanel);
            yield break;
        }

        DisableInteraction(fromGroup);
        DisableInteraction(toGroup);

        float fromStartAlpha = Mathf.Clamp01(fromGroup.alpha);
        Vector3 fromStartScale = SafeScale(fromPanel.transform.localScale);

        ActivateWithoutStandaloneIntro(toPanel);
        toPanel.transform.SetAsLastSibling();
        toPanel.transform.localScale = Vector3.one * incomingStartScale;
        toGroup.alpha = 0f;

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, switchDuration);
        float delay = Mathf.Clamp(incomingDelayNormalized, 0f, 0.5f);
        float incomingWindow = Mathf.Max(0.01f, 1f - delay);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float outgoingT = EaseInOutCubic(t);
            float incomingT = Mathf.Clamp01((t - delay) / incomingWindow);
            float incomingEase = EaseOutCubic(incomingT);

            fromGroup.alpha = Mathf.Lerp(
                fromStartAlpha,
                0f,
                outgoingT
            );

            fromPanel.transform.localScale = Vector3.LerpUnclamped(
                fromStartScale,
                Vector3.one * outgoingEndScale,
                outgoingT
            );

            toGroup.alpha = incomingEase;
            toPanel.transform.localScale = Vector3.LerpUnclamped(
                Vector3.one * incomingStartScale,
                Vector3.one,
                incomingEase
            );

            yield return null;
        }

        ApplyFinalState(fromPanel, false);
        ApplyFinalState(toPanel, true);
    }

    private IEnumerator ShowFullPanelRoutine(GameObject panel)
    {
        if (panel == null)
            yield break;

        CanvasGroup group = GetCanvasGroup(panel);
        DisableInteraction(group);

        ActivateWithoutStandaloneIntro(panel);
        panel.transform.SetAsLastSibling();
        panel.transform.localScale = Vector3.one * incomingStartScale;
        group.alpha = 0f;

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, switchDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(t);

            group.alpha = eased;
            panel.transform.localScale = Vector3.LerpUnclamped(
                Vector3.one * incomingStartScale,
                Vector3.one,
                eased
            );

            yield return null;
        }

        ApplyFinalState(panel, true);
    }

    private IEnumerator HideFullPanelRoutine(GameObject panel)
    {
        if (panel == null || !panel.activeSelf)
        {
            ApplyFinalState(panel, false);
            yield break;
        }

        CanvasGroup group = GetCanvasGroup(panel);
        DisableInteraction(group);

        float startAlpha = Mathf.Clamp01(group.alpha);
        Vector3 startScale = SafeScale(panel.transform.localScale);
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, switchDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseInOutCubic(t);

            group.alpha = Mathf.Lerp(startAlpha, 0f, eased);
            panel.transform.localScale = Vector3.LerpUnclamped(
                startScale,
                Vector3.one * outgoingEndScale,
                eased
            );

            yield return null;
        }

        ApplyFinalState(panel, false);
    }

    private IEnumerator ShowRoutine(GameObject panel)
    {
        if (panel == null)
            yield break;

        CanvasGroup group = GetCanvasGroup(panel);
        DisableInteraction(group);

        ActivateWithoutStandaloneIntro(panel);
        panel.transform.SetAsLastSibling();
        panel.transform.localScale = Vector3.one * overlayStartScale;
        group.alpha = 0f;

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, overlayDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float alphaEase = EaseOutCubic(t);
            float scaleEase = EaseOutBackSubtle(t);

            group.alpha = alphaEase;
            panel.transform.localScale = Vector3.LerpUnclamped(
                Vector3.one * overlayStartScale,
                Vector3.one,
                scaleEase
            );

            yield return null;
        }

        ApplyFinalState(panel, true);
    }

    private IEnumerator HideRoutine(GameObject panel)
    {
        if (panel == null || !panel.activeSelf)
        {
            ApplyFinalState(panel, false);
            yield break;
        }

        CanvasGroup group = GetCanvasGroup(panel);
        DisableInteraction(group);

        float startAlpha = Mathf.Clamp01(group.alpha);
        Vector3 startScale = SafeScale(panel.transform.localScale);
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, overlayDuration * 0.9f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseInCubic(t);

            group.alpha = Mathf.Lerp(startAlpha, 0f, eased);
            panel.transform.localScale = Vector3.LerpUnclamped(
                startScale,
                Vector3.one * overlayEndScale,
                eased
            );

            yield return null;
        }

        ApplyFinalState(panel, false);
    }

    private void CompleteCurrentTransition()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (trackedFirst != null)
            ApplyFinalState(trackedFirst, trackedFirstFinalState);

        if (trackedSecond != null)
            ApplyFinalState(trackedSecond, trackedSecondFinalState);

        ClearTracking();
    }

    private void TrackFinalStates(
        GameObject first,
        bool firstState,
        GameObject second,
        bool secondState)
    {
        trackedFirst = first;
        trackedFirstFinalState = firstState;
        trackedSecond = second;
        trackedSecondFinalState = secondState;
    }

    private void ClearTracking()
    {
        trackedFirst = null;
        trackedSecond = null;
        trackedFirstFinalState = false;
        trackedSecondFinalState = false;
    }

    private static void ApplyFinalState(GameObject panel, bool state)
    {
        if (panel == null)
            return;

        CanvasGroup group = GetCanvasGroup(panel);

        panel.transform.localScale = Vector3.one;
        group.alpha = state ? 1f : 0f;
        group.interactable = state;
        group.blocksRaycasts = state;

        if (state)
        {
            if (!panel.activeSelf)
                ActivateWithoutStandaloneIntro(panel);
        }
        else
        {
            if (panel.activeSelf)
                panel.SetActive(false);
        }
    }


    private static void ActivateWithoutStandaloneIntro(GameObject panel)
    {
        if (panel == null || panel.activeSelf)
            return;

        UIPanelAnimation standaloneAnimation =
            panel.GetComponent<UIPanelAnimation>();

        if (standaloneAnimation != null)
            standaloneAnimation.SuppressNextEnableAnimation();

        panel.SetActive(true);
    }

    private static void DisableInteraction(CanvasGroup group)
    {
        if (group == null)
            return;

        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private static CanvasGroup GetCanvasGroup(GameObject panel)
    {
        if (panel == null)
            return null;

        CanvasGroup group = panel.GetComponent<CanvasGroup>();

        if (group == null)
            group = panel.AddComponent<CanvasGroup>();

        return group;
    }

    private static Vector3 SafeScale(Vector3 scale)
    {
        if (scale.sqrMagnitude < 0.0001f)
            return Vector3.one;

        return scale;
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);

        if (t < 0.5f)
            return 4f * t * t * t;

        float value = -2f * t + 2f;
        return 1f - (value * value * value) * 0.5f;
    }

    private static float EaseOutBackSubtle(float t)
    {
        t = Mathf.Clamp01(t);

        // Smaller overshoot than the common 1.70158 constant.
        const float c1 = 0.8f;
        const float c3 = c1 + 1f;
        float x = t - 1f;

        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private void OnDisable()
    {
        CompleteCurrentTransition();
    }

    private void OnValidate()
    {
        switchDuration = Mathf.Max(0.05f, switchDuration);
        incomingStartScale = Mathf.Clamp(incomingStartScale, 0.95f, 1.05f);
        outgoingEndScale = Mathf.Clamp(outgoingEndScale, 0.95f, 1f);
        incomingDelayNormalized = Mathf.Clamp(incomingDelayNormalized, 0f, 0.5f);

        overlayDuration = Mathf.Max(0.05f, overlayDuration);
        overlayStartScale = Mathf.Clamp(overlayStartScale, 0.8f, 1f);
        overlayEndScale = Mathf.Clamp(overlayEndScale, 0.85f, 1f);
    }
}
