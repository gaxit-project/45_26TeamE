using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class KeyUIController : MonoBehaviour
{
    public static KeyUIController Instance { get; private set; }

    [Header("KeyImage")]
    [SerializeField] private Image[] keyImages;

    [Header("色設定")]
    [SerializeField] private Color lockedColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
    [SerializeField] private Color unlockedColor = Color.white;

    [Header("ポップ設定")]
    [SerializeField] private float punchScaleAmount = 1.3f;
    [SerializeField] private float animationDuration = 0.3f;

    [Header("フライアニメーション設定")]
    [SerializeField] private float flyDuration = 0.6f;
    [SerializeField] private float arcHeight = 150f;
    [SerializeField] private float startScale = 1.5f;

    private Canvas mainCanvas;
    private Camera mainCamera;
    private Camera canvasCamera;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        ResetKeys();
        mainCamera = Camera.main;
        mainCanvas = GetComponentInParent<Canvas>();
        if (mainCanvas != null && mainCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            canvasCamera = mainCanvas.worldCamera;
        }
    }

    public void FlyAndUpdateKeyUI(int collectedCount, Vector3 worldPos)
    {
        int targetIndex = collectedCount - 1;
        if (targetIndex >= 0 && targetIndex < keyImages.Length && mainCanvas != null && mainCamera != null)
        {
            StartCoroutine(FlyToUI(targetIndex, worldPos, collectedCount));
        }
        else
        {
            UpdateKeyUI(collectedCount);
        }
    }

    private IEnumerator FlyToUI(int targetIndex, Vector3 worldPosition, int finalCollectedCount)
    {
        RectTransform canvasRect = mainCanvas.GetComponent<RectTransform>();
        Image targetImage = keyImages[targetIndex];

        GameObject flyIcon = new GameObject("FlyKeyIcon");
        flyIcon.transform.SetParent(canvasRect, false);
        Image flyImage = flyIcon.AddComponent<Image>();
        flyImage.sprite = targetImage.sprite;
        flyImage.raycastTarget = false;
        
        RectTransform flyRect = flyIcon.GetComponent<RectTransform>();
        flyRect.sizeDelta = targetImage.rectTransform.rect.size;

        Vector3 screenStart = mainCamera.WorldToScreenPoint(worldPosition);
        if (screenStart.z < 0)
        {
            Destroy(flyIcon);
            UpdateKeyUI(finalCollectedCount);
            yield break;
        }

        Vector2 startLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenStart, canvasCamera, out startLocalPos);

        Vector3 containerScreenPos = RectTransformUtility.WorldToScreenPoint(canvasCamera, targetImage.transform.position);
        Vector2 endLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, containerScreenPos, canvasCamera, out endLocalPos);

        float elapsed = 0f;
        flyRect.anchoredPosition = startLocalPos;
        flyRect.localScale = Vector3.one * startScale;

        while (elapsed < flyDuration)
        {
            float t = elapsed / flyDuration;

            float easedT;
            if (t < 0.5f)
                easedT = 2f * t * t;
            else
                easedT = 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            Vector2 currentPos = Vector2.Lerp(startLocalPos, endLocalPos, easedT);
            float arc = arcHeight * 4f * t * (1f - t);
            currentPos.y += arc;

            flyRect.anchoredPosition = currentPos;
            flyRect.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, easedT);

            if (t > 0.8f)
            {
                float alpha = Mathf.Lerp(1f, 0.5f, (t - 0.8f) / 0.2f);
                Color c = flyImage.color;
                c.a = alpha;
                flyImage.color = c;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(flyIcon);
        UpdateKeyUI(finalCollectedCount);
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

    private Coroutine warningCoroutine; // 連打防止用の変数
    public void ShowWarning()
    {
        // 既にアニメーション中なら一旦止める（連打対策）
        if (warningCoroutine != null)
        {
            StopCoroutine(warningCoroutine);
        }
        // 新しくアニメーションを開始
        warningCoroutine = StartCoroutine(AnimateWarning());
    }
    private IEnumerator AnimateWarning()
    {
        Color warningColor = Color.red;
        float elapsed = 0f;
        float duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // スケールを 1.0 → 1.3 → 1.0 に変化
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;

            for (int i = 0; i < keyImages.Length; i++)
            {
                // ★ まだ持っていない鍵（unlockedColor ではないもの）だけを対象にする
                if (keyImages[i].color != unlockedColor)
                {
                    keyImages[i].color = warningColor; // 赤くする
                    keyImages[i].rectTransform.localScale = Vector3.one * scale; // 大きくする
                }
            }
            yield return null; // 1フレーム待つ
        }
        // アニメーションが終わったら、持っていない鍵を元の「暗い色(lockedColor)」とサイズに戻す
        for (int i = 0; i < keyImages.Length; i++)
        {
            if (keyImages[i].color != unlockedColor)
            {
                keyImages[i].rectTransform.localScale = Vector3.one;
                keyImages[i].color = lockedColor;
            }
        }

        warningCoroutine = null;
    }
}
