using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Clicalo.Application.Ports;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Foreground.Support;

/// <summary>
/// The panel of the S4 lease tests: a real <see cref="NonActivatingWindow"/> whose whole area is the «Buscar» (or «⚙»)
/// tile. A finger lifted on it raises <see cref="Tapped"/> on its UI thread, inside its window procedure, as the
/// gesture of a real tile would: the handler asks for the lease from the UI thread, as the product does.
/// </summary>
public sealed class LeasePanel : NonActivatingWindow
{
    /// <summary>Creates the hidden panel of <paramref name="width"/> × <paramref name="height"/> logical units.</summary>
    public LeasePanel(SurfaceId id, SurfaceRegistry registry, double width, double height)
        : base(id, registry)
    {
        Width = width;
        Height = height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x57, 0x9A)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(2),
            Focusable = false,
        };
    }

    /// <summary>A contact went up on the panel (<c>WM_POINTERUP</c>). Raised on the panel's UI thread.</summary>
    public event EventHandler? Tapped;

    /// <summary>The window as a handle (zero before it exists).</summary>
    public nint Handle => SurfaceWindow.Handle;

    /// <inheritdoc />
    protected override void OnSurfaceInitialized() =>
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(OnMessage);

    private nint OnMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)msg == NativeSurface.WmPointerUp)
        {
            Tapped?.Invoke(this, EventArgs.Empty);
        }

        return 0;
    }
}
