using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class ResultManager : MonoBehaviour
{
    /// <summary>
    /// ドロップアイテムの定義（見た目・金額・出現確率）
    /// </summary>
    [System.Serializable]
    public class LootItem
    {
        public Sprite sprite;                              // アイコン画像
        public int moneyValue;                             // 金額
        [Range(0, 100)] public int treasureBoxChance;      // 宝箱での出現確率（%）
        [Range(0, 100)] public int leatherBagChance;       // 皮袋での出現確率（%）
    }

    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI onHandResultText;

    [Header("取得アイテム表示")]
    [SerializeField, Tooltip("アイテムアイコンを並べるパネル（HorizontalLayoutGroup推奨）")]
    private Transform itemIconContainer;
    [SerializeField, Tooltip("アイコン用UIプレハブ（メインシーンと同じItemIcon.prefab）")]
    private GameObject itemIconPrefab;
    [SerializeField, Tooltip("アイコンを1つずつ表示する間隔（秒）")]
    private float iconInterval = 0.15f;
    [SerializeField, Tooltip("アイコンサイズ")]
    private float iconSize = 64f;

    [Header("宝箱・皮袋の設定")]
    [SerializeField, Tooltip("宝箱を開けた後の画像")]
    private Sprite openTreasureBoxSprite;
    [SerializeField, Tooltip("皮袋を開けた後の画像")]
    private Sprite openLeatherBagSprite;

    [Header("ドロップアイテム設定")]
    [SerializeField, Tooltip("宝石")]
    private LootItem lootJewel;
    [SerializeField, Tooltip("たくさんの宝石")]
    private LootItem lootManyJewels;
    [SerializeField, Tooltip("札")]
    private LootItem lootBill;
    [SerializeField, Tooltip("札束")]
    private LootItem lootBillBundle;

    [Header("フォーカス設定")]
    [SerializeField, Tooltip("すべて開け終わった後にフォーカスを移すボタン（次へボタンなど）")]
    private GameObject nextFocusWhenAllOpened;

    [Header("ボタン色設定")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.7f, 1f);
    [SerializeField] private Color pressedColor = new Color(1f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 1f, 0.5f, 1f);

    [Header("演出設定")]

    private bool isAnimationFinished = false;
    private bool skipRequested = false;
    private int openedCount = 0;

    // 生成したボタンのリスト（ナビゲーション設定用）
    private List<Button> itemButtons = new List<Button>();
    
    // 開封済み（1段階目）の宝箱・皮袋のインデックスを保持
    private HashSet<int> openedChestIndices = new HashSet<int>();

    // 各宝箱・皮袋のドロップ結果を保持（index → 抽選結果）
    private Dictionary<int, LootItem> droppedLootMap = new Dictionary<int, LootItem>();

    // ドロップアイテムとして生成されたアイコンを保持（消滅用）
    private Dictionary<int, GameObject> droppedIconMap = new Dictionary<int, GameObject>();

    void Start()
    {
        // 取得アイテムをUIに並べる
        StartCoroutine(ShowCollectedItems());
    }

    void Update()
    {
        bool wasPressed = false;

        // キーボードの何かのキーが押されたか
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) wasPressed = true;

        // ゲームパッド操作
        if (Gamepad.current != null)
        {
            if (Gamepad.current.startButton.wasPressedThisFrame) wasPressed = true;
            if (Gamepad.current.buttonEast.wasPressedThisFrame) wasPressed = true; // Bボタン/○ボタン
        }

        if (wasPressed)
        {
            // アイコンが並ぶアニメーションのスキップのみキー入力を受け付ける
            if (!isAnimationFinished)
            {
                skipRequested = true;
            }
        }
    }

    /// <summary>
    /// 次のシーンへ遷移するUIボタンの OnClick 等に登録して使用するリセット処理
    /// </summary>
    public void ResetCollectedData()
    {
        // 1. まず、まだクリックされていない（開封されていない）アイテムを全て自動で換金する
        MoneyManager mm = MoneyManager.Instance;
        List<ItemInventoryManager.ItemData> items = ItemInventoryManager.GetCollectedItemsForResult();

        if (mm != null && items.Count == itemButtons.Count)
        {
            int autoAddedMoney = 0;
            for (int i = 0; i < itemButtons.Count; i++)
            {
                if (itemButtons[i] != null && itemButtons[i].interactable)
                {
                    autoAddedMoney += items[i].moneyValue;
                    itemButtons[i].interactable = false; // 二重加算防止
                }
            }

            if (autoAddedMoney > 0)
            {
                mm.MoneyOnHandIncrease(autoAddedMoney);
                Debug.Log($"[ResultManager] 残っていた未回収のアイテムを自動換金しました: +{autoAddedMoney}");
            }
        }

        // 2. アイテムデータをクリア
        ItemInventoryManager.ClearCollectedData();
    }

    /// <summary>
    /// 取得アイテムを1つずつアニメーション付きで並べた後、カウントアップに進む
    /// </summary>
    private IEnumerator ShowCollectedItems()
    {
        List<ItemInventoryManager.ItemData> items = ItemInventoryManager.GetCollectedItemsForResult();

        if (itemIconContainer != null && itemIconPrefab != null && items.Count > 0)
        {
            for (int i = 0; i < items.Count; i++)
            {
                // スキップされたら残り全部を一気に表示
                if (skipRequested)
                {
                    for (int j = i; j < items.Count; j++)
                    {
                        CreateIcon(items[j], j);
                    }
                    break;
                }

                CreateIcon(items[i], i);
                yield return new WaitForSeconds(iconInterval);
            }

            // 全アイテム表示後、最初のボタンを選択状態にする（ゲームパッド用）
            SelectFirstButton();
        }

        // アイテム表示が終わったら、現在の所持金を表示して操作可能にする
        yield return new WaitForSeconds(0.3f);
        skipRequested = false;

        MoneyManager mm = MoneyManager.Instance;
        if (mm != null)
        {
            onHandResultText.text = mm.GetMoneyOnHand().ToString("N0");
        }
        
        isAnimationFinished = true;
    }

    /// <summary>
    /// アイコンをButtonとして生成し、ポップインアニメーション
    /// </summary>
    private void CreateIcon(ItemInventoryManager.ItemData item, int index)
    {
        if (item.icon == null) return;

        GameObject iconObj = Instantiate(itemIconPrefab, itemIconContainer);
        Image image = iconObj.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = item.icon;
        }

        // RectTransformのサイズを明示的に設定
        RectTransform rect = iconObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(iconSize, iconSize);
        }

        LayoutElement layout = iconObj.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = iconObj.AddComponent<LayoutElement>();
        }
        layout.preferredWidth = iconSize;
        layout.preferredHeight = iconSize;

        // ========== Button コンポーネントを追加 ==========
        Button button = iconObj.GetComponent<Button>();
        if (button == null)
        {
            button = iconObj.AddComponent<Button>();
        }

        // ボタンの色遷移を設定
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = highlightColor;
        colors.pressedColor = pressedColor;
        colors.selectedColor = selectedColor;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        // Image を targetGraphic に設定（色遷移の対象）
        button.targetGraphic = image;

        // 決定時のコールバックを登録
        int capturedIndex = index; // クロージャ用にキャプチャ
        button.onClick.AddListener(() => OnItemButtonClicked(capturedIndex));

        itemButtons.Add(button);

        // ゲームパッド用：左右ナビゲーションを手動で設定
        UpdateButtonNavigation();

        StartCoroutine(PopInAnimation(iconObj.transform));
    }

    /// <summary>
    /// ボタン間の左右ナビゲーションを設定（ゲームパッド対応）
    /// </summary>
    private void UpdateButtonNavigation()
    {
        for (int i = 0; i < itemButtons.Count; i++)
        {
            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.Automatic;
            itemButtons[i].navigation = nav;
        }
    }

    /// <summary>
    /// 最初のボタンを選択状態にする（ゲームパッド用）
    /// </summary>
    private void SelectFirstButton()
    {
        if (itemButtons.Count > 0 && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(itemButtons[0].gameObject);
        }
    }

    /// <summary>
    /// アイテムボタンが決定された時のコールバック
    /// </summary>
    private void OnItemButtonClicked(int index)
    {
        // 既に無効化されている場合は無視
        if (index < 0 || index >= itemButtons.Count || !itemButtons[index].interactable) return;

        List<ItemInventoryManager.ItemData> items = ItemInventoryManager.GetCollectedItemsForResult();
        if (index >= items.Count) return;

        ItemInventoryManager.ItemData item = items[index];
        Debug.Log($"[ResultManager] アイテム {index} が選択されました: {item.type}, 金額: {item.moneyValue}");

        Button btn = itemButtons[index];

        // 宝箱か皮袋の場合
        if (item.type == ItemType.TresureBox || item.type == ItemType.LeatherBag)
        {
            if (!openedChestIndices.Contains(index))
            {
                // ========== 1回目のクリック：開ける（無効化しない） ==========
                openedChestIndices.Add(index);

                // 画像を開いた状態に差し替え
                Image img = btn.GetComponent<Image>();
                if (img != null)
                {
                    if (item.type == ItemType.TresureBox && openTreasureBoxSprite != null)
                        img.sprite = openTreasureBoxSprite;
                    else if (item.type == ItemType.LeatherBag && openLeatherBagSprite != null)
                        img.sprite = openLeatherBagSprite;

                    // ボタンは有効なままなので、Imageの色を直接暗くする
                    img.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }

                // ランダムに中身を抽選
                LootItem loot = RollLoot(item.type);
                droppedLootMap[index] = loot;

                // 中身のアイコンをボタンの子として生成
                if (loot != null && loot.sprite != null)
                {
                    GameObject lootIcon = new GameObject("LootIcon");
                    lootIcon.transform.SetParent(btn.transform, false);

                    Image lootImg = lootIcon.AddComponent<Image>();
                    lootImg.sprite = loot.sprite;
                    lootImg.raycastTarget = false; // ボタンのクリックを邪魔しない

                    RectTransform lootRect = lootIcon.GetComponent<RectTransform>();
                    lootRect.anchorMin = Vector2.zero;
                    lootRect.anchorMax = Vector2.one;
                    lootRect.offsetMin = Vector2.zero;
                    lootRect.offsetMax = Vector2.zero;

                    droppedIconMap[index] = lootIcon;

                    // ポップイン演出
                    StartCoroutine(PopInAnimation(lootIcon.transform));
                }

            }
            else
            {
                // ========== 2回目のクリック：中身を入手して消滅させる ==========
                int lootMoney = 0;
                if (droppedLootMap.TryGetValue(index, out LootItem droppedLoot))
                {
                    lootMoney = droppedLoot.moneyValue;
                }
                AddMoneyAndDestroyButton(lootMoney, index, btn);
            }
        }
        else
        {
            // 宝石など（1回で入手して消滅）
            AddMoneyAndDestroyButton(item.moneyValue, index, btn);
        }
    }

    /// <summary>
    /// お金を追加し、ボタンを無効化して消滅させる共通処理
    /// </summary>
    private void AddMoneyAndDestroyButton(int moneyValue, int index, Button btn)
    {
        // 1. お金を追加
        MoneyManager mm = MoneyManager.Instance;
        if (mm != null)
        {
            mm.MoneyOnHandIncrease(moneyValue);
            onHandResultText.text = mm.GetMoneyOnHand().ToString("N0");
        }

        openedCount++; // 回収済みの数を増やす

        // 2. ボタンを無効化して消滅させる
        btn.interactable = false; // 連打防止
        SelectNextAvailableButton(index);
        StartCoroutine(ShrinkAndDestroy(btn.gameObject));
    }

    /// <summary>
    /// 次に操作可能なアイテムボタンを探してフォーカスを移す
    /// </summary>
    private void SelectNextAvailableButton(int currentIndex)
    {
        if (EventSystem.current == null) return;

        // まずは自分の後にある有効なボタンを探す
        for (int i = currentIndex + 1; i < itemButtons.Count; i++)
        {
            if (itemButtons[i] != null && itemButtons[i].interactable)
            {
                EventSystem.current.SetSelectedGameObject(itemButtons[i].gameObject);
                return;
            }
        }

        // 後ろに無ければ、自分より前にある有効なボタンを探す
        for (int i = currentIndex - 1; i >= 0; i--)
        {
            if (itemButtons[i] != null && itemButtons[i].interactable)
            {
                EventSystem.current.SetSelectedGameObject(itemButtons[i].gameObject);
                return;
            }
        }
        
        // 有効なボタンが一つも残っていない場合
        if (nextFocusWhenAllOpened != null)
        {
            EventSystem.current.SetSelectedGameObject(nextFocusWhenAllOpened);
        }
    }

    /// <summary>
    /// ボタンを縮小させてから破棄するアニメーション
    /// </summary>
    private IEnumerator ShrinkAndDestroy(GameObject target)
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 startScale = target.transform.localScale;

        while (elapsed < duration)
        {
            if (target == null) yield break;
            float t = elapsed / duration;
            target.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t); // easeIn
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (target != null)
        {
            Destroy(target);
        }
    }

    /// <summary>
    /// ポップインアニメーション（EaseOutBack）
    /// </summary>
    private IEnumerator PopInAnimation(Transform target)
    {
        float duration = 0.2f;
        float elapsed = 0f;
        target.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float overshoot = 1.7f;
            float easedT = 1f + (overshoot + 1f) * Mathf.Pow(t - 1f, 3f)
                              + overshoot * Mathf.Pow(t - 1f, 2f);
            target.localScale = Vector3.one * easedT;
            elapsed += Time.deltaTime;
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    /// <summary>
    /// 宝箱・皮袋の種類に応じて重み付きランダムで中身を抽選する
    /// </summary>
    private LootItem RollLoot(ItemType containerType)
    {
        LootItem[] allLoot = { lootJewel, lootManyJewels, lootBill, lootBillBundle };

        // 各アイテムの確率を取得し、重み付き抽選を行う
        int totalWeight = 0;
        List<(LootItem item, int weight)> weightedList = new List<(LootItem, int)>();

        foreach (var loot in allLoot)
        {
            if (loot == null || loot.sprite == null) continue;

            int chance = (containerType == ItemType.TresureBox)
                ? loot.treasureBoxChance
                : loot.leatherBagChance;

            if (chance <= 0) continue;

            weightedList.Add((loot, chance));
            totalWeight += chance;
        }

        if (totalWeight == 0 || weightedList.Count == 0) return null;

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var (item, weight) in weightedList)
        {
            cumulative += weight;
            if (roll < cumulative) return item;
        }

        return weightedList[weightedList.Count - 1].item;
    }
}
