using UnityEngine;
using UnityEngine.EventSystems;

public class ActiveExplanation : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("このボタンに対応する説明オブジェクト(Empty)")]
    [SerializeField] private GameObject descriptionGroup;

    // ボタンが選択されたとき（コントローラー・キーボード）
    public void OnSelect(BaseEventData eventData)
    {
        SetDescriptionActive(true);
    }

    // ボタンの選択が外れたとき
    public void OnDeselect(BaseEventData eventData)
    {
        SetDescriptionActive(false);
    }

    // マウスが乗ったとき
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetDescriptionActive(true);
    }

    // マウスが離れたとき
    public void OnPointerExit(PointerEventData eventData)
    {
        SetDescriptionActive(false);
    }

    private void SetDescriptionActive(bool isActive)
    {
        if (descriptionGroup != null)
        {
            descriptionGroup.SetActive(isActive);
        }
    }

    // 画面が閉じられたり非アクティブになった時のバグ防止
    private void OnDisable()
    {
        SetDescriptionActive(false);
    }
}
