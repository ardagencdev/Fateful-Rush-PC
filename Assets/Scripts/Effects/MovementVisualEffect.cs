using UnityEngine;

public class MovementVisualEffect : MonoBehaviour
{
    [Header("Visual")]
    public Transform visual;

    [Header("Movement Detection")]
    public float moveThreshold = 0.01f;

    [Header("Tilt")]
    public float tiltAmount = 8f;
    public float tiltSpeed = 10f;

    [Header("Float")]
    public float floatAmount = 0.04f;
    public float floatSpeed = 4f;

    private Vector3 lastPosition;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private bool hasSeparateVisual;

    private void Awake()
    {
        if (visual == null)
            visual = transform;

        hasSeparateVisual = visual != transform;

        lastPosition = transform.position;
        originalLocalPosition = visual.localPosition;
        originalLocalRotation = visual.localRotation;
    }

    private void LateUpdate()
    {
        Vector3 velocity = (transform.position - lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = transform.position;

        bool isMoving = velocity.magnitude > moveThreshold;

        ApplyFloat();
        ApplyTilt(velocity, isMoving);
    }

    private void ApplyFloat()
    {
        if (!hasSeparateVisual) return;

        float offsetY = Mathf.Sin(Time.time * floatSpeed) * floatAmount;
        visual.localPosition = originalLocalPosition + Vector3.up * offsetY;
    }

    private void ApplyTilt(Vector3 velocity, bool isMoving)
    {
        float targetZ = isMoving ? -velocity.x * tiltAmount : 0f;
        targetZ = Mathf.Clamp(targetZ, -tiltAmount, tiltAmount);

        Quaternion targetRotation = originalLocalRotation * Quaternion.Euler(0f, 0f, targetZ);

        visual.localRotation = Quaternion.Lerp(
            visual.localRotation,
            targetRotation,
            Time.deltaTime * tiltSpeed
        );
    }
}