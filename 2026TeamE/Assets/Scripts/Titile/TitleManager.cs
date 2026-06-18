using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TitleManager : MonoBehaviour
{
    [Header("UI Objects")]
    [SerializeField] private GameObject pressAnyButtonText;
    [SerializeField] private GameObject modeSelectionPanel;
    [SerializeField] private GameObject firstSelectedButton;

    [Header("Fade Settings")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 1f;

    [Header("OptionPanel")]
    [SerializeField] private GameObject optionPanel;
    [SerializeField] private GameObject optionFirstSelected;

    private bool isWaitingInput = true;

    public void Start()
    {
        SettingManager.Instance?.CloseSettingPanel(false);
        SoundManager.Instance.PlayBGM("Virtual_Adventure_2");
        pressAnyButtonText.SetActive(true);
        modeSelectionPanel.SetActive(false);
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    private void Update()
    {
        if (!isWaitingInput) return;
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
            isWaitingInput = false;
            StartCoroutine(TransitionToModeSelection());
        }
    }

    // タイトル画面からモード選択画面への遷移を開始するメソッド
    private IEnumerator TransitionToModeSelection()
    {
        isWaitingInput = false;
        SoundManager.Instance?.PlaySE("つるはしで掘る1");

        fadeCanvasGroup.blocksRaycasts = true;
        float elapsedTime = 0f;
        while(elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
            yield return null;
        }
        pressAnyButtonText.SetActive(false);
        modeSelectionPanel.SetActive(true);
        yield return new WaitForSeconds(0.1f);

        elapsedTime = 0f;
        while(elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            fadeCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsedTime / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.blocksRaycasts = false;

        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
    }

    // 入力待ち状態で、現在選択されているUI要素がない場合に最初のボタンを選択する
    private void LateUpdate()
    {
        if(EventSystem.current.currentSelectedGameObject == null && !isWaitingInput)
        {
            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        }
    }

    // オプションパネルを開くメソッド
    public void OpenOptionPanel()
    {
        SettingManager.Instance?.OpenSettingPanel();
        EventSystem.current.SetSelectedGameObject(optionFirstSelected);
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
        SceneLoader.Instance.LoadScene("02_Main");
    }

    // ゲーム終了のメソッド
    public void QuitGame()
    {
        SoundManager.Instance?.PlaySE("つるはしで掘る1");
        //UnityEditor.EditorApplication.isPlaying = false;
        Application.Quit();
    }
}
