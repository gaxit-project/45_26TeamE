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

    private float bgmSliderValue;
    private float seSliderValue;

    private bool isInitializing = false;
    private CriAtomExPlayback? loopPlayback;
    private string currentLoopCueName = "";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (bgmSource == null) bgmSource = GetComponent<CriAtomSource>();
            if (loopSeSource == null)
            {
                if (seSource != null)
                {
                    loopSeSource = seSource.gameObject.AddComponent<CriAtomSource>();
                }
                else
                {
                    loopSeSource = gameObject.AddComponent<CriAtomSource>();
                }
            }

            if (loopSeSource != null)
            {
                string cueSheetName = (seSource != null && !string.IsNullOrEmpty(seSource.cueSheet)) ? seSource.cueSheet : "CueSheet_SE";
                loopSeSource.cueSheet = cueSheetName;
                loopSeSource.use3dPositioning = false;
                loopSeSource.loop = true;
            }

            bgmSliderValue = SaveManager.Settings.bgmVolume;
            seSliderValue = SaveManager.Settings.seVolume;

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

        if (string.IsNullOrEmpty(loopSeSource.cueSheet))
        {
            string cueSheetName = (seSource != null && !string.IsNullOrEmpty(seSource.cueSheet)) ? seSource.cueSheet : "CueSheet_SE";
            loopSeSource.cueSheet = cueSheetName;
        }

        loopSeSource.loop = true;
        loopSeSource.use3dPositioning = false;

        float volume = seSliderValue * maxSEVolume;
        if (loopSeSource.volume != volume)
        {
            loopSeSource.volume = volume;
        }

        if (currentLoopCueName == cueName && (loopSeSource.status == CriAtomSourceBase.Status.Playing || loopSeSource.status == CriAtomSourceBase.Status.Prep))
        {
            return;
        }

        currentLoopCueName = cueName;
        loopSeSource.cueName = cueName;
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

        SaveManager.Settings.bgmVolume = bgmSliderValue;
    }

    public void SetSEVolume(float sliderValue)
    {
        if (isInitializing) return;

        seSliderValue = Mathf.Clamp01(sliderValue);
        ApplySEVolume();

        SaveManager.Settings.seVolume = seSliderValue;
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
