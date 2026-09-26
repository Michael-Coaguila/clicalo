using System.Windows.Interop;
using Clicalo.Application.Ports;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// The hidden owner window of every surface (blueprint §3.5). Owned surfaces stay out of Alt+Tab and the taskbar
/// without <c>WS_EX_TOOLWINDOW</c>, so UI Automation still treats them as normal windows (S3). It is never shown,
/// never activated and lives as long as the UI thread. Created on the UI thread.
/// </summary>
/// <remarks>
/// A bare <c>HwndSource</c> with no content: a <c>WS_POPUP</c> of 0×0 without <c>WS_VISIBLE</c>, with
/// <c>WS_EX_NOACTIVATE</c> and <c>WS_EX_TOOLWINDOW</c> (the anchor itself is never meant for UI Automation). Disposing
/// it destroys the window and, with it, every surface it still owns.
/// </remarks>
public sealed class OwnerAnchor : IDisposable
{
    private const string WindowName = "Clicalo.OwnerAnchor";

    private HwndSource? _source;

    /// <summary>The anchor window; <see cref="WindowToken.None"/> until <see cref="EnsureCreated"/> and after <see cref="Dispose"/>.</summary>
    public WindowToken Window => _source is { } source ? new(source.Handle) : WindowToken.None;

    /// <summary>Creates the hidden window if it does not exist yet and returns it.</summary>
    public WindowToken EnsureCreated()
    {
        if (_source is null)
        {
            var parameters = new HwndSourceParameters(WindowName)
            {
                WindowStyle = unchecked((int)WINDOW_STYLE.WS_POPUP),
                ExtendedWindowStyle = (int)(
                    WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW
                ),
                PositionX = 0,
                PositionY = 0,
                Width = 0,
                Height = 0,
            };
            _source = new HwndSource(parameters);
        }

        return Window;
    }

    /// <summary>Destroys the window.</summary>
    public void Dispose()
    {
        _source?.Dispose();
        _source = null;
    }
}
