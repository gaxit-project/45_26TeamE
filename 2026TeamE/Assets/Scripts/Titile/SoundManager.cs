using UnityEngine;
using CriWare;
using UnityEngine.UI;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("CriAtomSource")]
    [SerializeField] private CriAtomSource bgmSource;
    [SerializeField] private CriAtomSource seSource;
    [SerializeField] private CriAtomSource loopSeSource;

    [Header("Slider")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;

    [Header("Volume Settings")]
    [Tooltip("スライダーを最大(100%)にした時の実際の音量。全体的に音がデカい場合はここを下げる")]
    [Range(0f, 1f)][SerializeField] private float maxBGMVolume = 0.3f;
    [Tooltip("スライダーを最大(100%)にした時の実際の音量。全体的に音がデカい場合はここを下げる")]
    [Range(0f, 1f)][SerializeField] private float maxSEVolume = 0.3f;

    [Tooltip("初期状態のスライダー位置(0〜1)。0.5でちょうど真ん中")]
    [Range(0f, 1f)][SerializeField] private float defaultBGMSlider = 0.5f;
    [Tooltip("初期状態のスライダー位置(0〜1)。0.5でちょうど真ん中")]
    [Range(0f, 1f)][SerializeField] private float defaultSESlider = 0.5f;

    
    private float bgmSliderValue;
    private float seSliderValue;

    private bool isInitializing = false;
    private CriAtomExPlayback? loopPlayback;
    private string currentLoopCueName = "";

    private const string BGM_VOLUME_SAVE_KEY = "SavedBGMSliderValue";
    private const string SE_VOLUME_SAVE_KEY = "SavedSESliderValue";

    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (bgmSource == null) bgmSource = GetComponent<CriAtomSource>();
            if (loopSeSource == null && seSource != null) loopSeSource = seSource.gameObject.AddComponent<CriAtomSource>();

            
            bgmSliderValue = PlayerPrefs.GetFloat(BGM_VOLUME_SAVE_KEY, defaultBGMSlider);
            seSliderValue = PlayerPrefs.GetFloat(SE_VOLUME_SAVE_KEY, defaultSESlider);

            ApplyBGMVolume();
            ApplySEVolume();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitSlider();
    }

    
    
    
    private void ApplyBGMVolume()
    {
        if (bgmSource != null) bgmSource.volume = bgmSliderValue * maxBGMVolume;
    }

    
    
    
    private void ApplySEVolume()
    {
        float volume = seSliderValue * maxSEVolume;
        if (seSource != null) seSource.volume = volume;
        if (loopSeSource != null) loopSeSource.volume = volume;
    }

    public void InitSlider()
    {
        isInitializing = true;
        if (bgmSlider != null)
        {
            bgmSlider.minValue = 0f;
            bgmSlider.maxValue = 1f;
            bgmSlider.onValueChanged.RemoveListener(SetBGMVolume);
            bgmSlider.value = bgmSliderValue;
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        }

        if (seSlider != null)
        {
            seSlider.minValue = 0f;
            seSlider.maxValue = 1f;
            seSlider.onValueChanged.RemoveListener(SetSEVolume);
            seSlider.value = seSliderValue;
            seSlider.onValueChanged.AddListener(SetSEVolume);
        }

        isInitializing = false;
    }

    
    public void PlayBGM(string cueName)
    {
        bgmSource.Stop();
        bgmSource.cueName = cueName;
        bgmSource.Play();
    }

    
    public void StopBGM()
    {
        bgmSource.Stop();
    }

    
    public void PlaySE(string cueName)
    {
        seSource.Play(cueName);
    }

    public void PlayLoopSE(string cueName)
    {
        if (loopSeSource == null) return;
        if (currentLoopCueName == cueName && loopSeSource.status == CriAtomSourceBase.Status.Playing)
        {
            return;
        }
        currentLoopCueName = cueName;
        loopPlayback = loopSeSource.Play(cueName);
    }

    public void StopLoopSE()
    {
        if (loopSeSource != null)
        {
            loopSeSource.Stop();
        }
        if (loopPlayback.HasValue)
        {
            try { loopPlayback.Value.Stop(true); } catch (System.Exception) { }
            loopPlayback = null;
        }
        currentLoopCueName = "";
    }

    
    public void StopSE()
    {
        seSource.Stop();
    }

    
    public void SetBGMVolume(float sliderValue)
    {
        if (isInitializing) return;

        bgmSliderValue = Mathf.Clamp01(sliderValue);
        ApplyBGMVolume();

        PlayerPrefs.SetFloat(BGM_VOLUME_SAVE_KEY, bgmSliderValue);
        PlayerPrefs.Save();
    }

    
    public void SetSEVolume(float sliderValue)
    {
        if (isInitializing) return;

        seSliderValue = Mathf.Clamp01(sliderValue);
        ApplySEVolume();

        PlayerPrefs.SetFloat(SE_VOLUME_SAVE_KEY, seSliderValue);
        PlayerPrefs.Save();
    }

    public void PlaySERestart(string cueName)
    {
        seSource.Stop();
        seSource.Play(cueName);
    }

    
    public float GetBGMVolume()
    {
        return bgmSource != null ? bgmSource.volume : 0f;
    }

    
    public float GetSEVolume()
    {
        return seSource != null ? seSource.volume : 0f;
    }

    
    public bool IsSEPlaying()
    {
        return seSource.status == CriAtomSourceBase.Status.Playing;
    }
}
