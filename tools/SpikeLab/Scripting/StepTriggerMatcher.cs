using Clicalo.Domain.Touch;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>Decides whether something that happened counts as a repetition of the current step.</summary>
internal static class StepTriggerMatcher
{
    /// <summary>
    /// Null when <paramref name="candidate"/> counts for <paramref name="step"/>; otherwise why not, in one sentence
    /// the guide strip can show.
    /// </summary>
    public static string? Mismatch(ScriptStep step, RepetitionEvidence candidate)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(candidate);
        var trigger = candidate.Trigger;
        if (trigger is null)
        {
            return "No hay nada que contar.";
        }

        if (step.Trigger == StepTrigger.Manual)
        {
            return "Este paso se cuenta con «Funcionó» y «Falló».";
        }

        if (trigger.Kind != step.Trigger)
        {
            return "Este paso no se cuenta con " + Describe(trigger.Kind) + ".";
        }

        return trigger.Kind switch
        {
            StepTrigger.SurfaceTap or StepTrigger.HandleDrag => SurfaceMismatch(step, trigger),
            StepTrigger.UiaCommand => CommandMismatch(step, trigger),
            StepTrigger.LeaseCycle => LeaseMismatch(step, candidate.Lease),
            _ => null,
        };
    }

    private static string? SurfaceMismatch(ScriptStep step, TriggerInfo trigger)
    {
        if (step.Surface != SurfaceGroup.Any && trigger.Group != step.Surface)
        {
            return "Este paso se hace sobre " + Describe(step.Surface) + ".";
        }

        // A tap that arrived as promoted mouse (pointer layer pending) has no device: it counts, and the report shows
        // its channel.
        if (step.Pointer is { } pointer && trigger.Pointer is { } actual && actual != pointer)
        {
            return "Este paso se hace con "
                + Describe(pointer)
                + "; el toque con "
                + Describe(actual)
                + " no cuenta.";
        }

        return null;
    }

    private static string? CommandMismatch(ScriptStep step, TriggerInfo trigger)
    {
        if (step.Tile is { } tile && !string.Equals(trigger.Tile, tile, StringComparison.Ordinal))
        {
            return "Este paso se hace sobre otra ficha.";
        }

        if (step.Pattern is { } pattern && trigger.Pattern != pattern)
        {
            return "Este paso espera el patrón " + pattern + ".";
        }

        return null;
    }

    private static string? LeaseMismatch(ScriptStep step, LeaseEvidence? lease)
    {
        if (lease is null)
        {
            return "No hubo concesión.";
        }

        if (step.Lease is { } kind && lease.Kind != kind)
        {
            return "Este paso espera una concesión " + kind + ".";
        }

        if (step.Origin is { } origin && lease.Origin != origin)
        {
            return "Este paso espera el origen " + origin + ".";
        }

        return null;
    }

    private static string Describe(StepTrigger trigger) =>
        trigger switch
        {
            StepTrigger.SurfaceTap => "un toque",
            StepTrigger.HandleDrag => "un arrastre del asa",
            StepTrigger.UiaCommand => "una orden de UI Automation",
            StepTrigger.LeaseCycle => "una concesión",
            StepTrigger.ForcedActivation => "una activación forzada",
            _ => "«Funcionó» y «Falló»",
        };

    private static string Describe(SurfaceGroup surface) =>
        surface switch
        {
            SurfaceGroup.Panel => "el panel",
            SurfaceGroup.TabWithSide => "la Pestaña o su lateral",
            SurfaceGroup.Bubble => "la burbuja",
            _ => "cualquier superficie",
        };

    private static string Describe(PointerKind pointer) =>
        pointer switch
        {
            PointerKind.Finger => "el dedo",
            PointerKind.Pen => "el lápiz",
            _ => "el mouse",
        };
}
