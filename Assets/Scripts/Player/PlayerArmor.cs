using System.Collections;
using UnityEngine;

public class PlayerArmor : MonoBehaviour
{
    [Header("References")]
    public SoundManager soundManager;
    public SpriteRenderer playerSpriteRenderer;

    [Header("Visual")]
    public GameObject shieldVisual;

    [SerializeField]
    private SpriteRenderer shieldSpriteRenderer;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Bütün skinlerde Armor Visual için kullanılan ortak alpha.")]
    private float armorVisualAlpha = 0.55f;

    [SerializeField, Range(0.1f, 1.5f)]
    [Tooltip("Armor renginin genel parlaklığı.")]
    private float armorVisualIntensity = 0.85f;

    [SerializeField]
    [Tooltip(
        "Mevcut mor shield sprite'ın ana rengi. " +
        "Hedef skin rengini doğru göstermek için otomatik dengelenir."
    )]
    private Color sourceSpriteReferenceColor =
        new Color(0.60f, 0.46f, 0.87f, 1f);

    [SerializeField, Min(1f)]
    [Tooltip("Renk dengelemesinde kullanılabilecek en yüksek HDR kanal değeri.")]
    private float maximumTintChannel = 2.25f;

    [Header("Break Effect")]
    [Min(0.01f)]
    public float breakScaleDuration = 0.18f;

    [Header("Immune After Break")]
    [Min(0f)]
    public float immuneDuration = 0.8f;

    [Range(0f, 1f)]
    [Tooltip("Dokunulmazlik pulse'inda Player'in inecegi minimum alpha. Dusuk deger daha guclu ve belirgin pulse verir.")]
    public float immuneMinimumAlpha = 0.18f;

    [Min(0.01f)]
    [Tooltip("Dokunulmazlik alpha pulse hizi. 12 degeri yaklasik saniyede 6 tam pulse verir.")]
    public float immuneBlinkSpeed = 12f;

    [Range(1f, 4f)]
    [Tooltip("Pulse'un dusuk alpha bolgesini ne kadar vurgulayacagi. Yuksek deger Player'i daha belirgin sekilde yanip sonuyor gibi gosterir.")]
    public float immunePulseStrength = 2.2f;

    private Vector3 shieldOriginalScale;
    private float playerOriginalAlpha = 1f;
    private Color currentArmorVisualColor = Color.white;

    private Coroutine breakRoutine;
    private Coroutine immuneRoutine;

    public bool IsImmune { get; private set; }
    public bool HasArmor { get; private set; }

    // Void Clone, armor aktifken oyuncudaki mevcut shield visual'in
    // birebir gorsel kopyasini olusturmak icin bunu kullanir.
    public GameObject ArmorVisualObject => shieldVisual;

    private void Awake()
    {
        if (playerSpriteRenderer == null)
            playerSpriteRenderer = GetComponent<SpriteRenderer>();

        if (playerSpriteRenderer == null)
            playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (playerSpriteRenderer != null)
            playerOriginalAlpha = playerSpriteRenderer.color.a;

        if (soundManager == null)
            soundManager = FindAnyObjectByType<SoundManager>();

        FindShieldSpriteRenderer();

        if (shieldVisual != null)
        {
            shieldOriginalScale =
                shieldVisual.transform.localScale;

            shieldVisual.SetActive(false);
        }
    }

    public void ActivateArmor()
    {
        HasArmor = true;

        if (breakRoutine != null)
        {
            StopCoroutine(breakRoutine);
            breakRoutine = null;
        }

        if (shieldVisual != null)
        {
            ApplyVisualColor(
                currentArmorVisualColor
            );

            shieldVisual.transform.localScale =
                shieldOriginalScale;

            shieldVisual.SetActive(true);
        }
    }

    public void ApplyVisualColor(Color targetColor)
    {
        currentArmorVisualColor = targetColor;

        FindShieldSpriteRenderer();

        if (shieldSpriteRenderer == null)
            return;

        shieldSpriteRenderer.color =
            BuildCompensatedRendererTint(
                targetColor
            );
    }

    private Color BuildCompensatedRendererTint(
        Color targetColor
    )
    {
        Color normalizedTarget =
            NormalizeHdrColor(targetColor);

        normalizedTarget *= armorVisualIntensity;

        Color source = sourceSpriteReferenceColor;

        float redSource =
            Mathf.Max(0.05f, source.r);

        float greenSource =
            Mathf.Max(0.05f, source.g);

        float blueSource =
            Mathf.Max(0.05f, source.b);

        Color compensatedTint = new Color(
            normalizedTarget.r / redSource,
            normalizedTarget.g / greenSource,
            normalizedTarget.b / blueSource,
            armorVisualAlpha
        );

        compensatedTint.r = Mathf.Min(
            compensatedTint.r,
            maximumTintChannel
        );

        compensatedTint.g = Mathf.Min(
            compensatedTint.g,
            maximumTintChannel
        );

        compensatedTint.b = Mathf.Min(
            compensatedTint.b,
            maximumTintChannel
        );

        compensatedTint.a = armorVisualAlpha;

        return compensatedTint;
    }

    private static Color NormalizeHdrColor(Color color)
    {
        float highestChannel = Mathf.Max(
            color.r,
            color.g,
            color.b
        );

        if (highestChannel > 1f)
        {
            color.r /= highestChannel;
            color.g /= highestChannel;
            color.b /= highestChannel;
        }

        color.a = 1f;
        return color;
    }

    public void BreakArmor()
    {
        if (!HasArmor)
            return;

        HasArmor = false;

        // Immunity is core gameplay state, so establish it before optional
        // stats/audio/haptics. A device-specific feedback/SDK exception must
        // never consume armor and leave the player immediately vulnerable.
        StartImmunity();

        try
        {
            StatsManager.AddArmorSave();

            if (soundManager != null)
                soundManager.PlayArmorBreakSound(transform.position);

            VibrationManager.Instance?.VibrateArmorBreak();

            CameraShake.Instance?.Shake(
                0.18f,
                0.14f
            );
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "[PlayerArmor] Optional armor-break feedback/stat call failed. " +
                "Immunity remains active and gameplay continues.",
                this
            );
            Debug.LogException(exception, this);
        }

        if (shieldVisual == null)
            return;

        if (breakRoutine != null)
            StopCoroutine(breakRoutine);

        breakRoutine =
            StartCoroutine(BreakScaleEffect());
    }

    private void StartImmunity()
    {
        if (immuneRoutine != null)
            StopCoroutine(immuneRoutine);

        immuneRoutine =
            StartCoroutine(ImmuneRoutine());
    }

    private IEnumerator ImmuneRoutine()
    {
        IsImmune = true;

        if (immuneDuration <= 0f)
        {
            SetPlayerAlpha(playerOriginalAlpha);

            IsImmune = false;
            immuneRoutine = null;

            yield break;
        }

        float timer = 0f;

        while (timer < immuneDuration)
        {
            timer += Time.deltaTime;

            // 1 -> 0 -> 1 seklinde hizli ama smooth bir pulse.
            // Pow uygulanarak dusuk alpha bolgesi daha uzun ve daha belirgin
            // tutulur; bu sayede armor kirildiktan sonraki dokunulmazlik
            // mobil ekranda bile kolayca okunur.
            float pulse =
                1f - Mathf.PingPong(
                    timer * immuneBlinkSpeed,
                    1f
                );

            pulse = Mathf.SmoothStep(
                0f,
                1f,
                pulse
            );

            pulse = Mathf.Pow(
                pulse,
                Mathf.Max(1f, immunePulseStrength)
            );

            float minimumAlpha =
                Mathf.Min(
                    Mathf.Clamp01(immuneMinimumAlpha),
                    playerOriginalAlpha
                );

            float alpha =
                Mathf.Lerp(
                    minimumAlpha,
                    playerOriginalAlpha,
                    pulse
                );

            SetPlayerAlpha(alpha);

            yield return null;
        }

        SetPlayerAlpha(playerOriginalAlpha);

        IsImmune = false;
        immuneRoutine = null;
    }

    private IEnumerator BreakScaleEffect()
    {
        if (shieldVisual == null)
        {
            breakRoutine = null;
            yield break;
        }

        Vector3 startScale =
            shieldVisual.transform.localScale;

        float timer = 0f;

        while (timer < breakScaleDuration)
        {
            timer += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    timer / breakScaleDuration
                );

            t *= t;

            shieldVisual.transform.localScale =
                Vector3.Lerp(
                    startScale,
                    Vector3.zero,
                    t
                );

            yield return null;
        }

        shieldVisual.SetActive(false);

        shieldVisual.transform.localScale =
            shieldOriginalScale;

        breakRoutine = null;
    }

    private void SetPlayerAlpha(float alpha)
    {
        if (playerSpriteRenderer == null)
            return;

        Color color = playerSpriteRenderer.color;
        color.a = alpha;
        playerSpriteRenderer.color = color;
    }

    private void FindShieldSpriteRenderer()
    {
        if (shieldSpriteRenderer != null)
            return;

        if (shieldVisual == null)
            return;

        shieldSpriteRenderer =
            shieldVisual.GetComponentInChildren<SpriteRenderer>(
                true
            );
    }

    private void OnDisable()
    {
        if (immuneRoutine != null)
        {
            StopCoroutine(immuneRoutine);
            immuneRoutine = null;
        }

        if (breakRoutine != null)
        {
            StopCoroutine(breakRoutine);
            breakRoutine = null;
        }

        IsImmune = false;

        SetPlayerAlpha(playerOriginalAlpha);
    }

    private void OnValidate()
    {
        armorVisualAlpha =
            Mathf.Clamp01(armorVisualAlpha);

        armorVisualIntensity =
            Mathf.Clamp(
                armorVisualIntensity,
                0.1f,
                1.5f
            );

        immuneMinimumAlpha =
            Mathf.Clamp01(
                immuneMinimumAlpha
            );

        immuneBlinkSpeed =
            Mathf.Max(
                0.01f,
                immuneBlinkSpeed
            );

        immunePulseStrength =
            Mathf.Clamp(
                immunePulseStrength,
                1f,
                4f
            );

        maximumTintChannel =
            Mathf.Max(1f, maximumTintChannel);

        sourceSpriteReferenceColor.r =
            Mathf.Max(
                0.05f,
                sourceSpriteReferenceColor.r
            );

        sourceSpriteReferenceColor.g =
            Mathf.Max(
                0.05f,
                sourceSpriteReferenceColor.g
            );

        sourceSpriteReferenceColor.b =
            Mathf.Max(
                0.05f,
                sourceSpriteReferenceColor.b
            );
    }
}
