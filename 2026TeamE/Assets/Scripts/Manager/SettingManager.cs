using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[System.Serializable]
public class FrameRateOption
{
    public string Label;
    public int Value;
}

public class SettingManager : MonoBehaviour
{
    public static SettingManager Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject settingCanvas;

    [Header("Dropdowns")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown screenModeDropdown;
    [SerializeField] private TMP_Dropdown framerateDropdown;

    [Header("FPS Options")]
    [SerializeField]
    private List<FrameRateOption> fpsOptions = new List<FrameRateOption>
    {
        new FrameRateOption { Label = "30 FPS", Value = 30 },
        new FrameRateOption { Label = "60 FPS", Value = 60 },
        new FrameRateOption { Label = "120 FPS", Value = 120 },
        new FrameRateOption { Label = "144 FPS", Value = 144 },
        new FrameRateOption { Label = "240 FPS", Value = 240 },
        new FrameRateOption { Label = "無制限", Value = -1 }
    };
    [SerializeField] private int defaultFramerateIndex = 1;

    private readonly List<string> fixedResolutionOptions = new List<string>
    {
        "1366 x 768",
        "1920 x 1080",
        "2560 x 1440",
        "3840 x 2160",
    };
    private const string FPS_SAVE_KEY = "SavedFPSIndex";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            int savedFPSIndex = PlayerPrefs.GetInt(FPS_SAVE_KEY, defaultFramerateIndex);
            settingCanvas.SetActive(false);
            SetFrameRate(savedFPSIndex);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    
    public void OpenSettingPanel()
    {
        settingCanvas.SetActive(true);
        SoundManager.Instance?.PlaySE("つるはしで掘る1");
        SoundManager.Instance?.InitSlider();
        InitResolutionSettings();
        InitFrameRateSettings();

        if (EventSystem.current != null && resolutionDropdown != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(resolutionDropdown.gameObject);
        }
    }

    
    public void CloseSettingPanel(bool playSound = true)
    {
        settingCanvas.SetActive(false);
        if (playSound)
        {
            SoundManager.Instance?.PlaySE("つるはしで掘る3");
        }
    }

    
    public void InitResolutionSettings()
    {
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(fixedResolutionOptions);
        int currentResIndex = 0;
        string currentResStr = $"{Screen.width} x {Screen.height}";

        for (int i = 0; i < fixedResolutionOptions.Count; i++)
        {
            if(fixedResolutionOptions[i] == currentResStr)
            {
                currentResIndex = i;
                break;
            }
        }
        resolutionDropdown.value = currentResIndex;
        resolutionDropdown.RefreshShownValue();
    }

    
    public void InitFrameRateSettings()
    {
        framerateDropdown.ClearOptions();
        
        List<string> labels = fpsOptions.Select(x => x.Label).ToList();
        framerateDropdown.AddOptions(labels);
        
        int currentFPS = Application.targetFrameRate;
        int currentIndex = fpsOptions.FindIndex(x => x.Value == currentFPS);

        framerateDropdown.value = (currentIndex != -1) ? currentIndex : defaultFramerateIndex;
        framerateDropdown.RefreshShownValue();
    }

    
    public void SetResolution(int index)
    {
        string selectedText = fixedResolutionOptions[index];
        string[] parts = selectedText.Split('x');

        if(parts.Length == 2)
        {
            int width = int.Parse(parts[0].Trim());
            int height = int.Parse(parts[1].Trim());
            Screen.SetResolution(width, height, Screen.fullScreenMode);
        }
    }

    
    public void SetFrameRate(int index)
    {
        if(index < 0 || index >= fpsOptions.Count)
        {
            Debug.LogWarning("Invalid FPS index: " + index);
            return;
        }
        int targetFPS = fpsOptions[index].Value;
        Application.targetFrameRate = targetFPS;
        PlayerPrefs.SetInt(FPS_SAVE_KEY, index);
        PlayerPrefs.Save();
    }

    
    public void SetScreenMode(int index)
    {
        Screen.fullScreenMode = (index == 0) ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }
}
