namespace Clicalo.Application.Ports;

/// <summary>
/// The probable cause of an activation without a lease, recorded with every <c>reg01.violations</c> increment
/// (blueprint §3.5).
/// </summary>
public enum ActivationCause
{
    /// <summary>No recent event explains it.</summary>
    Unknown,

    /// <summary>It followed a <c>WM_DPICHANGED</c> (WPF calls <c>SetWindowPos</c> without <c>SWP_NOACTIVATE</c>, #7561).</summary>
    DpiChange,

    /// <summary>It followed a change of the topmost state.</summary>
    Topmost,

    /// <summary>It followed showing the surface.</summary>
    Show,

    /// <summary>Another process called <c>SetForegroundWindow</c> on the surface (the negative test does this).</summary>
    External,
}
