using System.Collections;
using UnityEngine;

public class MainMenuStarColorRandomizer : MonoBehaviour
{
    public static MainMenuStarColorRandomizer Instance
    {
        get;
        private set;
    }

    /// <summary>
    /// The exact color currently being used by NearStars.
    /// Alpha is included; UI callers can override alpha if needed.
    /// </summary>
    public Color CurrentColor => currentColor;

    [Header("Reference")]
    [SerializeField]
    private ParticleSystem nearStars;

    [Tooltip("Boş bırakılırsa Main Camera otomatik kullanılır.")]
    [SerializeField]
    private Camera nearStarsCamera;

    [Header("Skin Theme NearStars")]
    [Tooltip("NearStars use the selected player's UI accent color on every Main Menu panel.")]
    [SerializeField, Range(0f, 1f)]
    private float skinThemeAlpha = 0.9f;

    [Header("Base Panel Density")]
    [SerializeField, Min(0f)]
    private float basePanelEmissionRate = 1.5f;

    [SerializeField, Min(1)]
    private int basePanelMaxParticles = 50;

    [Header("Level Page Progression")]
    [Tooltip("Screen-edge sisteminde eski Volume emission değerlerinden daha düşük tutulmalı.")]
    [SerializeField, Min(0f)]
    private float firstPageEmissionRate = 4.25f;

    [Tooltip("Son level sayfasına doğru NearStars yoğunluğu artar.")]
    [SerializeField, Min(0f)]
    private float lastPageEmissionRate = 8.5f;

    [SerializeField, Min(1)]
    private int firstPageMaxParticles = 140;

    [SerializeField, Min(1)]
    private int lastPageMaxParticles = 260;

    [SerializeField, Min(0f)]
    private float firstPageFlowMultiplier = 0.7f;

    [SerializeField, Min(0f)]
    private float lastPageFlowMultiplier = 1.75f;

    [Header("Screen Edge Near Stars")]
    [Tooltip("NearStars ekrana giriş yapan kenarlardan doğar ve karşı kenardan tamamen çıktıktan sonra silinir.")]
    [SerializeField]
    private bool useScreenEdgeNearStars = true;

    [Tooltip("Lifetime görünürlük süresini belirlemez; yıldız karşı kenardan çıkınca script siler. Bu sadece güvenlik payıdır.")]
    [SerializeField]
    private Vector2 nearStarsLifetimeRange =
        new Vector2(90f, 120f);

    [Tooltip("Yıldızların ekranın biraz dışından doğması için dünya birimi cinsinden pay.")]
    [SerializeField, Min(0f)]
    private float nearStarsSpawnPadding = 0.35f;

    [Tooltip("Yıldız tamamen ekran dışına çıktıktan sonra silinmesi için pay.")]
    [SerializeField, Min(0f)]
    private float nearStarsExitPadding = 0.65f;

    [Tooltip("Main Menu açıldığında NearStars'ın hemen dolu görünmesi için başlangıç doluluk oranı.")]
    [SerializeField, Range(0f, 1f)]
    private float nearStarsInitialFill = 0.65f;

    [Header("Transition")]
    [SerializeField, Min(0.01f)]
    private float transitionDuration = 0.45f;

    [SerializeField, HideInInspector]
    private int screenEdgeSettingsVersion;

    private ParticleSystem.Particle[] particles;
    private Coroutine transitionRoutine;

    private Color currentColor;
    private float currentEmissionRate;
    private float currentMaxParticles;
    private float currentFlowMultiplier = 1f;

    private float originalEmissionRate;
    private int originalMaxParticles;
    private ParticleSystem.MinMaxCurve originalVelocityX;
    private ParticleSystem.MinMaxCurve originalVelocityY;
    private ParticleSystem.MinMaxCurve originalVelocityZ;

    private bool skinPreviewActive;
    private float skinPreviewRestoreEmissionRate;
    private float skinPreviewRestoreMaxParticles;
    private float skinPreviewRestoreFlowMultiplier;

    private float nearEmissionAccumulator;
    private bool nearFlowInitialized;
    private bool nearStarsSuspended;

    private const float MaxNearStarsFrameDelta = 0.1f;

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
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        PlayerSkinCatalog.SelectedSkinChanged -=
            HandleSelectedSkinChanged;
        PlayerSkinCatalog.SelectedSkinChanged +=
            HandleSelectedSkinChanged;

        if (nearStars == null)
            nearStars = GetComponent<ParticleSystem>();

        MigrateLegacySettingsIfNeeded();
        CacheOriginalParticleSettings();
        ConfigureScreenEdgeFlow();

        currentColor = GetSelectedSkinThemeColor();
        currentEmissionRate = basePanelEmissionRate;
        currentMaxParticles = basePanelMaxParticles;
        currentFlowMultiplier = 1f;

        ApplyStateInstant(
            currentColor,
            currentEmissionRate,
            Mathf.RoundToInt(currentMaxParticles),
            currentFlowMultiplier
        );
    }

    private void Start()
    {
        InitializeScreenEdgeFlow();
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

        ResolveCamera();

        if (nearStarsCamera == null)
            return;

        CullExitedNearStars();
        EmitNearStars(
            Mathf.Min(Time.unscaledDeltaTime, MaxNearStarsFrameDelta)
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

    private void OnDestroy()
    {
        PlayerSkinCatalog.SelectedSkinChanged -=
            HandleSelectedSkinChanged;

        if (Instance == this)
            Instance = null;
    }

    private void HandleSelectedSkinChanged()
    {
        skinPreviewActive = false;

        ChangeState(
            GetSelectedSkinThemeColor(),
            currentEmissionRate,
            Mathf.RoundToInt(currentMaxParticles),
            currentFlowMultiplier
        );
    }

    public void ShowMainMenuColor()
    {
        ChangeState(
            GetSelectedSkinThemeColor(),
            basePanelEmissionRate,
            basePanelMaxParticles,
            1f
        );
    }

    public void ShowLevelSelectionColor()
    {
        ShowLevelSelectionPage(0, 1);
    }

    public void ShowLevelSelectionPage(
        int pageIndex,
        int totalPageCount
    )
    {
        int safePageCount =
            Mathf.Max(1, totalPageCount);

        int safePageIndex =
            Mathf.Clamp(
                pageIndex,
                0,
                safePageCount - 1
            );

        float progress =
            safePageCount <= 1
                ? 0f
                : safePageIndex /
                  (float)(safePageCount - 1);

        Color targetColor =
            GetSelectedSkinThemeColor();

        float targetEmissionRate =
            Mathf.Lerp(
                firstPageEmissionRate,
                lastPageEmissionRate,
                progress
            );

        int targetMaxParticles =
            Mathf.RoundToInt(
                Mathf.Lerp(
                    firstPageMaxParticles,
                    lastPageMaxParticles,
                    progress
                )
            );

        float targetFlowMultiplier =
            Mathf.Lerp(
                firstPageFlowMultiplier,
                lastPageFlowMultiplier,
                progress
            );

        ChangeState(
            targetColor,
            targetEmissionRate,
            targetMaxParticles,
            targetFlowMultiplier
        );
    }

    public void ShowMissionBriefingColor()
    {
        ChangeState(
            GetSelectedSkinThemeColor(),
            basePanelEmissionRate,
            basePanelMaxParticles,
            1f
        );
    }

    public void ShowOptionsColor()
    {
        ChangeState(
            GetSelectedSkinThemeColor(),
            basePanelEmissionRate,
            basePanelMaxParticles,
            1f
        );
    }

    public void ShowStatsColor()
    {
        ChangeState(
            GetSelectedSkinThemeColor(),
            basePanelEmissionRate,
            basePanelMaxParticles,
            1f
        );
    }

    public void BeginSkinPreview()
    {
        if (nearStars == null || skinPreviewActive)
            return;

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        skinPreviewRestoreEmissionRate = currentEmissionRate;
        skinPreviewRestoreMaxParticles = currentMaxParticles;
        skinPreviewRestoreFlowMultiplier = currentFlowMultiplier;
        skinPreviewActive = true;
    }

    public void ShowSkinPreviewSkin(
        PlayerSkinCatalog.SkinEntry skin)
    {
        if (skin == null)
            return;

        ShowSkinPreviewColor(
            GetSkinThemeColor(skin)
        );
    }

    public void ShowSkinPreviewColor(Color skinColor)
    {
        if (nearStars == null)
            return;

        if (!skinPreviewActive)
            BeginSkinPreview();

        ChangeState(
            skinColor,
            currentEmissionRate,
            Mathf.RoundToInt(currentMaxParticles),
            currentFlowMultiplier
        );
    }

    public void EndSkinPreview()
    {
        if (!skinPreviewActive)
            return;

        skinPreviewActive = false;

        ChangeState(
            GetSelectedSkinThemeColor(),
            skinPreviewRestoreEmissionRate,
            Mathf.RoundToInt(skinPreviewRestoreMaxParticles),
            skinPreviewRestoreFlowMultiplier
        );
    }

    private void ChangeState(
        Color targetColor,
        float targetEmissionRate,
        int targetMaxParticles,
        float targetFlowMultiplier
    )
    {
        if (nearStars == null)
            return;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(
            StateTransitionRoutine(
                targetColor,
                Mathf.Max(0f, targetEmissionRate),
                Mathf.Max(1, targetMaxParticles),
                Mathf.Max(0f, targetFlowMultiplier)
            )
        );
    }

    private IEnumerator StateTransitionRoutine(
        Color targetColor,
        float targetEmissionRate,
        int targetMaxParticles,
        float targetFlowMultiplier
    )
    {
        Color startColor = currentColor;
        float startEmissionRate = currentEmissionRate;
        float startMaxParticles = currentMaxParticles;
        float startFlowMultiplier = currentFlowMultiplier;

        float timer = 0f;

        while (timer < transitionDuration)
        {
            timer += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                timer / transitionDuration
            );

            float easedProgress =
                EaseInOutCubic(progress);

            currentColor = Color.Lerp(
                startColor,
                targetColor,
                easedProgress
            );

            currentEmissionRate = Mathf.Lerp(
                startEmissionRate,
                targetEmissionRate,
                easedProgress
            );

            currentMaxParticles = Mathf.Lerp(
                startMaxParticles,
                targetMaxParticles,
                easedProgress
            );

            currentFlowMultiplier = Mathf.Lerp(
                startFlowMultiplier,
                targetFlowMultiplier,
                easedProgress
            );

            ApplyStateInstant(
                currentColor,
                currentEmissionRate,
                Mathf.RoundToInt(currentMaxParticles),
                currentFlowMultiplier
            );

            yield return null;
        }

        currentColor = targetColor;
        currentEmissionRate = targetEmissionRate;
        currentMaxParticles = targetMaxParticles;
        currentFlowMultiplier = targetFlowMultiplier;

        ApplyStateInstant(
            currentColor,
            currentEmissionRate,
            targetMaxParticles,
            currentFlowMultiplier
        );

        transitionRoutine = null;
    }

    private void ApplyStateInstant(
        Color color,
        float emissionRate,
        int maxParticles,
        float flowMultiplier
    )
    {
        if (nearStars == null)
            return;

        ParticleSystem.MainModule main =
            nearStars.main;

        main.startColor =
            new ParticleSystem.MinMaxGradient(color);

        main.maxParticles =
            Mathf.Max(1, maxParticles);

        ParticleSystem.EmissionModule emission =
            nearStars.emission;

        if (useScreenEdgeNearStars)
        {
            emission.enabled = false;
        }
        else
        {
            emission.enabled = true;
            emission.rateOverTime =
                Mathf.Max(0f, emissionRate);
        }

        ParticleSystem.VelocityOverLifetimeModule velocity =
            nearStars.velocityOverLifetime;

        velocity.x = ScaleCurve(
            originalVelocityX,
            flowMultiplier
        );

        velocity.y = ScaleCurve(
            originalVelocityY,
            flowMultiplier
        );

        velocity.z = ScaleCurve(
            originalVelocityZ,
            flowMultiplier
        );

        ApplyColorToLivingParticles(
            color,
            main.maxParticles
        );
    }

    private void ConfigureScreenEdgeFlow()
    {
        if (!useScreenEdgeNearStars || nearStars == null)
            return;

        float minLifetime = Mathf.Max(
            1f,
            Mathf.Min(
                nearStarsLifetimeRange.x,
                nearStarsLifetimeRange.y
            )
        );

        float maxLifetime = Mathf.Max(
            minLifetime,
            Mathf.Max(
                nearStarsLifetimeRange.x,
                nearStarsLifetimeRange.y
            )
        );

        ParticleSystem.MainModule main =
            nearStars.main;

        main.startLifetime =
            new ParticleSystem.MinMaxCurve(
                minLifetime,
                maxLifetime
            );

        main.prewarm = false;

        ParticleSystem.EmissionModule emission =
            nearStars.emission;

        emission.enabled = false;

        ParticleSystem.ShapeModule shape =
            nearStars.shape;

        shape.enabled = false;
    }

    private void InitializeScreenEdgeFlow()
    {
        if (!useScreenEdgeNearStars || nearStars == null)
            return;

        ResolveCamera();

        if (nearStarsCamera == null)
            return;

        ConfigureScreenEdgeFlow();

        nearStars.Clear(true);

        if (!nearStars.isPlaying)
            nearStars.Play(true);

        EnsureParticleBuffer();
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

        ResolveCamera();
        if (nearStarsCamera == null)
            return;

        if (!nearFlowInitialized)
        {
            InitializeScreenEdgeFlow();
            return;
        }

        // Returning from Alt-Tab/Home can produce a very large first-frame
        // unscaled delta. Re-seeding avoids an edge burst / stacked particles.
        nearStars.Clear(true);
        nearStars.Play(true);
        EnsureParticleBuffer();
        SeedInitialNearStars();
    }

    private void ResolveCamera()
    {
        if (nearStarsCamera == null)
            nearStarsCamera = Camera.main;
    }

    private void EmitNearStars(float deltaTime)
    {
        if (deltaTime <= 0f ||
            currentEmissionRate <= 0f)
        {
            return;
        }

        nearEmissionAccumulator +=
            currentEmissionRate * deltaTime;

        int emitCount =
            Mathf.FloorToInt(nearEmissionAccumulator);

        if (emitCount <= 0)
            return;

        nearEmissionAccumulator -= emitCount;

        int availableSlots = Mathf.Max(
            0,
            nearStars.main.maxParticles -
            nearStars.particleCount
        );

        emitCount =
            Mathf.Min(emitCount, availableSlots);

        emitCount =
            Mathf.Min(emitCount, 32);

        if (emitCount <= 0)
            return;

        CameraBounds2D bounds =
            GetCameraBounds();

        Vector2 flow =
            GetNearStarsWorldFlow();

        for (int i = 0; i < emitCount; i++)
        {
            Vector3 worldPosition =
                GetRandomEntryPosition(
                    bounds,
                    flow
                );

            EmitNearStarAtWorldPosition(
                worldPosition
            );
        }
    }

    private void SeedInitialNearStars()
    {
        int targetCount =
            Mathf.RoundToInt(
                nearStars.main.maxParticles *
                nearStarsInitialFill
            );

        if (targetCount <= 0)
            return;

        CameraBounds2D bounds =
            GetCameraBounds();

        for (int i = 0; i < targetCount; i++)
        {
            Vector3 worldPosition =
                new Vector3(
                    Random.Range(
                        bounds.left,
                        bounds.right
                    ),
                    Random.Range(
                        bounds.bottom,
                        bounds.top
                    ),
                    bounds.planeZ
                );

            EmitNearStarAtWorldPosition(
                worldPosition
            );
        }
    }

    private void EmitNearStarAtWorldPosition(
        Vector3 worldPosition
    )
    {
        ParticleSystem.EmitParams emitParams =
            new ParticleSystem.EmitParams
            {
                position =
                    WorldToSimulationPosition(
                        worldPosition
                    ),
                applyShapeToPosition = false
            };

        nearStars.Emit(emitParams, 1);
    }

    private Vector3 GetRandomEntryPosition(
        CameraBounds2D bounds,
        Vector2 flow
    )
    {
        bool hasHorizontalFlow =
            Mathf.Abs(flow.x) > 0.0001f;

        bool hasVerticalFlow =
            Mathf.Abs(flow.y) > 0.0001f;

        if (!hasHorizontalFlow &&
            !hasVerticalFlow)
        {
            return new Vector3(
                Random.Range(
                    bounds.left,
                    bounds.right
                ),
                bounds.top +
                nearStarsSpawnPadding,
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
            float width = Mathf.Max(
                0.01f,
                bounds.right - bounds.left
            );

            float height = Mathf.Max(
                0.01f,
                bounds.top - bounds.bottom
            );

            float verticalWeight =
                width * Mathf.Abs(flow.y);

            float horizontalWeight =
                height * Mathf.Abs(flow.x);

            float totalWeight =
                verticalWeight +
                horizontalWeight;

            useVerticalEdge =
                Random.value <
                verticalWeight /
                Mathf.Max(
                    0.0001f,
                    totalWeight
                );
        }

        if (useVerticalEdge)
        {
            float y =
                flow.y < 0f
                    ? bounds.top +
                      nearStarsSpawnPadding
                    : bounds.bottom -
                      nearStarsSpawnPadding;

            return new Vector3(
                Random.Range(
                    bounds.left,
                    bounds.right
                ),
                y,
                bounds.planeZ
            );
        }

        float x =
            flow.x > 0f
                ? bounds.left -
                  nearStarsSpawnPadding
                : bounds.right +
                  nearStarsSpawnPadding;

        return new Vector3(
            x,
            Random.Range(
                bounds.bottom,
                bounds.top
            ),
            bounds.planeZ
        );
    }

    private void CullExitedNearStars()
    {
        EnsureParticleBuffer();

        int count =
            nearStars.GetParticles(
                particles
            );

        if (count <= 0)
            return;

        CameraBounds2D bounds =
            GetCameraBounds();

        Vector2 flow =
            GetNearStarsWorldFlow();

        bool changed = false;

        for (int i = 0; i < count; i++)
        {
            Vector3 worldPosition =
                SimulationToWorldPosition(
                    particles[i].position
                );

            bool exited = false;

            if (flow.x > 0.0001f &&
                worldPosition.x >
                bounds.right +
                nearStarsExitPadding)
            {
                exited = true;
            }
            else if (
                flow.x < -0.0001f &&
                worldPosition.x <
                bounds.left -
                nearStarsExitPadding)
            {
                exited = true;
            }

            if (flow.y < -0.0001f &&
                worldPosition.y <
                bounds.bottom -
                nearStarsExitPadding)
            {
                exited = true;
            }
            else if (
                flow.y > 0.0001f &&
                worldPosition.y >
                bounds.top +
                nearStarsExitPadding)
            {
                exited = true;
            }

            if (!exited)
                continue;

            particles[i].remainingLifetime = 0f;
            changed = true;
        }

        if (changed)
        {
            nearStars.SetParticles(
                particles,
                count
            );
        }
    }

    private CameraBounds2D GetCameraBounds()
    {
        float planeZ =
            nearStars != null
                ? nearStars.transform.position.z
                : 0f;

        float depth = Mathf.Abs(
            planeZ -
            nearStarsCamera.transform.position.z
        );

        Vector3 bottomLeft =
            nearStarsCamera.ViewportToWorldPoint(
                new Vector3(
                    0f,
                    0f,
                    depth
                )
            );

        Vector3 topRight =
            nearStarsCamera.ViewportToWorldPoint(
                new Vector3(
                    1f,
                    1f,
                    depth
                )
            );

        return new CameraBounds2D
        {
            left =
                Mathf.Min(
                    bottomLeft.x,
                    topRight.x
                ),
            right =
                Mathf.Max(
                    bottomLeft.x,
                    topRight.x
                ),
            bottom =
                Mathf.Min(
                    bottomLeft.y,
                    topRight.y
                ),
            top =
                Mathf.Max(
                    bottomLeft.y,
                    topRight.y
                ),
            planeZ = planeZ
        };
    }

    private Vector2 GetNearStarsWorldFlow()
    {
        ParticleSystem.VelocityOverLifetimeModule velocity =
            nearStars.velocityOverLifetime;

        Vector3 flow =
            new Vector3(
                GetRepresentativeCurveValue(
                    velocity.x
                ),
                GetRepresentativeCurveValue(
                    velocity.y
                ),
                GetRepresentativeCurveValue(
                    velocity.z
                )
            );

        if (velocity.space ==
            ParticleSystemSimulationSpace.Local)
        {
            flow =
                nearStars.transform.TransformVector(
                    flow
                );
        }
        else if (
            velocity.space ==
            ParticleSystemSimulationSpace.Custom)
        {
            ParticleSystem.MainModule main =
                nearStars.main;

            if (main.customSimulationSpace != null)
            {
                flow =
                    main.customSimulationSpace.TransformVector(
                        flow
                    );
            }
        }

        return new Vector2(
            flow.x,
            flow.y
        );
    }

    private Vector3 WorldToSimulationPosition(
        Vector3 worldPosition
    )
    {
        ParticleSystem.MainModule main =
            nearStars.main;

        switch (main.simulationSpace)
        {
            case ParticleSystemSimulationSpace.Local:
                return nearStars.transform
                    .InverseTransformPoint(
                        worldPosition
                    );

            case ParticleSystemSimulationSpace.Custom:
                if (main.customSimulationSpace != null)
                {
                    return main.customSimulationSpace
                        .InverseTransformPoint(
                            worldPosition
                        );
                }

                return worldPosition;

            default:
                return worldPosition;
        }
    }

    private Vector3 SimulationToWorldPosition(
        Vector3 simulationPosition
    )
    {
        ParticleSystem.MainModule main =
            nearStars.main;

        switch (main.simulationSpace)
        {
            case ParticleSystemSimulationSpace.Local:
                return nearStars.transform
                    .TransformPoint(
                        simulationPosition
                    );

            case ParticleSystemSimulationSpace.Custom:
                if (main.customSimulationSpace != null)
                {
                    return main.customSimulationSpace
                        .TransformPoint(
                            simulationPosition
                        );
                }

                return simulationPosition;

            default:
                return simulationPosition;
        }
    }

    private void ApplyColorToLivingParticles(
        Color color,
        int maxParticles
    )
    {
        EnsureParticleBuffer();

        int particleCount =
            nearStars.GetParticles(
                particles
            );

        particleCount =
            Mathf.Min(
                particleCount,
                maxParticles
            );

        for (int i = 0;
             i < particleCount;
             i++)
        {
            particles[i].startColor = color;
        }

        nearStars.SetParticles(
            particles,
            particleCount
        );
    }

    private void EnsureParticleBuffer()
    {
        if (nearStars == null)
            return;

        int requiredSize = Mathf.Max(
            1,
            Mathf.Max(
                nearStars.main.maxParticles,
                nearStars.particleCount
            )
        );

        if (particles == null ||
            particles.Length < requiredSize)
        {
            particles =
                new ParticleSystem.Particle[
                    requiredSize
                ];
        }
    }

    private void CacheOriginalParticleSettings()
    {
        if (nearStars == null)
            return;

        ParticleSystem.MainModule main =
            nearStars.main;

        ParticleSystem.EmissionModule emission =
            nearStars.emission;

        ParticleSystem.VelocityOverLifetimeModule velocity =
            nearStars.velocityOverLifetime;

        originalEmissionRate =
            GetRepresentativeCurveValue(
                emission.rateOverTime
            );

        originalMaxParticles =
            Mathf.Max(
                1,
                main.maxParticles
            );

        originalVelocityX = velocity.x;
        originalVelocityY = velocity.y;
        originalVelocityZ = velocity.z;
    }

    private static ParticleSystem.MinMaxCurve ScaleCurve(
        ParticleSystem.MinMaxCurve source,
        float multiplier
    )
    {
        multiplier =
            Mathf.Max(
                0f,
                multiplier
            );

        switch (source.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return new ParticleSystem.MinMaxCurve(
                    source.constant *
                    multiplier
                );

            case ParticleSystemCurveMode.TwoConstants:
                return new ParticleSystem.MinMaxCurve(
                    source.constantMin *
                    multiplier,
                    source.constantMax *
                    multiplier
                );

            case ParticleSystemCurveMode.Curve:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier *
                    multiplier,
                    source.curve
                );

            case ParticleSystemCurveMode.TwoCurves:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier *
                    multiplier,
                    source.curveMin,
                    source.curveMax
                );

            default:
                return source;
        }
    }

    private static float GetRepresentativeCurveValue(
        ParticleSystem.MinMaxCurve curve
    )
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;

            case ParticleSystemCurveMode.TwoConstants:
                return (
                    curve.constantMin +
                    curve.constantMax
                ) * 0.5f;

            case ParticleSystemCurveMode.Curve:
                return curve.curve != null
                    ? curve.curve.Evaluate(0.5f) *
                      curve.curveMultiplier
                    : 0f;

            case ParticleSystemCurveMode.TwoCurves:
                float minValue =
                    curve.curveMin != null
                        ? curve.curveMin.Evaluate(0.5f)
                        : 0f;

                float maxValue =
                    curve.curveMax != null
                        ? curve.curveMax.Evaluate(0.5f)
                        : 0f;

                return (
                    minValue +
                    maxValue
                ) * 0.5f *
                curve.curveMultiplier;

            default:
                return 0f;
        }
    }

    private void MigrateLegacySettingsIfNeeded()
    {
        if (screenEdgeSettingsVersion >= 2)
            return;

        if (screenEdgeSettingsVersion < 1)
        {
            firstPageEmissionRate = 4.25f;
            lastPageEmissionRate = 8.5f;
            firstPageMaxParticles = 140;
            lastPageMaxParticles = 260;
        }

        basePanelEmissionRate = 1.5f;
        basePanelMaxParticles = 50;

        screenEdgeSettingsVersion = 2;
    }

    private Color GetSelectedSkinThemeColor()
    {
        PlayerSkinCatalog catalog = ResolveSkinCatalog();

        return GetSkinThemeColor(
            catalog != null
                ? catalog.GetSelectedSkin()
                : null
        );
    }

    private Color GetSkinThemeColor(
        PlayerSkinCatalog.SkinEntry skin)
    {
        Color color = skin != null
            ? PlayerSkinCatalog.GetUIThemeColor(skin)
            : Color.white;

        float highestChannel = Mathf.Max(
            color.r,
            color.g,
            color.b
        );

        if (highestChannel > 1f)
        {
            color.r /= highestChannel;
            color.g /= highestChannel;
            color.b /= highestChannel;
        }

        color.a = Mathf.Clamp01(skinThemeAlpha);
        return color;
    }

    private static PlayerSkinCatalog ResolveSkinCatalog()
    {
        if (PlayerSkinCatalog.LoadedInstance != null)
            return PlayerSkinCatalog.LoadedInstance;

        PlayerSkinCatalog[] catalogs =
            Resources.FindObjectsOfTypeAll<PlayerSkinCatalog>();

        if (catalogs == null || catalogs.Length == 0)
            return null;

        for (int i = 0; i < catalogs.Length; i++)
        {
            PlayerSkinCatalog catalog = catalogs[i];

            if (catalog != null &&
                string.Equals(
                    catalog.name,
                    "PlayerSkinCatalog",
                    System.StringComparison.Ordinal
                ))
            {
                return catalog;
            }
        }

        return catalogs[0];
    }

    private static float EaseInOutCubic(
        float value
    )
    {
        value = Mathf.Clamp01(value);

        if (value < 0.5f)
            return 4f * value * value * value;

        float inverse =
            -2f * value + 2f;

        return 1f -
               inverse *
               inverse *
               inverse /
               2f;
    }

    private void OnValidate()
    {
        if (nearStars == null)
            nearStars = GetComponent<ParticleSystem>();

        MigrateLegacySettingsIfNeeded();

        transitionDuration =
            Mathf.Max(
                0.01f,
                transitionDuration
            );

        skinThemeAlpha =
            Mathf.Clamp01(skinThemeAlpha);

        basePanelEmissionRate =
            Mathf.Max(
                0f,
                basePanelEmissionRate
            );

        basePanelMaxParticles =
            Mathf.Max(
                1,
                basePanelMaxParticles
            );

        firstPageEmissionRate =
            Mathf.Max(
                0f,
                firstPageEmissionRate
            );

        lastPageEmissionRate =
            Mathf.Max(
                firstPageEmissionRate,
                lastPageEmissionRate
            );

        firstPageMaxParticles =
            Mathf.Max(
                1,
                firstPageMaxParticles
            );

        lastPageMaxParticles =
            Mathf.Max(
                firstPageMaxParticles,
                lastPageMaxParticles
            );

        firstPageFlowMultiplier =
            Mathf.Max(
                0f,
                firstPageFlowMultiplier
            );

        lastPageFlowMultiplier =
            Mathf.Max(
                firstPageFlowMultiplier,
                lastPageFlowMultiplier
            );

        nearStarsSpawnPadding =
            Mathf.Max(
                0f,
                nearStarsSpawnPadding
            );

        nearStarsExitPadding =
            Mathf.Max(
                0f,
                nearStarsExitPadding
            );

        float minLifetime =
            Mathf.Max(
                1f,
                Mathf.Min(
                    nearStarsLifetimeRange.x,
                    nearStarsLifetimeRange.y
                )
            );

        float maxLifetime =
            Mathf.Max(
                minLifetime,
                Mathf.Max(
                    nearStarsLifetimeRange.x,
                    nearStarsLifetimeRange.y
                )
            );

        nearStarsLifetimeRange =
            new Vector2(
                minLifetime,
                maxLifetime
            );
    }
}