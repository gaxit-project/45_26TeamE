using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TextManager : MonoBehaviour
{
    [Header("検索するUIのオブジェクト名 (インスペクターで設定)")]
    public string imageName = "UI_Image"; // 背景画像のオブジェクト名
    public string textName = "UI_Text";   // テキストのオブジェクト名

    [Header("表示オブジェクト")]
    public Image Image;
    [Header("テキスト")]
    public TextMeshProUGUI Text;
    public float targetAlphaImg = 0.4f;
    public float targetAlphaTxt = 0.8f;
    public float fadeDuration = 0.5f;
    public float showTimes = 1f;

    public static TextManager Instance { get; private set; }

    private Coroutine currentCoroutine;

    void Awake()
    {
        if (Instance == null) 
        { 
            Instance = this; 
            SceneManager.sceneLoaded += OnSceneLoaded;
            FindUIComponents(); // 初回ロード時用に追加
        }
        else 
        { 
            Destroy(gameObject); 
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindUIComponents();
    }

    private void FindUIComponents()
    {
        // 背景画像のUIを探す (非アクティブでも検索可能にする)
        if (Image == null)
        {
            Image = FindInactiveComponentByName<Image>(imageName);
            if (Image == null)
            {
                Debug.LogWarning($"TextManager: '{imageName}' という名前の画像オブジェクトが見つかりません。");
            }
        }

        // テキストのUIを探す (非アクティブでも検索可能にする)
        if (Text == null)
        {
            Text = FindInactiveComponentByName<TextMeshProUGUI>(textName);
            if (Text == null)
            {
                Debug.LogWarning($"TextManager: '{textName}' という名前のテキストオブジェクトが見つかりません。");
            }
        }
        
        // 再割り当てできたら非表示にしておく
        if (Image != null)
        {
            Image.gameObject.SetActive(false);
        }
    }

    // 非アクティブなオブジェクトも名前に基づいて検索するヘルパーメソッド
    private T FindInactiveComponentByName<T>(string name) where T : Component
    {
        T[] allComponents = Resources.FindObjectsOfTypeAll<T>();
        foreach (T comp in allComponents)
        {
            // シーン上に存在し、名前が一致するものを返す (プレハブは除外)
            if (comp.gameObject.name == name && comp.gameObject.scene.isLoaded)
            {
                return comp;
            }
        }
        return null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void Start()
    {
        if (Image != null)
        {
            Image.gameObject.SetActive(false);
        }
    }

    public void ShowText(string text)
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }
        currentCoroutine = StartCoroutine(ShowTextCorutin(text));
    }

    private IEnumerator ShowTextCorutin(string text)
    {
        Text.text = text;
        Image.gameObject.SetActive(true);

        SetAlpha(0f,0f);

        float time = 0;
        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float t = time / fadeDuration;
            SetAlpha(Mathf.Lerp(0f, targetAlphaImg, t), Mathf.Lerp(0f, targetAlphaTxt, t));
            yield return null; // 1フレーム待機
        }
        SetAlpha(targetAlphaImg, targetAlphaTxt);

        yield return new WaitForSeconds(showTimes);

        time = 0;
        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float t = time / fadeDuration;
            SetAlpha(Mathf.Lerp(targetAlphaImg, 0f, t), Mathf.Lerp(targetAlphaTxt,0f, t));
            yield return null;
        }
        SetAlpha(0f,0f);

        Image.gameObject.SetActive(false);
    }

    private void SetAlpha(float alphaImg,float alphaTxt)
    {
        Color imgColor = Image.color;
        imgColor.a = alphaImg;
        Image.color = imgColor;

        Color txtColor = Text.color;
        txtColor.a = alphaTxt;
        Text.color = txtColor;
    }
}