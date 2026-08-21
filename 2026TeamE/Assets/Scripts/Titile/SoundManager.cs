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

    // スライダーの位置(0〜1)。実際の音量は「この値 × maxVolume」で決まる
    private float bgmSliderValue;
    private float seSliderValue;

    private bool isInitializing = false;
    private CriAtomExPlayback? loopPlayback;
    private string currentLoopCueName = "";

    private const string BGM_VOLUME_SAVE_KEY = "SavedBGMSliderValue";
    private const string SE_VOLUME_SAVE_KEY = "SavedSESliderValue";

    // シングルトンの実装
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (bgmSource == null) bgmSource = GetComponent<CriAtomSource>();
            if (loopSeSource == null && seSource != null) loopSeSource = seSource.gameObject.AddComponent<CriAtomSource>();

            // 保存されたスライダー位置があればそれを使い、なければInspectorの初期位置を使う
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

    /// <summary>
    /// スライダー位置から実際のBGM音量を反映する
    /// </summary>
    private void ApplyBGMVolume()
    {
        if (bgmSource != null) bgmSource.volume = bgmSliderValue * maxBGMVolume;
    }

    /// <summary>
    /// スライダー位置から実際のSE音量を反映する
    /// </summary>
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

    //  BGMを再生するメソッド
    public void PlayBGM(string cueName)
    {
        bgmSource.Stop();
        bgmSource.cueName = cueName;
        bgmSource.Play();
    }

    // BGMを停止するメソッド
    public void StopBGM()
    {
        bgmSource.Stop();
    }

    // SEを再生するメソッド
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

    // SEを停止するメソッド
    public void StopSE()
    {
        seSource.Stop();
    }

    // BGMスライダーが動かされた時に呼ばれる（引数はスライダーの位置 0〜1）
    public void SetBGMVolume(float sliderValue)
    {
        if (isInitializing) return;

        bgmSliderValue = Mathf.Clamp01(sliderValue);
        ApplyBGMVolume();

        PlayerPrefs.SetFloat(BGM_VOLUME_SAVE_KEY, bgmSliderValue);
        PlayerPrefs.Save();
    }

    // SEスライダーが動かされた時に呼ばれる（引数はスライダーの位置 0〜1）
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

    // BGMの音量を取得するメソッド（実際に鳴っている音量）
    public float GetBGMVolume()
    {
        return bgmSource != null ? bgmSource.volume : 0f;
    }

    // SEの音量を取得するメソッド（実際に鳴っている音量）
    public float GetSEVolume()
    {
        return seSource != null ? seSource.volume : 0f;
    }

    // BGMが再生中かどうかを確認するメソッド
    public bool IsSEPlaying()
    {
        return seSource.status == CriAtomSourceBase.Status.Playing;
    }
}
