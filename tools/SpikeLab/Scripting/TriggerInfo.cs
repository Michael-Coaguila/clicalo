using Clicalo.Domain.Touch;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>What happened that may count as a repetition: a tap, a drag, a UI Automation command, a lease or a forced activation.</summary>
/// <param name="Kind">The kind of trigger.</param>
internal sealed record TriggerInfo(StepTrigger Kind)
{
    /// <summary>The surface it happened on (for example «Panel#0»); null for leases from the tray or a hotkey.</summary>
    public string? Surface { get; init; }

    /// <summary>The group of the surface.</summary>
    public SurfaceGroup Group { get; init; } = SurfaceGroup.Any;

    /// <summary>The tile id.</summary>
    public string? Tile { get; init; }

    /// <summary>The pattern of a UI Automation command.</summary>
    public CommandPattern? Pattern { get; init; }

    /// <summary>The device of a tap or drag.</summary>
    public PointerKind? Pointer { get; init; }

    /// <summary>
    /// How the input arrived: «pointer» (<c>PointerInputSource</c> and <c>GestureHost</c>), «uia», «hotkey», «tray» or
    /// «lab».
    /// </summary>
    public string Channel { get; init; } = "lab";

    /// <summary>The voice number the tile showed, when «Números de voz» was on.</summary>
    public int? VoiceNumber { get; init; }
}
