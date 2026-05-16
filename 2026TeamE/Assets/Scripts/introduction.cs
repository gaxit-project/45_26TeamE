using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class introduction : MonoBehaviour
{
    [Header("最初にフォーカスするボタン")]
    public GameObject firstSelectedButton; 

    private IEnumerator Start()
    {
        // パネルを非表示にするためにCanvasGroupを使う（無ければ自動追加）
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }

        // 最初は透明にして、ボタンも押せない状態にする
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        // 1秒待つ
        yield return new WaitForSeconds(0.5f);

        // 1秒経ったら透明度を戻して表示する
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        // 時間を止める
        Time.timeScale = 0f;
        
        // 指定したボタンに自動でフォーカスを当てる
        if (firstSelectedButton != null)
        {
            yield return new WaitForSecondsRealtime(0.2f);
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        }
    }

    public void Onstart()
    {
        // 時間を元に戻して、この画面を非表示にする
        Time.timeScale = 1f;
        gameObject.SetActive(false);
    }
}
