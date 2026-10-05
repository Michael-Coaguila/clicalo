using System.Text.Json;

namespace Clicalo.Performance;

/// <summary>
/// The headless parts of the measurements, so <c>cl check</c> covers them: the percentile method, the parsing of the
/// variants <c>cl perf</c> passes, the budgets of <c>data/catalogs/budgets.json</c> and how they gate, and the reports. The measurements themselves (<see cref="S5StartupTests"/>,
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

    [Fact]
    [Trait("Req", "NFR-001")]
    public void The_budgets_are_the_numbers_of_the_plan()
    {
        var budgets = PerformanceBudgets.Shared;

        budgets.TouchToSendInput.ShouldBe(
            new PerformanceBudget(
                "TouchToSendInput",
                BudgetStatistic.P95,
                TimeSpan.FromMilliseconds(50),
                null,
                20,
                BudgetGate.EveryRun
            ),
            "the M2 exit criterion fails every run that measures it"
        );
        budgets.ColdFirstFrameMax.Duration.ShouldBe(TimeSpan.FromMilliseconds(1000));
        budgets.WarmFirstFrame.Duration.ShouldBe(TimeSpan.FromMilliseconds(300));
        budgets.WarmFirstFrame.Statistic.ShouldBe(BudgetStatistic.P50);
        budgets.WarmFirstFrameMax.Duration.ShouldBe(TimeSpan.FromMilliseconds(450));
        budgets.WorkingSet.Bytes.ShouldBe(120L * 1024 * 1024);
        budgets
            .All.Values.Where(static b =>
                !string.Equals(b.Name, "TouchToSendInput", StringComparison.Ordinal)
            )
            .ShouldAllBe(static b => b.Gate == BudgetGate.TouchLab);
    }

    [Fact]
    [Trait("Req", "NFR-001")]
    public void A_budget_fails_where_it_gates_and_is_a_trend_elsewhere()
    {
        var everyRun = new PerformanceBudget(
            "A",
            BudgetStatistic.P95,
            TimeSpan.FromMilliseconds(50),
            null,
            20,
            BudgetGate.EveryRun
        );
        var touchLab = everyRun with { Gate = BudgetGate.TouchLab };
        var over = TimeSpan.FromMilliseconds(50.1);
        var limit = TimeSpan.FromMilliseconds(50);

        everyRun.Judge(limit, 20, touchLab: false).ShouldBe(BudgetVerdict.Within);
        everyRun.Judge(over, 20, touchLab: false).ShouldBe(BudgetVerdict.Failed);
        everyRun.Judge(over, 20, touchLab: true).ShouldBe(BudgetVerdict.Failed);
        touchLab.Judge(over, 20, touchLab: false).ShouldBe(BudgetVerdict.OverTrend);
        touchLab.Judge(over, 20, touchLab: true).ShouldBe(BudgetVerdict.Failed);
        everyRun
            .Judge(limit, 19, touchLab: false)
            .ShouldBe(
                BudgetVerdict.Failed,
                "a p95 of fewer taps than the budget asks is no measurement"
            );
        new PerformanceBudget("M", BudgetStatistic.Max, null, 100, 1, BudgetGate.EveryRun)
            .Judge(101L, 1, touchLab: false)
            .ShouldBe(BudgetVerdict.Failed);
    }

    [Fact]
    [Trait("Req", "NFR-001")]
    public void A_touch_measurement_over_50_ms_fails_the_run_on_a_hosted_runner()
    {
        var budget = PerformanceBudgets.Shared.TouchToSendInput;
        TimeSpan[] fast = [.. Enumerable.Repeat(TimeSpan.FromMilliseconds(12), 20)];
        TimeSpan[] slow =
        [
            .. fast.Take(18),
            TimeSpan.FromMilliseconds(51),
            TimeSpan.FromMilliseconds(70),
        ];

        var within = TouchLatencyReport.Of(Machine, "sc-r2r", fast, budget, touchLab: false);
        var over = TouchLatencyReport.Of(Machine, "sc-r2r", slow, budget, touchLab: false);

        within.Verdict.ShouldBe(BudgetVerdict.Within);
        Should.NotThrow(within.Enforce);
        over.Measured.ShouldBe(TimeSpan.FromMilliseconds(51));
        over.Verdict.ShouldBe(BudgetVerdict.Failed);
        Should.Throw<ShouldAssertException>(over.Enforce).Message.ShouldContain("p95 51.0 ms");
        Should.Throw<ShouldAssertException>(
            TouchLatencyReport.Of(Machine, "sc-r2r", fast[..5], budget, touchLab: false).Enforce
        );
    }

    [Fact]
    public void The_touch_artifacts_carry_numbers_and_nothing_personal()
    {
        TimeSpan[] latencies =
        [
            .. Enumerable.Range(1, 20).Select(static ms => TimeSpan.FromMilliseconds(ms * 2.5)),
        ];
        var report = TouchLatencyReport.Of(
            Machine,
            "sc-r2r",
            latencies,
            PerformanceBudgets.Shared.TouchToSendInput,
            touchLab: false
        );

        using var json = JsonDocument.Parse(report.Json());
        var markdown = report.Markdown();

        json.RootElement.GetProperty("measuredMs").GetDouble().ShouldBe(47.5);
        json.RootElement.GetProperty("budgetMs").GetDouble().ShouldBe(50);
        json.RootElement.GetProperty("verdict").GetString().ShouldBe("Within");
        json.RootElement.GetProperty("latenciesMs").GetArrayLength().ShouldBe(20);
        markdown.ShouldContain(
            "| 20 | 25.0 ms | 47.5 ms | 50.0 ms | p95 ≤ 50.0 ms | dentro del presupuesto |"
        );
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
