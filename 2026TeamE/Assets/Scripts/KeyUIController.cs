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

                    
                    StartCoroutine(AnimateKeyGet(keyImages[i].rectTransform, collectedCount));
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

    private IEnumerator AnimateKeyGet(RectTransform target,int collectedCount=0)
    {

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("鍵ゲット");
        }

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

        if (collectedCount == 3)
        {
            TextManager.Instance.ShowText("先へ進めるようになった！");
            if(SoundManager.Instance != null)
                SoundManager.Instance.PlaySE("A_luxurious_upgrade");
        }
    }

    private bool isWarningActive = false;
    private Coroutine warningCoroutine;
    
    public void SetWarningActive(bool active)
    {
        if (isWarningActive == active) return;
        isWarningActive = active;

        if (active)
        {
            if (warningCoroutine != null) StopCoroutine(warningCoroutine);
            warningCoroutine = StartCoroutine(AnimateWarningContinuous());
        }
        else
        {
            if (warningCoroutine != null)
            {
                StopCoroutine(warningCoroutine);
                warningCoroutine = null;
            }

            for (int i = 0; i < keyImages.Length; i++)
            {
                if (keyImages[i].color != unlockedColor)
                {
                    keyImages[i].rectTransform.localScale = Vector3.one;
                    keyImages[i].color = lockedColor;
                }
            }
        }
    }

    private IEnumerator AnimateWarningContinuous()
    {
        Color warningColor = Color.red;
        float elapsed = 0f;
        float cycleDuration = 0.6f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = (elapsed % cycleDuration) / cycleDuration;

            float pingPong = Mathf.PingPong(t * 2f, 1f);
            float scale = 1f + pingPong * 0.3f;
            Color currentColor = Color.Lerp(lockedColor, warningColor, pingPong);

            for (int i = 0; i < keyImages.Length; i++)
            {
                if (keyImages[i].color != unlockedColor)
                {
                    keyImages[i].color = currentColor;
                    keyImages[i].rectTransform.localScale = Vector3.one * scale;
                }
            }
            yield return null;
        }
    }
}
