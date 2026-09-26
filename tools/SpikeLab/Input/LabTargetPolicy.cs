namespace Clicalo.Tools.SpikeLab.Input;

/// <summary>
/// Where the laboratory may inject its internal chords (S4.md): a window of its own process or the InputProbe window.
/// </summary>
internal static class LabTargetPolicy
{
    /// <summary>True when <paramref name="window"/> may receive an internal chord.</summary>
    /// <param name="window">The window in front.</param>
    /// <param name="windowProcessId">Its process.</param>
    /// <param name="ownProcessId">The laboratory's process.</param>
    /// <param name="probeWindow">The InputProbe window, or zero when the probe is not open.</param>
    public static bool IsOwnOrProbe(
        nint window,
        uint windowProcessId,
        uint ownProcessId,
        nint probeWindow
    ) =>
        window != 0
        && (windowProcessId == ownProcessId || (probeWindow != 0 && window == probeWindow));
}
