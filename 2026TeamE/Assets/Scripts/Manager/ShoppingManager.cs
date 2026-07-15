using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Video;

public class ShoppingManager : MonoBehaviour
{
    [System.Serializable]
    public class ShopItem
    {
        public string itemName;
        public Button buyButton;
        public TextMeshProUGUI priceText;
        public int basePrice = 100;
        public int maxLevel = 10;

        [TextArea(2, 4)]
        public List<string> levelDescriptions;

        [Header("個別説明テキスト")]
        public TextMeshProUGUI itemDescriptionText;

        [Header("レベル表示用の星UI")]
        public GameObject[] starImages;

        [Header("説明動画")]
        public VideoClip descriptionVideo;

        [Header("説明画像")]
        public Sprite descriptionSprite;

        [Header("表示先Image")]
        public Image descriptionImage;

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

    [Header("UI (全体共通の説明表示用。不要なら空でOK)")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("動画表示")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("エフェクト")]
    [SerializeField] private GameObject jewelSparkEffect;
    [SerializeField] private GameObject player;

    [Header("ボタンカラー")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color maxLevelColor = Color.gray;
    [SerializeField] private Color cannotBuyColor = Color.red;

    [Header("星のカラー設定")]
    [SerializeField] private Color activeStarColor = Color.white;
    [SerializeField] private Color inactiveStarColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    private static Dictionary<string, int> savedLevels = new Dictionary<string, int>();

    void Start()
    {
        for (int i = 0; i < shopItems.Count; i++)
        {
            var item = shopItems[i];
            item.currentLevel = UpgradeManager.GetLevel(item.itemName);

            ShopItem target = item;
            int index = i;
            target.buyButton.onClick.AddListener(() => TryPurchase(target));

            // ホバー/選択時に「全体説明（descriptionText）」や「動画」を切り替えたい場合のみイベントを登録
            EventTrigger trigger = target.buyButton.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = target.buyButton.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((data) => { ShowVideoAndCommonDesc(index); });
            trigger.triggers.Add(enterEntry);

            EventTrigger.Entry selectEntry = new EventTrigger.Entry();
            selectEntry.eventID = EventTriggerType.Select;
            selectEntry.callback.AddListener((data) => { ShowVideoAndCommonDesc(index); });
            trigger.triggers.Add(selectEntry);

            // 【変更】カーソルが外れたり、選択解除されたときの非表示処理（HideDescription）は登録しない

            if (item.descriptionImage != null)
            {
                item.descriptionImage.sprite = item.descriptionSprite;
                item.descriptionImage.enabled = item.descriptionSprite != null;
            }

            if (item.starImages != null)
            {
                foreach (var star in item.starImages)
                {
                    if (star != null) star.SetActive(true);
                }
            }

            RefreshUI(target);
        }

        // 初期状態で個別説明テキストはすべて最新レベルのものに更新しておく
        UpdateAllItemDescriptions();

        // 全体共有の説明や動画プレイヤーは初期状態でクリアしておく
        if (descriptionText != null) descriptionText.text = "";
        if (videoPlayer != null) videoPlayer.Stop();

        UpdateAllButtons();
    }

    private void Update()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            Debug.Log(videoPlayer.texture);
        }
    }

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

            if (jewelSparkEffect != null)
            {
                Instantiate(jewelSparkEffect, player.transform.position, Quaternion.Euler(-90, -90, 0));
            }

            UpgradeManager.IncreaseLevel(item.itemName);
            item.currentLevel = UpgradeManager.GetLevel(item.itemName);

            UpdateAllButtons();

            // 購入後にこのアイテムの常時表示説明を新しいレベルのものに更新
            UpdateItemDescription(item);

            // ホバー時用の全体説明や動画も更新
            ShowVideoAndCommonDesc(shopItems.IndexOf(item));
        }
        else SoundManager.Instance.PlaySE("つるはしで掘る3");
    }

    private void RefreshUI(ShopItem item)
    {
        bool isMax = item.currentLevel >= item.maxLevel;
        MoneyManager mm = MoneyManager.Instance;
        bool canBuy = mm != null && mm.GetMoney() >= item.CurrentPrice;

        if (item.priceText != null)
            item.priceText.text = isMax ? "---" : $"{item.CurrentPrice:N0}";

        if (item.starImages != null)
        {
            for (int i = 0; i < item.starImages.Length; i++)
            {
                if (item.starImages[i] != null)
                {
                    Image starImage = item.starImages[i].GetComponent<Image>();
                    if (starImage != null)
                    {
                        starImage.color = (i < item.currentLevel) ? activeStarColor : inactiveStarColor;
                    }
                }
            }
        }

        if (item.buyButton != null)
        {
            item.buyButton.interactable = true;
            ColorBlock cb = item.buyButton.colors;

            if (isMax)
            {
                cb.normalColor = maxLevelColor;
                cb.highlightedColor = maxLevelColor;
                cb.selectedColor = maxLevelColor;
                cb.pressedColor = maxLevelColor;
            }
            else if (!canBuy)
            {
                cb.normalColor = cannotBuyColor;
                cb.highlightedColor = cannotBuyColor;
                cb.selectedColor = cannotBuyColor;
                cb.pressedColor = cannotBuyColor;
            }
            else
            {
                cb.normalColor = normalColor;
                cb.highlightedColor = normalColor * 0.9f;
                cb.selectedColor = normalColor * 0.9f;
                cb.pressedColor = normalColor * 0.8f;
            }
            item.buyButton.colors = cb;
        }

        if (item.descriptionImage != null)
        {
            item.descriptionImage.sprite = item.descriptionSprite;
            item.descriptionImage.enabled = item.descriptionSprite != null;
        }
    }

    /// <summary>
    /// 対象アイテムの「個別説明テキスト」を現在のレベルに合わせて更新する（常時表示用）
    /// </summary>
    private void UpdateItemDescription(ShopItem item)
    {
        if (item.itemDescriptionText != null &&
            item.levelDescriptions != null &&
            item.levelDescriptions.Count > 0)
        {
            int descIndex = Mathf.Clamp(
                item.currentLevel - 1,
                0,
                item.levelDescriptions.Count - 1);

            item.itemDescriptionText.text = item.levelDescriptions[descIndex];
        }
    }

    /// <summary>
    /// すべてのアイテムの「個別説明テキスト」を最新レベルのものに更新する
    /// </summary>
    private void UpdateAllItemDescriptions()
    {
        foreach (var item in shopItems)
        {
            UpdateItemDescription(item);
        }
    }

    /// <summary>
    /// カーソルが合わさった時に、全体共通のテキストや説明動画を再生・更新する
    /// </summary>
    public void ShowVideoAndCommonDesc(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= shopItems.Count) return;

        ShopItem item = shopItems[itemIndex];

        // 共有用の説明UIがある場合のみ更新
        if (descriptionText != null && item.levelDescriptions != null && item.levelDescriptions.Count > 0)
        {
            int descIndex = Mathf.Clamp(item.currentLevel - 1, 0, item.levelDescriptions.Count - 1);
            descriptionText.text = item.levelDescriptions[descIndex];
        }

        // 動画の再生
        if (videoPlayer != null && item.descriptionVideo != null)
        {
            videoPlayer.Stop();
            videoPlayer.clip = item.descriptionVideo;
            videoPlayer.isLooping = true;
            videoPlayer.Play();
        }
    }

    private void UpdateAllButtons()
    {
        foreach (var item in shopItems)
        {
            RefreshUI(item);
        }
    }
}