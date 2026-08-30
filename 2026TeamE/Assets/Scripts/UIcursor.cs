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
    [SerializeField] private Vector2 sizePadding = new Vector2(20f, 20f); 

    [Header("SE")]
    [SerializeField] private string moveSE = "Select";
    private GameObject lastSelected;
    private RectTransform lastSelectedRect;

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
        
        if (!cancelPending && cancel != null)
        {
            bool cancelPressed = false;

            
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                cancelPressed = true;

            
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
                cancelPressed = true;

            if (cancelPressed)
            {
                cancelPending = true;
                cancelDelayFrames = 2;
            }
        }

        
        GameObject selected = EventSystem.current.currentSelectedGameObject;

        if (selected == null || cursor == null)
        {
            return;
        }

        if (selected != lastSelected)
        {
            SoundManager.Instance?.PlaySE("つるはしで掘る2");
            lastSelected = selected;
            lastSelectedRect = selected.GetComponent<RectTransform>();
        }

        Transform target = selected.transform;

        
        Vector3 newPos = cursor.transform.position;
        newPos.x = Mathf.Lerp(newPos.x, target.position.x, Time.unscaledDeltaTime * scrollSpeed);
        newPos.y = Mathf.Lerp(newPos.y, target.position.y, Time.unscaledDeltaTime * scrollSpeed);
        cursor.transform.position = newPos;

        
        if (autoResize && cursorRect != null)
        {
            RectTransform targetRect = lastSelectedRect;
            if (targetRect != null)
            {
                
                Vector2 targetSize = targetRect.rect.size;
                
                if (cursorRect.parent != null)
                {
                    
                    float relativeScaleX = targetRect.lossyScale.x / cursorRect.parent.lossyScale.x;
                    float relativeScaleY = targetRect.lossyScale.y / cursorRect.parent.lossyScale.y;
                    targetSize = new Vector2(targetRect.rect.width * relativeScaleX, targetRect.rect.height * relativeScaleY);
                }
                else
                {
                    
                    targetSize = new Vector2(targetRect.rect.width * targetRect.localScale.x, targetRect.rect.height * targetRect.localScale.y);
                }

                
                targetSize += sizePadding;
                
                
                cursorRect.sizeDelta = Vector2.Lerp(cursorRect.sizeDelta, targetSize, Time.unscaledDeltaTime * scrollSpeed);
            }
        }
    }

    void LateUpdate()
    {
        
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

    
    
    
    public void OnCancel(InputAction.CallbackContext context)
    {
        if (context.performed && cancel != null && !cancelPending)
        {
            cancelPending = true;
            cancelDelayFrames = 2;
        }
    }
}
