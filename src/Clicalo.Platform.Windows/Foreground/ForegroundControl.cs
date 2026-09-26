using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.Platform.Windows.Foreground;

/// <summary>
/// The single adapter of <see cref="IForegroundControl"/> and the ONLY file allowed to call
/// <c>SetForegroundWindow</c> and <c>AllowSetForegroundWindow</c> (banned-api-exceptions.json: foreground-control;
/// blueprint §3.6, §4.4). No <c>AttachThreadInput</c>, no synthetic Alt and no <c>LockSetForegroundWindow</c>.
/// </summary>
/// <remarks>
/// Every call is one Win32 request: whether Windows grants it depends only on the foreground rights Clícalo holds at
/// that moment (it received the last input, a <c>WM_HOTKEY</c>, or one of its windows is in front). The ladder,
/// retries and delays live in <c>ForegroundOrchestrator</c>.
/// </remarks>
public sealed class ForegroundControl : IForegroundControl
{
    /// <inheritdoc />
    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "The adapter of IForegroundControl: every foreground change goes through ForegroundOrchestrator leases (ADR-0005)."
    )]
    public bool TrySetForeground(WindowToken window)
    {
        var target = (HWND)window.Handle;
        if (window.IsNone || !PInvoke.IsWindow(target))
        {
            return false;
        }

        _ = PInvoke.SetForegroundWindow(target);

        // SetForegroundWindow can report success while Windows only flashed the taskbar button: verify.
        return PInvoke.GetForegroundWindow() == target;
    }

    /// <inheritdoc />
    public WindowToken GetForeground() => new(PInvoke.GetForegroundWindow());

    /// <inheritdoc />
    public unsafe void FlashTaskbar(WindowToken window)
    {
        if (window.IsNone)
        {
            return;
        }

        var flash = new FLASHWINFO
        {
            cbSize = (uint)sizeof(FLASHWINFO),
            hwnd = (HWND)window.Handle,
            dwFlags = FLASHWINFO_FLAGS.FLASHW_TRAY | FLASHWINFO_FLAGS.FLASHW_TIMERNOFG,
            uCount = 0,
            dwTimeout = 0,
        };
        _ = PInvoke.FlashWindowEx(in flash);
    }
}
