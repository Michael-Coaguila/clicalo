using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clicalo.Application.Ports;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Clicalo.Platform.Windows.SysEvents;

/// <summary>
/// Adapter of <see cref="IForegroundMonitor"/> (blueprint §7.9): <c>SetWinEventHook(EVENT_SYSTEM_FOREGROUND)</c>
/// out of context on the <see cref="SysEventsThread"/>; each event is confirmed with <c>GetForegroundWindow</c> and
/// <c>GetWindowThreadProcessId</c> before it is published, and repeated events for the same window are coalesced.
/// </summary>
/// <remarks>
/// <para>
/// Clícalo's own windows are never published, but they are noticed (the hook does not use
/// <c>WINEVENT_SKIPOWNPROCESS</c>): an app that comes back after a Clícalo window was in front is a change, so a lease
/// ends when the user taps the app it would return to. Only consecutive reports of the same app with nothing in
/// between are coalesced.
/// </para>
/// <para>
/// Shell, touch keyboard and Voice access windows are skipped (<see cref="NonAppWindows"/>). A Store app frame
/// (<c>ApplicationFrameHost.exe</c>) is resolved to the hosted app's process, and the elevation of that process is
/// read without failing (<see cref="ProcessElevation.Unknown"/>, EC-PER-03). No window title is ever read (LOG-001).
/// </para>
/// </remarks>
public sealed class ForegroundMonitor : IForegroundMonitor, IDisposable
{
    private static readonly ConcurrentDictionary<nint, ForegroundMonitor> Hooks = new();

    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private ExternalForeground? _current;
    private HWINEVENTHOOK _hook;
    private bool _ownInFront;
    private int _disposed;

    /// <summary>Creates the monitor on <paramref name="thread"/>, stamping with <paramref name="timeProvider"/>.</summary>
    public ForegroundMonitor(SysEventsThread thread, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Thread = thread;
        Clock = timeProvider;
    }

    /// <inheritdoc />
    public event EventHandler<ExternalForegroundChangedEventArgs>? ExternalForegroundChanged;

    /// <inheritdoc />
    public ExternalForeground? Current => Volatile.Read(ref _current);

    /// <summary>The thread that owns the hook.</summary>
    public SysEventsThread Thread { get; }

    /// <summary>Stamps each confirmed change.</summary>
    public TimeProvider Clock { get; }

    /// <summary>Installs the hook on the SysEvents thread and reads the current foreground once.</summary>
    public Task StartAsync()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return Thread.InvokeAsync(Install);
    }

    /// <summary>Removes the hook.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            Thread.Post(Uninstall);
        }
        catch (ObjectDisposedException)
        {
            // The SysEvents loop has ended; its hooks went with it.
            _ = Hooks.TryRemove(_hook, out _);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnWinEvent(
        HWINEVENTHOOK hook,
        uint eventType,
        HWND window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime
    )
    {
        if (!Hooks.TryGetValue(hook, out var monitor))
        {
            return;
        }

        try
        {
            monitor.Evaluate();
        }
        catch (Exception ex)
        {
            // Never across the unmanaged boundary.
            monitor.Thread.ReportUnhandled(ex);
        }
    }

    private unsafe void Install()
    {
        if (!_hook.IsNull || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _hook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_SYSTEM_FOREGROUND,
            PInvoke.EVENT_SYSTEM_FOREGROUND,
            default(HMODULE),
            &OnWinEvent,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT
        );
        if (_hook.IsNull)
        {
            throw new InvalidOperationException("SetWinEventHook(EVENT_SYSTEM_FOREGROUND) failed.");
        }

        Hooks[_hook] = this;
        Evaluate();
    }

    private void Uninstall()
    {
        if (_hook.IsNull)
        {
            return;
        }

        _ = Hooks.TryRemove(_hook, out _);
        _ = PInvoke.UnhookWinEvent(_hook);
        _hook = default;
    }

    /// <summary>Confirms the foreground with <c>GetForegroundWindow</c> and publishes it when it is a new external app.</summary>
    private unsafe void Evaluate()
    {
        var foreground = PInvoke.GetForegroundWindow();
        if (foreground.IsNull)
        {
            return;
        }

        uint processId = 0;
        var threadId = PInvoke.GetWindowThreadProcessId(foreground, &processId);
        if (processId == _ownProcessId)
        {
            _ownInFront = true;
            return;
        }

        var window = new WindowToken(foreground);
        var image = ProcessInspector.ImageFileName(processId);
        if (NonAppWindows.Contains(ClassName(foreground), image))
        {
            return;
        }

        if (!_ownInFront && _current is { } last && last.Window == window)
        {
            return;
        }

        var appProcessId = ProcessInspector.IsFrameHost(image)
            ? ProcessInspector.HostedAppProcess(window, processId)
            : processId;
        var appOrFrame = appProcessId != 0 ? appProcessId : processId;
        var foregroundApp = new ExternalForeground(window, processId, threadId, Clock.GetUtcNow())
        {
            AppProcessId = appOrFrame,
            Elevation = ProcessInspector.Elevation(appOrFrame),
        };
        _ownInFront = false;
        Volatile.Write(ref _current, foregroundApp);
        ExternalForegroundChanged?.Invoke(
            this,
            new ExternalForegroundChangedEventArgs(foregroundApp)
        );
    }

    private static unsafe string ClassName(HWND window)
    {
        const int MaxClassName = 256;
        var buffer = stackalloc char[MaxClassName];
        var length = PInvoke.GetClassName(window, new PWSTR(buffer), MaxClassName);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }
}
