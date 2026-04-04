using UnityEngine;

public class TitleManager : MonoBehaviour
{
    [Header("OptionPanel")]
    [SerializeField] private GameObject optionPanel;

    public void Start()
    {
        // タイトル画面のBGMを再生
        SoundManager.Instance.PlayBGM("かえるのピアノ");
    }

    // オプションパネルを開くメソッド
    public void OpenOptionPanel()
    {
        optionPanel.SetActive(true);
    }

    // オプションパネルを閉じるメソッド
    public void CloseOptionPanel()
    {
        optionPanel.SetActive(false);
    }
}
