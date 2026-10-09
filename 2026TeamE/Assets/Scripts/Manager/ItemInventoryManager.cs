using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemInventoryManager : MonoBehaviour
{
    public static ItemInventoryManager Instance { get; private set; }

    [Header("UI参照")]
    [SerializeField, Tooltip("メインCanvasのRectTransform")]
    private RectTransform canvasRect;

    [SerializeField, Tooltip("右上のアイコン配置パネル（HorizontalLayoutGroup推奨）")]
    private RectTransform iconContainer;

    [SerializeField, Tooltip("アイコン用UIプレハブ（Image + LayoutElement コンポーネント付き）")]
    private GameObject iconPrefab;

    [Header("フライアニメーション設定")]
    [SerializeField] private float flyDuration = 0.6f;
    [SerializeField] private float arcHeight = 150f;
    [SerializeField] private float startScale = 1.5f;

    [Header("ポップインアニメーション設定")]
    [SerializeField] private float popDuration = 0.25f;

    [Header("アイコンサイズ")]
    [SerializeField, Tooltip("アイコンの縦横サイズ（ピクセル）")]
    private float iconWidth = 64f;

    [System.Serializable]
    public class ItemDisplaySetting
    {
        public ItemType type;
        public Sprite icon;
    }

    [Header("最初から表示するアイテム設定（袋アイコン用）")]
    [SerializeField, Tooltip("ここに登録された袋のアイコンを右上に表示します")]
    private List<ItemDisplaySetting> displaySettings = new List<ItemDisplaySetting>();

    private Camera mainCamera;
    private Camera canvasCamera;

    private GameObject bagSlotObj;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;

        if (canvasRect != null)
        {
            Canvas canvas = canvasRect.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvasCamera = canvas.worldCamera;
            }
        }
        
        ClearItems();
    }

    private Sprite GetBagSprite()
    {
        foreach (var setting in displaySettings)
        {
            if (setting.type == ItemType.LeatherBag || setting.type == ItemType.GoldLeatherBag)
                return setting.icon;
        }
        return displaySettings.Count > 0 ? displaySettings[0].icon : null;
    }

    public void ClearItems()
    {
        if (iconContainer != null)
        {
            foreach (Transform child in iconContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // 袋のアイコンを1つだけ生成する
        if (iconContainer != null && iconPrefab != null)
        {
            bagSlotObj = Instantiate(iconPrefab, iconContainer);
            Image slotImage = bagSlotObj.GetComponent<Image>();
            Sprite bagSprite = GetBagSprite();
            if (slotImage != null && bagSprite != null) slotImage.sprite = bagSprite;

            LayoutElement layout = bagSlotObj.GetComponent<LayoutElement>();
            if (layout == null) layout = bagSlotObj.AddComponent<LayoutElement>();
            layout.preferredWidth = iconWidth;
            layout.preferredHeight = iconWidth;
            
            // NOTE: 金額のテキストは別途 MoneyUIInitializer 等で表示される想定
        }
    }

    // 互換性のためにメソッド名はAddItemのままにしておく（実際はお金として処理）
    public void AddItem(ItemType type, Sprite icon, Vector3 worldPosition, int moneyValue = 0)
    {
        if (type == ItemType.Key) return; 

        StartCoroutine(FlyToUI(icon, worldPosition, moneyValue));
    }

    private IEnumerator FlyToUI(Sprite flyIconSprite, Vector3 worldPosition, int moneyValue)
    {
        if (canvasRect == null || iconPrefab == null || mainCamera == null)
        {
            OnFlyFinished(moneyValue);
            yield break;
        }
        
        GameObject flyIcon = Instantiate(iconPrefab, canvasRect);
        Image flyImage = flyIcon.GetComponent<Image>();
        if (flyImage != null)
        {
            flyImage.sprite = flyIconSprite;
            flyImage.raycastTarget = false;
        }

        LayoutElement flyLayout = flyIcon.GetComponent<LayoutElement>();
        if (flyLayout != null) flyLayout.ignoreLayout = true;

        RectTransform flyRect = flyIcon.GetComponent<RectTransform>();

        Vector3 screenStart = mainCamera.WorldToScreenPoint(worldPosition);
        if (screenStart.z < 0)
        {
            Destroy(flyIcon);
            OnFlyFinished(moneyValue);
            yield break;
        }

        Vector2 startLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenStart, canvasCamera, out startLocalPos);

        Vector3 containerScreenPos = RectTransformUtility.WorldToScreenPoint(canvasCamera, iconContainer.position);
        
        if (bagSlotObj != null)
        {
            containerScreenPos = RectTransformUtility.WorldToScreenPoint(canvasCamera, bagSlotObj.transform.position);
        }

        Vector2 endLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, containerScreenPos, canvasCamera, out endLocalPos);

        float elapsed = 0f;
        flyRect.anchoredPosition = startLocalPos;
        flyRect.localScale = Vector3.one * startScale;

        if (SoundManager.Instance != null)
        {
            // お金が飛ぶ音などがあれば再生
            // SoundManager.Instance.PlaySE("コイン");
        }

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

            if (flyImage != null && t > 0.8f)
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
        
        OnFlyFinished(moneyValue);
    }

    private void OnFlyFinished(int moneyValue)
    {
        if (MoneyManager.Instance != null && moneyValue > 0)
        {
            MoneyManager.Instance.MoneyOnHandIncrease(moneyValue);
        }

        if (bagSlotObj != null)
        {
            StartCoroutine(PopInAnimation(bagSlotObj.transform));
        }
    }

    private IEnumerator PopInAnimation(Transform target)
    {
        float elapsed = 0f;
        
        while (elapsed < popDuration)
        {
            float t = elapsed / popDuration;
            float overshoot = 1.7f;
            float easedT = 1f + (overshoot + 1f) * Mathf.Pow(t - 1f, 3f)
                              + overshoot * Mathf.Pow(t - 1f, 2f);
            target.localScale = Vector3.one * easedT;
            elapsed += Time.deltaTime;
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    [System.Serializable]
    public class ItemData
    {
        public ItemType type;
        public Sprite icon;
        public int moneyValue;
    }

    // 古い互換性用メソッド群（エラーが出ないように空実装）
    public static List<ItemData> GetCollectedItemsForResult() => new List<ItemData>();
    public static int GetTotalMoneyValue() => 0;
    public static void ClearCollectedData() { }
    public int GetTotalItemCount() => 0;
    public int GetItemCount(ItemType type) => 0;
    public bool IsRemoving() => false;
    public int RemoveItemsFromEnd(int count) => count;
    public int RemoveItemsRandom(int count) => count;
    public List<ItemData> RemoveItemsRandomWithData(int count) => new List<ItemData>();
    public bool RemoveItemByType(ItemType type) => true;
    public bool RemoveLastItem() => true;
}
