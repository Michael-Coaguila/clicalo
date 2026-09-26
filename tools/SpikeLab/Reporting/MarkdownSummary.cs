using System.Globalization;
using System.Text;
using Clicalo.Tools.SpikeLab.Composition;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Reporting;

/// <summary>
/// The human summary of a run, in Spanish Markdown that reads well with Narrator: the verdict in the first sentence,
/// short headings, one table of steps and one list per failed step. The JSON report keeps every detail.
/// </summary>
internal static class MarkdownSummary
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Renders the summary of <paramref name="run"/>.</summary>
    public static string Render(ScriptSnapshot run, ReportContext context)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(context);
        var text = new StringBuilder(8 * 1024);
        text.Append("# Informe de SpikeLab · ").Append(run.Script.Title).Append("\n\n");
        text.Append("**Resultado: ").Append(Describe(run.Verdict)).Append(".** ");
        text.Append(Counts(run)).Append("\n\n");

        text.Append("- Guion: ").Append(run.Script.Document).Append('\n');
        text.Append("- Empezado: ")
            .Append(Time(run.StartedAt))
            .Append(" · Actualizado: ")
            .Append(Time(context.UpdatedAt))
            .Append(" (hora local)\n");
        text.Append("- Windows: ")
            .Append(context.Machine.Windows)
            .Append(" · ")
            .Append(context.Machine.Architecture)
            .Append(" · SpikeLab ")
            .Append(context.Machine.LabVersion)
            .Append('\n');
        text.Append("- Envío de teclas: ")
            .Append(context.SendsKeys ? "activado" : "desactivado")
            .Append(" · Números de voz: ")
            .Append(context.VoiceNumbers ? "activados" : "desactivados")
            .Append("\n\n");

        AppendComponents(text, context);
        AppendSteps(text, run);
        AppendFailures(text, run);

        if (context.DroppedEvents > 0)
        {
            text.Append(
                string.Create(
                    Spanish,
                    $"El registro de eventos llegó al máximo: se descartaron los {context.DroppedEvents} más antiguos.\n"
                )
            );
        }

        return text.ToString();
    }

    /// <summary>The verdict of a run, in lower case.</summary>
    public static string Describe(SpikeVerdict verdict) =>
        verdict switch
        {
            SpikeVerdict.Passed => "superado",
            SpikeVerdict.Failed => "fallido",
            _ => "incompleto",
        };

    /// <summary>The verdict of a step, capitalized.</summary>
    public static string Describe(StepVerdict verdict) =>
        verdict switch
        {
            StepVerdict.Passed => "Superado",
            StepVerdict.Failed => "Fallido",
            StepVerdict.Incomplete => "Incompleto",
            StepVerdict.NotApplicable => "No aplicable",
            StepVerdict.InProgress => "En curso",
            _ => "Pendiente",
        };

    private static string Counts(ScriptSnapshot run)
    {
        int Count(StepVerdict verdict) => run.Steps.Count(step => step.Verdict == verdict);
        var open =
            Count(StepVerdict.Pending)
            + Count(StepVerdict.InProgress)
            + Count(StepVerdict.Incomplete);
        return string.Create(
            Spanish,
            $"{Count(StepVerdict.Passed)} de {run.Steps.Length} pasos superados, {Count(StepVerdict.Failed)} fallidos, {Count(StepVerdict.NotApplicable)} no aplicables y {open} sin terminar."
        );
    }

    private static void AppendComponents(StringBuilder text, ReportContext context)
    {
        text.Append("## Piezas del producto\n\n");
        var notReady = context
            .Components.Where(component => component.State != LabComponentState.Ready)
            .ToArray();
        text.Append(
            notReady.Length == 0
                ? "Todas las piezas están listas.\n\n"
                : string.Create(
                    Spanish,
                    $"{notReady.Length} de {context.Components.Length} piezas no están listas: lo que dependa de ellas no se pudo medir.\n\n"
                )
        );
        foreach (var component in context.Components)
        {
            text.Append("- ")
                .Append(
                    component.State switch
                    {
                        LabComponentState.Ready => "Lista",
                        LabComponentState.Pending => "Pendiente",
                        _ => "Con error",
                    }
                )
                .Append(": ")
                .Append(component.Name)
                .Append(". ")
                .Append(Cell(component.Detail))
                .Append('\n');
        }

        text.Append('\n');
    }

    private static void AppendSteps(StringBuilder text, ScriptSnapshot run)
    {
        text.Append("## Pasos\n\n");
        text.Append("| Fila | Paso | Resultado | Correctas | Fallos | Latencia p95 (ms) |\n");
        text.Append("|---|---|---|---|---|---|\n");
        foreach (var progress in run.Steps)
        {
            var latency = LatencySummary.Of(progress.Repetitions);
            text.Append("| ")
                .Append(progress.Step.Id)
                .Append(" | ")
                .Append(Cell(progress.Step.Title))
                .Append(progress.Step.Decisive ? string.Empty : " (no decisivo)")
                .Append(" | ")
                .Append(Describe(progress.Verdict))
                .Append(
                    string.Create(
                        Spanish,
                        $" ({progress.PassedCount} de {progress.Step.Required}) | {progress.PassedCount} | {progress.FailedCount} | "
                    )
                )
                .Append(latency.Count == 0 ? "—" : latency.P95.ToString("0", Spanish))
                .Append(" |\n");
        }

        text.Append('\n');
    }

    private static void AppendFailures(StringBuilder text, ScriptSnapshot run)
    {
        text.Append("## Fallos y observaciones\n\n");
        var any = false;
        foreach (var progress in run.Steps)
        {
            var failed = progress.Repetitions.Where(repetition => !repetition.Passed).ToArray();
            if (failed.Length == 0 && progress.Discarded.IsEmpty)
            {
                continue;
            }

            any = true;
            text.Append("### Fila ")
                .Append(progress.Step.Id)
                .Append(" · ")
                .Append(progress.Step.Title)
                .Append("\n\n");
            foreach (var repetition in failed)
            {
                text.Append(
                        string.Create(
                            Spanish,
                            $"- Repetición {repetition.Index} ({Time(repetition.At)}): "
                        )
                    )
                    .Append(string.Join(' ', repetition.Problems))
                    .Append('\n');
            }

            foreach (var attempt in progress.Discarded)
            {
                var failures = attempt.Repetitions.Count(repetition => !repetition.Passed);
                text.Append(
                    string.Create(
                        Spanish,
                        $"- Intento descartado con «Repetir» ({Time(attempt.At)}): {attempt.Repetitions.Length} repeticiones, {failures} fallidas.\n"
                    )
                );
            }

            text.Append('\n');
        }

        if (!any)
        {
            text.Append("Ninguna repetición ha fallado.\n\n");
        }
    }

    private static string Time(DateTimeOffset at) =>
        at.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", Spanish);

    private static string Cell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
}
