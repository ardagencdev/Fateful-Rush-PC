using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PC-first menu navigation. Adds Escape/B back handling and makes sure a
/// controller/keyboard user can recover UI focus without touching the mouse.
/// No scene wiring is required.
/// </summary>
public sealed class PcUiNavigation : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<PcUiNavigation>() != null)
            return;

        GameObject host = new GameObject("[PC] UI Navigation");
        DontDestroyOnLoad(host);
        host.AddComponent<PcUiNavigation>();
    }

    private void Update()
    {
        bool isMainMenu =
            SceneManager.GetActiveScene().name == MainMenuSceneName;

        bool backPressed =
            (Keyboard.current != null &&
             Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null &&
             Gamepad.current.buttonEast.wasPressedThisFrame);

        if (isMainMenu && backPressed)
        {
            HandleBack();
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        EnsureSelectionForKeyboardOrGamepad(eventSystem);

        // InputSystemUIInputModule already performs normal Move/Submit events.
        // Only provide a submit fallback when the scene uses another module.
        if (eventSystem.currentInputModule is InputSystemUIInputModule)
            return;

        bool submitPressed =
            (Keyboard.current != null &&
             (Keyboard.current.enterKey.wasPressedThisFrame ||
              Keyboard.current.numpadEnterKey.wasPressedThisFrame)) ||
            (Gamepad.current != null &&
             Gamepad.current.buttonSouth.wasPressedThisFrame);

        if (submitPressed && eventSystem.currentSelectedGameObject != null)
        {
            ExecuteEvents.Execute(
                eventSystem.currentSelectedGameObject,
                new BaseEventData(eventSystem),
                ExecuteEvents.submitHandler
            );
        }
    }

    private static void EnsureSelectionForKeyboardOrGamepad(
        EventSystem eventSystem)
    {
        GameObject currentSelection =
            eventSystem.currentSelectedGameObject;

        if (currentSelection != null)
        {
            Selectable currentSelectable =
                currentSelection.GetComponent<Selectable>();

            bool selectionIsUsable =
                currentSelection.activeInHierarchy &&
                (currentSelectable == null ||
                 (currentSelectable.IsActive() &&
                  currentSelectable.IsInteractable()));

            if (selectionIsUsable)
                return;

            eventSystem.SetSelectedGameObject(null);
        }

        bool navigationPressed = false;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            navigationPressed =
                keyboard.tabKey.wasPressedThisFrame ||
                keyboard.upArrowKey.wasPressedThisFrame ||
                keyboard.downArrowKey.wasPressedThisFrame ||
                keyboard.leftArrowKey.wasPressedThisFrame ||
                keyboard.rightArrowKey.wasPressedThisFrame;
        }

        Gamepad gamepad = Gamepad.current;
        if (!navigationPressed && gamepad != null)
        {
            navigationPressed =
                gamepad.dpad.up.wasPressedThisFrame ||
                gamepad.dpad.down.wasPressedThisFrame ||
                gamepad.dpad.left.wasPressedThisFrame ||
                gamepad.dpad.right.wasPressedThisFrame;
        }

        if (!navigationPressed)
            return;

        Selectable[] selectables = FindObjectsByType<Selectable>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < selectables.Length; i++)
        {
            Selectable selectable = selectables[i];
            if (selectable == null ||
                !selectable.IsActive() ||
                !selectable.IsInteractable())
            {
                continue;
            }

            eventSystem.SetSelectedGameObject(selectable.gameObject);
            return;
        }
    }

    private static void HandleBack()
    {
        MissionBriefingPanelUI briefing =
            FindAnyObjectByType<MissionBriefingPanelUI>();
        if (briefing != null && briefing.IsOpen)
        {
            briefing.Close();
            return;
        }

        PlayerSkinPanelUI skins =
            FindAnyObjectByType<PlayerSkinPanelUI>();
        if (skins != null && skins.IsOpen)
        {
            skins.ClosePanel();
            return;
        }

        OptionsUI options = FindAnyObjectByType<OptionsUI>();
        if (options != null && options.HandleEscapeBack())
            return;

        StatsPanelUI stats = FindAnyObjectByType<StatsPanelUI>();
        if (stats != null && stats.HandleEscapeBack())
            return;

        ExtrasPanelUI extras = FindAnyObjectByType<ExtrasPanelUI>();
        if (extras != null && extras.HandleBack())
            return;

        LevelSelectPanel levelSelect =
            FindAnyObjectByType<LevelSelectPanel>();
        if (levelSelect != null && levelSelect.IsOpen)
            levelSelect.ClosePanel();
    }
}
