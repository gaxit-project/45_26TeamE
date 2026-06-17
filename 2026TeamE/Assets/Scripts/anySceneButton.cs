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
        // UI Button component auto-hookup if present
        UnityEngine.UI.Button uiButton = GetComponent<UnityEngine.UI.Button>();
        if (uiButton != null)
        {
            uiButton.onClick.AddListener(OnClick);
        }
    }

    /// <summary>
    /// UIボタンのOnClickイベントから呼び出す、または自動フックされます。
    /// </summary>
    public void OnClick()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        isTransitioning = true;

        // SEを鳴らす (SoundManagerが存在する場合)
        if (!string.IsNullOrEmpty(seCueName) && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(seCueName);
        }

        // 指定時間待機する
        yield return new WaitForSeconds(delay);

        // シーン遷移を行う
        if (!string.IsNullOrEmpty(sceneName))
        {
            // SceneLoaderがある場合はそちらを利用し、なければ通常のSceneManagerを利用
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(sceneName);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }
        else
        {
            Debug.LogWarning("遷移先のシーン名が設定されていません。");
            isTransitioning = false;
        }
    }
}

