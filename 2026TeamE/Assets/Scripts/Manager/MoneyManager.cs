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
        MoneyOnHand = MoneyOnHand + value;
        TotalEarnedMoney += value;
        OnMoneyOnHandChanged?.Invoke(MoneyOnHand);
    }

    
    
    
    
    public void MoneyOnHandDecrease(int value)
    {
        MoneyOnHand = Mathf.Max(0, MoneyOnHand - value);
        OnMoneyOnHandChanged?.Invoke(MoneyOnHand);
    }

    
    
    
    public void Cash()
    {
        Money += MoneyOnHand;
        MoneyOnHand = 0;
        OnMoneyChanged?.Invoke(Money);
        OnMoneyOnHandChanged?.Invoke(MoneyOnHand);
    }

    
    
    
    public void Refund()
    {
        TargetAmountOnPart -= Money;
        TargetAmount -= Money;
        Money = 0;
        OnMoneyChanged?.Invoke(Money);
        OnTargetAmountOnPartChanged?.Invoke(TargetAmountOnPart);
    }

    
    
    
    public void SpendMoney(int amount)
    {
        Money -= amount;
        OnMoneyChanged?.Invoke(Money);
    }

    
    
    
    public int GetMoneyOnHand()
    {
        return MoneyOnHand;
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
