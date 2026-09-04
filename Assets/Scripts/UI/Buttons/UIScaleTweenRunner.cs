using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implemented by UI controls that want their sprite/color state coalesced onto
/// the shared UI animation tick instead of dirtying the Canvas immediately for
/// every transient pointer event.
/// </summary>
public interface IUIScheduledVisual
{
    void ApplyScheduledVisualState();
}

/// <summary>
/// Shared, allocation-free scheduler for temporary uGUI button animation.
///
/// Main goals:
/// - No per-button Update methods.
/// - No stacked coroutines/tweens.
/// - Scale writes are capped at 120 Hz, so an uncapped PC build does not dirty
///   the UI at hundreds of FPS just because the mouse is over a button.
/// - Enter/exit events that happen between UI ticks are coalesced; buttons that
///   are crossed too quickly do no pointless scale/sprite work.
/// - Uses unscaled time, so pause/menu UI remains responsive at timeScale = 0.
///
/// No scene object or Inspector setup is required.
/// </summary>
public sealed class UIScaleTweenRunner : MonoBehaviour
{
    private const float MaxAnimationHz = 120f;
    private const float TickInterval = 1f / MaxAnimationHz;

    private sealed class TweenState
    {
        public RectTransform target;
        public Vector3 targetScale;
        public float speed;
        public float thresholdSqr;
    }

    private static UIScaleTweenRunner instance;

    private readonly Dictionary<EntityId, TweenState> tweens =
        new Dictionary<EntityId, TweenState>(32);

    private readonly Dictionary<EntityId, IUIScheduledVisual> scheduledVisuals =
        new Dictionary<EntityId, IUIScheduledVisual>(32);

    private readonly List<EntityId> removeBuffer =
        new List<EntityId>(32);

    private readonly List<IUIScheduledVisual> visualBuffer =
        new List<IUIScheduledVisual>(32);

    private float accumulatedUnscaledTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static void TweenTo(
        RectTransform target,
        Vector3 targetScale,
        float speed,
        float settleThreshold)
    {
        if (target == null)
            return;

        settleThreshold = Mathf.Max(0.000001f, settleThreshold);
        float thresholdSqr = settleThreshold * settleThreshold;

        if ((target.localScale - targetScale).sqrMagnitude <= thresholdSqr)
        {
            // Important for very fast mouse movement: if enter + exit happens
            // before the next UI tick, cancel without writing the same scale
            // back into the RectTransform and therefore without dirtying UI.
            Cancel(target);
            return;
        }

        if (speed <= 0f)
        {
            Cancel(target);
            target.localScale = targetScale;
            return;
        }

        EnsureInstance();

        EntityId id = target.GetEntityId();

        if (!instance.tweens.TryGetValue(id, out TweenState state))
        {
            state = new TweenState
            {
                target = target
            };

            instance.tweens.Add(id, state);
        }

        // Retarget the existing tween instead of starting another one.
        state.target = target;
        state.targetScale = targetScale;
        state.speed = speed;
        state.thresholdSqr = thresholdSqr;
    }

    public static void ScheduleVisual(IUIScheduledVisual visual)
    {
        if (visual == null)
            return;

        if (!(visual is MonoBehaviour behaviour) || behaviour == null)
            return;

        EnsureInstance();
        instance.scheduledVisuals[behaviour.GetEntityId()] = visual;
    }

    public static void CancelScheduledVisual(IUIScheduledVisual visual)
    {
        if (visual == null || instance == null)
            return;

        if (visual is MonoBehaviour behaviour && behaviour != null)
            instance.scheduledVisuals.Remove(behaviour.GetEntityId());
    }

    public static void Cancel(RectTransform target)
    {
        if (target == null || instance == null)
            return;

        instance.tweens.Remove(target.GetEntityId());
    }

    public static void CancelAndSnap(
        RectTransform target,
        Vector3 scale)
    {
        if (target == null)
            return;

        Cancel(target);

        if ((target.localScale - scale).sqrMagnitude > 0.0000000001f)
            target.localScale = scale;
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject runnerObject = new GameObject("[UI Animation Runner]");
        runnerObject.hideFlags = HideFlags.HideInHierarchy;

        instance = runnerObject.AddComponent<UIScaleTweenRunner>();
        DontDestroyOnLoad(runnerObject);
    }

    private void Update()
    {
        if (tweens.Count == 0 && scheduledVisuals.Count == 0)
        {
            accumulatedUnscaledTime = 0f;
            return;
        }

        accumulatedUnscaledTime += Time.unscaledDeltaTime;

        // At <=120 FPS this executes every rendered frame. Above 120 FPS it
        // deliberately coalesces UI changes while the rest of the game can
        // continue rendering at its uncapped/high-refresh rate.
        if (accumulatedUnscaledTime < TickInterval)
            return;

        float dt = accumulatedUnscaledTime;
        accumulatedUnscaledTime = 0f;

        ApplyScheduledVisuals();
        UpdateScaleTweens(dt);
    }

    private void ApplyScheduledVisuals()
    {
        if (scheduledVisuals.Count == 0)
            return;

        visualBuffer.Clear();

        foreach (IUIScheduledVisual visual in scheduledVisuals.Values)
            visualBuffer.Add(visual);

        // Clear first so a callback can safely schedule a future visual update.
        scheduledVisuals.Clear();

        for (int i = 0; i < visualBuffer.Count; i++)
        {
            IUIScheduledVisual visual = visualBuffer[i];

            if (visual is MonoBehaviour behaviour &&
                behaviour != null &&
                behaviour.isActiveAndEnabled)
            {
                visual.ApplyScheduledVisualState();
            }
        }
    }

    private void UpdateScaleTweens(float dt)
    {
        if (tweens.Count == 0)
            return;

        removeBuffer.Clear();

        foreach (KeyValuePair<EntityId, TweenState> pair in tweens)
        {
            TweenState state = pair.Value;
            RectTransform target = state.target;

            if (target == null || !target.gameObject.activeInHierarchy)
            {
                removeBuffer.Add(pair.Key);
                continue;
            }

            Vector3 current = target.localScale;
            Vector3 delta = current - state.targetScale;

            if (delta.sqrMagnitude <= state.thresholdSqr)
            {
                if (delta.sqrMagnitude > 0f)
                    target.localScale = state.targetScale;

                removeBuffer.Add(pair.Key);
                continue;
            }

            // Frame-rate independent equivalent of the old Lerp feel:
            // smooth exponential response, but independent of render FPS.
            float t = 1f - Mathf.Exp(-state.speed * dt);

            Vector3 next = Vector3.LerpUnclamped(
                current,
                state.targetScale,
                t
            );

            if ((next - state.targetScale).sqrMagnitude <= state.thresholdSqr)
            {
                next = state.targetScale;
                removeBuffer.Add(pair.Key);
            }

            target.localScale = next;
        }

        for (int i = 0; i < removeBuffer.Count; i++)
            tweens.Remove(removeBuffer[i]);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
