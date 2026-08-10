using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 選択状態の検知に必要

public class ScaleOnSelect : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    // 選択されたときの拡大率（1.1倍）
    [SerializeField] private float selectScale = 1.1f;

    // 元のサイズを記憶する変数
    private Vector3 initialScale;

    private void Awake()
    {
        // 初期サイズ（通常は 1, 1, 1）を保存
        initialScale = transform.localScale;
    }

    // ボタンが選択されたときに呼び出される（EventSystemが検知）
    public void OnSelect(BaseEventData eventData)
    {
        transform.localScale = initialScale * selectScale;
    }

    // ボタンの選択が外れたときに呼び出される
    public void OnDeselect(BaseEventData eventData)
    {
        transform.localScale = initialScale;
    }

    // オブジェクトが非表示になったときはサイズを元に戻す
    private void OnDisable()
    {
        transform.localScale = initialScale;
    }

    public void GoExchange()
    {

    }
}
