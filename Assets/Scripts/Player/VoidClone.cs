using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class VoidClone : MonoBehaviour
{
    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public float blinkSpeed = 10f;
    public float minAlpha = 0.35f;
    public float maxAlpha = 0.75f;

    [Header("Movement")]
    [Tooltip("Player inputu bunun altindaysa clone tamamen sabit kalir.")]
    public float minimumMovementSpeed = 0.1f;

    [Tooltip("Clone hizinin player hizina orani.")]
    [Range(0.5f, 1.5f)]
    public float speedMultiplier = 0.9f;

    [Tooltip("Player ayarlari alinamazsa kullanilacak hizlanma.")]
    public float fallbackAcceleration = 55f;

    [Tooltip("Player ayarlari alinamazsa kullanilacak donus hizlanmasi.")]
    public float fallbackTurnAcceleration = 90f;

    [Header("Natural Movement")]
    [Tooltip("Clone'un sakin bir sekilde yeni rota secme araligi.")]
    public Vector2 directionChangeInterval = new Vector2(0.75f, 1.25f);

    [Tooltip("Dogal rota degisimindeki maksimum aci.")]
    [Range(0f, 60f)]
    public float maximumTurnAngle = 18f;

    [Tooltip("Clone'un ilk kacis yonunu ne kadar koruyacagi.")]
    [Range(0f, 1f)]
    public float originalDirectionInfluence = 0.35f;

    [Header("Obstacle Avoidance - Same System As Stalker")]
    [Tooltip("Ek layerlar. Obstacle ve Wall layerlari otomatik eklenir.")]
    public LayerMask solidLayers;

    [Tooltip("Clone'un obstacle'i kac birim onceden fark edecegi.")]
    public float avoidanceLookAhead = 1.25f;

    [Range(2, 8)]
    public int obstacleAvoidanceAttempts = 5;

    [Range(0f, 1f)]
    public float obstacleOutwardBias = 0.25f;

    [Tooltip("Collider ile engel arasinda birakilacak guvenlik payi.")]
    public float collisionSkin = 0.04f;

    [Header("Advanced Unstuck")]
    [Min(0.1f)]
    public float stuckCheckTime = 0.35f;

    [Min(0.001f)]
    public float stuckDistance = 0.06f;

    [Min(0.1f)]
    public float escapeCheckRadius = 1.1f;

    [Min(1f)]
    public float escapeSpeedMultiplier = 1.9f;

    private Rigidbody2D rb;
    private Collider2D cloneCollider;

    private Vector2 originalDirection;
    private Vector2 desiredDirection;
    private Vector2 currentVelocity;
    private Vector2 lastPosition;

    private float targetSpeed;
    private float acceleration;
    private float turnAcceleration;
    private float directionTimer;
    private float nextDirectionChange;
    private float stuckTimer;

    private bool cloneActive;
    private bool shouldMove;

    private Vector3 originalScale;

    private int obstacleAvoidanceSide = 1;

    private ContactFilter2D navigationFilter;
    private readonly RaycastHit2D[] avoidanceHits = new RaycastHit2D[16];
    private readonly Collider2D[] escapeHits = new Collider2D[16];

    private GameObject clonedArmorVisual;

    // ShieldRotate clone armorunda bu yonu kullanir.
    // Clone sabitse Vector2.zero doner ve armor da aninda durur.
    public Vector2 VisualMoveDirection
    {
        get
        {
            if (!cloneActive ||
                !shouldMove ||
                currentVelocity.sqrMagnitude <= 0.0001f)
            {
                return Vector2.zero;
            }

            return currentVelocity.normalized;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cloneCollider = GetComponent<Collider2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        originalScale = transform.localScale;

        if (originalScale == Vector3.zero)
            originalScale = Vector3.one;

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.simulated = true;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        RebuildNavigationFilter();

        obstacleAvoidanceSide = Random.value < 0.5f
            ? -1
            : 1;

        lastPosition = rb.position;
    }

    private void RebuildNavigationFilter()
    {
        navigationFilter = new ContactFilter2D();
        navigationFilter.SetLayerMask(
            EnemyObstacleSteering2D.BuildNavigationMask(solidLayers)
        );
        navigationFilter.useLayerMask = true;
        navigationFilter.useTriggers = false;
    }

    public void SetSkin(Sprite skinSprite)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponentInChildren<SpriteRenderer>(true);
        }

        if (spriteRenderer != null &&
            skinSprite != null)
        {
            spriteRenderer.sprite = skinSprite;
        }
    }

    public void CopyArmorVisual(PlayerArmor sourceArmor)
    {
        ClearClonedArmorVisual();

        if (sourceArmor == null ||
            !sourceArmor.HasArmor ||
            sourceArmor.ArmorVisualObject == null)
        {
            return;
        }

        GameObject sourceVisual =
            sourceArmor.ArmorVisualObject;

        clonedArmorVisual = Instantiate(
            sourceVisual,
            transform,
            false
        );

        clonedArmorVisual.name = "CloneArmorVisual";

        // Armor visual player'a gore nasil konumlandiysa clone'da da aynisini koru.
        clonedArmorVisual.transform.localPosition =
            sourceVisual.transform.localPosition;

        clonedArmorVisual.transform.localRotation =
            sourceVisual.transform.localRotation;

        clonedArmorVisual.transform.localScale =
            sourceVisual.transform.localScale;

        // Clone armor sadece gorseldir; fizik/collision uretmemeli.
        Collider2D[] armorColliders =
            clonedArmorVisual.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < armorColliders.Length; i++)
            armorColliders[i].enabled = false;

        Rigidbody2D[] armorBodies =
            clonedArmorVisual.GetComponentsInChildren<Rigidbody2D>(true);

        for (int i = 0; i < armorBodies.Length; i++)
            armorBodies[i].simulated = false;

        // Player armorundaki mevcut ShieldRotate ayarlarini aynen koruyoruz.
        // Tek fark hareket kaynagi PlayerMovement yerine bu clone oluyor.
        // Boylece saga giderken saat yonu, sola giderken ters yon ve
        // clone sabitken tam durma davranisi player ile birebir ayni kalir.
        ShieldRotate[] armorRotators =
            clonedArmorVisual.GetComponentsInChildren<ShieldRotate>(true);

        for (int i = 0; i < armorRotators.Length; i++)
        {
            armorRotators[i].ConfigureForClone(this);
        }

        clonedArmorVisual.SetActive(true);
    }

    public void StartClone(
        float duration,
        PlayerMovement playerMovement)
    {
        StopAllCoroutines();

        Vector2 playerVelocity = Vector2.zero;
        Vector2 playerInput = Vector2.zero;
        float playerMoveSpeed = 0f;

        acceleration = fallbackAcceleration;
        turnAcceleration = fallbackTurnAcceleration;

        if (playerMovement != null)
        {
            playerVelocity = playerMovement.CurrentVelocity;
            playerInput = playerMovement.CurrentMoveInput;
            playerMoveSpeed = playerMovement.CurrentMoveSpeed;

            acceleration = Mathf.Max(
                0.01f,
                playerMovement.acceleration
            );

            turnAcceleration = Mathf.Max(
                0.01f,
                playerMovement.turnAcceleration
            );
        }

        // Kritik fark: Clone'un hareket edip etmeyecegine residual velocity degil,
        // oyuncunun o anda gercekten input verip vermedigi karar verir.
        // Oyuncu joystick'i biraktiysa player yavasliyor olsa bile clone sabit kalir.
        bool hasLivePlayerInput =
            playerMovement != null
                ? playerInput.magnitude >= minimumMovementSpeed
                : playerVelocity.magnitude >= minimumMovementSpeed;

        shouldMove = hasLivePlayerInput;

        if (shouldMove)
        {
            Vector2 sourceDirection =
                playerVelocity.sqrMagnitude > 0.01f
                    ? playerVelocity.normalized
                    : playerInput.normalized;

            originalDirection = -sourceDirection;
            desiredDirection = originalDirection;

            float effectivePlayerSpeed = Mathf.Max(
                playerVelocity.magnitude,
                playerMoveSpeed * Mathf.Clamp01(playerInput.magnitude)
            );

            targetSpeed = Mathf.Max(
                effectivePlayerSpeed,
                playerMoveSpeed * 0.7f
            ) * speedMultiplier;

            currentVelocity =
                originalDirection *
                Mathf.Min(effectivePlayerSpeed, targetSpeed) *
                0.55f;

            directionTimer = 0f;
            ScheduleNextDirectionChange();
        }
        else
        {
            originalDirection = Vector2.zero;
            desiredDirection = Vector2.zero;
            currentVelocity = Vector2.zero;
            targetSpeed = 0f;
        }

        obstacleAvoidanceSide = Random.value < 0.5f
            ? -1
            : 1;

        stuckTimer = 0f;
        lastPosition = rb != null
            ? rb.position
            : (Vector2)transform.position;

        cloneActive = true;
        UpdateFacing(currentVelocity);

        StartCoroutine(CloneLifetimeRoutine(duration));
    }

    private void FixedUpdate()
    {
        if (!cloneActive ||
            !shouldMove ||
            rb == null ||
            cloneCollider == null ||
            Time.timeScale <= 0f)
        {
            return;
        }

        float delta = GetCloneDeltaTime();

        UpdateNaturalDirection(delta);
        UpdateVelocity(delta);

        if (currentVelocity.sqrMagnitude <= 0.0001f)
        {
            HandleStuckCheck(delta);
            return;
        }

        float movementDistance =
            currentVelocity.magnitude * delta;

        Vector2 steeredDirection =
            EnemyObstacleSteering2D.GetSteeredDirection(
                cloneCollider,
                currentVelocity.normalized,
                desiredDirection,
                navigationFilter,
                avoidanceHits,
                avoidanceLookAhead,
                movementDistance,
                collisionSkin,
                obstacleAvoidanceAttempts,
                obstacleOutwardBias,
                ref obstacleAvoidanceSide
            );

        if (steeredDirection.sqrMagnitude > 0.001f)
        {
            float currentSpeed = currentVelocity.magnitude;

            currentVelocity =
                steeredDirection.normalized * currentSpeed;

            desiredDirection = Vector2.Lerp(
                desiredDirection,
                steeredDirection.normalized,
                0.72f
            ).normalized;

            rb.MovePosition(
                rb.position +
                currentVelocity * delta
            );

            UpdateFacing(currentVelocity);
        }
        else
        {
            TryImmediateEscape(delta);
        }

        HandleStuckCheck(delta);
    }

    private void UpdateNaturalDirection(float delta)
    {
        directionTimer += delta;

        if (directionTimer < nextDirectionChange)
            return;

        directionTimer = 0f;

        float randomAngle = Random.Range(
            -maximumTurnAngle,
            maximumTurnAngle
        );

        Vector2 naturalDirection = RotateVector(
            desiredDirection,
            randomAngle
        ).normalized;

        desiredDirection = Vector2.Lerp(
            naturalDirection,
            originalDirection,
            originalDirectionInfluence
        ).normalized;

        ScheduleNextDirectionChange();
    }

    private void UpdateVelocity(float delta)
    {
        Vector2 targetVelocity =
            desiredDirection * targetSpeed;

        float angle =
            currentVelocity.sqrMagnitude > 0.001f
                ? Vector2.Angle(
                    currentVelocity,
                    targetVelocity
                )
                : 0f;

        float rate = angle > 35f
            ? turnAcceleration
            : acceleration;

        currentVelocity = Vector2.MoveTowards(
            currentVelocity,
            targetVelocity,
            rate * delta
        );
    }

    private void TryImmediateEscape(float delta)
    {
        Vector2 escapeDirection = GetEscapeDirection();

        if (escapeDirection.sqrMagnitude <= 0.001f)
            return;

        float escapeSpeed = Mathf.Max(
            targetSpeed,
            currentVelocity.magnitude
        );

        if (escapeSpeed <= 0.01f)
            return;

        currentVelocity =
            escapeDirection * escapeSpeed;

        desiredDirection = escapeDirection;

        rb.MovePosition(
            rb.position +
            escapeDirection *
            escapeSpeed *
            escapeSpeedMultiplier *
            delta
        );

        UpdateFacing(currentVelocity);
        ResetStuckCheck();
    }

    private void HandleStuckCheck(float delta)
    {
        stuckTimer += delta;

        if (stuckTimer < stuckCheckTime)
            return;

        float movedSqrDistance =
            (rb.position - lastPosition).sqrMagnitude;

        float stuckSqrDistance =
            stuckDistance * stuckDistance;

        if (movedSqrDistance < stuckSqrDistance)
        {
            Vector2 escapeDirection = GetEscapeDirection();

            if (escapeDirection.sqrMagnitude > 0.001f)
            {
                float escapeSpeed = Mathf.Max(
                    targetSpeed,
                    0.1f
                );

                currentVelocity =
                    escapeDirection * escapeSpeed;

                desiredDirection = escapeDirection;

                rb.MovePosition(
                    rb.position +
                    escapeDirection *
                    escapeSpeed *
                    escapeSpeedMultiplier *
                    delta
                );

                obstacleAvoidanceSide =
                    Random.value < 0.5f ? -1 : 1;
            }
        }

        lastPosition = rb.position;
        stuckTimer = 0f;
    }

    private Vector2 GetEscapeDirection()
    {
        int hitCount = Physics2D.OverlapCircle(
            rb.position,
            escapeCheckRadius,
            navigationFilter,
            escapeHits
        );

        if (hitCount <= 0)
            return Vector2.zero;

        Vector2 escapeDirection = Vector2.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = escapeHits[i];

            if (hit == null || hit == cloneCollider)
                continue;

            if (hit.attachedRigidbody == rb)
                continue;

            Vector2 closestPoint =
                hit.ClosestPoint(rb.position);

            Vector2 awayFromObstacle =
                rb.position - closestPoint;

            if (awayFromObstacle.sqrMagnitude <= 0.001f)
            {
                awayFromObstacle =
                    rb.position -
                    (Vector2)hit.bounds.center;
            }

            if (awayFromObstacle.sqrMagnitude > 0.001f)
            {
                escapeDirection +=
                    awayFromObstacle.normalized;
            }
        }

        return escapeDirection.sqrMagnitude > 0.001f
            ? escapeDirection.normalized
            : Vector2.zero;
    }

    private void ResetStuckCheck()
    {
        stuckTimer = 0f;
        lastPosition = rb.position;
    }

    private void ScheduleNextDirectionChange()
    {
        float minimum = Mathf.Min(
            directionChangeInterval.x,
            directionChangeInterval.y
        );

        float maximum = Mathf.Max(
            directionChangeInterval.x,
            directionChangeInterval.y
        );

        nextDirectionChange = Random.Range(
            minimum,
            maximum
        );
    }

    private static Vector2 RotateVector(
        Vector2 vector,
        float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        );
    }

    private IEnumerator CloneLifetimeRoutine(float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            UpdateVisual();
            yield return null;
        }

        StopMovement();
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null)
            return;

        Color color = spriteRenderer.color;

        color.a = Mathf.Lerp(
            minAlpha,
            maxAlpha,
            Mathf.PingPong(
                Time.time * blinkSpeed,
                1f
            )
        );

        spriteRenderer.color = color;
    }

    private void UpdateFacing(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) <= 0.05f)
            return;

        Vector3 scale = transform.localScale;

        scale.x = direction.x > 0f
            ? Mathf.Abs(originalScale.x)
            : -Mathf.Abs(originalScale.x);

        transform.localScale = scale;
    }

    private float GetCloneDeltaTime()
    {
        if (Time.timeScale <= 0f)
            return Time.fixedDeltaTime;

        return Time.fixedDeltaTime / Time.timeScale;
    }

    private void StopMovement()
    {
        cloneActive = false;
        shouldMove = false;
        originalDirection = Vector2.zero;
        desiredDirection = Vector2.zero;
        currentVelocity = Vector2.zero;
        targetSpeed = 0f;
        directionTimer = 0f;
        nextDirectionChange = 0f;
        stuckTimer = 0f;
    }

    private void ClearClonedArmorVisual()
    {
        if (clonedArmorVisual == null)
            return;

        Destroy(clonedArmorVisual);
        clonedArmorVisual = null;
    }

    private void OnDisable()
    {
        StopMovement();
    }

    private void OnDestroy()
    {
        ClearClonedArmorVisual();
    }

    private void OnValidate()
    {
        minimumMovementSpeed = Mathf.Max(
            0f,
            minimumMovementSpeed
        );

        avoidanceLookAhead = Mathf.Max(
            0.05f,
            avoidanceLookAhead
        );

        collisionSkin = Mathf.Max(
            0f,
            collisionSkin
        );

        stuckCheckTime = Mathf.Max(
            0.1f,
            stuckCheckTime
        );

        stuckDistance = Mathf.Max(
            0.001f,
            stuckDistance
        );

        escapeCheckRadius = Mathf.Max(
            0.1f,
            escapeCheckRadius
        );

        escapeSpeedMultiplier = Mathf.Max(
            1f,
            escapeSpeedMultiplier
        );

        if (Application.isPlaying)
            RebuildNavigationFilter();
    }
}
