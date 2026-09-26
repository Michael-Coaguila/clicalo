using System.Runtime.InteropServices;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>Percentiles of the latencies of a step, in milliseconds (nearest-rank method).</summary>
/// <param name="Count">Samples.</param>
/// <param name="P50">Median.</param>
/// <param name="P95">95th percentile.</param>
/// <param name="Max">Largest sample.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct LatencySummary(int Count, double P50, double P95, double Max)
{
    /// <summary>No samples.</summary>
    public static LatencySummary Empty => default;

    /// <summary>Summarizes the latencies of <paramref name="repetitions"/> that have one.</summary>
    public static LatencySummary Of(IEnumerable<RepetitionRecord> repetitions)
    {
        ArgumentNullException.ThrowIfNull(repetitions);
        var samples = new List<double>();
        foreach (var repetition in repetitions)
        {
            if (repetition.Evidence.LatencyMs is { } latency)
            {
                samples.Add(latency);
            }
        }

        return Of(samples);
    }

    /// <summary>Summarizes <paramref name="samples"/>.</summary>
    public static LatencySummary Of(IReadOnlyCollection<double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
        {
            return Empty;
        }

        var sorted = samples.Order().ToArray();
        return new LatencySummary(
            sorted.Length,
            Rank(sorted, 0.50),
            Rank(sorted, 0.95),
            sorted[^1]
        );
    }

    private static double Rank(double[] sorted, double percentile)
    {
        var rank = (int)Math.Ceiling(percentile * sorted.Length);
        return sorted[Math.Clamp(rank, 1, sorted.Length) - 1];
    }
}
