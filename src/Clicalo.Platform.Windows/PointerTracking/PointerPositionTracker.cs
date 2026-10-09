using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Platform.Windows.SysEvents;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.Platform.Windows.PointerTracking;

/// <summary>
/// The last pointer position outside Clícalo's windows (EJE-009, blueprint §7.11). A touch on the panel takes the cursor
/// there, so the mouse actions use the position before it: <c>SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE)</c> out of
/// context on the <see cref="SysEventsThread"/>, without <c>WINEVENT_SKIPOWNPROCESS</c>, keeps only the cursor
/// (<c>OBJID_CURSOR</c>). The events of one turn of the message loop are coalesced into one sample, which reads
/// <c>GetCursorPos</c> and keeps the point when the window under it (<c>WindowFromPoint</c> → <c>GA_ROOT</c>) is not
/// Clícalo's. It is not a keyboard or mouse hook and never delays anybody's cursor.
/// </summary>
public sealed class PointerPositionTracker : IPointerPositionSource, IDisposable
{
    private const int CursorObject = -9; // OBJID_CURSOR

    private static readonly ConcurrentDictionary<nint, PointerPositionTracker> Hooks = new();

    private readonly SysEventsThread _thread;
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private StrongBox<PhysicalPoint>? _last;
    private HWINEVENTHOOK _hook;
    private int _samplePending;
    private int _disposed;

    /// <summary>Installs the hook on <paramref name="thread"/> and takes a first sample.</summary>
    /// <param name="thread">The SysEvents thread.</param>
    public PointerPositionTracker(SysEventsThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        _thread = thread;
        _thread.Post(Install);
    }

    /// <inheritdoc />
    public PhysicalPoint? LastExternal
    {
        get
        {
            if (Volatile.Read(ref _last) is not { } last)
            {
                return null;
            }

            // A monitor that is gone (unplugged since) makes the point useless: the action falls back to the window.
            var point = new System.Drawing.Point(last.Value.X, last.Value.Y);
            return PInvoke.MonitorFromPoint(point, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL).IsNull
                ? null
                : last.Value;
        }
    }

    /// <summary>Samples the cursor now (after an external foreground change, for example).</summary>
    public void Sample()
    {
        if (Volatile.Read(ref _disposed) == 0 && Interlocked.Exchange(ref _samplePending, 1) == 0)
        {
            try
            {
                _thread.Post(TakeSample);
            }
            catch (ObjectDisposedException)
            {
                // The SysEvents loop has ended with the process.
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _thread.Post(Uninstall);
        }
        catch (ObjectDisposedException)
        {
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
        if (objectId != CursorObject || !Hooks.TryGetValue(hook, out var tracker))
        {
            return;
        }

        try
        {
            tracker.Sample();
        }
        catch (Exception ex)
        {
            // Never across the unmanaged boundary.
            tracker._thread.ReportUnhandled(ex);
        }
    }

    private unsafe void Install()
    {
        if (!_hook.IsNull || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _hook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
            PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
            default(HMODULE),
            &OnWinEvent,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT
        );
        if (!_hook.IsNull)
        {
            Hooks[_hook] = this;
        }

        TakeSample();
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

    private unsafe void TakeSample()
    {
        Volatile.Write(ref _samplePending, 0);
        if (!PInvoke.GetCursorPos(out var point))
        {
            return;
        }

        var under = PInvoke.WindowFromPoint(point);
        if (!under.IsNull)
        {
            var root = PInvoke.GetAncestor(under, GET_ANCESTOR_FLAGS.GA_ROOT);
            uint processId = 0;
            _ = PInvoke.GetWindowThreadProcessId(root.IsNull ? under : root, &processId);
            if (processId == _ownProcessId)
            {
                return;
            }
        }

        Volatile.Write(
            ref _last,
            new StrongBox<PhysicalPoint>(new PhysicalPoint(point.X, point.Y))
        );
    }
}
