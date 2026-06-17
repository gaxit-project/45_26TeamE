using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アイテム取得時のUIフライアニメーション＆右上アイコン表示を管理するマネージャー
/// 
/// 【機能】
/// ・アイテム取得 → ワールド座標からUIへフライアニメーション → アイコン定着
/// ・ダメージ時のアイテム喪失 → 縮小アニメ → 隙間を滑らかに詰めて並び直し
/// ・リザルト画面用に取得アイテム＆金額データをシーン間で引き継ぐ
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
    [SerializeField, Tooltip("飛行時間（秒）")]
    private float flyDuration = 0.6f;

    [SerializeField, Tooltip("放物線の高さ（ピクセル）")]
    private float arcHeight = 150f;

    [SerializeField, Tooltip("開始時のスケール倍率")]
    private float startScale = 1.5f;

    [Header("ポップインアニメーション設定")]
    [SerializeField, Tooltip("ポップイン時間（秒）")]
    private float popDuration = 0.25f;

    [Header("削除アニメーション設定")]
    [SerializeField, Tooltip("削除アニメ時間（秒）")]
    private float removeDuration = 0.3f;

    [Header("アイコンサイズ")]
    [SerializeField, Tooltip("アイコンの幅（LayoutElement.preferredWidth）")]
    private float iconWidth = 64f;

    // ========== アイテムデータ（シーン間引き継ぎ用にstaticリスト） ==========

    /// <summary>
    /// リザルト画面に引き継ぐアイテムデータ
    /// </summary>
    [System.Serializable]
    public class ItemData
    {
        public ItemType type;
        public Sprite icon;
        public int moneyValue;
    }

    // staticリストでシーン遷移後もデータが残る
    private static List<ItemData> s_collectedItems = new List<ItemData>();

    // UI要素との紐付け（現在のシーン内でのみ有効）
    private class LiveItemData
    {
        public int dataIndex; // s_collectedItems 内のインデックス
        public GameObject uiObject;
    }
    private List<LiveItemData> liveItems = new List<LiveItemData>();

    // カメラ参照
    private Camera mainCamera;
    private Camera canvasCamera;

    // 削除アニメーション中のカウント
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

    /// <summary>
    /// アイテムを取得した時に呼び出す。
    /// お金はここでは加算せず、データとして保持する。リザルトで開封時に加算。
    /// </summary>
    /// <param name="type">アイテムの種類</param>
    /// <param name="icon">UI表示用のSprite</param>
    /// <param name="worldPosition">アイテムの3Dワールド座標（取得時の位置）</param>
    /// <param name="moneyValue">リザルトで開封時に加算される金額</param>
    public void AddItem(ItemType type, Sprite icon, Vector3 worldPosition, int moneyValue = 0)
    {
        // データをstaticリストに保存
        ItemData data = new ItemData
        {
            type = type,
            icon = icon,
            moneyValue = moneyValue
        };
        s_collectedItems.Add(data);

        StartCoroutine(FlyToUI(data, worldPosition));
    }

    /// <summary>
    /// ワールド座標 → UIへのフライアニメーション
    /// </summary>
    private IEnumerator FlyToUI(ItemData data, Vector3 worldPosition)
    {
        if (canvasRect == null || iconPrefab == null || mainCamera == null)
        {
            AddIconToPanel(data);
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
            AddIconToPanel(data);
            yield break;
        }

        Vector2 startLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenStart, canvasCamera, out startLocalPos);

        // 3. 終了位置
        Vector3 containerScreenPos = RectTransformUtility.WorldToScreenPoint(canvasCamera, iconContainer.position);
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

        // 6. 正式アイコンをパネルに追加
        AddIconToPanel(data);
    }

    /// <summary>
    /// アイコンをパネルに追加
    /// </summary>
    private void AddIconToPanel(ItemData data)
    {
        if (iconContainer == null || iconPrefab == null) return;

        GameObject slotIcon = Instantiate(iconPrefab, iconContainer);
        Image slotImage = slotIcon.GetComponent<Image>();
        if (slotImage != null)
        {
            slotImage.sprite = data.icon;
        }

        LayoutElement layout = slotIcon.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = slotIcon.AddComponent<LayoutElement>();
        }
        layout.preferredWidth = iconWidth;
        layout.preferredHeight = iconWidth;

        // UIオブジェクトとデータを紐付け
        int dataIndex = s_collectedItems.IndexOf(data);
        liveItems.Add(new LiveItemData
        {
            dataIndex = dataIndex,
            uiObject = slotIcon
        });

        StartCoroutine(PopInAnimation(slotIcon.transform));
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

    /// <summary>
    /// 末尾からN個のアイテムを削除する
    /// </summary>
    public int RemoveItemsFromEnd(int count)
    {
        int actualRemoved = 0;
        for (int i = 0; i < count; i++)
        {
            if (liveItems.Count == 0) break;
            RemoveItemAtLiveIndex(liveItems.Count - 1);
            actualRemoved++;
        }
        return actualRemoved;
    }

    /// <summary>
    /// ランダムな位置のアイテムをN個削除する
    /// </summary>
    public int RemoveItemsRandom(int count)
    {
        int actualRemoved = 0;
        for (int i = 0; i < count; i++)
        {
            if (liveItems.Count == 0) break;
            int randomIndex = Random.Range(0, liveItems.Count);
            RemoveItemAtLiveIndex(randomIndex);
            actualRemoved++;
        }
        return actualRemoved;
    }

    /// <summary>
    /// 指定タイプのアイテムを1つ削除する
    /// </summary>
    public bool RemoveItemByType(ItemType type)
    {
        for (int i = 0; i < liveItems.Count; i++)
        {
            int dataIdx = liveItems[i].dataIndex;
            if (dataIdx >= 0 && dataIdx < s_collectedItems.Count && s_collectedItems[dataIdx].type == type)
            {
                RemoveItemAtLiveIndex(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 最後のアイテムを1つ削除する
    /// </summary>
    public bool RemoveLastItem()
    {
        if (liveItems.Count == 0) return false;
        RemoveItemAtLiveIndex(liveItems.Count - 1);
        return true;
    }

    private void RemoveItemAtLiveIndex(int liveIndex)
    {
        if (liveIndex < 0 || liveIndex >= liveItems.Count) return;

        LiveItemData live = liveItems[liveIndex];

        // staticリストからも削除
        if (live.dataIndex >= 0 && live.dataIndex < s_collectedItems.Count)
        {
            s_collectedItems.RemoveAt(live.dataIndex);

            // インデックスを再計算（削除した分だけ後続のインデックスがずれる）
            for (int i = 0; i < liveItems.Count; i++)
            {
                if (liveItems[i].dataIndex > live.dataIndex)
                {
                    liveItems[i].dataIndex--;
                }
            }
        }

        liveItems.RemoveAt(liveIndex);

        if (live.uiObject != null)
        {
            StartCoroutine(RemoveAnimation(live.uiObject));
        }
    }

    private IEnumerator RemoveAnimation(GameObject target)
    {
        if (target == null) yield break;

        removingCount++;

        RectTransform rect = target.GetComponent<RectTransform>();
        Image image = target.GetComponent<Image>();
        LayoutElement layout = target.GetComponent<LayoutElement>();

        if (layout == null)
        {
            layout = target.AddComponent<LayoutElement>();
            layout.preferredWidth = iconWidth;
            layout.preferredHeight = iconWidth;
        }

        float elapsed = 0f;
        float startWidth = layout.preferredWidth;
        Color startColor = image != null ? image.color : Color.white;

        // フェーズ1: 赤フラッシュ + 振動
        float phase1Duration = removeDuration * 0.4f;
        while (elapsed < phase1Duration)
        {
            if (target == null) yield break;
            float t = elapsed / phase1Duration;

            if (image != null)
            {
                Color c = Color.Lerp(startColor, new Color(1f, 0.3f, 0.3f, 1f), t);
                image.color = c;
            }

            float shake = Mathf.Sin(t * Mathf.PI * 6f) * 3f * (1f - t);
            if (rect != null)
            {
                rect.localRotation = Quaternion.Euler(0, 0, shake);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // フェーズ2: 縮小 + 幅→0
        elapsed = 0f;
        float phase2Duration = removeDuration * 0.6f;
        while (elapsed < phase2Duration)
        {
            if (target == null) yield break;
            float t = elapsed / phase2Duration;
            float easedT = t * t;

            float scale = Mathf.Lerp(1f, 0f, easedT);
            target.transform.localScale = Vector3.one * scale;

            if (layout != null)
            {
                layout.preferredWidth = Mathf.Lerp(startWidth, 0f, easedT);
            }

            if (image != null)
            {
                Color c = image.color;
                c.a = 1f - easedT;
                image.color = c;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        removingCount--;
        Destroy(target);
    }

    // =====================================================================
    //  全クリア
    // =====================================================================

    /// <summary>
    /// 取得済みアイテムをすべてクリアする（ステージリセット時など）
    /// </summary>
    public void ClearItems()
    {
        s_collectedItems.Clear();
        liveItems.Clear();
        if (iconContainer == null) return;

        foreach (Transform child in iconContainer)
        {
            Destroy(child.gameObject);
        }
    }

    // =====================================================================
    //  リザルト画面用 API
    // =====================================================================

    /// <summary>
    /// リザルト画面用：取得済みアイテムのリストを返す（staticなので別シーンからでも参照可能）
    /// </summary>
    public static List<ItemData> GetCollectedItemsForResult()
    {
        return new List<ItemData>(s_collectedItems);
    }

    /// <summary>
    /// リザルト画面用：全アイテムの合計金額を返す
    /// </summary>
    public static int GetTotalMoneyValue()
    {
        int total = 0;
        foreach (var item in s_collectedItems)
        {
            total += item.moneyValue;
        }
        return total;
    }

    /// <summary>
    /// リザルト処理完了後にデータをクリアする（次のステージ用）
    /// </summary>
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
