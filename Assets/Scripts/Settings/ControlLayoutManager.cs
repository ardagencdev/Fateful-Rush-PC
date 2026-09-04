using UnityEngine;

/// <summary>
/// PC version of the mobile control-layout system.
///
/// The touch joystick stays disabled, but the Android left/right relationship
/// is preserved for the remaining HUD. Dash/Clone use the positions authored
/// in the PC scene as the source layout and are mirrored horizontally when the
/// HUD side changes, so mobile-only hard-coded offsets cannot overwrite the
/// PC HUD design.
/// </summary>
public sealed class ControlLayoutManager : MonoBehaviour
{
    public enum JoystickSide
    {
        Left = 0,
        Right = 1
    }

    public enum HudPosition
    {
        Left = 0,
        Right = 1
    }

    private const string JoystickSideKey = "JoystickSide";
    private const string TemporaryPcHudPositionKey = "HUDPosition";

    public static ControlLayoutManager Instance { get; private set; }

    [Header("Default")]
    [SerializeField]
    private JoystickSide defaultSide = JoystickSide.Right;

    [Header("HUD References")]
    [SerializeField]
    private RectTransform dashButton;

    [SerializeField]
    private RectTransform cloneButton;

    [SerializeField]
    private RectTransform pauseButton;

    // Kept serialized for scene/prefab compatibility with the old script.
    // They are intentionally no longer used on PC; the scene-authored
    // RectTransform layout is now the source of truth.
    [Header("Legacy Mobile Positions (unused on PC)")]
    [SerializeField]
    private Vector2 dashLeftPos = new Vector2(145f, 135f);

    [SerializeField]
    private Vector2 dashRightPos = new Vector2(-145f, 135f);

    [SerializeField]
    private Vector2 cloneLeftPos = new Vector2(145f, 255f);

    [SerializeField]
    private Vector2 cloneRightPos = new Vector2(-145f, 255f);

    private bool actionLayoutCaptured;
    private bool authoredButtonsOnLeft;
    private RectLayout dashAuthoredLayout;
    private RectLayout cloneAuthoredLayout;

    private bool pauseLayoutCaptured;
    private Vector2 pauseBaseAnchorMin;
    private Vector2 pauseBaseAnchorMax;
    private Vector2 pauseBasePivot;
    private Vector2 pauseBaseAnchoredPosition;

    public JoystickSide CurrentSide { get; private set; }

    public HudPosition CurrentPosition =>
        CurrentSide == JoystickSide.Right
            ? HudPosition.Left
            : HudPosition.Right;

    private struct RectLayout
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        ResolveHudReferences();

        // Capture BEFORE applying any saved side. This preserves the exact
        // positions the user authored in the PC scene/prefab.
        CaptureActionLayout();
        CapturePauseLayout();

        ApplySavedLayout();
    }

    private void OnEnable()
    {
        ResolveHudReferences();
        CaptureActionLayout();
        CapturePauseLayout();
        ApplySavedLayout();
    }

    /// <summary>
    /// PC-facing setting: put the visible action HUD on the left.
    /// This equals Android's Joystick Right layout.
    /// </summary>
    public void SetHUDPositionLeft()
    {
        SaveAndApply(JoystickSide.Right);
    }

    /// <summary>
    /// PC-facing setting: put the visible action HUD on the right.
    /// This equals Android's Joystick Left layout.
    /// </summary>
    public void SetHUDPositionRight()
    {
        SaveAndApply(JoystickSide.Left);
    }

    // Keep old UnityEvent/script bindings working exactly like Android.
    public void SetJoystickLeft()
    {
        SaveAndApply(JoystickSide.Left);
    }

    public void SetJoystickRight()
    {
        SaveAndApply(JoystickSide.Right);
    }

    public void ApplySavedLayout()
    {
        ApplyLayout(GetSavedSide());
    }

    public JoystickSide GetSavedSide()
    {
        if (!PlayerPrefs.HasKey(JoystickSideKey) &&
            PlayerPrefs.HasKey(TemporaryPcHudPositionKey))
        {
            // Migrate the short-lived PC HUDPosition save back to the Android
            // JoystickSide source-of-truth without changing the visible side.
            int oldHudPosition = PlayerPrefs.GetInt(
                TemporaryPcHudPositionKey,
                (int)HudPosition.Left
            );

            JoystickSide migratedSide =
                oldHudPosition == (int)HudPosition.Right
                    ? JoystickSide.Left
                    : JoystickSide.Right;

            PlayerPrefs.SetInt(
                JoystickSideKey,
                (int)migratedSide
            );
            PlayerPrefs.Save();

            return migratedSide;
        }

        int savedValue = PlayerPrefs.GetInt(
            JoystickSideKey,
            (int)defaultSide
        );

        return savedValue == (int)JoystickSide.Left
            ? JoystickSide.Left
            : JoystickSide.Right;
    }

    public HudPosition GetSavedPosition()
    {
        return GetSavedSide() == JoystickSide.Right
            ? HudPosition.Left
            : HudPosition.Right;
    }

    public void ApplyLayout(HudPosition position)
    {
        ApplyLayout(
            position == HudPosition.Left
                ? JoystickSide.Right
                : JoystickSide.Left
        );
    }

    public void ApplyLayout(JoystickSide side)
    {
        CurrentSide = side;

        ResolveHudReferences();
        CaptureActionLayout();
        CapturePauseLayout();

        // Exact Android relationship: action controls are opposite the old
        // joystick side. On PC there is no joystick, only the HUD side remains.
        bool buttonsOnLeft = side == JoystickSide.Right;

        ApplyAuthoredButtonLayout(
            dashButton,
            dashAuthoredLayout,
            buttonsOnLeft
        );

        ApplyAuthoredButtonLayout(
            cloneButton,
            cloneAuthoredLayout,
            buttonsOnLeft
        );

        ApplyPauseButton(buttonsOnLeft);

        Canvas.ForceUpdateCanvases();
    }

    private void SaveAndApply(JoystickSide side)
    {
        PlayerPrefs.SetInt(
            JoystickSideKey,
            (int)side
        );

        PlayerPrefs.Save();
        ApplyLayout(side);
    }

    private void ResolveHudReferences()
    {
        if (dashButton != null &&
            cloneButton != null &&
            pauseButton != null)
        {
            return;
        }

        GameStateManager gameStateManager =
            FindAnyObjectByType<GameStateManager>();

        if (gameStateManager == null)
            return;

        if (dashButton == null && gameStateManager.dashHUD != null)
        {
            dashButton =
                gameStateManager.dashHUD.GetComponent<RectTransform>();
        }

        if (cloneButton == null && gameStateManager.cloneHUD != null)
        {
            cloneButton =
                gameStateManager.cloneHUD.GetComponent<RectTransform>();
        }

        if (pauseButton == null && gameStateManager.pauseButtonHUD != null)
        {
            pauseButton =
                gameStateManager.pauseButtonHUD.GetComponent<RectTransform>();
        }
    }

    private void CaptureActionLayout()
    {
        if (actionLayoutCaptured)
            return;

        if (dashButton == null && cloneButton == null)
            return;

        RectTransform sideReference =
            dashButton != null
                ? dashButton
                : cloneButton;

        authoredButtonsOnLeft = IsRectOnLeft(sideReference);

        if (dashButton != null)
            dashAuthoredLayout = CaptureRectLayout(dashButton);

        if (cloneButton != null)
            cloneAuthoredLayout = CaptureRectLayout(cloneButton);

        actionLayoutCaptured = true;
    }

    private void ApplyAuthoredButtonLayout(
        RectTransform button,
        RectLayout authoredLayout,
        bool targetLeft)
    {
        if (button == null || !actionLayoutCaptured)
            return;

        RectLayout targetLayout =
            targetLeft == authoredButtonsOnLeft
                ? authoredLayout
                : MirrorHorizontally(authoredLayout);

        ApplyRectLayout(button, targetLayout);
    }

    private static RectLayout CaptureRectLayout(RectTransform rect)
    {
        return new RectLayout
        {
            anchorMin = rect.anchorMin,
            anchorMax = rect.anchorMax,
            pivot = rect.pivot,
            anchoredPosition = rect.anchoredPosition
        };
    }

    private static void ApplyRectLayout(
        RectTransform rect,
        RectLayout layout)
    {
        rect.anchorMin = layout.anchorMin;
        rect.anchorMax = layout.anchorMax;
        rect.pivot = layout.pivot;
        rect.anchoredPosition = layout.anchoredPosition;
    }

    private static RectLayout MirrorHorizontally(RectLayout source)
    {
        RectLayout mirrored = source;

        mirrored.anchorMin = new Vector2(
            1f - source.anchorMax.x,
            source.anchorMin.y
        );

        mirrored.anchorMax = new Vector2(
            1f - source.anchorMin.x,
            source.anchorMax.y
        );

        mirrored.pivot = new Vector2(
            1f - source.pivot.x,
            source.pivot.y
        );

        mirrored.anchoredPosition = new Vector2(
            -source.anchoredPosition.x,
            source.anchoredPosition.y
        );

        return mirrored;
    }

    private static bool IsRectOnLeft(RectTransform rect)
    {
        if (rect == null)
            return true;

        float anchorCenterX =
            (rect.anchorMin.x + rect.anchorMax.x) * 0.5f;

        if (Mathf.Abs(anchorCenterX - 0.5f) > 0.01f)
            return anchorCenterX < 0.5f;

        // Center-anchored fallback.
        return rect.anchoredPosition.x <= 0f;
    }

    private void CapturePauseLayout()
    {
        if (pauseLayoutCaptured || pauseButton == null)
            return;

        pauseBaseAnchorMin = pauseButton.anchorMin;
        pauseBaseAnchorMax = pauseButton.anchorMax;
        pauseBasePivot = pauseButton.pivot;
        pauseBaseAnchoredPosition = pauseButton.anchoredPosition;
        pauseLayoutCaptured = true;
    }

    private void ApplyPauseButton(bool left)
    {
        if (pauseButton == null)
            return;

        CapturePauseLayout();

        float xAnchor = left ? 0f : 1f;
        float xOffset = Mathf.Abs(pauseBaseAnchoredPosition.x);

        pauseButton.anchorMin = new Vector2(
            xAnchor,
            pauseBaseAnchorMin.y
        );

        pauseButton.anchorMax = new Vector2(
            xAnchor,
            pauseBaseAnchorMax.y
        );

        pauseButton.pivot = new Vector2(
            xAnchor,
            pauseBasePivot.y
        );

        pauseButton.anchoredPosition = new Vector2(
            left ? xOffset : -xOffset,
            pauseBaseAnchoredPosition.y
        );
    }

    [ContextMenu("Reset HUD Position Save")]
    private void ResetHudPositionSave()
    {
        PlayerPrefs.DeleteKey(JoystickSideKey);
        PlayerPrefs.Save();
        ApplySavedLayout();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
