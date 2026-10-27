/// <summary>
/// 設定画面で変更できる値。settings.jsonに保存される。
/// </summary>
[System.Serializable]
public class SettingsData
{
    /// <summary>
    /// データの形を変えたら1増やしてSaveManagerに変換処理を書く
    /// </summary>
    public const int CurrentVersion = 1;

    public const int DefaultFrameRate = 60;
    public const float DefaultVolume = 0.5f;

    public int version = CurrentVersion;
    public int frameRate = DefaultFrameRate;
    public float bgmVolume = DefaultVolume;
    public float seVolume = DefaultVolume;
}
