using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
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
    [Tooltip("1文字が上がって戻ってくるのにかかる時間")]
    [SerializeField] private float secondsPerCharacter = 0.15f;

    [Header("ロード完了後の開始待ち")]
    [Tooltip("ロード完了時に表示する「ボタンを押してスタート」のUI")]
    [SerializeField] private GameObject readyPrompt;
    [Tooltip("ロード完了時にNowLoadingテキストを差し替える文言")]
    [SerializeField] private string readyMessage = "";

    private Coroutine textWaveRoutine;

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

    /// <summary>
    /// シーンを非同期で読み込む。ロードが終わり次第そのまま遷移する。
    /// </summary>
    public void LoadScene(string sceneName)
    {
        LoadScene(sceneName, false);
    }

    /// <summary>
    /// シーンを非同期で読み込む。
    /// </summary>
    /// <param name="sceneName">読み込むシーン名</param>
    /// <param name="waitForStartInput">
    /// true にすると、ロード完了後に開始ボタンが押されるまでローディング画面を出したまま待つ。
    /// 説明パネルを読ませたいタイトルからのゲーム開始時に使う。
    /// </param>
    public void LoadScene(string sceneName, bool waitForStartInput)
    {
        if(loadingCanvas.activeSelf) return;
        StartCoroutine(LoadAsynchronously(sceneName, waitForStartInput));
    }

    // 非同期ロード
    private IEnumerator LoadAsynchronously(string sceneName, bool waitForStartInput)
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

            // シーンを切り替えた直後に走る重い初期化（地形生成など）の完了を待つ
            while (SceneInitializationGate.IsBusy)
            {
                yield return null;
            }

            // 弾むアニメーションを停止し、文字の位置を元に戻す
            if (textWaveRoutine != null)
            {
                StopCoroutine(textWaveRoutine);
                textWaveRoutine = null;
            }

            // 指定された時だけ、プレイヤーが説明を読み終えるのを待つ。
            // 毎回ボタンを要求するとテンポが悪くなるため、呼び出し側で使い分ける。
            if (waitForStartInput)
            {
                yield return WaitForStartInputRoutine();
            }

            // フェードアウト
            while (canvasGroup.alpha > 0)
            {
                canvasGroup.alpha -= Time.unscaledDeltaTime / fadeTime;
                yield return null;
            }

            loadingCanvas.SetActive(false);
            if (readyPrompt != null) readyPrompt.SetActive(false);
        }
        finally
        {
            // 途中で中断された場合でも時間が止まったままにならないよう、必ず元に戻す
            Time.timeScale = 1f;
            IsLoading = false;
        }
    }

    /// <summary>
    /// ロードが終わったことを表示し、開始ボタンが押されるまで待機する
    /// </summary>
    private IEnumerator WaitForStartInputRoutine()
    {
        if (progressText != null)
        {
            progressText.text = readyMessage;
            progressText.ForceMeshUpdate();
        }

        if (readyPrompt != null) readyPrompt.SetActive(true);

        yield return null;

        while (!IsStartPressed())
        {
            yield return null;
        }

        SoundManager.Instance?.PlaySE("つるはしで掘る1");
    }

    /// <summary>
    /// 開始ボタンが押されたかどうか
    /// </summary>
    private static bool IsStartPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.spaceKey.wasPressedThisFrame) return true;
            if (keyboard.enterKey.wasPressedThisFrame) return true;
            if (keyboard.numpadEnterKey.wasPressedThisFrame) return true;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.buttonSouth.wasPressedThisFrame) return true;
            if (pad.startButton.wasPressedThisFrame) return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;

        return false;
    }

    /// <summary>
    /// 「NowLoading...」の文字を1文字ずつ順番に弾ませるアニメーション
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
                // 全文字を一巡するのにかかる時間の中で、今どの文字かを求める
                float cycleDuration = characterCount * secondsPerCharacter;
                float cycleTime = Time.unscaledTime % cycleDuration;
                int activeIndex = Mathf.Clamp(Mathf.FloorToInt(cycleTime / secondsPerCharacter), 0, characterCount - 1);
                float t = (cycleTime - activeIndex * secondsPerCharacter) / secondsPerCharacter; // 0〜1

                // 山型に上下する量（sinの半周期）
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
