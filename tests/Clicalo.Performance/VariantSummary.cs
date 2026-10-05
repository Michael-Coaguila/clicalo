namespace Clicalo.Performance;

/// <summary>The S5 numbers of one publication variant.</summary>
/// <param name="Variant">The variant.</param>
/// <param name="Starts">How many starts were measured.</param>
/// <param name="FirstStart">First frame of the first start after publishing.</param>
/// <param name="WarmP50">p50 of the first frame of the other starts.</param>
/// <param name="WarmMax">Maximum of the first frame of the other starts.</param>
/// <param name="WorkingSetMaxBytes">Largest working set.</param>
/// <param name="PrivateMaxBytes">Largest private bytes.</param>
internal sealed record VariantSummary(
    string Variant,
    int Starts,
    TimeSpan FirstStart,
    TimeSpan WarmP50,
    TimeSpan WarmMax,
    long WorkingSetMaxBytes,
    long PrivateMaxBytes
)
{
    /// <summary>Summarizes the starts of one variant, in run order.</summary>
    /// <param name="samples">At least one start of the same variant.</param>
    public static VariantSummary Of(IReadOnlyList<StartupSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
        {
            throw new ArgumentException("A summary needs at least one start.", nameof(samples));
        }

        var ordered = samples.OrderBy(static sample => sample.Run).ToList();
        var warm = ordered.Skip(1).Select(static sample => sample.FirstFrame).ToList();
        if (warm.Count == 0)
        {
            warm.Add(ordered[0].FirstFrame);
        }

        return new VariantSummary(
            ordered[0].Variant,
            ordered.Count,
            ordered[0].FirstFrame,
            Percentiles.Of(warm, 0.5),
            warm.Max(),
            ordered.Max(static sample => sample.WorkingSetBytes),
            ordered.Max(static sample => sample.PrivateBytes)
        );
    }

    /// <summary>Whether every number is within its budget of <c>data/catalogs/budgets.json</c> (blueprint §10.3).</summary>
    public bool WithinBudgets =>
        Verdicts(touchLab: true).All(static v => v.Verdict == BudgetVerdict.Within);

    /// <summary>The budgets this variant broke where they gate (<see cref="BudgetVerdict.Failed"/>).</summary>
    /// <param name="touchLab">Whether this is the touch laboratory (<c>CLICALO_PERF_GATE=1</c>).</param>
    public IReadOnlyList<string> Failures(bool touchLab) =>
        [
            .. Verdicts(touchLab)
                .Where(static v => v.Verdict == BudgetVerdict.Failed)
                .Select(static v => v.Budget),
        ];

    private IEnumerable<(string Budget, BudgetVerdict Verdict)> Verdicts(bool touchLab)
    {
        var budgets = PerformanceBudgets.Shared;
        var warmStarts = Math.Max(1, Starts - 1);
        yield return (
            budgets.ColdFirstFrameMax.Name,
            budgets.ColdFirstFrameMax.Judge(FirstStart, 1, touchLab)
        );
        yield return (
            budgets.WarmFirstFrame.Name,
            budgets.WarmFirstFrame.Judge(WarmP50, warmStarts, touchLab)
        );
        yield return (
            budgets.WarmFirstFrameMax.Name,
            budgets.WarmFirstFrameMax.Judge(WarmMax, warmStarts, touchLab)
        );
        yield return (
            budgets.WorkingSet.Name,
            budgets.WorkingSet.Judge(WorkingSetMaxBytes, Starts, touchLab)
        );
    }
}
