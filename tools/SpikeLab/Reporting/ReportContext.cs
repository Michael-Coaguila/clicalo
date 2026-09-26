using System.Collections.Immutable;
using Clicalo.Tools.SpikeLab.Composition;

namespace Clicalo.Tools.SpikeLab.Reporting;

/// <summary>Everything a report needs besides the script run itself.</summary>
/// <param name="Machine">Machine and build.</param>
/// <param name="UpdatedAt">When the report was written.</param>
/// <param name="Components">The real pieces and their state.</param>
/// <param name="Events">The timeline, oldest first (at most <see cref="MaxEvents"/>).</param>
internal sealed record ReportContext(
    MachineInfo Machine,
    DateTimeOffset UpdatedAt,
    ImmutableArray<LabComponent> Components,
    ImmutableArray<LabLogEntry> Events
)
{
    /// <summary>Largest timeline kept in a report; older events are dropped and counted.</summary>
    public const int MaxEvents = 5000;

    /// <summary>Events dropped because the timeline was full.</summary>
    public int DroppedEvents { get; init; }

    /// <summary>True while the panel sends real chords to the app in front («Enviar teclas»).</summary>
    public bool SendsKeys { get; init; }

    /// <summary>True while «Números de voz» is on.</summary>
    public bool VoiceNumbers { get; init; }
}
