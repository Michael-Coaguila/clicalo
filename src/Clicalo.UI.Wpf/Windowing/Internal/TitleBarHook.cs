using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Clicalo.UI.Wpf.Windowing.Internal;

/// <summary>
/// The <c>HwndSource</c> hook of a <see cref="TitleBar"/> (D13). Over the maximize zone it answers
/// <c>HTMAXBUTTON</c> to <c>WM_NCHITTEST</c>, which is what makes Windows 11 show the Snap layouts; Windows then sends
/// that zone non-client messages, so the hook draws it hot (<c>WM_NCMOUSEMOVE</c>, <c>WM_NCMOUSELEAVE</c>) and clicks
/// it on release (<c>WM_NCLBUTTONDOWN</c> is swallowed so Windows draws no button of its own). Over the resize grip it
/// answers <c>HTBOTTOMRIGHT</c>. Every point comes in physical screen pixels and goes through
/// <see cref="System.Windows.Media.Visual.PointFromScreen"/>, so it holds at any scale and on any monitor.
/// </summary>
internal sealed class TitleBarHook
{
    private readonly TitleBar _bar;
    private bool _pressed;

    private TitleBarHook(TitleBar bar) => _bar = bar;

    /// <summary>Adds the hook to <paramref name="source"/>.</summary>
    public static void Attach(HwndSource source, TitleBar bar) =>
        source.AddHook(new TitleBarHook(bar).Hook);

    /// <summary>
    /// How far, in device-independent pixels, a maximized <paramref name="window"/> goes beyond the work area of its
    /// monitor on each side.
    /// </summary>
    public static Thickness Overhang(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !PInvoke.GetWindowRect((HWND)handle, out var bounds))
        {
            return new Thickness(0);
        }

        var monitor = PInvoke.MonitorFromWindow(
            (HWND)handle,
            MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST
        );
        var info = new MONITORINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>(),
        };
        if (!PInvoke.GetMonitorInfo(monitor, ref info))
        {
            return new Thickness(0);
        }

        var scale = VisualTreeHelper.GetDpi(window).DpiScaleX;
        scale = scale > 0 ? scale : 1;
        var work = info.rcWork;
        return new Thickness(
            Math.Max(0, work.left - bounds.left) / scale,
            Math.Max(0, work.top - bounds.top) / scale,
            Math.Max(0, bounds.right - work.right) / scale,
            Math.Max(0, bounds.bottom - work.bottom) / scale
        );
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch ((uint)msg)
        {
            case PInvoke.WM_NCHITTEST:
                if (Over(_bar.Maximize, lParam))
                {
                    handled = true;
                    return (IntPtr)PInvoke.HTMAXBUTTON;
                }

                if (_bar.ResizeGrip is { } grip && Over(grip, lParam))
                {
                    handled = true;
                    return (IntPtr)PInvoke.HTBOTTOMRIGHT;
                }

                break;
            case PInvoke.WM_NCMOUSEMOVE:
                _bar.Maximize.IsFrameHot = wParam.ToInt64() == PInvoke.HTMAXBUTTON;
                break;
            case PInvoke.WM_NCMOUSELEAVE:
                _bar.Maximize.IsFrameHot = false;
                _pressed = false;
                break;
            case PInvoke.WM_NCLBUTTONDOWN when wParam.ToInt64() == PInvoke.HTMAXBUTTON:
                _pressed = true;
                handled = true;
                return IntPtr.Zero;
            case PInvoke.WM_NCLBUTTONUP when wParam.ToInt64() == PInvoke.HTMAXBUTTON:
                handled = true;
                if (_pressed && _bar.Maximize.IsEnabled)
                {
                    _bar.Maximize.ClickFromFrame();
                }

                _pressed = false;
                return IntPtr.Zero;
            default:
                break;
        }

        return IntPtr.Zero;
    }

    private static bool Over(FrameworkElement element, IntPtr lParam)
    {
        if (!element.IsVisible || PresentationSource.FromVisual(element) is null)
        {
            return false;
        }

        var value = lParam.ToInt64();
        var screen = new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
        var point = element.PointFromScreen(screen);
        return new Rect(element.RenderSize).Contains(point);
    }
}
