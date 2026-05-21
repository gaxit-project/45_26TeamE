using UnityEngine;
using TMPro;

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [SerializeField, Header("所持している資金")]
    private int Money = 0;
    [SerializeField, Header("換金予定の資金")]
    private int MoneyOnHand = 0;
    [SerializeField, Header("通算取得額")]
    private int TotalEarnedMoney = 0;
    [SerializeField, Header("目標返済額")]
    private int TargetAmount = 0;
    [SerializeField, Header("今パート目標返済額")]
    private int TargetAmountOnPart = 0;

    [SerializeField, Header("所持している資金の表示場所")]
    private TextMeshProUGUI MoneyText;
    [SerializeField, Header("換金予定の資金の表示場所")]
    private TextMeshProUGUI MoneyOnHandText;
    [SerializeField, Header("目標返済額の表示場所")]
    private TextMeshProUGUI TargetAmountText;
    [SerializeField, Header("今パート目標返済額の表示場所")]
    private TextMeshProUGUI TargetAmountOnPartText;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }   
    }

    void Start()
    {
        MoneyOnHandText.text = MoneyOnHand.ToString("N0");
        //TargetAmountOnPartText.text = TargetAmountOnPart.ToString("0");
    }

    private void UpdateMoneyText()
    {
        MoneyOnHandText.text = MoneyOnHand.ToString("N0");
    }

    /// <summary>
    /// 手持ちの資金を増加
    /// </summary>
    /// <param name="value">増加する値</param>
    public void MoneyOnHandIncrease(int value)
    {
        MoneyOnHand = MoneyOnHand + value;
        TotalEarnedMoney += value; // 追記: 通算取得額にも加算
        UpdateMoneyText();
    }
    /// <summary>
    /// 手持ちの資金を減少
    /// </summary>
    /// <param name="value">現象する値</param>
    public void MoneyOnHandDecrease(int value)
    {
        MoneyOnHand = MoneyOnHand - value;
    }

    /// <summary>
    /// 手持ちの換金予定の資金を換金
    /// </summary>
    public void Cash()
    {
        Money += MoneyOnHand;
        MoneyOnHand = 0;
    }

    /// <summary>
    /// 資金を返済に当てる
    /// </summary>
    public void Refund()
    {
        TargetAmountOnPart -= Money;
        TargetAmount -= Money;
        Money = 0;
    }

    /// <summary>
    /// Result画面などで、現在のMoneyOnHandを読み取るための関数
    /// </summary>
    public int GetMoneyOnHand()
    {
        return MoneyOnHand;
    }

    /// <summary>
    /// Result画面などで、現在のTargetAmountOnPartを読み取るための関数
    /// </summary>
    public int GetTargetAmountOnPart()
    {
        return TargetAmountOnPart;
    }

    /// <summary>
    /// 通算取得額を返す
    /// </summary>
    public int GetTotalEarnedMoney()
    {
        return TotalEarnedMoney;
    }

    // MoneyManager.cs 内に追加

    // --- MoneyManager.cs の末尾（最後の } の直前）に追加 ---

    // 外部から現在の「所持金(Money)」を取得するための関数
    public int GetMoney()
    {
        return Money;
    }

    // 新しいシーンのテキストをマネージャーに登録し直す関数
    public void SetMoneyText(TextMeshProUGUI newText)
    {
        MoneyText = newText;
        UpdateTotalMoneyText();
    }

    // 所持金テキストの表示を更新する関数
    public void UpdateTotalMoneyText()
    {
        if (MoneyText != null)
        {
            MoneyText.text = Money.ToString("N0");
        }
    }
    public void SetMainSceneUI(TextMeshProUGUI onHand, TextMeshProUGUI targetPart)
    {
        MoneyOnHandText = onHand;
        TargetAmountOnPartText = targetPart;

        // 表示を現在の値で更新
        UpdateMoneyText();
        if (TargetAmountOnPartText != null)
        {
            TargetAmountOnPartText.text = TargetAmountOnPart.ToString("N0");
        }
    }

    // MoneyManager.cs 内に追加
    public void SpendMoney(int amount)
    {
        Money -= amount;
        UpdateTotalMoneyText(); // 既存の更新関数を呼ぶ
    }


}
