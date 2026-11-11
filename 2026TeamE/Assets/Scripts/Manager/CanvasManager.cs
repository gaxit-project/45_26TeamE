using UnityEngine;
using UnityEngine.SceneManagement;

public class CanvasManager : MonoBehaviour
{
    void GoTitle()
    {
        // TimerManager側でタイムアップ後のリザルト遷移を制御している場合は二重遷移を防ぐ
        if (TimerManager.Instance != null) return;

        UnityEngine.SceneManagement.SceneManager.LoadScene("01_Title");
    }
}
