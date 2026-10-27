#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CriWare;
using UnityEngine;

/// <summary>
/// デバッグ中（エディターと Development Build）だけ、他のウィンドウで作業していてもゲームを止めないようにする。
/// 自動プレイを一晩回したり、動かしながら別の作業をしたりするため。
///
/// ・Application.runInBackground を有効にして、フォーカスが外れてもゲームの更新を続ける
///   （UI の入力モジュールもこの値を見て、フォーカスが無いときの入力を受け付けるようになる）
/// ・自動プレイの仮想ゲームパッドは AutoPlayBot 側で「バックグラウンドでも動く」デバイスとして登録している
/// ・本物のキーボード・マウスは、Input System の既定どおりフォーカスが無い間は無視される
///   （他のウィンドウで打った文字がゲームに入ることはない）
/// ・フォーカスが無い間は音を消す。音は CRIWARE で鳴らしているので、AudioListener ではなく
///   マスターバス（MasterOut）の音量を 0 にする。止めるのではなく消音なので、戻ったときに音がずれることはない
///
/// 製品版ビルドではこのファイルごと含まれないので、通常どおりフォーカスが外れると止まる。
/// </summary>
public static class DebugBackgroundRunner
{
    /// <summary>CRI Atom Craft の ACF に定義されているマスターバスの名前。</summary>
    private const string MasterBus = "MasterOut";

    private static bool originalRunInBackground;
    private static bool muted;
    private static float volumeBeforeMute = 1f;

    /// <summary>フォーカスが外れている間もゲームを動かしているか。</summary>
    public static bool IsActive => Application.runInBackground;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Enable()
    {
        // エディターでは Player Settings の値と共有されている可能性があるため、終了時に元へ戻す
        originalRunInBackground = Application.runInBackground;
        Application.runInBackground = true;

        Application.focusChanged -= OnFocusChanged;
        Application.focusChanged += OnFocusChanged;
        Application.quitting -= Restore;
        Application.quitting += Restore;
    }

    private static void OnFocusChanged(bool focused)
    {
        // FPS が落ちた理由が「別のウィンドウを触っていたから」なのかを後で見分けられるよう、ログに残す
        GameLogger.Event("FocusChanged", $"focused={focused}");
        SetMuted(!focused);
    }

    private static void SetMuted(bool mute)
    {
        if (mute == muted || !CriAtomPlugin.IsLibraryInitialized()) return;

        if (mute)
        {
            CriAtomExAsr.GetBusVolume(MasterBus, out volumeBeforeMute);
            CriAtomExAsr.SetBusVolume(MasterBus, 0f);
        }
        else
        {
            CriAtomExAsr.SetBusVolume(MasterBus, volumeBeforeMute);
        }
        muted = mute;
    }

    private static void Restore()
    {
        Application.focusChanged -= OnFocusChanged;
        Application.quitting -= Restore;
        SetMuted(false);
        Application.runInBackground = originalRunInBackground;
    }
}
#endif
