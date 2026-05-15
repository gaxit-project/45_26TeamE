using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class PoseManager : MonoBehaviour
{
    // ポーズメニュー
    [Header("ポーズメニュー")]
    [SerializeField] private GameObject pauseMenu;

    [Header("最初にフォーカスするボタン（コントローラー用）")]
    [SerializeField] private GameObject firstSelectedButton;

    // ポーズ画面がアクティブかどうかを取得
    public bool IsPaused => pauseMenu.activeSelf;

    private float inputBlockEndTime = 0f;
    public bool IsTransitioning { get; private set; } = false;

    // ポーズ中、シーン遷移中、またはポーズ解除直後の0.1秒間は入力をブロックする
    public bool IsInputBlocked => pauseMenu.activeSelf || IsTransitioning || Time.unscaledTime < inputBlockEndTime;

    public void TogglePause(InputAction.CallbackContext context)
    {
        if (pauseMenu.activeSelf)
        {
            if (context.performed)
                ResumeGame();
        }
        else
        {
            if (context.performed)
                PauseGame();
        }
    }

    private void PauseGame()
    {
        Time.timeScale = 0f; // ゲームを停止
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("つるはしで掘る1");
        pauseMenu.SetActive(true); // ポーズメニューを表示

        // コントローラー操作用に指定したボタンへフォーカスを当てる
        if (firstSelectedButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        }
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f; // ゲームを再開
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("つるはしで掘る3");
        pauseMenu.SetActive(false); // ポーズメニューを非表示
        inputBlockEndTime = Time.unscaledTime + 0.1f; // 閉じた後0.1秒間は入力をブロック
    }

    // スタート画面（タイトル）に戻る処理
    public void ReturnToTitle()
    {
        StartCoroutine(ReturnToTitleCoroutine());
    }

    private IEnumerator ReturnToTitleCoroutine()
    {
        IsTransitioning = true; // 遷移開始（入力をブロック）

        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("つるはしで掘る1");
        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = 1f; // 時間の進行を元に戻す

        // 進行状況（ショップの強化状態など）を初期化
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        // お金やステージ進行を保持しているマネージャーを破棄してリセット
        if (MainManager.Instance != null)
        {
            Destroy(MainManager.Instance.gameObject);
        }
        if (MoneyManager.Instance != null)
        {
            Destroy(MoneyManager.Instance.gameObject);
        }
        
        SceneManager.LoadScene("01_Title"); // タイトルシーンを読み込む
    }

    // リザルト画面へ移行する処理
    public void GoToResult()
    {
        StartCoroutine(GoToResultCoroutine());
    }

    private IEnumerator GoToResultCoroutine()
    {
        IsTransitioning = true; // 遷移開始（入力をブロック）

        // BGMを止める（リザルト画面での重複再生を防ぐため）
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("つるはしで掘る1");
            SoundManager.Instance.StopBGM();
        }

        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = 1f; // 時間の進行を元に戻す

        SceneManager.LoadScene("Result"); // リザルトシーンを読み込む
        
    }

    public void QuitGame()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("つるはしで掘る1");
        Application.Quit(); // ゲームを終了
    }
}
