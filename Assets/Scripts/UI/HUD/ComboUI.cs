using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ComboUI : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI comboText;

    [Header("Combo Colors")]
    public Color combo1Color = new Color(0.68f, 0.68f, 0.72f);
    public Color combo2Color = new Color(1f, 0.82f, 0.18f);
    public Color combo3Color = new Color(1f, 0.56f, 0.12f);
    public Color combo4Color = new Color(1f, 0.28f, 0.10f);
    public Color combo5Color = new Color(0.95f, 0.18f, 0.52f);
    public Color combo6Color = new Color(0.72f, 0.42f, 1f);

    [Header("Regular Coin Pulse")]
    [Tooltip("Base scale punch used when collecting another coin without changing combo stage.")]
    [Min(1f)]
    public float pulseScale = 1.10f;

    [Tooltip("Duration of one half of the regular pulse.")]
    [Min(0.01f)]
    public float pulseDuration = 0.07f;

    [Header("Stage Up")]
    [Tooltip("Minimum scale punch when entering a new combo stage.")]
    [Min(1f)]
    public float stageUpMinScale = 1.20f;

    [Tooltip("Scale punch used near the highest combo stages.")]
    [Min(1f)]
    public float stageUpMaxScale = 1.36f;

    [Tooltip("Total duration of a normal stage-up animation.")]
    [Min(0.01f)]
    public float stageUpDuration = 0.20f;

    [Tooltip("Maximum upward movement in UI pixels during a stage-up.")]
    [Min(0f)]
    public float stageUpLift = 8f;

    [Tooltip("Maximum temporary rotation used on higher combo stages.")]
    [Min(0f)]
    public float stageUpTilt = 3.5f;

    [Tooltip("Small impact shake used only on the stronger combo stages.")]
    [Min(0f)]
    public float stageUpShakeAmount = 2.5f;

    [Header("Max Combo")]
    [Tooltip("Duration of the x6 MAX COMBO impact.")]
    [Min(0.01f)]
    public float maxComboShakeDuration = 0.24f;

    [Tooltip("Shake strength of the first x6 impact.")]
    [Min(0f)]
    public float maxComboShakeAmount = 7f;

    [Tooltip("Peak scale of the first x6 impact.")]
    [Min(1f)]
    public float maxComboScale = 1.48f;

    [Tooltip("Second, smaller hit after the first x6 impact.")]
    [Min(1f)]
    public float maxComboSecondPulseScale = 1.20f;

    [Tooltip("How strongly x6 flashes toward white on activation.")]
    [Range(0f, 1f)]
    public float maxComboFlashStrength = 0.80f;

    [Header("Timer Bar")]
    public Image comboTimerBar;
    public Color timerFullColor = Color.green;
    public Color timerLowColor = Color.red;

    [Header("Visibility")]
    [Tooltip("How long the combo counter takes to fade in when x2 is reached.")]
    [Min(0.01f)]
    public float showFadeDuration = 0.16f;

    [Tooltip("Starting scale used while the combo counter fades in at x2.")]
    [Range(0.75f, 1f)]
    public float showStartScale = 0.88f;

    [Header("Reset")]
    [Tooltip("How long the combo counter takes to fade out when the combo is lost.")]
    [Min(0.01f)]
    public float resetFadeDuration = 0.20f;

    [Tooltip("How small the combo counter becomes while fading out.")]
    [Range(0.75f, 1f)]
    public float resetScale = 0.90f;

    private Coroutine activeRoutine;

    private Vector3 originalScale;
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    private bool timerBarVisible;
    private int displayedCombo = 1;

    private void Awake()
    {
        RefreshReferences();

        if (comboText == null)
        {
            Debug.LogError(
                "ComboUI could not find a TextMeshProUGUI component.",
                this
            );

            enabled = false;
            return;
        }

        originalScale = comboText.transform.localScale;
        originalPosition = comboText.transform.localPosition;
        originalRotation = comboText.transform.localRotation;

        UpdateCombo(1);

        if (comboTimerBar != null)
        {
            comboTimerBar.fillAmount = 0f;
            timerBarVisible = comboTimerBar.gameObject.activeSelf;
            SetTimerBarVisible(false);
        }
    }

    private void OnDisable()
    {
        StopActiveRoutine();
        ResetTextTransform();

        if (comboText != null)
        {
            comboText.color = GetComboColor(displayedCombo);
        }

        if (comboTimerBar != null)
        {
            comboTimerBar.fillAmount = 0f;
            SetTimerBarVisible(false);
        }
    }

    public void ShowCombo(int gainedScore, int combo)
    {
        if (!isActiveAndEnabled || comboText == null)
            return;

        int safeCombo = Mathf.Max(1, combo);
        int previousCombo = displayedCombo;
        Color previousColor = comboText.color;

        bool stageIncreased =
            safeCombo > previousCombo;

        bool becameVisible =
            safeCombo > 1 &&
            previousCombo <= 1;

        bool reachedMaxCombo =
            safeCombo >= 6 &&
            previousCombo < 6;

        StopActiveRoutine();
        ResetTextTransform();

        // x1 is the normal state, so the combo counter stays hidden.
        if (safeCombo <= 1)
        {
            UpdateCombo(1);
            return;
        }

        UpdateCombo(safeCombo);

        if (stageIncreased)
            SoundManager.Instance?.PlayComboStageSound(comboText.rectTransform);

        Color targetColor = GetComboColor(safeCombo);

        if (reachedMaxCombo)
        {
            comboText.color = previousColor;

            activeRoutine =
                StartCoroutine(
                    MaxComboRoutine(targetColor)
                );

            return;
        }

        if (stageIncreased)
        {
            if (becameVisible)
            {
                comboText.color =
                    SetAlpha(targetColor, 0f);

                comboText.transform.localScale =
                    originalScale * showStartScale;
            }
            else
            {
                comboText.color = previousColor;
            }

            activeRoutine =
                StartCoroutine(
                    StageUpRoutine(
                        safeCombo,
                        targetColor,
                        becameVisible
                    )
                );

            return;
        }

        activeRoutine =
            StartCoroutine(
                CoinPulseRoutine(safeCombo)
            );
    }

    public void UpdateTimerBar(
        float normalizedTime,
        int combo)
    {
        if (!isActiveAndEnabled ||
            comboTimerBar == null)
        {
            return;
        }

        normalizedTime =
            Mathf.Clamp01(normalizedTime);

        bool shouldShow =
            combo > 1 &&
            normalizedTime > 0f;

        SetTimerBarVisible(shouldShow);

        if (!shouldShow)
        {
            comboTimerBar.fillAmount = 0f;
            return;
        }

        comboTimerBar.fillAmount =
            normalizedTime;

        comboTimerBar.color =
            Color.Lerp(
                timerLowColor,
                timerFullColor,
                normalizedTime
            );
    }

    public void UpdateCombo(int combo)
    {
        if (comboText == null)
            return;

        displayedCombo =
            Mathf.Max(1, combo);

        if (!comboText.gameObject.activeSelf)
        {
            comboText.gameObject.SetActive(true);
        }

        comboText.SetText(
            "x{0}",
            displayedCombo
        );

        Color targetColor =
            GetComboColor(displayedCombo);

        // Keep the object active so ComboUI can still receive calls even if
        // this component lives on the same GameObject as the TMP text.
        comboText.color =
            displayedCombo <= 1
                ? SetAlpha(targetColor, 0f)
                : targetColor;
    }

    public void ResetCombo()
    {
        if (!isActiveAndEnabled ||
            comboText == null)
        {
            return;
        }

        StopActiveRoutine();
        ResetTextTransform();

        // Runtime combo is already reset at this point.
        // Setting this immediately keeps the next pickup animation correct
        // even if it interrupts the reset animation.
        displayedCombo = 1;

        activeRoutine =
            StartCoroutine(ResetRoutine());
    }

    private IEnumerator CoinPulseRoutine(
        int combo)
    {
        float strength =
            Mathf.InverseLerp(
                1f,
                6f,
                Mathf.Clamp(combo, 1, 6)
            );

        float peakMultiplier =
            pulseScale +
            Mathf.Lerp(
                0f,
                0.10f,
                strength
            );

        Vector3 peakScale =
            originalScale * peakMultiplier;

        Vector3 settleScale =
            originalScale *
            Mathf.Lerp(
                0.985f,
                0.97f,
                strength
            );

        float firstDuration =
            pulseDuration;

        float secondDuration =
            pulseDuration * 0.75f;

        float thirdDuration =
            pulseDuration * 0.85f;

        yield return AnimateScale(
            originalScale,
            peakScale,
            firstDuration,
            EaseOutCubic
        );

        yield return AnimateScale(
            peakScale,
            settleScale,
            secondDuration,
            EaseInOutCubic
        );

        yield return AnimateScale(
            settleScale,
            originalScale,
            thirdDuration,
            EaseOutCubic
        );

        ResetTextTransform();
        comboText.color =
            GetComboColor(displayedCombo);

        activeRoutine = null;
    }

    private IEnumerator StageUpRoutine(
        int combo,
        Color targetColor,
        bool revealFromHidden = false)
    {
        float strength =
            Mathf.InverseLerp(
                2f,
                5f,
                Mathf.Clamp(combo, 2, 5)
            );

        float peakMultiplier =
            Mathf.Lerp(
                stageUpMinScale,
                stageUpMaxScale,
                strength
            );

        float lift =
            Mathf.Lerp(
                stageUpLift * 0.45f,
                stageUpLift,
                strength
            );

        float tilt =
            Mathf.Lerp(
                stageUpTilt * 0.30f,
                stageUpTilt,
                strength
            );

        if (combo % 2 == 0)
            tilt = -tilt;

        float shake =
            combo >= 4
                ? Mathf.Lerp(
                    stageUpShakeAmount * 0.45f,
                    stageUpShakeAmount,
                    strength
                )
                : 0f;

        Color startColor =
            comboText.color;

        Vector3 impactStartScale =
            revealFromHidden
                ? originalScale * showStartScale
                : originalScale;

        Color flashColor =
            Color.Lerp(
                targetColor,
                Color.white,
                Mathf.Lerp(0.25f, 0.55f, strength)
            );

        float revealElapsed = 0f;

        float impactDuration =
            stageUpDuration * 0.42f;

        float settleDuration =
            stageUpDuration * 0.58f;

        float elapsed = 0f;

        while (elapsed < impactDuration)
        {
            float deltaTime = Time.unscaledDeltaTime;
            elapsed += deltaTime;
            revealElapsed += deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / impactDuration
                );

            float eased =
                EaseOutBack(t);

            comboText.transform.localScale =
                Vector3.LerpUnclamped(
                    impactStartScale,
                    originalScale * peakMultiplier,
                    eased
                );

            comboText.transform.localPosition =
                Vector3.LerpUnclamped(
                    originalPosition,
                    originalPosition +
                    Vector3.up * lift,
                    EaseOutCubic(t)
                );

            comboText.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(
                        0f,
                        tilt,
                        EaseOutCubic(t)
                    )
                ) * originalRotation;

            Color animatedColor =
                Color.Lerp(
                    startColor,
                    flashColor,
                    EaseOutCubic(t)
                );

            if (revealFromHidden)
            {
                float revealT =
                    Mathf.Clamp01(
                        revealElapsed / showFadeDuration
                    );

                animatedColor.a =
                    Mathf.Lerp(
                        0f,
                        targetColor.a,
                        EaseOutCubic(revealT)
                    );
            }

            comboText.color = animatedColor;

            yield return null;
        }

        elapsed = 0f;

        while (elapsed < settleDuration)
        {
            float deltaTime = Time.unscaledDeltaTime;
            elapsed += deltaTime;
            revealElapsed += deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / settleDuration
                );

            float eased =
                EaseOutCubic(t);

            float shakeX = 0f;
            float shakeY = 0f;

            if (shake > 0f)
            {
                float decay =
                    1f - t;

                shakeX =
                    Mathf.Sin(
                        elapsed * 110f
                    ) *
                    shake *
                    decay;

                shakeY =
                    Mathf.Sin(
                        elapsed * 83f + 1.3f
                    ) *
                    shake *
                    0.45f *
                    decay;
            }

            comboText.transform.localScale =
                Vector3.Lerp(
                    originalScale * peakMultiplier,
                    originalScale,
                    eased
                );

            comboText.transform.localPosition =
                Vector3.Lerp(
                    originalPosition +
                    Vector3.up * lift,
                    originalPosition,
                    eased
                ) +
                new Vector3(
                    shakeX,
                    shakeY,
                    0f
                );

            comboText.transform.localRotation =
                Quaternion.Slerp(
                    Quaternion.Euler(
                        0f,
                        0f,
                        tilt
                    ) * originalRotation,
                    originalRotation,
                    eased
                );

            Color animatedColor =
                Color.Lerp(
                    flashColor,
                    targetColor,
                    eased
                );

            if (revealFromHidden)
            {
                float revealT =
                    Mathf.Clamp01(
                        revealElapsed / showFadeDuration
                    );

                animatedColor.a =
                    Mathf.Lerp(
                        0f,
                        targetColor.a,
                        EaseOutCubic(revealT)
                    );
            }

            comboText.color = animatedColor;

            yield return null;
        }

        ResetTextTransform();
        comboText.color = targetColor;

        activeRoutine = null;
    }

    private IEnumerator MaxComboRoutine(
        Color targetColor)
    {
        Color startColor =
            comboText.color;

        Color flashColor =
            Color.Lerp(
                targetColor,
                Color.white,
                maxComboFlashStrength
            );

        float impactDuration =
            maxComboShakeDuration * 0.34f;

        float recoilDuration =
            maxComboShakeDuration * 0.28f;

        float settleDuration =
            maxComboShakeDuration * 0.38f;

        float elapsed = 0f;

        // First hit: fast scale-up + lift + flash.
        while (elapsed < impactDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / impactDuration
                );

            float eased =
                EaseOutBack(t);

            comboText.transform.localScale =
                Vector3.LerpUnclamped(
                    originalScale,
                    originalScale * maxComboScale,
                    eased
                );

            comboText.transform.localPosition =
                Vector3.Lerp(
                    originalPosition,
                    originalPosition +
                    Vector3.up * 11f,
                    EaseOutCubic(t)
                );

            comboText.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(
                        0f,
                        -4.5f,
                        EaseOutCubic(t)
                    )
                ) * originalRotation;

            comboText.color =
                Color.Lerp(
                    startColor,
                    flashColor,
                    EaseOutCubic(t)
                );

            yield return null;
        }

        elapsed = 0f;

        // Recoil: deterministic shake, no Random allocation/noise.
        while (elapsed < recoilDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / recoilDuration
                );

            float decay =
                1f - t;

            float shakeX =
                Mathf.Sin(
                    elapsed * 145f
                ) *
                maxComboShakeAmount *
                decay;

            float shakeY =
                Mathf.Sin(
                    elapsed * 101f + 0.8f
                ) *
                maxComboShakeAmount *
                0.55f *
                decay;

            comboText.transform.localScale =
                Vector3.Lerp(
                    originalScale * maxComboScale,
                    originalScale * maxComboSecondPulseScale,
                    EaseInOutCubic(t)
                );

            comboText.transform.localPosition =
                originalPosition +
                new Vector3(
                    shakeX,
                    shakeY,
                    0f
                );

            comboText.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Sin(elapsed * 90f) *
                    3.2f *
                    decay
                ) * originalRotation;

            comboText.color =
                Color.Lerp(
                    flashColor,
                    targetColor,
                    EaseOutCubic(t)
                );

            yield return null;
        }

        elapsed = 0f;

        // Second hit / final settle.
        while (elapsed < settleDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / settleDuration
                );

            float pulse =
                Mathf.Sin(t * Mathf.PI);

            float scaleMultiplier =
                Mathf.Lerp(
                    maxComboSecondPulseScale,
                    1f,
                    EaseOutCubic(t)
                ) +
                pulse * 0.035f;

            comboText.transform.localScale =
                originalScale *
                scaleMultiplier;

            comboText.transform.localPosition =
                Vector3.Lerp(
                    comboText.transform.localPosition,
                    originalPosition,
                    EaseOutCubic(t)
                );

            comboText.transform.localRotation =
                Quaternion.Slerp(
                    comboText.transform.localRotation,
                    originalRotation,
                    EaseOutCubic(t)
                );

            comboText.color =
                Color.Lerp(
                    comboText.color,
                    targetColor,
                    EaseOutCubic(t)
                );

            yield return null;
        }

        ResetTextTransform();
        comboText.color = targetColor;

        activeRoutine = null;
    }

    private IEnumerator ResetRoutine()
    {
        Color startColor =
            comboText.color;

        Vector3 startScale =
            comboText.transform.localScale;

        Vector3 smallScale =
            originalScale * resetScale;

        float elapsed = 0f;

        while (elapsed < resetFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / resetFadeDuration
                );

            float eased =
                EaseInOutCubic(t);

            comboText.transform.localScale =
                Vector3.Lerp(
                    startScale,
                    smallScale,
                    eased
                );

            Color fadeColor =
                Color.Lerp(
                    startColor,
                    combo1Color,
                    eased
                );

            fadeColor.a =
                Mathf.Lerp(
                    startColor.a,
                    0f,
                    EaseOutCubic(t)
                );

            comboText.color = fadeColor;

            yield return null;
        }

        comboText.SetText("x1");

        ResetTextTransform();
        comboText.color =
            SetAlpha(combo1Color, 0f);

        activeRoutine = null;
    }

    private IEnumerator AnimateScale(
        Vector3 startScale,
        Vector3 endScale,
        float duration,
        System.Func<float, float> easing)
    {
        if (duration <= 0f)
        {
            comboText.transform.localScale =
                endScale;

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            float eased =
                easing != null
                    ? easing(t)
                    : t;

            comboText.transform.localScale =
                Vector3.LerpUnclamped(
                    startScale,
                    endScale,
                    eased
                );

            yield return null;
        }

        comboText.transform.localScale =
            endScale;
    }

    private void SetTimerBarVisible(
        bool visible)
    {
        if (comboTimerBar == null)
            return;

        timerBarVisible = visible;

        if (comboTimerBar.gameObject.activeSelf != visible)
        {
            comboTimerBar.gameObject.SetActive(visible);
        }
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine == null)
            return;

        StopCoroutine(activeRoutine);
        activeRoutine = null;
    }

    private void ResetTextTransform()
    {
        if (comboText == null)
            return;

        comboText.transform.localScale =
            originalScale;

        comboText.transform.localPosition =
            originalPosition;

        comboText.transform.localRotation =
            originalRotation;
    }

    private void RefreshReferences()
    {
        if (comboText == null)
        {
            comboText =
                GetComponent<TextMeshProUGUI>();
        }
    }

    private static Color SetAlpha(
        Color color,
        float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private Color GetComboColor(
        int combo)
    {
        if (combo >= 6)
            return combo6Color;

        if (combo == 5)
            return combo5Color;

        if (combo == 4)
            return combo4Color;

        if (combo == 3)
            return combo3Color;

        if (combo == 2)
            return combo2Color;

        return combo1Color;
    }

    private static float EaseOutCubic(
        float t)
    {
        t =
            Mathf.Clamp01(t);

        float inverse =
            1f - t;

        return 1f -
               inverse *
               inverse *
               inverse;
    }

    private static float EaseInOutCubic(
        float t)
    {
        t =
            Mathf.Clamp01(t);

        if (t < 0.5f)
        {
            return 4f *
                   t *
                   t *
                   t;
        }

        float value =
            -2f * t + 2f;

        return 1f -
               (value *
                value *
                value) /
               2f;
    }

    private static float EaseOutBack(
        float t)
    {
        t =
            Mathf.Clamp01(t);

        const float c1 =
            1.70158f;

        const float c3 =
            c1 + 1f;

        float shifted =
            t - 1f;

        return 1f +
               c3 *
               shifted *
               shifted *
               shifted +
               c1 *
               shifted *
               shifted;
    }

    private void OnValidate()
    {
        pulseScale =
            Mathf.Max(
                1f,
                pulseScale
            );

        pulseDuration =
            Mathf.Max(
                0.01f,
                pulseDuration
            );

        stageUpMinScale =
            Mathf.Max(
                1f,
                stageUpMinScale
            );

        stageUpMaxScale =
            Mathf.Max(
                stageUpMinScale,
                stageUpMaxScale
            );

        stageUpDuration =
            Mathf.Max(
                0.01f,
                stageUpDuration
            );

        stageUpLift =
            Mathf.Max(
                0f,
                stageUpLift
            );

        stageUpTilt =
            Mathf.Max(
                0f,
                stageUpTilt
            );

        stageUpShakeAmount =
            Mathf.Max(
                0f,
                stageUpShakeAmount
            );

        maxComboShakeDuration =
            Mathf.Max(
                0.01f,
                maxComboShakeDuration
            );

        maxComboShakeAmount =
            Mathf.Max(
                0f,
                maxComboShakeAmount
            );

        maxComboScale =
            Mathf.Max(
                1f,
                maxComboScale
            );

        maxComboSecondPulseScale =
            Mathf.Clamp(
                maxComboSecondPulseScale,
                1f,
                maxComboScale
            );

        showFadeDuration =
            Mathf.Max(
                0.01f,
                showFadeDuration
            );

        showStartScale =
            Mathf.Clamp(
                showStartScale,
                0.75f,
                1f
            );

        resetFadeDuration =
            Mathf.Max(
                0.01f,
                resetFadeDuration
            );

        resetScale =
            Mathf.Clamp(
                resetScale,
                0.75f,
                1f
            );
    }
}