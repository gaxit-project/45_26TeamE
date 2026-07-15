using UnityEngine;
using CriWare;
using UnityEngine.UI;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("CriAtomSource")]
    [SerializeField] private CriAtomSource bgmSource;
    [SerializeField] private CriAtomSource seSource;

    [Header("Slider")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;

    [Header("Volume Settings")]
    [SerializeField] private float defaultBGMVolume = 0.2f;
    [SerializeField] private float defaultSEVolume = 0.2f;

    private bool isInitializing = false;
    private CriAtomExPlayback? loopPlayback;
    private string currentLoopCueName = "";

    // シングルトンの実装
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if(bgmSource == null) bgmSource = GetComponent<CriAtomSource>();
            if(bgmSource != null) bgmSource.volume = defaultBGMVolume;
            if(seSource != null) seSource.volume = defaultSEVolume;
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

            // スライダーのイベントリスナーをリセットして、現在の音量に合わせてスライダーの値を更新
            bgmSlider.onValueChanged.RemoveListener(SetBGMVolume);
            bgmSlider.value = bgmSource.volume;
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        }

        if (seSlider != null)
        {
            seSlider.minValue = 0f;
            seSlider.maxValue = 1f;

            // SEのスライダーも同様にリセットして更新
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
        // 既に同じ音が鳴っている（または準備中）なら何もしない
        if(currentLoopCueName == cueName) return;
        
        // 別のループ音が鳴っていれば確実に止める
        if (loopPlayback.HasValue)
        {
            try { loopPlayback.Value.Stop(); } catch (System.Exception) { }
        }

        currentLoopCueName = cueName;
        if (seSource != null)
        {
            try { loopPlayback = seSource.Play(cueName); } catch (System.Exception) { loopPlayback = null; }
        }
    }

    public void StopLoopSE()
    {
        // 状態に関わらず確実に止める
        if (loopPlayback.HasValue)
        {
            try { loopPlayback.Value.Stop(); } catch (System.Exception) { }
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
        bgmSource.volume = volume;
    }

    // SEの音量を変更するメソッド
    public void SetSEVolume(float volume)
    {
        if(isInitializing) return;
        seSource.volume = volume;
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
