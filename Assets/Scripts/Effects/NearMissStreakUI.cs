using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class NearMissStreakUI : MonoBehaviour
{
    private static NearMissStreakUI instance;

    [Header("Colors")]
    [SerializeField]
    private Color firstColor = Color.white;

    [SerializeField]
    private Color maxColor = new Color(1f, 0.08f, 0.04f, 1f);

    [Header("Streak Visual")]
    [SerializeField, Min(2)]
    private int maxVisualStreak = 6;

    [SerializeField, Min(1f)]
    private float minPunchScale = 1.14f;

    [SerializeField, Min(1f)]
    private float maxPunchScale = 1.36f;

    [SerializeField, Min(0f)]
    private float minShakePixels = 1.5f;

    [SerializeField, Min(0f)]
    private float maxShakePixels = 7f;

    [SerializeField, Min(0f)]
    private float maxTiltDegrees = 3.5f;

    [SerializeField, Min(0.01f)]
    private float impactDuration = 0.18f;

    [Header("Disappear")]
    [SerializeField, Min(0.01f)]
    private float fadeDuration = 0.22f;

    private TextMeshProUGUI text;
    private RectTransform rectTransform;
    private Coroutine activeRoutine;

    // These values are captured ONCE from the HUD TMP in Awake.
    // Every Near Miss effect is calculated relative to these values,
    // so scale/position/rotation can never accumulate between streaks.
    private Vector2 baseAnchoredPosition;
    private Vector3 baseScale;
    private Quaternion baseRotation;
    private bool baseTransformCaptured;

    private void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();

        CaptureBaseTransformOnce();
        ResetTransform();
        SetVisible(false);

        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.LogWarning(
                "More than one NearMissStreakUI exists in the scene. " +
                "The first active instance will be used.",
                this
            );
        }
    }

    private void OnEnable()
    {
        // Register again whenever this HUD object becomes active.
        // This also makes the setup safe when the HUD starts disabled.
        if (instance == null || instance == this)
            instance = this;

        // If the object was disabled while an effect was running,
        // always return to the HUD-authored transform when it comes back.
        if (baseTransformCaptured)
            ResetTransform();
    }

    private void Update()
    {
        if (GameStateManager.IsGameplayEnded)
            HideImmediately();
    }

    private void OnDisable()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (baseTransformCaptured)
            ResetTransform();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void ShowNearMiss(
        int streak,
        float closeness01)
    {
        NearMissStreakUI ui = GetExistingUI();

        if (ui == null)
            return;

        ui.Play(
            Mathf.Max(1, streak),
            Mathf.Clamp01(closeness01)
        );
    }

    private static NearMissStreakUI GetExistingUI()
    {
        if (instance != null)
            return instance;

        // Search INCLUDING inactive HUD objects.
        // FindAnyObjectByType() excludes inactive objects, which could make a
        // correctly configured NearMissText look as if it did not exist.
        NearMissStreakUI[] found = UnityFindCompat.FindObjectsByType<NearMissStreakUI>(
    FindObjectsInactive.Include
);

        if (found != null && found.Length > 0)
        {
            instance = found[0];

            // NearMissText itself should stay active. Visibility is controlled
            // only through TMP alpha, never by disabling the GameObject.
            if (!instance.gameObject.activeSelf)
                instance.gameObject.SetActive(true);

            if (!instance.enabled)
                instance.enabled = true;

            return instance;
        }

        Debug.LogWarning(
            "NearMissStreakUI could not find the HUD NearMissText. " +
            "Keep one NearMissStreakUI component on the HUD NearMissText TMP object."
        );

        return null;
    }

    private void Play(
        int streak,
        float closeness01)
    {
        if (!isActiveAndEnabled ||
            text == null ||
            rectTransform == null)
        {
            return;
        }

        // IMPORTANT:
        // Cancel the previous effect and go back to the exact HUD-authored
        // transform BEFORE calculating the new streak effect.
        // This prevents a previous x6/x25-looking punch from becoming the
        // starting scale of a later x1 Near Miss.
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        ResetTransform();

        // Only the displayed string is changed.
        // Font, font size, Auto Size, alignment, spacing, material, etc.
        // are completely controlled by the TMP in the HUD Inspector.
        text.SetText("NEAR MISS  x{0}", streak);

        float streak01 = Mathf.InverseLerp(
            1f,
            Mathf.Max(2, maxVisualStreak),
            Mathf.Clamp(
                streak,
                1,
                Mathf.Max(2, maxVisualStreak)
            )
        );

        Color targetColor = Color.Lerp(
            firstColor,
            maxColor,
            streak01
        );

        text.color = SetAlpha(targetColor, 1f);

        activeRoutine = StartCoroutine(
            ImpactAndFadeRoutine(
                streak01,
                closeness01,
                targetColor
            )
        );
    }

    private IEnumerator ImpactAndFadeRoutine(
        float streak01,
        float closeness01,
        Color targetColor)
    {
        float closenessFactor = Mathf.Lerp(
            0.85f,
            1f,
            closeness01
        );

        // This is a MULTIPLIER of the original HUD scale captured in Awake.
        // It is never based on the scale left by a previous Near Miss.
        float punchMultiplier = Mathf.Lerp(
            minPunchScale,
            maxPunchScale,
            streak01
        ) * Mathf.Lerp(0.96f, 1f, closeness01);

        float shakePixels = Mathf.Lerp(
            minShakePixels,
            maxShakePixels,
            streak01
        ) * closenessFactor;

        float tilt =
            maxTiltDegrees *
            streak01 *
            closenessFactor;

        float elapsed = 0f;

        while (elapsed < impactDuration)
        {
            if (Time.timeScale <= 0f)
            {
                yield return null;
                continue;
            }

            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / impactDuration
            );

            float impact = 1f - progress;

            float currentScaleMultiplier = Mathf.Lerp(
                1f,
                punchMultiplier,
                impact
            );

            rectTransform.localScale =
                baseScale * currentScaleMultiplier;

            rectTransform.anchoredPosition =
                baseAnchoredPosition +
                Random.insideUnitCircle *
                shakePixels *
                impact;

            rectTransform.localRotation =
                baseRotation *
                Quaternion.Euler(
                    0f,
                    0f,
                    Random.Range(-tilt, tilt) * impact
                );

            yield return null;
        }

        // Impact is over: return EXACTLY to the original HUD transform.
        ResetTransform();
        text.color = SetAlpha(targetColor, 1f);

        float holdDuration = Mathf.Max(
            0f,
            NearMissFeedback.StreakTimeout - impactDuration
        );

        float holdElapsed = 0f;

        while (holdElapsed < holdDuration)
        {
            if (Time.timeScale > 0f)
                holdElapsed += Time.unscaledDeltaTime;

            yield return null;
        }

        float fadeElapsed = 0f;

        while (fadeElapsed < fadeDuration)
        {
            if (Time.timeScale <= 0f)
            {
                yield return null;
                continue;
            }

            fadeElapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                fadeElapsed / fadeDuration
            );

            text.color = SetAlpha(
                targetColor,
                1f - progress
            );

            // Keep the old fade shrink effect, but always relative to
            // the original HUD scale so it cannot accumulate either.
            rectTransform.localScale =
                baseScale *
                Mathf.Lerp(1f, 0.92f, progress);

            yield return null;
        }

        SetVisible(false);
        ResetTransform();
        activeRoutine = null;
    }

    private void CaptureBaseTransformOnce()
    {
        if (baseTransformCaptured || rectTransform == null)
            return;

        baseAnchoredPosition = rectTransform.anchoredPosition;
        baseScale = rectTransform.localScale;
        baseRotation = rectTransform.localRotation;
        baseTransformCaptured = true;
    }

    private void ResetTransform()
    {
        if (!baseTransformCaptured || rectTransform == null)
            return;

        rectTransform.anchoredPosition = baseAnchoredPosition;
        rectTransform.localScale = baseScale;
        rectTransform.localRotation = baseRotation;
    }

    private void SetVisible(bool visible)
    {
        if (text == null)
            return;

        Color color = text.color;
        color.a = visible ? 1f : 0f;
        text.color = color;
    }

    private static Color SetAlpha(
        Color color,
        float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private void HideImmediately()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        SetVisible(false);
        ResetTransform();
    }

    private void OnValidate()
    {
        maxVisualStreak = Mathf.Max(2, maxVisualStreak);

        minPunchScale = Mathf.Max(1f, minPunchScale);
        maxPunchScale = Mathf.Max(minPunchScale, maxPunchScale);

        minShakePixels = Mathf.Max(0f, minShakePixels);
        maxShakePixels = Mathf.Max(minShakePixels, maxShakePixels);

        maxTiltDegrees = Mathf.Max(0f, maxTiltDegrees);
        impactDuration = Mathf.Max(0.01f, impactDuration);
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
    }
}
