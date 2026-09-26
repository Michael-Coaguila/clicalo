using Clicalo.Application.Ports;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.UI.Wpf.Windowing.Internal;

/// <summary>
/// The common <c>HwndSource</c> hook of every surface (blueprint §3.5). It is added in
/// <see cref="NonActivatingWindow"/>'s sealed <c>OnSourceInitialized</c>, after WPF's own hooks, so it sees every
/// message first.
/// </summary>
/// <remarks>
/// <list type="table">
/// <item><term><c>WM_MOUSEACTIVATE</c>, <c>WM_POINTERACTIVATE</c></term><description><c>MA_NOACTIVATE</c> and
/// <c>PA_NOACTIVATE</c>: a click or a touch reaches the surface without activating it.</description></item>
/// <item><term><c>WM_WINDOWPOSCHANGING</c></term><description>Adds <c>SWP_NOACTIVATE</c> to every position change,
/// WPF's included, except while a <c>TextInput</c> or <c>KeyboardNavigation</c> lease allows the surface to activate
/// (<see cref="SurfaceRegistry.IsActivationAllowed"/>). Windows has already decided the activation of that call by
/// then (measured in S1), so this keeps the flags honest for the rest of the chain but does not by itself stop a
/// <c>SetWindowPos</c> without <c>SWP_NOACTIVATE</c>: the product never makes one, and WPF's known one is
/// <c>WM_DPICHANGED</c> below.</description></item>
/// <item><term><c>WM_ACTIVATE</c>, <c>WM_NCACTIVATE(TRUE)</c>, <c>WM_ACTIVATEAPP(TRUE)</c></term><description>To
/// <see cref="ActivationGuard.OnActivated"/>. A <c>WM_ACTIVATE</c> the guard judges a violation is handled here, so
/// neither WPF nor <c>DefWindowProc</c> reacts to it; its matching <c>WA_INACTIVE</c> is swallowed too.</description></item>
/// <item><term><c>WM_DPICHANGED</c></term><description>Handled here (#7561): WPF's <c>HwndTarget</c> answers it with
/// <c>SetWindowPos(SWP_NOZORDER | SWP_ASYNCWINDOWPOS)</c>, which activates the surface. The hook notes the probable
/// cause, puts the bounds of a running <see cref="NonActivatingWindow.MovePassive"/> in place of the suggested
/// rectangle, hands the message to WPF inside an <see cref="ActivationVeto"/> (WPF must see it to rescale, ACC-008),
/// applies the rectangle with <c>SWP_NOZORDER | SWP_NOACTIVATE</c> (in case WPF did not) and marks it
/// handled.</description></item>
/// <item><term><c>WM_GETDPISCALEDSIZE</c></term><description>The surface's own logical size at the new DPI, so it
/// keeps its size in logical units across monitors.</description></item>
/// <item><term><c>WM_NCHITTEST</c></term><description><c>HTNOWHERE</c> inside
/// <see cref="NonActivatingWindow.ShadowMargin"/>: the shadow never acts as the surface. Letting the click through to
/// the window below is decided in spike S6 (<c>SetWindowRgn</c> or a layered window).</description></item>
/// <item><term><c>WM_DISPLAYCHANGE</c>, theme changes</term><description>Ask for an integrity check
/// (<see cref="SurfaceIntegrityCheck"/>); so does <c>WM_DPICHANGED</c>.</description></item>
/// </list>
/// </remarks>
internal sealed unsafe class SurfaceHook(NonActivatingWindow surface, SurfaceRegistry registry)
{
    private const double BaseDpi = 96;

    private bool _forwardingDpiChange;

    /// <summary>The <c>HwndSourceHook</c>.</summary>
    public nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch ((uint)msg)
        {
            case PInvoke.WM_MOUSEACTIVATE:
                handled = true;
                return (nint)PInvoke.MA_NOACTIVATE;

            case PInvoke.WM_POINTERACTIVATE:
                handled = true;
                return (nint)PInvoke.PA_NOACTIVATE;

            case PInvoke.WM_WINDOWPOSCHANGING:
                OnWindowPosChanging((WINDOWPOS*)lParam);
                break;

            case PInvoke.WM_ACTIVATE:
                handled = OnActivate(wParam);
                break;

            case PInvoke.WM_NCACTIVATE:
                if (wParam != 0)
                {
                    _ = registry.Guard.OnActivated(
                        surface,
                        ActivationMessage.NcActivate,
                        registry.Hints.Current
                    );
                }

                break;

            case PInvoke.WM_ACTIVATEAPP:
                OnActivateApp(wParam != 0);
                break;

            case PInvoke.WM_DPICHANGED when !_forwardingDpiChange:
                OnDpiChanged((HWND)hwnd, wParam, (RECT*)lParam);
                handled = true;
                return 0;

            case PInvoke.WM_GETDPISCALEDSIZE:
                handled = OnGetDpiScaledSize(wParam, (SIZE*)lParam);
                return handled ? 1 : 0;

            case PInvoke.WM_NCHITTEST:
                handled = IsInShadowMargin(hwnd, lParam);
                return handled ? (nint)PInvoke.HTNOWHERE : 0;

            case PInvoke.WM_DISPLAYCHANGE
            or PInvoke.WM_THEMECHANGED
            or PInvoke.WM_SYSCOLORCHANGE
            or PInvoke.WM_SETTINGCHANGE:
                registry.RequestIntegrityCheck();
                break;

            case PInvoke.WM_NCDESTROY:
                surface.OnHandleDestroyed();
                break;
        }

        return 0;
    }

    private static int Scale(double logical, double dpi) =>
        (int)Math.Round(logical * dpi / BaseDpi, MidpointRounding.AwayFromZero);

    private void OnWindowPosChanging(WINDOWPOS* position)
    {
        if (!registry.IsActivationAllowed(surface.Id))
        {
            position->flags |= SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        }
    }

    private bool OnActivate(nint wParam)
    {
        var state = (uint)(wParam & 0xFFFF);
        return state != PInvoke.WA_INACTIVE
            ? registry.Guard.OnActivated(
                surface,
                ActivationMessage.Activate,
                registry.Hints.Current
            )
            : registry.Guard.OnDeactivated(surface, ActivationMessage.Activate);
    }

    private void OnActivateApp(bool active)
    {
        if (!active)
        {
            _ = registry.Guard.OnDeactivated(surface, ActivationMessage.ActivateApp);
            return;
        }

        // Another application handed the activation over to this thread.
        registry.Hints.Note(ActivationCause.External);
        _ = registry.Guard.OnActivated(
            surface,
            ActivationMessage.ActivateApp,
            registry.Hints.Current
        );
    }

    private void OnDpiChanged(HWND window, nint wParam, RECT* suggested)
    {
        registry.Hints.Note(ActivationCause.DpiChange);
        if (surface.PendingMove is { } bounds)
        {
            suggested->left = bounds.Left;
            suggested->top = bounds.Top;
            suggested->right = bounds.Right;
            suggested->bottom = bounds.Bottom;
        }

        // WPF rescales from this message (ACC-008), and its SetWindowPos would activate the surface (#7561).
        var veto = registry.IsActivationAllowed(surface.Id)
            ? HHOOK.Null
            : ActivationVeto.Begin(window);
        _forwardingDpiChange = true;
        try
        {
            _ = PInvoke.SendMessage(
                window,
                PInvoke.WM_DPICHANGED,
                new WPARAM((nuint)wParam),
                new LPARAM((nint)suggested)
            );
        }
        finally
        {
            _forwardingDpiChange = false;
            ActivationVeto.End(veto);
        }

        // WPF only applies it when it rescales (per-monitor scaling on and another DPI): the rectangle is applied anyway.
        _ = PInvoke.SetWindowPos(
            window,
            HWND.Null,
            suggested->left,
            suggested->top,
            suggested->right - suggested->left,
            suggested->bottom - suggested->top,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER
                | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
                | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER
        );
        registry.RequestIntegrityCheck();
    }

    private bool OnGetDpiScaledSize(nint wParam, SIZE* size)
    {
        if (!surface.TryGetLogicalSize(out var width, out var height))
        {
            return false;
        }

        var dpi = (double)(uint)wParam;
        size->cx = Scale(width, dpi);
        size->cy = Scale(height, dpi);
        return true;
    }

    private bool IsInShadowMargin(nint hwnd, nint lParam)
    {
        var margin = surface.ShadowMargin;
        if (margin is { Left: 0, Top: 0, Right: 0, Bottom: 0 })
        {
            return false;
        }

        if (!PInvoke.GetWindowRect((HWND)hwnd, out var bounds))
        {
            return false;
        }

        var x = (short)((long)lParam & 0xFFFF);
        var y = (short)(((long)lParam >> 16) & 0xFFFF);
        if (x < bounds.left || x >= bounds.right || y < bounds.top || y >= bounds.bottom)
        {
            return false;
        }

        var dpi = (double)PInvoke.GetDpiForWindow((HWND)hwnd);
        var inside =
            x >= bounds.left + Scale(margin.Left, dpi)
            && x < bounds.right - Scale(margin.Right, dpi)
            && y >= bounds.top + Scale(margin.Top, dpi)
            && y < bounds.bottom - Scale(margin.Bottom, dpi);
        return !inside;
    }
}
