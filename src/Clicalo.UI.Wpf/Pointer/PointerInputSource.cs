using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Clicalo.Domain.Touch;

namespace Clicalo.UI.Wpf.Pointer;

/// <summary>
/// Translates the <c>WM_POINTER*</c> messages of ONE window into <see cref="PointerFrame"/>s (blueprint §8.3,
/// ADR-0006): <c>GetPointerFrameTouchInfo</c> for fingers (with <c>rcContact</c>), <c>GetPointerPenInfo</c> for pens
/// and <c>GetPointerInfo</c> for the mouse; positions in physical screen pixels; timestamps from the
/// <see cref="TimeProvider"/>; the origin from <c>GetCurrentInputMessageSource</c>. It hooks the window's
/// <c>HwndSource</c> and lives on its UI thread.
/// </summary>
/// <remarks>
/// It never captures the mouse (<c>SetCapture</c> belongs to the foreground window, ADR-0005), never activates the
/// window and marks the pointer messages it consumes as handled, so WPF does not promote them to mouse input on a
/// surface. On a Workspace window (Control Center) it also turns vertical drags into panning of the
/// <c>ScrollViewer</c>, because <c>PanningMode</c> needs the WPF touch stack that is switched off.
/// </remarks>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the pointer package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class PointerInputSource : IDisposable
{
    /// <summary>Creates a source for <paramref name="window"/> that delivers to <paramref name="sink"/>.</summary>
    /// <param name="window">The window whose pointer messages are translated.</param>
    /// <param name="sink">Receives the frames synchronously.</param>
    /// <param name="timeProvider">Stamps the frames.</param>
    public PointerInputSource(Window window, IPointerFrameSink sink, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Window = window;
        Sink = sink;
        Clock = timeProvider;
    }

    /// <summary>The window whose pointer messages are translated.</summary>
    public Window Window { get; }

    /// <summary>Receives the frames.</summary>
    public IPointerFrameSink Sink { get; }

    /// <summary>Stamps the frames.</summary>
    public TimeProvider Clock { get; }

    /// <summary>True between <see cref="Attach"/> and <see cref="Detach"/>.</summary>
    public bool IsAttached => throw new NotImplementedException("M1 pointer package.");

    /// <summary>
    /// Hooks the window's <c>HwndSource</c> (the handle must exist: call it from
    /// <c>NonActivatingWindow.OnSurfaceInitialized</c> or after <c>SourceInitialized</c>).
    /// </summary>
    public void Attach() => throw new NotImplementedException("M1 pointer package.");

    /// <summary>
    /// Removes the hook. Every contact still down is delivered as <see cref="PointerPhase.Cancel"/> first, so no
    /// hold stays active.
    /// </summary>
    public void Detach() => throw new NotImplementedException("M1 pointer package.");

    /// <summary>Detaches if attached.</summary>
    public void Dispose() { }
}
