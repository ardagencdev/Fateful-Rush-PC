using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonEffect : MonoBehaviour,
    IUIScheduledVisual,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    public enum AbilityFeedbackStyle
    {
        None,
        Dash,
        Clone
    }

    [Header("Sprites")]
    public Sprite normalSprite;
    public Sprite highlightedSprite;

    [Header("Scale")]
    [Tooltip("Optional. If left empty, this button's own RectTransform is animated.")]
    [SerializeField] private RectTransform scaleTarget;

    [Min(0f)]
    public float hoverScale = 1.08f;

    [Min(0f)]
    public float clickScale = 0.95f;

    [Header("Persistent Selected State")]
    [SerializeField]
    private bool usePersistentSelectedState;

    [SerializeField, Min(0f)]
    private float selectedScale = 1.05f;

    [Header("Smooth")]
    [Min(0f)]
    public float transitionSpeed = 10f;

    [SerializeField, Min(0.000001f)]
    private float settleThreshold = 0.00005f;

    [SerializeField] private Image spriteTarget;

    private Button cachedButton;
    private Vector3 originalScale;
    private bool originalScaleCaptured;

    private AbilityFeedbackStyle abilityFeedbackStyle =
        AbilityFeedbackStyle.None;

    private Coroutine abilityFeedbackRoutine;

    private bool isHovering;
    private bool isPressed;
    private bool isSelected;

    private void Awake()
    {
        ResolveReferences();

        CaptureOriginalScale();

        DisableNonInteractiveTextRaycasts();
        ApplyCurrentSprite();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isHovering)
            return;

        isHovering = true;
        UIScaleTweenRunner.ScheduleVisual(this);

        if (abilityFeedbackRoutine == null)
            AnimateToCurrentState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHovering && !isPressed)
            return;

        isHovering = false;
        isPressed = false;

        UIScaleTweenRunner.ScheduleVisual(this);

        if (abilityFeedbackRoutine == null)
            AnimateToCurrentState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (cachedButton != null && !cachedButton.interactable)
            return;

        if (isPressed)
            return;

        isPressed = true;
        UIScaleTweenRunner.ScheduleVisual(this);

        // Ability buttons play their own success feedback from the ability
        // script. This prevents a fake punch while the ability is on cooldown.
        if (abilityFeedbackStyle == AbilityFeedbackStyle.None)
            AnimateToCurrentState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!isPressed)
            return;

        isPressed = false;
        UIScaleTweenRunner.ScheduleVisual(this);

        if (abilityFeedbackRoutine == null)
            AnimateToCurrentState();
    }

    public void ConfigureAbilityFeedback(AbilityFeedbackStyle style)
    {
        ResolveReferences();
        CaptureOriginalScale();
        abilityFeedbackStyle = style;
    }

    public void PlayAbilityActivation()
    {
        if (abilityFeedbackStyle == AbilityFeedbackStyle.None ||
            scaleTarget == null ||
            !isActiveAndEnabled)
        {
            return;
        }

        StartAbilityFeedback(AbilityFeedbackKind.Activation);
    }

    public void PlayReadyPulse()
    {
        if (abilityFeedbackStyle == AbilityFeedbackStyle.None ||
            scaleTarget == null ||
            !isActiveAndEnabled)
        {
            return;
        }

        StartAbilityFeedback(AbilityFeedbackKind.Ready);
    }

    private enum AbilityFeedbackKind
    {
        Activation,
        Ready
    }

    private void StartAbilityFeedback(AbilityFeedbackKind kind)
    {
        StopAbilityFeedback();
        UIScaleTweenRunner.Cancel(scaleTarget);

        abilityFeedbackRoutine = StartCoroutine(
            AbilityFeedbackRoutine(kind)
        );
    }

    private System.Collections.IEnumerator AbilityFeedbackRoutine(
        AbilityFeedbackKind kind)
    {
        CaptureOriginalScale();

        if (kind == AbilityFeedbackKind.Ready)
        {
            if (abilityFeedbackStyle == AbilityFeedbackStyle.Dash)
            {
                yield return AnimateScaleMultiplier(1.045f, 0.055f);
                yield return AnimateScaleMultiplier(1f, 0.10f);
            }
            else
            {
                yield return AnimateScaleMultiplier(1.035f, 0.09f);
                yield return AnimateScaleMultiplier(1f, 0.13f);
            }
        }
        else if (abilityFeedbackStyle == AbilityFeedbackStyle.Dash)
        {
            // Dash: short, hard compression followed by a sharp rebound.
            yield return AnimateScaleMultiplier(0.84f, 0.035f);
            yield return AnimateScaleMultiplier(1.07f, 0.055f);
            yield return AnimateScaleMultiplier(1f, 0.085f);
        }
        else
        {
            // Clone: softer and slightly wider pulse than Dash.
            yield return AnimateScaleMultiplier(0.93f, 0.055f);
            yield return AnimateScaleMultiplier(1.06f, 0.10f);
            yield return AnimateScaleMultiplier(0.99f, 0.075f);
            yield return AnimateScaleMultiplier(1f, 0.09f);
        }

        abilityFeedbackRoutine = null;
    }

    private System.Collections.IEnumerator AnimateScaleMultiplier(
        float multiplier,
        float duration)
    {
        if (scaleTarget == null)
            yield break;

        Vector3 startScale = scaleTarget.localScale;
        Vector3 targetScale = originalScale * multiplier;

        duration = Mathf.Max(0.001f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (scaleTarget == null)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            scaleTarget.localScale = Vector3.LerpUnclamped(
                startScale,
                targetScale,
                t
            );

            yield return null;
        }

        if (scaleTarget != null)
            scaleTarget.localScale = targetScale;
    }

    private void StopAbilityFeedback()
    {
        if (abilityFeedbackRoutine == null)
            return;

        StopCoroutine(abilityFeedbackRoutine);
        abilityFeedbackRoutine = null;
    }

    private void CaptureOriginalScale()
    {
        if (originalScaleCaptured)
            return;

        originalScale = scaleTarget != null
            ? scaleTarget.localScale
            : Vector3.one;

        originalScaleCaptured = true;
    }

    public void SetSelected(bool selected)
    {
        if (!usePersistentSelectedState || isSelected == selected)
            return;

        isSelected = selected;

        UIScaleTweenRunner.ScheduleVisual(this);
        AnimateToCurrentState();
    }

    public void ResetButtonVisual()
    {
        StopAbilityFeedback();
        isHovering = false;
        isPressed = false;

        ApplyCurrentSprite();

        UIScaleTweenRunner.CancelAndSnap(
            scaleTarget,
            GetRestingScale()
        );
    }

    private void AnimateToCurrentState()
    {
        if (scaleTarget == null)
            return;

        Vector3 desiredScale;

        if (isPressed)
        {
            desiredScale = originalScale * clickScale;
        }
        else if (isHovering)
        {
            desiredScale = originalScale * hoverScale;
        }
        else
        {
            desiredScale = GetRestingScale();
        }

        UIScaleTweenRunner.TweenTo(
            scaleTarget,
            desiredScale,
            transitionSpeed,
            settleThreshold
        );
    }

    private Vector3 GetRestingScale()
    {
        if (usePersistentSelectedState && isSelected)
            return originalScale * selectedScale;

        return originalScale;
    }

    public void ApplyScheduledVisualState()
    {
        ApplyCurrentSprite();
    }

    private void ApplyCurrentSprite()
    {
        bool shouldHighlight = isHovering || isPressed;

        Sprite desiredSprite = shouldHighlight && highlightedSprite != null
            ? highlightedSprite
            : normalSprite;

        if (spriteTarget == null || desiredSprite == null)
            return;

        // Avoid even a redundant property assignment on a Graphic.
        if (spriteTarget.sprite != desiredSprite)
            spriteTarget.sprite = desiredSprite;
    }

    private void ResolveReferences()
    {
        if (cachedButton == null)
            cachedButton = GetComponent<Button>();

        if (scaleTarget == null)
            scaleTarget = transform as RectTransform;

        if (spriteTarget != null)
            return;

        if (cachedButton != null && cachedButton.targetGraphic is Image targetImage)
        {
            spriteTarget = targetImage;
            return;
        }

        spriteTarget = GetComponent<Image>();
    }

    private void DisableNonInteractiveTextRaycasts()
    {
        // Unity recommends disabling Raycast Target on non-interactive text
        // inside buttons. The Button's target Graphic already handles input.
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
            texts[i].raycastTarget = false;
    }

    private void OnDisable()
    {
        UIScaleTweenRunner.CancelScheduledVisual(this);
        ResetButtonVisual();
    }

    private void OnDestroy()
    {
        StopAbilityFeedback();
        UIScaleTweenRunner.CancelScheduledVisual(this);
        UIScaleTweenRunner.Cancel(scaleTarget);
    }

    private void OnValidate()
    {
        hoverScale = Mathf.Max(0f, hoverScale);
        clickScale = Mathf.Max(0f, clickScale);
        selectedScale = Mathf.Max(0f, selectedScale);
        transitionSpeed = Mathf.Max(0f, transitionSpeed);
        settleThreshold = Mathf.Max(0.000001f, settleThreshold);
    }
}
