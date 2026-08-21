using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// 最後に使われた入力デバイスに応じて、マウスカーソルの表示/非表示を自動で切り替える。
/// コントローラーの入力があれば非表示、キーボード/マウスの入力があれば表示にする。
/// </summary>
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Tooltip("スティック・トリガーがこの値以上動いたら「コントローラー入力あり」とみなす")]
    [SerializeField] private float gamepadDeadzone = 0.3f;

    [Tooltip("マウスがこの量以上動いたら「マウス入力あり」とみなす")]
    [SerializeField] private float mouseMoveThreshold = 0.1f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (IsGamepadInput())
        {
            SetCursorVisible(false);
        }
        else if (IsKeyboardOrMouseInput())
        {
            SetCursorVisible(true);
        }
    }

    /// <summary>
    /// このフレームにコントローラーの入力があったかどうか
    /// </summary>
    private bool IsGamepadInput()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) return false;

        foreach (var control in pad.allControls)
        {
            if (control is ButtonControl button && button.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (pad.leftStick.ReadValue().magnitude >= gamepadDeadzone) return true;
        if (pad.rightStick.ReadValue().magnitude >= gamepadDeadzone) return true;
        if (pad.leftTrigger.ReadValue() >= gamepadDeadzone) return true;
        if (pad.rightTrigger.ReadValue() >= gamepadDeadzone) return true;

        return false;
    }

    /// <summary>
    /// このフレームにキーボードまたはマウスの入力があったかどうか
    /// </summary>
    private bool IsKeyboardOrMouseInput()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Mouse.current.rightButton.wasPressedThisFrame) return true;
            if (Mouse.current.middleButton.wasPressedThisFrame) return true;
            if (Mouse.current.delta.ReadValue().magnitude >= mouseMoveThreshold) return true;
            if (!Mathf.Approximately(Mouse.current.scroll.ReadValue().y, 0f)) return true;
        }

        return false;
    }

    private void SetCursorVisible(bool visible)
    {
        if (Cursor.visible == visible) return;
        Cursor.visible = visible;
    }
}
