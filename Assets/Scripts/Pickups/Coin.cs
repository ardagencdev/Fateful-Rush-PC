using UnityEngine;

public enum CoinType
{
    Normal = 0,
    Gold = 1,
    Rare = 2
}

public class Coin : MonoBehaviour
{
    [Min(1)]
    public int value = 1;

    [SerializeField]
    private CoinType coinType = CoinType.Normal;

    private bool isCollected;
    private Collider2D[] cachedColliders;
    private Rigidbody2D[] cachedRigidbodies;
    private Vector3 magnetVelocity;
    private bool wasMagnetAffected;

    public bool IsCollected => isCollected;
    public CoinType Type => coinType;
    public bool WasMagnetAffected => wasMagnetAffected;

    private void Awake()
    {
        CachePhysics();
    }

    private void OnEnable()
    {
        isCollected = false;
        magnetVelocity = Vector3.zero;
        wasMagnetAffected = false;
        RestorePhysicsAndCollisions();
    }

    private void Update()
    {
        if (isCollected)
            return;

        PlayerCoinCollector collector =
            PlayerCoinCollector.Instance;

        if (collector == null)
        {
            magnetVelocity = Vector3.zero;
            return;
        }

        if (!collector.TryGetComboMagnetSettings(
                transform.position,
                out float maxSpeed,
                out float smoothTime))
        {
            // Combo bittiğinde coin kendi kendine kaymaya devam etmesin.
            magnetVelocity = Vector3.zero;
            return;
        }

        wasMagnetAffected = true;

        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = collector.transform.position;
        targetPosition.z = currentPosition.z;

        transform.position = Vector3.SmoothDamp(
            currentPosition,
            targetPosition,
            ref magnetVelocity,
            smoothTime,
            maxSpeed,
            Time.deltaTime
        );
    }

    public void Configure(CoinType type, int coinValue)
    {
        coinType = type;
        value = Mathf.Max(1, coinValue);
    }

    /// <summary>
    /// Coin toplama işlemini yalnızca bir kez başlatır.
    /// Aynı fizik karesinde birden fazla trigger çağrısı gelse bile
    /// skorun ikinci kez eklenmesini engeller.
    /// </summary>
    public bool TryBeginCollection()
    {
        if (isCollected)
            return false;

        isCollected = true;
        magnetVelocity = Vector3.zero;
        SpawnAreaRegistry.Unregister(gameObject);
        DisablePhysicsAndCollisions();
        return true;
    }

    private void CachePhysics()
    {
        cachedColliders =
            GetComponentsInChildren<Collider2D>(true);

        cachedRigidbodies =
            GetComponentsInChildren<Rigidbody2D>(true);
    }

    private void DisablePhysicsAndCollisions()
    {
        if (cachedColliders == null || cachedRigidbodies == null)
            CachePhysics();

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider2D collider = cachedColliders[i];
            if (collider != null)
                collider.enabled = false;
        }

        for (int i = 0; i < cachedRigidbodies.Length; i++)
        {
            Rigidbody2D body = cachedRigidbodies[i];
            if (body != null)
                body.simulated = false;
        }
    }

    private void RestorePhysicsAndCollisions()
    {
        if (cachedColliders == null || cachedRigidbodies == null)
            CachePhysics();

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider2D collider = cachedColliders[i];
            if (collider != null)
                collider.enabled = true;
        }

        for (int i = 0; i < cachedRigidbodies.Length; i++)
        {
            Rigidbody2D body = cachedRigidbodies[i];
            if (body != null)
                body.simulated = true;
        }
    }

    private void OnDisable()
    {
        magnetVelocity = Vector3.zero;
        SpawnAreaRegistry.Unregister(gameObject);
    }
}
