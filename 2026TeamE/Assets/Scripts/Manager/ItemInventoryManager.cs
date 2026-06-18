using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アイテム取得時のUIフライアニメーション＆右上アイコン表示を管理するマネージャー
/// </summary>
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

    [Header("削除アニメーション設定")]
    [SerializeField] private float removeDuration = 0.3f;

    [Header("アイコンサイズ")]
    [SerializeField, Tooltip("アイコンの縦横サイズ（ピクセル）")]
    private float iconWidth = 64f;

    [Header("テキスト設定")]
    [SerializeField, Tooltip("「x0」などの個数テキストのフォントサイズ")]
    private float countTextSize = 24f;
    [SerializeField, Tooltip("テキストの表示位置のズレ（右下のアンカーからのオフセット）")]
    private Vector2 countTextOffset = new Vector2(10f, -10f);

    [System.Serializable]
    public class ItemDisplaySetting
    {
        public ItemType type;
        public Sprite icon;
    }

    [Header("最初から表示するアイテム設定")]
    [SerializeField, Tooltip("未取得状態(x0)でもアイコンを表示したいものを登録")]
    private List<ItemDisplaySetting> displaySettings = new List<ItemDisplaySetting>();

    // ========== アイテムデータ（シーン間引き継ぎ用にstaticリスト） ==========

    [System.Serializable]
    public class ItemData
    {
        public ItemType type;
        public Sprite icon;
        public int moneyValue;
    }

    private static List<ItemData> s_collectedItems = new List<ItemData>();

    // UIスロットの管理
    private Dictionary<ItemType, GameObject> uiSlots = new Dictionary<ItemType, GameObject>();
    private Dictionary<ItemType, TextMeshProUGUI> countTexts = new Dictionary<ItemType, TextMeshProUGUI>();

    private Camera mainCamera;
    private Camera canvasCamera;
    private int removingCount = 0;

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
        ClearItems();

        mainCamera = Camera.main;

        if (canvasRect != null)
        {
            Canvas canvas = canvasRect.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvasCamera = canvas.worldCamera;
            }
        }
    }

    // =====================================================================
    //  アイテム追加
    // =====================================================================

    public void AddItem(ItemType type, Sprite icon, Vector3 worldPosition, int moneyValue = 0)
    {
        ItemData data = new ItemData
        {
            type = type,
            icon = icon,
            moneyValue = moneyValue
        };
        s_collectedItems.Add(data);

        StartCoroutine(FlyToUI(data, worldPosition));
    }

    private IEnumerator FlyToUI(ItemData data, Vector3 worldPosition)
    {
        if (canvasRect == null || iconPrefab == null || mainCamera == null)
        {
            CreateOrUpdateSlot(data.type, data.icon);
            yield break;
        }

        // 1. フライ用の一時アイコンを Canvas 直下に生成
        GameObject flyIcon = Instantiate(iconPrefab, canvasRect);
        Image flyImage = flyIcon.GetComponent<Image>();
        if (flyImage != null)
        {
            flyImage.sprite = data.icon;
            flyImage.raycastTarget = false;
        }

        LayoutElement flyLayout = flyIcon.GetComponent<LayoutElement>();
        if (flyLayout != null) flyLayout.ignoreLayout = true;

        RectTransform flyRect = flyIcon.GetComponent<RectTransform>();

        // 2. 開始位置
        Vector3 screenStart = mainCamera.WorldToScreenPoint(worldPosition);
        if (screenStart.z < 0)
        {
            Destroy(flyIcon);
            CreateOrUpdateSlot(data.type, data.icon);
            yield break;
        }

        Vector2 startLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenStart, canvasCamera, out startLocalPos);

        // 3. 終了位置
        Vector3 containerScreenPos = RectTransformUtility.WorldToScreenPoint(canvasCamera, iconContainer.position);
        
        // もし既にそのタイプのスロットがあれば、そこに向かって飛ぶ
        if (uiSlots.TryGetValue(data.type, out GameObject slotObj))
        {
            containerScreenPos = RectTransformUtility.WorldToScreenPoint(canvasCamera, slotObj.transform.position);
        }

        Vector2 endLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, containerScreenPos, canvasCamera, out endLocalPos);

        // 4. フライアニメーション
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

        // 5. フライアイコン削除
        Destroy(flyIcon);

        // 6. スロット更新
        CreateOrUpdateSlot(data.type, data.icon);
    }

    private void CreateOrUpdateSlot(ItemType type, Sprite icon, bool isInitial = false)
    {
        int count = GetItemCount(type);

        if (uiSlots.TryGetValue(type, out GameObject slotObj))
        {
            if (countTexts.TryGetValue(type, out TextMeshProUGUI text))
            {
                text.text = "x" + count;
            }
            if (!isInitial)
            {
                StartCoroutine(PopInAnimation(slotObj.transform));
            }
        }
        else
        {
            if (iconContainer == null || iconPrefab == null) return;

            slotObj = Instantiate(iconPrefab, iconContainer);
            Image slotImage = slotObj.GetComponent<Image>();
            if (slotImage != null && icon != null) slotImage.sprite = icon;

            LayoutElement layout = slotObj.GetComponent<LayoutElement>();
            if (layout == null) layout = slotObj.AddComponent<LayoutElement>();
            layout.preferredWidth = iconWidth;
            layout.preferredHeight = iconWidth;

            // 個数テキストを動的に追加
            GameObject textObj = new GameObject("CountText");
            textObj.transform.SetParent(slotObj.transform, false);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "x" + count;
            tmp.fontSize = countTextSize; // インスペクターから設定可能に
            tmp.alignment = TextAlignmentOptions.BottomRight;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            // インスペクターのオフセット値を適用
            textRect.offsetMin = new Vector2(0, countTextOffset.y);
            textRect.offsetMax = new Vector2(countTextOffset.x, 0);

            uiSlots[type] = slotObj;
            countTexts[type] = tmp;

            if (!isInitial)
            {
                StartCoroutine(PopInAnimation(slotObj.transform));
            }
        }
    }

    private IEnumerator PopInAnimation(Transform target)
    {
        float elapsed = 0f;
        target.localScale = Vector3.zero;

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

    // =====================================================================
    //  アイテム削除（ダメージによる喪失）
    // =====================================================================

    public int RemoveItemsFromEnd(int count)
    {
        int actualRemoved = 0;
        HashSet<ItemType> typesToAnimate = new HashSet<ItemType>();

        for (int i = 0; i < count; i++)
        {
            if (s_collectedItems.Count == 0) break;
            int lastIndex = s_collectedItems.Count - 1;
            ItemType type = s_collectedItems[lastIndex].type;
            
            s_collectedItems.RemoveAt(lastIndex);
            actualRemoved++;
            typesToAnimate.Add(type);
        }

        AnimateRemovals(typesToAnimate);
        return actualRemoved;
    }

    public int RemoveItemsRandom(int count)
    {
        int actualRemoved = 0;
        HashSet<ItemType> typesToAnimate = new HashSet<ItemType>();

        for (int i = 0; i < count; i++)
        {
            if (s_collectedItems.Count == 0) break;
            int randomIndex = Random.Range(0, s_collectedItems.Count);
            ItemType type = s_collectedItems[randomIndex].type;
            
            s_collectedItems.RemoveAt(randomIndex);
            actualRemoved++;
            typesToAnimate.Add(type);
        }

        AnimateRemovals(typesToAnimate);
        return actualRemoved;
    }

    public bool RemoveItemByType(ItemType type)
    {
        for (int i = 0; i < s_collectedItems.Count; i++)
        {
            if (s_collectedItems[i].type == type)
            {
                s_collectedItems.RemoveAt(i);
                
                HashSet<ItemType> types = new HashSet<ItemType> { type };
                AnimateRemovals(types);
                return true;
            }
        }
        return false;
    }

    public bool RemoveLastItem()
    {
        return RemoveItemsFromEnd(1) > 0;
    }

    private void AnimateRemovals(HashSet<ItemType> typesToAnimate)
    {
        foreach (var type in typesToAnimate)
        {
            int newCount = GetItemCount(type);
            if (countTexts.TryGetValue(type, out TextMeshProUGUI text))
            {
                text.text = "x" + newCount;
            }
            if (uiSlots.TryGetValue(type, out GameObject slotObj))
            {
                StartCoroutine(RemoveAnimation(slotObj));
            }
        }
    }

    private IEnumerator RemoveAnimation(GameObject target)
    {
        if (target == null) yield break;

        removingCount++;

        Image image = target.GetComponent<Image>();
        RectTransform rect = target.GetComponent<RectTransform>();
        Color startColor = image != null ? image.color : Color.white;

        float duration = removeDuration;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            if (target == null) yield break;
            float t = elapsed / duration;

            if (image != null)
            {
                // 赤フラッシュ
                float flashT = Mathf.PingPong(t * 3f, 1f);
                image.color = Color.Lerp(startColor, new Color(1f, 0.3f, 0.3f, 1f), flashT);
            }

            // 振動
            float shake = Mathf.Sin(t * Mathf.PI * 8f) * 10f * (1f - t);
            if (rect != null)
            {
                rect.localRotation = Quaternion.Euler(0, 0, shake);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (image != null) image.color = startColor;
        if (rect != null) rect.localRotation = Quaternion.identity;

        removingCount--;
    }

    // =====================================================================
    //  全クリア
    // =====================================================================

    public void ClearItems()
    {
        s_collectedItems.Clear();
        uiSlots.Clear();
        countTexts.Clear();
        
        if (iconContainer != null)
        {
            foreach (Transform child in iconContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // インスペクターで設定されたアイテムを x0 として最初から表示
        foreach (var setting in displaySettings)
        {
            CreateOrUpdateSlot(setting.type, setting.icon, true);
        }
    }

    // =====================================================================
    //  リザルト画面用 API
    // =====================================================================

    public static List<ItemData> GetCollectedItemsForResult()
    {
        return new List<ItemData>(s_collectedItems);
    }

    public static int GetTotalMoneyValue()
    {
        int total = 0;
        foreach (var item in s_collectedItems)
        {
            total += item.moneyValue;
        }
        return total;
    }

    public static void ClearCollectedData()
    {
        s_collectedItems.Clear();
    }

    // =====================================================================
    //  ユーティリティ
    // =====================================================================

    public int GetTotalItemCount()
    {
        return s_collectedItems.Count;
    }

    public int GetItemCount(ItemType type)
    {
        int count = 0;
        foreach (var item in s_collectedItems)
        {
            if (item.type == type) count++;
        }
        return count;
    }

    public bool IsRemoving()
    {
        return removingCount > 0;
    }
}
