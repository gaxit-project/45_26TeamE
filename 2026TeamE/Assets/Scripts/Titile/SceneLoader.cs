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

    
    
    
    public void LoadScene(string sceneName)
    {
        LoadScene(sceneName, false);
    }

    
    
    
    
    
    
    
    
    public void LoadScene(string sceneName, bool waitForStartInput)
    {
        if(loadingCanvas.activeSelf) return;
        StartCoroutine(LoadAsynchronously(sceneName, waitForStartInput));
    }

    
    private IEnumerator LoadAsynchronously(string sceneName, bool waitForStartInput)
    {
        SoundManager.Instance?.StopBGM();

        
        
        IsLoading = true;
        Time.timeScale = 0f;

        try
        {
            
            loadingCanvas.SetActive(true);
            float fadeTime = 0.5f;
            while (canvasGroup.alpha < 1)
            {
                canvasGroup.alpha += Time.unscaledDeltaTime / fadeTime;
                yield return null;
            }

            
            if (progressText != null)
            {
                progressText.text = loadingMessage;
                textWaveRoutine = StartCoroutine(AnimateLoadingText());
            }

            
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

            
            operation.allowSceneActivation = true;

            while (!operation.isDone)
            {
                yield return null;
            }

            
            while (SceneInitializationGate.IsBusy)
            {
                yield return null;
            }

            
            if (textWaveRoutine != null)
            {
                StopCoroutine(textWaveRoutine);
                textWaveRoutine = null;
            }

            
            
            if (waitForStartInput)
            {
                yield return WaitForStartInputRoutine();
            }

            
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
            
            Time.timeScale = 1f;
            IsLoading = false;
        }
    }

    
    
    
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

    
    
    
    private IEnumerator AnimateLoadingText()
    {
        while (true)
        {
            
            
            progressText.ForceMeshUpdate();
            TMP_TextInfo textInfo = progressText.textInfo;
            int characterCount = textInfo.characterCount;

            if (characterCount > 0 && secondsPerCharacter > 0f)
            {
                
                float cycleDuration = characterCount * secondsPerCharacter;
                float cycleTime = Time.unscaledTime % cycleDuration;
                int activeIndex = Mathf.Clamp(Mathf.FloorToInt(cycleTime / secondsPerCharacter), 0, characterCount - 1);
                float t = (cycleTime - activeIndex * secondsPerCharacter) / secondsPerCharacter; 

                
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
