using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class VoidCloneAbility : MonoBehaviour
{
    public static Transform ActiveCloneTarget { get; private set; }
    public static bool HasActiveClone => ActiveCloneTarget != null;

    [Header("Clone")]
    public GameObject clonePrefab;
    public float cloneDuration = 3f;

    [Header("Cooldown")]
    [Tooltip("Starts only after the clone disappears.")]
    public float cloneCooldown = 12f;

    [Header("UI")]
    public Button cloneButton;
    public Image cloneButtonImage;
    public Image cooldownFill;
    public TMP_Text cooldownText;
    public Sprite readySprite;
    public Sprite usedSprite;

    [Header("Clone Active Overlay")]
    [Tooltip(
        "Clone aktifken buttonu ustten asagi dolduran overlay rengi."
    )]
    public Color activeOverlayColor = Color.black;

    [Header("References")]
    public PlayerMovement playerMovement;
    public PlayerArmor playerArmor;
    public SoundManager soundManager;
    public PlayerSkinApplier playerSkinApplier;

    private bool canUseClone = true;
    private bool cloneActive;
    private bool gameOverHandled;
    private bool cooldownActive;

    private float activeTimer;
    private float cooldownTimer;
    private float textRefreshTimer;

    private const float TextRefreshInterval = 0.1f;

    private GameObject activeCloneObject;
    private Coroutine cloneRoutine;

    private EventTrigger cloneButtonEventTrigger;
    private EventTrigger.Entry pointerDownEntry;
    private bool immediatePressConfigured;
    private UIButtonEffect cloneButtonEffect;

    // Active fazinda cooldownFill'i gecici olarak Vertical/Top yapacagiz.
    // Cooldown basladiginda Inspector'daki normal ayarlara geri doner.
    private bool cooldownFillDefaultsCaptured;
    private Color cooldownFillDefaultColor;
    private Image.Type cooldownFillDefaultType;
    private Image.FillMethod cooldownFillDefaultMethod;
    private int cooldownFillDefaultOrigin;
    private bool cooldownFillDefaultClockwise;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerArmor == null)
            playerArmor = GetComponent<PlayerArmor>();

        if (playerSkinApplier == null)
            playerSkinApplier = GetComponent<PlayerSkinApplier>();

        if (soundManager == null)
            soundManager = SoundManager.Instance;

        if (soundManager == null)
            soundManager = FindAnyObjectByType<SoundManager>();

        PreloadCloneSound();
        CaptureCooldownFillDefaults();
        ConfigureAbilityButton();
        ResetCloneState();
    }

    private void OnEnable()
    {
        ConfigureAbilityButton();
        ResetCloneState();
    }

    private void OnDisable()
    {
        ClearAllCloneState();
    }

    private void OnDestroy()
    {
        RemoveImmediateButtonPress();
        ClearAllCloneState();
    }

    private void Update()
    {
        if (!GameStateManager.IsGameplayStarted)
            return;

        if (playerMovement != null && playerMovement.IsGameOver)
        {
            HandleGameOver();
            return;
        }

        bool keyboardPressed =
            Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame;

        bool gamepadPressed =
            Gamepad.current != null &&
            Gamepad.current.rightShoulder.wasPressedThisFrame;

        if (keyboardPressed || gamepadPressed)
            UseClone();

        if (cooldownActive)
            UpdateCooldown();
    }

    public void SetCloneButton(Button button)
    {
        if (cloneButton == button)
        {
            ConfigureAbilityButton();
            return;
        }

        RemoveImmediateButtonPress();
        cloneButton = button;
        cloneButtonEffect = null;
        ConfigureAbilityButton();
        UpdateUI();
    }

    private void ConfigureAbilityButton()
    {
        if (cloneButton == null)
            return;

        ConfigureImmediateButtonPress();

        if (cloneButtonEffect == null)
        {
            cloneButtonEffect =
                cloneButton.GetComponent<UIButtonEffect>();

            if (cloneButtonEffect == null)
            {
                cloneButtonEffect =
                    cloneButton.gameObject.AddComponent<UIButtonEffect>();
            }
        }

        cloneButtonEffect.ConfigureAbilityFeedback(
            UIButtonEffect.AbilityFeedbackStyle.Clone
        );
    }

    private void ConfigureImmediateButtonPress()
    {
        if (immediatePressConfigured || cloneButton == null)
            return;

        cloneButtonEventTrigger =
            cloneButton.GetComponent<EventTrigger>();

        if (cloneButtonEventTrigger == null)
        {
            cloneButtonEventTrigger =
                cloneButton.gameObject.AddComponent<EventTrigger>();
        }

        if (cloneButtonEventTrigger.triggers == null)
        {
            cloneButtonEventTrigger.triggers =
                new List<EventTrigger.Entry>();
        }

        pointerDownEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerDown
        };

        pointerDownEntry.callback.AddListener(
            _ => UseClone()
        );

        cloneButtonEventTrigger.triggers.Add(
            pointerDownEntry
        );

        immediatePressConfigured = true;
    }

    private void RemoveImmediateButtonPress()
    {
        if (!immediatePressConfigured ||
            cloneButtonEventTrigger == null ||
            pointerDownEntry == null ||
            cloneButtonEventTrigger.triggers == null)
        {
            return;
        }

        cloneButtonEventTrigger.triggers.Remove(
            pointerDownEntry
        );

        pointerDownEntry = null;
        cloneButtonEventTrigger = null;
        immediatePressConfigured = false;
    }

    private void PreloadCloneSound()
    {
        if (soundManager == null ||
            soundManager.voidCloneSound == null)
        {
            return;
        }

        if (soundManager.voidCloneSound.loadState ==
            AudioDataLoadState.Unloaded)
        {
            soundManager.voidCloneSound.LoadAudioData();
        }
    }

    private void HandleGameOver()
    {
        if (gameOverHandled)
            return;

        gameOverHandled = true;
        ClearAllCloneState();

        if (cloneButton != null)
            cloneButton.interactable = false;

        HideCooldownUI();
    }

    public void SetCloneCooldown(float cooldown)
    {
        cloneCooldown = Mathf.Max(0.1f, cooldown);
    }

    public void UseClone()
    {
        if (!isActiveAndEnabled ||
            !GameStateManager.IsGameplayStarted ||
            Time.timeScale <= 0f ||
            gameOverHandled ||
            !canUseClone ||
            cloneActive ||
            cooldownActive)
        {
            return;
        }

        if (playerMovement != null && playerMovement.IsGameOver)
            return;

        if (clonePrefab == null)
            return;

        canUseClone = false;
        cloneActive = true;
        activeTimer = Mathf.Max(0.01f, cloneDuration);

        StatsManager.AddCloneUse();

        // PointerDown uzerinden calistigi icin mobilde parmak kaldirilmasini
        // beklemeden clone ve SFX ayni anda baslar.
        if (soundManager != null)
            soundManager.PlayVoidCloneSound(transform.position);

        VibrationManager.Instance?.VibrateClone();
        cloneButtonEffect?.PlayAbilityActivation();

        ShowActiveUI();
        UpdateActiveVisuals();
        UpdateUI();

        cloneRoutine = StartCoroutine(CloneRoutine());
    }

    private IEnumerator CloneRoutine()
    {
        activeCloneObject = Instantiate(
            clonePrefab,
            transform.position,
            Quaternion.identity
        );

        ActiveCloneTarget = activeCloneObject.transform;

        VoidClone cloneScript =
            activeCloneObject.GetComponent<VoidClone>();

        if (cloneScript != null)
        {
            cloneScript.SetSkin(GetCurrentPlayerSprite());
            cloneScript.CopyArmorVisual(playerArmor);
            cloneScript.StartClone(cloneDuration, playerMovement);
        }

        activeTimer = Mathf.Max(0.01f, cloneDuration);

        while (activeTimer > 0f)
        {
            activeTimer = Mathf.Max(
                0f,
                activeTimer - Time.deltaTime
            );

            UpdateActiveVisuals();
            yield return null;
        }

        cloneRoutine = null;
        DestroyCloneObject();
        BeginCooldown();
    }

    private Sprite GetCurrentPlayerSprite()
    {
        if (playerSkinApplier != null &&
            playerSkinApplier.CurrentSprite != null)
        {
            return playerSkinApplier.CurrentSprite;
        }

        SpriteRenderer renderer =
            GetComponentInChildren<SpriteRenderer>(true);

        return renderer != null
            ? renderer.sprite
            : null;
    }

    private void DestroyCloneObject()
    {
        ActiveCloneTarget = null;

        if (activeCloneObject != null)
            Destroy(activeCloneObject);

        activeCloneObject = null;
        cloneActive = false;
        activeTimer = 0f;
    }

    private void BeginCooldown()
    {
        cooldownActive = true;
        cooldownTimer = cloneCooldown;
        textRefreshTimer = 0f;

        ShowCooldownUI();
        UpdateCooldownVisuals();
        UpdateUI();
    }

    private void UpdateCooldown()
    {
        cooldownTimer = Mathf.Max(
            0f,
            cooldownTimer - Time.deltaTime
        );

        UpdateCooldownVisuals();

        if (cooldownTimer > 0f)
            return;

        cooldownActive = false;
        canUseClone = true;

        HideCooldownUI();
        UpdateUI();
        cloneButtonEffect?.PlayReadyPulse();
    }

    private void UpdateActiveVisuals()
    {
        if (cooldownFill == null)
            return;

        float duration = Mathf.Max(
            0.01f,
            cloneDuration
        );

        // Clone basladiginda 0, clone biterken 1.
        // Vertical + Top origin sayesinde siyahlik ustten asagi iner.
        float activeProgress = 1f - Mathf.Clamp01(
            activeTimer / duration
        );

        cooldownFill.fillAmount = activeProgress;
    }

    private void UpdateCooldownVisuals()
    {
        if (cooldownFill != null)
        {
            cooldownFill.fillAmount = cloneCooldown <= 0f
                ? 0f
                : Mathf.Clamp01(cooldownTimer / cloneCooldown);
        }

        textRefreshTimer -= Time.deltaTime;

        if (textRefreshTimer > 0f)
            return;

        textRefreshTimer = TextRefreshInterval;

        if (cooldownText != null)
        {
            cooldownText.text = cooldownTimer > 0f
                ? cooldownTimer.ToString("F1")
                : "";
        }
    }

    private void ShowActiveUI()
    {
        if (cooldownFill != null)
        {
            CaptureCooldownFillDefaults();

            cooldownFill.gameObject.SetActive(true);
            cooldownFill.color = activeOverlayColor;
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.fillMethod = Image.FillMethod.Vertical;
            cooldownFill.fillOrigin =
                (int)Image.OriginVertical.Top;
            cooldownFill.fillAmount = 0f;
        }

        // Clone aktifken hicbir yazi yok.
        if (cooldownText != null)
        {
            cooldownText.text = "";
            cooldownText.gameObject.SetActive(false);
        }
    }

    private void ShowCooldownUI()
    {
        RestoreCooldownFillDefaults();

        if (cooldownFill != null)
        {
            cooldownFill.gameObject.SetActive(true);
            cooldownFill.fillAmount = 1f;
        }

        if (cooldownText != null)
            cooldownText.gameObject.SetActive(true);
    }

    private void HideCooldownUI()
    {
        RestoreCooldownFillDefaults();

        if (cooldownFill != null)
        {
            cooldownFill.gameObject.SetActive(false);
            cooldownFill.fillAmount = 0f;
        }

        if (cooldownText != null)
        {
            cooldownText.text = "";
            cooldownText.gameObject.SetActive(false);
        }
    }

    private void CaptureCooldownFillDefaults()
    {
        if (cooldownFillDefaultsCaptured ||
            cooldownFill == null)
        {
            return;
        }

        cooldownFillDefaultColor =
            cooldownFill.color;

        cooldownFillDefaultType =
            cooldownFill.type;

        cooldownFillDefaultMethod =
            cooldownFill.fillMethod;

        cooldownFillDefaultOrigin =
            cooldownFill.fillOrigin;

        cooldownFillDefaultClockwise =
            cooldownFill.fillClockwise;

        cooldownFillDefaultsCaptured = true;
    }

    private void RestoreCooldownFillDefaults()
    {
        if (!cooldownFillDefaultsCaptured ||
            cooldownFill == null)
        {
            return;
        }

        cooldownFill.color =
            cooldownFillDefaultColor;

        cooldownFill.type =
            cooldownFillDefaultType;

        cooldownFill.fillMethod =
            cooldownFillDefaultMethod;

        cooldownFill.fillOrigin =
            cooldownFillDefaultOrigin;

        cooldownFill.fillClockwise =
            cooldownFillDefaultClockwise;
    }

    private void ClearAllCloneState()
    {
        if (cloneRoutine != null)
        {
            StopCoroutine(cloneRoutine);
            cloneRoutine = null;
        }

        DestroyCloneObject();

        cooldownActive = false;
        cooldownTimer = 0f;
        textRefreshTimer = 0f;
    }

    public void ResetCloneState()
    {
        StopAllCoroutines();
        cloneRoutine = null;

        DestroyCloneObject();

        canUseClone = true;
        cooldownActive = false;
        gameOverHandled = false;
        activeTimer = 0f;
        cooldownTimer = 0f;
        textRefreshTimer = 0f;

        HideCooldownUI();
        UpdateUI();
    }

    private void UpdateUI()
    {
        bool usable =
            canUseClone &&
            !cloneActive &&
            !cooldownActive &&
            !gameOverHandled;

        if (cloneButton != null)
        {
            // Clone aktifken Button'i Unity'nin Disabled rengine gecirmiyoruz;
            // aksi halde ustten asagi dolan siyah overlay gorunmeden
            // buton bir anda kararirdi. Tekrar basmalar UseClone guard'inda engelli.
            cloneButton.interactable =
                cloneActive && !gameOverHandled
                    ? true
                    : usable;
        }

        if (cloneButtonImage != null)
        {
            if (cloneActive)
            {
                cloneButtonImage.sprite = readySprite;
            }
            else
            {
                cloneButtonImage.sprite = usable
                    ? readySprite
                    : usedSprite;
            }
        }
    }
}
