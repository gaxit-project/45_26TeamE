using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Video;
using UnityEngine.InputSystem;

public class ShoppingManager : MonoBehaviour
{
    public static ShoppingManager Instance { get; private set; }

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

    [Header("ショップ画面ルートパネル（開閉対象）")]
    [Tooltip("ショップUI全体の親オブジェクト。未設定の場合はこのコンポーネント自身のGameObjectが開閉対象になります。")]
    [SerializeField] private GameObject shopPanelRoot;

    [Header("閉じるボタン")]
    [SerializeField] private Button closeButton;

    [Header("初期フォーカスボタン（コントローラー用）")]
    [SerializeField] private GameObject firstSelectedButton;

    [Header("所持金表示（任意）")]
    [SerializeField] private TextMeshProUGUI totalMoneyText;

    [Header("カメラ切り替え設定")]
    [Tooltip("普段のゲームプレイ用カメラ（未設定の場合は Camera.main を自動検索）")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("ショップ画面用のカメラ（開いた時にON、閉じた時にOFF）")]
    [SerializeField] private Camera shopCamera;

    [Header("ショップ開店時に非表示にするCanvas・UI設定")]
    [Tooltip("ショップを開いた際に非表示にし、閉じた際に再表示するCanvas（またはUIオブジェクト）")]
    [SerializeField] private List<GameObject> canvasesToHide = new List<GameObject>();

    [Header("時間停止中のアニメーション設定")]
    [Tooltip("ショップ表示中（時間停止中）でも動かしたいプレイヤーのAnimator。2つあるうち動かしたい方をアタッチしてください。")]
    [SerializeField] private Animator animatedPlayerAnimator;

    private AnimatorUpdateMode originalUpdateMode = AnimatorUpdateMode.Normal;
    private Animator activeAnimatedAnimator;

    [Header("ショップ設定")]
    [SerializeField] private List<ShopItem> shopItems = new List<ShopItem>();

    [Header("UI (全体共通の説明表示用。不要なら空でOK)")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("動画表示")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("エフェクト設定")]
    [Tooltip("強化購入時にキャラクターから生成するエフェクトのプレハブ")]
    [SerializeField] private GameObject purchaseEffectPrefab;

    [Tooltip("以前の設定との互換用（purchaseEffectPrefabが空の場合に使用）")]
    [SerializeField] private GameObject jewelSparkEffect;

    [Tooltip("エフェクトを発生させる位置（特定の手や頭など）。未設定の場合は指定したキャラクターの位置になります。")]
    [SerializeField] private Transform effectSpawnPoint;

    [Tooltip("エフェクトの発生位置オフセット（高さなどの微調整用）")]
    [SerializeField] private Vector3 effectOffset = Vector3.zero;

    [Tooltip("プレハブ自体の回転をそのまま使用するかどうか")]
    [SerializeField] private bool usePrefabRotation = false;

    [Tooltip("usePrefabRotationがfalseの場合の回転角度")]
    [SerializeField] private Vector3 customEffectRotation = new Vector3(-90, -90, 0);

    [Tooltip("生成したエフェクトをキャラクターの子オブジェクトにするかどうか")]
    [SerializeField] private bool parentEffectToCharacter = false;

    [SerializeField] private GameObject player;

    [Header("ボタンカラー")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color maxLevelColor = Color.gray;
    [SerializeField] private Color cannotBuyColor = Color.red;

    [Header("星のカラー設定")]
    [SerializeField] private Color activeStarColor = Color.white;
    [SerializeField] private Color inactiveStarColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    private bool isInitialized = false;

    /// <summary>ショップ画面が開いているかどうか</summary>
    public bool IsOpen => (shopPanelRoot != null ? shopPanelRoot.activeSelf : gameObject.activeSelf);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeIfNeeded();

        // 起動時はパネルを閉じておく（メインシーン配置の場合）
        if (shopPanelRoot != null && shopPanelRoot != gameObject)
        {
            shopPanelRoot.SetActive(false);
        }

        // 起動時はショップ用カメラをOFFにしておく
        if (shopCamera != null)
        {
            shopCamera.enabled = false;
            shopCamera.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged += OnMoneyChanged;
        }
    }

    private void OnDisable()
    {
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged -= OnMoneyChanged;
        }
    }

    private void OnMoneyChanged(int newMoney)
    {
        UpdateTotalMoneyText();
        UpdateAllButtons();
    }

    private void InitializeIfNeeded()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (shopPanelRoot == null) shopPanelRoot = gameObject;

        AutoBindMissingReferences();

        if (closeButton != null)
        {
            // 旧シーン再読み込みスクリプトが付いている場合は削除
            var asb = closeButton.GetComponent<anySceneButton>();
            if (asb != null)
            {
                Destroy(asb);
            }

            closeButton.onClick.RemoveListener(CloseShop);
            closeButton.onClick.AddListener(CloseShop);
        }

        for (int i = 0; i < shopItems.Count; i++)
        {
            var item = shopItems[i];
            item.currentLevel = UpgradeManager.GetLevel(item.itemName);

            ShopItem target = item;
            int index = i;

            if (target.buyButton != null)
            {
                target.buyButton.onClick.RemoveAllListeners();
                target.buyButton.onClick.AddListener(() => TryPurchase(target));

                EventTrigger trigger = target.buyButton.gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = target.buyButton.gameObject.AddComponent<EventTrigger>();
                trigger.triggers.Clear();

                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => { ShowVideoAndCommonDesc(index); });
                trigger.triggers.Add(enterEntry);

                EventTrigger.Entry selectEntry = new EventTrigger.Entry();
                selectEntry.eventID = EventTriggerType.Select;
                selectEntry.callback.AddListener((data) => { ShowVideoAndCommonDesc(index); });
                trigger.triggers.Add(selectEntry);
            }

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
        }

        // 動画プレイヤーがポーズ中（Time.timeScale=0）でも再生できるように設定
        if (videoPlayer != null)
        {
            videoPlayer.timeReference = VideoTimeReference.Freerun;
        }
    }

    /// <summary>
    /// 未アタッチのUIパーツ（ボタン、テキスト、星など）をShopCanvas内から自動検索して補完する
    /// </summary>
    private void AutoBindMissingReferences()
    {
        if (shopPanelRoot == null) shopPanelRoot = gameObject;

        // 1. 閉じるボタンの自動設定
        if (closeButton == null)
        {
            var btnTransform = shopPanelRoot.transform.Find("RePlay");
            if (btnTransform != null) closeButton = btnTransform.GetComponent<Button>();
        }

        // 2. 所持金テキストの自動設定
        if (totalMoneyText == null)
        {
            var moneyTransform = shopPanelRoot.transform.Find("Money (1)");
            if (moneyTransform != null)
            {
                totalMoneyText = moneyTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        // 3. 動画プレイヤーの自動設定
        if (videoPlayer == null)
        {
            videoPlayer = shopPanelRoot.GetComponentInChildren<VideoPlayer>(true);
        }

        // 4. 各アイテムのボタン・星・価格テキスト等の自動設定
        Button[] allButtons = shopPanelRoot.GetComponentsInChildren<Button>(true);

        foreach (var item in shopItems)
        {
            // ボタンの検索 (Drill Button, Sonar Button, Radar/Rader Button, Jet Button)
            if (item.buyButton == null)
            {
                string targetName = item.itemName.ToLower();
                if (targetName == "rader") targetName = "radar";

                foreach (var b in allButtons)
                {
                    string bName = b.gameObject.name.ToLower();
                    if (bName.Contains(targetName))
                    {
                        item.buyButton = b;
                        break;
                    }
                }
            }

            if (item.buyButton != null)
            {
                Transform bTransform = item.buyButton.transform;

                // 価格テキスト (子オブジェクト "Money" の TextMeshProUGUI)
                if (item.priceText == null)
                {
                    var priceObj = bTransform.Find("Money");
                    if (priceObj != null)
                    {
                        item.priceText = priceObj.GetComponent<TextMeshProUGUI>();
                    }
                }

                // 説明テキスト (子オブジェクト "Text (TMP) (1)")
                if (item.itemDescriptionText == null)
                {
                    var descObj = bTransform.Find("Text (TMP) (1)");
                    if (descObj != null)
                    {
                        item.itemDescriptionText = descObj.GetComponent<TextMeshProUGUI>();
                    }
                }

                // アイコン画像 (子オブジェクト "Image (1)")
                if (item.descriptionImage == null)
                {
                    var imgObj = bTransform.Find("Image (1)");
                    if (imgObj != null)
                    {
                        item.descriptionImage = imgObj.GetComponent<Image>();
                    }
                }

                // 星UI (子オブジェクト "Stars" 配下の全子オブジェクト)
                if (item.starImages == null || item.starImages.Length == 0 || item.starImages[0] == null)
                {
                    var starsObj = bTransform.Find("Stars");
                    if (starsObj != null)
                    {
                        int childCount = starsObj.childCount;
                        item.starImages = new GameObject[childCount];
                        for (int c = 0; c < childCount; c++)
                        {
                            item.starImages[c] = starsObj.GetChild(c).gameObject;
                        }
                    }
                }
            }
        }
    }


    /// <summary>
    /// メインシーン上でショップ画面を開く
    /// </summary>
    public void OpenShop()
    {
        InitializeIfNeeded();

        SoundManager.Instance.PlayBGM("room2");

        if (shopPanelRoot != null)
        {
            shopPanelRoot.SetActive(true);
        }

        // 指定されたCanvas・UIを非表示にする
        if (canvasesToHide != null)
        {
            foreach (var ui in canvasesToHide)
            {
                if (ui != null) ui.SetActive(false);
            }
        }

        // カメラの切り替え（メインカメラOFF -> ショップカメラON）
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera != null && shopCamera != null)
        {
            mainCamera.enabled = false;
            shopCamera.gameObject.SetActive(true);
            shopCamera.enabled = true;
        }

        // ゲーム内時間を一時停止
        Time.timeScale = 0f;

        if (player == null)
        {
            player = GameObject.FindWithTag("Player");
        }

        // 指定されたAnimatorを時間停止中（UnscaledTime）でも動くように設定
        activeAnimatedAnimator = animatedPlayerAnimator;
        if (activeAnimatedAnimator == null && player != null)
        {
            activeAnimatedAnimator = player.GetComponentInChildren<Animator>();
        }

        if (activeAnimatedAnimator != null)
        {
            originalUpdateMode = activeAnimatedAnimator.updateMode;
            activeAnimatedAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        RefreshAll();

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("つるはしで掘る1");
            SoundManager.Instance.StopLoopSE();
        }
        if (HapticsManager.Instance != null)
        {
            HapticsManager.Instance.Stop();
        }

        // 最初のボタンを選択状態にしてコントローラー操作を可能にする
        GameObject toSelect = firstSelectedButton;
        if (toSelect == null && shopItems.Count > 0 && shopItems[0].buyButton != null)
        {
            toSelect = shopItems[0].buyButton.gameObject;
        }

        if (toSelect != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(toSelect);
        }
    }

    /// <summary>
    /// ショップ画面を閉じてゲームを再開する
    /// </summary>
    public void CloseShop()
    {
        SoundManager.Instance.PlayBGM("メインBGM");

        if (shopPanelRoot != null)
        {
            shopPanelRoot.SetActive(false);
        }

        // Animatorの動作モードを元に戻す
        if (activeAnimatedAnimator != null)
        {
            activeAnimatedAnimator.updateMode = originalUpdateMode;
            activeAnimatedAnimator = null;
        }

        // 指定されたCanvas・UIを再表示する
        if (canvasesToHide != null)
        {
            foreach (var ui in canvasesToHide)
            {
                if (ui != null) ui.SetActive(true);
            }
        }

        // カメラを元に戻す（ショップカメラOFF -> メインカメラON）
        if (shopCamera != null)
        {
            shopCamera.enabled = false;
            shopCamera.gameObject.SetActive(false);
        }

        if (mainCamera != null)
        {
            mainCamera.enabled = true;
        }

        // ゲーム内時間を再開
        Time.timeScale = 1f;

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("つるはしで掘る3");
        }
    }

    public void RefreshAll()
    {
        for (int i = 0; i < shopItems.Count; i++)
        {
            var item = shopItems[i];
            item.currentLevel = UpgradeManager.GetLevel(item.itemName);
            RefreshUI(item);
        }

        UpdateAllItemDescriptions();
        UpdateAllButtons();
        UpdateTotalMoneyText();

        if (descriptionText != null) descriptionText.text = "";
        if (videoPlayer != null) videoPlayer.Stop();
    }

    private void UpdateTotalMoneyText()
    {
        if (totalMoneyText != null && MoneyManager.Instance != null)
        {
            totalMoneyText.text = MoneyManager.Instance.GetMoney().ToString("N0");
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

            GameObject effectPrefabToSpawn = purchaseEffectPrefab != null ? purchaseEffectPrefab : jewelSparkEffect;
            if (effectPrefabToSpawn != null)
            {
                Transform targetTransform = effectSpawnPoint;
                if (targetTransform == null)
                {
                    targetTransform = (activeAnimatedAnimator != null) ? activeAnimatedAnimator.transform : (player != null ? player.transform : transform);
                }

                Vector3 spawnPos = (targetTransform != null ? targetTransform.position : Vector3.zero) + effectOffset;
                Quaternion spawnRot = usePrefabRotation ? effectPrefabToSpawn.transform.rotation : Quaternion.Euler(customEffectRotation);
                Transform parentTransform = parentEffectToCharacter ? targetTransform : null;

                GameObject effect = Instantiate(effectPrefabToSpawn, spawnPos, spawnRot, parentTransform);

                // 時間停止中でもパーティクルが動くように設定
                var pSystems = effect.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in pSystems)
                {
                    var main = ps.main;
                    main.useUnscaledTime = true;
                    ps.Play();
                }

                // エフェクト内にAnimatorがある場合も時間停止中に動くように設定
                var animators = effect.GetComponentsInChildren<Animator>(true);
                foreach (var anim in animators)
                {
                    anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                }
            }

            UpgradeManager.IncreaseLevel(item.itemName);
            item.currentLevel = UpgradeManager.GetLevel(item.itemName);

            UpdateAllButtons();
            UpdateItemDescription(item);
            UpdateTotalMoneyText();
            ShowVideoAndCommonDesc(shopItems.IndexOf(item));
        }
        else
        {
            SoundManager.Instance.PlaySE("つるはしで掘る3");
        }
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

    private void UpdateAllItemDescriptions()
    {
        foreach (var item in shopItems)
        {
            UpdateItemDescription(item);
        }
    }

    public void ShowVideoAndCommonDesc(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= shopItems.Count) return;

        ShopItem item = shopItems[itemIndex];

        if (descriptionText != null && item.levelDescriptions != null && item.levelDescriptions.Count > 0)
        {
            int descIndex = Mathf.Clamp(item.currentLevel - 1, 0, item.levelDescriptions.Count - 1);
            descriptionText.text = item.levelDescriptions[descIndex];
        }

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