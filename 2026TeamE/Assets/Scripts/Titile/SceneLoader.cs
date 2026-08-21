using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private GameObject loadingCanvas;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("NowLoadingテキストの表示内容")]
    [SerializeField] private string loadingMessage = "NowLoading...";

    [Header("1文字ずつ弾むアニメーションの設定")]
    [Tooltip("跳ねる高さ")]
    [SerializeField] private float bounceHeight = 10f;
    [Tooltip("1文字が跳ね切る（上がって戻ってくる）のにかかる時間（秒）")]
    [SerializeField] private float secondsPerCharacter = 0.15f;

    private Coroutine textWaveRoutine;

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

        // 波打ちアニメーションを開始
        if (progressText != null)
        {
            progressText.text = loadingMessage;
            textWaveRoutine = StartCoroutine(AnimateLoadingText());
        }

        // シーンの非同期ロード
        float displayedProgress = 0f;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f || displayedProgress < 0.99f)
        {
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, Time.deltaTime * 2);
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        // シーンの切り替え
        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
            yield return null;
        }

        // 波打ちアニメーションを停止
        if (textWaveRoutine != null)
        {
            StopCoroutine(textWaveRoutine);
            textWaveRoutine = null;
        }

        // フェードアウト
        while (canvasGroup.alpha > 0)
        {
            canvasGroup.alpha -= Time.deltaTime / fadeTime;
            yield return null;
        }

        loadingCanvas.SetActive(false);
    }

    /// <summary>
    /// 「NowLoading...」の文字を1文字ずつ順番に弾ませるアニメーション。
    /// ある瞬間には1文字だけが上に跳ね、それが終わったら次の文字が跳ねる。
    /// </summary>
    private IEnumerator AnimateLoadingText()
    {
        while (true)
        {
            // 毎フレーム、元のレイアウト（跳ねていない状態）の頂点位置を取り直してから
            // 今回分のオフセットだけを加える。これをしないと前フレームの位置に積み重なってズレていく。
            progressText.ForceMeshUpdate();
            TMP_TextInfo textInfo = progressText.textInfo;
            int characterCount = textInfo.characterCount;

            if (characterCount > 0 && secondsPerCharacter > 0f)
            {
                // 全文字を一巡するのにかかる時間の中で、今どの文字が担当かを求める
                float cycleDuration = characterCount * secondsPerCharacter;
                float cycleTime = Time.time % cycleDuration;
                int activeIndex = Mathf.Clamp(Mathf.FloorToInt(cycleTime / secondsPerCharacter), 0, characterCount - 1);
                float t = (cycleTime - activeIndex * secondsPerCharacter) / secondsPerCharacter; // 0〜1

                // 0→1→0と山型に上下する量（sinの半周期）
                float bounce = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * bounceHeight;

                for (int i = 0; i < characterCount; i++)
                {
                    TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                    if (!charInfo.isVisible) continue;

                    int materialIndex = charInfo.materialReferenceIndex;
                    int vertexIndex = charInfo.vertexIndex;
                    Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                    float offsetY = (i == activeIndex) ? bounce : 0f;
                    if (offsetY != 0f)
                    {
                        Vector3 offset = new Vector3(0f, offsetY, 0f);
                        vertices[vertexIndex + 0] += offset;
                        vertices[vertexIndex + 1] += offset;
                        vertices[vertexIndex + 2] += offset;
                        vertices[vertexIndex + 3] += offset;
                    }
                }

                for (int i = 0; i < textInfo.meshInfo.Length; i++)
                {
                    textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                    progressText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
                }
            }

            yield return null;
        }
    }
}
