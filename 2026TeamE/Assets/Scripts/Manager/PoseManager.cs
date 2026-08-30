using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class PoseManager : MonoBehaviour
{
    
    [Header("ポーズメニュー")]
    [SerializeField] private GameObject pauseMenu;

    [Header("最初にフォーカスするボタン（コントローラー用）")]
    [SerializeField] private GameObject firstSelectedButton;

    
    public bool IsPaused => pauseMenu != null && pauseMenu.activeSelf;

    private float inputBlockEndTime = 0f;
    public bool IsTransitioning { get; private set; } = false;

    
    public bool IsInputBlocked => (pauseMenu != null && pauseMenu.activeSelf) || IsTransitioning || Time.unscaledTime < inputBlockEndTime;

    public void TogglePause(InputAction.CallbackContext context)
    {
        
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) return;

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
        Time.timeScale = 0f; 
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("つるはしで掘る1");
        pauseMenu.SetActive(true); 

        
        if (firstSelectedButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        }
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f; 
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("つるはしで掘る3");
        pauseMenu.SetActive(false); 
        inputBlockEndTime = Time.unscaledTime + 0.1f; 
    }

    
    public void ReturnToTitle()
    {
        StartCoroutine(ReturnToTitleCoroutine());
    }

    private IEnumerator ReturnToTitleCoroutine()
    {
        IsTransitioning = true; 

        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("つるはしで掘る1");
        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = 1f; 

        
        SceneManager.LoadScene("01_Title"); 
    }

    
    public void GoToResult()
    {
        StartCoroutine(GoToResultCoroutine());
    }

    private IEnumerator GoToResultCoroutine()
    {
        IsTransitioning = true; 

        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("つるはしで掘る1");
            SoundManager.Instance.StopBGM();
        }

        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = 1f; 

        SceneManager.LoadScene("Result"); 
        
    }

    public void QuitGame()
    {
        StartCoroutine (QuitGameCoroutine());
    }

    public IEnumerator QuitGameCoroutine()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("つるはしで掘る1");
        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = 1f; 
        Quit();
    }

    private void Quit()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
