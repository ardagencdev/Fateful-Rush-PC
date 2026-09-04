#if UNITY_ANDROID && !UNITY_EDITOR && PLAY_GAMES_PC_INPUT_SDK
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Java.Lang;
using Java.Util;
using Google.Android.Libraries.Play.Games.Inputmapping;
using Google.Android.Libraries.Play.Games.Inputmapping.Datamodel;
using Google.LibraryWrapper.Java;
using AndroidContext = Google.Android.Libraries.Play.Games.Inputmapping.ExternalType.Android.Content.Context;

/// <summary>
/// Google Play Games on PC Input SDK bootstrap. The file is compiled only
/// after the official Google Input SDK package is detected by the editor
/// watcher. On physical Android devices it remains inactive.
/// </summary>
public sealed class GpgPcInputSdkIntegration : MonoBehaviour
{
    private InputMappingClient inputMappingClient;
    private FatefulRushInputMappingProvider inputMappingProvider;
    private bool registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<GpgPcInputSdkIntegration>() != null)
            return;

        GameObject host = new GameObject("[GPG PC] Input SDK");
        DontDestroyOnLoad(host);
        host.AddComponent<GpgPcInputSdkIntegration>();
    }

    private IEnumerator Start()
    {
        // Android activity/platform feature detection can become available a
        // few frames after Unity scene startup, so give it a short retry span.
        float deadline = Time.realtimeSinceStartup + 5f;

        while (!RuntimePerformancePolicy.IsGooglePlayGamesOnPC &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForSecondsRealtime(0.25f);
        }

        if (!RuntimePerformancePolicy.IsGooglePlayGamesOnPC)
            yield break;

        RegisterInputMap();
    }

    private void RegisterInputMap()
    {
        if (registered)
            return;

        try
        {
            AndroidContext context =
                (AndroidContext)Utils.GetUnityActivity().GetRawObject();

            inputMappingClient =
                Google.Android.Libraries.Play.Games.Inputmapping.Input
                    .GetInputMappingClient(context);

            inputMappingProvider = new FatefulRushInputMappingProvider();
            inputMappingClient.SetInputMappingProvider(inputMappingProvider);

            SceneManager.sceneLoaded += HandleSceneLoaded;
            SetContextForScene(SceneManager.GetActiveScene());

            registered = true;
            Debug.Log("[GPG PC] Input SDK mapping registered.");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                "[GPG PC] Input SDK registration failed: " +
                exception.Message
            );
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetContextForScene(scene);
    }

    private void SetContextForScene(Scene scene)
    {
        if (inputMappingClient == null)
            return;

        // Gameplay scene is named "a" throughout the current project.
        InputContext context =
            scene.name == "a"
                ? FatefulRushInputMappingProvider.GameplayContext
                : FatefulRushInputMappingProvider.MenuContext;

        inputMappingClient.SetInputContext(context);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (inputMappingClient == null)
            return;

        try
        {
            inputMappingClient.ClearInputMappingProvider();
        }
        catch
        {
            // App shutdown can tear the Android activity down first.
        }
    }
}

/// <summary>
/// Input map shown by the Google Play Games on PC overlay. Contexts keep menu
/// navigation and gameplay controls separate while using the same physical
/// arrow/WASD keys where appropriate.
/// </summary>
public sealed class FatefulRushInputMappingProvider :
    InputMappingProviderCallbackHelper
{
    private const string InputMapVersion = "1.0.0";

    private enum ActionId : long
    {
        MoveUp = 1,
        MoveDown = 2,
        MoveLeft = 3,
        MoveRight = 4,
        Dash = 5,
        Clone = 6,
        Pause = 7,
        MenuUp = 20,
        MenuDown = 21,
        MenuLeft = 22,
        MenuRight = 23,
        Confirm = 24,
        Back = 25,
        PreviousPage = 26,
        NextPage = 27
    }

    private enum GroupId : long
    {
        Movement = 100,
        Abilities = 101,
        MenuNavigation = 110,
        MenuActions = 111
    }

    private enum ContextId : long
    {
        Gameplay = 200,
        Menu = 201
    }

    private const long MapId = 1000;

    private static readonly InputGroup MovementGroup = InputGroup.Create(
        "Movement",
        ToJavaList(
            KeyAction("Move Up", ActionId.MoveUp,
                AndroidKeyCode.KEYCODE_W),
            KeyAction("Move Down", ActionId.MoveDown,
                AndroidKeyCode.KEYCODE_S),
            KeyAction("Move Left", ActionId.MoveLeft,
                AndroidKeyCode.KEYCODE_A),
            KeyAction("Move Right", ActionId.MoveRight,
                AndroidKeyCode.KEYCODE_D)
        ),
        InputIdentifier.Create(
            InputMapVersion,
            (long)GroupId.Movement
        ),
        InputEnums.REMAP_OPTION_ENABLED
    );

    private static readonly InputGroup AbilityGroup = InputGroup.Create(
        "Abilities",
        ToJavaList(
            KeyAction("Dash", ActionId.Dash,
                AndroidKeyCode.KEYCODE_SPACE),
            KeyAction("Clone", ActionId.Clone,
                AndroidKeyCode.KEYCODE_E),
            FixedKeyAction("Pause / Back", ActionId.Pause,
                AndroidKeyCode.KEYCODE_ESCAPE)
        ),
        InputIdentifier.Create(
            InputMapVersion,
            (long)GroupId.Abilities
        ),
        InputEnums.REMAP_OPTION_ENABLED
    );

    private static readonly InputGroup MenuNavigationGroup = InputGroup.Create(
        "Menu Navigation",
        ToJavaList(
            KeyAction("Navigate Up", ActionId.MenuUp,
                AndroidKeyCode.KEYCODE_DPAD_UP),
            KeyAction("Navigate Down", ActionId.MenuDown,
                AndroidKeyCode.KEYCODE_DPAD_DOWN),
            KeyAction("Navigate Left", ActionId.MenuLeft,
                AndroidKeyCode.KEYCODE_DPAD_LEFT),
            KeyAction("Navigate Right", ActionId.MenuRight,
                AndroidKeyCode.KEYCODE_DPAD_RIGHT)
        ),
        InputIdentifier.Create(
            InputMapVersion,
            (long)GroupId.MenuNavigation
        ),
        InputEnums.REMAP_OPTION_ENABLED
    );

    private static readonly InputGroup MenuActionsGroup = InputGroup.Create(
        "Menu Actions",
        ToJavaList(
            KeyAction("Confirm", ActionId.Confirm,
                AndroidKeyCode.KEYCODE_ENTER),
            FixedKeyAction("Back", ActionId.Back,
                AndroidKeyCode.KEYCODE_ESCAPE),
            KeyAction("Previous Page", ActionId.PreviousPage,
                AndroidKeyCode.KEYCODE_Q),
            KeyAction("Next Page", ActionId.NextPage,
                AndroidKeyCode.KEYCODE_E)
        ),
        InputIdentifier.Create(
            InputMapVersion,
            (long)GroupId.MenuActions
        ),
        InputEnums.REMAP_OPTION_ENABLED
    );

    public static readonly InputContext GameplayContext = InputContext.Create(
        "Gameplay",
        InputIdentifier.Create(
            InputMapVersion,
            (long)ContextId.Gameplay
        ),
        ToJavaList(MovementGroup, AbilityGroup)
    );

    public static readonly InputContext MenuContext = InputContext.Create(
        "Menus",
        InputIdentifier.Create(
            InputMapVersion,
            (long)ContextId.Menu
        ),
        ToJavaList(MenuNavigationGroup, MenuActionsGroup)
    );

    private static readonly InputMap Map = InputMap.Create(
        ToJavaList(
            MovementGroup,
            AbilityGroup,
            MenuNavigationGroup,
            MenuActionsGroup
        ),
        MouseSettings.Create(false, false),
        InputIdentifier.Create(InputMapVersion, MapId),
        InputEnums.REMAP_OPTION_ENABLED,
        ToJavaList(
            InputControls.Create(
                ToJavaList(new Integer(AndroidKeyCode.KEYCODE_ESCAPE)),
                new ArrayList<Integer>()
            )
        )
    );

    public override InputMap OnProvideInputMap() => Map;

    private static InputAction KeyAction(
        string label,
        ActionId id,
        params int[] keyCodes)
    {
        return InputAction.Create(
            label,
            InputControls.Create(
                ToIntegerList(keyCodes),
                new ArrayList<Integer>()
            ),
            InputIdentifier.Create(InputMapVersion, (long)id),
            InputEnums.REMAP_OPTION_ENABLED
        );
    }

    private static InputAction FixedKeyAction(
        string label,
        ActionId id,
        params int[] keyCodes)
    {
        return InputAction.Create(
            label,
            InputControls.Create(
                ToIntegerList(keyCodes),
                new ArrayList<Integer>()
            ),
            InputIdentifier.Create(InputMapVersion, (long)id),
            InputEnums.REMAP_OPTION_DISABLED
        );
    }

    private static Java.Util.List<Integer> ToIntegerList(int[] values)
    {
        ArrayList<Integer> list = new ArrayList<Integer>();
        for (int i = 0; i < values.Length; i++)
            list.Add(new Integer(values[i]));
        return list;
    }

    private static Java.Util.List<T> ToJavaList<T>(params T[] values)
        where T : Google.LibraryWrapper.Java.JavaObject
    {
        ArrayList<T> list = new ArrayList<T>();
        for (int i = 0; i < values.Length; i++)
            list.Add(values[i]);
        return list;
    }
}
#endif
