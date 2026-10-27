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
    public bool IsOpen => settingCanvas != null && settingCanvas.activeSelf;

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

    private readonly List<string> fixedResolutionOptions = new List<string>
    {
        "1366 x 768",
        "1920 x 1080",
        "2560 x 1440",
        "3840 x 2160",
    };

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            settingCanvas.SetActive(false);
            Application.targetFrameRate = LoadFrameRate();
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
        InitScreenModeSettings();
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
        SaveManager.SaveSettings();
        if (playSound)
        {
            SoundManager.Instance?.PlaySE("つるはしで掘る3");
        }
    }

    private void OnApplicationQuit()
    {
        SaveManager.SaveSettings();
    }

    /// <summary>
    /// このモニタで実際に表示できる解像度だけを選択肢に残したもの。
    /// </summary>
    /// <remarks>
    /// モニタより大きい解像度を指定すると、OS側でウィンドウが切り詰められて
    /// アスペクト比が崩れ、カメラの映る範囲が変わって背景が欠けるなどの不具合になる。
    /// </remarks>
    private List<string> availableResolutionOptions = new List<string>();

    public void InitResolutionSettings()
    {
        BuildAvailableResolutionOptions();

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(availableResolutionOptions);

        int currentResIndex = 0;
        string currentResStr = $"{Screen.width} x {Screen.height}";

        for (int i = 0; i < availableResolutionOptions.Count; i++)
        {
            if (availableResolutionOptions[i] == currentResStr)
            {
                currentResIndex = i;
                break;
            }
        }
        resolutionDropdown.SetValueWithoutNotify(currentResIndex);
    }

    private void BuildAvailableResolutionOptions()
    {
        availableResolutionOptions.Clear();

        int maxWidth = Display.main.systemWidth;
        int maxHeight = Display.main.systemHeight;

        foreach (string option in fixedResolutionOptions)
        {
            if (!TryParseResolution(option, out int width, out int height)) continue;
            if (width > maxWidth || height > maxHeight) continue;

            availableResolutionOptions.Add(option);
        }

        // モニタが小さすぎてどれも選べない場合は、一番小さいものだけ残す
        if (availableResolutionOptions.Count == 0 && fixedResolutionOptions.Count > 0)
        {
            availableResolutionOptions.Add(fixedResolutionOptions[0]);
        }
    }

    private static bool TryParseResolution(string text, out int width, out int height)
    {
        width = 0;
        height = 0;

        string[] parts = text.Split('x');
        if (parts.Length != 2) return false;

        return int.TryParse(parts[0].Trim(), out width) && int.TryParse(parts[1].Trim(), out height);
    }

    public void InitFrameRateSettings()
    {
        framerateDropdown.ClearOptions();

        framerateDropdown.AddOptions(fpsOptions.Select(x => x.Label).ToList());
        int currentIndex = fpsOptions.FindIndex(x => x.Value == Application.targetFrameRate);
        if(currentIndex < 0)
        {
            currentIndex = fpsOptions.FindIndex(x => x.Value == SettingsData.DefaultFrameRate);
        }

        framerateDropdown.SetValueWithoutNotify(Mathf.Max(currentIndex, 0));
    }

    public void InitScreenModeSettings()
    {
        int index = (Screen.fullScreenMode == FullScreenMode.Windowed) ? 1 : 0;
        screenModeDropdown.SetValueWithoutNotify(index);
    }

    public void SetResolution(int index)
    {
        if (index < 0 || index >= availableResolutionOptions.Count) return;

        if (TryParseResolution(availableResolutionOptions[index], out int width, out int height))
        {
            Screen.SetResolution(width, height, Screen.fullScreenMode);
        }
    }

    private int LoadFrameRate(){
        int saved = SaveManager.Settings.frameRate;
        return fpsOptions.Exists(x => x.Value == saved) ? saved : SettingsData.DefaultFrameRate;
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
        SaveManager.Settings.frameRate = targetFPS;
    }

    public void SetScreenMode(int index)
    {
        Screen.fullScreenMode = (index == 0) ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }
}
