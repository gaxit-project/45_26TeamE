using UnityEngine;

/// <summary>
/// シーン切り替え直後に走る「重い初期化処理」の進行状況を共有するためのゲート。
/// </summary>
/// <remarks>
/// 初期化を行う側は <see cref="Begin"/> と <see cref="End"/> で処理を囲み、
/// 待つ側（ローディング画面など）は <see cref="IsBusy"/> を監視する。
///
/// こうすることで、ローディング画面は「地形生成」「アイテム配置」といった
/// 個々の初期化処理の存在を知らなくてよくなり、初期化処理を後から追加しても
/// ローディング画面側は変更不要になる（依存関係の逆転）。
///
/// 複数の初期化処理が同時に走ってもよいように、単純なbool ではなく参照カウントで管理している。
/// </remarks>
public static class SceneInitializationGate
{
    private static int pendingCount;

    /// <summary>完了していない初期化処理が1つ以上あるかどうか。</summary>
    public static bool IsBusy => pendingCount > 0;

    /// <summary>初期化処理の開始を通知する。必ず <see cref="End"/> と対で呼ぶこと。</summary>
    public static void Begin()
    {
        pendingCount++;
    }

    /// <summary>初期化処理の完了を通知する。</summary>
    public static void End()
    {
        if (pendingCount > 0)
        {
            pendingCount--;
        }
    }

    /// <summary>
    /// カウントを強制的に初期化する。
    /// static変数はPlay Mode終了後も残り得るため、ゲーム起動時に必ずリセットする。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Reset()
    {
        pendingCount = 0;
    }
}
