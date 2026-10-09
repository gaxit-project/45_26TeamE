using UnityEngine;
using TMPro;

public class MoneyExchange : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI totalMoneyText;

    private MoneyManager mm;
    private bool cashed = false;

    void Start()
    {
        if (mm == null)
        {
            mm = MoneyManager.Instance;
            if (mm != null && !cashed)
            {
                mm.Cash();
                cashed = true;
                UpdateMoneyText(mm.GetMoney());
                Debug.Log($"換金完了！ 現在の所持金: {mm.GetMoney()}");
                mm.OnMoneyChanged += UpdateMoneyText;
            }
        }
    }

    void OnEnable()
    {
        if (mm == null) mm = MoneyManager.Instance;
        if (mm != null)
        {
            if (!cashed)
            {
                mm.Cash();
                cashed = true;
                UpdateMoneyText(mm.GetMoney());
                Debug.Log($"換金完了！ 現在の所持金: {mm.GetMoney()}");
            }
            else
            {
                UpdateMoneyText(mm.GetMoney());
            }
            mm.OnMoneyChanged += UpdateMoneyText;
        }
    }

    void OnDisable()
    {
        if (mm != null) mm.OnMoneyChanged -= UpdateMoneyText;
    }

    private void UpdateMoneyText(int value)
    {
        if (totalMoneyText != null)
        {
            totalMoneyText.text = value.ToString("N0");
        }
    }
}
