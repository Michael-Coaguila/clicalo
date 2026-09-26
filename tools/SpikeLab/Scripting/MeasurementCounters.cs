using System.Runtime.InteropServices;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>
/// The running counters of the laboratory's automatic measurements. A repetition keeps the difference between the
/// counters at its end and at its start (<see cref="op_Subtraction"/>).
/// </summary>
/// <param name="ForegroundChanges">Every change of <c>GetForegroundWindow</c> (<c>EVENT_SYSTEM_FOREGROUND</c>).</param>
/// <param name="OwnForegroundChanges">Changes whose new foreground is a surface of the laboratory.</param>
/// <param name="SurfaceActivations">Activation messages received by the lab surfaces.</param>
/// <param name="Violations"><c>ActivationGuard.Violations</c> (<c>reg01.violations</c>).</param>
/// <param name="ProbeF24">Key messages of <c>VK_F24</c> received by InputProbe.</param>
/// <param name="ProbeChars">Character messages received by InputProbe (counted, never read).</param>
/// <param name="ProbeMenus"><c>WM_SYSCOMMAND(SC_KEYMENU)</c> received by InputProbe.</param>
/// <param name="ProbeModifierKeys">Shift, Ctrl, Alt or Windows key messages received by InputProbe (recorded, not a failure).</param>
/// <param name="RightsChords">Internal rights chords (Ctrl+Alt+Shift+F24) injected by the laboratory.</param>
/// <param name="RefusedInjections">Batches the laboratory refused to inject because a safety rule did not hold.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct MeasurementCounters(
    long ForegroundChanges,
    long OwnForegroundChanges,
    long SurfaceActivations,
    long Violations,
    long ProbeF24,
    long ProbeChars,
    long ProbeMenus,
    long ProbeModifierKeys,
    long RightsChords,
    long RefusedInjections
)
{
    /// <summary>The difference between two readings: what happened in between.</summary>
    public static MeasurementCounters operator -(
        MeasurementCounters end,
        MeasurementCounters start
    ) =>
        new(
            end.ForegroundChanges - start.ForegroundChanges,
            end.OwnForegroundChanges - start.OwnForegroundChanges,
            end.SurfaceActivations - start.SurfaceActivations,
            end.Violations - start.Violations,
            end.ProbeF24 - start.ProbeF24,
            end.ProbeChars - start.ProbeChars,
            end.ProbeMenus - start.ProbeMenus,
            end.ProbeModifierKeys - start.ProbeModifierKeys,
            end.RightsChords - start.RightsChords,
            end.RefusedInjections - start.RefusedInjections
        );

    /// <summary>Named alternative of the subtraction operator (CA2225).</summary>
    public static MeasurementCounters Subtract(
        MeasurementCounters end,
        MeasurementCounters start
    ) => end - start;
}
