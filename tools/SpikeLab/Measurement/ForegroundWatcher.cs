using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Clicalo.Tools.SpikeLab.Measurement;

/// <summary>
/// The laboratory's own view of the foreground: <c>SetWinEventHook(EVENT_SYSTEM_FOREGROUND)</c> out of context
/// WITHOUT <c>WINEVENT_SKIPOWNPROCESS</c>, so it also sees a lab surface taking the foreground (which the product's
/// <c>ForegroundMonitor</c> deliberately skips). Each event is confirmed with <c>GetForegroundWindow</c>. Installed on
/// the UI thread, whose message loop delivers the callbacks. One instance per process.
/// </summary>
internal sealed class ForegroundWatcher : IDisposable
{
    private static ForegroundWatcher? _instance;

    private readonly TimeProvider _time;
    private readonly Func<nint, string?> _surfaceOf;
    private readonly Func<nint> _probeWindow;
    private readonly Action<ForegroundChange> _changed;
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private HWINEVENTHOOK _hook;
    private nint _last;

    /// <summary>Creates the watcher; <see cref="Start"/> installs the hook.</summary>
    /// <param name="time">Stamps the changes.</param>
    /// <param name="surfaceOf">The name of the lab surface that owns a window, or null.</param>
    /// <param name="probeWindow">The InputProbe window, or zero.</param>
    /// <param name="changed">Receives every confirmed change, on the UI thread.</param>
    public ForegroundWatcher(
        TimeProvider time,
        Func<nint, string?> surfaceOf,
        Func<nint> probeWindow,
        Action<ForegroundChange> changed
    )
    {
        _time = time;
        _surfaceOf = surfaceOf;
        _probeWindow = probeWindow;
        _changed = changed;
    }

    /// <summary>The window in front now, described.</summary>
    public ForegroundChange Current => Describe(PInvoke.GetForegroundWindow());

    /// <summary>Installs the hook on the calling (UI) thread.</summary>
    public unsafe void Start()
    {
        if (Interlocked.CompareExchange(ref _instance, this, null) is not null)
        {
            throw new InvalidOperationException("Only one ForegroundWatcher can run per process.");
        }

        _last = PInvoke.GetForegroundWindow();
        _hook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_SYSTEM_FOREGROUND,
            PInvoke.EVENT_SYSTEM_FOREGROUND,
            HMODULE.Null,
            &OnWinEvent,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT
        );
        if (_hook.IsNull)
        {
            Interlocked.CompareExchange(ref _instance, null, this);
            throw new InvalidOperationException(
                "SetWinEventHook(EVENT_SYSTEM_FOREGROUND) failed with Win32 error "
                    + Marshal
                        .GetLastPInvokeError()
                        .ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "."
            );
        }
    }

    /// <summary>Describes <paramref name="window"/> as a foreground change happening now.</summary>
    public ForegroundChange Describe(nint window)
    {
        var (processId, name) = ProcessNames.Of(window);
        var probe = _probeWindow();
        return new ForegroundChange(window, processId, name, _time.GetUtcNow())
        {
            Surface = window == 0 ? null : _surfaceOf(window),
            IsOwnProcess = processId == _ownProcessId,
            IsProbe = window != 0 && window == probe,
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_hook.IsNull)
        {
            _ = PInvoke.UnhookWinEvent(_hook);
            _hook = default;
        }

        Interlocked.CompareExchange(ref _instance, null, this);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnWinEvent(
        HWINEVENTHOOK hook,
        uint winEvent,
        HWND window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime
    )
    {
        try
        {
            Volatile.Read(ref _instance)?.OnForeground();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // An exception must never cross the native callback boundary; the next event re-reads the foreground.
        }
    }

    private void OnForeground()
    {
        var foreground = (nint)PInvoke.GetForegroundWindow();
        if (foreground == _last)
        {
            return;
        }

        _last = foreground;
        _changed(Describe(foreground));
    }
}
