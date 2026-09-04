using System.Collections;
using UnityEngine;


// Dynamic AOE danger preview.
// CPU sadece 1D angular visibility texture'i belirli araliklarla raycast ile gunceller.
// Full-screen danger/safe cizimi, wave, gradient ve fade tamamen shader tarafinda yapilir.
public sealed class EnemyDangerPreviewRuntime : MonoBehaviour
{
    private Texture2D visibilityTexture;
    private Color[] visibilityPixels;
    private Material previewMaterial;

    private Vector2 origin;
    private bool useRadius;
    private float radius;
    private LayerMask coverLayers;

    private float minX;
    private float maxX;
    private float minY;
    private float maxY;

    private float maxRange;
    private int angularSamples;

    private Vector2[] rayDirections;
    private float[] rayMaximumDistances;

    private bool useMobileTimeSlicing;
    private float refreshInterval;
    private float nextVisibilityRefreshTime;

    private float samplesPerSecond;
    private float sampleBudget;
    private int sampleCursor;
    private int maxSamplesPerFrame;

    private float currentProgress;
    private float opacityMultiplier = 1f;
    private float strikeWaveProgress = -1f;
    private float strikeWaveWidth = 0.10f;
    private float strikeWaveBoost = 1.35f;

    private const float TwoPi = Mathf.PI * 2f;
    private const float HitSkin = 0.02f;
    private const float MaxBudgetDeltaTime = 0.05f;

    public void Initialize(
        Material material,
        Texture2D visibility,
        Color[] pixels,
        Vector2 worldOrigin,
        bool localRadiusMode,
        float localRadius,
        LayerMask layers,
        float arenaMinX,
        float arenaMaxX,
        float arenaMinY,
        float arenaMaxY,
        float maximumRange,
        int sampleCount,
        float visibilityRefreshRate)
    {
        previewMaterial = material;
        visibilityTexture = visibility;
        visibilityPixels = pixels;

        origin = worldOrigin;
        useRadius = localRadiusMode;
        radius = Mathf.Max(0.01f, localRadius);
        coverLayers = layers;

        minX = arenaMinX;
        maxX = arenaMaxX;
        minY = arenaMinY;
        maxY = arenaMaxY;

        maxRange = Mathf.Max(0.01f, maximumRange);
        angularSamples = Mathf.Max(64, sampleCount);

        BuildRayCache();

        float safeRate = Mathf.Clamp(
            visibilityRefreshRate,
            1f,
            60f
        );

        useMobileTimeSlicing =
            RuntimePerformancePolicy.IsPhysicalMobileRuntime;

        // Mobile keeps the same angular resolution and effective refresh
        // target, but spreads the raycasts across multiple rendered frames
        // instead of executing the whole sweep in one frame.
        if (useMobileTimeSlicing)
            safeRate = Mathf.Min(safeRate, 10f);

        refreshInterval = 1f / safeRate;

        samplesPerSecond =
            angularSamples * safeRate;

        maxSamplesPerFrame =
            useMobileTimeSlicing
                ? Mathf.Clamp(
                    Mathf.CeilToInt(
                        angularSamples * 0.5f
                    ),
                    96,
                    256
                )
                : angularSamples;

        sampleBudget = 0f;
        sampleCursor = 0;

        currentProgress = 0f;
        opacityMultiplier = 1f;
        strikeWaveProgress = -1f;

        if (useMobileTimeSlicing)
        {
            // CreatePreview starts fully transparent (_Progress = 0).
            // The first visibility sweep is therefore built progressively
            // during the first few frames instead of causing a spawn spike.
            nextVisibilityRefreshTime = -1f;
        }
        else
        {
            RefreshVisibilityImmediate();
            nextVisibilityRefreshTime =
                Time.unscaledTime + refreshInterval;
        }

        PushVisualState();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        if (useMobileTimeSlicing)
            RefreshVisibilityTimeSliced();
        else
            RefreshVisibilityScheduled();
    }

    public void SetProgress(float progress)
    {
        currentProgress = Mathf.Clamp01(progress);
        PushVisualState();
    }

    public void SetOpacity(float opacity)
    {
        opacityMultiplier = Mathf.Clamp01(opacity);
        PushVisualState();
    }

    public void SetStrikeWave(
        float progress,
        float width,
        float boost)
    {
        strikeWaveProgress = progress < 0f
            ? -1f
            : Mathf.Clamp01(progress);

        strikeWaveWidth =
            Mathf.Clamp(width, 0.02f, 0.35f);

        strikeWaveBoost =
            Mathf.Max(0f, boost);

        PushVisualState();
    }

    public void ReleaseResources()
    {
        if (visibilityTexture != null)
        {
            Destroy(visibilityTexture);
            visibilityTexture = null;
        }

        visibilityPixels = null;
        rayDirections = null;
        rayMaximumDistances = null;
        previewMaterial = null;
    }

    private void PushVisualState()
    {
        if (previewMaterial == null)
            return;

        previewMaterial.SetFloat(
            "_Progress",
            currentProgress
        );

        previewMaterial.SetFloat(
            "_Opacity",
            opacityMultiplier
        );

        previewMaterial.SetFloat(
            "_StrikeWaveProgress",
            strikeWaveProgress
        );

        previewMaterial.SetFloat(
            "_StrikeWaveWidth",
            strikeWaveWidth
        );

        previewMaterial.SetFloat(
            "_StrikeWaveBoost",
            strikeWaveBoost
        );
    }

    private void BuildRayCache()
    {
        rayDirections =
            new Vector2[angularSamples];

        rayMaximumDistances =
            new float[angularSamples];

        for (int i = 0; i < angularSamples; i++)
        {
            float normalized =
                (i + 0.5f) / angularSamples;

            float angle =
                normalized * TwoPi - Mathf.PI;

            Vector2 direction =
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                );

            rayDirections[i] = direction;

            float maximumDistance =
                useRadius
                    ? radius
                    : DistanceToRectEdge(
                        origin,
                        direction,
                        minX,
                        maxX,
                        minY,
                        maxY
                    );

            if (maximumDistance <= 0f ||
                float.IsInfinity(maximumDistance) ||
                float.IsNaN(maximumDistance))
            {
                maximumDistance = 0.01f;
            }

            rayMaximumDistances[i] =
                maximumDistance;
        }
    }

    private void RefreshVisibilityScheduled()
    {
        if (!CanRefreshVisibility())
            return;

        float now = Time.unscaledTime;

        if (now < nextVisibilityRefreshTime)
            return;

        nextVisibilityRefreshTime =
            now + refreshInterval;

        RefreshVisibilityImmediate();
    }

    private void RefreshVisibilityTimeSliced()
    {
        if (!CanRefreshVisibility())
            return;

        float deltaTime =
            Mathf.Min(
                Time.unscaledDeltaTime,
                MaxBudgetDeltaTime
            );

        sampleBudget =
            Mathf.Min(
                sampleBudget +
                samplesPerSecond * deltaTime,
                maxSamplesPerFrame
            );

        int samplesThisFrame =
            Mathf.Min(
                Mathf.FloorToInt(sampleBudget),
                maxSamplesPerFrame
            );

        if (samplesThisFrame <= 0)
            return;

        sampleBudget -= samplesThisFrame;

        for (int i = 0; i < samplesThisFrame; i++)
        {
            RefreshVisibilitySample(
                sampleCursor
            );

            sampleCursor++;

            if (sampleCursor < angularSamples)
                continue;

            UploadVisibilityTexture();
            sampleCursor = 0;
        }
    }

    private void RefreshVisibilityImmediate()
    {
        if (!CanRefreshVisibility())
            return;

        for (int i = 0; i < angularSamples; i++)
            RefreshVisibilitySample(i);

        UploadVisibilityTexture();
        sampleCursor = 0;
        sampleBudget = 0f;
    }

    private bool CanRefreshVisibility()
    {
        return visibilityTexture != null &&
               visibilityPixels != null &&
               previewMaterial != null &&
               rayDirections != null &&
               rayMaximumDistances != null;
    }

    private void RefreshVisibilitySample(
        int sampleIndex)
    {
        Vector2 direction =
            rayDirections[sampleIndex];

        float maximumDistance =
            rayMaximumDistances[sampleIndex];

        RaycastHit2D hit =
            Physics2D.Raycast(
                origin,
                direction,
                maximumDistance,
                coverLayers
            );

        float visibleDistance =
            hit.collider != null
                ? Mathf.Max(
                    0.001f,
                    hit.distance - HitSkin
                )
                : maximumDistance;

        float normalizedDistance =
            Mathf.Clamp01(
                visibleDistance / maxRange
            );

        visibilityPixels[sampleIndex] =
            new Color(
                normalizedDistance,
                0f,
                0f,
                1f
            );
    }

    private void UploadVisibilityTexture()
    {
        visibilityTexture.SetPixels(
            visibilityPixels
        );

        visibilityTexture.Apply(
            false,
            false
        );
    }

    private static float DistanceToRectEdge(
        Vector2 origin,
        Vector2 direction,
        float minX,
        float maxX,
        float minY,
        float maxY)
    {
        float distance =
            float.PositiveInfinity;

        const float epsilon = 0.0001f;

        if (direction.x > epsilon)
        {
            float t =
                (maxX - origin.x) /
                direction.x;

            if (t > 0f)
                distance =
                    Mathf.Min(distance, t);
        }
        else if (direction.x < -epsilon)
        {
            float t =
                (minX - origin.x) /
                direction.x;

            if (t > 0f)
                distance =
                    Mathf.Min(distance, t);
        }

        if (direction.y > epsilon)
        {
            float t =
                (maxY - origin.y) /
                direction.y;

            if (t > 0f)
                distance =
                    Mathf.Min(distance, t);
        }
        else if (direction.y < -epsilon)
        {
            float t =
                (minY - origin.y) /
                direction.y;

            if (t > 0f)
                distance =
                    Mathf.Min(distance, t);
        }

        return distance;
    }
}


public static class EnemyDangerPreviewMesh
{
    public static GameObject CreatePreview(
        Vector2 origin,
        bool useRadius,
        float radius,
        LayerMask coverLayers,
        Color color,
        int rayCount,
        int sortingOrder,
        float innerRadius = 0.35f,
        int radialSegments = 14,
        int smoothingPasses = 2,
        float innerAlphaMultiplier = 0.22f,
        float waveFrontWidth = 0.12f,
        float waveFrontBoost = 0.75f,
        float innerBrightness = 0.55f,
        Shader customShader = null,
        float visibilityRefreshRate = 15f,
        float coverFeather = 0.08f)
    {
        if (!TryGetArenaRect(
                out float minX,
                out float maxX,
                out float minY,
                out float maxY))
        {
            return null;
        }

        Shader shader = customShader;

        if (shader == null)
            shader = Shader.Find("FatefulRush/BossDangerPreview");

        if (shader == null)
        {
            Debug.LogError(
                "BossDangerPreview shader bulunamadi. " +
                "BossDangerPreview.shader dosyasini projeye ekle " +
                "veya Inspector'daki Danger Preview Shader alanina ata."
            );
            return null;
        }

        int requestedSamples = Mathf.Max(180, rayCount);

        int angularSamples = RuntimePerformancePolicy.IsPhysicalMobileRuntime
            ? Mathf.Clamp(requestedSamples, 256, 512)
            : Mathf.Clamp(requestedSamples * 2, 512, 1024);

        float maxRange =
            useRadius
                ? Mathf.Max(0.01f, radius)
                : GetFarthestCornerDistance(
                    origin,
                    minX,
                    maxX,
                    minY,
                    maxY
                );

        TextureFormat visibilityTextureFormat =
            SystemInfo.SupportsTextureFormat(
                TextureFormat.RHalf
            )
                ? TextureFormat.RHalf
                : SystemInfo.SupportsTextureFormat(
                    TextureFormat.RGBAHalf
                )
                    ? TextureFormat.RGBAHalf
                    : TextureFormat.RGBAFloat;

        Texture2D visibilityTexture =
            new Texture2D(
                angularSamples,
                1,
                visibilityTextureFormat,
                false,
                true
            )
            {
                name = "AOE_Visibility1D",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 0
            };

        Color[] visibilityPixels =
            new Color[angularSamples];

        for (int i = 0; i < angularSamples; i++)
            visibilityPixels[i] = Color.white;

        visibilityTexture.SetPixels(visibilityPixels);
        visibilityTexture.Apply(false, false);

        GameObject previewObject =
            new GameObject("AOE_DangerPreview");

        previewObject.transform.position =
            Vector3.zero;

        MeshFilter meshFilter =
            previewObject.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer =
            previewObject.AddComponent<MeshRenderer>();

        Mesh quad =
            CreateArenaQuad(
                minX,
                maxX,
                minY,
                maxY
            );

        meshFilter.sharedMesh = quad;

        Material material =
            new Material(shader)
            {
                name =
                    "AOE_DangerPreviewMaterial"
            };

        material.SetTexture(
            "_VisibilityTex",
            visibilityTexture
        );

        material.SetColor(
            "_DangerColor",
            color
        );

        material.SetVector(
            "_Origin",
            new Vector4(
                origin.x,
                origin.y,
                0f,
                0f
            )
        );

        material.SetFloat(
            "_InnerRadius",
            Mathf.Max(0f, innerRadius)
        );

        material.SetFloat(
            "_MaxRange",
            maxRange
        );

        material.SetFloat(
            "_UseRadius",
            useRadius ? 1f : 0f
        );

        material.SetFloat(
            "_Radius",
            Mathf.Max(0.01f, radius)
        );

        material.SetFloat(
            "_InnerAlphaMultiplier",
            Mathf.Clamp01(
                innerAlphaMultiplier
            )
        );

        material.SetFloat(
            "_WaveFrontWidth",
            Mathf.Clamp(
                waveFrontWidth,
                0.01f,
                0.5f
            )
        );

        material.SetFloat(
            "_WaveFrontBoost",
            Mathf.Max(
                0f,
                waveFrontBoost
            )
        );

        material.SetFloat(
            "_InnerBrightness",
            Mathf.Clamp(
                innerBrightness,
                0.1f,
                1f
            )
        );

        material.SetFloat(
            "_CoverFeather",
            Mathf.Max(
                0.001f,
                coverFeather
            )
        );

        material.SetFloat("_Progress", 0f);
        material.SetFloat("_Opacity", 1f);
        material.SetFloat("_StrikeWaveProgress", -1f);
        material.SetFloat("_StrikeWaveWidth", 0.10f);
        material.SetFloat("_StrikeWaveBoost", 1.35f);

        meshRenderer.sharedMaterial =
            material;

        meshRenderer.sortingOrder =
            sortingOrder;

        EnemyDangerPreviewRuntime runtime =
            previewObject.AddComponent<EnemyDangerPreviewRuntime>();

        runtime.Initialize(
            material,
            visibilityTexture,
            visibilityPixels,
            origin,
            useRadius,
            radius,
            coverLayers,
            minX,
            maxX,
            minY,
            maxY,
            maxRange,
            angularSamples,
            visibilityRefreshRate
        );

        return previewObject;
    }

    public static void SetPreviewAlpha(
        GameObject previewObject,
        Color targetColor,
        float normalizedAlpha)
    {
        if (previewObject == null)
            return;

        EnemyDangerPreviewRuntime runtime =
            previewObject.GetComponent<EnemyDangerPreviewRuntime>();

        if (runtime != null)
        {
            runtime.SetProgress(normalizedAlpha);
            return;
        }
    }

    public static void SetPreviewOpacity(
        GameObject previewObject,
        float normalizedOpacity)
    {
        if (previewObject == null)
            return;

        EnemyDangerPreviewRuntime runtime =
            previewObject.GetComponent<EnemyDangerPreviewRuntime>();

        if (runtime != null)
        {
            runtime.SetOpacity(normalizedOpacity);
            return;
        }
    }

    public static void SetStrikeWave(
        GameObject previewObject,
        float progress,
        float width,
        float boost)
    {
        if (previewObject == null)
            return;

        EnemyDangerPreviewRuntime runtime =
            previewObject.GetComponent<EnemyDangerPreviewRuntime>();

        if (runtime != null)
        {
            runtime.SetStrikeWave(
                progress,
                width,
                boost
            );
        }
    }

    public static void DestroyPreview(
        ref GameObject previewObject)
    {
        if (previewObject == null)
            return;

        EnemyDangerPreviewRuntime runtime =
            previewObject.GetComponent<EnemyDangerPreviewRuntime>();

        if (runtime != null)
            runtime.ReleaseResources();

        MeshFilter filter =
            previewObject.GetComponent<MeshFilter>();

        MeshRenderer renderer =
            previewObject.GetComponent<MeshRenderer>();

        if (filter != null &&
            filter.sharedMesh != null)
        {
            Object.Destroy(
                filter.sharedMesh
            );
        }

        if (renderer != null &&
            renderer.sharedMaterial != null)
        {
            Object.Destroy(
                renderer.sharedMaterial
            );
        }

        Object.Destroy(previewObject);
        previewObject = null;
    }

    private static Mesh CreateArenaQuad(
        float minX,
        float maxX,
        float minY,
        float maxY)
    {
        Vector3[] vertices =
        {
            new Vector3(minX, minY, 0f),
            new Vector3(maxX, minY, 0f),
            new Vector3(minX, maxY, 0f),
            new Vector3(maxX, maxY, 0f)
        };

        Vector2[] uvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        int[] triangles =
        {
            0, 2, 1,
            1, 2, 3
        };

        Mesh mesh =
            new Mesh
            {
                name =
                    "AOE_DangerPreview_ScreenQuad"
            };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    private static float GetFarthestCornerDistance(
        Vector2 origin,
        float minX,
        float maxX,
        float minY,
        float maxY)
    {
        float maxDistance = 0.01f;

        maxDistance = Mathf.Max(
            maxDistance,
            Vector2.Distance(
                origin,
                new Vector2(minX, minY)
            )
        );

        maxDistance = Mathf.Max(
            maxDistance,
            Vector2.Distance(
                origin,
                new Vector2(minX, maxY)
            )
        );

        maxDistance = Mathf.Max(
            maxDistance,
            Vector2.Distance(
                origin,
                new Vector2(maxX, minY)
            )
        );

        maxDistance = Mathf.Max(
            maxDistance,
            Vector2.Distance(
                origin,
                new Vector2(maxX, maxY)
            )
        );

        return maxDistance;
    }

    private static bool TryGetArenaRect(
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        CameraWorldBounds worldBounds =
            CameraWorldBounds.Instance;

        if (worldBounds != null)
        {
            minX = worldBounds.MinX;
            maxX = worldBounds.MaxX;
            minY = worldBounds.MinY;
            maxY = worldBounds.MaxY;
            return true;
        }

        Camera camera = Camera.main;

        if (camera == null)
        {
            minX = maxX = minY = maxY = 0f;
            return false;
        }

        float planeDistance =
            Mathf.Abs(
                camera.transform.position.z
            );

        Vector3 bottomLeft =
            camera.ViewportToWorldPoint(
                new Vector3(
                    0f,
                    0f,
                    planeDistance
                )
            );

        Vector3 topRight =
            camera.ViewportToWorldPoint(
                new Vector3(
                    1f,
                    1f,
                    planeDistance
                )
            );

        minX = Mathf.Min(
            bottomLeft.x,
            topRight.x
        );

        maxX = Mathf.Max(
            bottomLeft.x,
            topRight.x
        );

        minY = Mathf.Min(
            bottomLeft.y,
            topRight.y
        );

        maxY = Mathf.Max(
            bottomLeft.y,
            topRight.y
        );

        return true;
    }
}



[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BossEnemyFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Movement")]
    public float speed = 1.2f;

    [Tooltip("Boss normal chase durumundayken kullanilan minimal shake miktari.")]
    [Min(0f)] public float normalShakeAmount = 0.015f;

    [Tooltip("Hedef yonune donusun ne kadar yumusak olacagi.")]
    public float directionSmoothness = 7f;

    [Header("Collision")]
    public LayerMask solidLayers;
    public float castSkin = 0.05f;
    public float obstacleProbeDistance = 1f;
    [Range(0f, 1f)] public float obstacleOutwardBias = 0.3f;

    [Tooltip("Dogrudan hareket mumkun degilse kac farkli kayma acisi denenecek.")]
    [Range(1, 8)]
    public int slideDirectionAttempts = 4;

    [Header("Advanced Unstuck")]
    public LayerMask obstacleLayer;
    public float escapeCheckRadius = 1.2f;
    public float escapeSpeedMultiplier = 2.2f;

    [Header("Boss Route Planning")]
    [Tooltip("Bossun obstacle/corner problemlerini onceden gorebilmesi icin kullanilan uzun menzilli shape-cast mesafesi.")]
    [Min(1f)] public float routeLookAheadDistance = 3.6f;

    [Tooltip("Boss bir obstacle'in hangi tarafindan dolanacagina karar verdikten sonra bu kadar sure o tarafa sadik kalir. Ayni dar koridora tekrar tekrar donmesini engeller.")]
    [Min(0.1f)] public float routeCommitDuration = 1.15f;

    [Tooltip("Uzun menzilli rota adaylarinin kac farkli acida test edilecegi.")]
    [Range(4, 9)] public int routeAngleSamples = 7;

    [Tooltip("Boss collider'i ile ekran siniri arasinda birakilacak ekstra guvenlik boslugu.")]
    [Min(0f)] public float arenaEdgePadding = 0.10f;

    [Tooltip("Rota seciminde mevcut commit edilen tarafa verilen bonus. Yuksek deger sag-sol kararsizligini azaltir.")]
    [Range(0f, 1f)] public float routeSideCommitment = 0.35f;

    [Header("Spawn Effect")]
    [Tooltip("Diger enemyler gibi Boss da 0 scale'dan kendi boyutuna smooth sekilde gelir.")]
    [Min(0f)] public float spawnEffectDuration = 0.15f;

    [Header("Boss Global AOE")]
    public bool aoeEnabled = true;

    [Tooltip("Stalker absorption bittikten sonra ilk AOE charge baslamadan once beklenecek sure.")]
    [Min(0f)] public float firstAoeDelay = 1.5f;

    [Tooltip("Bir AOE bittikten sonra yeni AOE charge baslayana kadar Boss playeri kovalar.")]
    [Min(0f)] public float aoeCooldown = 5f;

    [Tooltip("Boss bu sure boyunca tamamen sabit kalir. Shake ve kirmizi danger preview 0'dan maksimuma dogru artar; sure bitince strike gerceklesir.")]
    [Min(3f)] public float aoeChargeDuration = 3f;

    [Tooltip("AOE patlamasina yaklasirken ulasilacak maksimum shake miktari.")]
    [Min(0f)] public float aoeMaxShakeAmount = 0.18f;

    [Tooltip("0 birakilirsa obstacleLayer cover icin kullanilir.")]
    public LayerMask aoeCoverLayers;

    [Header("Boss Power-Up")]
    [Tooltip("Tum Stalkerlar emildikten sonra Bossun kalici olarak buyuyecegi oran.")]
    [Min(1f)] public float powerUpScaleMultiplier = 1.12f;

    [Tooltip("Final buyumeden once hedef boyutun kac kere hizlica gorunup kaybolacagi.")]
    [Range(0, 4)] public int powerUpPreviewFlashes = 2;

    [Tooltip("Hedef boyut preview'inin ekranda kaldigi cok kisa sure.")]
    [Min(0.01f)] public float powerUpPreviewOnDuration = 0.055f;

    [Tooltip("Preview flashlari arasindaki cok kisa bosluk.")]
    [Min(0.01f)] public float powerUpPreviewOffDuration = 0.035f;

    [Tooltip("Preview ghost'un alpha degeri.")]
    [Range(0.05f, 1f)] public float powerUpPreviewAlpha = 0.55f;

    [Tooltip("Preview bittikten sonra Bossun asil boyutuna yumusak gecis suresi.")]
    [Min(0.01f)] public float powerUpGrowDuration = 0.12f;

    public AudioClip powerUpSfx;

    [Header("Boss AOE Danger Preview / SFX")]
    [Tooltip("AOE charge boyunca 0 alphadan bu maksimum renge dogru ilerler. Safe alanlar obstacle arkalarinda tamamen bos kalir.")]
    public Color dangerPreviewColor =
        new Color(1f, 0.025f, 0.025f, 0.46f);

    [Tooltip("AOE strike gerceklestigi anda Boss merkezinden disariya yayilan parlak shockwave'in suresi.")]
    [Min(0.05f)] public float dangerStrikeWaveDuration = 0.38f;

    [Tooltip("Strike shockwave halkasinin radial genisligi.")]
    [Range(0.03f, 0.30f)] public float dangerStrikeWaveWidth = 0.09f;

    [Tooltip("Strike shockwave'in normal red danger alanina gore ekstra parlaklik/alpha gucu.")]
    [Range(0f, 3f)] public float dangerStrikeWaveBoost = 1.55f;

    [Tooltip("AOE strike + shockwave bittikten sonra kirmizi alanin alpha ile tamamen kaybolma suresi. Bu fade bitene kadar Boss sabit kalir.")]
    [Min(0.05f)] public float dangerPreviewFadeOutDuration = 0.6f;

    [Tooltip("Boss sprite/collider kenari ile kirmizi dalganin baslangici arasindaki bosluk. Bossun ustune kirmizi binmesini engeller.")]
    [Min(0f)] public float dangerPreviewInnerPadding = 0.16f;

    [Tooltip("Boss cevresindeki kirmizi bolgenin alpha carpani. Ekran kenarina dogru alpha artar.")]
    [Range(0.05f, 1f)] public float dangerPreviewInnerAlphaMultiplier = 0.22f;

    [Tooltip("Dalga on cephesinin yumusaklik/genislik miktari.")]
    [Range(0.03f, 0.3f)] public float dangerPreviewWaveFrontWidth = 0.12f;

    [Tooltip("Disariya ilerleyen dalga on cephesinin ekstra parlaklik/alpha gucu.")]
    [Range(0f, 2f)] public float dangerPreviewWaveFrontBoost = 0.75f;

    [Tooltip("Boss cevresindeki kirmizinin ne kadar koyu baslayacagi. Ekran kenarina dogru normal renge doner.")]
    [Range(0.1f, 1f)] public float dangerPreviewInnerBrightness = 0.55f;

    [Tooltip("Merkezden disariya kac halka kullanilacagi. Yuksek deger dalga gradientini daha yumusak yapar.")]
    [Range(6, 24)] public int dangerPreviewRadialSegments = 14;

    [Tooltip("Obstacle golge kenarlarindaki pütürleri azaltmak icin kac smoothing pass uygulanacagi.")]
    [Range(0, 4)] public int dangerPreviewEdgeSmoothingPasses = 2;

    [Tooltip("Obstacle arkasi safe alanlarin acisal hassasiyeti. Kod en az 360 ray kullanir.")]
    [Range(360, 960)] public int dangerPreviewRayCount = 360;

    [Tooltip("Danger preview icin kullanilan shader. Bos birakilirsa FatefulRush/BossDangerPreview Shader.Find ile aranir.")]
    public Shader dangerPreviewShader;

    [Tooltip("Hareketli obstacle cover bilgisinin saniyede kac kez guncellenecegi. 12-20 arasi mobil icin ideal.")]
    [Range(5f, 30f)] public float dangerPreviewVisibilityRefreshRate = 15f;

    [Tooltip("Obstacle safe-area kenarlarinin dunya birimi cinsinden yumusaklik miktari.")]
    [Range(0.01f, 0.35f)] public float dangerPreviewCoverFeather = 0.08f;

    [Tooltip("Danger mesh'in SpriteRenderer'larin ustunde gorunmesi icin sorting order.")]
    public int dangerPreviewSortingOrder = 20000;

    public AudioClip aoeSfx;

    [Header("Split Settings")]
    public GameObject miniBossPrefab;
    public bool canSplit = true;
    public float miniBossSpeed = 2.5f;
    public float splitDelay = 0.8f;
    public float splitDistance = 1.2f;
    public float splitShakeAmount = 0.18f;
    public Color splitFlashColor =
        new Color(0.45f, 0f, 0f, 1f);
    public float flashSpeed = 0.08f;

    [Tooltip("Ikinci MiniBossun ilk AOE'si, birinciden bu kadar daha gec hazir olur.")]
    [Min(0f)] public float miniBossAoeStagger = 1.25f;

    [Header("Split Visual")]
    public float splitScaleMin = 0.92f;
    public float splitScaleMax = 1.08f;

    [Tooltip("Split bittiginde Boss kuculerek kaybolur.")]
    public float splitDisappearDuration = 0.12f;

    [Header("Stuck Fix")]
    public float stuckCheckTime = 0.5f;
    public float stuckDistance = 0.08f;
    public float unstuckDuration = 0.5f;
    public float unstuckSideForce = 1.5f;

    private PlayerMovement playerMovement;
    private PlayerArmor playerArmor;

    private Rigidbody2D rb;
    private Collider2D bossCollider;
    private SpriteRenderer spriteRenderer;

    private Vector2 lastPosition;
    private Vector2 smoothedDirection;

    private float stuckTimer;
    private float unstuckTimer;
    private int unstuckDirection = 1;

    private bool isSplitting;
    private bool stopped;
    private bool isSpawning;
    private Coroutine spawnRoutine;

    private AudioSource bossSfxSource;
    private float bossSfxBasePitch = 1f;
    private bool bossSfxFollowsGameTime;
    private bool bossSfxPausedByGame;
    private float bossSfxVolumeMultiplier = 1f;

    private Color originalColor;
    private Vector3 originalScale;
    private Vector3 currentScaleMagnitude;
    private float scaleSignY = 1f;
    private float scaleSignZ = 1f;
    private int facingSign = 1;

    private ContactFilter2D navigationFilter;

    private readonly RaycastHit2D[] castHits =
        new RaycastHit2D[8];

    private readonly RaycastHit2D[] avoidanceHits =
        new RaycastHit2D[12];

    private readonly RaycastHit2D[] routePlanningHits =
        new RaycastHit2D[16];

    private readonly Collider2D[] escapeHits =
        new Collider2D[16];

    private int committedRouteSide;
    private float routeCommitTimer;

    private bool absorptionStarted;
    private int pendingStalkerAbsorptions;
    private bool aoeUnlocked;
    private float aoeCooldownTimer;
    private bool isChargingAoe;
    private bool isAoeFadingOut;
    private float aoeChargeProgress;
    private Vector2 aoeChargeCenter;
    private Coroutine aoeRoutine;
    private Coroutine powerUpRoutine;
    private GameObject powerUpPreviewGhost;
    private GameObject dangerPreviewObject;

    public bool IsAoeUnlocked => aoeUnlocked;
    public bool IsChargingAoe => isChargingAoe;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bossCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        originalScale = transform.localScale;

        if (originalScale == Vector3.zero)
            originalScale = Vector3.one;

        facingSign = originalScale.x < 0f ? -1 : 1;
        scaleSignY = originalScale.y < 0f ? -1f : 1f;
        scaleSignZ = originalScale.z < 0f ? -1f : 1f;

        currentScaleMagnitude = new Vector3(
            Mathf.Max(0.0001f, Mathf.Abs(originalScale.x)),
            Mathf.Max(0.0001f, Mathf.Abs(originalScale.y)),
            Mathf.Max(0.0001f, Mathf.Abs(originalScale.z))
        );

        ApplyCurrentScaleMagnitude();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        CreateBossSfxSource();

        EnemyObstacleSteering2D.ConfigureAIMovementBody(rb, true);

        RefreshNavigationFilter();
    }

    private void Start()
    {
        FindPlayerIfNeeded();
        RefreshNavigationFilter();

        lastPosition = rb.position;

        unstuckDirection =
            Random.Range(0, 2) == 0 ? -1 : 1;

        committedRouteSide = unstuckDirection;
        routeCommitTimer = 0f;

        spawnRoutine = StartCoroutine(SpawnEffectRoutine());

    }

    private IEnumerator SpawnEffectRoutine()
    {
        isSpawning = true;

        Vector3 targetScale = new Vector3(
            currentScaleMagnitude.x * facingSign,
            currentScaleMagnitude.y * scaleSignY,
            currentScaleMagnitude.z * scaleSignZ
        );

        transform.localScale = Vector3.zero;

        float duration = Mathf.Max(0f, spawnEffectDuration);

        if (duration > 0f)
        {
            float timer = 0f;

            while (timer < duration)
            {
                if (stopped || isSplitting)
                {
                    spawnRoutine = null;
                    yield break;
                }

                timer += Time.deltaTime;

                float t = Mathf.Clamp01(
                    timer / duration
                );

                // EnemyFollow / ProjectileEnemyFollow ile ayni smooth-step giris.
                t = t * t * (3f - 2f * t);

                transform.localScale = Vector3.Lerp(
                    Vector3.zero,
                    targetScale,
                    t
                );

                yield return null;
            }
        }

        ApplyCurrentScaleMagnitude();

        isSpawning = false;
        spawnRoutine = null;

        ResetStuckCheck();

        if (!stopped && !isSplitting)
            BeginAbsorptionOfCurrentStalkers();
    }

    private void Update()
    {
        UpdateBossSfxState();

        if (stopped || isSplitting || isSpawning)
            return;

        FindPlayerIfNeeded();

        if (playerMovement != null &&
            playerMovement.IsGameOver)
        {
            // Strike sonrasi danger fade devam ederken Boss sabit kalir
            // ve preview smooth sekilde tamamen kaybolur.
            if (isAoeFadingOut)
                return;

            StopBoss();
            return;
        }

        if (!aoeEnabled ||
            !aoeUnlocked ||
            isChargingAoe)
        {
            return;
        }

        if (aoeCooldownTimer > 0f)
        {
            aoeCooldownTimer -= Time.deltaTime;
            return;
        }

        aoeRoutine = StartCoroutine(AoeStrikeRoutine());
    }

    private void FixedUpdate()
    {
        if (stopped || isSplitting || isSpawning)
            return;

        FindPlayerIfNeeded();

        if (rb == null || player == null)
            return;

        if (isAoeFadingOut)
        {
            // Fade bitene kadar Boss tam strike merkezinde kilitli kalir.
            RestoreAoeChargeCenter();
            return;
        }

        if (playerMovement != null &&
            playerMovement.IsGameOver)
        {
            StopBoss();
            return;
        }

        if (isChargingAoe)
        {
            ApplyAoeChargeShake();
            return;
        }

        MoveBoss();
    }

    private void RefreshNavigationFilter()
    {
        navigationFilter = new ContactFilter2D();
        navigationFilter.SetLayerMask(
            EnemyObstacleSteering2D.BuildNavigationMask(
                (LayerMask)(solidLayers.value | obstacleLayer.value)
            )
        );
        navigationFilter.useLayerMask = true;
        navigationFilter.useTriggers = false;
    }

    private void FindPlayerIfNeeded()
    {
        if (player != null)
        {
            if (playerMovement == null)
                playerMovement = player.GetComponent<PlayerMovement>();

            if (playerArmor == null)
                playerArmor = player.GetComponent<PlayerArmor>();

            return;
        }

        GameObject foundPlayer =
            GameObject.FindGameObjectWithTag("Player");

        if (foundPlayer == null)
            return;

        player = foundPlayer.transform;
        playerMovement = foundPlayer.GetComponent<PlayerMovement>();
        playerArmor = foundPlayer.GetComponent<PlayerArmor>();
    }

    public void BeginAbsorptionOfCurrentStalkers()
    {
        if (absorptionStarted)
            return;

        absorptionStarted = true;
        pendingStalkerAbsorptions = 0;

        EnemyFollow[] stalkers = UnityFindCompat.FindObjectsByType<EnemyFollow>();

        for (int i = 0; i < stalkers.Length; i++)
        {
            EnemyFollow stalker = stalkers[i];

            if (stalker == null ||
                !stalker.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (stalker.BeginBossAbsorption(this))
                pendingStalkerAbsorptions++;
        }

        if (pendingStalkerAbsorptions <= 0)
        {
            // Boss sahneye geldiginde emilecek Stalker yoksa power-up
            // animasyonu/SFX'i oynatma. Boss direkt AOE kullanmaya hazir olsun.
            UnlockAoeImmediatelyWithoutPowerUp();
        }
    }

    private void UnlockAoeImmediatelyWithoutPowerUp()
    {
        if (aoeUnlocked || stopped || isSplitting)
            return;

        DestroyPowerUpPreviewGhost();

        aoeUnlocked = true;

        // Stalker yoksa sadece absorption/power-up asamasini atla.
        // Ilk AOE zamanlamasi normal sekilde firstAoeDelay kullanmaya devam eder.
        aoeCooldownTimer = Mathf.Max(0f, firstAoeDelay);
    }

    public void NotifyStalkerAbsorbed(EnemyFollow stalker)
    {
        if (!absorptionStarted || aoeUnlocked)
            return;

        pendingStalkerAbsorptions =
            Mathf.Max(0, pendingStalkerAbsorptions - 1);

        if (pendingStalkerAbsorptions == 0)
            CompleteAbsorptionPhase();
    }

    private void CompleteAbsorptionPhase()
    {
        if (aoeUnlocked || powerUpRoutine != null)
            return;

        powerUpRoutine = StartCoroutine(
            PowerUpAfterAbsorptionRoutine()
        );
    }

    private IEnumerator PowerUpAfterAbsorptionRoutine()
    {
        float scaleMultiplier =
            Mathf.Max(1f, powerUpScaleMultiplier);

        if (spriteRenderer != null &&
            powerUpPreviewFlashes > 0 &&
            scaleMultiplier > 1f)
        {
            powerUpPreviewGhost =
                CreatePowerUpPreviewGhost(scaleMultiplier);

            if (powerUpPreviewGhost != null)
            {
                for (int i = 0;
                     i < powerUpPreviewFlashes;
                     i++)
                {
                    if (stopped || isSplitting)
                        break;

                    powerUpPreviewGhost.SetActive(true);

                    yield return new WaitForSeconds(
                        Mathf.Max(
                            0.01f,
                            powerUpPreviewOnDuration
                        )
                    );

                    if (powerUpPreviewGhost == null)
                        break;

                    powerUpPreviewGhost.SetActive(false);

                    yield return new WaitForSeconds(
                        Mathf.Max(
                            0.01f,
                            powerUpPreviewOffDuration
                        )
                    );
                }
            }
        }

        DestroyPowerUpPreviewGhost();

        if (stopped || isSplitting)
        {
            powerUpRoutine = null;
            yield break;
        }

        PlayBossSfx(powerUpSfx);

        Vector3 startMagnitude = currentScaleMagnitude;
        Vector3 targetMagnitude =
            startMagnitude * scaleMultiplier;

        float growDuration =
            Mathf.Max(0.01f, powerUpGrowDuration);

        float timer = 0f;

        while (timer < growDuration)
        {
            if (stopped || isSplitting)
            {
                powerUpRoutine = null;
                yield break;
            }

            timer += Time.deltaTime;

            float t = Mathf.Clamp01(
                timer / growDuration
            );

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            currentScaleMagnitude = Vector3.Lerp(
                startMagnitude,
                targetMagnitude,
                smoothT
            );

            ApplyCurrentScaleMagnitude();

            yield return null;
        }

        currentScaleMagnitude = targetMagnitude;
        ApplyCurrentScaleMagnitude();

        aoeUnlocked = true;
        aoeCooldownTimer = Mathf.Max(0f, firstAoeDelay);
        powerUpRoutine = null;
    }

    private GameObject CreatePowerUpPreviewGhost(
        float scaleMultiplier)
    {
        if (spriteRenderer == null)
            return null;

        GameObject ghost =
            new GameObject("BossPowerUpScalePreview");

        ghost.transform.SetParent(transform, false);

        if (spriteRenderer.transform == transform)
        {
            ghost.transform.localPosition = Vector3.zero;
            ghost.transform.localRotation = Quaternion.identity;
            ghost.transform.localScale =
                Vector3.one * scaleMultiplier;
        }
        else
        {
            ghost.transform.position =
                spriteRenderer.transform.position;

            ghost.transform.rotation =
                spriteRenderer.transform.rotation;

            Vector3 desiredWorldScale =
                spriteRenderer.transform.lossyScale *
                scaleMultiplier;

            Vector3 parentWorldScale = transform.lossyScale;

            ghost.transform.localScale = new Vector3(
                SafeScaleDivision(
                    desiredWorldScale.x,
                    parentWorldScale.x
                ),
                SafeScaleDivision(
                    desiredWorldScale.y,
                    parentWorldScale.y
                ),
                SafeScaleDivision(
                    desiredWorldScale.z,
                    parentWorldScale.z
                )
            );
        }

        SpriteRenderer ghostRenderer =
            ghost.AddComponent<SpriteRenderer>();

        ghostRenderer.sprite = spriteRenderer.sprite;
        ghostRenderer.sharedMaterial =
            spriteRenderer.sharedMaterial;
        ghostRenderer.flipX = spriteRenderer.flipX;
        ghostRenderer.flipY = spriteRenderer.flipY;
        ghostRenderer.sortingLayerID =
            spriteRenderer.sortingLayerID;
        ghostRenderer.sortingOrder =
            spriteRenderer.sortingOrder + 1;

        Color ghostColor = spriteRenderer.color;
        ghostColor.a *= Mathf.Clamp01(powerUpPreviewAlpha);
        ghostRenderer.color = ghostColor;

        ghost.SetActive(false);
        return ghost;
    }

    private static float SafeScaleDivision(
        float numerator,
        float denominator)
    {
        if (Mathf.Abs(denominator) <= 0.0001f)
            return numerator;

        return numerator / denominator;
    }

    private void DestroyPowerUpPreviewGhost()
    {
        if (powerUpPreviewGhost == null)
            return;

        Destroy(powerUpPreviewGhost);
        powerUpPreviewGhost = null;
    }

    private void ApplyCurrentScaleMagnitude()
    {
        transform.localScale = new Vector3(
            currentScaleMagnitude.x * facingSign,
            currentScaleMagnitude.y * scaleSignY,
            currentScaleMagnitude.z * scaleSignZ
        );
    }

    private IEnumerator AoeStrikeRoutine()
    {
        if (isChargingAoe ||
            stopped ||
            isSplitting)
        {
            yield break;
        }

        isChargingAoe = true;
        GameAudioMixerController.SetBossDanger(this, true);
        aoeChargeProgress = 0f;
        aoeChargeCenter = rb != null
            ? rb.position
            : (Vector2)transform.position;

        ResetStuckCheck();
        routeCommitTimer = 0f;
        ZeroVelocity();

        // Boss 3 saniyelik charge boyunca sabit kalir.
        // Inspector'da eski bir deger kalmis olsa bile 3 saniyenin altina dusmez.
        float duration = Mathf.Max(3f, aoeChargeDuration);
        float timer = 0f;

        // Danger alanini charge basinda alpha 0 ile olustur.
        ShowGlobalDangerPreview();
        UpdateGlobalDangerPreviewAlpha(0f);

        SoundManager soundManager = SoundManager.Instance;

        PlayBossSfx(
            soundManager != null
                ? soundManager.bossAoeWarningSound
                : null,
            soundManager != null
                ? soundManager.bossAoeWarningVolume
                : 1f,
            duration
        );

        while (timer < duration)
        {
            if (stopped ||
                isSplitting ||
                (playerMovement != null && playerMovement.IsGameOver))
            {
                CancelAoeCharge();
                yield break;
            }

            timer += Time.deltaTime;

            aoeChargeProgress = Mathf.Clamp01(
                timer / duration
            );

            // Daha profesyonel gorunmesi icin alpha lineer patlamak yerine
            // yumusak bir 0 -> full gecis yapar.
            float visualProgress = Mathf.SmoothStep(
                0f,
                1f,
                aoeChargeProgress
            );

            UpdateGlobalDangerPreviewAlpha(visualProgress);

            yield return null;
        }

        aoeChargeProgress = 1f;
        UpdateGlobalDangerPreviewAlpha(1f);

        RestoreAoeChargeCenter();

        // HASAR TAM BU ANDA UYGULANIR.
        ExecuteGlobalAoeStrike();

        // Strike anini oyuncuya net gostermek icin Boss merkezinden
        // ekran disina dogru hizli bir shockwave halkasi yayilir.
        // Boss shockwave + fade tamamen bitene kadar hareket etmez.
        isAoeFadingOut = true;

        float strikeWaveDuration =
            Mathf.Max(0.05f, dangerStrikeWaveDuration);

        float strikeWaveTimer = 0f;

        UpdateGlobalDangerStrikeWave(0f);

        while (strikeWaveTimer < strikeWaveDuration)
        {
            if (stopped ||
                isSplitting ||
                (playerMovement != null && playerMovement.IsGameOver))
            {
                CancelAoeCharge();
                yield break;
            }

            RestoreAoeChargeCenter();

            strikeWaveTimer += Time.deltaTime;

            float strikeWaveProgress =
                Mathf.Clamp01(
                    strikeWaveTimer / strikeWaveDuration
                );

            UpdateGlobalDangerStrikeWave(
                Mathf.SmoothStep(
                    0f,
                    1f,
                    strikeWaveProgress
                )
            );

            yield return null;
        }

        // Shockwave ekran ucuna ulastiginda kapatilir,
        // ardindan mevcut red danger alan smooth fade-out yapar.
        DisableGlobalDangerStrikeWave();

        float fadeDuration =
            Mathf.Max(0.05f, dangerPreviewFadeOutDuration);

        float fadeTimer = 0f;

        while (fadeTimer < fadeDuration)
        {
            if (stopped ||
                isSplitting ||
                (playerMovement != null && playerMovement.IsGameOver))
            {
                CancelAoeCharge();
                yield break;
            }

            RestoreAoeChargeCenter();

            fadeTimer += Time.deltaTime;

            float fadeProgress = Mathf.Clamp01(
                fadeTimer / fadeDuration
            );

            float opacity =
                1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    fadeProgress
                );

            UpdateGlobalDangerPreviewOpacity(opacity);

            yield return null;
        }

        UpdateGlobalDangerPreviewOpacity(0f);
        HideDangerPreview();

        isAoeFadingOut = false;
        isChargingAoe = false;
        GameAudioMixerController.SetBossDanger(this, false);
        aoeChargeProgress = 0f;
        aoeCooldownTimer = Mathf.Max(0f, aoeCooldown);
        aoeRoutine = null;

        lastPosition = rb != null
            ? rb.position
            : (Vector2)transform.position;
    }

    private void ApplyAoeChargeShake()
    {
        if (rb == null)
            return;

        float shake = Mathf.Lerp(
            Mathf.Max(0f, normalShakeAmount),
            Mathf.Max(normalShakeAmount, aoeMaxShakeAmount),
            Mathf.SmoothStep(0f, 1f, aoeChargeProgress)
        );

        Vector2 offset = Random.insideUnitCircle * shake;
        rb.MovePosition(aoeChargeCenter + offset);
    }

    private void RestoreAoeChargeCenter()
    {
        if (rb != null)
        {
            rb.position = aoeChargeCenter;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        else
        {
            transform.position = aoeChargeCenter;
        }
    }

    private void ExecuteGlobalAoeStrike()
    {
        if (player == null || playerMovement == null)
            return;

        PlayBossSfx(aoeSfx);

        // The camera hit happens exactly on the shockwave/strike frame.
        CameraShake.Instance?.Shake(
            0.26f,
            0.20f
        );

        VibrationManager.Instance?.VibrateBossAoe();

        EnemyAreaStrikeUtility.ExecuteStrike(
            transform,
            player,
            playerMovement,
            playerArmor,
            GetAoeCoverLayers(),
            false,
            0f,
            "BOSS"
        );
    }

    private void ShowGlobalDangerPreview()
    {
        HideDangerPreview();

        Vector2 origin = rb != null
            ? aoeChargeCenter
            : (Vector2)transform.position;

        float bossVisualRadius = 0.35f;

        if (bossCollider != null)
        {
            Bounds bounds = bossCollider.bounds;

            bossVisualRadius = Mathf.Max(
                bounds.extents.x,
                bounds.extents.y
            );
        }

        // Shake sirasinda bile kirmizi mesh Boss sprite/collider ustune binmesin.
        float innerRadius =
            bossVisualRadius +
            Mathf.Max(0f, dangerPreviewInnerPadding) +
            Mathf.Max(0f, aoeMaxShakeAmount);

        dangerPreviewObject =
            EnemyDangerPreviewMesh.CreatePreview(
                origin,
                false,
                0f,
                GetAoeCoverLayers(),
                dangerPreviewColor,
                dangerPreviewRayCount,
                dangerPreviewSortingOrder,
                innerRadius,
                dangerPreviewRadialSegments,
                dangerPreviewEdgeSmoothingPasses,
                dangerPreviewInnerAlphaMultiplier,
                dangerPreviewWaveFrontWidth,
                dangerPreviewWaveFrontBoost,
                dangerPreviewInnerBrightness,
                dangerPreviewShader,
                dangerPreviewVisibilityRefreshRate,
                dangerPreviewCoverFeather
            );
    }

    private void UpdateGlobalDangerPreviewAlpha(
        float normalizedAlpha)
    {
        EnemyDangerPreviewMesh.SetPreviewAlpha(
            dangerPreviewObject,
            dangerPreviewColor,
            normalizedAlpha
        );
    }

    private void UpdateGlobalDangerPreviewOpacity(
        float normalizedOpacity)
    {
        EnemyDangerPreviewMesh.SetPreviewOpacity(
            dangerPreviewObject,
            normalizedOpacity
        );
    }

    private void UpdateGlobalDangerStrikeWave(
        float normalizedProgress)
    {
        EnemyDangerPreviewMesh.SetStrikeWave(
            dangerPreviewObject,
            normalizedProgress,
            dangerStrikeWaveWidth,
            dangerStrikeWaveBoost
        );
    }

    private void DisableGlobalDangerStrikeWave()
    {
        EnemyDangerPreviewMesh.SetStrikeWave(
            dangerPreviewObject,
            -1f,
            dangerStrikeWaveWidth,
            dangerStrikeWaveBoost
        );
    }

    private void HideDangerPreview()
    {
        EnemyDangerPreviewMesh.DestroyPreview(
            ref dangerPreviewObject
        );
    }

    private LayerMask GetAoeCoverLayers()
    {
        return aoeCoverLayers.value != 0
            ? aoeCoverLayers
            : obstacleLayer;
    }

    private void CancelAoeCharge()
    {
        HideDangerPreview();
        RestoreAoeChargeCenter();
        isAoeFadingOut = false;
        isChargingAoe = false;
        GameAudioMixerController.SetBossDanger(this, false);
        aoeChargeProgress = 0f;
        aoeRoutine = null;
    }

    private void MoveBoss()
    {
        Vector2 toPlayer =
            (Vector2)player.position - rb.position;

        if (toPlayer.sqrMagnitude <= 0.001f)
        {
            ResetStuckCheck();
            return;
        }

        Vector2 targetDirection = toPlayer.normalized;

        smoothedDirection =
            Vector2.Lerp(
                smoothedDirection == Vector2.zero
                    ? targetDirection
                    : smoothedDirection,
                targetDirection,
                directionSmoothness * Time.fixedDeltaTime
            ).normalized;

        // Kisa menzilli steering'e gelmeden once Boss kendi buyuk collider'i
        // icin uzun menzilli bir rota karari verir. Ozellikle obstacle + ekran
        // kenari arasindaki dar koridorlari daha yaklasmadan eler.
        Vector2 plannedDirection =
            GetBossPlannedDirection(
                smoothedDirection
            );

        if (plannedDirection.sqrMagnitude <= 0.001f)
            plannedDirection = smoothedDirection;

        FlipSprite(plannedDirection);

        Vector2 finalDirection = plannedDirection;

        if (unstuckTimer > 0f)
        {
            unstuckTimer -= Time.fixedDeltaTime;

            Vector2 sideDirection =
                GetPerpendicularDirection(
                    smoothedDirection,
                    unstuckDirection
                );

            finalDirection =
                (smoothedDirection +
                 sideDirection * unstuckSideForce).normalized;
        }

        bool moved = MoveWithCollision(finalDirection);
        HandleStuckCheck(moved);
    }

    private Vector2 GetBossPlannedDirection(
        Vector2 goalDirection)
    {
        if (goalDirection.sqrMagnitude <= 0.001f)
            return Vector2.zero;

        goalDirection.Normalize();

        float lookAhead =
            Mathf.Max(
                obstacleProbeDistance,
                routeLookAheadDistance
            );

        float directClearance =
            GetBossRouteClearance(
                goalDirection,
                lookAhead
            );

        bool directRouteClear =
            directClearance >= lookAhead - 0.02f;

        if (routeCommitTimer > 0f)
            routeCommitTimer -= Time.fixedDeltaTime;

        // Tam uzunlukta yol acildiysa artik detour'a gerek yok.
        if (directRouteClear)
        {
            routeCommitTimer = 0f;
            return goalDirection;
        }

        int preferredSide =
            committedRouteSide == 0
                ? (unstuckDirection >= 0 ? 1 : -1)
                : committedRouteSide;

        Vector2 bestDirection = Vector2.zero;
        float bestScore = float.NegativeInfinity;
        int bestSide = preferredSide;

        int samples =
            Mathf.Clamp(
                routeAngleSamples,
                4,
                9
            );

        // Commit aktifken once ayni taraftaki genis detour acilarini test et.
        // Bu sayede Boss evade'den sonra ayni dar araliga tekrar yonelmez.
        EvaluateSide(preferredSide, true);

        // Tercih edilen taraf tamamen kapaliysa diger tarafa izin ver.
        EvaluateSide(-preferredSide, false);

        if (bestDirection.sqrMagnitude > 0.001f)
        {
            bool changedSide =
                bestSide != committedRouteSide;

            if (routeCommitTimer <= 0f ||
                committedRouteSide == 0 ||
                changedSide)
            {
                committedRouteSide = bestSide;
                routeCommitTimer =
                    Mathf.Max(
                        0.1f,
                        routeCommitDuration
                    );
            }

            return bestDirection.normalized;
        }

        // Uzun menzilde iyi rota bulunamazsa mevcut local steering/stuck
        // sistemi son guvenlik kati olarak calismaya devam eder.
        return goalDirection;

        void EvaluateSide(
            int side,
            bool preferred)
        {
            side = side >= 0 ? 1 : -1;

            for (int i = 0; i < samples; i++)
            {
                float t =
                    samples <= 1
                        ? 0f
                        : i / (float)(samples - 1);

                // Boss buyuk oldugu icin kucuk 10-15 derecelik sapmalar yerine
                // obstacle'i gercekten dolanabilecek daha genis acilar kullan.
                float angle =
                    Mathf.Lerp(
                        28f,
                        118f,
                        t
                    );

                Vector2 candidate =
                    RotateDirection(
                        goalDirection,
                        angle * side
                    );

                float clearance =
                    GetBossRouteClearance(
                        candidate,
                        lookAhead
                    );

                // En az Boss'un kendi capina yakin bir ilerleme alani yoksa
                // bu rota bir "dar koridor" kabul edilir.
                float bossDiameter =
                    GetBossDiameter();

                float minimumUsefulClearance =
                    Mathf.Max(
                        0.8f,
                        bossDiameter * 0.9f
                    );

                if (clearance < minimumUsefulClearance)
                    continue;

                float clearanceScore =
                    Mathf.Clamp01(
                        clearance / lookAhead
                    );

                float goalProgress =
                    Vector2.Dot(
                        candidate,
                        goalDirection
                    );

                // Uzağa giden ve player yonunde makul ilerleme saglayan rota
                // tercih edilir. Commit edilen tarafa ek bonus verilir.
                float sideBonus =
                    side == committedRouteSide
                        ? routeSideCommitment
                        : 0f;

                if (routeCommitTimer > 0f &&
                    preferred)
                {
                    sideBonus += 0.18f;
                }

                float score =
                    clearanceScore * 2.2f +
                    goalProgress * 0.65f +
                    sideBonus;

                // Tam look-ahead boyunca acik rota ciddi bonus alir.
                if (clearance >= lookAhead - 0.02f)
                    score += 0.55f;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestDirection = candidate;
                bestSide = side;
            }
        }
    }

    private float GetBossRouteClearance(
        Vector2 direction,
        float requestedDistance)
    {
        if (direction.sqrMagnitude <= 0.001f ||
            requestedDistance <= 0f)
        {
            return 0f;
        }

        direction.Normalize();

        float physicsClearance =
            EnemyObstacleSteering2D.GetPathClearance(
                bossCollider,
                direction,
                navigationFilter,
                routePlanningHits,
                requestedDistance
            );

        float arenaClearance =
            GetArenaClearance(
                direction,
                requestedDistance
            );

        return Mathf.Min(
            physicsClearance,
            arenaClearance
        );
    }

    private float GetArenaClearance(
        Vector2 direction,
        float requestedDistance)
    {
        CameraWorldBounds bounds =
            CameraWorldBounds.Instance;

        if (bounds == null ||
            rb == null ||
            bossCollider == null)
        {
            return requestedDistance;
        }

        direction.Normalize();

        Bounds colliderBounds =
            bossCollider.bounds;

        float safeMinX =
            bounds.MinX +
            colliderBounds.extents.x +
            Mathf.Max(0f, arenaEdgePadding);

        float safeMaxX =
            bounds.MaxX -
            colliderBounds.extents.x -
            Mathf.Max(0f, arenaEdgePadding);

        float safeMinY =
            bounds.MinY +
            colliderBounds.extents.y +
            Mathf.Max(0f, arenaEdgePadding);

        float safeMaxY =
            bounds.MaxY -
            colliderBounds.extents.y -
            Mathf.Max(0f, arenaEdgePadding);

        Vector2 position =
            rb.position;

        float clearance =
            requestedDistance;

        const float epsilon =
            0.0001f;

        if (direction.x > epsilon)
        {
            float xDistance =
                (safeMaxX - position.x) /
                direction.x;

            clearance =
                Mathf.Min(
                    clearance,
                    Mathf.Max(0f, xDistance)
                );
        }
        else if (direction.x < -epsilon)
        {
            float xDistance =
                (safeMinX - position.x) /
                direction.x;

            clearance =
                Mathf.Min(
                    clearance,
                    Mathf.Max(0f, xDistance)
                );
        }

        if (direction.y > epsilon)
        {
            float yDistance =
                (safeMaxY - position.y) /
                direction.y;

            clearance =
                Mathf.Min(
                    clearance,
                    Mathf.Max(0f, yDistance)
                );
        }
        else if (direction.y < -epsilon)
        {
            float yDistance =
                (safeMinY - position.y) /
                direction.y;

            clearance =
                Mathf.Min(
                    clearance,
                    Mathf.Max(0f, yDistance)
                );
        }

        return Mathf.Clamp(
            clearance,
            0f,
            requestedDistance
        );
    }

    private float GetBossDiameter()
    {
        if (bossCollider == null)
            return 1f;

        Bounds bounds =
            bossCollider.bounds;

        return Mathf.Max(
            bounds.size.x,
            bounds.size.y
        );
    }

    private static Vector2 RotateDirection(
        Vector2 direction,
        float degrees)
    {
        float radians =
            degrees *
            Mathf.Deg2Rad;

        float sin =
            Mathf.Sin(radians);

        float cos =
            Mathf.Cos(radians);

        return new Vector2(
            direction.x * cos -
            direction.y * sin,
            direction.x * sin +
            direction.y * cos
        ).normalized;
    }

    private bool MoveWithCollision(Vector2 direction)
    {
        float movementDistance =
            speed * Time.fixedDeltaTime;

        if (EnemyObstacleSteering2D.TryGetOverlapRecovery(
                bossCollider,
                navigationFilter,
                out Vector2 overlapDirection,
                out float penetrationDepth))
        {
            float recoveryDistance =
                EnemyObstacleSteering2D.GetOverlapRecoveryDistance(
                    penetrationDepth,
                    movementDistance,
                    castSkin
                );

            rb.MovePosition(
                rb.position +
                overlapDirection * recoveryDistance
            );

            return true;
        }

        if (direction.sqrMagnitude <= 0.001f)
            return false;

        Vector2 steeredDirection =
            EnemyObstacleSteering2D.GetSteeredDirection(
                bossCollider,
                direction,
                direction,
                navigationFilter,
                avoidanceHits,
                obstacleProbeDistance,
                movementDistance,
                castSkin,
                slideDirectionAttempts,
                obstacleOutwardBias,
                ref unstuckDirection
            );

        if (steeredDirection.sqrMagnitude <= 0.001f)
            return false;

        Vector2 intendedMovement =
            steeredDirection * movementDistance;

        Vector2 shakeOffset =
            Random.insideUnitCircle * Mathf.Max(0f, normalShakeAmount);

        Vector2 movement = intendedMovement + shakeOffset;

        if (EnemyObstacleSteering2D.MoveDisplacementWithPhysicsSlide(
                rb,
                bossCollider,
                movement,
                Time.fixedDeltaTime,
                navigationFilter,
                7))
        {
            return true;
        }

        if (TrySlideAroundObstacle(
                steeredDirection,
                intendedMovement.magnitude))
        {
            return true;
        }

        unstuckDirection *= -1;
        return false;
    }

    private bool TrySlideAroundObstacle(
        Vector2 forwardDirection,
        float movementDistance)
    {
        if (movementDistance <= 0f)
            return false;

        Vector2 leftDirection =
            GetPerpendicularDirection(forwardDirection, 1);

        Vector2 rightDirection =
            GetPerpendicularDirection(forwardDirection, -1);

        for (int attempt = 0;
             attempt < slideDirectionAttempts;
             attempt++)
        {
            float blend =
                (attempt + 1f) / slideDirectionAttempts;

            Vector2 firstSide =
                unstuckDirection > 0
                    ? leftDirection
                    : rightDirection;

            Vector2 secondSide =
                unstuckDirection > 0
                    ? rightDirection
                    : leftDirection;

            Vector2 firstDirection =
                Vector2.Lerp(
                    forwardDirection,
                    firstSide,
                    blend
                ).normalized;

            Vector2 firstMovement =
                firstDirection * movementDistance;

            if (CanMove(firstMovement))
            {
                rb.MovePosition(rb.position + firstMovement);
                return true;
            }

            Vector2 secondDirection =
                Vector2.Lerp(
                    forwardDirection,
                    secondSide,
                    blend
                ).normalized;

            Vector2 secondMovement =
                secondDirection * movementDistance;

            if (CanMove(secondMovement))
            {
                rb.MovePosition(rb.position + secondMovement);
                unstuckDirection *= -1;
                return true;
            }
        }

        return false;
    }

    private Vector2 GetPerpendicularDirection(
        Vector2 direction,
        int side)
    {
        return new Vector2(-direction.y, direction.x) * side;
    }

    private bool CanMove(Vector2 movement)
    {
        if (bossCollider == null ||
            movement.sqrMagnitude <= 0.001f)
        {
            return true;
        }

        float movementDistance =
            movement.magnitude;

        float arenaClearance =
            GetArenaClearance(
                movement.normalized,
                movementDistance +
                Mathf.Max(castSkin, 0f)
            );

        if (arenaClearance <
            movementDistance - 0.001f)
        {
            return false;
        }

        int hitCount =
            bossCollider.Cast(
                movement.normalized,
                navigationFilter,
                castHits,
                movement.magnitude + Mathf.Max(castSkin, 0f)
            );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = castHits[i].collider;

            if (hitCollider == null ||
                hitCollider == bossCollider)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void HandleStuckCheck(bool attemptedMove)
    {
        stuckTimer += Time.fixedDeltaTime;

        float effectiveStuckCheckTime = Mathf.Min(
            Mathf.Max(0.05f, stuckCheckTime),
            0.25f
        );

        if (stuckTimer < effectiveStuckCheckTime)
            return;

        float movedDistanceSqr =
            (rb.position - lastPosition).sqrMagnitude;

        float requiredDistanceSqr =
            stuckDistance * stuckDistance;

        if (movedDistanceSqr < requiredDistanceSqr)
        {
            Vector2 escapeDirection = GetEscapeDirection();

            if (escapeDirection.sqrMagnitude <= 0.001f)
            {
                Vector2 playerDirection =
                    player != null
                        ? ((Vector2)player.position - rb.position).normalized
                        : Vector2.right;

                Vector2 sideDirection =
                    GetPerpendicularDirection(
                        playerDirection,
                        unstuckDirection
                    );

                escapeDirection =
                    (sideDirection +
                     Random.insideUnitCircle * 0.35f).normalized;
            }

            Vector2 escapeMovement =
                escapeDirection *
                speed *
                escapeSpeedMultiplier *
                Time.fixedDeltaTime;

            if (CanMove(escapeMovement))
            {
                rb.MovePosition(rb.position + escapeMovement);
            }
            else
            {
                unstuckDirection *= -1;
            }

            unstuckTimer = unstuckDuration;
        }

        lastPosition = rb.position;
        stuckTimer = 0f;
    }

    private void ResetStuckCheck()
    {
        stuckTimer = 0f;

        if (rb != null)
            lastPosition = rb.position;
    }

    private Vector2 GetEscapeDirection()
    {
        ContactFilter2D obstacleFilter =
            new ContactFilter2D();

        obstacleFilter.SetLayerMask(
            obstacleLayer | solidLayers
        );

        obstacleFilter.useLayerMask = true;
        obstacleFilter.useTriggers = true;

        int hitCount =
            Physics2D.OverlapCircle(
                rb.position,
                escapeCheckRadius,
                obstacleFilter,
                escapeHits
            );

        if (hitCount <= 0)
            return Vector2.zero;

        Vector2 escapeDirection = Vector2.zero;
        int validHitCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = escapeHits[i];

            if (hit == null || hit == bossCollider)
                continue;

            Vector2 closestPoint =
                hit.ClosestPoint(rb.position);

            Vector2 awayFromObstacle =
                rb.position - closestPoint;

            if (awayFromObstacle.sqrMagnitude <= 0.001f)
            {
                awayFromObstacle =
                    rb.position - (Vector2)hit.bounds.center;
            }

            if (awayFromObstacle.sqrMagnitude <= 0.001f)
                continue;

            float distance = awayFromObstacle.magnitude;
            float weight = 1f / Mathf.Max(distance, 0.05f);

            escapeDirection +=
                awayFromObstacle.normalized * weight;

            validHitCount++;
        }

        if (validHitCount <= 0 ||
            escapeDirection.sqrMagnitude <= 0.001f)
        {
            return Vector2.zero;
        }

        return escapeDirection.normalized;
    }

    private void FlipSprite(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) <= 0.01f)
            return;

        facingSign = direction.x > 0f ? 1 : -1;
        ApplyCurrentScaleMagnitude();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (stopped || isSplitting || isSpawning)
            return;

        if (collision == null)
            return;

        GameObject playerObject =
            FindPlayerObjectInParents(collision.gameObject);

        if (playerObject == null)
            return;

        if (playerMovement == null)
            playerMovement = playerObject.GetComponent<PlayerMovement>();

        if (playerArmor == null)
            playerArmor = playerObject.GetComponent<PlayerArmor>();

        if (playerMovement == null ||
            playerMovement.IsGameOver)
        {
            return;
        }

        if (playerArmor != null && playerArmor.IsImmune)
            return;

        if (playerArmor != null && playerArmor.HasArmor)
        {
            playerArmor.BreakArmor();
            StatsManager.AddArmorEnemyKill();

            if (canSplit)
                StartCoroutine(SplitRoutine());
            else
                Destroy(gameObject);

            return;
        }

        StopBossMovement();
        playerMovement.GameOver("BOSS");
    }

    private GameObject FindPlayerObjectInParents(GameObject hitObject)
    {
        if (hitObject == null)
            return null;

        Transform current = hitObject.transform;

        while (current != null)
        {
            if (current.CompareTag("Player"))
                return current.gameObject;

            current = current.parent;
        }

        return null;
    }

    private IEnumerator SplitRoutine()
    {
        if (isSplitting || stopped)
            yield break;

        isSplitting = true;

        if (aoeRoutine != null)
        {
            StopCoroutine(aoeRoutine);
            aoeRoutine = null;
        }

        if (powerUpRoutine != null)
        {
            StopCoroutine(powerUpRoutine);
            powerUpRoutine = null;
        }

        StopBossSfx();
        DestroyPowerUpPreviewGhost();
        HideDangerPreview();

        if (isChargingAoe)
            RestoreAoeChargeCenter();

        isAoeFadingOut = false;
        isChargingAoe = false;
        aoeChargeProgress = 0f;

        Vector3 splitCenter = transform.position;
        Vector3 splitStartScale = transform.localScale;

        Color splitStartColor =
            spriteRenderer != null
                ? spriteRenderer.color
                : originalColor;

        if (bossCollider != null)
            bossCollider.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        float safeFlashSpeed = Mathf.Max(flashSpeed, 0.01f);

        // Armor Break must finish first. The boss keeps shaking during that
        // time, then the actual split cue plays exactly when the split happens.
        float armorBreakDuration =
            SoundManager.Instance != null
                ? SoundManager.Instance.ArmorBreakSoundDuration
                : 0f;

        float effectiveSplitDelay = Mathf.Max(
            splitDelay,
            armorBreakDuration
        );

        float timer = 0f;

        while (timer < effectiveSplitDelay)
        {
            if (playerMovement != null &&
                playerMovement.IsGameOver)
            {
                StopBoss();
                yield break;
            }

            timer += Time.deltaTime;

            Vector3 shakeOffset = new Vector3(
                Random.Range(-splitShakeAmount, splitShakeAmount),
                Random.Range(-splitShakeAmount, splitShakeAmount),
                0f
            );

            transform.position = splitCenter + shakeOffset;

            if (spriteRenderer != null)
            {
                float flash = Mathf.PingPong(
                    timer / safeFlashSpeed,
                    1f
                );

                spriteRenderer.color = Color.Lerp(
                    splitStartColor,
                    splitFlashColor,
                    flash
                );
            }

            float scaleJitter = Random.Range(
                splitScaleMin,
                splitScaleMax
            );

            transform.localScale = splitStartScale * scaleJitter;
            yield return null;
        }

        transform.position = splitCenter;
        transform.localScale = splitStartScale;

        if (spriteRenderer != null)
            spriteRenderer.color = splitStartColor;

        SoundManager soundManager = SoundManager.Instance;

        PlayBossSfx(
            soundManager != null
                ? soundManager.bossSplitSound
                : null,
            soundManager != null
                ? soundManager.bossSplitVolume
                : 1f
        );

        CameraShake.Instance?.Shake(
            0.22f,
            0.16f
        );

        VibrationManager.Instance?.VibrateBossSplit();

        SpawnMiniBosses(splitCenter);

        if (splitDisappearDuration > 0f)
        {
            yield return SplitDisappearRoutine(
                splitStartScale,
                splitStartColor
            );
        }

        // Split SFX Boss objesiyle birlikte yarida kesilmesin.
        // Pause sirasinda source Pause olur; resume'da kaldigi yerden devam eder.
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        while (bossSfxSource != null &&
               (bossSfxSource.isPlaying ||
                bossSfxPausedByGame))
        {
            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator SplitDisappearRoutine(
        Vector3 startScale,
        Color startColor)
    {
        float timer = 0f;

        while (timer < splitDisappearDuration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(
                timer / splitDisappearDuration
            );

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.localScale = Vector3.Lerp(
                startScale,
                Vector3.zero,
                smoothT
            );

            if (spriteRenderer != null)
            {
                Color color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, smoothT);
                spriteRenderer.color = color;
            }

            yield return null;
        }
    }

    private void SpawnMiniBosses(Vector2 bossPosition)
    {
        if (miniBossPrefab == null || player == null)
            return;

        StatsManager.AddBossSplit();

        Vector2 playerDirection =
            (Vector2)player.position - bossPosition;

        if (playerDirection.sqrMagnitude <= 0.001f)
            playerDirection = Vector2.right;
        else
            playerDirection.Normalize();

        Vector2 splitDirection =
            Mathf.Abs(playerDirection.x) > Mathf.Abs(playerDirection.y)
                ? Vector2.up
                : Vector2.right;

        Vector2 firstPosition =
            FindSafeMiniBossPosition(
                bossPosition + splitDirection * splitDistance,
                splitDirection
            );

        Vector2 secondPosition =
            FindSafeMiniBossPosition(
                bossPosition - splitDirection * splitDistance,
                -splitDirection
            );

        CreateMiniBoss(
            firstPosition,
            true,
            NormalEnemyPursuitRole.Pursuer,
            0f
        );

        CreateMiniBoss(
            secondPosition,
            false,
            NormalEnemyPursuitRole.Interceptor,
            miniBossAoeStagger
        );
    }

    private Vector2 FindSafeMiniBossPosition(
        Vector2 desiredPosition,
        Vector2 searchDirection)
    {
        Vector2 clampedPosition =
            ClampToCameraBounds(desiredPosition);

        if (IsMiniBossPositionClear(clampedPosition))
            return clampedPosition;

        const int attempts = 8;

        for (int i = 1; i <= attempts; i++)
        {
            float distance = 0.25f * i;

            Vector2 candidate = ClampToCameraBounds(
                clampedPosition +
                searchDirection.normalized * distance
            );

            if (IsMiniBossPositionClear(candidate))
                return candidate;

            Vector2 sideDirection = new Vector2(
                -searchDirection.y,
                searchDirection.x
            );

            candidate = ClampToCameraBounds(
                clampedPosition + sideDirection * distance
            );

            if (IsMiniBossPositionClear(candidate))
                return candidate;

            candidate = ClampToCameraBounds(
                clampedPosition - sideDirection * distance
            );

            if (IsMiniBossPositionClear(candidate))
                return candidate;
        }

        return clampedPosition;
    }

    private bool IsMiniBossPositionClear(Vector2 position)
    {
        Collider2D hit = Physics2D.OverlapCircle(
            position,
            0.35f,
            solidLayers
        );

        return hit == null;
    }

    private Vector2 ClampToCameraBounds(Vector2 position)
    {
        if (CameraWorldBounds.Instance == null)
            return position;

        float padding = Mathf.Max(
            splitDistance * 0.25f,
            0.7f
        );

        position.x = Mathf.Clamp(
            position.x,
            CameraWorldBounds.Instance.MinX + padding,
            CameraWorldBounds.Instance.MaxX - padding
        );

        position.y = Mathf.Clamp(
            position.y,
            CameraWorldBounds.Instance.MinY + padding,
            CameraWorldBounds.Instance.MaxY - padding
        );

        return position;
    }

    private void CreateMiniBoss(
        Vector2 spawnPosition,
        bool canTargetClone,
        NormalEnemyPursuitRole role,
        float extraAoeDelay)
    {
        GameObject miniBoss = Instantiate(
            miniBossPrefab,
            spawnPosition,
            Quaternion.identity
        );

        MiniBossFollow miniScript =
            miniBoss.GetComponent<MiniBossFollow>();

        if (miniScript == null)
            return;

        miniScript.player = player;
        miniScript.solidLayers = solidLayers;
        miniScript.obstacleLayer = obstacleLayer;
        miniScript.speed = miniBossSpeed;
        miniScript.canTargetClone = canTargetClone;
        miniScript.ConfigurePursuitRole(role);
        miniScript.AddInitialAoeDelay(extraAoeDelay);
    }

    private void CreateBossSfxSource()
    {
        bossSfxSource = gameObject.AddComponent<AudioSource>();
        bossSfxSource.playOnAwake = false;
        bossSfxSource.loop = false;
        bossSfxSource.volume = SoundManager.SFXVolume;

        SoundManager manager = SoundManager.Instance;

        if (manager != null)
        {
            manager.ConfigureWorldAudioSource(bossSfxSource);

            AudioSource template = manager.sfxSource;

            if (template != null)
            {
                bossSfxSource.outputAudioMixerGroup =
                    template.outputAudioMixerGroup;

                bossSfxSource.priority =
                    template.priority;

                bossSfxSource.bypassEffects =
                    template.bypassEffects;

                bossSfxSource.bypassListenerEffects =
                    template.bypassListenerEffects;

                bossSfxSource.bypassReverbZones =
                    template.bypassReverbZones;

                bossSfxSource.ignoreListenerVolume =
                    template.ignoreListenerVolume;
            }
        }
        else
        {
            SoundManager.ConfigureAsWorld3D(
                bossSfxSource
            );
        }

        GameAudioMixerController.Route(
            bossSfxSource,
            GameAudioMixerController.AudioBus.CriticalSFX
        );

        // Boss gameplay SFX'leri pause'dan muaf olmamali.
        bossSfxSource.ignoreListenerPause = false;
    }

    private void PlayBossSfx(
        AudioClip clip,
        float volumeMultiplier = 1f,
        float scaledDuration = 0f)
    {
        if (clip == null ||
            GameStateManager.IsGameplayEnded)
        {
            return;
        }

        if (bossSfxSource == null)
            CreateBossSfxSource();

        bossSfxVolumeMultiplier =
            Mathf.Max(0f, volumeMultiplier);

        bossSfxFollowsGameTime = scaledDuration > 0.01f;
        bossSfxBasePitch = bossSfxFollowsGameTime
            ? Mathf.Clamp(clip.length / scaledDuration, 0.25f, 3f)
            : 1f;

        bossSfxPausedByGame = false;

        bossSfxSource.Stop();
        bossSfxSource.clip = clip;
        bossSfxSource.pitch = GetBossSfxPitch();
        ApplyBossSfxVolume();
        bossSfxSource.Play();

        if (Time.timeScale <= 0f)
        {
            bossSfxSource.Pause();
            bossSfxPausedByGame = true;
        }
    }

    private void UpdateBossSfxState()
    {
        if (bossSfxSource == null)
            return;

        ApplyBossSfxVolume();
        bossSfxSource.pitch = GetBossSfxPitch();

        if (GameStateManager.IsGameplayEnded)
        {
            StopBossSfx();
            return;
        }

        bool shouldPause =
            Time.timeScale <= 0f;

        if (shouldPause)
        {
            if (!bossSfxPausedByGame &&
                bossSfxSource.isPlaying)
            {
                bossSfxSource.Pause();
                bossSfxPausedByGame = true;
            }

            return;
        }

        if (bossSfxPausedByGame)
        {
            bossSfxSource.UnPause();
            bossSfxPausedByGame = false;
        }
    }


    private float GetBossSfxPitch()
    {
        if (!bossSfxFollowsGameTime)
            return Mathf.Clamp(bossSfxBasePitch, 0.01f, 3f);

        float gameplayScale = 1f;

        if (SlowPowerUp.isSlowActive)
        {
            gameplayScale = Mathf.Clamp(
                SlowPowerUp.currentSlowMultiplier,
                0.01f,
                1f
            );
        }

        return Mathf.Clamp(
            bossSfxBasePitch * gameplayScale,
            0.01f,
            3f
        );
    }

    private void ApplyBossSfxVolume()
    {
        if (bossSfxSource == null)
            return;

        bossSfxSource.volume =
            SoundManager.SFXVolume *
            bossSfxVolumeMultiplier;
    }

    private void StopBossSfx()
    {
        if (bossSfxSource == null)
            return;

        bossSfxSource.Stop();
        bossSfxSource.clip = null;
        bossSfxPausedByGame = false;
        bossSfxBasePitch = 1f;
        bossSfxFollowsGameTime = false;
    }

    private void ZeroVelocity()
    {
        if (rb == null)
            return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private void StopBossMovement()
    {
        speed = 0f;
        ZeroVelocity();
    }

    public void StopForGameEnd()
    {
        StopBoss();
    }

    private void StopBoss()
    {
        if (stopped)
            return;

        stopped = true;
        isSplitting = false;
        isSpawning = false;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        if (aoeRoutine != null)
        {
            StopCoroutine(aoeRoutine);
            aoeRoutine = null;
        }

        if (powerUpRoutine != null)
        {
            StopCoroutine(powerUpRoutine);
            powerUpRoutine = null;
        }

        StopBossSfx();
        DestroyPowerUpPreviewGhost();
        HideDangerPreview();

        if (isChargingAoe)
            RestoreAoeChargeCenter();

        isAoeFadingOut = false;
        isChargingAoe = false;
        GameAudioMixerController.SetBossDanger(this, false);
        aoeChargeProgress = 0f;

        StopBossMovement();

        if (bossCollider != null)
            bossCollider.enabled = false;

        enabled = false;
    }

    private void OnDisable()
    {
        if (!stopped)
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }

            if (aoeRoutine != null)
            {
                StopCoroutine(aoeRoutine);
                aoeRoutine = null;
            }

            if (powerUpRoutine != null)
            {
                StopCoroutine(powerUpRoutine);
                powerUpRoutine = null;
            }

            StopBossSfx();
            DestroyPowerUpPreviewGhost();
            HideDangerPreview();

            isSpawning = false;
            isAoeFadingOut = false;
            isChargingAoe = false;
            GameAudioMixerController.SetBossDanger(this, false);
            aoeChargeProgress = 0f;
        }
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        normalShakeAmount = Mathf.Max(0f, normalShakeAmount);
        spawnEffectDuration = Mathf.Max(0f, spawnEffectDuration);
        aoeCooldown = Mathf.Max(0f, aoeCooldown);
        firstAoeDelay = Mathf.Max(0f, firstAoeDelay);
        aoeChargeDuration = Mathf.Max(3f, aoeChargeDuration);
        aoeMaxShakeAmount = Mathf.Max(normalShakeAmount, aoeMaxShakeAmount);
        powerUpScaleMultiplier = Mathf.Max(1f, powerUpScaleMultiplier);
        powerUpPreviewFlashes = Mathf.Clamp(powerUpPreviewFlashes, 0, 4);
        powerUpPreviewOnDuration = Mathf.Max(0.01f, powerUpPreviewOnDuration);
        powerUpPreviewOffDuration = Mathf.Max(0.01f, powerUpPreviewOffDuration);
        powerUpGrowDuration = Mathf.Max(0.01f, powerUpGrowDuration);
        dangerStrikeWaveDuration =
            Mathf.Max(0.05f, dangerStrikeWaveDuration);
        dangerStrikeWaveWidth =
            Mathf.Clamp(dangerStrikeWaveWidth, 0.03f, 0.30f);
        dangerStrikeWaveBoost =
            Mathf.Clamp(dangerStrikeWaveBoost, 0f, 3f);
        dangerPreviewFadeOutDuration =
            Mathf.Max(0.05f, dangerPreviewFadeOutDuration);
        dangerPreviewInnerPadding = Mathf.Max(0f, dangerPreviewInnerPadding);
        dangerPreviewInnerAlphaMultiplier =
            Mathf.Clamp01(dangerPreviewInnerAlphaMultiplier);
        dangerPreviewWaveFrontWidth =
            Mathf.Clamp(dangerPreviewWaveFrontWidth, 0.03f, 0.3f);
        dangerPreviewWaveFrontBoost =
            Mathf.Max(0f, dangerPreviewWaveFrontBoost);
        dangerPreviewInnerBrightness =
            Mathf.Clamp(dangerPreviewInnerBrightness, 0.1f, 1f);
        dangerPreviewRadialSegments =
            Mathf.Clamp(dangerPreviewRadialSegments, 6, 24);
        dangerPreviewEdgeSmoothingPasses =
            Mathf.Clamp(dangerPreviewEdgeSmoothingPasses, 0, 4);
        dangerPreviewRayCount =
            Mathf.Clamp(dangerPreviewRayCount, 360, 960);
        dangerPreviewVisibilityRefreshRate =
            Mathf.Clamp(dangerPreviewVisibilityRefreshRate, 5f, 30f);
        dangerPreviewCoverFeather =
            Mathf.Clamp(dangerPreviewCoverFeather, 0.01f, 0.35f);
        routeLookAheadDistance =
            Mathf.Max(1f, routeLookAheadDistance);
        routeCommitDuration =
            Mathf.Max(0.1f, routeCommitDuration);
        routeAngleSamples =
            Mathf.Clamp(routeAngleSamples, 4, 9);
        arenaEdgePadding =
            Mathf.Max(0f, arenaEdgePadding);
        routeSideCommitment =
            Mathf.Clamp01(routeSideCommitment);
        slideDirectionAttempts = Mathf.Clamp(slideDirectionAttempts, 1, 8);
    }
}