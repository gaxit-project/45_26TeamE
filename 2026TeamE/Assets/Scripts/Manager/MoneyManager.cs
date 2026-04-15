using UnityEngine;
using TMPro;

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [SerializeField,Header("所持している資金")]
    private int Money=0;
    [SerializeField, Header("換金予定の資金")]
    private int MoneyOnHand=0;
    [SerializeField, Header("目標返済額")]
    private int TargetAmount=0;
    [SerializeField, Header("今パート目標返済額")]
    private int TargetAmountOnPart=0;

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

    void Update()
    {
        MoneyText.text = Money.ToString("NO");
        MoneyOnHandText.text = MoneyOnHand.ToString("NO");
        TargetAmountText.text = TargetAmount.ToString("NO");
        TargetAmountOnPartText.text = TargetAmountOnPart.ToString("NO");
    }
    /// <summary>
    /// 手持ちの資金を増加
    /// </summary>
    /// <param name="value">増加する値</param>
    public void MoneOnHandIncrease(int value)
    {
        MoneyOnHand = MoneyOnHand + value;
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


    
}
