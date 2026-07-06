using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class ResultManager : MonoBehaviour
{
    [System.Serializable]
    public class LootItem
    {
        public Sprite sprite;
        public int moneyValue;
        [Range(0, 100)] public int GoldLeatherBagChance;
        [Range(0, 100)] public int leatherBagChance;
    }

    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI onHandResultText;

    [Header("取得アイテム表示")]
    [SerializeField] private Transform itemIconContainer;
    [SerializeField] private GameObject itemIconPrefab;
    [SerializeField] private float iconInterval = 0.3f;
    [SerializeField] private float iconSize = 64f;
    [SerializeField, Tooltip("宝石の個数を表示するテキスト（外部配置）")]
    private TextMeshProUGUI gemCountText;

    [Header("宝箱・皮袋の設定")]
    [SerializeField] private Sprite openGoldLeatherBagSprite;
    [SerializeField] private Sprite openLeatherBagSprite;

    [Header("ドロップアイテム設定")]
    [SerializeField] private LootItem lootJewel;
    [SerializeField] private LootItem lootManyJewels;
    [SerializeField] private LootItem lootBill;
    [SerializeField] private LootItem lootBillBundle;

    [Header("フォーカス設定")]
    [SerializeField, Tooltip("処理完了後にフォーカスを当てるボタン")]
    private GameObject nextFocusButton;

    private bool isSkipRequested = false;
    private bool isAnimationFinished = false;

    void Start()
    {
        // 自動演出コルーチン開始
        StartCoroutine(AutoResultSequence());
    }

    void Update()
    {
        if (isAnimationFinished || isSkipRequested) return;

        bool wasPressed = false;
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) wasPressed = true;
        if (Gamepad.current != null)
        {
            if (Gamepad.current.startButton.wasPressedThisFrame) wasPressed = true;
            if (Gamepad.current.buttonEast.wasPressedThisFrame) wasPressed = true; // B/○
            if (Gamepad.current.buttonSouth.wasPressedThisFrame) wasPressed = true; // A/×
        }
        
        if (wasPressed)
        {
            isSkipRequested = true;
        }
    }

    private IEnumerator WaitOrSkip(float time)
    {
        if (isSkipRequested) yield break;
        float elapsed = 0f;
        while (elapsed < time)
        {
            if (isSkipRequested) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator AutoResultSequence()
    {
        List<ItemInventoryManager.ItemData> items = ItemInventoryManager.GetCollectedItemsForResult();
        MoneyManager mm = MoneyManager.Instance;

        // 初期所持金表示
        if (mm != null)
        {
            onHandResultText.text = mm.GetMoneyOnHand().ToString("N0");
        }

        List<ItemInventoryManager.ItemData> gems = new List<ItemInventoryManager.ItemData>();
        List<ItemInventoryManager.ItemData> bags = new List<ItemInventoryManager.ItemData>();

        foreach (var item in items)
        {
            if (item.type == ItemType.LeatherBag || item.type == ItemType.GoldLeatherBag) 
            {
                bags.Add(item);
            }
            else
            {
                gems.Add(item);
            }
        }

        // ==========================================
        // 1. 宝石の演出
        // ==========================================
        if (gems.Count > 0)
        {
            int gemTotalMoney = 0;
            foreach (var gem in gems)
            {
                gemTotalMoney += gem.moneyValue;
            }

            if (gemCountText != null)
            {
                // 0.3秒で一気にカウントアップ
                float duration = 0.3f;
                float elapsed = 0f;
                int targetCount = gems.Count;

                while (elapsed < duration)
                {
                    if (isSkipRequested) break;
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    int currentCount = Mathf.RoundToInt(Mathf.Lerp(0, targetCount, t));
                    gemCountText.text = "x" + currentCount;
                    yield return null;
                }
                gemCountText.text = "x" + targetCount;
            }

            // 袋の開封前に合計額へ加算
            if (mm != null && gemTotalMoney > 0)
            {
                mm.MoneyOnHandIncrease(gemTotalMoney);
                onHandResultText.text = mm.GetMoneyOnHand().ToString("N0");
            }

            yield return StartCoroutine(WaitOrSkip(0.5f));
        }

        // ==========================================
        // 2. 袋の演出
        // ==========================================
        if (bags.Count > 0)
        {
            List<GameObject> bagIconObjs = new List<GameObject>();

            // まずすべての袋アイコンを未開封の状態で並べる
            foreach (var bag in bags)
            {
                GameObject bagIconObj = CreateIconObject(bag.icon);
                bagIconObjs.Add(bagIconObj);
                yield return StartCoroutine(PopInAnimation(bagIconObj.transform));
            }

            yield return StartCoroutine(WaitOrSkip(iconInterval));

            // 左端から順番に開封
            for (int i = 0; i < bags.Count; i++)
            {
                var bag = bags[i];
                var bagObj = bagIconObjs[i];

                // 中身抽選
                LootItem loot = RollLoot(bag.type);

                // 画像差し替え (開封)
                Image img = bagObj.GetComponent<Image>();
                if (img != null)
                {
                    if (bag.type == ItemType.LeatherBag && openLeatherBagSprite != null)
                        img.sprite = openLeatherBagSprite;
                    else if (bag.type == ItemType.GoldLeatherBag && openGoldLeatherBagSprite != null)
                        img.sprite = openGoldLeatherBagSprite;
                    
                    // ここではまだ元の明るさのまま
                    img.color = Color.white;
                }

                Image lootImg = null;

                // 中身アイコンの表示
                if (loot != null && loot.sprite != null)
                {
                    GameObject lootIcon = new GameObject("LootIcon");
                    lootIcon.transform.SetParent(bagObj.transform, false);

                    lootImg = lootIcon.AddComponent<Image>();
                    lootImg.sprite = loot.sprite;
                    lootImg.raycastTarget = false;

                    RectTransform lootRect = lootIcon.GetComponent<RectTransform>();
                    lootRect.anchorMin = Vector2.zero;
                    lootRect.anchorMax = Vector2.one;
                    lootRect.offsetMin = Vector2.zero;
                    lootRect.offsetMax = Vector2.zero;

                    yield return StartCoroutine(PopInAnimation(lootIcon.transform));

                    // 合計額へ加算（換金）
                    if (mm != null && loot.moneyValue > 0)
                    {
                        mm.MoneyOnHandIncrease(loot.moneyValue);
                        onHandResultText.text = mm.GetMoneyOnHand().ToString("N0");

                        // 換金された金額をポップアップ表示（待機せずに並行してアニメーション）
                        StartCoroutine(PopupMoneyText(bagObj.transform, loot.moneyValue));
                    }
                }

                // 換金し終えたことを示すため、袋と中身を暗くする
                if (img != null) img.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                if (lootImg != null) lootImg.color = new Color(0.6f, 0.6f, 0.6f, 1f);

                // 次の袋を開封するまでの猶予
                yield return StartCoroutine(WaitOrSkip(iconInterval));
            }
        }

        // 全て終わった後の待機
        yield return StartCoroutine(WaitOrSkip(iconInterval * 2));

        isAnimationFinished = true;

        // 指定したボタンにフォーカスを当てる
        if (nextFocusButton != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(nextFocusButton);
        }
    }

    private GameObject CreateIconObject(Sprite iconSprite)
    {
        GameObject iconObj = Instantiate(itemIconPrefab, itemIconContainer);
        Image image = iconObj.GetComponent<Image>();
        if (image != null && iconSprite != null)
        {
            image.sprite = iconSprite;
        }

        RectTransform rect = iconObj.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(iconSize, iconSize);
        }

        LayoutElement layout = iconObj.GetComponent<LayoutElement>();
        if (layout == null) layout = iconObj.AddComponent<LayoutElement>();
        layout.preferredWidth = iconSize;
        layout.preferredHeight = iconSize;

        // ボタン機能がアタッチされている場合は無効化する
        Button btn = iconObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.interactable = false;
        }

        return iconObj;
    }

    private IEnumerator PopInAnimation(Transform target)
    {
        if (target == null) yield break;

        float duration = 0.2f;
        float elapsed = 0f;
        target.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            if (isSkipRequested) break;
            if (target == null) yield break;
            float t = elapsed / duration;
            float overshoot = 1.7f;
            float easedT = 1f + (overshoot + 1f) * Mathf.Pow(t - 1f, 3f)
                              + overshoot * Mathf.Pow(t - 1f, 2f);
            target.localScale = Vector3.one * easedT;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (target != null) target.localScale = Vector3.one;
    }

    private IEnumerator PopupMoneyText(Transform parent, int amount)
    {
        if (parent == null || amount <= 0) yield break;

        // テキストオブジェクトを生成
        GameObject textObj = new GameObject("PopupMoneyText");
        textObj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "+" + amount.ToString("N0");
        tmp.fontSize = 48f; // サイズを元の1.5倍に
        tmp.fontStyle = FontStyles.Bold; // 太字に
        tmp.color = new Color(1f, 0.9f, 0.4f, 1f); // 黄色っぽく
        tmp.alignment = TextAlignmentOptions.Center;

        // デフォルトフォントが外れるのを防ぐため、既存のテキストからフォントをコピー
        if (onHandResultText != null)
        {
            tmp.font = onHandResultText.font;
            tmp.fontMaterial = onHandResultText.fontMaterial;
        }

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(400f, 100f); // 横幅・縦幅に余裕を持たせる

        // 改行を防ぎ、レイキャストを受けないようにする
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        float duration = 0.8f;
        float elapsed = 0f;
        float moveDistance = 80f; // 浮かび上がる距離

        Vector2 startPos = rect.anchoredPosition;

        while (elapsed < duration)
        {
            if (isSkipRequested) break; // スキップされたら即消す

            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // EaseOutっぽく上へ移動
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            rect.anchoredPosition = startPos + new Vector2(0f, moveDistance * easedT);

            // 後半でフェードアウト
            if (t > 0.5f)
            {
                Color c = tmp.color;
                c.a = 1f - ((t - 0.5f) * 2f);
                tmp.color = c;
            }

            yield return null;
        }

        Destroy(textObj);
    }

    private LootItem RollLoot(ItemType containerType)
    {
        LootItem[] allLoot = { lootJewel, lootManyJewels, lootBill, lootBillBundle };

        int totalWeight = 0;
        List<(LootItem item, int weight)> weightedList = new List<(LootItem, int)>();

        foreach (var loot in allLoot)
        {
            if (loot == null || loot.sprite == null) continue;

            int chance = 0;
            if (containerType == ItemType.GoldLeatherBag)
            {
                chance = loot.GoldLeatherBagChance; // 金の皮袋（元宝箱設定）用
            }
            else
            {
                chance = loot.leatherBagChance; // 普通の皮袋用
            }
            
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

    private void OnDestroy()
    {
        // 次のシーンでもアイテムデータを参照する可能性があるためクリアしない
    }
}
