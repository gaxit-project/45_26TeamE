using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class introduction : MonoBehaviour
{
    [Header("最初にフォーカスするボタン")]
    public GameObject firstSelectedButton;

    /// <summary>ゲーム開始からイントロダクション画面を表示するまでの待ち時間（秒）。カメラ演出の尺に合わせている。</summary>
    private const float ShowDelaySeconds = 9f;

    // 他のスクリプトから「今イントロダクション画面が開いているか」を確認できるようにする
    public static bool IsActive { get; private set; }
    private static bool hasAlreadyShown = false;

    private void Awake()
    {
        if (hasAlreadyShown)
        {
            IsActive = false;
            gameObject.SetActive(false);
            return;
        }

        IsActive = false;
    }

    private void OnDisable()
    {
        IsActive = false;
    }

    private IEnumerator Start()
    {
        // Awakeで非表示にされなかった（＝1回目の）場合のみ、以下の処理が進みます
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }

        // 最初は非表示
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        // ローディング画面が出ている間は待機を始めない。
        // ここで待たないと、ロードにかかった時間の分だけ表示が早まってしまう。
        while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
        {
            yield return null;
        }

        yield return new WaitForSecondsRealtime(ShowDelaySeconds);

        // 待機後に表示
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        // 画面が開いた状態にする
        IsActive = true;

        hasAlreadyShown = true;

        // ゲーム内の時間を停止
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
