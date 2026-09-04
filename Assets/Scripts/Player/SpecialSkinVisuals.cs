using UnityEngine;

/// <summary>
/// Cosmetic-only prestige skin effects.
///
/// Dark / Golden:
/// - dash afterimages
/// - level start / spawn pulse
/// - death pulse
///
/// Purple:
/// - level start / spawn pulse only
///
/// White / Blue / Cyan / Yellow / Orange / Green / Red / Purple:
/// - shared white coin collection sprite tinted with Armor Visual Color
///
/// Dark:
/// - unique sprite-based coin collection effect
///
/// Golden:
/// - unique sprite-based coin collection effect
///
/// No gameplay values are changed here.
/// </summary>
[DisallowMultipleComponent]
public class SpecialSkinVisuals : MonoBehaviour
{
    private const string DarkSkinId = "dark";
    private const string GoldenSkinId = "golden";
    private const string PurpleSkinId = "purple";

    private const int BurstPoolSize = 10;
    private const int PrestigePulsePoolSize = 4;
    private const int AfterimagePoolSize = 6;

    private const string DarkCoinBurstResourcePath =
        "SpecialSkinVFX/DarkCoinBurst";

    private const string GoldenCoinBurstResourcePath =
        "SpecialSkinVFX/GoldenCoinBurst";

    private const string StandardCoinBurstResourcePath =
        "SpecialSkinVFX/StandardCoinBurst";

    [Header("Prestige Dash Afterimage")]
    [SerializeField, Min(0.01f)]
    private float afterimageInterval = 0.045f;

    [SerializeField, Min(0.05f)]
    private float afterimageLifetime = 0.18f;

    [SerializeField, Range(0f, 1f)]
    private float afterimageAlpha = 0.24f;

    [Header("Prestige Spawn / Death")]
    [SerializeField, Min(0.05f)]
    private float spawnBurstDuration = 0.34f;

    [SerializeField, Min(0.05f)]
    private float deathBurstDuration = 0.40f;

    [SerializeField, Range(0.05f, 1f)]
    private float spawnPulseAlpha = 0.42f;

    [SerializeField, Range(0.05f, 1f)]
    private float deathPulseAlpha = 0.62f;


    [Header("Coin Collection Sprites")]
    [Tooltip(
        "White generic coin collect sprite used by every non-prestige skin. " +
        "It is tinted automatically with that skin's Armor Visual Color. " +
        "If empty, Resources/SpecialSkinVFX/StandardCoinBurst is loaded."
    )]
    [SerializeField]
    private Sprite standardCoinBurstSprite;

    [Tooltip(
        "Optional. If empty, Resources/SpecialSkinVFX/DarkCoinBurst is loaded."
    )]
    [SerializeField]
    private Sprite darkCoinBurstSprite;

    [Tooltip(
        "Optional. If empty, Resources/SpecialSkinVFX/GoldenCoinBurst is loaded."
    )]
    [SerializeField]
    private Sprite goldenCoinBurstSprite;

    [Header("Coin Burst Animation")]
    [SerializeField, Min(0.05f)]
    private float burstDuration = 0.28f;

    [Tooltip(
        "How large the effect becomes compared with the collected coin."
    )]
    [SerializeField, Min(1f)]
    private float finalBurstSizeMultiplier = 2.5f;

    [Tooltip(
        "The effect begins very small, then expands to its final size."
    )]
    [SerializeField, Range(0.01f, 0.9f)]
    private float startSizeRatio = 0.30f;

    [SerializeField, Range(0f, 1f)]
    private float burstAlpha = 0.85f;

    private PlayerSkinApplier skinApplier;
    private PlayerDash playerDash;
    private SpriteRenderer playerRenderer;

    private string activeSkinId = string.Empty;
    private Color activeDashColor = Color.white;
    private Color activeArmorColor = Color.white;

    private float afterimageTimer;
    private bool levelSpawnPlayed;

    private GameObject afterimagePoolRoot;
    private PrestigeAfterimageFade[] afterimagePool;
    private int afterimagePoolCursor;

    private GameObject burstPoolRoot;
    private SpecialSkinCoinBurstSprite[] burstPool;
    private int burstPoolCursor;

    private GameObject prestigePulsePoolRoot;
    private SpecialSkinPulseSprite[] prestigePulsePool;
    private int prestigePulsePoolCursor;

    public string ActiveSkinId => activeSkinId;

    private bool IsDark => activeSkinId == DarkSkinId;
    private bool IsGolden => activeSkinId == GoldenSkinId;
    private bool IsPurple => activeSkinId == PurpleSkinId;

    private bool UsesPrestigeEffects =>
        IsDark || IsGolden;

    // Purple shares only the level-start spawn pulse.
    // Dash afterimages and death pulse remain exclusive to Dark / Golden.
    private bool UsesSpawnEffect =>
        UsesPrestigeEffects || IsPurple;

    private void Awake()
    {
        skinApplier = GetComponent<PlayerSkinApplier>();
        playerDash = GetComponent<PlayerDash>();

        FindPlayerRenderer();
        LoadOptionalSprites();
    }

    private void OnEnable()
    {
        RefreshFromCurrentSkin();
    }

    private void Update()
    {
        TryPlayLevelSpawnEffect();
        UpdatePrestigeAfterimages();
    }

    private void OnDestroy()
    {
        if (afterimagePoolRoot != null)
            Destroy(afterimagePoolRoot);

        if (burstPoolRoot != null)
            Destroy(burstPoolRoot);

        // This root lives outside the player hierarchy so a death pulse can
        // finish even if the player is destroyed immediately afterwards.
        if (prestigePulsePoolRoot != null)
            Destroy(prestigePulsePoolRoot, 1f);
    }

    public void ApplySkin(
        PlayerSkinCatalog.SkinEntry skin)
    {
        string newSkinId =
            skin != null &&
            !string.IsNullOrWhiteSpace(skin.id)
                ? skin.id.Trim().ToLowerInvariant()
                : string.Empty;

        bool skinChanged = activeSkinId != newSkinId;

        activeSkinId = newSkinId;

        if (skin != null)
        {
            activeDashColor = MakeVisibleColor(
                skin.dashTrailColor,
                Color.white
            );

            activeArmorColor = MakeVisibleColor(
                skin.armorVisualColor,
                activeDashColor
            );
        }
        else
        {
            activeDashColor = Color.white;
            activeArmorColor = Color.white;
        }

        if (skinChanged)
            levelSpawnPlayed = false;

        afterimageTimer = 0f;

        FindPlayerRenderer();
        LoadOptionalSprites();
    }

    public void PlayDeathEffect()
    {
        if (!UsesPrestigeEffects)
            return;

        FindPlayerRenderer();

        Vector3 effectPosition =
            playerRenderer != null
                ? playerRenderer.transform.position
                : transform.position;

        PrestigeStyle style = GetPrestigeStyle();

        PlayPrestigePulse(
            effectPosition,
            1.00f,
            1.90f * style.pulseScaleMultiplier,
            deathBurstDuration,
            deathPulseAlpha,
            style.secondaryColor
        );

    }

    public void PlayCoinCollectBurst(
        Vector3 worldPosition,
        int coinValue,
        float coinWorldSize)
    {
        Sprite selectedSprite;
        Color burstColor;

        if (IsDark)
        {
            selectedSprite = darkCoinBurstSprite;
            burstColor = Color.white;
        }
        else if (IsGolden)
        {
            selectedSprite = goldenCoinBurstSprite;
            burstColor = Color.white;
        }
        else
        {
            selectedSprite = standardCoinBurstSprite;
            burstColor = activeArmorColor;
        }

        if (selectedSprite == null)
            return;

        int safeValue =
            Mathf.Max(1, coinValue);

        float safeCoinSize =
            Mathf.Max(0.05f, coinWorldSize);

        float valueScale =
            Mathf.Clamp(
                1f + (safeValue - 1) * 0.10f,
                1f,
                1.30f
            );

        float finalWorldSize =
            safeCoinSize *
            finalBurstSizeMultiplier *
            valueScale;

        float startWorldSize =
            finalWorldSize *
            startSizeRatio;

        CreateSpriteBurst(
            worldPosition,
            selectedSprite,
            startWorldSize,
            finalWorldSize,
            burstDuration,
            burstAlpha,
            burstColor
        );
    }

    // Compatibility overload for older call sites.
    public void PlayCoinCollectBurst(
        Vector3 worldPosition,
        int coinValue)
    {
        PlayCoinCollectBurst(
            worldPosition,
            coinValue,
            0.6f
        );
    }

    private void TryPlayLevelSpawnEffect()
    {
        if (levelSpawnPlayed ||
            !UsesSpawnEffect ||
            !GameStateManager.IsGameplayStarted)
        {
            return;
        }

        levelSpawnPlayed = true;

        FindPlayerRenderer();

        Vector3 effectPosition =
            playerRenderer != null
                ? playerRenderer.transform.position
                : transform.position;

        PrestigeStyle style = GetPrestigeStyle();

        PlayPrestigePulse(
            effectPosition,
            0.58f,
            1.55f * style.pulseScaleMultiplier,
            spawnBurstDuration,
            spawnPulseAlpha,
            style.secondaryColor
        );

    }

    private void RefreshFromCurrentSkin()
    {
        if (skinApplier == null)
            skinApplier = GetComponent<PlayerSkinApplier>();

        ApplySkin(
            skinApplier != null
                ? skinApplier.CurrentSkin
                : null
        );
    }

    private void LoadOptionalSprites()
    {
        if (standardCoinBurstSprite == null)
        {
            standardCoinBurstSprite =
                Resources.Load<Sprite>(
                    StandardCoinBurstResourcePath
                );
        }

        if (darkCoinBurstSprite == null)
        {
            darkCoinBurstSprite =
                Resources.Load<Sprite>(
                    DarkCoinBurstResourcePath
                );
        }

        if (goldenCoinBurstSprite == null)
        {
            goldenCoinBurstSprite =
                Resources.Load<Sprite>(
                    GoldenCoinBurstResourcePath
                );
        }
    }

    private void FindPlayerRenderer()
    {
        Sprite expectedSprite =
            skinApplier != null
                ? skinApplier.CurrentSprite
                : null;

        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(
                true
            );

        if (expectedSprite != null)
        {
            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                SpriteRenderer candidate =
                    renderers[i];

                if (candidate != null &&
                    candidate.sprite ==
                    expectedSprite)
                {
                    playerRenderer = candidate;
                    return;
                }
            }
        }

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            SpriteRenderer candidate =
                renderers[i];

            if (candidate == null ||
                candidate.sprite == null)
            {
                continue;
            }

            playerRenderer = candidate;
            return;
        }
    }

    private void UpdatePrestigeAfterimages()
    {
        if (!UsesPrestigeEffects ||
            playerDash == null ||
            !playerDash.IsDashing)
        {
            afterimageTimer = 0f;
            return;
        }

        afterimageTimer -=
            Time.unscaledDeltaTime;

        if (afterimageTimer > 0f)
            return;

        afterimageTimer =
            afterimageInterval;

        SpawnAfterimage();
    }

    private void SpawnAfterimage()
    {
        if (playerRenderer == null ||
            playerRenderer.sprite == null)
        {
            FindPlayerRenderer();
        }

        if (playerRenderer == null ||
            playerRenderer.sprite == null)
        {
            return;
        }

        PrestigeAfterimageFade fade =
            GetAfterimageFromPool();

        if (fade == null)
            return;

        GameObject ghost = fade.gameObject;
        SpriteRenderer ghostRenderer = fade.Renderer;

        if (ghostRenderer == null)
            return;

        ghost.transform.position =
            playerRenderer.transform.position;

        ghost.transform.rotation =
            playerRenderer.transform.rotation;

        ghost.transform.localScale =
            playerRenderer.transform.lossyScale;

        ghostRenderer.sprite =
            playerRenderer.sprite;

        ghostRenderer.flipX =
            playerRenderer.flipX;

        ghostRenderer.flipY =
            playerRenderer.flipY;

        ghostRenderer.sortingLayerID =
            playerRenderer.sortingLayerID;

        ghostRenderer.sortingOrder =
            playerRenderer.sortingOrder - 1;

        ghostRenderer.color =
            new Color(
                1f,
                1f,
                1f,
                afterimageAlpha
            );

        fade.Initialize(
            ghostRenderer,
            afterimageLifetime
        );

        ghost.SetActive(true);
    }

    private PrestigeAfterimageFade GetAfterimageFromPool()
    {
        EnsureAfterimagePool();

        if (afterimagePool == null ||
            afterimagePool.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < afterimagePool.Length; i++)
        {
            int index =
                (afterimagePoolCursor + i) %
                afterimagePool.Length;

            PrestigeAfterimageFade candidate =
                afterimagePool[index];

            if (candidate != null &&
                !candidate.gameObject.activeSelf)
            {
                afterimagePoolCursor =
                    (index + 1) % afterimagePool.Length;

                return candidate;
            }
        }

        PrestigeAfterimageFade fallback =
            afterimagePool[afterimagePoolCursor];

        afterimagePoolCursor =
            (afterimagePoolCursor + 1) % afterimagePool.Length;

        return fallback;
    }

    private void EnsureAfterimagePool()
    {
        if (afterimagePool != null &&
            afterimagePool.Length == AfterimagePoolSize)
        {
            return;
        }

        if (afterimagePoolRoot == null)
        {
            afterimagePoolRoot =
                new GameObject(
                    "PrestigeDashAfterimagePool"
                );
        }

        afterimagePool =
            new PrestigeAfterimageFade[AfterimagePoolSize];

        for (int i = 0; i < afterimagePool.Length; i++)
        {
            GameObject ghost =
                new GameObject(
                    $"PrestigeDashAfterimage_{i}"
                );

            ghost.transform.SetParent(
                afterimagePoolRoot.transform,
                false
            );

            SpriteRenderer ghostRenderer =
                ghost.AddComponent<SpriteRenderer>();

            PrestigeAfterimageFade fade =
                ghost.AddComponent<PrestigeAfterimageFade>();

            fade.Prepare(ghostRenderer);
            ghost.SetActive(false);
            afterimagePool[i] = fade;
        }
    }

    private void PlayPrestigePulse(
        Vector3 worldPosition,
        float startScaleMultiplier,
        float finalScaleMultiplier,
        float duration,
        float alpha,
        Color color)
    {
        FindPlayerRenderer();

        if (playerRenderer == null ||
            playerRenderer.sprite == null)
        {
            return;
        }

        SpecialSkinPulseSprite pulse =
            GetPrestigePulse();

        if (pulse == null)
            return;

        pulse.Play(
            worldPosition,
            playerRenderer.sprite,
            playerRenderer.transform.rotation,
            playerRenderer.transform.lossyScale,
            playerRenderer.flipX,
            playerRenderer.flipY,
            startScaleMultiplier,
            finalScaleMultiplier,
            duration,
            alpha,
            color
        );
    }

    private PrestigeStyle GetPrestigeStyle()
    {
        PrestigeStyle style = new PrestigeStyle
        {
            secondaryColor = activeArmorColor,
            pulseScaleMultiplier = 1f
        };

        if (IsDark)
        {
            style.pulseScaleMultiplier = 1.08f;
        }
        else if (IsGolden)
        {
            style.pulseScaleMultiplier = 1.12f;
        }

        return style;
    }

    private SpecialSkinPulseSprite
        GetPrestigePulse()
    {
        EnsurePrestigePulsePool();

        if (prestigePulsePool == null ||
            prestigePulsePool.Length == 0)
        {
            return null;
        }

        for (int i = 0;
             i < prestigePulsePool.Length;
             i++)
        {
            int index =
                (prestigePulsePoolCursor + i) %
                prestigePulsePool.Length;

            SpecialSkinPulseSprite candidate =
                prestigePulsePool[index];

            if (candidate != null &&
                !candidate.gameObject.activeSelf)
            {
                prestigePulsePoolCursor =
                    (index + 1) %
                    prestigePulsePool.Length;

                return candidate;
            }
        }

        SpecialSkinPulseSprite fallback =
            prestigePulsePool[
                prestigePulsePoolCursor
            ];

        prestigePulsePoolCursor =
            (prestigePulsePoolCursor + 1) %
            prestigePulsePool.Length;

        return fallback;
    }

    private void EnsurePrestigePulsePool()
    {
        if (prestigePulsePool != null &&
            prestigePulsePool.Length ==
            PrestigePulsePoolSize)
        {
            return;
        }

        if (prestigePulsePoolRoot == null)
        {
            prestigePulsePoolRoot =
                new GameObject(
                    "PrestigeSkinPulsePool"
                );
        }

        FindPlayerRenderer();

        int sortingLayerId =
            playerRenderer != null
                ? playerRenderer.sortingLayerID
                : 0;

        int sortingOrder =
            playerRenderer != null
                ? playerRenderer.sortingOrder + 2
                : 29;

        prestigePulsePool =
            new SpecialSkinPulseSprite[
                PrestigePulsePoolSize
            ];

        for (int i = 0;
             i < prestigePulsePool.Length;
             i++)
        {
            GameObject pulseObject =
                new GameObject(
                    $"PrestigePulse_{i}"
                );

            pulseObject.transform.SetParent(
                prestigePulsePoolRoot.transform,
                false
            );

            SpecialSkinPulseSprite pulse =
                pulseObject.AddComponent<
                    SpecialSkinPulseSprite
                >();

            pulse.Prepare(
                sortingLayerId,
                sortingOrder
            );

            pulseObject.SetActive(false);
            prestigePulsePool[i] = pulse;
        }
    }

    private void CreateSpriteBurst(
        Vector3 worldPosition,
        Sprite sprite,
        float startWorldSize,
        float finalWorldSize,
        float duration,
        float alpha,
        Color tintColor)
    {
        EnsureBurstPool();

        if (burstPool == null ||
            burstPool.Length == 0)
        {
            return;
        }

        SpecialSkinCoinBurstSprite selected =
            null;

        for (int i = 0;
             i < burstPool.Length;
             i++)
        {
            int index =
                (burstPoolCursor + i) %
                burstPool.Length;

            SpecialSkinCoinBurstSprite candidate =
                burstPool[index];

            if (candidate != null &&
                !candidate.gameObject.activeSelf)
            {
                selected = candidate;

                burstPoolCursor =
                    (index + 1) %
                    burstPool.Length;

                break;
            }
        }

        if (selected == null)
        {
            selected =
                burstPool[
                    burstPoolCursor
                ];

            burstPoolCursor =
                (burstPoolCursor + 1) %
                burstPool.Length;
        }

        if (selected == null)
            return;

        selected.Play(
            worldPosition,
            sprite,
            startWorldSize,
            finalWorldSize,
            duration,
            alpha,
            tintColor
        );
    }

    private void EnsureBurstPool()
    {
        if (burstPool != null &&
            burstPool.Length ==
            BurstPoolSize)
        {
            return;
        }

        if (burstPoolRoot == null)
        {
            burstPoolRoot =
                new GameObject(
                    "SpecialSkinCoinBurstPool"
                );
        }

        FindPlayerRenderer();

        int sortingLayerId =
            playerRenderer != null
                ? playerRenderer.sortingLayerID
                : 0;

        int sortingOrder =
            playerRenderer != null
                ? playerRenderer.sortingOrder + 2
                : 20;

        burstPool =
            new SpecialSkinCoinBurstSprite[
                BurstPoolSize
            ];

        for (int i = 0;
             i < burstPool.Length;
             i++)
        {
            GameObject burstObject =
                new GameObject(
                    $"SpecialSkinCoinBurst_{i}"
                );

            burstObject.transform.SetParent(
                burstPoolRoot.transform,
                false
            );

            SpecialSkinCoinBurstSprite burst =
                burstObject.AddComponent<
                    SpecialSkinCoinBurstSprite
                >();

            burst.Prepare(
                sortingLayerId,
                sortingOrder
            );

            burstObject.SetActive(false);
            burstPool[i] = burst;
        }
    }

    private static Color MakeVisibleColor(
        Color color,
        Color fallback)
    {
        float maximumChannel =
            Mathf.Max(
                color.r,
                Mathf.Max(color.g, color.b)
            );

        if (maximumChannel <= 0.001f)
            color = fallback;

        color.a = 1f;
        return color;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        afterimageInterval =
            Mathf.Max(
                0.01f,
                afterimageInterval
            );

        afterimageLifetime =
            Mathf.Max(
                0.05f,
                afterimageLifetime
            );

        spawnBurstDuration =
            Mathf.Max(
                0.05f,
                spawnBurstDuration
            );

        deathBurstDuration =
            Mathf.Max(
                0.05f,
                deathBurstDuration
            );


        burstDuration =
            Mathf.Max(
                0.05f,
                burstDuration
            );

        finalBurstSizeMultiplier =
            Mathf.Max(
                1f,
                finalBurstSizeMultiplier
            );
    }
#endif

    private struct PrestigeStyle
    {
        public Color secondaryColor;
        public float pulseScaleMultiplier;
    }
}

/// <summary>
/// Short-lived dash ghost used by prestige skins.
/// </summary>
public class PrestigeAfterimageFade :
    MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float lifetime;
    private float elapsed;
    private Color startColor;

    public SpriteRenderer Renderer => spriteRenderer;

    public void Prepare(SpriteRenderer renderer)
    {
        spriteRenderer = renderer;
    }

    public void Initialize(
        SpriteRenderer renderer,
        float duration)
    {
        spriteRenderer = renderer;
        elapsed = 0f;

        lifetime =
            Mathf.Max(
                0.01f,
                duration
            );

        startColor =
            spriteRenderer != null
                ? spriteRenderer.color
                : Color.white;
    }

    private void Update()
    {
        elapsed +=
            Time.unscaledDeltaTime;

        float t =
            Mathf.Clamp01(
                elapsed / lifetime
            );

        if (spriteRenderer != null)
        {
            Color color = startColor;

            color.a =
                Mathf.Lerp(
                    startColor.a,
                    0f,
                    t
                );

            spriteRenderer.color = color;
        }

        if (elapsed >= lifetime)
            gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        elapsed = 0f;
    }
}

/// <summary>
/// A pooled tinted copy of the player's sprite used as the soft pulse layer
/// for prestige spawn and death effects.
/// </summary>
public class SpecialSkinPulseSprite :
    MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    private float duration;
    private float elapsed;
    private float maximumAlpha;

    private Vector3 startScale;
    private Vector3 finalScale;
    private Color pulseColor;

    public void Prepare(
        int sortingLayerId,
        int sortingOrder)
    {
        spriteRenderer =
            gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sortingLayerID =
            sortingLayerId;

        spriteRenderer.sortingOrder =
            sortingOrder;

        spriteRenderer.color = Color.clear;
    }

    public void Play(
        Vector3 worldPosition,
        Sprite sprite,
        Quaternion rotation,
        Vector3 playerWorldScale,
        bool flipX,
        bool flipY,
        float startScaleMultiplier,
        float finalScaleMultiplier,
        float effectDuration,
        float alpha,
        Color color)
    {
        if (spriteRenderer == null ||
            sprite == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        spriteRenderer.sprite = sprite;
        spriteRenderer.flipX = flipX;
        spriteRenderer.flipY = flipY;

        transform.position = worldPosition;
        transform.rotation = rotation;

        duration =
            Mathf.Max(0.05f, effectDuration);

        maximumAlpha =
            Mathf.Clamp01(alpha);

        pulseColor = color;
        pulseColor.a = maximumAlpha;

        elapsed = 0f;

        startScale =
            Vector3.Scale(
                playerWorldScale,
                Vector3.one *
                Mathf.Max(0.01f, startScaleMultiplier)
            );

        finalScale =
            Vector3.Scale(
                playerWorldScale,
                Vector3.one *
                Mathf.Max(
                    startScaleMultiplier,
                    finalScaleMultiplier
                )
            );

        transform.localScale = startScale;
        spriteRenderer.color = pulseColor;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;

        float t =
            Mathf.Clamp01(
                elapsed / duration
            );

        float eased =
            1f - Mathf.Pow(1f - t, 3f);

        transform.localScale =
            Vector3.Lerp(
                startScale,
                finalScale,
                eased
            );

        Color color = pulseColor;

        color.a =
            maximumAlpha *
            Mathf.Pow(1f - t, 2f);

        if (spriteRenderer != null)
            spriteRenderer.color = color;

        if (elapsed >= duration)
            gameObject.SetActive(false);
    }
}

/// <summary>
/// Pooled sprite-based coin collection effect.
///
/// Starts very small at the collected coin,
/// expands beyond the coin,
/// and fades out while expanding.
/// </summary>
public class SpecialSkinCoinBurstSprite :
    MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    private float duration;
    private float elapsed;
    private float maxAlpha;

    private Vector3 startScale;
    private Vector3 finalScale;
    private Color activeTintColor = Color.white;

    public void Prepare(
        int sortingLayerId,
        int sortingOrder)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer =
                gameObject.AddComponent<
                    SpriteRenderer
                >();
        }

        spriteRenderer.sortingLayerID =
            sortingLayerId;

        spriteRenderer.sortingOrder =
            sortingOrder;

        spriteRenderer.color =
            Color.clear;
    }

    public void Play(
        Vector3 worldPosition,
        Sprite sprite,
        float startWorldSize,
        float finalWorldSize,
        float effectDuration,
        float alpha,
        Color tintColor)
    {
        if (sprite == null ||
            spriteRenderer == null)
        {
            gameObject.SetActive(false);
            return;
        }

        spriteRenderer.sprite = sprite;

        duration =
            Mathf.Max(
                0.05f,
                effectDuration
            );

        maxAlpha =
            Mathf.Clamp01(alpha);

        elapsed = 0f;

        transform.position =
            worldPosition;

        transform.rotation =
            Quaternion.identity;

        float spriteLocalSize =
            Mathf.Max(
                sprite.bounds.size.x,
                sprite.bounds.size.y
            );

        if (spriteLocalSize <= 0.001f)
            spriteLocalSize = 1f;

        float startScaleValue =
            Mathf.Max(
                0.001f,
                startWorldSize /
                spriteLocalSize
            );

        float finalScaleValue =
            Mathf.Max(
                startScaleValue,
                finalWorldSize /
                spriteLocalSize
            );

        startScale =
            Vector3.one *
            startScaleValue;

        finalScale =
            Vector3.one *
            finalScaleValue;

        transform.localScale =
            startScale;

        activeTintColor = MakeRenderableTint(tintColor);

        Color startColor = activeTintColor;
        startColor.a = maxAlpha;
        spriteRenderer.color = startColor;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }


    private static Color MakeRenderableTint(Color color)
    {
        float maximumChannel = Mathf.Max(
            color.r,
            Mathf.Max(color.g, color.b)
        );

        if (maximumChannel <= 0.001f)
            color = Color.white;

        color.a = 1f;
        return color;
    }

    private void Update()
    {
        elapsed +=
            Time.unscaledDeltaTime;

        float t =
            Mathf.Clamp01(
                elapsed / duration
            );

        float expandT =
            1f -
            Mathf.Pow(
                1f - t,
                3f
            );

        transform.localScale =
            Vector3.Lerp(
                startScale,
                finalScale,
                expandT
            );

        float fade =
            1f - t;

        fade *= fade;

        if (spriteRenderer != null)
        {
            Color color = activeTintColor;
            color.a = maxAlpha * fade;
            spriteRenderer.color = color;
        }

        if (elapsed >= duration)
            gameObject.SetActive(false);
    }
}