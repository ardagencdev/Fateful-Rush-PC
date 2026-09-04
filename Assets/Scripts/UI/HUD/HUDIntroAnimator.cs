using System;
using System.Collections;
using UnityEngine;

public class HUDIntroAnimator : MonoBehaviour
{
    public static bool HUDIntroFinished { get; private set; }

    [Serializable]
    public class HUDItem
    {
        public GameObject target;

        [Min(0f)]
        public float delay;
    }

    [Header("HUD Order")]
    public HUDItem[] hudItems;

    [Header("Animation")]
    [Min(0f)]
    public float popDuration = 0.18f;

    [Min(0f)]
    public float startScale;

    [Min(0f)]
    public float overshootScale = 1.15f;

    [Min(0f)]
    public float finalScale = 1f;

    private Coroutine activeRoutine;

    private void Awake()
    {
        HUDIntroFinished = false;
        HideInstant();
    }

    private void OnDisable()
    {
        StopActiveRoutine();
        HUDIntroFinished = false;
    }

    public void Play()
    {
        Play(null);
    }

    public void RegisterRuntimeItem(
        GameObject target,
        float delay = -1f)
    {
        if (target == null)
            return;

        if (hudItems != null)
        {
            for (int i = 0; i < hudItems.Length; i++)
            {
                HUDItem existingItem = hudItems[i];

                if (existingItem != null &&
                    existingItem.target == target)
                {
                    return;
                }
            }
        }

        int oldLength =
            hudItems != null
                ? hudItems.Length
                : 0;

        HUDItem[] expandedItems =
            new HUDItem[oldLength + 1];

        for (int i = 0; i < oldLength; i++)
            expandedItems[i] = hudItems[i];

        float resolvedDelay =
            ResolveRuntimeItemDelay(delay);

        expandedItems[oldLength] =
            new HUDItem
            {
                target = target,
                delay = resolvedDelay
            };

        hudItems = expandedItems;

        ResetItem(
            target,
            false,
            startScale
        );
    }

    private float ResolveRuntimeItemDelay(float delay)
    {
        if (delay >= 0f)
            return delay;

        if (hudItems == null)
            return 0f;

        for (int i = hudItems.Length - 1; i >= 0; i--)
        {
            HUDItem item = hudItems[i];

            if (!HasValidTarget(item))
                continue;

            return Mathf.Max(0f, item.delay);
        }

        return 0f;
    }

    public void Play(Func<GameObject, bool> shouldAnimate)
    {
        StopActiveRoutine();

        HUDIntroFinished = false;
        activeRoutine = StartCoroutine(
            PlayRoutine(shouldAnimate)
        );
    }

    public IEnumerator PlayAndWait()
    {
        yield return PlayAndWait(null);
    }

    public IEnumerator PlayAndWait(
        Func<GameObject, bool> shouldAnimate)
    {
        StopActiveRoutine();

        HUDIntroFinished = false;
        activeRoutine = StartCoroutine(
            PlayRoutine(shouldAnimate)
        );

        yield return activeRoutine;
    }

    public void HideInstant()
    {
        StopActiveRoutine();

        HUDIntroFinished = false;

        if (hudItems == null)
            return;

        foreach (HUDItem item in hudItems)
        {
            if (!HasValidTarget(item))
                continue;

            ResetItem(item.target, false, finalScale);
        }
    }

    private IEnumerator PlayRoutine(
        Func<GameObject, bool> shouldAnimate)
    {
        PrepareItemsForIntro();

        if (hudItems == null || hudItems.Length == 0)
        {
            FinishIntro();
            yield break;
        }

        foreach (HUDItem item in hudItems)
        {
            if (!HasValidTarget(item))
                continue;

            if (!ShouldAnimateItem(item.target, shouldAnimate))
            {
                ResetItem(item.target, false, finalScale);
                continue;
            }

            /*
             * Delay yalnızca gerçekten gösterilecek HUD için uygulanır.
             * Win condition nedeniyle kapalı olan Score, Combo veya Timer HUD
             * animasyon sırasını yavaşlatmaz.
             */
            if (item.delay > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    item.delay
                );
            }

            yield return PopItem(item.target);
        }

        FinishIntro();
    }

    private IEnumerator PopItem(GameObject target)
    {
        if (target == null)
            yield break;

        Transform targetTransform = target.transform;

        /*
         * UIButtonEffect gibi componentlerin Awake sırasında sıfır scale
         * değerini başlangıç scale'i olarak kaydetmesini engeller.
         */
        targetTransform.localScale =
            Vector3.one * finalScale;

        target.SetActive(true);

        if (popDuration <= 0f)
        {
            targetTransform.localScale =
                Vector3.one * finalScale;

            yield break;
        }

        float elapsedTime = 0f;

        targetTransform.localScale =
            Vector3.one * startScale;

        while (elapsedTime < popDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    elapsedTime / popDuration
                );

            float scale =
                EvaluateScale(normalizedTime);

            targetTransform.localScale =
                Vector3.one * scale;

            yield return null;
        }

        targetTransform.localScale =
            Vector3.one * finalScale;
    }

    private float EvaluateScale(float normalizedTime)
    {
        const float overshootPoint = 0.65f;

        if (normalizedTime < overshootPoint)
        {
            float progress =
                normalizedTime / overshootPoint;

            return Mathf.Lerp(
                startScale,
                overshootScale,
                EaseOutBack(progress)
            );
        }

        float returnProgress =
            (normalizedTime - overshootPoint) /
            (1f - overshootPoint);

        return Mathf.Lerp(
            overshootScale,
            finalScale,
            returnProgress
        );
    }

    private void PrepareItemsForIntro()
    {
        if (hudItems == null)
            return;

        foreach (HUDItem item in hudItems)
        {
            if (!HasValidTarget(item))
                continue;

            ResetItem(
                item.target,
                false,
                startScale
            );
        }
    }

    private static bool ShouldAnimateItem(
        GameObject target,
        Func<GameObject, bool> shouldAnimate)
    {
        if (target == null)
            return false;

        return shouldAnimate == null ||
               shouldAnimate(target);
    }

    private static bool HasValidTarget(HUDItem item)
    {
        return item != null && item.target != null;
    }

    private static void ResetItem(
        GameObject target,
        bool active,
        float scale)
    {
        if (target == null)
            return;

        target.transform.localScale =
            Vector3.one * scale;

        target.SetActive(active);
    }

    private void FinishIntro()
    {
        HUDIntroFinished = true;
        activeRoutine = null;
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine == null)
            return;

        StopCoroutine(activeRoutine);
        activeRoutine = null;
    }

    private static float EaseOutBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        float shiftedValue = value - 1f;

        return 1f +
               c3 * shiftedValue * shiftedValue * shiftedValue +
               c1 * shiftedValue * shiftedValue;
    }

    private void OnValidate()
    {
        popDuration = Mathf.Max(0f, popDuration);
        startScale = Mathf.Max(0f, startScale);
        overshootScale = Mathf.Max(0f, overshootScale);
        finalScale = Mathf.Max(0f, finalScale);

        if (hudItems == null)
            return;

        foreach (HUDItem item in hudItems)
        {
            if (item != null)
                item.delay = Mathf.Max(0f, item.delay);
        }
    }
}