using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SlowScreenEffect : MonoBehaviour
{
    [Header("References")]
    public RawImage vignetteImage;
    public Camera mainCamera;

    [Header("Vignette")]
    public float vignetteAlpha = 0.35f;
    public float vignetteFadeIn = 0.15f;
    public float vignetteFadeOut = 0.25f;

    [Header("Camera Pulse")]
    public float zoomAmount = 0.35f;
    public float zoomInDuration = 0.08f;
    public float zoomOutDuration = 0.18f;

    private Coroutine effectRoutine;
    private Coroutine zoomRoutine;

    private Texture2D generatedVignetteTexture;

    private float baseCameraSize;
    private bool cameraSizeCached;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        CacheCameraSize();

        if (vignetteImage != null)
        {
            generatedVignetteTexture = CreateVignetteTexture(512, 512);

            vignetteImage.texture = generatedVignetteTexture;
            vignetteImage.color = new Color(0f, 0f, 0f, 0f);
            vignetteImage.raycastTarget = false;
        }
    }

    public void PlayEffect(float duration)
    {
        duration = Mathf.Max(0f, duration);

        StopEffectRoutine();
        StopZoomRoutineAndRestoreCamera();

        effectRoutine = StartCoroutine(EffectRoutine(duration));
        zoomRoutine = StartCoroutine(CameraPulseRoutine());
    }

    private IEnumerator EffectRoutine(float duration)
    {
        if (vignetteImage == null)
        {
            effectRoutine = null;
            yield break;
        }

        float currentAlpha = vignetteImage.color.a;

        yield return FadeVignette(
            currentAlpha,
            vignetteAlpha,
            vignetteFadeIn
        );

        if (duration > 0f)
            yield return new WaitForSecondsRealtime(duration);

        currentAlpha = vignetteImage.color.a;

        yield return FadeVignette(
            currentAlpha,
            0f,
            vignetteFadeOut
        );

        effectRoutine = null;
    }

    private IEnumerator CameraPulseRoutine()
    {
        if (mainCamera == null)
        {
            zoomRoutine = null;
            yield break;
        }

        CacheCameraSize();

        float targetSize = Mathf.Max(
            0.01f,
            baseCameraSize - Mathf.Max(0f, zoomAmount)
        );

        yield return AnimateCameraSize(
            baseCameraSize,
            targetSize,
            zoomInDuration
        );

        yield return AnimateCameraSize(
            targetSize,
            baseCameraSize,
            zoomOutDuration
        );

        mainCamera.orthographicSize = baseCameraSize;
        zoomRoutine = null;
    }

    private IEnumerator AnimateCameraSize(
        float from,
        float to,
        float duration)
    {
        if (mainCamera == null)
            yield break;

        if (duration <= 0f)
        {
            mainCamera.orthographicSize = to;
            yield break;
        }

        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(time / duration);

            mainCamera.orthographicSize =
                Mathf.Lerp(from, to, t);

            yield return null;
        }

        mainCamera.orthographicSize = to;
    }

    private IEnumerator FadeVignette(
        float from,
        float to,
        float duration)
    {
        if (vignetteImage == null)
            yield break;

        if (duration <= 0f)
        {
            SetVignetteAlpha(to);
            yield break;
        }

        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(time / duration);
            float alpha = Mathf.Lerp(from, to, t);

            SetVignetteAlpha(alpha);

            yield return null;
        }

        SetVignetteAlpha(to);
    }

    private void CacheCameraSize()
    {
        if (mainCamera == null || cameraSizeCached)
            return;

        baseCameraSize = mainCamera.orthographicSize;
        cameraSizeCached = true;
    }

    private void StopEffectRoutine()
    {
        if (effectRoutine == null)
            return;

        StopCoroutine(effectRoutine);
        effectRoutine = null;
    }

    private void StopZoomRoutineAndRestoreCamera()
    {
        if (zoomRoutine != null)
        {
            StopCoroutine(zoomRoutine);
            zoomRoutine = null;
        }

        RestoreCameraSize();
    }

    private void RestoreCameraSize()
    {
        if (mainCamera == null || !cameraSizeCached)
            return;

        mainCamera.orthographicSize = baseCameraSize;
    }

    private void SetVignetteAlpha(float alpha)
    {
        if (vignetteImage == null)
            return;

        Color color = vignetteImage.color;
        color.a = Mathf.Clamp01(alpha);

        vignetteImage.color = color;
    }

    private Texture2D CreateVignetteTexture(
        int width,
        int height)
    {
        Texture2D texture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false
        );

        texture.name = "Runtime Slow Vignette";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(
            width * 0.5f,
            height * 0.5f
        );

        float maxDistance =
            Vector2.Distance(Vector2.zero, center);

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Vector2.Distance(
                    new Vector2(x, y),
                    center
                );

                float t = Mathf.InverseLerp(
                    maxDistance * 0.35f,
                    maxDistance,
                    distance
                );

                t = Mathf.SmoothStep(0f, 1f, t);

                pixels[(y * width) + x] =
                    new Color(0f, 0f, 0f, t);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        return texture;
    }

    private void OnDisable()
    {
        StopEffectRoutine();
        StopZoomRoutineAndRestoreCamera();
        SetVignetteAlpha(0f);
    }

    private void OnDestroy()
    {
        StopEffectRoutine();
        StopZoomRoutineAndRestoreCamera();

        if (generatedVignetteTexture != null)
        {
            Destroy(generatedVignetteTexture);
            generatedVignetteTexture = null;
        }
    }
}