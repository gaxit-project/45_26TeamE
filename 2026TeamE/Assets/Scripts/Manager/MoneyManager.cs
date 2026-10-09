using System;
using UnityEngine;

public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [SerializeField, Header("所持金 (統合済み)")]
    private int Money = 0;
    
    // シリアライズ互換性のために残す（使用しない）
    [SerializeField, Header("仮の資金(未使用)")]
    private int MoneyOnHand = 0;
    
    [SerializeField, Header("累計獲得金額")]
    private int TotalEarnedMoney = 0;
    [SerializeField, Header("目標金額")]
    private int TargetAmount = 0;
    [SerializeField, Header("パート目標金額")]
    private int TargetAmountOnPart = 0;

    public event Action<int> OnMoneyChanged;
    public event Action<int> OnMoneyOnHandChanged;
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

    public void MoneyOnHandIncrease(int value)
    {
        Money += value;
        TotalEarnedMoney += value;
        OnMoneyOnHandChanged?.Invoke(Money);
        OnMoneyChanged?.Invoke(Money);
    }

    public void MoneyOnHandDecrease(int value)
    {
        Money = Mathf.Max(0, Money - value);
        OnMoneyOnHandChanged?.Invoke(Money);
        OnMoneyChanged?.Invoke(Money);
    }

    public void Cash()
    {
        // 換金プロセスを廃止し、MoneyとMoneyOnHandを統合したため、何も移動させる必要はありません。
        // UIの更新だけ行います。
        OnMoneyChanged?.Invoke(Money);
        OnMoneyOnHandChanged?.Invoke(Money);
    }

    public void Refund()
    {
        TargetAmountOnPart -= Money;
        TargetAmount -= Money;
        Money = 0;
        OnMoneyChanged?.Invoke(Money);
        OnMoneyOnHandChanged?.Invoke(Money);
        OnTargetAmountOnPartChanged?.Invoke(TargetAmountOnPart);
    }

    public void SpendMoney(int amount)
    {
        Money -= amount;
        OnMoneyChanged?.Invoke(Money);
        OnMoneyOnHandChanged?.Invoke(Money);
    }

    public int GetMoneyOnHand()
    {
        return Money;
    }

    public int GetTargetAmountOnPart()
    {
        return TargetAmountOnPart;
    }

    public int GetTotalEarnedMoney()
    {
        return TotalEarnedMoney;
    }

    public int GetMoney()
    {
        return Money;
    }
}
