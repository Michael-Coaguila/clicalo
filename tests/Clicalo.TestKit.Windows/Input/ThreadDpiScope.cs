using Windows.Win32;
using Windows.Win32.UI.HiDpi;

namespace Clicalo.TestKit.Windows.Input;

/// <summary>
/// Runs the calling thread per-monitor DPI aware (v2) until disposed, so that <c>WindowFromPoint</c>,
/// <c>GetCursorPos</c>, the virtual screen metrics and the injected coordinates all speak physical pixels, whatever the
/// awareness of the test process.
/// </summary>
internal readonly struct ThreadDpiScope : IDisposable
{
    private readonly DPI_AWARENESS_CONTEXT _previous;

    private ThreadDpiScope(DPI_AWARENESS_CONTEXT previous) => _previous = previous;

    /// <summary>Switches the calling thread to <c>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2</c>.</summary>
    public static ThreadDpiScope PerMonitorV2() =>
        new(
            PInvoke.SetThreadDpiAwarenessContext(
                DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
            )
        );

    /// <summary>Restores the awareness the thread had before.</summary>
    public void Dispose()
    {
        if (_previous != default)
        {
            _ = PInvoke.SetThreadDpiAwarenessContext(_previous);
        }
    }
}
