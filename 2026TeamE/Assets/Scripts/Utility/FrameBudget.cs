/// <summary>
/// 重い処理を複数フレームに分割するための「1フレームあたりの処理時間の予算」
/// </summary>
/// <remarks>
/// ループの内側で <see cref="IsExhausted"/> を確認し、予算を使い切っていたら
/// 呼び出し側で 1 フレーム待ってから <see cref="Renew"/> を呼ぶ、という使い方を想定している。
/// 「何件処理したら中断するか」ではなく「何ミリ秒使ったら中断するか」で判断するため、
/// 実行環境の速さに関係なくフレームレートを一定に保ちやすい。
///
/// <code>
/// var budget = new FrameBudget();
/// foreach (var item in items)
/// {
///     Process(item);
///     if (budget.IsExhausted)
///     {
///         yield return null;
///         budget.Renew();
///     }
/// }
/// </code>
/// </remarks>
public sealed class FrameBudget
{
    /// <summary>1フレームあたりに使う既定の処理時間、60FPSの半分程度目安</summary>
    public const double DefaultMilliseconds = 8.0;

    private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    private readonly double budgetMilliseconds;

    public FrameBudget(double budgetMilliseconds = DefaultMilliseconds)
    {
        this.budgetMilliseconds = budgetMilliseconds;
        stopwatch.Start();
    }

    /// <summary>このフレームの持ち時間を使い切ったかどうか</summary>
    public bool IsExhausted => stopwatch.Elapsed.TotalMilliseconds >= budgetMilliseconds;

    /// <summary>フレームをまたいだ後に呼び、次のフレーム分の持ち時間を割り当て直す</summary>
    public void Renew() => stopwatch.Restart();
}
