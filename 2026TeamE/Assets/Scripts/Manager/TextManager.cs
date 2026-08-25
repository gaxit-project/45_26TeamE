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
        }
        else 
        { 
            Destroy(gameObject); 
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 背景画像のUIを探す
        GameObject imgObj = GameObject.Find(imageName);
        if (imgObj != null)
        {
            Image = imgObj.GetComponent<Image>();
        }
        else
        {
            Debug.LogWarning($"TextManager: '{imageName}' という名前の画像オブジェクトが見つかりません。");
        }

        // テキストのUIを探す
        GameObject textObj = GameObject.Find(textName);
        if (textObj != null)
        {
            Text = textObj.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            Debug.LogWarning($"TextManager: '{textName}' という名前のテキストオブジェクトが見つかりません。");
        }
        
        // 再割り当てできたら非表示にしておく
        if (Image != null)
        {
            Image.gameObject.SetActive(false);
        }
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