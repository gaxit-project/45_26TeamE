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

    
    private void LateUpdate()
    {
        if(EventSystem.current.currentSelectedGameObject == null && !isWaitingInput)
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
        Invoke(nameof(SelectFirstButton), 0.5f);
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
