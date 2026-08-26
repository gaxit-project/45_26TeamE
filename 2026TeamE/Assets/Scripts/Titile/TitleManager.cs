using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TitleManager : MonoBehaviour
{
    [Header("UI Objects")]
    [SerializeField] private GameObject pressAnyButtonText;
    [SerializeField] private Animator mainMenuAnimator;
    [SerializeField] private GameObject firstSelectedButton;

    [Header("OptionPanel")]
    [SerializeField] private GameObject optionPanel;
    [SerializeField] private GameObject optionFirstSelected;

    private bool isWaitingInput = true;

    public void Start()
    {
        ResetGameState();
        SettingManager.Instance?.CloseSettingPanel(false);
        SoundManager.Instance.PlayBGM("Virtual_Adventure_2");
        pressAnyButtonText.SetActive(true);

        // 再読み込み時に生き残ったSettingManager内のCloseButtonの参照先を新しいTitleManagerに繋ぎ直す
        if (SettingManager.Instance != null)
        {
            var buttons = SettingManager.Instance.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.gameObject.name == "CloseButton")
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(CloseOptionPanel);
                }
            }
        }
    }

    private void ResetGameState()
    {
        // ゲーム全体の進行状態の一括リセットはResetManagerに集約
        ResetManager.ResetAll();
    }

    private void Update()
    {
        if(!isWaitingInput)
        {
            return;
        }

        bool keyboardPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;

        bool gamepadPressed = false;
        if (Gamepad.current != null)
        {
            foreach(var control in Gamepad.current.allControls)
            {
                if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame)
                {
                    gamepadPressed = true;
                    break;
                }
            }
        }

        if(keyboardPressed || gamepadPressed)
        {
            PushToStart();
        }
    }

    // 入力待ち状態で、現在選択されているUI要素がない場合に最初のボタンを選択する
    private void LateUpdate()
    {
        if(EventSystem.current.currentSelectedGameObject == null && !isWaitingInput)
        {
            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        }
    }

    // スタート画面のアニメーションを開始するメソッド
    private void PushToStart()
    {
        isWaitingInput = false;
        pressAnyButtonText.SetActive(false);
        mainMenuAnimator.SetTrigger("SlideIn");
        SoundManager.Instance?.PlaySE("つるはしで掘る1");
        Invoke(nameof(SelectFirstButton), 0.5f);
    }

    // 最初のボタンを選択するメソッド
    private void SelectFirstButton()
    {
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
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
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
    }

    // ゲーム開始のメソッド
    public void StartGame()
    {
        SoundManager.Instance?.PlaySE("つるはしで掘る1");

        // タイトルからの開始時だけ、ローディング画面で説明を読ませてからスタートさせる
        SceneLoader.Instance.LoadScene("02_Main", true);
    }

    // ゲーム終了のメソッド
    public void QuitGame()
    {
        SoundManager.Instance?.PlaySE("つるはしで掘る1");
        //UnityEditor.EditorApplication.isPlaying = false;
        Application.Quit();
    }
}
