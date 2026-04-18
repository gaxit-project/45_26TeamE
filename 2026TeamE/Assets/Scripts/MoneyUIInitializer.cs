using UnityEngine;
using TMPro;

public class MoneyUIInitializer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI onHandText;
    [SerializeField] private TextMeshProUGUI targetPartText;

    void Start()
    {
        MoneyManager mm = MoneyManager.Instance;
        if (mm != null)
        {
            // MoneyManagerに、このシーンの新しいUIを登録し直す関数を呼ぶ
            mm.SetMainSceneUI(onHandText, targetPartText);
        }
    }
}
