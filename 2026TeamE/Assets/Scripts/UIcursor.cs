using UnityEngine;
using UnityEngine.EventSystems;
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
        GameObject selected = EventSystem.current.currentSelectedGameObject;

        if (selected == null || cursor == null)
        {
            return;
        }

        if (selected != lastSelected)
        {
            SoundManager.Instance?.PlaySE("つるはしで掘る1");
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
}
