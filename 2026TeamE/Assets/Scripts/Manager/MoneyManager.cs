using System;
using UnityEngine;

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

    /// <summary>
    /// Moneyが変化した時に発火
    /// </summary>
    public event Action<int> OnMoneyChanged;
    /// <summary>
    /// MoneyOnHandが変化した時に発火
    /// </summary>
    public event Action<int> OnMoneyOnHandChanged;
    /// <summary>
    /// TargetAmountOnPartが変化した時に発火
    /// </summary>
    public event Action<int> OnTargetAmountOnPartChanged;

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

    /// <summary>
    /// 手持ちの資金を増加
    /// </summary>
    /// <param name="value">増加する値</param>
    public void MoneyOnHandIncrease(int value)
    {
        MoneyOnHand = MoneyOnHand + value;
        TotalEarnedMoney += value;
        OnMoneyOnHandChanged?.Invoke(MoneyOnHand);
    }

    /// <summary>
    /// 手持ちの資金を減少
    /// </summary>
    /// <param name="value">減少する値</param>
    public void MoneyOnHandDecrease(int value)
    {
        MoneyOnHand = Mathf.Max(0, MoneyOnHand - value);
        OnMoneyOnHandChanged?.Invoke(MoneyOnHand);
    }

    /// <summary>
    /// 手持ちの換金予定の資金を換金
    /// </summary>
    public void Cash()
    {
        Money += MoneyOnHand;
        MoneyOnHand = 0;
        OnMoneyChanged?.Invoke(Money);
        OnMoneyOnHandChanged?.Invoke(MoneyOnHand);
    }

    /// <summary>
    /// 資金を返済に当てる
    /// </summary>
    public void Refund()
    {
        TargetAmountOnPart -= Money;
        TargetAmount -= Money;
        Money = 0;
        OnMoneyChanged?.Invoke(Money);
        OnTargetAmountOnPartChanged?.Invoke(TargetAmountOnPart);
    }

    /// <summary>
    /// 所持金を消費する
    /// </summary>
    public void SpendMoney(int amount)
    {
        Money -= amount;
        OnMoneyChanged?.Invoke(Money);
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

    /// <summary>
    /// 外部から現在の「所持金(Money)」を取得するための関数
    /// </summary>
    public int GetMoney()
    {
        return Money;
    }
}
