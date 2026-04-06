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

    private bool isInitializing = false;

    // シングルトンの実装
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if(bgmSource == null) bgmSource = GetComponent<CriAtomSource>();
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

        // スライダーのイベントリスナーをリセットして、現在の音量に合わせてスライダーの値を更新
        bgmSlider.onValueChanged.RemoveListener(SetBGMVolume);
        bgmSlider.value = bgmSource.volume;
        bgmSlider.onValueChanged.AddListener(SetBGMVolume);

        // SEのスライダーも同様にリセットして更新
        seSlider.onValueChanged.RemoveListener(SetSEVolume);
        seSlider.value = seSource.volume;
        seSlider.onValueChanged.AddListener(SetSEVolume);

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
}
