using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.UI.Wpf.Windowing.Internal;

/// <summary>
/// The window styles and z-order of a surface (blueprint §3.5), read and written with Win32 so that nothing goes
/// through WPF's activating paths. Every position change carries <c>SWP_NOACTIVATE</c>.
/// </summary>
internal static class SurfaceStyles
{
    /// <summary>Extended styles every surface has: it never activates and it stays in the topmost band.</summary>
    public const WINDOW_EX_STYLE Required =
        WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TOPMOST;

    /// <summary>
    /// Extended styles a surface never has: <c>WS_EX_APPWINDOW</c> would put it in Alt+Tab and the taskbar, and
    /// <c>WS_EX_TOOLWINDOW</c> would make UI Automation treat it as a tool window (the owner anchor keeps it out of
    /// Alt+Tab instead).
    /// </summary>
    public const WINDOW_EX_STYLE Forbidden =
        WINDOW_EX_STYLE.WS_EX_APPWINDOW | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;

    private const SET_WINDOW_POS_FLAGS KeepPlaceAndSize =
        SET_WINDOW_POS_FLAGS.SWP_NOMOVE
        | SET_WINDOW_POS_FLAGS.SWP_NOSIZE
        | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
        | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER;

    /// <summary>The extended style of <paramref name="window"/>.</summary>
    public static WINDOW_EX_STYLE Get(HWND window) =>
        (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

    /// <summary>
    /// Adds <see cref="Required"/> and removes <see cref="Forbidden"/>. Changing an extended style never activates.
    /// </summary>
    public static void ApplyNonActivation(HWND window)
    {
        var current = Get(window);
        var wanted = (current | Required) & ~Forbidden;
        if (wanted != current)
        {
            Set(window, wanted);
        }
    }

    /// <summary>
    /// Adds (<paramref name="noActivate"/> true) or removes <c>WS_EX_NOACTIVATE</c>. Returns true when the style
    /// changed. Works from any thread of the process.
    /// </summary>
    public static bool SetNoActivate(HWND window, bool noActivate)
    {
        var current = Get(window);
        var wanted = noActivate
            ? current | WINDOW_EX_STYLE.WS_EX_NOACTIVATE
            : current & ~WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        if (wanted == current)
        {
            return false;
        }

        Set(window, wanted);
        return true;
    }

    /// <summary>
    /// Puts <paramref name="window"/> at the top of the topmost band without activating, moving or resizing it, and
    /// shows it when <paramref name="show"/> is true (<c>SWP_SHOWWINDOW</c>). The owner anchor is not reordered.
    /// </summary>
    public static bool PlaceOnTop(HWND window, bool show) =>
        PInvoke.SetWindowPos(
            window,
            HWND.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            show ? KeepPlaceAndSize | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW : KeepPlaceAndSize
        );

    private static void Set(HWND window, WINDOW_EX_STYLE style) =>
        _ = PInvoke.SetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)style);
}
