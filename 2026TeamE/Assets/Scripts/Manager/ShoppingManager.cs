using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShoppingManager : MonoBehaviour
{
    [System.Serializable]
    public class ShopItem
    {
        public string itemName;           // 識別用（保存キーにも使います）
        public Button buyButton;
        public TextMeshProUGUI levelText;
        public int price = 100;

        [HideInInspector] public int currentLevel = 0;
    }

    [Header("ショップ設定")]
    [SerializeField] private List<ShopItem> shopItems = new List<ShopItem>();

    void Start()
    {
        foreach (var item in shopItems)
        {
            // 1. 保存されているレベルを読み込む（なければ0）
            item.currentLevel = PlayerPrefs.GetInt("Level_" + item.itemName, 0);

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

            // 2. レベルアップして保存
            item.currentLevel++;
            PlayerPrefs.SetInt("Level_" + item.itemName, item.currentLevel);
            PlayerPrefs.Save(); // 念のため即時保存

            RefreshUI(item);
            Debug.Log($"{item.itemName} を購入！ 現在Lv: {item.currentLevel}");
        }
        else
        {
            Debug.Log("所持金が不足しています");
        }
    }

    private void RefreshUI(ShopItem item)
    {
        if (item.levelText != null)
        {
            item.levelText.text = $"{item.currentLevel}";
        }
    }
}