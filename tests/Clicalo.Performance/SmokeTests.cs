using System.Text.Json;

namespace Clicalo.Performance;

/// <summary>
/// The headless parts of the measurements, so <c>cl check</c> covers them: the percentile method, the parsing of the
/// variants <c>cl perf</c> passes and the S5 report. The measurements themselves (<see cref="S5StartupTests"/>,
/// <see cref="TouchToSendInputTests"/>) run only through <c>cl perf</c>.
/// </summary>
public sealed class SmokeTests
{
    private static readonly MeasurementContext Machine = new(
        "runner-1",
        "x64",
        4,
        SendInput: false
    );

    [Fact]
    public void Percentiles_use_the_nearest_rank()
    {
        TimeSpan[] values =
        [
            .. Enumerable.Range(1, 20).Select(static ms => TimeSpan.FromMilliseconds(ms)),
        ];

        Percentiles.Of(values, 0.95).ShouldBe(TimeSpan.FromMilliseconds(19));
        Percentiles.Of(values, 0.5).ShouldBe(TimeSpan.FromMilliseconds(10));
        Percentiles.Of(values, 1).ShouldBe(TimeSpan.FromMilliseconds(20));
        Percentiles.Of([TimeSpan.FromMilliseconds(7)], 0.95).ShouldBe(TimeSpan.FromMilliseconds(7));
    }

    [Fact]
    public void The_variants_of_cl_perf_are_parsed_and_malformed_entries_skipped()
    {
        var variants = AppVariant.Parse(
            " sc-r2r=C:\\a\\Clicalo.exe;fdd = C:\\b\\Clicalo.exe ; =x; broken; nopath="
        );

        variants.ShouldBe([
            new AppVariant("sc-r2r", "C:\\a\\Clicalo.exe"),
            new AppVariant("fdd", "C:\\b\\Clicalo.exe"),
        ]);
        AppVariant.Parse(null).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "NFR-001")]
    public void A_variant_is_summarized_by_its_first_start_and_its_warm_starts()
    {
        var summary = VariantSummary.Of([
            Sample(2, 250, 90),
            Sample(1, 820, 100),
            Sample(3, 310, 95),
        ]);

        summary.FirstStart.ShouldBe(TimeSpan.FromMilliseconds(820));
        summary.WarmP50.ShouldBe(TimeSpan.FromMilliseconds(250));
        summary.WarmMax.ShouldBe(TimeSpan.FromMilliseconds(310));
        summary.WorkingSetMaxBytes.ShouldBe(100L * 1024 * 1024);
        summary.WithinBudgets.ShouldBeTrue();
        VariantSummary
            .Of([Sample(1, 1200, 90)])
            .WithinBudgets.ShouldBeFalse("a cold start over 1 s");
        VariantSummary
            .Of([Sample(1, 500, 130), Sample(2, 200, 130)])
            .WithinBudgets.ShouldBeFalse("over 120 MB");
    }

    [Fact]
    public void The_S5_artifacts_carry_numbers_and_nothing_personal()
    {
        List<StartupSample> samples = [Sample(1, 640.26, 95), Sample(2, 280, 96)];
        var summaries = new[] { VariantSummary.Of(samples) };

        using var json = JsonDocument.Parse(S5Report.Json(Machine, summaries, samples));
        var markdown = S5Report.Markdown(Machine, summaries);

        var variant = json.RootElement.GetProperty("variants")[0];
        variant.GetProperty("variant").GetString().ShouldBe("sc-r2r");
        variant.GetProperty("firstStartMs").GetDouble().ShouldBe(640.3);
        variant.GetProperty("workingSetMaxMb").GetDouble().ShouldBe(96);
        json.RootElement.GetProperty("starts").GetArrayLength().ShouldBe(2);
        json.RootElement.GetProperty("sendInput").GetBoolean().ShouldBeFalse();
        markdown.ShouldContain("| sc-r2r | 2 | 640 ms | 280 ms | 280 ms | 96.0 MB |");
        markdown.ShouldContain("sin envío de teclas");
        markdown.ShouldNotContain(Environment.UserName, Case.Insensitive);
    }

    private static StartupSample Sample(int run, double firstFrameMs, long workingSetMb) =>
        new(
            "sc-r2r",
            run,
            TimeSpan.FromMilliseconds(firstFrameMs),
            workingSetMb * 1024 * 1024,
            workingSetMb * 1024 * 1024 / 2
        );
}
