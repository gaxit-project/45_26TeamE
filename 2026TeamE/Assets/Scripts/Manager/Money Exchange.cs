using UnityEngine;
using TMPro;

public class MoneyExchange : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI totalMoneyText;

    private MoneyManager mm;

    void Awake()
    {
        mm = MoneyManager.Instance;

        if (mm != null)
        {
            // 換金処理を実行
            mm.Cash();
            UpdateMoneyText(mm.GetMoney());

            Debug.Log($"換金完了！ 現在の所持金: {mm.GetMoney()}");
        }
    }

    void OnEnable()
    {
        if (mm != null) mm.OnMoneyChanged += UpdateMoneyText;
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
