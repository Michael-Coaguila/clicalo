namespace Clicalo.Performance;

/// <summary>Nearest-rank percentiles of a sample (the method of the budgets of blueprint §10.3).</summary>
internal static class Percentiles
{
    /// <summary>The value below which <paramref name="percentile"/> of the sample lies (nearest rank).</summary>
    /// <param name="values">The sample; at least one value.</param>
    /// <param name="percentile">Between 0 (exclusive) and 1 (inclusive): 0.5 for p50, 0.95 for p95.</param>
    public static TimeSpan Of(IReadOnlyCollection<TimeSpan> values, double percentile)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException("A percentile needs at least one value.", nameof(values));
        }

        if (percentile is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentile),
                percentile,
                "Between 0 and 1."
            );
        }

        var sorted = values.Order().ToList();
        var rank = (int)Math.Ceiling(percentile * sorted.Count);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
    }
}
