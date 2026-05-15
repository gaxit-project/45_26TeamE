using UnityEngine;
using UnityEngine.SceneManagement;

public class CanvasManager : MonoBehaviour
{
    void GoTitle()
    {
        // タイトルシーンに遷移する
        UnityEngine.SceneManagement.SceneManager.LoadScene("01_Title");
    }
}
