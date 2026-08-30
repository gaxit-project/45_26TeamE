using UnityEngine;
using UnityEngine.SceneManagement; 

public class GoTitle : MonoBehaviour
{
    [SerializeField]
    private string sceneName = "01_Title"; 

    
    public void ChangeScene()
    {
        SceneManager.LoadScene(sceneName);
    }
}
