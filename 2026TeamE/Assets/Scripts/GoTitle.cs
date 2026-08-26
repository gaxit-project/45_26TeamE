using UnityEngine;
using UnityEngine.SceneManagement; // シーン切り替えに必要な名前空間

public class GoTitle : MonoBehaviour
{
    [SerializeField]
    private string sceneName = "01_Title"; // インスペクターから設定するシーン名

    // ボタンのクリックイベントなどに割り当てて呼び出します
    public void ChangeScene()
    {
        SceneManager.LoadScene(sceneName);
    }
}
