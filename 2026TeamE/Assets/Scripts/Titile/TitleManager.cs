using UnityEngine;

public class TitleManager : MonoBehaviour
{
    [Header("OptionPanel")]
    [SerializeField] private GameObject optionPanel;

    public void Start()
    {
        SettingManager.Instance?.CloseSettingPanel(false);
        SoundManager.Instance.PlayBGM("かえるのピアノ");
    }

    // オプションパネルを開くメソッド
    public void OpenOptionPanel()
    {
        SettingManager.Instance?.OpenSettingPanel();
    }

    // オプションパネルを閉じるメソッド
    public void CloseOptionPanel()
    {
        SettingManager.Instance?.CloseSettingPanel();
    }

    // ゲーム開始のメソッド
    public void StartGame()
    {
        SoundManager.Instance?.PlaySE("つるはしで掘る1");
        SceneLoader.Instance.LoadScene("02_Main");
    }
}
