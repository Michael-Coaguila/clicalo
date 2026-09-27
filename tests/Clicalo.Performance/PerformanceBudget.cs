namespace Clicalo.Performance;

/// <summary>One budget of <c>data/catalogs/budgets.json</c> (blueprint §10.3, NFR-001).</summary>
/// <param name="Name">Its name in the catalog, for example <c>TouchToSendInput</c>.</param>
/// <param name="Statistic">How the sample is reduced.</param>
/// <param name="Duration">The limit of a time budget; <see langword="null"/> for a memory budget.</param>
/// <param name="Bytes">The limit of a memory budget; <see langword="null"/> for a time budget.</param>
/// <param name="MinSamples">The fewest values the statistic may be computed from.</param>
/// <param name="Gate">Where it fails the run.</param>
internal sealed record PerformanceBudget(
    string Name,
    BudgetStatistic Statistic,
    TimeSpan? Duration,
    long? Bytes,
    int MinSamples,
    BudgetGate Gate
)
{
    /// <summary>Whether this budget fails the run here, or is only a trend.</summary>
    /// <param name="touchLab">Whether this is the touch laboratory (<c>CLICALO_PERF_GATE=1</c>).</param>
    public bool GatesHere(bool touchLab) => Gate == BudgetGate.EveryRun || touchLab;

    /// <summary>The statistic of <paramref name="values"/> this budget is compared with.</summary>
    /// <param name="values">At least one value.</param>
    public TimeSpan Reduce(IReadOnlyCollection<TimeSpan> values) =>
        Statistic switch
        {
            BudgetStatistic.P50 => Percentiles.Of(values, 0.5),
            BudgetStatistic.P95 => Percentiles.Of(values, 0.95),
            _ => Percentiles.Of(values, 1),
        };

    /// <summary>Judges a time already reduced by <see cref="Reduce"/>.</summary>
    /// <param name="measured">The statistic.</param>
    /// <param name="samples">How many values it was computed from.</param>
    /// <param name="touchLab">Whether this is the touch laboratory.</param>
    public BudgetVerdict Judge(TimeSpan measured, int samples, bool touchLab) =>
        Verdict(
            measured
                <= (
                    Duration ?? throw new InvalidOperationException(Name + " is not a time budget.")
                ),
            samples,
            touchLab
        );

    /// <summary>Judges a memory figure already reduced to the statistic of this budget.</summary>
    /// <param name="measuredBytes">The statistic, in bytes.</param>
    /// <param name="samples">How many values it was computed from.</param>
    /// <param name="touchLab">Whether this is the touch laboratory.</param>
    public BudgetVerdict Judge(long measuredBytes, int samples, bool touchLab) =>
        Verdict(
            measuredBytes
                <= (
                    Bytes ?? throw new InvalidOperationException(Name + " is not a memory budget.")
                ),
            samples,
            touchLab
        );

    private BudgetVerdict Verdict(bool within, int samples, bool touchLab)
    {
        if (samples < MinSamples)
        {
            return BudgetVerdict.Failed;
        }

        if (within)
        {
            return BudgetVerdict.Within;
        }

        return GatesHere(touchLab) ? BudgetVerdict.Failed : BudgetVerdict.OverTrend;
    }
}
