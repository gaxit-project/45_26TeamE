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
        pauseMenu.SetActive(false); // ポーズメニューを非表示
    }

    // スタート画面（タイトル）に戻る処理
    public void ReturnToTitle()
    {
        Time.timeScale = 1f; // 時間の進行を元に戻す（重要）

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
        Time.timeScale = 1f; // 時間の進行を元に戻す

        // BGMを止める（リザルト画面での重複再生を防ぐため）
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopBGM();
        }

        SceneManager.LoadScene("Result"); // リザルトシーンを読み込む
    }
}
