using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.UI.Wpf.Windowing.Internal;

/// <summary>
/// Keeps one surface from being activated while WPF runs code of its own that activates it (dotnet/wpf#7561: the
/// <c>SetWindowPos</c> of <c>WM_DPICHANGED</c> has no <c>SWP_NOACTIVATE</c>, and adding it in
/// <c>WM_WINDOWPOSCHANGING</c> does not stop the activation, as the S1 tests show). A <c>WH_CBT</c> hook of the UI
/// thread refuses <c>HCBT_ACTIVATE</c> for the vetoed windows between <see cref="Begin"/> and <see cref="End"/>, and
/// for no other window.
/// </summary>
/// <remarks>
/// <para>
/// The scope is as short as the WPF call it covers: an activation from another process that arrived inside it would
/// also be refused, and then the guard would not see it. The hook runs on the thread that installed it.
/// </para>
/// <para>
/// Scopes nest (a <c>WM_DPICHANGED</c> can arrive while WPF shows another surface): the thread keeps one hook while any
/// scope is open and refuses the activation of every window with an open scope, so ending an inner scope never lifts
/// the veto of an outer one.
/// </para>
/// </remarks>
internal static unsafe class ActivationVeto
{
    [ThreadStatic]
    private static List<nint>? t_windows;

    [ThreadStatic]
    private static HHOOK t_hook;

    /// <summary>
    /// Starts refusing the activation of <paramref name="window"/> on the calling thread. Returns false, with no veto,
    /// when Windows refuses the hook; only a true result is ended with <see cref="End"/>.
    /// </summary>
    public static bool Begin(HWND window)
    {
        var windows = t_windows ??= [];
        if (windows.Count == 0)
        {
            var hook = PInvoke.SetWindowsHookEx(
                WINDOWS_HOOK_ID.WH_CBT,
                &OnCbt,
                HINSTANCE.Null,
                PInvoke.GetCurrentThreadId()
            );
            if (hook.IsNull)
            {
                return false;
            }

            t_hook = hook;
        }

        windows.Add((nint)window);
        return true;
    }

    /// <summary>Stops refusing it (the scope opened by a successful <see cref="Begin"/> on this thread).</summary>
    public static void End(HWND window)
    {
        var windows = t_windows;
        if (windows is null)
        {
            return;
        }

        var index = windows.LastIndexOf((nint)window);
        if (index < 0)
        {
            return;
        }

        windows.RemoveAt(index);
        if (windows.Count == 0 && !t_hook.IsNull)
        {
            _ = PInvoke.UnhookWindowsHookEx(t_hook);
            t_hook = HHOOK.Null;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT OnCbt(int code, WPARAM wParam, LPARAM lParam) =>
        code == (int)PInvoke.HCBT_ACTIVATE
        && t_windows is { Count: > 0 } windows
        && windows.Contains((nint)wParam.Value)
            ? new LRESULT(1)
            : PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);
}
