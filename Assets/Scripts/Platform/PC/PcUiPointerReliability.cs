using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PC/GPG pointer hardening for Unity UI.
///
/// The Google Play Games PC client delivers a real mouse when FEATURE_PC is
/// declared in the final Android manifest. This component complements that by
/// making sure every Button has a full-RectTransform raycast surface, labels do
/// not steal hits, and duplicate legacy/new input modules cannot process the
/// same click twice. No Inspector setup is required.
/// </summary>
public sealed class PcUiPointerReliability : MonoBehaviour
{
    // A few short post-load passes catch UI that is instantiated during scene
    // startup without doing a full Button/Canvas search forever during play.
    private static readonly WaitForSecondsRealtime ShortRepairDelay =
        new WaitForSecondsRealtime(0.15f);

    private static readonly WaitForSecondsRealtime FinalRepairDelay =
        new WaitForSecondsRealtime(0.85f);

    private Coroutine repairRoutine;

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

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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

        // Pass 3: delayed level/menu construction. After this, stop scanning.
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

        // If both modules are present, both can consume pointer state. The
        // project already uses Unity's Input System, so keep the Input System
        // UI module authoritative and disable only the duplicate legacy one.
        if (inputSystemModule != null)
        {
            BaseInputModule[] modules =
                eventSystem.GetComponents<BaseInputModule>();

            for (int i = 0; i < modules.Length; i++)
            {
                BaseInputModule module = modules[i];
                if (module == null || module == inputSystemModule)
                    continue;

                // Avoid a hard dependency on the legacy module type; if a
                // StandaloneInputModule is present beside InputSystemUIInputModule,
                // disable the duplicate pointer processor.
                if (module.GetType().Name == "StandaloneInputModule" &&
                    module.enabled)
                {
                    module.enabled = false;
                }
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
                // A missing/stale event camera can make Screen Space - Camera
                // UI appear correct while pointer coordinates raycast wrong.
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

        // A Button itself is not a raycast target; a Graphic is. If a prefab
        // keeps the visible Image on a smaller child, only that child can be
        // clickable. Give the Button's full RectTransform an invisible input
        // surface so the visual and logical button area match.
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

            if (button.targetGraphic == null)
                button.targetGraphic = hitSurface;
        }
        else if (!rootGraphic.raycastTarget)
        {
            rootGraphic.raycastTarget = true;
        }

        // Labels never need to be the raycast endpoint. Events will resolve on
        // the stable full-size Button surface instead of a glyph/text rect.
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
    }

    private static void EnsureStableAnimatedHitArea(
        Button button,
        RectTransform buttonRect)
    {
        // Android shrinks buttons on PointerDown. Keep that exact visual
        // feedback on PC, but add a counter-scaled invisible child so the
        // mouse hit area does not shrink with the visual at the edges.
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

            hitAreaObject.transform.SetParent(button.transform, false);
            hitAreaObject.transform.SetAsFirstSibling();

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
                stableHitArea = existing.gameObject.AddComponent<PcStableButtonHitArea>();
        }

        stableHitArea.Initialize(buttonRect);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (repairRoutine != null)
        {
            StopCoroutine(repairRoutine);
            repairRoutine = null;
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
