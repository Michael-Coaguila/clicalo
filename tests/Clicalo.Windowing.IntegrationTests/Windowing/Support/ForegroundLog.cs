using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Clicalo.TestKit.Windows.Rendering;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// Every foreground change (<c>EVENT_SYSTEM_FOREGROUND</c>, out of context, on the WPF test thread) with the window, its
/// process name (never its title) and a <see cref="Stopwatch"/> timestamp, for the failure messages of the desktop
/// tests. Disposing it removes the hook.
/// </summary>
public sealed class ForegroundLog : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint OutOfContext = 0x0000;

    private readonly ConcurrentQueue<(long Timestamp, string Line)> _entries = new();
    private readonly WinEventProc _callback;
    private nint _hook;

    private ForegroundLog()
    {
        _callback = OnEvent;
    }

    private delegate void WinEventProc(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint thread,
        uint time
    );

    /// <summary>Starts recording on the WPF test thread.</summary>
    public static ForegroundLog Start()
    {
        var log = new ForegroundLog();
        WpfThread.Invoke(() =>
            log._hook = SetWinEventHook(
                EventSystemForeground,
                EventSystemForeground,
                0,
                log._callback,
                0,
                0,
                OutOfContext
            )
        );
        return log;
    }

    /// <summary>Every change, each with its milliseconds after <paramref name="origin"/> (negative before it).</summary>
    public IReadOnlyList<string> Relative(long origin) =>
        [
            .. _entries.Select(entry =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"+{Stopwatch.GetElapsedTime(origin, entry.Timestamp).TotalMilliseconds:0.0} ms foreground {entry.Line}"
                )
            ),
        ];

    public void Dispose()
    {
        var hook = Interlocked.Exchange(ref _hook, 0);
        if (hook != 0)
        {
            WpfThread.Invoke(() => UnhookWinEvent(hook));
        }
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

    private void OnEvent(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint thread,
        uint time
    )
    {
        _ = GetWindowThreadProcessId(window, out var processId);
        _entries.Enqueue(
            (
                Stopwatch.GetTimestamp(),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"0x{window:X} of {ProcessName(processId)} (pid {processId})"
                )
            )
        );
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint module,
        WinEventProc callback,
        uint processId,
        uint threadId,
        uint flags
    );

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint hook);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
