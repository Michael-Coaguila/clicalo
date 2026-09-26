namespace Clicalo.Application.Foreground;

/// <summary>How the foreground was given back when a lease ended (blueprint §3.6).</summary>
public enum RestoreOutcome
{
    /// <summary>The previous window is in front, verified with <c>GetForegroundWindow</c> at the first attempt.</summary>
    Restored,

    /// <summary>The previous window is in front after the single retry.</summary>
    RestoredAfterRetry,

    /// <summary>Windows refused; the taskbar button of the destination flashes instead (PRB-007).</summary>
    Flashed,

    /// <summary>
    /// The previous window could not be restored. For <see cref="LeaseKind.TextInput"/> nothing is sent and the
    /// user is told (BUS-002 d).
    /// </summary>
    Failed,
}
