using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// The hidden owner window of every surface (blueprint §3.5). Owned surfaces stay out of Alt+Tab and the taskbar
/// without <c>WS_EX_TOOLWINDOW</c>, so UI Automation still treats them as normal windows (S3). It is never shown,
/// never activated and lives as long as the UI thread. Created on the UI thread.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the windowing package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class OwnerAnchor : IDisposable
{
    /// <summary>The anchor window; created by <see cref="EnsureCreated"/>.</summary>
    public WindowToken Window => throw new NotImplementedException("M1 windowing package.");

    /// <summary>Creates the hidden window if it does not exist yet and returns it.</summary>
    public WindowToken EnsureCreated() =>
        throw new NotImplementedException("M1 windowing package.");

    /// <summary>Destroys the window.</summary>
    public void Dispose() { }
}
