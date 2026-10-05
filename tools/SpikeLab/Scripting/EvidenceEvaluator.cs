using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Application.Foreground;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>
/// Applies the automatic checks of a step to the evidence of one repetition and explains every failure in one
/// sentence, as the guide strip and the report show it.
/// </summary>
internal static class EvidenceEvaluator
{
    private static readonly CultureInfo Display = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>The problems found; empty when the repetition passed every check.</summary>
    /// <param name="checks">The checks of the step.</param>
    /// <param name="evidence">What was measured.</param>
    /// <param name="restoreBudget">The limit of the forced-activation restoration (<c>Timings.Windowing.ViolationRestoreBudget</c>).</param>
    public static ImmutableArray<string> Evaluate(
        EvidenceCheck checks,
        RepetitionEvidence evidence,
        TimeSpan restoreBudget
    )
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var problems = ImmutableArray.CreateBuilder<string>();
        var delta = evidence.Delta;

        if (checks.HasFlag(EvidenceCheck.NoSurfaceActivation) && delta.SurfaceActivations > 0)
        {
            problems.Add(
                Format(
                    $"Una superficie recibió {delta.SurfaceActivations} mensajes de activación (WM_ACTIVATE, WM_NCACTIVATE o WM_ACTIVATEAPP)."
                )
            );
        }

        if (checks.HasFlag(EvidenceCheck.NoOwnForeground) && delta.OwnForegroundChanges > 0)
        {
            problems.Add(
                Format(
                    $"El primer plano pasó {delta.OwnForegroundChanges} veces a una ventana de SpikeLab."
                )
            );
        }

        if (checks.HasFlag(EvidenceCheck.NoViolation) && delta.Violations > 0)
        {
            problems.Add(Format($"reg01.violations subió en {delta.Violations}."));
        }

        if (checks.HasFlag(EvidenceCheck.TargetStillInFront) && evidence.TargetInFront == false)
        {
            problems.Add(
                "La app objetivo ("
                    + (evidence.TargetProcess ?? "desconocida")
                    + ") dejó de estar delante; ahora está "
                    + (evidence.ForegroundProcess ?? "ninguna ventana")
                    + "."
            );
        }

        CheckForcedActivation(checks, evidence, restoreBudget, problems);
        CheckLease(checks, evidence, problems);

        if (
            checks.HasFlag(EvidenceCheck.TextReachedField)
            && evidence.Lease is { Granted: true }
            && evidence.FieldsWithText < evidence.FieldCount
        )
        {
            problems.Add(
                Format(
                    $"Solo {evidence.FieldsWithText} de {evidence.FieldCount} campos recibieron texto."
                )
            );
        }

        if (checks.HasFlag(EvidenceCheck.ProbeSilent))
        {
            if (delta.ProbeF24 > 0 || delta.ProbeChars > 0 || delta.ProbeMenus > 0)
            {
                problems.Add(
                    Format(
                        $"La sonda recibió F24 = {delta.ProbeF24}, caracteres = {delta.ProbeChars}, menú = {delta.ProbeMenus}."
                    )
                );
            }
        }

        if (
            checks.HasFlag(EvidenceCheck.ProbeWasTarget)
            && evidence.Lease is { } lease
            && !lease.PreviousWasProbe
        )
        {
            problems.Add(
                "La sonda no estaba delante al pedir la concesión (estaba "
                    + (lease.PreviousProcess ?? "ninguna ventana")
                    + ")."
            );
        }

        if (
            checks.HasFlag(EvidenceCheck.VoiceNumberInName) && evidence.Trigger?.VoiceNumber is null
        )
        {
            problems.Add(
                "La ficha no tenía número de voz: activa «Números de voz» en la ventana de control."
            );
        }

        return problems.ToImmutable();
    }

    private static void CheckForcedActivation(
        EvidenceCheck checks,
        RepetitionEvidence evidence,
        TimeSpan restoreBudget,
        ImmutableArray<string>.Builder problems
    )
    {
        if (checks.HasFlag(EvidenceCheck.ViolationCounted) && evidence.Delta.Violations < 1)
        {
            problems.Add(
                Format(
                    $"reg01.violations subió en {evidence.Delta.Violations}; debía subir al menos en 1."
                )
            );
        }

        if (checks.HasFlag(EvidenceCheck.RestoredWithinBudget))
        {
            if (evidence.RestoredWithinMs is not { } restored)
            {
                problems.Add("El primer plano anterior no volvió.");
            }
            else if (restored > restoreBudget.TotalMilliseconds)
            {
                problems.Add(
                    Format(
                        $"El primer plano anterior volvió en {restored:0} ms; el límite es {restoreBudget.TotalMilliseconds:0} ms."
                    )
                );
            }
        }

        if (
            checks.HasFlag(EvidenceCheck.NoActivateStyleKept)
            && evidence.NoActivateStyleKept != true
        )
        {
            problems.Add("El panel perdió WS_EX_NOACTIVATE.");
        }
    }

    private static void CheckLease(
        EvidenceCheck checks,
        RepetitionEvidence evidence,
        ImmutableArray<string>.Builder problems
    )
    {
        var lease = evidence.Lease;
        if (checks.HasFlag(EvidenceCheck.LeaseGranted))
        {
            if (lease is null)
            {
                problems.Add("No se pidió ninguna concesión.");
            }
            else if (lease.Unavailable is { } unavailable)
            {
                problems.Add("La concesión no pudo pedirse: " + unavailable);
            }
            else if (!lease.Granted)
            {
                problems.Add(
                    "La concesión se denegó (" + (lease.Denial?.ToString() ?? "sin motivo") + ")."
                );
            }
        }

        if (checks.HasFlag(EvidenceCheck.ForegroundReturned) && lease is { Granted: true })
        {
            if (lease.Restore is not (RestoreOutcome.Restored or RestoreOutcome.RestoredAfterRetry))
            {
                problems.Add(
                    "La devolución del primer plano terminó en "
                        + (lease.Restore?.ToString() ?? "nada")
                        + "."
                );
            }
            else if (!lease.ForegroundReturned)
            {
                problems.Add(
                    "La devolución dijo "
                        + lease.Restore
                        + " pero delante no está la ventana de antes."
                );
            }
        }
    }

    private static string Format(FormattableString text) => text.ToString(Display);
}
