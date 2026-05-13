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

    // --- ShoppingManager.cs の Start内を変更 ---
    void Start()
    {
        foreach (var item in shopItems)
        {
            // 修正点：一元管理クラスからレベルをロード
            item.currentLevel = UpgradeManager.GetLevel(item.itemName);

            ShopItem target = item;
            target.buyButton.onClick.AddListener(() => TryPurchase(target));

            RefreshUI(target);
        }
    }

    // --- ShoppingManager.cs の TryPurchase内を変更 ---
    private void TryPurchase(ShopItem item)
    {
        if (item.currentLevel >= item.maxLevel) return;

        MoneyManager mm = MoneyManager.Instance;
        if (mm == null) return;

        int cost = item.CurrentPrice;

        if (mm.GetMoney() >= cost)
        {
            mm.SpendMoney(cost);

            // 修正点：一元管理クラスを通じてレベルアップとセーブを実行
            UpgradeManager.IncreaseLevel(item.itemName);
            item.currentLevel = UpgradeManager.GetLevel(item.itemName);

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
