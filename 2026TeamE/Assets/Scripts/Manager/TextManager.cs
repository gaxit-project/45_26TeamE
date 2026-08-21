using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TextManager : MonoBehaviour
{
    [Header("表示するオブジェクト")]
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
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        Image.gameObject.SetActive(false);
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