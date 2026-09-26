using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Tests.Scripting;

public sealed class LatencySummaryTests
{
    [Fact]
    public void No_samples_give_an_empty_summary() =>
        LatencySummary.Of(Array.Empty<double>()).ShouldBe(LatencySummary.Empty);

    [Fact]
    public void Percentiles_use_the_nearest_rank()
    {
        var summary = LatencySummary.Of([
            .. Enumerable.Range(1, 20).Select(value => (double)value),
        ]);

        summary.Count.ShouldBe(20);
        summary.P50.ShouldBe(10);
        summary.P95.ShouldBe(19);
        summary.Max.ShouldBe(20);
    }

    [Fact]
    public void Only_repetitions_with_a_latency_count()
    {
        RepetitionRecord Record(double? latency) =>
            new(
                1,
                DateTimeOffset.UnixEpoch,
                RepetitionSource.Automatic,
                true,
                [],
                new RepetitionEvidence { LatencyMs = latency }
            );

        var summary = LatencySummary.Of([Record(30), Record(null), Record(10)]);

        summary.Count.ShouldBe(2);
        summary.P50.ShouldBe(10);
        summary.Max.ShouldBe(30);
    }
}
