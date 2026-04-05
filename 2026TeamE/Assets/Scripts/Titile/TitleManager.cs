using UnityEngine;

public class TitleManager : MonoBehaviour
{
    [Header("OptionPanel")]
    [SerializeField] private GameObject optionPanel;

    public void Start()
    {
        CloseOptionPanel();
        SoundManager.Instance.PlayBGM("かえるのピアノ");
    }

    // オプションパネルを開くメソッド
    public void OpenOptionPanel()
    {
        optionPanel.SetActive(true);
        SoundManager.Instance.InitSlider();
    }

    // オプションパネルを閉じるメソッド
    public void CloseOptionPanel()
    {
        optionPanel.SetActive(false);
    }

    // ゲーム開始のメソッド
    public void StartGame()
    {
        SoundManager.Instance?.PlaySE("つるはしで掘る1");
        SceneLoader.Instance.LoadScene("02_Main");
    }
}
