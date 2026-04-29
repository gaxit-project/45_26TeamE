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
        public int price = 100;
        // [HideInInspector] public int currentLevel = 0; // 変数は使わずManagerから都度取る
    }

    [Header("ショップ設定")]
    [SerializeField] private List<ShopItem> shopItems = new List<ShopItem>();

    void Start()
    {
        foreach (var item in shopItems)
        {
            ShopItem target = item;
            target.buyButton.onClick.AddListener(() => TryPurchase(target));
            RefreshUI(target);
        }
    }

    private void TryPurchase(ShopItem item)
    {
        MoneyManager mm = MoneyManager.Instance;
        if (mm == null) return;

        if (mm.GetMoney() >= item.price)
        {
            mm.SpendMoney(item.price);

            // MoneyManagerにレベルを保存（アプリ終了で消える）
            int nextLevel = mm.GetItemLevel(item.itemName) + 1;
            mm.SetItemLevel(item.itemName, nextLevel);

            if (item.itemName == "Drill")
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    player.SetDrillLevel(nextLevel + 1);
                }
            }

            RefreshUI(item);
        }
    }

    private void RefreshUI(ShopItem item)
    {
        if (item.levelText != null && MoneyManager.Instance != null)
        {
            // Managerから現在のレベルを取って表示
            int lv = MoneyManager.Instance.GetItemLevel(item.itemName);
            item.levelText.text = lv.ToString();
        }
    }
}
