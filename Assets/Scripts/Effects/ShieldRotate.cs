using UnityEngine;
using UnityEngine.Serialization;

public class ShieldRotate : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerMovement playerMovement;

    [SerializeField]
    private SpriteRenderer shieldSpriteRenderer;

    // Runtime'da sadece clone armor icin atanir.
    // Null ise normal player davranisi kullanilir.
    private VoidClone cloneMovementSource;

    [Header("Movement Spin")]
    [SerializeField, Min(0f)]
    [Tooltip(
        "Player hareket ederken Armor Visual'ın sürekli dönüş hızı. " +
        "Sağ yön saat yönünde, sol yön saat yönünün tersindedir."
    )]
    private float rotationSpeed = 110f;

    [FormerlySerializedAs("rotationSharpness")]
    [SerializeField, Min(0f)]
    [Tooltip(
        "Dönüş yönü değiştiğinde yeni yöne ne kadar hızlı geçtiğini belirler. " +
        "Yüksek değer daha tepkisel, düşük değer daha yumuşaktır."
    )]
    private float rotationResponsiveness = 35f;

    [SerializeField, Range(0f, 0.5f)]
    [Tooltip(
        "Çok küçük yatay inputların dönüş yönünü gereksiz yere " +
        "değiştirmesini önler."
    )]
    private float horizontalDirectionThreshold = 0.05f;

    [Header("Visual")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip(
        "Armor Visual'ın ortak saydamlığı. Düşük değer player skinini " +
        "daha az kapatır."
    )]
    private float visualAlpha = 0.28f;

    private const float MovementThresholdSqr = 0.0001f;

    private float currentAngularSpeed;
    private float currentWorldAngle;

    // Unity'de negatif Z dönüşü ekranda saat yönündedir.
    private float spinDirection = -1f;

    private void Awake()
    {
        FindMissingReferences();

        currentWorldAngle =
            transform.eulerAngles.z;

        ApplyVisualAlpha();
    }

    private void OnEnable()
    {
        FindMissingReferences();

        currentWorldAngle =
            transform.eulerAngles.z;

        currentAngularSpeed = 0f;

        ApplyVisualAlpha();
    }

    private void LateUpdate()
    {
        // PlayerArmor rengi tekrar uygulasa bile ortak düşük alpha korunur.
        ApplyVisualAlpha();

        if (Time.timeScale <= 0f)
            return;

        Vector2 moveDirection;

        if (cloneMovementSource != null)
        {
            moveDirection =
                cloneMovementSource.VisualMoveDirection;
        }
        else
        {
            FindPlayerMovement();

            if (playerMovement == null)
                return;

            moveDirection =
                playerMovement.VisualMoveDirection;
        }

        // Player hareket etmiyorsa Armor bulunduğu açıda anında durur.
        if (moveDirection.sqrMagnitude <= MovementThresholdSqr)
        {
            currentAngularSpeed = 0f;
            return;
        }

        UpdateSpinDirection(moveDirection);

        float targetAngularSpeed =
            spinDirection * rotationSpeed;

        float deltaTime = Time.unscaledDeltaTime;

        float response =
            rotationResponsiveness <= 0f
                ? 1f
                : 1f - Mathf.Exp(
                    -rotationResponsiveness *
                    deltaTime
                );

        currentAngularSpeed = Mathf.Lerp(
            currentAngularSpeed,
            targetAngularSpeed,
            response
        );

        currentWorldAngle = Mathf.Repeat(
            currentWorldAngle +
            currentAngularSpeed * deltaTime,
            360f
        );

        // World rotation kullanıldığı için Player'ın negatif X scale ile
        // sola dönmesi Armor'ın saat yönünü tersine çevirmez.
        transform.rotation = Quaternion.Euler(
            0f,
            0f,
            currentWorldAngle
        );
    }

    public void ConfigureForClone(VoidClone cloneSource)
    {
        cloneMovementSource = cloneSource;
        playerMovement = null;

        currentWorldAngle =
            transform.eulerAngles.z;

        currentAngularSpeed = 0f;

        ApplyVisualAlpha();
        enabled = true;
    }

    private void UpdateSpinDirection(
        Vector2 moveDirection
    )
    {
        if (moveDirection.x >
            horizontalDirectionThreshold)
        {
            // Sağa hareket: saat yönü.
            spinDirection = -1f;
        }
        else if (moveDirection.x <
                 -horizontalDirectionThreshold)
        {
            // Sola hareket: saat yönünün tersi.
            spinDirection = 1f;
        }

        // Tam yukarı veya aşağı harekette son dönüş yönü korunur.
        // Böylece Armor bütün hareket açılarında kesintisiz dönmeye devam eder.
    }

    private void ApplyVisualAlpha()
    {
        FindShieldSpriteRenderer();

        if (shieldSpriteRenderer == null)
            return;

        Color color = shieldSpriteRenderer.color;

        if (Mathf.Approximately(
                color.a,
                visualAlpha))
        {
            return;
        }

        color.a = visualAlpha;
        shieldSpriteRenderer.color = color;
    }

    private void FindMissingReferences()
    {
        FindPlayerMovement();
        FindShieldSpriteRenderer();
    }

    private void FindPlayerMovement()
    {
        if (cloneMovementSource != null ||
            playerMovement != null)
        {
            return;
        }

        playerMovement =
            GetComponentInParent<PlayerMovement>(true);
    }

    private void FindShieldSpriteRenderer()
    {
        if (shieldSpriteRenderer != null)
            return;

        shieldSpriteRenderer =
            GetComponent<SpriteRenderer>();

        if (shieldSpriteRenderer == null)
        {
            shieldSpriteRenderer =
                GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void OnDisable()
    {
        currentAngularSpeed = 0f;
    }

    private void OnValidate()
    {
        rotationSpeed =
            Mathf.Max(0f, rotationSpeed);

        rotationResponsiveness =
            Mathf.Max(0f, rotationResponsiveness);

        horizontalDirectionThreshold =
            Mathf.Clamp(
                horizontalDirectionThreshold,
                0f,
                0.5f
            );

        visualAlpha =
            Mathf.Clamp01(visualAlpha);

        if (Application.isPlaying)
            ApplyVisualAlpha();
    }
}