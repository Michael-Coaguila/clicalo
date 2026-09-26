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
/// thread refuses <c>HCBT_ACTIVATE</c> for that window between <see cref="Begin"/> and <see cref="End"/>, and for no
/// other window.
/// </summary>
/// <remarks>
/// The scope is as short as the WPF call it covers: an activation from another process that arrived inside it would
/// also be refused, and then the guard would not see it. The hook runs on the thread that installed it.
/// </remarks>
internal static unsafe class ActivationVeto
{
    [ThreadStatic]
    private static nint t_window;

    /// <summary>Starts refusing the activation of <paramref name="window"/> on the calling thread.</summary>
    public static HHOOK Begin(HWND window)
    {
        t_window = (nint)window;
        return PInvoke.SetWindowsHookEx(
            WINDOWS_HOOK_ID.WH_CBT,
            &OnCbt,
            HINSTANCE.Null,
            PInvoke.GetCurrentThreadId()
        );
    }

    /// <summary>Stops refusing it.</summary>
    public static void End(HHOOK hook)
    {
        t_window = 0;
        if (!hook.IsNull)
        {
            _ = PInvoke.UnhookWindowsHookEx(hook);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT OnCbt(int code, WPARAM wParam, LPARAM lParam) =>
        code == (int)PInvoke.HCBT_ACTIVATE && (nint)wParam.Value == t_window && t_window != 0
            ? new LRESULT(1)
            : PInvoke.CallNextHookEx(HHOOK.Null, code, wParam, lParam);
}
