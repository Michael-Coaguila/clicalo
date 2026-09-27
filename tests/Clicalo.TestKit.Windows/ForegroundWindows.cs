using System.Diagnostics;
using System.Globalization;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.TestKit.Windows;

/// <summary>Foreground queries and diagnostics for desktop tests.</summary>
public static class ForegroundWindows
{
    /// <summary>The current foreground window (<c>GetForegroundWindow</c>), zero when there is none.</summary>
    public static nint Current => PInvoke.GetForegroundWindow();

    /// <summary>The desktop root window: it exists in every session and is never the foreground window.</summary>
    public static nint DesktopWindow => PInvoke.GetDesktopWindow();

    /// <summary>True when <paramref name="window"/> is a live window that owns the foreground right now.</summary>
    public static bool IsForeground(nint window) => window != 0 && Current == window;

    /// <summary>
    /// Asks Windows to put another app's <paramref name="window"/> in front and says whether it is. Only the legitimate
    /// means (<c>SetForegroundWindow</c>): it works when this process may set the foreground, for example right after
    /// a synthetic tap on one of its own windows. Nothing is injected.
    /// </summary>
    public static bool TryBringToFront(nint window)
    {
        if (!Exists(window))
        {
            return false;
        }

        _ = PInvoke.SetForegroundWindow((HWND)window);
        return IsForeground(window);
    }

    /// <summary>True when <paramref name="window"/> is an existing window handle.</summary>
    public static bool Exists(nint window) => window != 0 && PInvoke.IsWindow((HWND)window);

    /// <summary>True when <paramref name="window"/> belongs to this process.</summary>
    public static unsafe bool IsOfThisProcess(nint window)
    {
        uint processId = 0;
        _ = PInvoke.GetWindowThreadProcessId((HWND)window, &processId);
        return processId != 0 && processId == (uint)Environment.ProcessId;
    }

    /// <summary>
    /// One line describing who owns the foreground (handle and process name, never the window title, which may
    /// contain user content), the foreground lock timeout and this process's session, for failure messages.
    /// </summary>
    public static unsafe string Describe()
    {
        var foreground = PInvoke.GetForegroundWindow();
        var owner = "none";
        if (!foreground.IsNull)
        {
            uint processId;
            _ = PInvoke.GetWindowThreadProcessId(foreground, &processId);
            owner = string.Create(
                CultureInfo.InvariantCulture,
                $"{ProcessName(processId)} (pid {processId})"
            );
        }

        uint lockTimeout = 0;
        var lockTimeoutText = PInvoke.SystemParametersInfo(
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETFOREGROUNDLOCKTIMEOUT,
            0,
            &lockTimeout,
            default
        )
            ? string.Create(CultureInfo.InvariantCulture, $"{lockTimeout} ms")
            : "unknown";

        using var self = Process.GetCurrentProcess();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"foreground window 0x{(nint)foreground:X} owned by {owner}; foreground lock timeout {lockTimeoutText}; test process session {self.SessionId}"
        );
    }

    private static string ProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return "an exited process";
        }
    }
}
