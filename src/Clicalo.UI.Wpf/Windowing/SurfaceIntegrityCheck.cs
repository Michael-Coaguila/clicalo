using System.Windows.Threading;
using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Clicalo.UI.Wpf.Windowing.Internal;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// Repairs what can drift on a surface (blueprint §3.5): every <c>Timings.Foreground.SurfaceIntegrityInterval</c>
/// (30 s) and after each <c>WM_DPICHANGED</c>, <c>WM_DISPLAYCHANGE</c> or theme change, it checks
/// <c>GWL_EXSTYLE</c> (<c>WS_EX_NOACTIVATE</c>, <c>WS_EX_TOPMOST</c>) and the topmost band of each registered
/// surface and fixes them with <c>SWP_NOACTIVATE</c>. Surfaces under an activation lease keep their lease style.
/// Runs on the UI thread.
/// </summary>
/// <remarks>
/// The timer fires on the thread pool and only queues the check on the registry's dispatcher; the checks asked for by
/// a message run after that message has been handled (for <c>WM_DPICHANGED</c>, after WPF applied its rectangle).
/// Queued checks are coalesced: a <c>WM_DISPLAYCHANGE</c> reaches every surface, and one check covers them all.
/// </remarks>
public sealed class SurfaceIntegrityCheck : IDisposable
{
    private long _repairs;
    private int _queued;
    private ITimer? _timer;

    /// <summary>Creates the check over <paramref name="registry"/>, scheduled with <paramref name="timeProvider"/>.</summary>
    public SurfaceIntegrityCheck(SurfaceRegistry registry, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Registry = registry;
        TimeProvider = timeProvider;
    }

    /// <summary>Repairs made since the process started (each one is also raised as <see cref="Repaired"/>).</summary>
    public long Repairs => Interlocked.Read(ref _repairs);

    /// <summary>Raised on the UI thread for each property put back, with the surface and the property.</summary>
    public event EventHandler<SurfaceRepairedEventArgs>? Repaired;

    private SurfaceRegistry Registry { get; }

    private TimeProvider TimeProvider { get; }

    /// <summary>Starts the periodic check and the checks asked for by the surfaces. Call it on the UI thread.</summary>
    public void Start()
    {
        Registry.Dispatcher.VerifyAccess();
        if (_timer is not null)
        {
            return;
        }

        Registry.IntegrityCheckRequested += OnCheckRequested;
        _timer = TimeProvider.CreateTimer(
            static state => ((SurfaceIntegrityCheck)state!).Queue(),
            this,
            Timings.Foreground.SurfaceIntegrityInterval,
            Timings.Foreground.SurfaceIntegrityInterval
        );
    }

    /// <summary>Checks every surface now and returns how many properties it repaired.</summary>
    public int CheckNow()
    {
        Registry.Dispatcher.VerifyAccess();
        var repaired = 0;
        foreach (var surface in Registry.Surfaces)
        {
            repaired += Check(surface);
        }

        return repaired;
    }

    /// <summary>Stops the periodic check.</summary>
    public void Dispose()
    {
        var timer = Interlocked.Exchange(ref _timer, null);
        if (timer is null)
        {
            return;
        }

        timer.Dispose();
        Registry.IntegrityCheckRequested -= OnCheckRequested;
    }

    private int Check(NonActivatingWindow surface)
    {
        var token = surface.SurfaceWindow;
        if (token.IsNone)
        {
            return 0;
        }

        var window = (HWND)token.Handle;
        var style = SurfaceStyles.Get(window);
        var repaired = 0;
        if (
            (style & WINDOW_EX_STYLE.WS_EX_NOACTIVATE) != WINDOW_EX_STYLE.WS_EX_NOACTIVATE
            && !Registry.IsActivationAllowed(surface.Id)
            && SurfaceStyles.SetNoActivate(window, noActivate: true)
        )
        {
            repaired += Report(surface.Id, SurfaceRepairKind.NoActivateStyle);
        }

        if ((style & WINDOW_EX_STYLE.WS_EX_TOPMOST) != WINDOW_EX_STYLE.WS_EX_TOPMOST)
        {
            var previous = Registry.Hints.Enter(ActivationCause.Topmost);
            try
            {
                if (SurfaceStyles.PlaceOnTop(window, show: false))
                {
                    repaired += Report(surface.Id, SurfaceRepairKind.Topmost);
                }
            }
            finally
            {
                Registry.Hints.Exit(previous);
            }
        }

        return repaired;
    }

    private int Report(SurfaceId surface, SurfaceRepairKind kind)
    {
        Interlocked.Increment(ref _repairs);
        Repaired?.Invoke(this, new SurfaceRepairedEventArgs(surface, kind));
        return 1;
    }

    private void OnCheckRequested(object? sender, EventArgs e) => Queue();

    /// <summary>Queues one check on the UI thread unless one is already queued.</summary>
    private void Queue()
    {
        if (Interlocked.Exchange(ref _queued, 1) == 0)
        {
            _ = Registry.Dispatcher.InvokeAsync(RunQueued, DispatcherPriority.Background);
        }
    }

    private void RunQueued()
    {
        Volatile.Write(ref _queued, 0);
        if (Volatile.Read(ref _timer) is not null)
        {
            _ = CheckNow();
        }
    }
}
