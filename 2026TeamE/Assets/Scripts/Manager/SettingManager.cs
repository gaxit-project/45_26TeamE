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

    private readonly List<string> fixedResolutionOptions = new List<string>
    {
        "3840 x 2160",
        "2560 x 1440",
        "1920 x 1080",
        "1366 x 768",
    };

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            settingCanvas.SetActive(false);
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

    // 画面モードを変更するメソッド
    public void SetScreenMode(int index)
    {
        Screen.fullScreenMode = (index == 0) ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }
}
