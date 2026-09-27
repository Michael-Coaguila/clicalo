using System.Globalization;

namespace Clicalo.Performance;

/// <summary>
/// Spike S5 as a measurement (blueprint §15.1, docs/testing/spikes/S5.md): the time from the creation of the process
/// to the first frame of the panel and the working set, for every publication variant <c>cl perf</c> built
/// (self-contained ReadyToRun, self-contained composite ReadyToRun, framework-dependent). In continuous integration
/// the self-contained ReadyToRun variant is also measured with Sentinel launched after the first frame instead of in
/// parallel (§3.1). The numbers go to <c>s5.json</c> and <c>s5.md</c> in the artifacts. On a hosted runner they are a
/// trend; with <c>CLICALO_PERF_GATE=1</c> (the touch laboratory) the budgets of §10.3 fail the run (every S5 budget
/// of <c>data/catalogs/budgets.json</c> has the gate <c>touchLab</c>).
/// </summary>
[Trait("Requires", "Desktop")]
[Trait("Category", "Perf")]
[Trait("Req", "NFR-001")]
public sealed class S5StartupTests
{
    /// <summary>How long a start settles before its memory is read.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

    /// <summary>The pause between two starts, so the previous process and its files are gone.</summary>
    private static readonly TimeSpan BetweenStarts = TimeSpan.FromMilliseconds(500);

    [PerfFact]
    public async Task Every_variant_shows_its_first_frame_and_the_numbers_are_recorded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sendInput = PerfEnvironment.SendInput;
        var samples = new List<StartupSample>();
        var runs = PerfEnvironment
            .Variants.Select(static variant => (variant.Name, variant, Array.Empty<string>()))
            .ToList();
        if (
            sendInput
            && PerfEnvironment.Variants.FirstOrDefault(static v =>
                string.Equals(v.Name, "sc-r2r", StringComparison.Ordinal)
            )
                is { } parallel
        )
        {
            runs.Add(("sc-r2r · Sentinel después", parallel, ["--guardian", "after-first-frame"]));
        }

        foreach (var (name, variant, arguments) in runs)
        {
            for (var run = 1; run <= PerfEnvironment.Starts; run++)
            {
                using (var launch = AppLaunch.Start(variant, sendInput, arguments))
                {
                    var (workingSet, privateBytes) = await launch.SettledMemoryAsync(
                        Settle,
                        cancellationToken
                    );
                    samples.Add(
                        new StartupSample(name, run, launch.FirstFrame, workingSet, privateBytes)
                    );
                }

                await Task.Delay(BetweenStarts, cancellationToken);
            }
        }

        var summaries = samples
            .GroupBy(static sample => sample.Variant, StringComparer.Ordinal)
            .Select(static group => VariantSummary.Of([.. group]))
            .ToList();
        var context = MeasurementContext.Current(sendInput);
        var folder = PerfEnvironment.ResultsDirectory;
        await File.WriteAllTextAsync(
            Path.Combine(folder, "s5.json"),
            S5Report.Json(context, summaries, samples),
            cancellationToken
        );
        var markdown = S5Report.Markdown(context, summaries);
        await File.WriteAllTextAsync(Path.Combine(folder, "s5.md"), markdown, cancellationToken);
        TestContext.Current.TestOutputHelper?.WriteLine(markdown);

        samples.Count.ShouldBe(
            runs.Count * PerfEnvironment.Starts,
            "every start reached its first frame"
        );
        summaries
            .SelectMany(static summary =>
                summary
                    .Failures(PerfEnvironment.Gate)
                    .Select(budget => summary.Variant + ": " + budget)
            )
            .ShouldBeEmpty(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"S5 budgets of data/catalogs/budgets.json (blueprint §10.3); detail in {folder}"
                )
            );
    }
}
