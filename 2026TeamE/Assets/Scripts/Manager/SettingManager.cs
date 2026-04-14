using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SettingManager : MonoBehaviour
{
    public static SettingManager Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject settingCanvas;

    [Header("Dropdowns")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown screenModeDropdown;
    [SerializeField] private TMP_Dropdown framerateDropdown;

    private readonly List<string> fixedResolutionOptions = new List<string>
    {
        "3840 x 2160",
        "2560 x 1440",
        "1920 x 1080",
        "1366 x 768",
    };

    private readonly List<string> fixedFramerateOptions = new List<string>
    {
        "30 FPS",
        "60 FPS",
        "120 FPS",
        "144 FPS",
        "240 FPS",
        "無制限"
    };

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            settingCanvas.SetActive(false);

            if(Application.targetFrameRate <= 0)
            {
                Application.targetFrameRate = 60;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 設定パネルを開くメソッド
    public void OpenSettingPanel()
    {
        settingCanvas.SetActive(true);
        SoundManager.Instance?.PlaySE("つるはしで掘る1");
        SoundManager.Instance?.InitSlider();
        InitResolutionSettings();
        InitFrameRateSettings();
    }

    // 設定パネルを閉じるメソッド
    public void CloseSettingPanel(bool playSound = true)
    {
        settingCanvas.SetActive(false);
        if (playSound)
        {
            SoundManager.Instance?.PlaySE("つるはしで掘る3");
        }
    }

    // 解像度の選択肢を初期化するメソッド
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

    // フレームレートの選択肢を初期化するメソッド
    public void InitFrameRateSettings()
    {
        framerateDropdown.ClearOptions();
        framerateDropdown.AddOptions(fixedFramerateOptions);
        
        int currentIndex = 0;
        int currentTarget = Application.targetFrameRate;

        if(currentTarget <= 0)
        {
            currentTarget = fixedFramerateOptions.Count - 1;
        }
        else
        {
            for(int i = 0; i < fixedFramerateOptions.Count; i++)
            {
                if(int.TryParse(fixedFramerateOptions[i], out int val) && val == currentTarget)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        framerateDropdown.value = currentIndex;
        framerateDropdown.RefreshShownValue();
    }

    // 解像度を変更するメソッド
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

    // フレームレートを変更するメソッド
    public void SetFrameRate(int index)
    {
        if (index == fixedFramerateOptions.Count - 1)
        {
            Application.targetFrameRate = -1;
        }
        else
        {
            string selectedText = fixedFramerateOptions[index];
            if (int.TryParse(selectedText.Replace(" FPS", "").Trim(), out int targetFPS))
            {
                Application.targetFrameRate = targetFPS;
            }
        }
    }

    // 画面モードを変更するメソッド
    public void SetScreenMode(int index)
    {
        Screen.fullScreenMode = (index == 0) ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }
}
