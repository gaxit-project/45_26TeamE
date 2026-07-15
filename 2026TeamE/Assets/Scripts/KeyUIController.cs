using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class KeyUIController : MonoBehaviour
{
    public static KeyUIController Instance { get; private set; }

    [Header("KeyImage")]
    [SerializeField] private Image[] keyImages;

    [Header("êFê›íË")]
    [SerializeField] private Color lockedColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
    [SerializeField] private Color unlockedColor = Color.white;

    [Header("ââèoópê›íË")]
    [SerializeField] private float punchScaleAmount = 1.3f;
    [SerializeField] private float animationDuration = 0.3f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        ResetKeys();
    }

    public void UpdateKeyUI(int collectedCount)
    {
        for (int i = 0; i < keyImages.Length; i++)
        {
            if (i < collectedCount)
            {
                if (keyImages[i].color != unlockedColor)
                {
                    keyImages[i].color = unlockedColor;
                    StartCoroutine(AnimateKeyGet(keyImages[i].rectTransform));
                }
            }
            else
            {
                keyImages[i].color = lockedColor;
            }
        }
    }

    public void ResetKeys()
    {
        foreach (var img in keyImages)
        {
            img.color = lockedColor;
            img.rectTransform.localScale = Vector3.one;
        }
    }

    private IEnumerator AnimateKeyGet(RectTransform target)
    {
        float elapsed = 0f;
        Vector3 initialScale = Vector3.one;
        Vector3 targetScale = Vector3.one * punchScaleAmount;
        while (elapsed < animationDuration * 0.4f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (animationDuration * 0.4f);
            target.localScale = Vector3.Lerp(initialScale, targetScale, t);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < animationDuration * 0.6f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (animationDuration * 0.6f);
            target.localScale = Vector3.Lerp(targetScale, initialScale, t);
            yield return null;
        }

        target.localScale = Vector3.one;
    }
}