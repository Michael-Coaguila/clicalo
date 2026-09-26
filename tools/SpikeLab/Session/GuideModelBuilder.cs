using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Tools.SpikeLab.Reporting;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Session;

/// <summary>
/// Words the guide strip from the script run and the live measurements: the current step in large type, the
/// repetitions, the automatic measurements (foreground, activation messages, <c>reg01.violations</c>, lease, restore,
/// latency, probe) and green or red with the state also in words.
/// </summary>
internal static class GuideModelBuilder
{
    private const EvidenceCheck LiveChecks =
        EvidenceCheck.NoSurfaceActivation
        | EvidenceCheck.NoOwnForeground
        | EvidenceCheck.NoViolation;

    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>The model of the guide strip.</summary>
    /// <param name="run">The script run.</param>
    /// <param name="status">The live measurements.</param>
    /// <param name="restoreBudget">The limit of the forced-activation check.</param>
    public static GuideModel Build(ScriptSnapshot run, LabStatus status, TimeSpan restoreBudget)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(status);
        var measurements = Measurements(status);
        if (run.Current is not { } current)
        {
            var verdict = run.Verdict;
            return new GuideModel(
                run.Script.Title + " · guion terminado",
                "Resultado: " + MarkdownSummary.Describe(verdict),
                "Has llegado al final del guion. El informe está guardado; puedes cerrar SpikeLab o volver a un paso "
                    + "desde la ventana de control.",
                Format(
                    $"{run.Steps.Count(step => step.Verdict == StepVerdict.Passed)} de {run.Steps.Length} pasos superados."
                ),
                verdict != SpikeVerdict.Failed,
                "Guion terminado: " + MarkdownSummary.Describe(verdict),
                measurements,
                status.Notice,
                StepActionName: null
            );
        }

        var step = current.Step;
        var liveProblems = EvidenceEvaluator.Evaluate(
            step.Checks & LiveChecks,
            new RepetitionEvidence { Delta = status.SinceStep },
            restoreBudget
        );
        var isGreen = current.FailedCount == 0 && liveProblems.IsEmpty;
        var progress =
            Format($"Repeticiones: {current.Repetitions.Length} de {step.Required}")
            + Format($" · correctas: {current.PassedCount} · fallos: {current.FailedCount}")
            + (
                step.NeedsConfirmation
                    ? " · comprobación final: " + (current.Confirmed ? "hecha" : "pendiente")
                    : string.Empty
            )
            + (
                current.Discarded.IsEmpty
                    ? string.Empty
                    : Format($" · intentos repetidos: {current.Discarded.Length}")
            );

        return new GuideModel(
            Format(
                $"{run.Script.Id} · paso {run.CurrentIndex + 1} de {run.Steps.Length} (fila {step.Id})"
            ),
            step.Title,
            step.Instruction,
            progress,
            isGreen,
            StatusWord(current, isGreen, liveProblems),
            measurements,
            status.Notice,
            ActionName(step.Action, status.VoiceNumbers)
        );
    }

    /// <summary>
    /// The name of the step action button; the one of S3 row 6 says what it will do with the voice numbers, which
    /// are on while <paramref name="voiceNumbers"/>.
    /// </summary>
    public static string? ActionName(StepAction action, bool voiceNumbers) =>
        action switch
        {
            StepAction.ForceActivation => "Forzar activación del panel",
            StepAction.PoliteNotice => "Aviso cortés",
            StepAction.AssertiveNotice => "Aviso urgente",
            StepAction.ToggleVoiceNumbers => voiceNumbers
                ? "Quitar números de Clícalo"
                : "Activar números de Clícalo",
            _ => null,
        };

    private static string StatusWord(
        StepProgress current,
        bool isGreen,
        ImmutableArray<string> liveProblems
    )
    {
        if (!isGreen)
        {
            return "Algo cambió: "
                + (liveProblems.IsEmpty ? "hay repeticiones fallidas." : liveProblems[0]);
        }

        return current.Verdict switch
        {
            StepVerdict.Passed => "Paso superado: toca «Siguiente».",
            _ when current.HasAllRepetitions
                    && current.Step.NeedsConfirmation
                    && !current.Confirmed =>
                "Todo bien. Haz la comprobación final y toca «Funcionó».",
            _ => "Todo bien.",
        };
    }

    private static ImmutableArray<string> Measurements(LabStatus status)
    {
        var since = status.SinceStep;
        var lines = ImmutableArray.CreateBuilder<string>();

        // One line fewer in the compact strip: the settings go with the foreground.
        lines.Add(
            "Primer plano: "
                + status.Foreground
                + " · Enviar teclas: "
                + (status.SendsKeys ? "sí" : "no")
                + (
                    status.PiecesNotReady > 0
                        ? Format(
                            $" · piezas que no arrancaron: {status.PiecesNotReady} (ver la ventana de control)"
                        )
                        : string.Empty
                )
        );
        lines.Add(
            Format(
                $"Cambios de primer plano: {since.ForegroundChanges} · activaciones de superficies (WM_ACTIVATE): {since.SurfaceActivations} · reg01.violations: {status.TotalViolations}"
            )
        );
        if (status.LastCommand is not null || status.LastLatencyMs is not null)
        {
            lines.Add(
                "Última orden: "
                    + (status.LastCommand ?? "ninguna")
                    + (
                        status.LastLatencyMs is { } latency
                            ? Format($" · latencia: {latency:0.0} ms")
                            : string.Empty
                    )
            );
        }

        if (status.LastLease is not null)
        {
            lines.Add(
                "Concesión: "
                    + status.LastLease
                    + (
                        status.LastRestore is { } restore
                            ? " · devolución: " + restore
                            : string.Empty
                    )
            );
        }

        if (status.ProbeOpen)
        {
            lines.Add(
                Format(
                    $"La sonda recibió: F24 = {since.ProbeF24}, caracteres = {since.ProbeChars}, menú = {since.ProbeMenus}"
                )
            );
        }

        return lines.ToImmutable();
    }

    private static string Format(FormattableString text) => text.ToString(Spanish);
}
