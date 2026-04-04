using UnityEngine;
using CriWare;
using UnityEditor;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private CriAtomSource bgmSource;
    [SerializeField] private CriAtomSource seSource;

    // シングルトンの実装
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            bgmSource = GetComponent<CriAtomSource>();
        }
        else
        {
            Destroy(gameObject);
        }
    }


    //  BGMを再生するメソッド
    public void PlayBGM(string cueName)
    {
        bgmSource.Stop();
        bgmSource.cueName = cueName;
        bgmSource.Play();
    }

    // SEを再生するメソッド
    public void PlaySE(string cueName)
    {
        seSource.Play(cueName);
    }

    // BGMの音量を変更するメソッド
    public void SetBGMVolume(float volume)
    {
        bgmSource.volume = volume;
    }

    // SEの音量を変更するメソッド
    public void SetSEVolume(float volume)
    {
        seSource.volume = volume;

        // SEの音量を変更した時はSEを再生する
        if (!seSource.status.ToString().Contains("Playing"))
        {
            // PlaySE();
        }
    }
}
