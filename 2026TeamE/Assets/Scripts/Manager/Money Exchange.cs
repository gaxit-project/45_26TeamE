using UnityEngine;
using TMPro;

public class MoneyExchange : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI totalMoneyText; // このシーンでの所持金表示用

    void Awake()
    {
        MoneyManager mm = MoneyManager.Instance;

        if (mm != null)
        {
            // 1. このシーンのUIをマネージャーに登録（これでmm内部のMoneyTextが更新される）
            mm.SetMoneyText(totalMoneyText);

            // 2. 換金処理を実行（OnHandをMoneyに合算）
            mm.Cash();

            // 3. マネージャーに表示を更新させる
            mm.UpdateTotalMoneyText();

            Debug.Log($"換金完了！ 現在の総資産: {mm.GetMoney()}");
        }
    }
}
