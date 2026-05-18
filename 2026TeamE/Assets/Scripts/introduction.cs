using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class introduction : MonoBehaviour
{
    [Header("最初にフォーカスするボタン")]
    public GameObject firstSelectedButton; 

    // 他のスクリプトから「今イントロダクション画面が開いているか」を確認できるようにする
    public static bool IsActive { get; private set; }

    private void OnEnable()
    {
        IsActive = true;
    }

    private void OnDisable()
    {
        IsActive = false;
    }

    private IEnumerator Start()
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        yield return new WaitForSeconds(1f);

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        Time.timeScale = 0f;
        
        if (firstSelectedButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        }
    }

    private void Update()
    {
        // 画面が開いている間は、決定ボタン以外にフォーカスが移らないように強制ロックする
        if (IsActive && groupIsVisible() && firstSelectedButton != null && EventSystem.current != null)
        {
            if (EventSystem.current.currentSelectedGameObject != firstSelectedButton)
            {
                EventSystem.current.SetSelectedGameObject(firstSelectedButton);
            }
        }
    }

    // 表示が完了しているかどうかを判定
    private bool groupIsVisible()
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        return group != null && group.alpha >= 1f;
    }

    public void Onstart()
    {
        Time.timeScale = 1f;
        gameObject.SetActive(false);
    }
}
