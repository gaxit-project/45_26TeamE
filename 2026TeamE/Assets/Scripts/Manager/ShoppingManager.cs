using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

        [TextArea(2, 4)]
        public List<string> levelDescriptions;

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

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("エフェクト")]
    [SerializeField] private GameObject jewelSparkEffect;
    [SerializeField] private GameObject player;

    private static Dictionary<string, int> savedLevels = new Dictionary<string, int>();

    // --- ShoppingManager.cs の Start内を変更 ---
    void Start()
    {
        for (int i = 0; i < shopItems.Count; i++)
        {
            var item = shopItems[i];
            // 修正点：一元管理クラスからレベルをロード
            item.currentLevel = UpgradeManager.GetLevel(item.itemName);

            ShopItem target = item;
            int index = i;
            target.buyButton.onClick.AddListener(() => TryPurchase(target));

            EventTrigger trigger = target.buyButton.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = target.buyButton.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((data) => { ShowDescription(index); });
            trigger.triggers.Add(enterEntry);

            EventTrigger.Entry selectEntry = new EventTrigger.Entry();
            selectEntry.eventID = EventTriggerType.Select;
            selectEntry.callback.AddListener((data) => { ShowDescription(index); });
            trigger.triggers.Add(selectEntry);

            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) => { HideDescription(); });
            trigger.triggers.Add(exitEntry);

            EventTrigger.Entry deselectEntry = new EventTrigger.Entry();
            deselectEntry.eventID = EventTriggerType.Deselect;
            deselectEntry.callback.AddListener((data) => { HideDescription(); });
            trigger.triggers.Add(deselectEntry);

            RefreshUI(target);
        }
        HideDescription();
    }

    // --- ShoppingManager.cs の TryPurchase内を変更 ---
    private void TryPurchase(ShopItem item)
    {
        if (item.currentLevel >= item.maxLevel)
        {
            SoundManager.Instance.PlaySE("つるはしで掘る3");
            return;
        }

        MoneyManager mm = MoneyManager.Instance;
        if (mm == null)
        {
            SoundManager.Instance.PlaySE("つるはしで掘る3");
            return;
        }

        int cost = item.CurrentPrice;

        if (mm.GetMoney() >= cost)
        {
            SoundManager.Instance.PlaySE("つるはしで掘る4");
            mm.SpendMoney(cost);

            // 購入成功エフェクトの再生
            if (jewelSparkEffect != null)
            {
                Instantiate(jewelSparkEffect, player.transform.position + new Vector3(0, 0, 0), Quaternion.Euler(-90, -90, 0));
            }

            // 修正点：一元管理クラスを通じてレベルアップとセーブを実行
            UpgradeManager.IncreaseLevel(item.itemName);
            item.currentLevel = UpgradeManager.GetLevel(item.itemName);

            RefreshUI(item);
            ShowDescription(shopItems.IndexOf(item));
        }
        else SoundManager.Instance.PlaySE("つるはしで掘る3");
    }


    private void RefreshUI(ShopItem item)
    {
        bool isMax = item.currentLevel >= item.maxLevel;

        if (item.levelText != null)
            item.levelText.text = $"{item.currentLevel}";

        if (item.priceText != null)
            item.priceText.text = isMax ? "---" : $"$:{item.CurrentPrice:N0}";

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
                cb.selectedColor = Color.gray;
                cb.pressedColor = Color.gray;
            }
            else
            {
                // 通常時の色（元の色に戻す）
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
                cb.selectedColor = new Color(0.9f, 0.9f, 0.9f);
                cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            }
            item.buyButton.colors = cb;
        }
    }

    public void ShowDescription(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= shopItems.Count) return;
        ShopItem item = shopItems[itemIndex];

        if (descriptionText != null && item.levelDescriptions != null && item.levelDescriptions.Count > 0)
        {
            int descIndex = Mathf.Clamp(item.currentLevel - 1, 0, item.levelDescriptions.Count - 1);
            descriptionText.text = item.levelDescriptions[descIndex];
        }
    }

    public void HideDescription()
    {
        if (descriptionText != null)
        {
            descriptionText.text = "";
        }
    }
}
