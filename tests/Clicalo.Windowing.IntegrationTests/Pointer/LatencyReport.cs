using System.Globalization;
using System.IO;
using System.Text.Json;
using Clicalo.TestKit;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// Percentiles of a latency sample and where they are recorded: the test output and a JSON file next to the test
/// results of <c>cl desk</c> (<c>artifacts/cl/test-results/</c>, uploaded by the CI desktop job), so S2 can compare runs.
/// </summary>
/// <param name="Name">What was measured.</param>
/// <param name="Samples">The measured latencies, in milliseconds.</param>
public sealed record LatencyReport(string Name, IReadOnlyList<double> Samples)
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public double P50 => Percentile(0.50);

    public double P95 => Percentile(0.95);

    public double Max => Samples.Max();

    /// <summary>The nearest-rank percentile.</summary>
    public double Percentile(double fraction)
    {
        var sorted = Samples.Order().ToList();
        var rank = (int)Math.Ceiling(fraction * sorted.Count);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
    }

    /// <summary>One line for the test output.</summary>
    public string Summary(IReadOnlyDictionary<string, string> context) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Name}: n={Samples.Count} p50={P50:0.0} ms p95={P95:0.0} ms max={Max:0.0} ms ({string.Join(", ", context.Select(c => c.Key + "=" + c.Value))})"
        );

    /// <summary>Writes the report to the test output and to <c>artifacts/cl/test-results/&lt;name&gt;.json</c>.</summary>
    public void Record(IReadOnlyDictionary<string, string> context)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(Summary(context));
        var folder = RepoPaths.Combine("artifacts", "cl", "test-results");
        Directory.CreateDirectory(folder);
        var json = JsonSerializer.Serialize(
            new
            {
                name = Name,
                count = Samples.Count,
                p50Ms = P50,
                p95Ms = P95,
                maxMs = Max,
                samplesMs = Samples,
                context,
            },
            Indented
        );
        File.WriteAllText(Path.Combine(folder, Name + ".json"), json);
    }
}
