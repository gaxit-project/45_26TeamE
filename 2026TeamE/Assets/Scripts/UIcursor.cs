using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UICursor : MonoBehaviour
{
    [SerializeField] float scrollSpeed = 10f;
    [SerializeField] public GameObject cursor;

    [Header("サイズ自動調整")]
    [SerializeField] private bool autoResize = true;
    [SerializeField] private Vector2 sizePadding = new Vector2(20f, 20f); // ボタンよりも少し大きめに囲うための余白

    [Header("SE")]
    [SerializeField] private string moveSE = "Select";
    private GameObject lastSelected;

    private RectTransform cursorRect;

    [Header("キャンセルボタンでフォーカスするもの")]
    [SerializeField] public GameObject cancel;


    private bool cancelPending = false;
    private int cancelDelayFrames = 0;

    void Start()
    {
        lastSelected = EventSystem.current.currentSelectedGameObject;
        if (cursor != null)
        {
            cursorRect = cursor.GetComponent<RectTransform>();
        }
    }

    void Update()
    {
        // ====== キャンセル入力の直接検出 ======
        if (!cancelPending && cancel != null)
        {
            bool cancelPressed = false;

            // キーボード: Escape
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                cancelPressed = true;

            // ゲームパッド: Bボタン（East）
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
                cancelPressed = true;

            if (cancelPressed)
            {
                cancelPending = true;
                cancelDelayFrames = 2;
            }
        }

        // ====== カーソル追従処理 ======
        GameObject selected = EventSystem.current.currentSelectedGameObject;

        if (selected == null || cursor == null)
        {
            return;
        }

        if (selected != lastSelected)
        {
            SoundManager.Instance?.PlaySE("つるはしで掘る2");
            lastSelected = selected;
        }

        Transform target = selected.GetComponent<Transform>();

        // X軸・Y軸の両方を追従させ、ポーズ中も動くように unscaledDeltaTime を使用する
        Vector3 newPos = cursor.transform.position;
        newPos.x = Mathf.Lerp(newPos.x, target.position.x, Time.unscaledDeltaTime * scrollSpeed);
        newPos.y = Mathf.Lerp(newPos.y, target.position.y, Time.unscaledDeltaTime * scrollSpeed);
        cursor.transform.position = newPos;

        // ボタンのサイズ（Scale・Width・Height）を読み取って自動調整
        if (autoResize && cursorRect != null)
        {
            RectTransform targetRect = selected.GetComponent<RectTransform>();
            if (targetRect != null)
            {
                // 親のスケールも考慮して、見た目の絶対的な大きさを計算する
                Vector2 targetSize = targetRect.rect.size;
                
                if (cursorRect.parent != null)
                {
                    // カーソルの親から見た相対的なスケール倍率を算出
                    float relativeScaleX = targetRect.lossyScale.x / cursorRect.parent.lossyScale.x;
                    float relativeScaleY = targetRect.lossyScale.y / cursorRect.parent.lossyScale.y;
                    targetSize = new Vector2(targetRect.rect.width * relativeScaleX, targetRect.rect.height * relativeScaleY);
                }
                else
                {
                    // 親がない場合はローカルスケールをそのまま掛ける
                    targetSize = new Vector2(targetRect.rect.width * targetRect.localScale.x, targetRect.rect.height * targetRect.localScale.y);
                }

                // 目標のサイズ（実際の見た目のサイズ + 余白）
                targetSize += sizePadding;
                
                // サイズを滑らかに変更（Lerp）
                cursorRect.sizeDelta = Vector2.Lerp(cursorRect.sizeDelta, targetSize, Time.unscaledDeltaTime * scrollSpeed);
            }
        }
    }

    void LateUpdate()
    {
        // キャンセル入力のフォーカス移動を、全てのUI処理が終わった後に実行する
        if (cancelPending)
        {
            cancelDelayFrames--;
            if (cancelDelayFrames <= 0)
            {
                cancelPending = false;
                if (cancel != null && EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    EventSystem.current.SetSelectedGameObject(cancel);
                    Debug.Log($"[UICursor] キャンセル入力: {cancel.name} にフォーカスを移動しました");
                }
            }
        }
    }

    /// <summary>
    /// PlayerInput経由で呼ばれた場合の互換用（直接検出がメインのため、通常は使われない）
    /// </summary>
    public void OnCancel(InputAction.CallbackContext context)
    {
        if (context.performed && cancel != null && !cancelPending)
        {
            cancelPending = true;
            cancelDelayFrames = 2;
        }
    }
}
