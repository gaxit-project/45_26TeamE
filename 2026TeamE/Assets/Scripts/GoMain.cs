using UnityEngine;
using UnityEngine.SceneManagement;

public class GoMain : MonoBehaviour
{
    public void GoMainScene()
    {
        SceneManager.LoadScene("02_Main");
    }

    public void Sound()
    {
        SoundManager.Instance.PlaySE("ƒhƒŠƒ‹‚ÅŒ@‚é1");
    }
}
