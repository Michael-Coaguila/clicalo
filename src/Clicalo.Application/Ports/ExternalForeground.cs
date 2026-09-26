namespace Clicalo.Application.Ports;

/// <summary>
/// A foreground window of another process, confirmed with <c>GetForegroundWindow</c> after
/// <c>EVENT_SYSTEM_FOREGROUND</c> (blueprint §7.9). The richer <c>ForegroundInfo</c> of §7.9 (AUMID, kind, keyboard
/// layout) extends this record in M2; the fields here are the ones S1 and S4 need. No title (LOG-001).
/// </summary>
/// <param name="Window">The confirmed foreground window.</param>
/// <param name="ProcessId">Its process (<c>GetWindowThreadProcessId</c>); never Clícalo's own.</param>
/// <param name="ThreadId">Its UI thread.</param>
/// <param name="ObservedAt">When the monitor confirmed it, from the monitor's <see cref="TimeProvider"/>.</param>
public sealed record ExternalForeground(
    WindowToken Window,
    uint ProcessId,
    uint ThreadId,
    DateTimeOffset ObservedAt
)
{
    private readonly uint _appProcessId;

    /// <summary>
    /// The process of the app the user sees. For a Store app hosted by <c>ApplicationFrameHost.exe</c> it is the
    /// hosted app's process (found among the frame's child windows); otherwise it equals <see cref="ProcessId"/>.
    /// </summary>
    public uint AppProcessId
    {
        get => _appProcessId != 0 ? _appProcessId : ProcessId;
        init => _appProcessId = value;
    }

    /// <summary>Whether <see cref="AppProcessId"/> runs elevated; <see cref="ProcessElevation.Unknown"/> when it could not be read.</summary>
    public ProcessElevation Elevation { get; init; }
}
