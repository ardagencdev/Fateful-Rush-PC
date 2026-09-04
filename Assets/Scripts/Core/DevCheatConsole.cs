#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor-only cheat console used for balancing / QA.
/// No scene setup is required: the console bootstraps itself before the first scene.
///
/// It is excluded from every player build at compile time, including Android,
/// Google Play Games on PC and native Windows builds.
/// Editor toggle: ` / ~
/// </summary>
public sealed class DevCheatConsole : MonoBehaviour
{
    public static DevCheatConsole Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.isOpen;

    private const int MissionCount = 40;

    private const int MaxHistoryLines = 11;
    private const string UnlockedLevelKey = "UnlockedLevel";

    private readonly List<string> history = new List<string>();

    private Canvas canvas;
    private GameObject blockerObject;
    private GameObject panelObject;
    private TMP_Text historyText;
    private TMP_InputField inputField;

    private GameObject temporaryEventSystem;

    private bool isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!ShouldExistInCurrentBuild())
            return;

        if (Instance != null)
            return;

        GameObject host = new GameObject("[DEV] Cheat Console");
        DontDestroyOnLoad(host);
        host.AddComponent<DevCheatConsole>();
    }

    public static bool CheatsAvailable => true;

    private static bool ShouldExistInCurrentBuild()
    {
        return true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildUI();
        SetConsoleVisible(false);

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!ShouldExistInCurrentBuild())
            return;

        HandleDesktopToggle();

        if (isOpen)
            HandleOpenConsoleKeys();
    }

    private void HandleDesktopToggle()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || !keyboard.backquoteKey.wasPressedThisFrame)
            return;

        if (isOpen)
            CloseConsole();
        else
            OpenConsole();
    }

    private void HandleOpenConsoleKeys()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
            CloseConsole();
    }

    public void OpenConsole()
    {
        if (isOpen)
            return;

        isOpen = true;

        EnsureEventSystem();
        SetConsoleVisible(true);

        AddHistory("<color=#8FE8FF>FATEFUL RUSH DEV CONSOLE</color>");
        AddHistory("Type <color=#FFFFFF>help</color> to list commands.");

        FocusInputField();
    }

    public void CloseConsole()
    {
        if (!isOpen)
            return;

        isOpen = false;

        if (inputField != null)
            inputField.DeactivateInputField();

        if (EventSystem.current != null &&
            inputField != null &&
            EventSystem.current.currentSelectedGameObject == inputField.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        SetConsoleVisible(false);
        DestroyTemporaryEventSystem();
    }

    private void SubmitCurrentInput()
    {
        if (inputField == null)
            return;

        string command = inputField.text;
        inputField.text = string.Empty;

        ExecuteCommand(command);
        FocusInputField();
    }

    private void ExecuteCommand(string rawCommand)
    {
        string trimmed = rawCommand == null
            ? string.Empty
            : rawCommand.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        AddHistory($"> <color=#FFFFFF>{EscapeRichText(trimmed)}</color>");

        string[] pieces = trimmed.Split(
            new[] { ' ', '\t' },
            StringSplitOptions.RemoveEmptyEntries
        );

        if (pieces.Length == 0)
            return;

        string command = pieces[0].ToLowerInvariant();

        switch (command)
        {
            case "help":
            case "yardim":
            case "?":
                PrintHelp();
                break;

            case "levels":
            case "levellar":
            case "unlocklevels":
                UnlockAllLevels();
                break;

            case "skins":
            case "skinler":
            case "unlockskins":
                UnlockAllSkins();
                break;

            case "unlockall":
            case "hepsi":
                UnlockAllLevels();
                UnlockAllSkins();
                AddHistory("<color=#80FF9B>All level + skin access enabled.</color>");
                break;

            case "reset":
            case "sifirla":
                ResetProgression();
                break;

            case "level":
            case "unlocklevel":
                HandleLevelCommand(pieces);
                break;

            case "skinsoff":
            case "lockskins":
                DisableSkinCheat();
                break;

            case "status":
                PrintStatus();
                break;

            case "clear":
                history.Clear();
                RefreshHistoryText();
                break;

            case "close":
            case "exit":
                CloseConsole();
                break;

            default:
                AddHistory(
                    $"<color=#FF8C8C>Unknown command:</color> {EscapeRichText(command)}"
                );
                AddHistory("Type <color=#FFFFFF>help</color>.");
                break;
        }
    }

    private void PrintHelp()
    {
        AddHistory("<color=#F7D774>levels</color> - unlock Levels 1-40");
        AddHistory("<color=#F7D774>skins</color> - unlock all skins");
        AddHistory("<color=#F7D774>unlockall</color> - unlock levels + skins");
        AddHistory("<color=#F7D774>level 25</color> - unlock through a specific level");
        AddHistory("<color=#F7D774>skinsoff</color> - return skins to normal progression");
        AddHistory("<color=#F7D774>reset</color> - reset mission progression + skin cheat");
        AddHistory("<color=#F7D774>status</color> - show current cheat/progression status");
        AddHistory("<color=#F7D774>clear</color> - clear console history");
        AddHistory("<color=#F7D774>close</color> - close the dev console");
    }

    private void UnlockAllLevels()
    {
        PlayerPrefs.SetInt(UnlockedLevelKey, MissionCount);
        PlayerPrefs.Save();

        RefreshProgressionUI();

        AddHistory(
            $"<color=#80FF9B>Levels 1-{MissionCount} unlocked.</color> " +
            "Completion flags were not changed."
        );
    }

    private void HandleLevelCommand(string[] pieces)
    {
        if (pieces.Length < 2 || !int.TryParse(pieces[1], out int requestedLevel))
        {
            AddHistory("Usage: <color=#FFFFFF>level 25</color>");
            return;
        }

        int safeLevel = Mathf.Clamp(requestedLevel, 1, MissionCount);
        PlayerPrefs.SetInt(UnlockedLevelKey, safeLevel);
        PlayerPrefs.Save();

        RefreshProgressionUI();

        AddHistory(
            $"<color=#80FF9B>Levels 1-{safeLevel} unlocked.</color>"
        );
    }

    private void UnlockAllSkins()
    {
        PlayerSkinCatalog.SetDebugAllSkinsUnlocked(true);
        RefreshSkinUI();

        AddHistory("<color=#80FF9B>All player skins unlocked.</color>");
    }

    private void DisableSkinCheat()
    {
        PlayerSkinCatalog.SetDebugAllSkinsUnlocked(false);
        RefreshSkinUI();

        AddHistory(
            "<color=#FFD37A>Skin unlocks returned to normal progression.</color>"
        );
    }

    private void ResetProgression()
    {
        for (int i = 1; i <= MissionCount; i++)
        {
            PlayerPrefs.DeleteKey($"CompletedLevel_{i}");
            PlayerPrefs.DeleteKey($"BestTime_Level_{i}");
        }

        PlayerPrefs.SetInt(UnlockedLevelKey, 1);

        PlayerSkinCatalog.SetDebugAllSkinsUnlocked(false);
        PlayerSkinCatalog.ClearSavedSelection();

        PlayerPrefs.Save();

        RefreshProgressionUI();
        RefreshSkinUI();

        AddHistory(
            "<color=#FFD37A>Mission progression reset.</color> " +
            "Settings and global statistics were kept."
        );
    }

    private void PrintStatus()
    {
        int unlockedLevel = Mathf.Clamp(
            PlayerPrefs.GetInt(UnlockedLevelKey, 1),
            1,
            MissionCount
        );

        int completed = 0;

        for (int i = 1; i <= MissionCount; i++)
        {
            if (PlayerPrefs.GetInt($"CompletedLevel_{i}", 0) == 1)
                completed++;
        }

        string skins = PlayerSkinCatalog.AreAllSkinsDebugUnlocked
            ? "ALL (cheat)"
            : "normal progression";

        AddHistory(
            $"Unlocked level: <color=#FFFFFF>{unlockedLevel}</color> | " +
            $"Completed: <color=#FFFFFF>{completed}/{MissionCount}</color>"
        );
        AddHistory($"Skins: <color=#FFFFFF>{skins}</color>");
    }

    private void RefreshProgressionUI()
    {
        // The console is only usable while MainMenuPanel is active, so the
        // active MainMenu singleton is the only UI that needs an immediate
        // refresh. LevelSelectPanel rebuilds / refreshes its buttons when the
        // Missions panel is opened, so we deliberately avoid scanning inactive
        // scene objects here. This also keeps the code compatible with Unity
        // 6.5 where the old FindObjectsSortMode overloads are deprecated.
        MainMenu.Instance?.RefreshContinueState();
    }

    private static void RefreshSkinUI()
    {
        // PlayerSkinCatalog.SetDebugAllSkinsUnlocked() already raises
        // SelectedSkinChanged, which refreshes the skin-themed UI. The actual
        // PlayerSkinPanelUI refreshes its displayed skin when OpenPanel() runs.
        // Do not call a panel-specific refresh method here: older/current
        // project revisions do not all expose the same public method.
        // Keeping this intentionally empty makes the console independent from
        // PlayerSkinPanelUI implementation details.
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isOpen)
            return;

        // The console is scene-independent, but it should not remain open
        // after changing scenes.
        CloseConsole();
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("DevConsoleCanvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        blockerObject = CreateUIObject("InputBlocker", canvasObject.transform);
        RectTransform blockerRect = blockerObject.GetComponent<RectTransform>();
        StretchFull(blockerRect);

        Image blockerImage = blockerObject.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.001f);
        // Keep the game running, but swallow UI pointer events behind the console.
        // This blocks menu Buttons without touching Time.timeScale or gameplay updates.
        blockerImage.raycastTarget = true;

        panelObject = CreateUIObject("ConsolePanel", blockerObject.transform);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(0f, 380f);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.015f, 0.02f, 0.028f, 0.96f);
        panelImage.raycastTarget = true;

        TMP_Text title = CreateText(
            "Title",
            panelObject.transform,
            "FATEFUL RUSH // DEV CONSOLE",
            24f,
            TextAlignmentOptions.Left
        );

        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -46f);
        titleRect.offsetMax = new Vector2(-24f, -10f);
        title.color = new Color32(144, 232, 255, 255);

        historyText = CreateText(
            "History",
            panelObject.transform,
            string.Empty,
            18f,
            TextAlignmentOptions.TopLeft
        );

        RectTransform historyRect = historyText.rectTransform;
        historyRect.anchorMin = new Vector2(0f, 0f);
        historyRect.anchorMax = new Vector2(1f, 1f);
        historyRect.offsetMin = new Vector2(24f, 68f);
        historyRect.offsetMax = new Vector2(-24f, -54f);
        historyText.textWrappingMode = TextWrappingModes.NoWrap;
        historyText.overflowMode = TextOverflowModes.Truncate;
        historyText.richText = true;
        historyText.color = new Color32(214, 222, 232, 255);

        CreateInputField(panelObject.transform);
    }

    private void CreateInputField(Transform parent)
    {
        GameObject inputObject = CreateUIObject("CommandInput", parent);
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0f);
        inputRect.pivot = new Vector2(0.5f, 0f);
        inputRect.offsetMin = new Vector2(24f, 14f);
        inputRect.offsetMax = new Vector2(-24f, 58f);

        Image background = inputObject.AddComponent<Image>();
        background.color = new Color(0.07f, 0.085f, 0.105f, 1f);

        inputField = inputObject.AddComponent<TMP_InputField>();
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.contentType = TMP_InputField.ContentType.Standard;
        inputField.richText = false;
        inputField.characterLimit = 64;
        inputField.restoreOriginalTextOnEscape = false;

        GameObject viewportObject = CreateUIObject("Text Area", inputObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(14f, 4f);
        viewportRect.offsetMax = new Vector2(-14f, -4f);
        viewportObject.AddComponent<RectMask2D>();

        TMP_Text text = CreateText(
            "Text",
            viewportObject.transform,
            string.Empty,
            21f,
            TextAlignmentOptions.MidlineLeft
        );
        StretchFull(text.rectTransform);
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text placeholder = CreateText(
            "Placeholder",
            viewportObject.transform,
            "ENTER COMMAND...",
            21f,
            TextAlignmentOptions.MidlineLeft
        );
        StretchFull(placeholder.rectTransform);
        placeholder.color = new Color(1f, 1f, 1f, 0.28f);
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;

        inputField.textViewport = viewportRect;
        inputField.textComponent = text;
        inputField.placeholder = placeholder;

        inputField.onSubmit.AddListener(_ => SubmitCurrentInput());
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(objectName, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();

        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        return text;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void SetConsoleVisible(bool visible)
    {
        if (canvas != null)
            canvas.enabled = visible;

        if (blockerObject != null)
            blockerObject.SetActive(visible);
    }

    private void FocusInputField()
    {
        if (inputField == null || !isOpen)
            return;

        EnsureEventSystem();

        inputField.ActivateInputField();
        inputField.Select();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(inputField.gameObject);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        temporaryEventSystem = new GameObject("[DEV] Temporary EventSystem");
        temporaryEventSystem.AddComponent<EventSystem>();
        temporaryEventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private void DestroyTemporaryEventSystem()
    {
        if (temporaryEventSystem == null)
            return;

        Destroy(temporaryEventSystem);
        temporaryEventSystem = null;
    }

    private void AddHistory(string line)
    {
        history.Add(line);

        while (history.Count > MaxHistoryLines)
            history.RemoveAt(0);

        RefreshHistoryText();
    }

    private void RefreshHistoryText()
    {
        if (historyText == null)
            return;

        historyText.text = string.Join("\n", history);
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
#endif
