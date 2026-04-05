using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private GameObject loadingCanvas;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            loadingCanvas.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadScene(string sceneName)
    {
        if(loadingCanvas.activeSelf) return;
        StartCoroutine(LoadAsynchronously(sceneName));
    }

    // 非同期でシーンをロードするコルーチン
    private IEnumerator LoadAsynchronously(string sceneName)
    {
        SoundManager.Instance?.StopBGM();

        // フェードイン
        loadingCanvas.SetActive(true);
        float fadeTime = 0.5f;
        while (canvasGroup.alpha < 1)
        {
            canvasGroup.alpha += Time.deltaTime / fadeTime;
            yield return null;
        }

        // シーンの非同期ロード
        progressBar.value = 0;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f || progressBar.value < 0.99f)
        {
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            progressBar.value = Mathf.MoveTowards(progressBar.value, targetProgress, Time.deltaTime * 2);
            progressText.text = $"Loading... {Mathf.RoundToInt(progressBar.value * 100)}%";
            yield return null;
        }

        progressBar.value = 1;
        yield return new WaitForSeconds(0.5f);

        // シーンの切り替え
        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
            yield return null;
        }

        // フェードアウト
        while (canvasGroup.alpha > 0)
        {
            canvasGroup.alpha -= Time.deltaTime / fadeTime;
            yield return null;
        }

        loadingCanvas.SetActive(false);
    }
}
