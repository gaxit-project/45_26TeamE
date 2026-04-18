using UnityEngine;
using UnityEngine.InputSystem;

public class PoseManager : MonoBehaviour
{
    //ポーズの管理
    [Header("ポーズの管理")]
    [SerializeField] private GameObject pauseMenu;

    //ポーズかどうかのフラグ　他から参照可能
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
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f; // ゲームを再開
        pauseMenu.SetActive(false); // ポーズメニューを非表示
    }
}
