using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Clicalo.Performance;

/// <summary>
/// The artifacts of the touch → <c>SendInput</c> measurement: <c>touch-to-sendinput.json</c> for machines and trends,
/// <c>touch-to-sendinput.md</c> to read (in Spanish; the CI job also shows it as the run summary). Numbers only: no
/// path, user name or window title.
/// </summary>
/// <param name="Machine">Where it ran.</param>
/// <param name="Variant">The publication variant measured.</param>
/// <param name="Latencies">One latency per tap, in tap order.</param>
/// <param name="Budget">The budget of <c>data/catalogs/budgets.json</c>.</param>
/// <param name="Measured">The statistic of the budget over <paramref name="Latencies"/>.</param>
/// <param name="Verdict">What the statistic means for the budget here.</param>
internal sealed record TouchLatencyReport(
    MeasurementContext Machine,
    string Variant,
    IReadOnlyList<TimeSpan> Latencies,
    PerformanceBudget Budget,
    TimeSpan Measured,
    BudgetVerdict Verdict
)
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Reduces <paramref name="latencies"/> to the statistic of <paramref name="budget"/> and judges it.</summary>
    /// <param name="machine">Where it ran.</param>
    /// <param name="variant">The publication variant measured.</param>
    /// <param name="latencies">One latency per tap; at least one.</param>
    /// <param name="budget">The budget of <c>data/catalogs/budgets.json</c>.</param>
    /// <param name="touchLab">Whether this is the touch laboratory (<c>CLICALO_PERF_GATE=1</c>).</param>
    public static TouchLatencyReport Of(
        MeasurementContext machine,
        string variant,
        IReadOnlyList<TimeSpan> latencies,
        PerformanceBudget budget,
        bool touchLab
    )
    {
        ArgumentNullException.ThrowIfNull(budget);
        var measured = budget.Reduce(latencies);
        return new TouchLatencyReport(
            machine,
            variant,
            latencies,
            budget,
            measured,
            budget.Judge(measured, latencies.Count, touchLab)
        );
    }

    /// <summary>One English line for the test output and the failure message.</summary>
    public string Summary =>
        string.Create(
            Invariant,
            $"Touch → SendInput over {Latencies.Count} taps ({Variant}): p50 {Ms(Percentiles.Of(Latencies, 0.5))}, {Statistic} {Ms(Measured)} (budget {Ms(Limit)}, gate {Budget.Gate}), max {Ms(Percentiles.Of(Latencies, 1))}: {Verdict}."
        );

    private TimeSpan Limit => Budget.Duration ?? TimeSpan.Zero;

    private string Statistic => Budget.Statistic.ToString().ToLowerInvariant();

    /// <summary>Fails the measurement when the budget gates here and was broken (or measured on too few taps).</summary>
    public void Enforce() => Verdict.ShouldNotBe(BudgetVerdict.Failed, Summary);

    /// <summary>The JSON artifact.</summary>
    public string Json()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("measurement", "touch-to-sendinput");
            writer.WriteString("budget", Budget.Name);
            writer.WriteString("statistic", Statistic);
            writer.WriteNumber("budgetMs", Round(Limit));
            writer.WriteString("gate", Budget.Gate.ToString());
            writer.WriteString("verdict", Verdict.ToString());
            writer.WriteString("runner", Machine.Runner);
            writer.WriteString("architecture", Machine.Architecture);
            writer.WriteNumber("logicalProcessors", Machine.LogicalProcessors);
            writer.WriteString("variant", Variant);
            writer.WriteNumber("taps", Latencies.Count);
            writer.WriteNumber("p50Ms", Round(Percentiles.Of(Latencies, 0.5)));
            writer.WriteNumber("measuredMs", Round(Measured));
            writer.WriteNumber("maxMs", Round(Percentiles.Of(Latencies, 1)));
            writer.WriteStartArray("latenciesMs");
            foreach (var latency in Latencies)
            {
                writer.WriteNumberValue(Round(latency));
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>The Markdown artifact.</summary>
    public string Markdown()
    {
        var verdict = Verdict switch
        {
            BudgetVerdict.Within => "dentro del presupuesto",
            BudgetVerdict.OverTrend => "por encima del presupuesto (solo tendencia aquí)",
            _ => "**fuera del presupuesto: la ejecución falla**",
        };
        var text = new StringBuilder();
        text.Append("# Toque → SendInput\n\n");
        text.Append(
            string.Create(
                Invariant,
                $"Equipo: {Machine.Runner}, {Machine.Architecture}, {Machine.LogicalProcessors} procesadores lógicos; variante {Variant}. "
            )
        );
        text.Append(
            "Desde que el dedo sintético se levanta sobre la primera ficha de Clicalo.exe hasta que la primera tecla "
                + "llega a InputProbe (criterio de salida de M2, NFR-001; presupuesto en data/catalogs/budgets.json).\n\n"
        );
        text.Append("| Toques | p50 | ");
        text.Append(Statistic);
        text.Append(" | Máximo | Presupuesto | Resultado |\n|---|---|---|---|---|---|\n");
        text.Append(
            string.Create(
                Invariant,
                $"| {Latencies.Count} | {Ms(Percentiles.Of(Latencies, 0.5))} | {Ms(Measured)} | {Ms(Percentiles.Of(Latencies, 1))} | {Statistic} ≤ {Ms(Limit)} | {verdict} |\n"
            )
        );
        return text.ToString();
    }

    private static double Round(TimeSpan value) => Math.Round(value.TotalMilliseconds, 1);

    private static string Ms(TimeSpan value) =>
        value.TotalMilliseconds.ToString("0.0", Invariant) + " ms";
}
