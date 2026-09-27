using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Clicalo.Performance;

/// <summary>
/// The artifact of spike S5 (docs/testing/spikes/S5.md): <c>s5.json</c> for machines and trends, <c>s5.md</c> to read
/// (in Spanish, like every report the maintainer reads). Numbers only: no path, user name or window title.
/// </summary>
internal static class S5Report
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>The JSON artifact.</summary>
    /// <param name="machine">Where it ran (runner, architecture, mode).</param>
    /// <param name="summaries">One summary per variant.</param>
    /// <param name="samples">Every start.</param>
    public static string Json(
        MeasurementContext machine,
        IReadOnlyList<VariantSummary> summaries,
        IReadOnlyList<StartupSample> samples
    )
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("spike", "S5");
            writer.WriteString("runner", machine.Runner);
            writer.WriteString("architecture", machine.Architecture);
            writer.WriteBoolean("sendInput", machine.SendInput);
            writer.WriteNumber("logicalProcessors", machine.LogicalProcessors);
            writer.WriteStartArray("variants");
            foreach (var summary in summaries)
            {
                writer.WriteStartObject();
                writer.WriteString("variant", summary.Variant);
                writer.WriteNumber("starts", summary.Starts);
                writer.WriteNumber(
                    "firstStartMs",
                    Math.Round(summary.FirstStart.TotalMilliseconds, 1)
                );
                writer.WriteNumber("warmP50Ms", Math.Round(summary.WarmP50.TotalMilliseconds, 1));
                writer.WriteNumber("warmMaxMs", Math.Round(summary.WarmMax.TotalMilliseconds, 1));
                writer.WriteNumber("workingSetMaxMb", Megabytes(summary.WorkingSetMaxBytes));
                writer.WriteNumber("privateMaxMb", Megabytes(summary.PrivateMaxBytes));
                writer.WriteBoolean("withinBudgets", summary.WithinBudgets);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("starts");
            foreach (var sample in samples)
            {
                writer.WriteStartObject();
                writer.WriteString("variant", sample.Variant);
                writer.WriteNumber("run", sample.Run);
                writer.WriteNumber(
                    "firstFrameMs",
                    Math.Round(sample.FirstFrame.TotalMilliseconds, 1)
                );
                writer.WriteNumber("workingSetMb", Megabytes(sample.WorkingSetBytes));
                writer.WriteNumber("privateMb", Megabytes(sample.PrivateBytes));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>The Markdown artifact.</summary>
    /// <param name="machine">Where it ran.</param>
    /// <param name="summaries">One summary per variant.</param>
    public static string Markdown(
        MeasurementContext machine,
        IReadOnlyList<VariantSummary> summaries
    )
    {
        var text = new StringBuilder();
        text.Append("# S5 · Arranque y memoria\n\n");
        text.Append(
            string.Create(
                Invariant,
                $"Equipo: {machine.Runner}, {machine.Architecture}, {machine.LogicalProcessors} procesadores lógicos; "
            )
        );
        text.Append(
            machine.SendInput ? "con envío de teclas.\n\n" : "sin envío de teclas (--no-input).\n\n"
        );
        text.Append(
            "Primer frame: desde la creación del proceso hasta el primer frame del panel. «Primer arranque» es el "
                + "primero tras publicar (lo más parecido a un arranque en frío que ofrece un runner alojado; el frío "
                + "real, tras reiniciar, se mide en el equipo táctil). Presupuestos del plano §10.3: frío ≤ 1000 ms, "
                + "caliente p50 ≤ 300 ms y máximo ≤ 450 ms, working set ≤ 120 MB.\n\n"
        );
        text.Append(
            "| Variante | Arranques | Primer arranque | Caliente p50 | Caliente máx. | Working set máx. | Privada máx. | Dentro del presupuesto |\n"
        );
        text.Append("|---|---|---|---|---|---|---|---|\n");
        foreach (var summary in summaries)
        {
            text.Append(
                string.Create(
                    Invariant,
                    $"| {summary.Variant} | {summary.Starts} | {Ms(summary.FirstStart)} | {Ms(summary.WarmP50)} | {Ms(summary.WarmMax)} | {Megabytes(summary.WorkingSetMaxBytes):0.0} MB | {Megabytes(summary.PrivateMaxBytes):0.0} MB | {(summary.WithinBudgets ? "sí" : "no")} |\n"
                )
            );
        }

        return text.ToString();
    }

    private static string Ms(TimeSpan value) =>
        value.TotalMilliseconds.ToString("0", Invariant) + " ms";

    private static double Megabytes(long bytes) => Math.Round(bytes / (1024.0 * 1024.0), 1);
}
