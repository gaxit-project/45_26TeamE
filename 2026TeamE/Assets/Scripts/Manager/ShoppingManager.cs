using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShoppingManager : MonoBehaviour
{
    [System.Serializable]
    public class ShopItem
    {
        public string itemName;
        public Button buyButton;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI priceText;
        public int basePrice = 100;
        public int maxLevel = 10;

        [HideInInspector] public int currentLevel = 1;

        public int CurrentPrice
        {
            get
            {
                return Mathf.FloorToInt(basePrice * Mathf.Pow(1.5f, currentLevel - 1));
            }
        }
    }

    [Header("ショップ設定")]
    [SerializeField] private List<ShopItem> shopItems = new List<ShopItem>();

    private static Dictionary<string, int> savedLevels = new Dictionary<string, int>();

    void Start()
    {
        foreach (var item in shopItems)
        {
            if (savedLevels.ContainsKey(item.itemName))
            {
                item.currentLevel = savedLevels[item.itemName];
            }
            else
            {
                item.currentLevel = 1;
            }

            ShopItem target = item;
            target.buyButton.onClick.AddListener(() => TryPurchase(target));

            RefreshUI(target);
        }
    }

    private void TryPurchase(ShopItem item)
    {
        // ★重要：上限に達していたら、ボタンは「押せる状態」でも処理を中断する
        if (item.currentLevel >= item.maxLevel) return;

        MoneyManager mm = MoneyManager.Instance;
        if (mm == null) return;

        int cost = item.CurrentPrice;

        if (mm.GetMoney() >= cost)
        {
            mm.SpendMoney(cost);
            item.currentLevel++;
            savedLevels[item.itemName] = item.currentLevel;

            if (item.itemName == "Drill")
            {
                PlayerController player = FindAnyObjectByType<PlayerController>();
                if (player != null) player.SetDrillLevel(item.currentLevel);
            }

            RefreshUI(item);
        }
    }

    private void RefreshUI(ShopItem item)
    {
        bool isMax = item.currentLevel >= item.maxLevel;

        if (item.levelText != null)
            item.levelText.text = $"{item.currentLevel}";

        if (item.priceText != null)
            item.priceText.text = isMax ? "---" : $"$:{item.CurrentPrice}";

        if (item.buyButton != null)
        {
            item.buyButton.interactable = true;

            //ここはあとから色とかサイズとか変える（見た目自体も変えるかも）

            // 見た目（色）を変
            ColorBlock cb = item.buyButton.colors;
            if (isMax)
            {
                // 上限に達した時の色
                cb.normalColor = Color.gray;
                cb.highlightedColor = Color.gray; // 選択中の色もグレーに固定
            }
            else
            {
                // 通常時の色（元の色に戻す）
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
            }
            item.buyButton.colors = cb;
        }
    }

}
