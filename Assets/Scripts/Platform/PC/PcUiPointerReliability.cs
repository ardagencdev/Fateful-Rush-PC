using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PC/GPG pointer hardening for Unity UI.
///
/// Google Play Games on PC is configured for raw mouse input through
/// android.hardware.type.pc. This component keeps uGUI's logical hit area
/// stable while press/hover animations scale visuals, unifies pointer devices
/// inside InputSystemUIInputModule, and provides a one-frame fallback only when
/// Unity's normal Button.onClick was genuinely missed.
/// </summary>
public sealed class PcUiPointerReliability : MonoBehaviour
{
    private static readonly WaitForSecondsRealtime ShortRepairDelay =
        new WaitForSecondsRealtime(0.15f);

    private static readonly WaitForSecondsRealtime FinalRepairDelay =
        new WaitForSecondsRealtime(0.85f);

    private readonly List<RaycastResult> raycastResults =
        new List<RaycastResult>(32);

    private Coroutine repairRoutine;
    private Coroutine clickFallbackRoutine;

#if UNITY_ANDROID && !UNITY_EDITOR
    private Button pressedMouseButton;
    private PcButtonClickObserver pressedClickObserver;
    private int pressedClickVersion;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<PcUiPointerReliability>() != null)
            return;

        GameObject host = new GameObject("[PC] UI Pointer Reliability");
        DontDestroyOnLoad(host);
        host.AddComponent<PcUiPointerReliability>();
    }

    private void Awake()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        SchedulePostLoadRepair();
    }

    private void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        ProcessRawMouseClickFallback();
#endif
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearPendingMousePress();
        SchedulePostLoadRepair();
    }

    private void SchedulePostLoadRepair()
    {
        if (!isActiveAndEnabled)
            return;

        if (repairRoutine != null)
            StopCoroutine(repairRoutine);

        repairRoutine = StartCoroutine(PostLoadRepairRoutine());
    }

    private IEnumerator PostLoadRepairRoutine()
    {
        // Pass 1: UI that already exists in the loaded scene.
        yield return null;
        RepairLoadedUi();

        // Pass 2: objects created by Start/first-frame setup.
        yield return ShortRepairDelay;
        RepairLoadedUi();

        // Pass 3: delayed level/menu construction. Dynamic buttons are also
        // repaired on first mouse-down by the fallback path below.
        yield return FinalRepairDelay;
        RepairLoadedUi();

        repairRoutine = null;
    }

    private static void RepairLoadedUi()
    {
        RepairEventSystem();
        RepairCanvases();
        RepairButtons();
    }

    private static void RepairEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        InputSystemUIInputModule inputSystemModule =
            eventSystem.GetComponent<InputSystemUIInputModule>();

        if (inputSystemModule == null)
            return;

        // A dedicated PC build should behave as one cursor even if Android
        // exposes both Mouse and Touchscreen-style pointer devices. With the
        // default mode, activity from a second pointer type can replace the
        // mouse pointer and generate PointerExit between down/up.
        inputSystemModule.pointerBehavior =
            UIPointerBehavior.SingleUnifiedPointer;

        // Only one input module should process the same EventSystem. Keep the
        // project's Input System module authoritative and disable a duplicate
        // legacy module if an old scene/prefab still contains one.
        BaseInputModule[] modules =
            eventSystem.GetComponents<BaseInputModule>();

        for (int i = 0; i < modules.Length; i++)
        {
            BaseInputModule module = modules[i];
            if (module == null || module == inputSystemModule)
                continue;

            if (module.GetType().Name == "StandaloneInputModule" &&
                module.enabled)
            {
                module.enabled = false;
            }
        }
    }

    private static void RepairCanvases()
    {
        Canvas[] canvases = UnityFindCompat.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include
        );

        Camera mainCamera = Camera.main;

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                (canvas.worldCamera == null ||
                 !canvas.worldCamera.isActiveAndEnabled) &&
                mainCamera != null)
            {
                // A stale event camera can make the UI render correctly while
                // mouse coordinates raycast against the wrong screen space.
                canvas.worldCamera = mainCamera;
            }

            if (canvas.GetComponent<GraphicRaycaster>() != null)
                continue;

            if (canvas.GetComponentInChildren<Selectable>(true) != null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private static void RepairButtons()
    {
        Button[] buttons = UnityFindCompat.FindObjectsByType<Button>(
            FindObjectsInactive.Include
        );

        for (int i = 0; i < buttons.Length; i++)
            RepairButton(buttons[i]);
    }

    private static void RepairButton(Button button)
    {
        if (button == null)
            return;

        RectTransform buttonRect = button.transform as RectTransform;
        if (buttonRect == null)
            return;

        // Button input comes from a Graphic, not from the Button component by
        // itself. Ensure the full root RectTransform always has a raycastable
        // graphic instead of depending on a smaller sprite/text child.
        Graphic rootGraphic = null;
        Graphic[] rootGraphics = button.GetComponents<Graphic>();

        for (int i = 0; i < rootGraphics.Length; i++)
        {
            Graphic candidate = rootGraphics[i];
            if (candidate == null || candidate is TMP_Text || candidate is Text)
                continue;

            rootGraphic = candidate;
            break;
        }

        if (rootGraphic == null)
        {
            Image hitSurface = button.gameObject.AddComponent<Image>();
            hitSurface.sprite = null;
            hitSurface.color = new Color(1f, 1f, 1f, 0.001f);
            hitSurface.raycastTarget = true;
            hitSurface.maskable = true;

            rootGraphic = hitSurface;

            if (button.targetGraphic == null)
                button.targetGraphic = hitSurface;
        }
        else
        {
            rootGraphic.raycastTarget = true;

        }

        // Labels do not need to be the raycast endpoint. Events resolve on the
        // stable full-size Button surface instead of glyph/text rectangles.
        TMP_Text[] tmpLabels = button.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmpLabels.Length; i++)
        {
            if (tmpLabels[i] != null)
                tmpLabels[i].raycastTarget = false;
        }

        Text[] legacyLabels = button.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < legacyLabels.Length; i++)
        {
            if (legacyLabels[i] != null)
                legacyLabels[i].raycastTarget = false;
        }

        EnsureStableAnimatedHitArea(button, buttonRect);
        EnsureClickObserver(button);
    }

    private static void EnsureStableAnimatedHitArea(
        Button button,
        RectTransform buttonRect)
    {
        // UIButtonEffect/LevelButtonUI can shrink the Button itself on press.
        // Unity only emits Button.onClick after down/up resolve to the same
        // logical target. Keep an invisible counter-scaled child at the
        // original on-screen size so the hit target never shrinks with visuals.
        bool hasAnimatedPressVisual =
            button.GetComponent<UIButtonEffect>() != null ||
            button.GetComponent<LevelButtonUI>() != null;

        if (!hasAnimatedPressVisual)
            return;

        const string hitAreaName = "[PC] Stable Hit Area";
        Transform existing = button.transform.Find(hitAreaName);

        PcStableButtonHitArea stableHitArea;

        if (existing == null)
        {
            GameObject hitAreaObject = new GameObject(
                hitAreaName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(PcStableButtonHitArea)
            );

            hitAreaObject.layer = button.gameObject.layer;
            hitAreaObject.transform.SetParent(button.transform, false);

            RectTransform hitRect =
                hitAreaObject.GetComponent<RectTransform>();

            hitRect.anchorMin = Vector2.zero;
            hitRect.anchorMax = Vector2.one;
            hitRect.offsetMin = Vector2.zero;
            hitRect.offsetMax = Vector2.zero;
            hitRect.pivot = buttonRect.pivot;

            Image hitImage = hitAreaObject.GetComponent<Image>();
            hitImage.sprite = null;
            hitImage.color = new Color(1f, 1f, 1f, 0.001f);
            hitImage.raycastTarget = true;
            hitImage.maskable = true;

            stableHitArea =
                hitAreaObject.GetComponent<PcStableButtonHitArea>();
        }
        else
        {
            stableHitArea = existing.GetComponent<PcStableButtonHitArea>();
            if (stableHitArea == null)
            {
                stableHitArea =
                    existing.gameObject.AddComponent<PcStableButtonHitArea>();
            }

            Image hitImage = existing.GetComponent<Image>();
            if (hitImage != null)
            {
                hitImage.raycastTarget = true;
                }
        }

        // Put the invisible input surface above decorative children. It has no
        // pointer handler itself, so events bubble cleanly to the Button parent.
        stableHitArea.transform.SetAsLastSibling();
        stableHitArea.Initialize(buttonRect);
    }

    private static PcButtonClickObserver EnsureClickObserver(Button button)
    {
        if (button == null)
            return null;

        PcButtonClickObserver observer =
            button.GetComponent<PcButtonClickObserver>();

        if (observer == null)
            observer = button.gameObject.AddComponent<PcButtonClickObserver>();

        observer.Initialize(button);
        return observer;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void ProcessRawMouseClickFallback()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        Vector2 mousePosition = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Button button = FindButtonAtScreenPoint(mousePosition);

            pressedMouseButton = button;
            pressedClickObserver = null;
            pressedClickVersion = 0;

            if (button != null)
            {
                // Covers buttons instantiated long after the scene-load repair
                // passes (for example a level grid opened later from a panel).
                RepairButton(button);
                pressedClickObserver = EnsureClickObserver(button);

                if (pressedClickObserver != null)
                    pressedClickVersion = pressedClickObserver.ClickVersion;
            }
        }

        if (!mouse.leftButton.wasReleasedThisFrame)
            return;

        Button pressed = pressedMouseButton;
        PcButtonClickObserver observer = pressedClickObserver;
        int versionAtPress = pressedClickVersion;

        ClearPendingMousePress();

        if (!IsUsableButton(pressed))
            return;

        Button releasedOver = FindButtonAtScreenPoint(mousePosition);

        bool sameLogicalButton = releasedOver == pressed;

        // If Unity's raycaster temporarily loses the target because the visual
        // is in the middle of a scale tween, accept the release only when there
        // is no different blocking UI result and the cursor is still inside the
        // button's stable logical rectangle.
        if (!sameLogicalButton && releasedOver == null)
        {
            sameLogicalButton =
                IsScreenPointInsideLogicalButton(pressed, mousePosition);
        }

        if (!sameLogicalButton)
            return;

        if (observer == null)
            observer = EnsureClickObserver(pressed);

        if (observer == null || observer.ClickVersion != versionAtPress)
            return;

        if (clickFallbackRoutine != null)
            StopCoroutine(clickFallbackRoutine);

        clickFallbackRoutine = StartCoroutine(
            DeliverMissedClickNextFrame(
                pressed,
                observer,
                versionAtPress
            )
        );
    }

    private IEnumerator DeliverMissedClickNextFrame(
        Button button,
        PcButtonClickObserver observer,
        int versionAtPress)
    {
        // Let InputSystemUIInputModule finish this frame first. If Unity's
        // normal Button.onClick fires, the observer version changes and this
        // coroutine becomes a no-op. This prevents double activation.
        yield return null;

        clickFallbackRoutine = null;

        if (!IsUsableButton(button) ||
            observer == null ||
            observer.ClickVersion != versionAtPress)
        {
            yield break;
        }

#if DEVELOPMENT_BUILD
        Debug.Log(
            "[GPG PC] Recovered a missed raw-mouse UI click on: " +
            button.name
        );
#endif

        button.onClick.Invoke();
    }

    private Button FindButtonAtScreenPoint(Vector2 screenPoint)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return null;

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = screenPoint
        };

        raycastResults.Clear();
        eventSystem.RaycastAll(pointerData, raycastResults);

        if (raycastResults.Count > 0)
        {
            // Results are front-to-back. Respect a real blocking graphic instead
            // of clicking through modals/overlays into a hidden button.
            for (int i = 0; i < raycastResults.Count; i++)
            {
                GameObject hitObject = raycastResults[i].gameObject;
                if (hitObject == null)
                    continue;

                Button button = hitObject.GetComponentInParent<Button>();
                if (IsUsableButton(button))
                    return button;

                Graphic blocker = hitObject.GetComponent<Graphic>();
                if (blocker != null && blocker.raycastTarget)
                    return null;
            }

            return null;
        }

        // Extremely defensive fallback for a transient GraphicRaycaster miss.
        // It does not run when another raycastable UI object is blocking input.
        return FindButtonByLogicalRect(screenPoint);
    }

    private static Button FindButtonByLogicalRect(Vector2 screenPoint)
    {
        Button[] buttons = UnityFindCompat.FindObjectsByType<Button>(
            FindObjectsInactive.Exclude
        );

        Button best = null;
        int bestCanvasOrder = int.MinValue;
        int bestHierarchyDepth = int.MinValue;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (!IsUsableButton(button) ||
                !IsScreenPointInsideLogicalButton(button, screenPoint))
            {
                continue;
            }

            Canvas canvas = button.GetComponentInParent<Canvas>();
            int canvasOrder = canvas != null ? canvas.sortingOrder : 0;
            int hierarchyDepth = GetHierarchyDepth(button.transform);

            if (best == null ||
                canvasOrder > bestCanvasOrder ||
                (canvasOrder == bestCanvasOrder &&
                 hierarchyDepth > bestHierarchyDepth))
            {
                best = button;
                bestCanvasOrder = canvasOrder;
                bestHierarchyDepth = hierarchyDepth;
            }
        }

        return best;
    }

    private static bool IsScreenPointInsideLogicalButton(
        Button button,
        Vector2 screenPoint)
    {
        if (button == null)
            return false;

        RectTransform rect = null;
        Transform stable = button.transform.Find("[PC] Stable Hit Area");

        if (stable != null)
            rect = stable as RectTransform;

        if (rect == null)
            rect = button.transform as RectTransform;

        if (rect == null)
            return false;

        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera eventCamera = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera != null
                ? canvas.worldCamera
                : Camera.main;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            rect,
            screenPoint,
            eventCamera
        );
    }

    private static int GetHierarchyDepth(Transform transform)
    {
        int depth = 0;
        Transform current = transform;

        while (current != null)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }
#endif

    private static bool IsUsableButton(Button button)
    {
        return button != null &&
               button.gameObject.activeInHierarchy &&
               button.IsActive() &&
               button.IsInteractable();
    }

    private void ClearPendingMousePress()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        pressedMouseButton = null;
        pressedClickObserver = null;
        pressedClickVersion = 0;
#endif
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (repairRoutine != null)
        {
            StopCoroutine(repairRoutine);
            repairRoutine = null;
        }

        if (clickFallbackRoutine != null)
        {
            StopCoroutine(clickFallbackRoutine);
            clickFallbackRoutine = null;
        }
    }
}

/// <summary>
/// Keeps an invisible Button child at the Button's original on-screen size
/// while UIButtonEffect/LevelButtonUI scale the parent for press feedback.
/// Pointer events received by the child bubble to the Button on its parent.
/// </summary>
public sealed class PcStableButtonHitArea : MonoBehaviour
{
    private RectTransform owner;
    private Vector3 baselineOwnerScale = Vector3.one;
    private RectTransform ownRect;
    private bool initialized;

    public void Initialize(RectTransform ownerRect)
    {
        if (ownerRect == null)
            return;

        owner = ownerRect;
        ownRect = transform as RectTransform;

        if (!initialized ||
            Mathf.Abs(baselineOwnerScale.x) < 0.0001f ||
            Mathf.Abs(baselineOwnerScale.y) < 0.0001f)
        {
            baselineOwnerScale = owner.localScale;
        }

        initialized = true;
        RefreshCounterScale();
    }

    private void LateUpdate()
    {
        RefreshCounterScale();
    }

    private void RefreshCounterScale()
    {
        if (!initialized || owner == null || ownRect == null)
            return;

        Vector3 currentScale = owner.localScale;

        float inverseX = Mathf.Abs(currentScale.x) > 0.0001f
            ? baselineOwnerScale.x / currentScale.x
            : 1f;

        float inverseY = Mathf.Abs(currentScale.y) > 0.0001f
            ? baselineOwnerScale.y / currentScale.y
            : 1f;

        ownRect.localScale = new Vector3(inverseX, inverseY, 1f);
    }
}

/// <summary>
/// Tracks successfully delivered Button.onClick calls. The PC pointer fallback
/// uses a monotonically increasing version to know whether Unity already
/// delivered the click before attempting recovery on the next frame.
/// </summary>
public sealed class PcButtonClickObserver : MonoBehaviour
{
    private Button button;

    public int ClickVersion { get; private set; }

    public void Initialize(Button target)
    {
        if (button == target && button != null)
            return;

        if (button != null)
            button.onClick.RemoveListener(HandleClickDelivered);

        button = target;

        if (button != null)
            button.onClick.AddListener(HandleClickDelivered);
    }

    private void HandleClickDelivered()
    {
        unchecked
        {
            ClickVersion++;
        }
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClickDelivered);

        button = null;
    }
}
