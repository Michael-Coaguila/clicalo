namespace Clicalo.Application.Ports;

/// <summary>
/// Reports the EXTERNAL foreground: windows of other processes that become the foreground (blueprint §7.9).
/// Implemented by <c>Clicalo.Platform.Windows.SysEvents.ForegroundMonitor</c> with
/// <c>SetWinEventHook(EVENT_SYSTEM_FOREGROUND)</c> out of context on the SysEvents thread. Clícalo's own activations
/// are never reported here (<c>ActivationGuard</c> sees them through window messages, §3.5); they only mark that
/// Clícalo was in front, so going back to the same app after a lease is reported as a change. Shell, touch keyboard
/// and Voice access windows are not apps and are skipped too.
/// </summary>
public interface IForegroundMonitor
{
    /// <summary>The last external foreground confirmed so far; null before the first one.</summary>
    ExternalForeground? Current { get; }

    /// <summary>
    /// Raised on the SysEvents thread after each confirmed change to another window. Handlers must not block
    /// (they enqueue work, §3.2).
    /// </summary>
    event EventHandler<ExternalForegroundChangedEventArgs>? ExternalForegroundChanged;
}
