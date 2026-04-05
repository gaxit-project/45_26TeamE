using UnityEngine;

public class TitleManager : MonoBehaviour
{
    [Header("OptionPanel")]
    [SerializeField] private GameObject optionPanel;

    public void Start()
    {
        CloseOptionPanelNoSound();
        SoundManager.Instance.PlayBGM("かえるのピアノ");
    }

    // オプションパネルを開くメソッド
    public void OpenOptionPanel()
    {
        SoundManager.Instance?.PlaySE("つるはしで掘る1");
        optionPanel.SetActive(true);
        SoundManager.Instance.InitSlider();
    }

    // オプションパネルを閉じるメソッド
    public void CloseOptionPanel()
    {
        SoundManager.Instance?.PlaySE("つるはしで掘る3");
        optionPanel.SetActive(false);
    }

    public void CloseOptionPanelNoSound()
    {
        optionPanel.SetActive(false);
    }

    public void OnSEDrugEnd()
    {
        SoundManager.Instance?.PlaySERestart("つるはしで掘る1");
    }

    // ゲーム開始のメソッド
    public void StartGame()
    {
        SoundManager.Instance?.PlaySE("つるはしで掘る1");
        SceneLoader.Instance.LoadScene("02_Main");
    }
}
