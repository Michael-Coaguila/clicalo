using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Platform.Windows.SysEvents;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>
/// The tray menu (blueprint §3.6, §8.1): an own hidden top-level window (<c>WS_EX_TOOLWINDOW</c>, 0×0 off screen) on
/// the <see cref="SysEventsThread"/>, the ONLY file allowed to call <c>TrackPopupMenuEx</c>
/// (banned-api-exceptions.json: tray-menu-host). It runs inside a <c>TrayMenu</c> lease whose target is
/// <see cref="Window"/>; after the menu closes it posts <c>WM_NULL</c> and the lease restores the previous window.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the foreground package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class TrayMenuHost : IDisposable
{
    /// <summary>Creates the host on <paramref name="thread"/>.</summary>
    public TrayMenuHost(SysEventsThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        Thread = thread;
    }

    /// <summary>The thread that owns the host window.</summary>
    public SysEventsThread Thread { get; }

    /// <summary>The hidden host window: the target of the <c>TrayMenu</c> lease.</summary>
    public WindowToken Window => throw new NotImplementedException("M1 foreground package.");

    /// <summary>Creates the hidden host window on the SysEvents thread.</summary>
    public Task StartAsync() => throw new NotImplementedException("M1 foreground package.");

    /// <summary>
    /// Shows <paramref name="items"/> at <paramref name="anchor"/> with <c>TrackPopupMenuEx(TPM_RETURNCMD)</c> while
    /// the host is in the foreground. Completes with the chosen <see cref="TrayMenuItem.Id"/>, or null when the menu
    /// was dismissed.
    /// </summary>
    public Task<int?> ShowMenuAsync(IReadOnlyList<TrayMenuItem> items, PhysicalPoint anchor) =>
        throw new NotImplementedException("M1 foreground package.");

    /// <summary>Destroys the host window.</summary>
    public void Dispose() { }
}
