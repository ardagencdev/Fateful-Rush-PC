using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class AmbientTextGlitchSwapper : MonoBehaviour
{
    private const int RequiredTextCount = 3;

    [Header("References")]
    [Tooltip("SignalLostText, ThreatUnknownText ve NoReturnVectorText sırasıyla atanmalı.")]
    [SerializeField] private MenuFloatingText[] floatingTexts =
        new MenuFloatingText[RequiredTextCount];

    [Header("Swap Timing")]
    [Min(0.5f)]
    [SerializeField] private float swapIntervalMin = 10f;

    [Min(0.5f)]
    [SerializeField] private float swapIntervalMax = 20f;

    [Header("Shared Glitch")]
    [Range(0.04f, 0.25f)]
    [SerializeField] private float sharedGlitchDuration = 0.09f;

    [Range(0.1f, 0.9f)]
    [SerializeField] private float swapMoment = 0.5f;

    [Header("Reset")]
    [Tooltip("Main menu kapanınca yazıları kendi başlangıç metinlerine döndürür.")]
    [FormerlySerializedAs("restoreOriginalOrderOnDisable")]
    [SerializeField] private bool restoreOriginalTextsOnDisable = true;

    private readonly string[] originalTexts = new string[RequiredTextCount];
    private readonly string[] currentTexts = new string[RequiredTextCount];
    private readonly string[] nextTexts = new string[RequiredTextCount];

    private Coroutine swapRoutine;
    private bool originalTextsCaptured;

    private void Reset()
    {
        AutoAssignChildren();
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();

        if (!HasValidReferences())
            return;

        if (swapRoutine != null)
            StopCoroutine(swapRoutine);

        swapRoutine = StartCoroutine(SwapLoop());
    }

    private IEnumerator SwapLoop()
    {
        while (isActiveAndEnabled && !AllTextsInitialized())
            yield return null;

        if (!isActiveAndEnabled)
            yield break;

        SetSharedControl(true);

        if (!originalTextsCaptured)
            CaptureOriginalTexts();

        while (isActiveAndEnabled)
        {
            float minimum = Mathf.Max(0.5f, swapIntervalMin);
            float maximum = Mathf.Max(minimum, swapIntervalMax);

            yield return new WaitForSecondsRealtime(Random.Range(minimum, maximum));

            while (isActiveAndEnabled && !AllTextsReadyForGlitch())
                yield return null;

            if (!isActiveAndEnabled)
                yield break;

            yield return PlayGlitchAndSwapTexts();
        }
    }

    private IEnumerator PlayGlitchAndSwapTexts()
    {
        BuildRandomTextDerangement();

        float duration = Mathf.Max(0.04f, sharedGlitchDuration);
        float switchTime = duration * Mathf.Clamp(swapMoment, 0.1f, 0.9f);
        float elapsed = 0f;
        bool textsChanged = false;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            // Metin değişimi glitch'in tam ortasında gerçekleşir. Transformlara
            // kesinlikle dokunulmaz; her obje kendi fiziksel konumunda kalır.
            if (!textsChanged && elapsed >= switchTime)
            {
                ApplyNextTextAssignment();
                textsChanged = true;
            }

            for (int i = 0; i < RequiredTextCount; i++)
                floatingTexts[i].ApplySharedGlitchFrame();

            yield return null;
        }

        if (!textsChanged)
            ApplyNextTextAssignment();

        EndAllGlitches();
    }

    private void BuildRandomTextDerangement()
    {
        for (int i = 0; i < RequiredTextCount; i++)
            currentTexts[i] = floatingTexts[i].StableText;

        // Üç yazıda, hiçbir yazının aynı yerde kalmadığı iki güvenli dağılım vardır:
        // sola döndürme veya sağa döndürme. Böylece aynı metin iki objeye kopyalanmaz.
        bool rotateForward = Random.value < 0.5f;
        int offset = rotateForward ? 1 : RequiredTextCount - 1;

        for (int i = 0; i < RequiredTextCount; i++)
            nextTexts[i] = currentTexts[(i + offset) % RequiredTextCount];
    }

    private void ApplyNextTextAssignment()
    {
        for (int i = 0; i < RequiredTextCount; i++)
            floatingTexts[i].SetStableText(nextTexts[i]);
    }

    private void CaptureOriginalTexts()
    {
        for (int i = 0; i < RequiredTextCount; i++)
            originalTexts[i] = floatingTexts[i].StableText;

        originalTextsCaptured = true;
    }

    private bool AllTextsInitialized()
    {
        if (!HasValidReferences())
            return false;

        for (int i = 0; i < RequiredTextCount; i++)
        {
            if (!floatingTexts[i].IsInitialized)
                return false;
        }

        return true;
    }

    private bool AllTextsReadyForGlitch()
    {
        for (int i = 0; i < RequiredTextCount; i++)
        {
            if (!floatingTexts[i].IsReadyForSharedGlitch)
                return false;
        }

        return true;
    }

    private void SetSharedControl(bool controlled)
    {
        if (floatingTexts == null)
            return;

        for (int i = 0; i < floatingTexts.Length; i++)
        {
            if (floatingTexts[i] != null)
                floatingTexts[i].SetSharedGlitchControlled(controlled);
        }
    }

    private void EndAllGlitches()
    {
        if (floatingTexts == null)
            return;

        for (int i = 0; i < floatingTexts.Length; i++)
        {
            if (floatingTexts[i] != null)
                floatingTexts[i].EndSharedGlitch();
        }
    }

    private void RestoreOriginalTexts()
    {
        if (!originalTextsCaptured || !HasValidReferences())
            return;

        for (int i = 0; i < RequiredTextCount; i++)
            floatingTexts[i].SetStableText(originalTexts[i]);
    }

    private void EnsureReferences()
    {
        if (HasValidReferences())
            return;

        AutoAssignChildren();

        if (!HasValidReferences())
        {
            Debug.LogWarning(
                $"{nameof(AmbientTextGlitchSwapper)} requires exactly three " +
                $"{nameof(MenuFloatingText)} references.",
                this
            );
        }
    }

    private void AutoAssignChildren()
    {
        MenuFloatingText[] foundTexts =
            GetComponentsInChildren<MenuFloatingText>(true);

        if (foundTexts.Length != RequiredTextCount)
            return;

        floatingTexts = foundTexts;
    }

    private bool HasValidReferences()
    {
        if (floatingTexts == null || floatingTexts.Length != RequiredTextCount)
            return false;

        for (int i = 0; i < RequiredTextCount; i++)
        {
            if (floatingTexts[i] == null)
                return false;

            for (int j = i + 1; j < RequiredTextCount; j++)
            {
                if (floatingTexts[i] == floatingTexts[j])
                    return false;
            }
        }

        return true;
    }

    private void OnDisable()
    {
        if (swapRoutine != null)
        {
            StopCoroutine(swapRoutine);
            swapRoutine = null;
        }

        EndAllGlitches();

        if (restoreOriginalTextsOnDisable)
            RestoreOriginalTexts();

        SetSharedControl(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        swapIntervalMin = Mathf.Max(0.5f, swapIntervalMin);
        swapIntervalMax = Mathf.Max(swapIntervalMin, swapIntervalMax);
        sharedGlitchDuration = Mathf.Max(0.04f, sharedGlitchDuration);
        swapMoment = Mathf.Clamp(swapMoment, 0.1f, 0.9f);
    }
#endif
}
