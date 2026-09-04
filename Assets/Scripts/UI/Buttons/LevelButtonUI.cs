using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelButtonUI : MonoBehaviour,
    IUIScheduledVisual,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private const string UnlockedLevelKey = "UnlockedLevel";
    private const string CompletedLevelKeyPrefix = "CompletedLevel_";

    [Header("References")]
    public Button button;
    public Image buttonImage;
    public TMP_Text levelText;

    [Header("Unlocked Sprites")]
    public Sprite unlockedNormalSprite;
    public Sprite unlockedHighlightedSprite;

    [Header("Locked Sprites")]
    public Sprite lockedNormalSprite;
    public Sprite lockedHighlightedSprite;

    [Header("Completed Sprites")]
    public Sprite completedNormalSprite;
    public Sprite completedHighlightedSprite;

    [Header("Text Colors")]
    public Color unlockedTextColor = Color.white;
    public Color completedTextColor =
        new Color32(255, 200, 70, 255);

    [Header("Scale")]
    [Tooltip("Optional. If left empty, this level button's own RectTransform is animated.")]
    [SerializeField] private RectTransform scaleTarget;

    [Min(0f)]
    public float hoverScale = 1.08f;

    [Min(0f)]
    public float clickScale = 0.95f;

    [Min(0f)]
    public float transitionSpeed = 10f;

    [SerializeField, Min(0.000001f)]
    private float settleThreshold = 0.00005f;

    private LevelConfig config;
    private LevelSelectPanel panel;

    private bool unlocked;
    private bool completed;
    private bool hovering;
    private bool pressing;

    private Vector3 originalScale;

    private void Awake()
    {
        ResolveReferences();

        originalScale = scaleTarget != null
            ? scaleTarget.localScale
            : Vector3.one;

        // A UI template can occasionally be left at zero scale by an editor or
        // previous animation state. Never cache zero as the button's permanent
        // resting scale, otherwise every hover/reset tween will drive it away.
        if (originalScale.sqrMagnitude < 0.0001f)
            originalScale = Vector3.one;

        if (levelText != null)
            levelText.raycastTarget = false;
    }

    private void OnEnable()
    {
        PlayerSkinCatalog.SelectedSkinChanged -= HandleSelectedSkinChanged;
        PlayerSkinCatalog.SelectedSkinChanged += HandleSelectedSkinChanged;
    }

    private void OnDisable()
    {
        PlayerSkinCatalog.SelectedSkinChanged -= HandleSelectedSkinChanged;
        UIScaleTweenRunner.CancelScheduledVisual(this);

        hovering = false;
        pressing = false;

        UIScaleTweenRunner.CancelAndSnap(
            scaleTarget,
            originalScale
        );

        ApplyNormalSprite();
    }

    private void OnDestroy()
    {
        PlayerSkinCatalog.SelectedSkinChanged -= HandleSelectedSkinChanged;

        UIScaleTweenRunner.CancelScheduledVisual(this);
        UIScaleTweenRunner.Cancel(scaleTarget);

        if (button != null)
            button.onClick.RemoveListener(PlayLevel);
    }

    public void Setup(
        LevelConfig levelConfig,
        LevelSelectPanel owner)
    {
        config = levelConfig;
        panel = owner;

        ResolveReferences();

        hovering = false;
        pressing = false;

        // Setup is the authoritative visual reset for a freshly-created page
        // button. This also cancels any stale shared tween before the panel
        // transition gets a chance to render the first frame.
        UIScaleTweenRunner.CancelAndSnap(
            scaleTarget,
            originalScale
        );

        if (button != null)
        {
            button.onClick.RemoveListener(PlayLevel);
            button.onClick.AddListener(PlayLevel);
        }

        Refresh();

        // Level buttons are instantiated when the panel opens, so apply
        // the currently equipped skin theme immediately.
        SkinUIButtonThemeController.ApplyButtonTheme(button);
    }

    public void Refresh()
    {
        if (config == null)
        {
            SetInvalidState();
            return;
        }

        int unlockedLevel = PlayerPrefs.GetInt(
            UnlockedLevelKey,
            1
        );

        unlocked = config.levelNumber <= unlockedLevel;

        completed = PlayerPrefs.GetInt(
            CompletedLevelKeyPrefix + config.levelNumber,
            0
        ) == 1;

        // Locked buttons intentionally remain interactable so the game can
        // play its locked SFX/vibration feedback when the player presses one.
        if (button != null && !button.interactable)
            button.interactable = true;

        ApplyCurrentSprite();
        RefreshLevelText();
        AnimateToCurrentState();
    }

    private void PlayLevel()
    {
        if (config == null)
            return;

        if (panel != null && panel.IsMissionBriefingOpen)
            return;

        if (!unlocked)
        {
            SoundManager.Instance?.PlayLockedLevelSound(button != null ? button.transform as RectTransform : transform as RectTransform);
            VibrationManager.Instance?.VibrateUI();
            return;
        }

        SoundManager.Instance?.PlayMissionSelectSound(button != null ? button.transform as RectTransform : transform as RectTransform);

        if (panel != null)
        {
            panel.ShowMissionBriefing(config);
        }
        else
        {
            Debug.LogWarning(
                "LevelButtonUI has no LevelSelectPanel reference.",
                this
            );
        }
    }

    private void RefreshLevelText()
    {
        if (levelText == null)
            return;

        if (!unlocked)
        {
            if (!string.IsNullOrEmpty(levelText.text))
                levelText.text = string.Empty;

            if (levelText.alpha != 0f)
                levelText.alpha = 0f;

            return;
        }

        string desiredText = config.levelNumber.ToString();

        if (levelText.text != desiredText)
            levelText.text = desiredText;

        if (levelText.alpha != 1f)
            levelText.alpha = 1f;

        Color desiredColor = completed
            ? GetCompletedLevelTextColor()
            : unlockedTextColor;

        if (levelText.color != desiredColor)
            levelText.color = desiredColor;
    }

    private Color GetCompletedLevelTextColor()
    {
        // Completed levels use the fixed completion color configured on
        // the LevelButtonUI (green in the current prefab). This keeps
        // completed and uncompleted levels readable even with White/Silver skins.
        return completedTextColor;
    }

    private void HandleSelectedSkinChanged()
    {
        if (!isActiveAndEnabled)
            return;

        RefreshLevelText();
    }

    private void ApplyCurrentSprite()
    {
        if (hovering)
            ApplyHighlightedSprite();
        else
            ApplyNormalSprite();
    }

    private void ApplyNormalSprite()
    {
        if (buttonImage == null)
            return;

        Sprite desiredSprite;

        if (!unlocked)
        {
            desiredSprite = lockedNormalSprite;
        }
        else if (completed && completedNormalSprite != null)
        {
            desiredSprite = completedNormalSprite;
        }
        else
        {
            desiredSprite = unlockedNormalSprite;
        }

        SetButtonSpriteIfChanged(desiredSprite);
    }

    private void ApplyHighlightedSprite()
    {
        if (buttonImage == null)
            return;

        Sprite desiredSprite;

        if (!unlocked)
        {
            desiredSprite = lockedHighlightedSprite != null
                ? lockedHighlightedSprite
                : lockedNormalSprite;
        }
        else if (completed)
        {
            desiredSprite = completedHighlightedSprite != null
                ? completedHighlightedSprite
                : completedNormalSprite != null
                    ? completedNormalSprite
                    : unlockedNormalSprite;
        }
        else
        {
            desiredSprite = unlockedHighlightedSprite != null
                ? unlockedHighlightedSprite
                : unlockedNormalSprite;
        }

        SetButtonSpriteIfChanged(desiredSprite);
    }

    private void SetButtonSpriteIfChanged(Sprite desiredSprite)
    {
        if (buttonImage == null || desiredSprite == null)
            return;

        if (buttonImage.sprite != desiredSprite)
            buttonImage.sprite = desiredSprite;
    }

    private void AnimateToCurrentState()
    {
        if (scaleTarget == null)
            return;

        Vector3 desiredScale;

        if (pressing)
        {
            desiredScale = originalScale * clickScale;
        }
        else if (hovering)
        {
            desiredScale = originalScale * hoverScale;
        }
        else
        {
            desiredScale = originalScale;
        }

        UIScaleTweenRunner.TweenTo(
            scaleTarget,
            desiredScale,
            transitionSpeed,
            settleThreshold
        );
    }

    private void SetInvalidState()
    {
        unlocked = false;
        completed = false;

        if (button != null)
            button.interactable = false;

        if (levelText != null)
        {
            if (!string.IsNullOrEmpty(levelText.text))
                levelText.text = string.Empty;

            if (levelText.alpha != 0f)
                levelText.alpha = 0f;
        }

        ApplyNormalSprite();
        AnimateToCurrentState();
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (scaleTarget == null)
            scaleTarget = transform as RectTransform;

        if (buttonImage == null && button != null)
            buttonImage = button.targetGraphic as Image;

        if (buttonImage == null)
            buttonImage = GetComponent<Image>();

        if (levelText == null)
            levelText = GetComponentInChildren<TMP_Text>(true);

        if (levelText != null)
            levelText.raycastTarget = false;
    }

    public void ApplyScheduledVisualState()
    {
        ApplyCurrentSprite();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hovering)
            return;

        hovering = true;
        UIScaleTweenRunner.ScheduleVisual(this);
        AnimateToCurrentState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!hovering && !pressing)
            return;

        hovering = false;
        pressing = false;

        UIScaleTweenRunner.ScheduleVisual(this);
        AnimateToCurrentState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (button != null && !button.interactable)
            return;

        if (pressing)
            return;

        pressing = true;
        AnimateToCurrentState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!pressing)
            return;

        pressing = false;
        AnimateToCurrentState();
    }

    private void OnValidate()
    {
        hoverScale = Mathf.Max(0f, hoverScale);
        clickScale = Mathf.Max(0f, clickScale);
        transitionSpeed = Mathf.Max(0f, transitionSpeed);
        settleThreshold = Mathf.Max(0.000001f, settleThreshold);
    }
}
