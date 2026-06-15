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
/// 
/// 【使い方】
/// 1. 02_Mainシーンに空のGameObjectを作り、このスクリプトをアタッチ
/// 2. Canvasの直下に「ItemIconPanel」(HorizontalLayoutGroup) を右上に配置
/// 3. アイコンPrefab（Image + LayoutElement付きGameObject, 64x64推奨）を作成
/// 4. インスペクターで canvasRect, iconContainer, iconPrefab を設定
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

    // 取得済みアイテムの情報を保持する内部クラス
    private class CollectedItemData
    {
        public ItemType type;
        public Sprite icon;
        public GameObject uiObject; // iconContainer内のUI要素
    }

    // 取得済みアイテムリスト（UIオブジェクトと紐付け）
    private List<CollectedItemData> collectedItems = new List<CollectedItemData>();

    // カメラ参照
    private Camera mainCamera;
    private Camera canvasCamera; // Screen Space - Camera用（Overlayならnull）

    // 削除アニメーション中のアイテム数（同時削除の制御用）
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

        // CanvasのrenderModeに応じてカメラを取得
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
    /// ワールド座標からUIの右上パネルへ飛んでいくアニメーションを再生し、アイコンを追加する。
    /// </summary>
    public void AddItem(ItemType type, Sprite icon, Vector3 worldPosition)
    {
        StartCoroutine(FlyToUI(type, icon, worldPosition));
    }

    /// <summary>
    /// ワールド座標 → UIへのフライアニメーション
    /// </summary>
    private IEnumerator FlyToUI(ItemType type, Sprite icon, Vector3 worldPosition)
    {
        if (canvasRect == null || iconPrefab == null || mainCamera == null)
        {
            AddIconToPanel(type, icon);
            yield break;
        }

        // ========== 1. フライ用の一時アイコンを Canvas 直下に生成 ==========
        GameObject flyIcon = Instantiate(iconPrefab, canvasRect);
        Image flyImage = flyIcon.GetComponent<Image>();
        if (flyImage != null)
        {
            flyImage.sprite = icon;
            flyImage.raycastTarget = false;
        }

        // フライ中はLayoutElementを無効化（Canvas直下に置くため）
        LayoutElement flyLayout = flyIcon.GetComponent<LayoutElement>();
        if (flyLayout != null) flyLayout.ignoreLayout = true;

        RectTransform flyRect = flyIcon.GetComponent<RectTransform>();

        // ========== 2. 開始位置：ワールド座標 → Canvas内ローカル座標 ==========
        Vector3 screenStart = mainCamera.WorldToScreenPoint(worldPosition);
        if (screenStart.z < 0)
        {
            Destroy(flyIcon);
            AddIconToPanel(type, icon);
            yield break;
        }

        Vector2 startLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenStart, canvasCamera, out startLocalPos);

        // ========== 3. 終了位置：iconContainer の位置 → Canvas内ローカル座標 ==========
        Vector3 containerScreenPos = RectTransformUtility.WorldToScreenPoint(canvasCamera, iconContainer.position);
        Vector2 endLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, containerScreenPos, canvasCamera, out endLocalPos);

        // ========== 4. フライアニメーション ==========
        float elapsed = 0f;
        flyRect.anchoredPosition = startLocalPos;
        flyRect.localScale = Vector3.one * startScale;

        while (elapsed < flyDuration)
        {
            float t = elapsed / flyDuration;

            // EaseInOutQuad
            float easedT;
            if (t < 0.5f)
                easedT = 2f * t * t;
            else
                easedT = 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            // 放物線アーチ
            Vector2 currentPos = Vector2.Lerp(startLocalPos, endLocalPos, easedT);
            float arc = arcHeight * 4f * t * (1f - t);
            currentPos.y += arc;

            flyRect.anchoredPosition = currentPos;
            flyRect.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, easedT);

            // 終盤の軽いフェード
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

        // ========== 5. フライアイコン削除 ==========
        Destroy(flyIcon);

        // ========== 6. 正式アイコンをパネルに追加 ==========
        AddIconToPanel(type, icon);
    }

    /// <summary>
    /// アイコンをパネルに追加し、ポップインアニメーションを再生する
    /// </summary>
    private void AddIconToPanel(ItemType type, Sprite icon)
    {
        if (iconContainer == null || iconPrefab == null) return;

        GameObject slotIcon = Instantiate(iconPrefab, iconContainer);
        Image slotImage = slotIcon.GetComponent<Image>();
        if (slotImage != null)
        {
            slotImage.sprite = icon;
        }

        // LayoutElement を確保（なければ追加）→ 削除アニメ時に幅を操作する
        LayoutElement layout = slotIcon.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = slotIcon.AddComponent<LayoutElement>();
        }
        layout.preferredWidth = iconWidth;
        layout.preferredHeight = iconWidth;

        // データリストに追加
        collectedItems.Add(new CollectedItemData
        {
            type = type,
            icon = icon,
            uiObject = slotIcon
        });

        StartCoroutine(PopInAnimation(slotIcon.transform));
    }

    /// <summary>
    /// スケール0→1のポップインアニメーション（EaseOutBack）
    /// </summary>
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
    /// 末尾からN個のアイテムを削除する（爆弾ダメージなど）
    /// 縮小アニメーション後にHorizontalLayoutGroupが自動的に隙間を詰める
    /// </summary>
    /// <param name="count">削除するアイテム数</param>
    /// <returns>実際に削除されたアイテム数</returns>
    public int RemoveItemsFromEnd(int count)
    {
        int actualRemoved = 0;
        // 末尾から順に削除
        for (int i = 0; i < count; i++)
        {
            if (collectedItems.Count == 0) break;

            int lastIndex = collectedItems.Count - 1;
            RemoveItemAtIndex(lastIndex);
            actualRemoved++;
        }
        return actualRemoved;
    }

    /// <summary>
    /// ランダムな位置のアイテムをN個削除する（列の途中が消えて詰め直し）
    /// </summary>
    /// <param name="count">削除するアイテム数</param>
    /// <returns>実際に削除されたアイテム数</returns>
    public int RemoveItemsRandom(int count)
    {
        int actualRemoved = 0;
        for (int i = 0; i < count; i++)
        {
            if (collectedItems.Count == 0) break;

            int randomIndex = Random.Range(0, collectedItems.Count);
            RemoveItemAtIndex(randomIndex);
            actualRemoved++;
        }
        return actualRemoved;
    }

    /// <summary>
    /// 指定タイプのアイテムを1つ削除する（最初に見つかったもの）
    /// </summary>
    /// <returns>削除できたかどうか</returns>
    public bool RemoveItemByType(ItemType type)
    {
        for (int i = 0; i < collectedItems.Count; i++)
        {
            if (collectedItems[i].type == type)
            {
                RemoveItemAtIndex(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 最後のアイテムを1つ削除する
    /// </summary>
    /// <returns>削除できたかどうか</returns>
    public bool RemoveLastItem()
    {
        if (collectedItems.Count == 0) return false;
        RemoveItemAtIndex(collectedItems.Count - 1);
        return true;
    }

    /// <summary>
    /// 指定インデックスのアイテムを削除（縮小アニメーション付き）
    /// リストからは即座に除外し、UIは非同期で消える
    /// </summary>
    private void RemoveItemAtIndex(int index)
    {
        if (index < 0 || index >= collectedItems.Count) return;

        CollectedItemData itemData = collectedItems[index];
        collectedItems.RemoveAt(index);

        if (itemData.uiObject != null)
        {
            // アニメーション付きで削除
            StartCoroutine(RemoveAnimation(itemData.uiObject));
        }
    }

    /// <summary>
    /// 削除アニメーション：
    /// 1. スケール縮小 + 透明度フェードアウト
    /// 2. LayoutElementの幅を0に縮めることで、HorizontalLayoutGroupが隙間を滑らかに詰める
    /// 3. 完了後にGameObject破棄
    /// </summary>
    private IEnumerator RemoveAnimation(GameObject target)
    {
        if (target == null) yield break;

        removingCount++;

        RectTransform rect = target.GetComponent<RectTransform>();
        Image image = target.GetComponent<Image>();
        LayoutElement layout = target.GetComponent<LayoutElement>();

        // LayoutElementがない場合は追加
        if (layout == null)
        {
            layout = target.AddComponent<LayoutElement>();
            layout.preferredWidth = iconWidth;
            layout.preferredHeight = iconWidth;
        }

        float elapsed = 0f;
        float startWidth = layout.preferredWidth;
        float startHeight = layout.preferredHeight;
        Color startColor = image != null ? image.color : Color.white;

        // ---- フェーズ1: 赤く光って縮小（フィードバック感） ----
        float phase1Duration = removeDuration * 0.4f;
        while (elapsed < phase1Duration)
        {
            if (target == null) yield break;

            float t = elapsed / phase1Duration;

            // 赤フラッシュ → フェードアウト
            if (image != null)
            {
                Color c = Color.Lerp(startColor, new Color(1f, 0.3f, 0.3f, 1f), t);
                image.color = c;
            }

            // 少し揺れる
            float shake = Mathf.Sin(t * Mathf.PI * 6f) * 3f * (1f - t);
            if (rect != null)
            {
                rect.localRotation = Quaternion.Euler(0, 0, shake);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // ---- フェーズ2: 縮小 + 幅を0にして隙間を詰める ----
        elapsed = 0f;
        float phase2Duration = removeDuration * 0.6f;
        while (elapsed < phase2Duration)
        {
            if (target == null) yield break;

            float t = elapsed / phase2Duration;

            // EaseInQuad で加速的に縮む
            float easedT = t * t;

            // スケール縮小
            float scale = Mathf.Lerp(1f, 0f, easedT);
            target.transform.localScale = Vector3.one * scale;

            // 幅を縮める → HorizontalLayoutGroupが自動で隣を詰める
            if (layout != null)
            {
                layout.preferredWidth = Mathf.Lerp(startWidth, 0f, easedT);
            }

            // フェードアウト
            if (image != null)
            {
                Color c = image.color;
                c.a = 1f - easedT;
                image.color = c;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // ---- 完了: GameObject を破棄 ----
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
        collectedItems.Clear();
        if (iconContainer == null) return;

        foreach (Transform child in iconContainer)
        {
            Destroy(child.gameObject);
        }
    }

    // =====================================================================
    //  ユーティリティ
    // =====================================================================

    /// <summary>
    /// 取得済みアイテムの種類リストのコピーを返す
    /// </summary>
    public List<ItemType> GetCollectedItemTypes()
    {
        List<ItemType> types = new List<ItemType>();
        foreach (var item in collectedItems)
        {
            types.Add(item.type);
        }
        return types;
    }

    /// <summary>
    /// 指定タイプのアイテム取得数を返す
    /// </summary>
    public int GetItemCount(ItemType type)
    {
        int count = 0;
        foreach (var item in collectedItems)
        {
            if (item.type == type) count++;
        }
        return count;
    }

    /// <summary>
    /// 全アイテムの取得数を返す
    /// </summary>
    public int GetTotalItemCount()
    {
        return collectedItems.Count;
    }

    /// <summary>
    /// 削除アニメーション実行中かどうか
    /// </summary>
    public bool IsRemoving()
    {
        return removingCount > 0;
    }
}
