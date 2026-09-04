using UnityEngine;

public class SpaceFloatVisual : MonoBehaviour
{
    [Header("Float")]
    public float floatAmount = 0.08f;
    public float floatSpeed = 2f;

    [Header("Rotation")]
    public float rotationAmount = 3f;
    public float rotationSpeed = 1.5f;

    [Header("Random Offset")]
    public bool randomizeOffset = true;

    private Vector3 startLocalPos;
    private Quaternion startLocalRot;
    private float offset;

    private void Awake()
    {
        startLocalPos = transform.localPosition;
        startLocalRot = transform.localRotation;

        if (randomizeOffset)
            offset = Random.Range(0f, 100f);
    }

    private void Update()
    {
        float y = Mathf.Sin((Time.time + offset) * floatSpeed) * floatAmount;
        float rotZ = Mathf.Sin((Time.time + offset) * rotationSpeed) * rotationAmount;

        transform.localPosition = startLocalPos + new Vector3(0f, y, 0f);
        transform.localRotation = startLocalRot * Quaternion.Euler(0f, 0f, rotZ);
    }
}