using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class anySceneButton : MonoBehaviour
{
    [Header("Transition Settings")]
    [SerializeField] private string sceneName;
    [SerializeField] private float delay = 0.5f;

    [Header("SE Settings")]
    [SerializeField] private string seCueName;

    private bool isTransitioning = false;

    private void Start()
    {
        UnityEngine.UI.Button uiButton = GetComponent<UnityEngine.UI.Button>();
        if (uiButton != null)
        {
            uiButton.onClick.AddListener(OnClick);
        }
    }
    public void OnClick()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        isTransitioning = true;

        if (!string.IsNullOrEmpty(seCueName) && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(seCueName);
        }

        
        yield return new WaitForSeconds(delay);

        
        if (!string.IsNullOrEmpty(sceneName))
        {
            string targetScene = sceneName;

            
            if (GoalJewelry.isGoalReached && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Result")
            {
                targetScene = "FinalResult";
                GoalJewelry.isGoalReached = false;
            }

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(targetScene);
            }
            else
            {
                SceneManager.LoadScene(targetScene);
            }
        }
        else
        {
            Debug.LogWarning("遷移先のシーン名が設定されていません。");
            isTransitioning = false;
        }
    }
}

