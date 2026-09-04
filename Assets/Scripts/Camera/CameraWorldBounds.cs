using UnityEngine;

[DefaultExecutionOrder(1100)]
public class CameraWorldBounds : MonoBehaviour
{
    public static CameraWorldBounds Instance { get; private set; }

    [Header("References")]
    [SerializeField]
    private Camera cam;

    [Header("Bounds")]
    [SerializeField, Min(0f)]
    private float padding = 0.5f;

    public float MinX { get; private set; }
    public float MaxX { get; private set; }
    public float MinY { get; private set; }
    public float MaxY { get; private set; }

    public float Width => MaxX - MinX;
    public float Height => MaxY - MinY;

    public Vector2 Center =>
        new Vector2(
            (MinX + MaxX) * 0.5f,
            (MinY + MaxY) * 0.5f
        );

    private Vector3 lastStableCameraPosition;
    private float lastOrthographicSize;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastCameraAspect;
    private Rect lastCameraRect;
    private Rect lastCameraPixelRect;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "Birden fazla CameraWorldBounds bulundu. Fazladan olan siliniyor.",
                this
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;

        FindCamera();

        if (cam == null)
        {
            Debug.LogError(
                "CameraWorldBounds: Kullanılacak kamera bulunamadı.",
                this
            );

            enabled = false;
            return;
        }

        if (!cam.orthographic)
        {
            Debug.LogWarning(
                "CameraWorldBounds: Kamera orthographic değil. Bu script 2D orthographic kamera için tasarlandı.",
                this
            );
        }

        UpdateBounds();
        CacheCameraState();
    }

    private void LateUpdate()
    {
        if (cam == null)
            return;

        if (!HasCameraStateChanged())
            return;

        UpdateBounds();
        CacheCameraState();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ForceRefreshBounds();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
            ForceRefreshBounds();
    }

    public void ForceRefreshBounds()
    {
        if (cam == null)
            FindCamera();

        if (cam == null)
            return;

        UpdateBounds();
        CacheCameraState();
    }

    public void UpdateBounds()
    {
        if (cam == null)
            return;

        float distanceToWorldPlane =
            Mathf.Abs(cam.transform.position.z);

        Vector3 bottomLeft =
            cam.ViewportToWorldPoint(
                new Vector3(
                    0f,
                    0f,
                    distanceToWorldPlane
                )
            );

        Vector3 topRight =
            cam.ViewportToWorldPoint(
                new Vector3(
                    1f,
                    1f,
                    distanceToWorldPlane
                )
            );

        // CameraShake moves the rendered camera, but the gameplay arena must
        // stay fixed. Remove only the shake translation from the viewport
        // points before calculating physical arena bounds.
        Vector3 shakeOffset = GetCameraShakeWorldOffset();
        bottomLeft -= shakeOffset;
        topRight -= shakeOffset;

        MinX = bottomLeft.x + padding;
        MaxX = topRight.x - padding;
        MinY = bottomLeft.y + padding;
        MaxY = topRight.y - padding;

        if (MinX > MaxX || MinY > MaxY)
        {
            Debug.LogWarning(
                "CameraWorldBounds: Padding kamera alanından daha büyük.",
                this
            );
        }
    }

    public Vector2 RandomPointInside(
        float extraPadding = 0f)
    {
        extraPadding = Mathf.Max(
            0f,
            extraPadding
        );

        float minimumX =
            MinX + extraPadding;

        float maximumX =
            MaxX - extraPadding;

        float minimumY =
            MinY + extraPadding;

        float maximumY =
            MaxY - extraPadding;

        if (minimumX > maximumX)
        {
            float centerX =
                (MinX + MaxX) * 0.5f;

            minimumX = centerX;
            maximumX = centerX;
        }

        if (minimumY > maximumY)
        {
            float centerY =
                (MinY + MaxY) * 0.5f;

            minimumY = centerY;
            maximumY = centerY;
        }

        return new Vector2(
            Random.Range(minimumX, maximumX),
            Random.Range(minimumY, maximumY)
        );
    }

    private void FindCamera()
    {
        if (cam != null)
            return;

        cam = Camera.main;

        if (cam == null)
        {
            cam = FindAnyObjectByType<Camera>();
        }
    }

    private bool HasCameraStateChanged()
    {
        return GetStableCameraPosition() != lastStableCameraPosition ||
               !Mathf.Approximately(
                   cam.orthographicSize,
                   lastOrthographicSize
               ) ||
               Screen.width != lastScreenWidth ||
               Screen.height != lastScreenHeight ||
               !Mathf.Approximately(
                   cam.aspect,
                   lastCameraAspect
               ) ||
               cam.rect != lastCameraRect ||
               cam.pixelRect != lastCameraPixelRect;
    }

    private void CacheCameraState()
    {
        lastStableCameraPosition =
            GetStableCameraPosition();

        lastOrthographicSize =
            cam.orthographicSize;

        lastScreenWidth =
            Screen.width;

        lastScreenHeight =
            Screen.height;

        lastCameraAspect =
            cam.aspect;

        lastCameraRect =
            cam.rect;

        lastCameraPixelRect =
            cam.pixelRect;
    }


    private Vector3 GetStableCameraPosition()
    {
        CameraShake cameraShake = CameraShake.Instance;

        if (IsCameraAffectedByShake(cameraShake))
        {
            return cam.transform.position -
                   cameraShake.CurrentWorldShakeOffset;
        }

        return cam.transform.position;
    }

    private Vector3 GetCameraShakeWorldOffset()
    {
        CameraShake cameraShake = CameraShake.Instance;

        if (IsCameraAffectedByShake(cameraShake))
            return cameraShake.CurrentWorldShakeOffset;

        return Vector3.zero;
    }

    private bool IsCameraAffectedByShake(CameraShake cameraShake)
    {
        if (cameraShake == null || cam == null)
            return false;

        Transform shakeTransform = cameraShake.transform;

        return cam.transform == shakeTransform ||
               cam.transform.IsChildOf(shakeTransform);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        padding = Mathf.Max(0f, padding);
    }
}