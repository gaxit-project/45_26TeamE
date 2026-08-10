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
    [SerializeField] private float defaultBGMVolume = 0.2f;
    [SerializeField] private float defaultSEVolume = 0.2f;

    private bool isInitializing = false;
    private CriAtomExPlayback? loopPlayback;
    private string currentLoopCueName = "";

    private const string BGM_VOLUME_SAVE_KEY = "SavedBGMVolume";
    private const string SE_VOLUME_SAVE_KEY = "SavedSEVolume";

    // シングルトンの実装
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if(bgmSource == null) bgmSource = GetComponent<CriAtomSource>();

            // 保存された音量があればそれを使い、なければInspectorのデフォルト値を使う
            float savedBGMVolume = PlayerPrefs.GetFloat(BGM_VOLUME_SAVE_KEY, defaultBGMVolume);
            float savedSEVolume = PlayerPrefs.GetFloat(SE_VOLUME_SAVE_KEY, defaultSEVolume);

            if(bgmSource != null) bgmSource.volume = savedBGMVolume;
            if(seSource != null) seSource.volume = savedSEVolume;
            if (loopSeSource == null && seSource != null) loopSeSource = seSource.gameObject.AddComponent<CriAtomSource>();
            if (loopSeSource != null) loopSeSource.volume = savedSEVolume;
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

    public void InitSlider()
    {
        isInitializing = true;
        if (bgmSlider != null)
        {
            bgmSlider.minValue = 0f;
            bgmSlider.maxValue = 1f;
            bgmSlider.onValueChanged.RemoveListener(SetBGMVolume);
            bgmSlider.value = bgmSource.volume;
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        }

        if (seSlider != null)
        {
            seSlider.minValue = 0f;
            seSlider.maxValue = 1f;
            seSlider.onValueChanged.RemoveListener(SetSEVolume);
            seSlider.value = seSource.volume;
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

    // BGMの音量を変更するメソッド
    public void SetBGMVolume(float volume)
    {
        if (bgmSource != null) bgmSource.volume = volume;

        PlayerPrefs.SetFloat(BGM_VOLUME_SAVE_KEY, volume);
        PlayerPrefs.Save();
    }

    // SEの音量を変更するメソッド
    public void SetSEVolume(float volume)
    {
        if(isInitializing) return;
        seSource.volume = volume;
        if (loopSeSource != null) loopSeSource.volume = volume;

        PlayerPrefs.SetFloat(SE_VOLUME_SAVE_KEY, volume);
        PlayerPrefs.Save();
    }

    public void PlaySERestart(string cueName)
    {
        seSource.Stop();
        seSource.Play(cueName);
    }

    // BGMの音量を取得するメソッド
    public float GetBGMVolume()
    {
        return bgmSource.volume;
    }

    // SEの音量を取得するメソッド
    public float GetSEVolume()
    {
        return seSource.volume;
    }

    // BGMが再生中かどうかを確認するメソッド
    public bool IsSEPlaying()
    {
        return seSource.status == CriAtomSourceBase.Status.Playing;
    }
}
