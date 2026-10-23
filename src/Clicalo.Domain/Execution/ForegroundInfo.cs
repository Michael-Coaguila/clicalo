using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Execution;

/// <summary>The verified external foreground app as the engine sees it (blueprint §7.9).</summary>
/// <param name="Window">Its window.</param>
/// <param name="Process">Its process (ApplicationFrameHost resolved to the real app).</param>
/// <param name="Epoch">Raised on every verified change; injections carry it (INV-6).</param>
/// <param name="Elevation">Whether Clícalo may send to it.</param>
/// <param name="Layout">Its keyboard layout.</param>
public sealed record ForegroundInfo(
    ForegroundWindowId Window,
    ProcessName Process,
    long Epoch,
    ElevationState Elevation,
    KeyboardLayoutSnapshot Layout
)
{
    /// <summary>
    /// The app as the notices name it («Microsoft Word», «Editor del Registro»): the description of its executable once
    /// it was read (<see cref="EngineEvent.ForegroundNamed"/>); null until then (EJE-003).
    /// </summary>
    public string? DisplayName { get; init; }
}
