





















public sealed class FrameBudget
{
    
    public const double DefaultMilliseconds = 8.0;

    private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    private readonly double budgetMilliseconds;

    public FrameBudget(double budgetMilliseconds = DefaultMilliseconds)
    {
        this.budgetMilliseconds = budgetMilliseconds;
        stopwatch.Start();
    }

    
    public bool IsExhausted => stopwatch.Elapsed.TotalMilliseconds >= budgetMilliseconds;

    
    public void Renew() => stopwatch.Restart();
}
