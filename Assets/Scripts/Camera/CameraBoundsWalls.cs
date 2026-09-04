using UnityEngine;

[DefaultExecutionOrder(1200)]
public class CameraBoundsWalls : MonoBehaviour
{
    [Header("Wall Colliders")]
    [SerializeField]
    private BoxCollider2D leftWall;

    [SerializeField]
    private BoxCollider2D rightWall;

    [SerializeField]
    private BoxCollider2D topWall;

    [SerializeField]
    private BoxCollider2D bottomWall;

    [Header("Wall Settings")]
    [SerializeField, Min(0.01f)]
    private float thickness = 1f;

    private CameraWorldBounds cameraBounds;

    private float lastMinX;
    private float lastMaxX;
    private float lastMinY;
    private float lastMaxY;
    private float lastThickness;

    private void Start()
    {
        cameraBounds =
            CameraWorldBounds.Instance;

        if (cameraBounds == null)
        {
            cameraBounds =
                FindAnyObjectByType<CameraWorldBounds>();
        }

        if (cameraBounds == null)
        {
            Debug.LogError(
                "CameraBoundsWalls: Sahnede aktif CameraWorldBounds bulunamadı.",
                this
            );

            enabled = false;
            return;
        }

        if (!HasAllWallReferences())
        {
            Debug.LogError(
                "CameraBoundsWalls: Wall collider referanslarından en az biri eksik.",
                this
            );

            enabled = false;
            return;
        }

        WarnAboutInvalidScales();

        UpdateWalls();
        CacheCurrentState();
    }

    private void LateUpdate()
    {
        if (cameraBounds == null)
            return;

        if (!HaveBoundsChanged())
            return;

        UpdateWalls();
        CacheCurrentState();
    }

    private void UpdateWalls()
    {
        float minX = cameraBounds.MinX;
        float maxX = cameraBounds.MaxX;
        float minY = cameraBounds.MinY;
        float maxY = cameraBounds.MaxY;

        float width = maxX - minX;
        float height = maxY - minY;

        float centerX =
            (minX + maxX) * 0.5f;

        float centerY =
            (minY + maxY) * 0.5f;

        leftWall.transform.position =
            new Vector2(
                minX - thickness * 0.5f,
                centerY
            );

        leftWall.size =
            new Vector2(
                thickness,
                height + thickness * 2f
            );

        rightWall.transform.position =
            new Vector2(
                maxX + thickness * 0.5f,
                centerY
            );

        rightWall.size =
            new Vector2(
                thickness,
                height + thickness * 2f
            );

        topWall.transform.position =
            new Vector2(
                centerX,
                maxY + thickness * 0.5f
            );

        topWall.size =
            new Vector2(
                width + thickness * 2f,
                thickness
            );

        bottomWall.transform.position =
            new Vector2(
                centerX,
                minY - thickness * 0.5f
            );

        bottomWall.size =
            new Vector2(
                width + thickness * 2f,
                thickness
            );
    }

    private bool HasAllWallReferences()
    {
        return leftWall != null &&
               rightWall != null &&
               topWall != null &&
               bottomWall != null;
    }

    private bool HaveBoundsChanged()
    {
        return !Mathf.Approximately(
                   lastMinX,
                   cameraBounds.MinX
               ) ||
               !Mathf.Approximately(
                   lastMaxX,
                   cameraBounds.MaxX
               ) ||
               !Mathf.Approximately(
                   lastMinY,
                   cameraBounds.MinY
               ) ||
               !Mathf.Approximately(
                   lastMaxY,
                   cameraBounds.MaxY
               ) ||
               !Mathf.Approximately(
                   lastThickness,
                   thickness
               );
    }

    private void CacheCurrentState()
    {
        lastMinX = cameraBounds.MinX;
        lastMaxX = cameraBounds.MaxX;
        lastMinY = cameraBounds.MinY;
        lastMaxY = cameraBounds.MaxY;
        lastThickness = thickness;
    }

    private void WarnAboutInvalidScales()
    {
        CheckWallScale(leftWall, "Left Wall");
        CheckWallScale(rightWall, "Right Wall");
        CheckWallScale(topWall, "Top Wall");
        CheckWallScale(bottomWall, "Bottom Wall");
    }

    private void CheckWallScale(
        BoxCollider2D wall,
        string wallName)
    {
        if (wall.transform.lossyScale == Vector3.one)
            return;

        Debug.LogWarning(
            $"CameraBoundsWalls: {wallName} scale değeri 1,1,1 değil. Collider boyutları beklenenden farklı olabilir.",
            wall
        );
    }

    private void OnValidate()
    {
        thickness = Mathf.Max(
            0.01f,
            thickness
        );
    }
}