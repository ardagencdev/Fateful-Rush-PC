using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class ObstacleIdleAnimation : MonoBehaviour
{
    public enum IdlePreset
    {
        Custom,
        Asteroid,
        HeavyAsteroid,
        SharpDebris,
        Satellite,
        BrokenSatellite,
        Wreckage,
        EnergyObject,
        Star,
        BlackHole,
        AlienOrganic,
        TechWall,
        Planet
    }

    public enum RotationMotion
    {
        Continuous,
        Sway
    }

    public enum AnimationTimeSource
    {
        Scaled,
        Unscaled
    }

    [Header("Preset")]
    [SerializeField] private IdlePreset preset = IdlePreset.Custom;
    [SerializeField, HideInInspector] private IdlePreset lastAppliedPreset = (IdlePreset)(-1);

    [Header("References")]
    [Tooltip("The transform that receives the idle animation. Leave empty to animate this object.")]
    [SerializeField] private Transform animationTarget;

    [Tooltip("Renderer used by Color Pulse. Leave empty to find the first SpriteRenderer automatically.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("General")]
    [SerializeField] private AnimationTimeSource timeSource = AnimationTimeSource.Scaled;

    [Tooltip("Gives duplicated obstacles different phases without changing Unity's global Random state.")]
    [SerializeField] private bool randomizePhase = true;

    [Tooltip("Change this value when you want a different deterministic variation.")]
    [SerializeField] private int randomSeed;

    [Tooltip("Smoothly blends the idle animation in after the object is enabled.")]
    [SerializeField, Min(0f)] private float blendInDuration = 0.2f;

    [Header("Rotation")]
    [SerializeField] private bool rotate = true;
    [SerializeField] private RotationMotion rotationMotion = RotationMotion.Continuous;

    [Tooltip("Degrees per second in Continuous mode.")]
    [SerializeField] private float rotateSpeed = 10f;

    [Tooltip("Randomly chooses clockwise or counter-clockwise rotation per instance.")]
    [SerializeField] private bool randomizeRotateDirection = true;

    [Tooltip("Random per-instance speed difference. 0.25 means up to ±25%.")]
    [SerializeField, Range(0f, 0.9f)] private float rotateSpeedVariation = 0.2f;

    [Tooltip("Maximum angle in Sway mode.")]
    [SerializeField, Min(0f)] private float swayAngle = 5f;

    [Tooltip("Sway oscillation speed.")]
    [SerializeField, Min(0f)] private float swaySpeed = 1f;

    [Header("Hover")]
    [SerializeField] private bool hover;

    [Tooltip("Local X and Y movement amount.")]
    [SerializeField] private Vector2 hoverAmount = new Vector2(0.03f, 0.05f);

    [SerializeField, Min(0f)] private float hoverSpeed = 1f;

    [Tooltip("Creates a less mechanical elliptical movement.")]
    [SerializeField, Range(0.1f, 2f)] private float horizontalFrequencyMultiplier = 0.73f;

    [Header("Organic Drift")]
    [SerializeField] private bool drift;
    [SerializeField, Min(0f)] private float driftAmount = 0.02f;
    [SerializeField, Min(0f)] private float driftSpeed = 0.35f;

    [Header("Scale Pulse")]
    [SerializeField] private bool pulseScale;

    [Tooltip("0.05 means a maximum scale change of 5%.")]
    [SerializeField, Range(0f, 0.5f)] private float pulseAmount = 0.08f;

    [SerializeField, Min(0f)] private float pulseSpeed = 2f;

    [Tooltip("Use (1,1) for uniform breathing. A negative axis creates squash-and-stretch.")]
    [SerializeField] private Vector2 pulseAxis = Vector2.one;

    [Header("Color Pulse")]
    [SerializeField] private bool pulseColor;
    [SerializeField] private bool useOriginalColorAsColor1 = true;
    [SerializeField] private Color color1 = Color.white;
    [SerializeField] private Color color2 = Color.cyan;
    [SerializeField, Min(0f)] private float colorPulseSpeed = 2f;
    [SerializeField] private bool preserveOriginalAlpha = true;

    [Header("Shake")]
    [SerializeField] private bool shake;
    [SerializeField, Min(0f)] private float shakeAmount = 0.05f;
    [SerializeField, Min(0f)] private float shakeSpeed = 20f;

    private Transform activeTarget;
    private SpriteRenderer activeRenderer;

    // Unity 6 recommends that a Collider2D which moves at runtime is moved
    // through a Rigidbody2D rather than by changing its Transform directly.
    // Animated obstacle roots therefore become Kinematic physics bodies.
    private Rigidbody2D physicsBody;
    private bool drivePhysicsWithRigidbody;

    // Planned motion for the current physics step. Enemy avoidance reads this
    // before contact happens so moving/rotating obstacle sweeps can be predicted.
    private Vector2 plannedLinearVelocity;
    private float plannedAngularVelocity;

    private Vector3 lastPositionOffset;
    private Quaternion lastRotationOffset = Quaternion.identity;
    private Vector3 lastScaleMultiplier = Vector3.one;

    private Color baseColor;
    private Color lastAppliedColor;
    private bool colorApplied;

    private float elapsedTime;
    private float continuousRotation;
    private float rotationDirection = 1f;
    private float rotationSpeedMultiplier = 1f;

    private float hoverPhaseX;
    private float hoverPhaseY;
    private float scalePhase;
    private float colorPhase;
    private float swayPhase;
    private float driftSeedX;
    private float driftSeedY;
    private float shakeSeedX;
    private float shakeSeedY;

    private bool initialized;

    public IdlePreset Preset => preset;

    private void Reset()
    {
        ResolveReferences();
        preset = IdlePreset.Custom;
        lastAppliedPreset = IdlePreset.Custom;
        ClampValues();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        InitializeAnimation();
    }

    private void FixedUpdate()
    {
        if (!initialized || GetResolvedTarget() != activeTarget)
            InitializeAnimation();

        if (activeTarget == null || !drivePhysicsWithRigidbody)
            return;

        // Rigidbody2D.MovePosition/MoveRotation are intended to be requested
        // from FixedUpdate so the collider participates in the same physics
        // step as the enemies that are avoiding it.
        ApplyTransformAnimationStep(Time.fixedDeltaTime, true);
    }

    private void LateUpdate()
    {
        if (!initialized || GetResolvedTarget() != activeTarget)
            InitializeAnimation();

        if (activeTarget == null)
            return;

        if (!drivePhysicsWithRigidbody)
        {
            float deltaTime = timeSource == AnimationTimeSource.Unscaled
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            ApplyTransformAnimationStep(deltaTime, false);
        }

        float blend = blendInDuration <= 0f
            ? 1f
            : Smooth01(elapsedTime / blendInDuration);

        UpdateColorPulse(blend);
    }

    private void ApplyTransformAnimationStep(
        float deltaTime,
        bool useRigidbody)
    {
        deltaTime = Mathf.Max(0f, deltaTime);
        elapsedTime += deltaTime;

        RemovePreviousTransformContribution(
            out Vector3 externalPosition,
            out Quaternion externalRotation,
            out Vector3 externalScale);

        float blend = blendInDuration <= 0f
            ? 1f
            : Smooth01(elapsedTime / blendInDuration);

        Vector3 positionOffset = CalculatePositionOffset(blend);
        Quaternion rotationOffset = CalculateRotationOffset(deltaTime, blend);

        // Scaling a Transform that owns Collider2D geometry also scales the
        // collider outside the physics solver. For physics-driven obstacles,
        // keep collider scale fixed; color/position/rotation polish remains.
        Vector3 scaleMultiplier = useRigidbody
            ? Vector3.one
            : CalculateScaleMultiplier(blend);

        Vector3 desiredLocalPosition = externalPosition + positionOffset;
        Quaternion desiredLocalRotation = externalRotation * rotationOffset;
        Vector3 desiredLocalScale = Vector3.Scale(
            externalScale,
            scaleMultiplier
        );

        if (useRigidbody && physicsBody != null)
        {
            Vector3 desiredWorldPosition =
                activeTarget.parent != null
                    ? activeTarget.parent.TransformPoint(desiredLocalPosition)
                    : desiredLocalPosition;

            Quaternion desiredWorldRotation =
                activeTarget.parent != null
                    ? activeTarget.parent.rotation * desiredLocalRotation
                    : desiredLocalRotation;

            float safeDeltaTime = Mathf.Max(deltaTime, 0.0001f);

            plannedLinearVelocity =
                ((Vector2)desiredWorldPosition - physicsBody.position) /
                safeDeltaTime;

            plannedAngularVelocity = Mathf.DeltaAngle(
                physicsBody.rotation,
                desiredWorldRotation.eulerAngles.z
            ) / safeDeltaTime;

            physicsBody.MovePosition(desiredWorldPosition);
            physicsBody.MoveRotation(desiredWorldRotation.eulerAngles.z);

            // Rigidbody2D controls position/rotation. Scale is intentionally
            // left at its unanimated value for collider stability.
            activeTarget.localScale = desiredLocalScale;
        }
        else
        {
            plannedLinearVelocity = Vector2.zero;
            plannedAngularVelocity = 0f;

            activeTarget.localPosition = desiredLocalPosition;
            activeTarget.localRotation = desiredLocalRotation;
            activeTarget.localScale = desiredLocalScale;
        }

        lastPositionOffset = positionOffset;
        lastRotationOffset = rotationOffset;
        lastScaleMultiplier = scaleMultiplier;
    }

    private void OnDisable()
    {
        RemoveAnimationContribution();
        plannedLinearVelocity = Vector2.zero;
        plannedAngularVelocity = 0f;
        initialized = false;
    }

    /// <summary>
    /// Returns this animation's intended Rigidbody2D motion for the current
    /// physics step. Local obstacle avoidance uses it as relative velocity
    /// instead of waiting for the obstacle to physically enter an enemy.
    /// </summary>
    public bool TryGetPhysicsMotion(
        Rigidbody2D queriedBody,
        out Vector2 linearVelocity,
        out float angularVelocity)
    {
        linearVelocity = Vector2.zero;
        angularVelocity = 0f;

        if (!initialized ||
            !drivePhysicsWithRigidbody ||
            physicsBody == null ||
            queriedBody == null ||
            queriedBody != physicsBody)
        {
            return false;
        }

        linearVelocity = plannedLinearVelocity;
        angularVelocity = plannedAngularVelocity;
        return true;
    }

    public void ApplySelectedPreset()
    {
        ApplyPresetValues(preset);
        lastAppliedPreset = preset;
        ClampValues();

        if (Application.isPlaying && isActiveAndEnabled)
            InitializeAnimation();
    }

    public void SetPreset(IdlePreset newPreset, bool applyImmediately = true)
    {
        preset = newPreset;

        if (applyImmediately)
            ApplySelectedPreset();
    }

    [ContextMenu("Apply Selected Preset")]
    private void ApplySelectedPresetFromContextMenu()
    {
        ApplySelectedPreset();
    }

    [ContextMenu("Recapture Base Pose")]
    public void RecaptureBasePose()
    {
        RemoveAnimationContribution();
        InitializeAnimation();
    }

    [ContextMenu("Restart Idle Animation")]
    public void RestartAnimation()
    {
        InitializeAnimation();
    }

    private void InitializeAnimation()
    {
        RemoveAnimationContribution();
        ResolveReferences();

        activeTarget = GetResolvedTarget();
        activeRenderer = GetResolvedRenderer();
        ConfigurePhysicsDrive();

        plannedLinearVelocity = Vector2.zero;
        plannedAngularVelocity = 0f;

        lastPositionOffset = Vector3.zero;
        lastRotationOffset = Quaternion.identity;
        lastScaleMultiplier = Vector3.one;

        elapsedTime = 0f;
        continuousRotation = 0f;

        rotationDirection = randomizeRotateDirection && Random01(101) < 0.5f
            ? -1f
            : 1f;

        rotationSpeedMultiplier = 1f + Mathf.Lerp(
            -rotateSpeedVariation,
            rotateSpeedVariation,
            Random01(102));

        hoverPhaseX = GetPhase(201);
        hoverPhaseY = GetPhase(202);
        scalePhase = GetPhase(203);
        colorPhase = GetPhase(204);
        swayPhase = GetPhase(205);

        driftSeedX = Random01(301) * 1000f;
        driftSeedY = Random01(302) * 1000f;
        shakeSeedX = Random01(401) * 1000f;
        shakeSeedY = Random01(402) * 1000f;

        if (activeRenderer != null)
        {
            baseColor = activeRenderer.color;
            lastAppliedColor = baseColor;
        }

        colorApplied = false;
        initialized = activeTarget != null;
    }

    private Vector3 CalculatePositionOffset(float blend)
    {
        Vector3 offset = Vector3.zero;

        if (hover)
        {
            float x = Mathf.Sin(
                elapsedTime * hoverSpeed * horizontalFrequencyMultiplier + hoverPhaseX) * hoverAmount.x;

            float y = Mathf.Sin(
                elapsedTime * hoverSpeed + hoverPhaseY) * hoverAmount.y;

            offset += new Vector3(x, y, 0f);
        }

        if (drift && driftAmount > 0f)
        {
            float sample = elapsedTime * driftSpeed;

            float x = (Mathf.PerlinNoise(driftSeedX + sample, driftSeedY) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(driftSeedX, driftSeedY + sample) - 0.5f) * 2f;

            offset += new Vector3(x, y, 0f) * driftAmount;
        }

        if (shake && shakeAmount > 0f)
        {
            float sample = elapsedTime * shakeSpeed;

            float x = (Mathf.PerlinNoise(shakeSeedX + sample, shakeSeedY) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(shakeSeedX, shakeSeedY + sample) - 0.5f) * 2f;

            offset += new Vector3(x, y, 0f) * shakeAmount;
        }

        return offset * blend;
    }

    private Quaternion CalculateRotationOffset(float deltaTime, float blend)
    {
        if (!rotate)
            return Quaternion.identity;

        float angle;

        if (rotationMotion == RotationMotion.Continuous)
        {
            continuousRotation +=
                rotateSpeed * rotationDirection * rotationSpeedMultiplier * deltaTime;

            continuousRotation = Mathf.Repeat(continuousRotation + 180f, 360f) - 180f;
            angle = continuousRotation;
        }
        else
        {
            angle = Mathf.Sin(elapsedTime * swaySpeed + swayPhase) * swayAngle;
        }

        return Quaternion.Euler(0f, 0f, angle * blend);
    }

    private Vector3 CalculateScaleMultiplier(float blend)
    {
        if (!pulseScale || pulseAmount <= 0f)
            return Vector3.one;

        float wave = Mathf.Sin(elapsedTime * pulseSpeed + scalePhase);
        float strength = wave * pulseAmount * blend;

        float x = Mathf.Max(0.01f, 1f + strength * pulseAxis.x);
        float y = Mathf.Max(0.01f, 1f + strength * pulseAxis.y);

        return new Vector3(x, y, 1f);
    }

    private void UpdateColorPulse(float blend)
    {
        if (activeRenderer == null)
            return;

        Color currentColor = activeRenderer.color;

        if (colorApplied && !Approximately(currentColor, lastAppliedColor))
            baseColor = currentColor;

        if (!pulseColor)
        {
            if (colorApplied && Approximately(currentColor, lastAppliedColor))
                activeRenderer.color = baseColor;

            colorApplied = false;
            return;
        }

        Color from = useOriginalColorAsColor1 ? baseColor : color1;
        float wave = (Mathf.Sin(elapsedTime * colorPulseSpeed + colorPhase) + 1f) * 0.5f;
        Color pulsedColor = Color.Lerp(from, color2, wave * blend);

        if (preserveOriginalAlpha)
            pulsedColor.a = baseColor.a;

        activeRenderer.color = pulsedColor;
        lastAppliedColor = pulsedColor;
        colorApplied = true;
    }

    private void RemovePreviousTransformContribution(
        out Vector3 externalPosition,
        out Quaternion externalRotation,
        out Vector3 externalScale)
    {
        externalPosition = activeTarget.localPosition - lastPositionOffset;
        externalRotation = activeTarget.localRotation * Quaternion.Inverse(lastRotationOffset);
        externalScale = Divide(activeTarget.localScale, lastScaleMultiplier);
    }

    private void RemoveAnimationContribution()
    {
        if (!initialized || activeTarget == null)
            return;

        RemovePreviousTransformContribution(
            out Vector3 externalPosition,
            out Quaternion externalRotation,
            out Vector3 externalScale);

        if (drivePhysicsWithRigidbody && physicsBody != null)
        {
            Vector3 worldPosition =
                activeTarget.parent != null
                    ? activeTarget.parent.TransformPoint(externalPosition)
                    : externalPosition;

            Quaternion worldRotation =
                activeTarget.parent != null
                    ? activeTarget.parent.rotation * externalRotation
                    : externalRotation;

            physicsBody.position = worldPosition;
            physicsBody.rotation = worldRotation.eulerAngles.z;
            activeTarget.localScale = externalScale;
        }
        else
        {
            activeTarget.localPosition = externalPosition;
            activeTarget.localRotation = externalRotation;
            activeTarget.localScale = externalScale;
        }

        if (activeRenderer != null && colorApplied && Approximately(activeRenderer.color, lastAppliedColor))
            activeRenderer.color = baseColor;

        lastPositionOffset = Vector3.zero;
        lastRotationOffset = Quaternion.identity;
        lastScaleMultiplier = Vector3.one;
        colorApplied = false;
    }

    private void ConfigurePhysicsDrive()
    {
        drivePhysicsWithRigidbody = false;
        physicsBody = null;

        if (!Application.isPlaying || activeTarget == null)
            return;

        Collider2D[] colliders =
            activeTarget.GetComponentsInChildren<Collider2D>(true);

        if (colliders == null || colliders.Length == 0)
            return;

        physicsBody = activeTarget.GetComponent<Rigidbody2D>();

        if (physicsBody == null)
            physicsBody = activeTarget.gameObject.AddComponent<Rigidbody2D>();

        // This object is animated explicitly by this script, so Kinematic is
        // the correct body type. Full contacts keep callbacks/query behaviour
        // available while its path stays completely script-controlled.
        physicsBody.bodyType = RigidbodyType2D.Kinematic;
        physicsBody.gravityScale = 0f;
        physicsBody.useFullKinematicContacts = true;
        physicsBody.simulated = true;
        physicsBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        physicsBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        physicsBody.linearVelocity = Vector2.zero;
        physicsBody.angularVelocity = 0f;

        drivePhysicsWithRigidbody = true;
    }

    private void ResolveReferences()
    {
        if (animationTarget == null)
            animationTarget = transform;

        if (spriteRenderer == null)
        {
            spriteRenderer = animationTarget.GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private Transform GetResolvedTarget()
    {
        return animationTarget != null ? animationTarget : transform;
    }

    private SpriteRenderer GetResolvedRenderer()
    {
        if (spriteRenderer != null)
            return spriteRenderer;

        Transform target = GetResolvedTarget();
        SpriteRenderer found = target != null ? target.GetComponent<SpriteRenderer>() : null;

        return found != null ? found : GetComponentInChildren<SpriteRenderer>(true);
    }

    private float GetPhase(int salt)
    {
        return randomizePhase ? Random01(salt) * Mathf.PI * 2f : 0f;
    }

    private float Random01(int salt)
    {
        unchecked
        {
            uint value = unchecked((uint)GetEntityId().GetHashCode());
            value ^= (uint)randomSeed * 747796405u;
            value ^= (uint)salt * 2891336453u;
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;

            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static Vector3 Divide(Vector3 value, Vector3 divisor)
    {
        return new Vector3(
            SafeDivide(value.x, divisor.x),
            SafeDivide(value.y, divisor.y),
            SafeDivide(value.z, divisor.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.00001f ? value / divisor : value;
    }

    private static float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    private static bool Approximately(Color a, Color b)
    {
        const float tolerance = 0.002f;

        return Mathf.Abs(a.r - b.r) <= tolerance &&
               Mathf.Abs(a.g - b.g) <= tolerance &&
               Mathf.Abs(a.b - b.b) <= tolerance &&
               Mathf.Abs(a.a - b.a) <= tolerance;
    }

    private void ApplyPresetValues(IdlePreset selectedPreset)
    {
        if (selectedPreset == IdlePreset.Custom)
            return;

        SetNeutralDefaults();

        switch (selectedPreset)
        {
            case IdlePreset.Asteroid:
                rotate = true;
                rotationMotion = RotationMotion.Continuous;
                rotateSpeed = 7f;
                rotateSpeedVariation = 0.35f;
                randomizeRotateDirection = true;
                hover = true;
                hoverAmount = new Vector2(0.018f, 0.025f);
                hoverSpeed = 0.55f;
                break;

            case IdlePreset.HeavyAsteroid:
                rotate = true;
                rotationMotion = RotationMotion.Continuous;
                rotateSpeed = 3.5f;
                rotateSpeedVariation = 0.25f;
                randomizeRotateDirection = true;
                hover = true;
                hoverAmount = new Vector2(0.012f, 0.018f);
                hoverSpeed = 0.4f;
                break;

            case IdlePreset.SharpDebris:
                rotate = true;
                rotationMotion = RotationMotion.Continuous;
                rotateSpeed = 10f;
                rotateSpeedVariation = 0.4f;
                randomizeRotateDirection = true;
                hover = true;
                hoverAmount = new Vector2(0.025f, 0.035f);
                hoverSpeed = 0.7f;
                drift = true;
                driftAmount = 0.012f;
                driftSpeed = 0.3f;
                break;

            case IdlePreset.Satellite:
                rotate = true;
                rotationMotion = RotationMotion.Sway;
                swayAngle = 4f;
                swaySpeed = 0.75f;
                hover = true;
                hoverAmount = new Vector2(0.035f, 0.055f);
                hoverSpeed = 0.75f;
                drift = true;
                driftAmount = 0.012f;
                driftSpeed = 0.28f;
                break;

            case IdlePreset.BrokenSatellite:
                rotate = true;
                rotationMotion = RotationMotion.Continuous;
                rotateSpeed = 6.5f;
                rotateSpeedVariation = 0.35f;
                randomizeRotateDirection = true;
                hover = true;
                hoverAmount = new Vector2(0.035f, 0.045f);
                hoverSpeed = 0.65f;
                drift = true;
                driftAmount = 0.018f;
                driftSpeed = 0.4f;
                shake = true;
                shakeAmount = 0.006f;
                shakeSpeed = 2.5f;
                break;

            case IdlePreset.Wreckage:
                rotate = true;
                rotationMotion = RotationMotion.Continuous;
                rotateSpeed = 8f;
                rotateSpeedVariation = 0.45f;
                randomizeRotateDirection = true;
                hover = true;
                hoverAmount = new Vector2(0.03f, 0.04f);
                hoverSpeed = 0.6f;
                drift = true;
                driftAmount = 0.02f;
                driftSpeed = 0.38f;
                break;

            case IdlePreset.EnergyObject:
                rotate = true;
                rotationMotion = RotationMotion.Continuous;
                rotateSpeed = 15f;
                rotateSpeedVariation = 0.15f;
                randomizeRotateDirection = true;
                pulseScale = true;
                pulseAmount = 0.045f;
                pulseSpeed = 2.2f;
                pulseAxis = Vector2.one;
                pulseColor = true;
                useOriginalColorAsColor1 = true;
                color2 = new Color(0.2f, 0.95f, 1f, 1f);
                colorPulseSpeed = 2.2f;
                break;

            case IdlePreset.Star:
                rotate = true;
                rotationMotion = RotationMotion.Continuous;
                rotateSpeed = 2.5f;
                rotateSpeedVariation = 0.2f;
                randomizeRotateDirection = true;
                pulseScale = true;
                pulseAmount = 0.035f;
                pulseSpeed = 1.5f;
                pulseAxis = Vector2.one;
                pulseColor = true;
                useOriginalColorAsColor1 = true;
                color2 = new Color(1f, 0.72f, 0.28f, 1f);
                colorPulseSpeed = 1.35f;
                break;

            case IdlePreset.BlackHole:
                rotate = true;
                rotationMotion = RotationMotion.Continuous;
                rotateSpeed = 20f;
                rotateSpeedVariation = 0.12f;
                randomizeRotateDirection = true;
                pulseScale = true;
                pulseAmount = 0.055f;
                pulseSpeed = 1.8f;
                pulseAxis = Vector2.one;
                pulseColor = true;
                useOriginalColorAsColor1 = true;
                color2 = new Color(0.72f, 0.18f, 1f, 1f);
                colorPulseSpeed = 1.7f;
                break;

            case IdlePreset.AlienOrganic:
                rotate = true;
                rotationMotion = RotationMotion.Sway;
                swayAngle = 3.5f;
                swaySpeed = 0.9f;
                hover = true;
                hoverAmount = new Vector2(0.025f, 0.04f);
                hoverSpeed = 0.8f;
                drift = true;
                driftAmount = 0.018f;
                driftSpeed = 0.45f;
                pulseScale = true;
                pulseAmount = 0.045f;
                pulseSpeed = 1.65f;
                pulseAxis = new Vector2(0.8f, 1f);
                shake = true;
                shakeAmount = 0.006f;
                shakeSpeed = 2.2f;
                break;

            case IdlePreset.TechWall:
                rotate = false;
                hover = false;
                pulseScale = true;
                pulseAmount = 0.012f;
                pulseSpeed = 2f;
                pulseAxis = new Vector2(0.15f, 1f);
                pulseColor = true;
                useOriginalColorAsColor1 = true;
                color2 = new Color(0.15f, 0.95f, 1f, 1f);
                colorPulseSpeed = 1.8f;
                break;

            case IdlePreset.Planet:
                rotate = true;
                rotationMotion = RotationMotion.Sway;
                swayAngle = 1.5f;
                swaySpeed = 0.35f;
                hover = true;
                hoverAmount = new Vector2(0.018f, 0.028f);
                hoverSpeed = 0.45f;
                break;
        }
    }

    private void SetNeutralDefaults()
    {
        rotate = false;
        rotationMotion = RotationMotion.Continuous;
        rotateSpeed = 10f;
        randomizeRotateDirection = false;
        rotateSpeedVariation = 0f;
        swayAngle = 5f;
        swaySpeed = 1f;

        hover = false;
        hoverAmount = new Vector2(0.03f, 0.05f);
        hoverSpeed = 1f;
        horizontalFrequencyMultiplier = 0.73f;

        drift = false;
        driftAmount = 0.02f;
        driftSpeed = 0.35f;

        pulseScale = false;
        pulseAmount = 0.08f;
        pulseSpeed = 2f;
        pulseAxis = Vector2.one;

        pulseColor = false;
        useOriginalColorAsColor1 = true;
        color1 = Color.white;
        color2 = Color.cyan;
        colorPulseSpeed = 2f;
        preserveOriginalAlpha = true;

        shake = false;
        shakeAmount = 0.05f;
        shakeSpeed = 20f;
    }

    private void OnValidate()
    {
        ResolveReferences();

        if (preset != IdlePreset.Custom && preset != lastAppliedPreset)
        {
            ApplyPresetValues(preset);
            lastAppliedPreset = preset;
        }
        else if (preset == IdlePreset.Custom)
        {
            lastAppliedPreset = IdlePreset.Custom;
        }

        ClampValues();
    }

    private void ClampValues()
    {
        blendInDuration = Mathf.Max(0f, blendInDuration);
        rotateSpeedVariation = Mathf.Clamp(rotateSpeedVariation, 0f, 0.9f);
        swayAngle = Mathf.Max(0f, swayAngle);
        swaySpeed = Mathf.Max(0f, swaySpeed);

        hoverAmount.x = Mathf.Max(0f, hoverAmount.x);
        hoverAmount.y = Mathf.Max(0f, hoverAmount.y);
        hoverSpeed = Mathf.Max(0f, hoverSpeed);
        horizontalFrequencyMultiplier = Mathf.Clamp(horizontalFrequencyMultiplier, 0.1f, 2f);

        driftAmount = Mathf.Max(0f, driftAmount);
        driftSpeed = Mathf.Max(0f, driftSpeed);

        pulseAmount = Mathf.Clamp(pulseAmount, 0f, 0.5f);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);
        pulseAxis.x = Mathf.Clamp(pulseAxis.x, -2f, 2f);
        pulseAxis.y = Mathf.Clamp(pulseAxis.y, -2f, 2f);

        colorPulseSpeed = Mathf.Max(0f, colorPulseSpeed);
        shakeAmount = Mathf.Max(0f, shakeAmount);
        shakeSpeed = Mathf.Max(0f, shakeSpeed);
    }
}
