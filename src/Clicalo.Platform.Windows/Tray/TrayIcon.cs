using System.Diagnostics.CodeAnalysis;
using Clicalo.Platform.Windows.SysEvents;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>
/// The notification area icon (blueprint §8.1): <c>Shell_NotifyIcon</c> with <c>NOTIFYICON_VERSION_4</c> and its
/// callback message on the <see cref="SysEventsThread"/>. Clicking it never activates a window by itself: the click
/// only gives foreground rights (<c>LeaseOrigin.Tray</c>); showing the panel is passive and the menu goes through
/// <see cref="TrayMenuHost"/> inside a <c>TrayMenu</c> lease. It is re-added after <c>TaskbarCreated</c>.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the foreground package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class TrayIcon : IDisposable
{
    /// <summary>Creates the icon on <paramref name="thread"/>.</summary>
    public TrayIcon(SysEventsThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        Thread = thread;
    }

    /// <summary>Primary click or <c>NIN_SELECT</c>/<c>NIN_KEYSELECT</c>: show or hide the panel.</summary>
    public event EventHandler<TrayIconEventArgs>? Invoked;

    /// <summary>Secondary click or <c>WM_CONTEXTMENU</c>: open the menu.</summary>
    public event EventHandler<TrayIconEventArgs>? MenuRequested;

    /// <summary>The thread that owns the callback window.</summary>
    public SysEventsThread Thread { get; }

    /// <summary>Adds the icon with the localized <paramref name="tooltip"/>.</summary>
    public Task ShowAsync(string tooltip) =>
        throw new NotImplementedException("M1 foreground package.");

    /// <summary>Changes the localized tooltip (language change, paused state).</summary>
    public Task SetTooltipAsync(string tooltip) =>
        throw new NotImplementedException("M1 foreground package.");

    /// <summary>Removes the icon.</summary>
    public void Dispose() { }

    private void RaiseInvoked(TrayIconEventArgs args) => Invoked?.Invoke(this, args);

    private void RaiseMenuRequested(TrayIconEventArgs args) => MenuRequested?.Invoke(this, args);
}
