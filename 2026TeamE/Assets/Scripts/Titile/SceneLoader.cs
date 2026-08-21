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

    /// <summary>
    /// ローディング画面を表示中かどうか。
    /// 表示中は Time.timeScale を 0 にしているため、他の処理が時間を操作しないよう
    /// この間はポーズなどを受け付けないようにする。
    /// </summary>
    public bool IsLoading { get; private set; }

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
    // ローディング中はゲーム内の時間を止めるため、このコルーチン自身は
    // timeScale の影響を受けない unscaled な時間で動かしている。
    private IEnumerator LoadAsynchronously(string sceneName)
    {
        SoundManager.Instance?.StopBGM();

        // ローディング画面を出している間、遷移先シーンのタイマーや演出が
        // 裏側で進んでしまわないようにゲーム内の時間を止める
        IsLoading = true;
        Time.timeScale = 0f;

        try
        {
            // フェードイン
            loadingCanvas.SetActive(true);
            float fadeTime = 0.5f;
            while (canvasGroup.alpha < 1)
            {
                canvasGroup.alpha += Time.unscaledDeltaTime / fadeTime;
                yield return null;
            }

            // 1文字ずつ弾むアニメーションを開始
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
                displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, Time.unscaledDeltaTime * 2);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.5f);

            // シーンの切り替え
            operation.allowSceneActivation = true;

            while (!operation.isDone)
            {
                yield return null;
            }

            // シーンを切り替えた直後に走る重い初期化（地形生成など）の完了を待つ。
            // ここで待たないと、生成中の重いフレームがローディング画面の裏側で走り、
            // 画面が固まったように見えてしまう。
            while (SceneInitializationGate.IsBusy)
            {
                yield return null;
            }

            // 弾むアニメーションを停止
            if (textWaveRoutine != null)
            {
                StopCoroutine(textWaveRoutine);
                textWaveRoutine = null;
            }

            // フェードアウト
            while (canvasGroup.alpha > 0)
            {
                canvasGroup.alpha -= Time.unscaledDeltaTime / fadeTime;
                yield return null;
            }

            loadingCanvas.SetActive(false);
        }
        finally
        {
            // 途中で中断された場合でも時間が止まったままにならないよう、必ず元に戻す
            Time.timeScale = 1f;
            IsLoading = false;
        }
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
                // 全文字を一巡するのにかかる時間の中で、今どの文字が担当かを求める。
                // ローディング中はtimeScaleが0のため、影響を受けないunscaledTimeを使う。
                float cycleDuration = characterCount * secondsPerCharacter;
                float cycleTime = Time.unscaledTime % cycleDuration;
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
