using UnityEngine;
using TMPro;

public class MoneyUIInitializer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI onHandText;
    [SerializeField] private TextMeshProUGUI targetPartText;

    private MoneyManager mm;

    void Start()
    {
        // AwakeだとMoneyManagerの初期化より先に呼ばれてnullになる可能性があるため、Startでも取得を試みる
        if (mm == null)
        {
            mm = MoneyManager.Instance;
            if (mm != null)
            {
                mm.OnMoneyOnHandChanged += UpdateOnHandText;
                mm.OnTargetAmountOnPartChanged += UpdateTargetPartText;
                
                UpdateOnHandText(mm.GetMoneyOnHand());
                UpdateTargetPartText(mm.GetTargetAmountOnPart());
            }
        }
    }

    void OnEnable()
    {
        if (mm == null) mm = MoneyManager.Instance;
        if (mm == null) return;

        mm.OnMoneyOnHandChanged += UpdateOnHandText;
        mm.OnTargetAmountOnPartChanged += UpdateTargetPartText;

        UpdateOnHandText(mm.GetMoneyOnHand());
        UpdateTargetPartText(mm.GetTargetAmountOnPart());
    }

    void OnDisable()
    {
        if (mm == null) return;

        mm.OnMoneyOnHandChanged -= UpdateOnHandText;
        mm.OnTargetAmountOnPartChanged -= UpdateTargetPartText;
    }

    private void UpdateOnHandText(int value)
    {
        if (onHandText != null) onHandText.text = value.ToString("N0");
    }

    private void UpdateTargetPartText(int value)
    {
        if (targetPartText != null) targetPartText.text = value.ToString("N0");
    }
}
