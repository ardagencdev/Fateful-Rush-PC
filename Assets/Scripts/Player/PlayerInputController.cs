using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PC-first gameplay movement input for Google Play Games on PC.
/// Keyboard: WASD / Arrow Keys. Gamepad: left stick / D-pad.
/// Legacy joystick methods remain as no-op compatibility hooks so existing
/// scene/prefab references do not become Missing Script or broken UnityEvents.
/// </summary>
public class PlayerInputController : MonoBehaviour
{
    [Header("References")]
    public PlayerMovement playerMovement;

    [Header("Legacy Mobile UI - Auto Hidden on PC")]
    [SerializeField] private RectTransform joystickBG;
    [SerializeField] private RectTransform joystickHandle;

    [Header("PC Input")]
    [Range(0f, 0.5f)]
    [SerializeField] private float gamepadDeadZone = 0.12f;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        HideLegacyJoystickUi();
    }

    private void OnEnable()
    {
        // Scene/HUD intro code can toggle legacy UI roots during startup.
        // Re-assert the PC-only state whenever this controller is enabled.
        HideLegacyJoystickUi();
    }

    private void OnDisable()
    {
        ForceStopInput();
    }

    private void Update()
    {
        if (playerMovement == null)
            return;

        if (Time.timeScale <= 0f ||
            playerMovement.IsGameOver ||
            GameStateManager.IsGameplayEnded)
        {
            ForceStopInput();
            return;
        }

        Vector2 input = ReadKeyboardInput();

        if (input.sqrMagnitude <= 0.001f)
            input = ReadGamepadInput();

        playerMovement.SetMoveInput(Vector2.ClampMagnitude(input, 1f));
    }

    private static Vector2 ReadKeyboardInput()
    {
        if (Keyboard.current == null)
            return Vector2.zero;

        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed ||
            Keyboard.current.upArrowKey.isPressed)
        {
            input.y += 1f;
        }

        if (Keyboard.current.sKey.isPressed ||
            Keyboard.current.downArrowKey.isPressed)
        {
            input.y -= 1f;
        }

        if (Keyboard.current.dKey.isPressed ||
            Keyboard.current.rightArrowKey.isPressed)
        {
            input.x += 1f;
        }

        if (Keyboard.current.aKey.isPressed ||
            Keyboard.current.leftArrowKey.isPressed)
        {
            input.x -= 1f;
        }

        return Vector2.ClampMagnitude(input, 1f);
    }

    private Vector2 ReadGamepadInput()
    {
        Gamepad gamepad = Gamepad.current;

        if (gamepad == null)
            return Vector2.zero;

        Vector2 stick = gamepad.leftStick.ReadValue();

        if (stick.sqrMagnitude < gamepadDeadZone * gamepadDeadZone)
            stick = Vector2.zero;

        Vector2 dpad = gamepad.dpad.ReadValue();
        Vector2 input = dpad.sqrMagnitude > 0.001f ? dpad : stick;

        return Vector2.ClampMagnitude(input, 1f);
    }

    private void HideLegacyJoystickUi()
    {
        if (joystickBG != null)
            joystickBG.gameObject.SetActive(false);

        if (joystickHandle != null &&
            (joystickBG == null ||
             !joystickHandle.IsChildOf(joystickBG)))
        {
            joystickHandle.gameObject.SetActive(false);
        }
    }

    public void ForceStopInput()
    {
        if (playerMovement != null)
            playerMovement.SetMoveInput(Vector2.zero);
    }

    // Legacy mobile compatibility hooks. Existing scene references can stay
    // connected without retaining any touch/joystick runtime work on PC.
    public void PrepareForJoystickLayoutChange() => ForceStopInput();
    public void RefreshJoystickBasePosition() { }
}
