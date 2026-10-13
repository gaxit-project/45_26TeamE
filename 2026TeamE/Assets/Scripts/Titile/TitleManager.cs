using System.Collections;
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
    private UnityEngine.InputSystem.Controls.ButtonControl[] cachedGamepadButtons;
    private Gamepad cachedGamepad;

    public void Start()
    {
        ResetGameState();
        SettingManager.Instance?.CloseSettingPanel(false);
        SoundManager.Instance.PlayBGM("Virtual_Adventure_2");
        pressAnyButtonText.SetActive(true);

        
        if (SettingManager.Instance != null)
        {
            var buttons = SettingManager.Instance.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.gameObject.name == "CloseButton")
                {
                    btn.onClick.RemoveListener(CloseOptionPanel);
                    btn.onClick.AddListener(CloseOptionPanel);
                }
            }
        }
    }

    private void ResetGameState()
    {
        
        ResetManager.ResetAll();
    }

    private void Update()
    {
        if(!isWaitingInput)
        {
            return;
        }

        bool keyboardPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;

        bool gamepadPressed = CheckGamepadPressed();

        if (keyboardPressed || gamepadPressed)
        {
            PushToStart();
        }
    }

    private bool CheckGamepadPressed()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) return false;

        if (cachedGamepad != pad)
        {
            cachedGamepad = pad;
            cachedGamepadButtons = pad.allControls.OfType<UnityEngine.InputSystem.Controls.ButtonControl>().ToArray();
        }

        if (cachedGamepadButtons != null)
        {
            for (int i = 0; i < cachedGamepadButtons.Length; i++)
            {
                if (cachedGamepadButtons[i].wasPressedThisFrame) return true;
            }
        }
        return false;
    }

    private void LateUpdate()
    {
        if (isWaitingInput || EventSystem.current == null) return;
        if (SettingManager.Instance != null && SettingManager.Instance.IsOpen) return;

        if (EventSystem.current.currentSelectedGameObject == null)
        {
            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        }
    }

    
    private void PushToStart()
    {
        isWaitingInput = false;
        pressAnyButtonText.SetActive(false);
        mainMenuAnimator.SetTrigger("SlideIn");
        SoundManager.Instance?.PlaySE("つるはしで掘る1");

        // Invoke は Time.timeScale の影響を受けるため、0 のまま戻ってくると永久に発火せず、
        // ボタンが1つも選択されないまま操作不能になる。実時間で待つ。
        StartCoroutine(SelectFirstButtonAfterDelay(0.5f));
    }

    private IEnumerator SelectFirstButtonAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        SelectFirstButton();
    }

    
    private void SelectFirstButton()
    {
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
    }

    
    public void OpenOptionPanel()
    {
        SettingManager.Instance?.OpenSettingPanel();
    }

    
    public void CloseOptionPanel()
    {
        SettingManager.Instance?.CloseSettingPanel();
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
    }

    
    public void StartGame()
    {
        SoundManager.Instance?.PlaySE("つるはしで掘る1");

        
        SceneLoader.Instance.LoadScene("02_Main", true);
    }

    
    public void QuitGame()
    {
        SoundManager.Instance?.PlaySE("つるはしで掘る1");
        Application.Quit();
    }
}
