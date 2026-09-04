using UnityEngine;

[DisallowMultipleComponent]
public class StarfieldController : MonoBehaviour
{
    public Color CurrentNearStarsColor { get; private set; } = Color.white;

    [Header("STAR LAYERS")]
    [SerializeField] private ParticleSystem farStars;
    [SerializeField] private ParticleSystem midStars;
    [SerializeField] private ParticleSystem nearStars;
    [SerializeField] private ParticleSystem sparkleStars;

    [Header("COLOR MIX")]
    [Tooltip("MidStars renginin level rengine ne kadar yaklaşacağı.")]
    [Range(0f, 1f)]
    [SerializeField] private float midColorInfluence = 0.25f;

    [Tooltip("SparkleStars renginin level rengine ne kadar yaklaşacağı.")]
    [Range(0f, 1f)]
    [SerializeField] private float sparkleColorInfluence = 0.12f;

    [Header("SAFE RUNTIME LIMITS")]
    [Tooltip("LevelConfig içindeki yoğunluk çarpanının güvenli alt sınırı.")]
    [Range(0.25f, 2f)]
    [SerializeField] private float minDensityMultiplier = 0.75f;

    [Tooltip("LevelConfig içindeki yoğunluk çarpanının güvenli üst sınırı.")]
    [Range(0.25f, 2f)]
    [SerializeField] private float maxDensityMultiplier = 1.25f;

    [Header("NEAR STARS SCREEN FLOW")]
    [Tooltip("NearStars sadece ekrana giriş yapan kenarlardan doğar ve karşı kenardan çıktıktan sonra silinir.")]
    [SerializeField] private bool useScreenEdgeNearStars = true;

    [Tooltip("Boş bırakılırsa Main Camera otomatik kullanılır.")]
    [SerializeField] private Camera nearStarsCamera;

    [Tooltip("Yeni NearStar üretim hızı. Eski Box/Volume sisteminden farklı olarak üretilen yıldızların neredeyse tamamı ekrandan geçer.")]
    [Min(0f)]
    [SerializeField] private float nearStarsBaseEmissionRate = 4.25f;

    [Tooltip("NearStars için güvenli üst particle limiti.")]
    [Min(1)]
    [SerializeField] private int nearStarsMaxParticles = 220;

    [Tooltip("Lifetime artık görünürlük süresini belirlemez; yıldız karşı kenardan çıkınca script tarafından silinir. Bu değer sadece güvenlik payıdır.")]
    [SerializeField] private Vector2 nearStarsLifetimeRange = new Vector2(90f, 120f);

    [Tooltip("Yıldızın ekranın biraz dışından doğması için dünya birimi cinsinden pay.")]
    [Min(0f)]
    [SerializeField] private float nearStarsSpawnPadding = 0.35f;

    [Tooltip("Yıldızın tamamen ekran dışına çıktıktan sonra silinmesi için dünya birimi cinsinden pay.")]
    [Min(0f)]
    [SerializeField] private float nearStarsExitPadding = 0.65f;

    [Tooltip("Scene açıldığında ekranın boş başlayıp dolmasını beklememek için ilk doluluk oranı.")]
    [Range(0f, 1f)]
    [SerializeField] private float nearStarsInitialFill = 0.45f;

    private LayerDefaults midDefaults;
    private LayerDefaults nearDefaults;
    private LayerDefaults sparkleDefaults;

    private bool defaultsCached;

    private float currentNearEmissionRate;
    private bool nearLevelSettingsApplied;
    private float currentNearSpeedMultiplier = 1f;
    private float nearEmissionAccumulator;
    private bool nearFlowInitialized;
    private bool nearStarsSuspended;
    private ParticleSystem.Particle[] nearParticleBuffer;

    private const float MaxNearStarsFrameDelta = 0.1f;

    private struct LayerDefaults
    {
        public ParticleSystem.MinMaxCurve startSize;
        public ParticleSystem.MinMaxCurve emissionRate;
        public ParticleSystem.MinMaxCurve velocityX;
        public ParticleSystem.MinMaxCurve velocityY;
        public ParticleSystem.MinMaxCurve velocityZ;
    }

    private struct CameraBounds2D
    {
        public float left;
        public float right;
        public float bottom;
        public float top;
        public float planeZ;
    }

    private void Awake()
    {
        ResolveLayerReferences();
        ConfigureNearStarsBaseSettings();
        CacheDefaults();

        if (!nearLevelSettingsApplied)
            currentNearEmissionRate = Mathf.Max(0f, nearStarsBaseEmissionRate);
    }

    private void Start()
    {
        InitializeNearStarsFlow();
    }

    private void Update()
    {
        if (!useScreenEdgeNearStars ||
            !nearFlowInitialized ||
            nearStarsSuspended ||
            nearStars == null)
        {
            return;
        }

        ResolveNearStarsCamera();
        if (nearStarsCamera == null)
            return;

        CullExitedNearStars();
        EmitNearStars(
            Mathf.Min(Time.deltaTime, MaxNearStarsFrameDelta)
        );
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ResumeNearStarsFlow();
        else
            SuspendNearStarsFlow();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            SuspendNearStarsFlow();
        else
            ResumeNearStarsFlow();
    }

    private void Reset()
    {
        ResolveLayerReferences();
    }

    public void ApplyLevel(LevelConfig level)
    {
        if (level == null)
            return;

        ResolveLayerReferences();
        ConfigureNearStarsBaseSettings();
        CacheDefaults();

        Color levelColor = level.randomizeNearStarsColor
            ? GenerateRandomStarColor()
            : ForceOpaque(level.nearStarsColor);

        // Store the exact color applied to gameplay NearStars so HUD elements
        // can match it one-to-one (including randomized test levels).
        CurrentNearStarsColor = levelColor;

        float speedMultiplier = Mathf.Max(0f, level.nearStarsSpeedMultiplier);
        float sizeMultiplier = Mathf.Max(0f, level.nearStarsSizeMultiplier);

        float densityMultiplier = level.starfieldDensityMultiplier;
        if (densityMultiplier <= 0f)
            densityMultiplier = 1f;

        densityMultiplier = Mathf.Clamp(
            densityMultiplier,
            Mathf.Min(minDensityMultiplier, maxDensityMultiplier),
            Mathf.Max(minDensityMultiplier, maxDensityMultiplier)
        );

        // FarStars is intentionally stable: white, static and burst-only.
        ApplyColor(farStars, Color.white);

        Color midColor = Color.Lerp(
            Color.white,
            levelColor,
            midColorInfluence
        );

        Color sparkleColor = Color.Lerp(
            Color.white,
            levelColor,
            sparkleColorInfluence
        );

        ApplyLayer(
            midStars,
            midDefaults,
            midColor,
            speedMultiplier,
            sizeMultiplier,
            densityMultiplier,
            applyVelocity: true,
            applyEmission: true
        );

        ApplyLayer(
            nearStars,
            nearDefaults,
            levelColor,
            speedMultiplier,
            sizeMultiplier,
            densityMultiplier,
            applyVelocity: true,
            applyEmission: !useScreenEdgeNearStars
        );

        if (useScreenEdgeNearStars)
        {
            currentNearEmissionRate =
                Mathf.Max(0f, GetRepresentativeCurveValue(nearDefaults.emissionRate)) *
                densityMultiplier;

            currentNearSpeedMultiplier = speedMultiplier;
            nearLevelSettingsApplied = true;
        }

        // Sparkles do not move. They only fade in/out through Color over Lifetime.
        ApplyLayer(
            sparkleStars,
            sparkleDefaults,
            sparkleColor,
            speedMultiplier: 1f,
            sizeMultiplier: Mathf.Lerp(1f, sizeMultiplier, 0.35f),
            densityMultiplier: densityMultiplier,
            applyVelocity: false,
            applyEmission: true
        );
    }

    private void ApplyLayer(
        ParticleSystem system,
        LayerDefaults defaults,
        Color color,
        float speedMultiplier,
        float sizeMultiplier,
        float densityMultiplier,
        bool applyVelocity,
        bool applyEmission)
    {
        if (system == null)
            return;

        ParticleSystem.MainModule main = system.main;
        main.startColor = ForceOpaque(color);
        main.startSize = ScaleCurve(defaults.startSize, sizeMultiplier);

        if (applyVelocity)
        {
            ParticleSystem.VelocityOverLifetimeModule velocity =
                system.velocityOverLifetime;

            velocity.x = ScaleCurve(defaults.velocityX, speedMultiplier);
            velocity.y = ScaleCurve(defaults.velocityY, speedMultiplier);
            velocity.z = ScaleCurve(defaults.velocityZ, speedMultiplier);
        }

        if (applyEmission)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = ScaleCurve(
                defaults.emissionRate,
                densityMultiplier
            );
        }

        ApplyToExistingParticles(
            system,
            ForceOpaque(color),
            sizeMultiplier
        );
    }

    private void ApplyColor(ParticleSystem system, Color color)
    {
        if (system == null)
            return;

        ParticleSystem.MainModule main = system.main;
        main.startColor = ForceOpaque(color);
        ApplyColorToExistingParticles(system, ForceOpaque(color));
    }

    private void ConfigureNearStarsBaseSettings()
    {
        if (!useScreenEdgeNearStars || nearStars == null)
            return;

        float minLifetime = Mathf.Max(1f, Mathf.Min(
            nearStarsLifetimeRange.x,
            nearStarsLifetimeRange.y
        ));

        float maxLifetime = Mathf.Max(minLifetime, Mathf.Max(
            nearStarsLifetimeRange.x,
            nearStarsLifetimeRange.y
        ));

        ParticleSystem.MainModule main = nearStars.main;
        main.maxParticles = Mathf.Max(1, nearStarsMaxParticles);
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            minLifetime,
            maxLifetime
        );
        main.prewarm = false;

        ParticleSystem.EmissionModule emission = nearStars.emission;
        emission.rateOverTime = Mathf.Max(0f, nearStarsBaseEmissionRate);
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = nearStars.shape;
        shape.enabled = false;

    }

    private void InitializeNearStarsFlow()
    {
        if (!useScreenEdgeNearStars || nearStars == null)
            return;

        ResolveNearStarsCamera();
        if (nearStarsCamera == null)
            return;

        ConfigureNearStarsBaseSettings();

        nearStars.Clear(true);
        if (!nearStars.isPlaying)
            nearStars.Play(true);

        EnsureNearParticleBuffer();
        SeedInitialNearStars();

        nearEmissionAccumulator = 0f;
        nearFlowInitialized = true;
    }

    private void SuspendNearStarsFlow()
    {
        if (!useScreenEdgeNearStars ||
            nearStars == null ||
            nearStarsSuspended)
        {
            return;
        }

        nearStarsSuspended = true;
        nearEmissionAccumulator = 0f;

        if (nearStars.isPlaying)
            nearStars.Pause(true);
    }

    private void ResumeNearStarsFlow()
    {
        if (!nearStarsSuspended)
            return;

        nearStarsSuspended = false;
        nearEmissionAccumulator = 0f;

        if (!useScreenEdgeNearStars || nearStars == null)
            return;

        ResolveNearStarsCamera();
        if (nearStarsCamera == null)
            return;

        if (!nearFlowInitialized)
        {
            InitializeNearStarsFlow();
            return;
        }

        // Do not let a long OS/app suspension collapse many particles onto
        // the same edge on the first resumed frame. Rebuild an already-spread
        // field instead.
        nearStars.Clear(true);
        nearStars.Play(true);
        EnsureNearParticleBuffer();
        SeedInitialNearStars();
    }

    private void ResolveNearStarsCamera()
    {
        if (nearStarsCamera == null)
            nearStarsCamera = Camera.main;
    }

    private void EmitNearStars(float deltaTime)
    {
        if (deltaTime <= 0f || currentNearEmissionRate <= 0f)
            return;

        nearEmissionAccumulator += currentNearEmissionRate * deltaTime;

        int emitCount = Mathf.FloorToInt(nearEmissionAccumulator);
        if (emitCount <= 0)
            return;

        nearEmissionAccumulator -= emitCount;

        int availableSlots = Mathf.Max(
            0,
            nearStars.main.maxParticles - nearStars.particleCount
        );

        emitCount = Mathf.Min(emitCount, availableSlots);
        emitCount = Mathf.Min(emitCount, 32);

        if (emitCount <= 0)
            return;

        CameraBounds2D bounds = GetCameraBounds();
        Vector2 flow = GetNearStarsWorldFlow();

        for (int i = 0; i < emitCount; i++)
        {
            Vector3 worldPosition = GetRandomEntryPosition(bounds, flow);
            EmitNearStarAtWorldPosition(worldPosition);
        }
    }

    private void SeedInitialNearStars()
    {
        int targetCount = Mathf.RoundToInt(
            nearStars.main.maxParticles * nearStarsInitialFill
        );

        if (targetCount <= 0)
            return;

        CameraBounds2D bounds = GetCameraBounds();

        for (int i = 0; i < targetCount; i++)
        {
            Vector3 worldPosition = new Vector3(
                Random.Range(bounds.left, bounds.right),
                Random.Range(bounds.bottom, bounds.top),
                bounds.planeZ
            );

            EmitNearStarAtWorldPosition(worldPosition);
        }
    }

    private void EmitNearStarAtWorldPosition(Vector3 worldPosition)
    {
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = WorldToSimulationPosition(worldPosition),
            applyShapeToPosition = false
        };

        nearStars.Emit(emitParams, 1);
    }

    private Vector3 GetRandomEntryPosition(
        CameraBounds2D bounds,
        Vector2 flow)
    {
        bool hasHorizontalFlow = Mathf.Abs(flow.x) > 0.0001f;
        bool hasVerticalFlow = Mathf.Abs(flow.y) > 0.0001f;

        if (!hasHorizontalFlow && !hasVerticalFlow)
        {
            return new Vector3(
                Random.Range(bounds.left, bounds.right),
                bounds.top + nearStarsSpawnPadding,
                bounds.planeZ
            );
        }

        bool useVerticalEdge;

        if (!hasHorizontalFlow)
        {
            useVerticalEdge = true;
        }
        else if (!hasVerticalFlow)
        {
            useVerticalEdge = false;
        }
        else
        {
            float width = Mathf.Max(0.01f, bounds.right - bounds.left);
            float height = Mathf.Max(0.01f, bounds.top - bounds.bottom);

            float verticalWeight = width * Mathf.Abs(flow.y);
            float horizontalWeight = height * Mathf.Abs(flow.x);
            float totalWeight = verticalWeight + horizontalWeight;

            useVerticalEdge =
                Random.value < verticalWeight / Mathf.Max(0.0001f, totalWeight);
        }

        if (useVerticalEdge)
        {
            float y = flow.y < 0f
                ? bounds.top + nearStarsSpawnPadding
                : bounds.bottom - nearStarsSpawnPadding;

            return new Vector3(
                Random.Range(bounds.left, bounds.right),
                y,
                bounds.planeZ
            );
        }

        float x = flow.x > 0f
            ? bounds.left - nearStarsSpawnPadding
            : bounds.right + nearStarsSpawnPadding;

        return new Vector3(
            x,
            Random.Range(bounds.bottom, bounds.top),
            bounds.planeZ
        );
    }

    private void CullExitedNearStars()
    {
        EnsureNearParticleBuffer();

        int count = nearStars.GetParticles(nearParticleBuffer);
        if (count <= 0)
            return;

        CameraBounds2D bounds = GetCameraBounds();
        Vector2 flow = GetNearStarsWorldFlow();
        bool changed = false;

        for (int i = 0; i < count; i++)
        {
            Vector3 worldPosition = SimulationToWorldPosition(
                nearParticleBuffer[i].position
            );

            bool exited = false;

            if (flow.x > 0.0001f &&
                worldPosition.x > bounds.right + nearStarsExitPadding)
            {
                exited = true;
            }
            else if (flow.x < -0.0001f &&
                     worldPosition.x < bounds.left - nearStarsExitPadding)
            {
                exited = true;
            }

            if (flow.y < -0.0001f &&
                worldPosition.y < bounds.bottom - nearStarsExitPadding)
            {
                exited = true;
            }
            else if (flow.y > 0.0001f &&
                     worldPosition.y > bounds.top + nearStarsExitPadding)
            {
                exited = true;
            }

            if (!exited)
                continue;

            nearParticleBuffer[i].remainingLifetime = 0f;
            changed = true;
        }

        if (changed)
            nearStars.SetParticles(nearParticleBuffer, count);
    }

    private CameraBounds2D GetCameraBounds()
    {
        float planeZ = nearStars != null
            ? nearStars.transform.position.z
            : 0f;

        float depth = Mathf.Abs(
            planeZ - nearStarsCamera.transform.position.z
        );

        Vector3 bottomLeft = nearStarsCamera.ViewportToWorldPoint(
            new Vector3(0f, 0f, depth)
        );

        Vector3 topRight = nearStarsCamera.ViewportToWorldPoint(
            new Vector3(1f, 1f, depth)
        );

        return new CameraBounds2D
        {
            left = Mathf.Min(bottomLeft.x, topRight.x),
            right = Mathf.Max(bottomLeft.x, topRight.x),
            bottom = Mathf.Min(bottomLeft.y, topRight.y),
            top = Mathf.Max(bottomLeft.y, topRight.y),
            planeZ = planeZ
        };
    }

    private Vector2 GetNearStarsWorldFlow()
    {
        Vector3 flow = new Vector3(
            GetRepresentativeCurveValue(nearDefaults.velocityX),
            GetRepresentativeCurveValue(nearDefaults.velocityY),
            GetRepresentativeCurveValue(nearDefaults.velocityZ)
        ) * currentNearSpeedMultiplier;

        if (nearStars != null)
        {
            ParticleSystem.VelocityOverLifetimeModule velocity =
                nearStars.velocityOverLifetime;

            if (velocity.space == ParticleSystemSimulationSpace.Local)
                flow = nearStars.transform.TransformVector(flow);
            else if (velocity.space == ParticleSystemSimulationSpace.Custom)
            {
                ParticleSystem.MainModule main = nearStars.main;
                if (main.customSimulationSpace != null)
                    flow = main.customSimulationSpace.TransformVector(flow);
            }
        }

        return new Vector2(flow.x, flow.y);
    }

    private Vector3 WorldToSimulationPosition(Vector3 worldPosition)
    {
        ParticleSystem.MainModule main = nearStars.main;

        switch (main.simulationSpace)
        {
            case ParticleSystemSimulationSpace.Local:
                return nearStars.transform.InverseTransformPoint(worldPosition);

            case ParticleSystemSimulationSpace.Custom:
                if (main.customSimulationSpace != null)
                    return main.customSimulationSpace.InverseTransformPoint(worldPosition);
                return worldPosition;

            default:
                return worldPosition;
        }
    }

    private Vector3 SimulationToWorldPosition(Vector3 simulationPosition)
    {
        ParticleSystem.MainModule main = nearStars.main;

        switch (main.simulationSpace)
        {
            case ParticleSystemSimulationSpace.Local:
                return nearStars.transform.TransformPoint(simulationPosition);

            case ParticleSystemSimulationSpace.Custom:
                if (main.customSimulationSpace != null)
                    return main.customSimulationSpace.TransformPoint(simulationPosition);
                return simulationPosition;

            default:
                return simulationPosition;
        }
    }

    private void EnsureNearParticleBuffer()
    {
        int requiredSize = Mathf.Max(1, nearStars.main.maxParticles);

        if (nearParticleBuffer == null || nearParticleBuffer.Length < requiredSize)
            nearParticleBuffer = new ParticleSystem.Particle[requiredSize];
    }

    private void CacheDefaults()
    {
        if (defaultsCached)
            return;

        midDefaults = CaptureDefaults(midStars);
        nearDefaults = CaptureDefaults(nearStars);
        sparkleDefaults = CaptureDefaults(sparkleStars);

        defaultsCached = true;
    }

    private static LayerDefaults CaptureDefaults(ParticleSystem system)
    {
        if (system == null)
            return default;

        ParticleSystem.MainModule main = system.main;
        ParticleSystem.EmissionModule emission = system.emission;
        ParticleSystem.VelocityOverLifetimeModule velocity =
            system.velocityOverLifetime;

        return new LayerDefaults
        {
            startSize = main.startSize,
            emissionRate = emission.rateOverTime,
            velocityX = velocity.x,
            velocityY = velocity.y,
            velocityZ = velocity.z
        };
    }

    private void ResolveLayerReferences()
    {
        if (farStars != null &&
            midStars != null &&
            nearStars != null &&
            sparkleStars != null)
        {
            return;
        }

        ParticleSystem[] systems =
            GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];

            switch (system.gameObject.name)
            {
                case "FarStars":
                    if (farStars == null)
                        farStars = system;
                    break;

                case "MidStars":
                    if (midStars == null)
                        midStars = system;
                    break;

                case "NearStars":
                    if (nearStars == null)
                        nearStars = system;
                    break;

                case "SparkleStars":
                    if (sparkleStars == null)
                        sparkleStars = system;
                    break;
            }
        }
    }

    private static void ApplyColorToExistingParticles(
        ParticleSystem system,
        Color color)
    {
        if (system == null)
            return;

        int maxParticles = system.main.maxParticles;
        if (maxParticles <= 0)
            return;

        ParticleSystem.Particle[] particles =
            new ParticleSystem.Particle[maxParticles];

        int particleCount = system.GetParticles(particles);

        for (int i = 0; i < particleCount; i++)
            particles[i].startColor = color;

        if (particleCount > 0)
            system.SetParticles(particles, particleCount);
    }

    private static void ApplyToExistingParticles(
        ParticleSystem system,
        Color color,
        float sizeMultiplier)
    {
        if (system == null)
            return;

        int maxParticles = system.main.maxParticles;
        if (maxParticles <= 0)
            return;

        ParticleSystem.Particle[] particles =
            new ParticleSystem.Particle[maxParticles];

        int particleCount = system.GetParticles(particles);

        for (int i = 0; i < particleCount; i++)
        {
            particles[i].startColor = color;
            particles[i].startSize *= sizeMultiplier;
        }

        if (particleCount > 0)
            system.SetParticles(particles, particleCount);
    }

    private static ParticleSystem.MinMaxCurve ScaleCurve(
        ParticleSystem.MinMaxCurve source,
        float multiplier)
    {
        multiplier = Mathf.Max(0f, multiplier);

        switch (source.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return new ParticleSystem.MinMaxCurve(
                    source.constant * multiplier
                );

            case ParticleSystemCurveMode.TwoConstants:
                return new ParticleSystem.MinMaxCurve(
                    source.constantMin * multiplier,
                    source.constantMax * multiplier
                );

            case ParticleSystemCurveMode.Curve:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier * multiplier,
                    source.curve
                );

            case ParticleSystemCurveMode.TwoCurves:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier * multiplier,
                    source.curveMin,
                    source.curveMax
                );

            default:
                return source;
        }
    }

    private static float GetRepresentativeCurveValue(
        ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;

            case ParticleSystemCurveMode.TwoConstants:
                return (curve.constantMin + curve.constantMax) * 0.5f;

            case ParticleSystemCurveMode.Curve:
                return curve.curve != null
                    ? curve.curve.Evaluate(0.5f) * curve.curveMultiplier
                    : 0f;

            case ParticleSystemCurveMode.TwoCurves:
                float minValue = curve.curveMin != null
                    ? curve.curveMin.Evaluate(0.5f)
                    : 0f;

                float maxValue = curve.curveMax != null
                    ? curve.curveMax.Evaluate(0.5f)
                    : 0f;

                return (minValue + maxValue) * 0.5f * curve.curveMultiplier;

            default:
                return 0f;
        }
    }

    private static Color ForceOpaque(Color color)
    {
        color.a = 1f;
        return color;
    }

    private static Color GenerateRandomStarColor()
    {
        Color color = Random.ColorHSV(
            0f,
            1f,
            0.65f,
            1f,
            0.8f,
            1f,
            1f,
            1f
        );

        color.a = 1f;
        return color;
    }

    private void OnValidate()
    {
        nearStarsBaseEmissionRate = Mathf.Max(0f, nearStarsBaseEmissionRate);
        nearStarsMaxParticles = Mathf.Max(1, nearStarsMaxParticles);
        nearStarsSpawnPadding = Mathf.Max(0f, nearStarsSpawnPadding);
        nearStarsExitPadding = Mathf.Max(0f, nearStarsExitPadding);

        float minLifetime = Mathf.Max(1f, Mathf.Min(
            nearStarsLifetimeRange.x,
            nearStarsLifetimeRange.y
        ));

        float maxLifetime = Mathf.Max(minLifetime, Mathf.Max(
            nearStarsLifetimeRange.x,
            nearStarsLifetimeRange.y
        ));

        nearStarsLifetimeRange = new Vector2(minLifetime, maxLifetime);
    }
}