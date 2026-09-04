using UnityEngine;

[DefaultExecutionOrder(1000)]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Shake Quality")]
    [SerializeField, Min(1f)]
    private float frequency = 34f;

    [SerializeField, Range(1f, 5f)]
    private float decayPower = 1.35f;

    [SerializeField, Min(0.05f)]
    private float maximumCombinedOffset = 0.42f;

    private Vector3 baseLocalPosition;
    private Vector3 appliedLocalOffset;

    private bool impactActive;
    private float impactDuration;
    private float impactElapsed;
    private float impactStrength;
    private float impactSeedX;
    private float impactSeedY;

    private bool softActive;
    private float softDuration;
    private float softElapsed;
    private float softStrength;
    private float softSeedX;
    private float softSeedY;

    public Vector3 StableWorldPosition
    {
        get
        {
            if (transform.parent == null)
                return baseLocalPosition;

            return transform.parent.TransformPoint(baseLocalPosition);
        }
    }

    public Vector3 CurrentWorldShakeOffset =>
        transform.position - StableWorldPosition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        baseLocalPosition = transform.localPosition;
        appliedLocalOffset = Vector3.zero;
    }

    private void LateUpdate()
    {
        // Remove the offset that this component applied on the previous frame.
        // This lets another camera script move the camera normally without the
        // shake system snapping it back to the Awake position.
        baseLocalPosition =
            transform.localPosition - appliedLocalOffset;

        float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);

        Vector3 impactOffset = UpdateChannel(
            ref impactActive,
            ref impactElapsed,
            impactDuration,
            impactStrength,
            impactSeedX,
            impactSeedY,
            deltaTime
        );

        Vector3 softOffset = UpdateChannel(
            ref softActive,
            ref softElapsed,
            softDuration,
            softStrength,
            softSeedX,
            softSeedY,
            deltaTime
        );

        Vector3 combinedOffset = impactOffset + softOffset;

        float maxOffset = Mathf.Max(0.05f, maximumCombinedOffset);

        if (combinedOffset.sqrMagnitude > maxOffset * maxOffset)
        {
            combinedOffset =
                combinedOffset.normalized * maxOffset;
        }

        appliedLocalOffset = combinedOffset;
        transform.localPosition = baseLocalPosition + appliedLocalOffset;
    }

    public void Shake(float duration, float strength)
    {
        StartImpactShake(duration, strength);
    }

    // Lightweight gameplay feedback such as Near Miss. This channel is
    // independent from the main impact channel, so it never cancels a boss,
    // armor-break or death impact.
    public void ShakeSoft(float duration, float strength)
    {
        StartSoftShake(duration, strength);
    }

    private void StartImpactShake(float duration, float strength)
    {
        duration = Mathf.Max(0f, duration);
        strength = Mathf.Max(0f, strength);

        if (duration <= 0f ||
            strength <= 0f ||
            !isActiveAndEnabled)
        {
            return;
        }

        float currentStrength =
            GetCurrentChannelStrength(
                impactActive,
                impactElapsed,
                impactDuration,
                impactStrength
            );

        float remainingDuration =
            impactActive
                ? Mathf.Max(0f, impactDuration - impactElapsed)
                : 0f;

        // A weaker impact should not cut off a stronger one that is already
        // playing. Stronger/newer impacts (for example death after an AOE)
        // replace the current impact cleanly.
        if (impactActive &&
            strength < currentStrength &&
            duration <= remainingDuration)
        {
            return;
        }

        impactActive = true;
        impactDuration = duration;
        impactElapsed = 0f;
        impactStrength = strength;
        impactSeedX = Random.Range(0f, 1000f);
        impactSeedY = Random.Range(0f, 1000f);
    }

    private void StartSoftShake(float duration, float strength)
    {
        duration = Mathf.Max(0f, duration);
        strength = Mathf.Max(0f, strength);

        if (duration <= 0f ||
            strength <= 0f ||
            !isActiveAndEnabled)
        {
            return;
        }

        float currentStrength =
            GetCurrentChannelStrength(
                softActive,
                softElapsed,
                softDuration,
                softStrength
            );

        // Do not let a weaker near-miss replace a stronger near-miss that is
        // still at its peak. A stronger streak hit can still upgrade it.
        if (softActive && strength < currentStrength)
            return;

        softActive = true;
        softDuration = duration;
        softElapsed = 0f;
        softStrength = strength;
        softSeedX = Random.Range(0f, 1000f);
        softSeedY = Random.Range(0f, 1000f);
    }

    private Vector3 UpdateChannel(
        ref bool active,
        ref float elapsed,
        float duration,
        float strength,
        float seedX,
        float seedY,
        float deltaTime)
    {
        if (!active || duration <= 0f || strength <= 0f)
            return Vector3.zero;

        elapsed += deltaTime;

        float progress = Mathf.Clamp01(elapsed / duration);
        float decay = Mathf.Pow(1f - progress, decayPower);

        float sampleTime = Time.unscaledTime * frequency;

        float noiseX =
            Mathf.PerlinNoise(seedX, sampleTime) * 2f - 1f;

        float noiseY =
            Mathf.PerlinNoise(seedY, sampleTime) * 2f - 1f;

        Vector3 offset =
            new Vector3(noiseX, noiseY, 0f) *
            strength *
            decay;

        if (elapsed >= duration)
        {
            active = false;
            elapsed = 0f;
            return Vector3.zero;
        }

        return offset;
    }

    private float GetCurrentChannelStrength(
        bool active,
        float elapsed,
        float duration,
        float strength)
    {
        if (!active || duration <= 0f || strength <= 0f)
            return 0f;

        float progress = Mathf.Clamp01(elapsed / duration);
        float decay = Mathf.Pow(1f - progress, decayPower);
        return strength * decay;
    }

    public void StopAllShake()
    {
        impactActive = false;
        softActive = false;
        impactElapsed = 0f;
        softElapsed = 0f;

        transform.localPosition = baseLocalPosition;
        appliedLocalOffset = Vector3.zero;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            StopAllShake();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            StopAllShake();
    }

    private void OnDisable()
    {
        StopAllShake();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        frequency = Mathf.Max(1f, frequency);

        decayPower = Mathf.Clamp(
            decayPower,
            1f,
            5f
        );

        maximumCombinedOffset =
            Mathf.Max(0.05f, maximumCombinedOffset);
    }
}
