using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-40)]
public class HUDPlayerOcclusionController : MonoBehaviour
{
    [Header("Occlusion")]
    [Range(0f, 1f)]
    [SerializeField]
    private float minimumAlpha = 0.20f;

    [Tooltip("Player sprite sinirinin disina eklenen ekran-piksel payi.")]
    [Min(0f)]
    [SerializeField]
    private float paddingPixelsAt1080p = 4f;

    [Tooltip("Alpha gecisinin yumusaklik genisligi (1080p referans).")]
    [Min(0f)]
    [SerializeField]
    private float featherPixelsAt1080p = 10f;

    [Tooltip("Image/RawImage icin lokal fade mesh kalitesi. 2 = mobil icin iyi denge.")]
    [Range(0, 3)]
    [SerializeField]
    private int graphicSubdivisionDepth = 2;

    private Transform playerRoot;
    private SpriteRenderer playerSpriteRenderer;
    private Camera gameplayCamera;
    private Canvas hudCanvas;

    private Vector2 playerScreenCenter;
    private Vector2 playerScreenRadii;
    private float featherPixels;
    private int cachedFrame = -1;
    private bool hasValidOcclusionArea;

    private readonly HashSet<Graphic> registeredGraphics =
        new HashSet<Graphic>();

    public int GraphicSubdivisionDepth =>
        graphicSubdivisionDepth;

    public bool IsOcclusionActive =>
        EnsureOcclusionArea();

    public void Configure(
        Transform player,
        Canvas canvas,
        HUDIntroAnimator introAnimator,
        params GameObject[] hudRoots)
    {
        playerRoot = player;
        hudCanvas = canvas;

        ResolvePlayerRenderer();
        ResolveGameplayCamera();

        registeredGraphics.Clear();

        if (hudRoots != null)
        {
            for (int i = 0; i < hudRoots.Length; i++)
                RegisterHUDRoot(hudRoots[i]);
        }

        if (introAnimator != null &&
            introAnimator.hudItems != null)
        {
            for (int i = 0;
                 i < introAnimator.hudItems.Length;
                 i++)
            {
                HUDIntroAnimator.HUDItem item =
                    introAnimator.hudItems[i];

                if (item != null)
                    RegisterHUDRoot(item.target);
            }
        }

        cachedFrame = -1;
    }

    public void RegisterHUDRoot(GameObject root)
    {
        if (root == null)
            return;

        Graphic[] graphics =
            root.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null)
                continue;

            // Runtime-created HUDs (Current Level HUD gibi) intro sirasinda
            // disable/enable olabilir. Daha once kayitli olsa bile effect'i
            // yeniden Configure ederek TMP pre-render baglantisini garanti et.
            registeredGraphics.Add(graphic);

            TMP_Text tmpText = graphic as TMP_Text;

            if (tmpText != null)
            {
                HUDPlayerOcclusionTMP effect =
                    graphic.GetComponent<HUDPlayerOcclusionTMP>();

                if (effect == null)
                {
                    effect =
                        graphic.gameObject.AddComponent
                            <HUDPlayerOcclusionTMP>();
                }

                effect.Configure(this, tmpText);
                continue;
            }

            if (graphic is Image ||
                graphic is RawImage)
            {
                HUDPlayerOcclusionGraphicEffect effect =
                    graphic.GetComponent
                        <HUDPlayerOcclusionGraphicEffect>();

                if (effect == null)
                {
                    effect =
                        graphic.gameObject.AddComponent
                            <HUDPlayerOcclusionGraphicEffect>();
                }

                effect.Configure(this, graphic);
            }
        }
    }

    public bool IsPotentiallyOverlapping(
        RectTransform rectTransform)
    {
        if (rectTransform == null ||
            !EnsureOcclusionArea())
        {
            return false;
        }

        Vector3[] worldCorners = new Vector3[4];
        rectTransform.GetWorldCorners(worldCorners);

        Camera uiCamera = GetUICamera();

        Vector2 min = new Vector2(
            float.PositiveInfinity,
            float.PositiveInfinity
        );

        Vector2 max = new Vector2(
            float.NegativeInfinity,
            float.NegativeInfinity
        );

        for (int i = 0; i < 4; i++)
        {
            Vector2 screenPoint =
                RectTransformUtility.WorldToScreenPoint(
                    uiCamera,
                    worldCorners[i]
                );

            min = Vector2.Min(min, screenPoint);
            max = Vector2.Max(max, screenPoint);
        }

        Vector2 outerRadii =
            playerScreenRadii +
            Vector2.one * featherPixels;

        Vector2 playerMin =
            playerScreenCenter - outerRadii;

        Vector2 playerMax =
            playerScreenCenter + outerRadii;

        return
            max.x >= playerMin.x &&
            min.x <= playerMax.x &&
            max.y >= playerMin.y &&
            min.y <= playerMax.y;
    }

    public float GetAlphaMultiplier(
        Vector2 screenPoint)
    {
        if (!EnsureOcclusionArea())
            return 1f;

        float radiusX =
            Mathf.Max(1f, playerScreenRadii.x);

        float radiusY =
            Mathf.Max(1f, playerScreenRadii.y);

        Vector2 delta =
            screenPoint - playerScreenCenter;

        float normalizedDistance = Mathf.Sqrt(
            (delta.x * delta.x) /
            (radiusX * radiusX) +
            (delta.y * delta.y) /
            (radiusY * radiusY)
        );

        float referenceRadius =
            Mathf.Max(1f, Mathf.Min(radiusX, radiusY));

        float featherNormalized =
            Mathf.Clamp(
                featherPixels / referenceRadius,
                0.02f,
                0.75f
            );

        float fadeStart =
            Mathf.Max(0f, 1f - featherNormalized);

        float fadeEnd =
            1f + featherNormalized;

        float t = Mathf.InverseLerp(
            fadeStart,
            fadeEnd,
            normalizedDistance
        );

        t = t * t * (3f - 2f * t);

        return Mathf.Lerp(
            minimumAlpha,
            1f,
            t
        );
    }

    public Vector2 WorldToUIScreenPoint(
        Vector3 worldPoint)
    {
        return RectTransformUtility.WorldToScreenPoint(
            GetUICamera(),
            worldPoint
        );
    }

    private bool EnsureOcclusionArea()
    {
        if (cachedFrame == Time.frameCount)
            return hasValidOcclusionArea;

        cachedFrame = Time.frameCount;
        hasValidOcclusionArea = false;

        if (playerRoot == null ||
            !playerRoot.gameObject.activeInHierarchy ||
            !GameStateManager.IsGameplayStarted ||
            GameStateManager.IsGameplayEnded)
        {
            return false;
        }

        if (playerSpriteRenderer == null)
            ResolvePlayerRenderer();

        if (gameplayCamera == null ||
            !gameplayCamera.isActiveAndEnabled)
        {
            ResolveGameplayCamera();
        }

        if (gameplayCamera == null)
            return false;

        float resolutionScale = Mathf.Clamp(
            Screen.height / 1080f,
            0.6f,
            2f
        );

        float padding =
            paddingPixelsAt1080p * resolutionScale;

        featherPixels =
            featherPixelsAt1080p * resolutionScale;

        if (playerSpriteRenderer != null &&
            playerSpriteRenderer.enabled)
        {
            Bounds bounds = playerSpriteRenderer.bounds;

            Vector3 minWorld = bounds.min;
            Vector3 maxWorld = bounds.max;

            Vector3 a = gameplayCamera.WorldToScreenPoint(
                new Vector3(
                    minWorld.x,
                    minWorld.y,
                    bounds.center.z
                )
            );

            Vector3 b = gameplayCamera.WorldToScreenPoint(
                new Vector3(
                    maxWorld.x,
                    maxWorld.y,
                    bounds.center.z
                )
            );

            playerScreenCenter =
                (new Vector2(a.x, a.y) +
                 new Vector2(b.x, b.y)) * 0.5f;

            playerScreenRadii = new Vector2(
                Mathf.Abs(b.x - a.x) * 0.5f + padding,
                Mathf.Abs(b.y - a.y) * 0.5f + padding
            );
        }
        else
        {
            Vector3 screenPosition =
                gameplayCamera.WorldToScreenPoint(
                    playerRoot.position
                );

            playerScreenCenter =
                new Vector2(
                    screenPosition.x,
                    screenPosition.y
                );

            float fallbackRadius =
                28f * resolutionScale + padding;

            playerScreenRadii =
                Vector2.one * fallbackRadius;
        }

        hasValidOcclusionArea =
            playerScreenRadii.x > 0f &&
            playerScreenRadii.y > 0f;

        return hasValidOcclusionArea;
    }

    private Camera GetUICamera()
    {
        if (hudCanvas == null ||
            hudCanvas.renderMode ==
                RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        if (hudCanvas.worldCamera != null)
            return hudCanvas.worldCamera;

        return gameplayCamera;
    }

    private void ResolveGameplayCamera()
    {
        gameplayCamera = Camera.main;

        if (gameplayCamera != null)
            return;

        Camera[] cameras =
            UnityFindCompat.FindObjectsByType<Camera>();

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null &&
                cameras[i].isActiveAndEnabled)
            {
                gameplayCamera = cameras[i];
                return;
            }
        }
    }

    private void ResolvePlayerRenderer()
    {
        playerSpriteRenderer = null;

        if (playerRoot == null)
            return;

        SpriteRenderer[] renderers =
            playerRoot.GetComponentsInChildren
                <SpriteRenderer>(true);

        if (renderers == null ||
            renderers.Length == 0)
        {
            return;
        }

        PlayerSkinApplier skinApplier =
            playerRoot.GetComponentInChildren
                <PlayerSkinApplier>(true);

        Sprite selectedSprite =
            skinApplier != null
                ? skinApplier.CurrentSprite
                : null;

        if (selectedSprite != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];

                if (renderer != null &&
                    renderer.sprite == selectedSprite)
                {
                    playerSpriteRenderer = renderer;
                    return;
                }
            }
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null &&
                renderers[i].sprite != null)
            {
                playerSpriteRenderer = renderers[i];
                return;
            }
        }
    }
}
