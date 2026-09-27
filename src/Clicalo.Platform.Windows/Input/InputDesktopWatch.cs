using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Timing;
using Windows.Win32;
using Windows.Win32.System.StationsAndDesktops;

namespace Clicalo.Platform.Windows.Input;

/// <summary>
/// Sends again the releases the secure desktop refused as soon as the input desktop is Clícalo's again, without
/// waiting for an unlock (blueprint §7.6, INV-3, D-22). UAC and Ctrl+Alt+Del show the secure desktop without locking
/// the session: a Hold or Toggle released meanwhile (deadline, app switch) is refused with <c>ERROR_ACCESS_DENIED</c>,
/// and neither <c>WTS_SESSION_UNLOCK</c> nor a resume ever comes to send it again.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SessionKeyRelease"/> calls <see cref="OnDesktopSwitched"/> for every <c>EVENT_SYSTEM_DESKTOPSWITCH</c>.
/// When the input desktop can be opened (<see cref="InputDesktopIsReachable"/>), the engine gets
/// <see cref="EngineEvent.SessionResumed"/>; while it cannot, it is checked again after each wait of
/// <c>Timings.KeySafety.InputDesktopRecheck</c>, and after the last one the watch waits for the next switch.
/// </para>
/// <para>Called on the SysEvents thread and on the time provider's timer thread; it never blocks.</para>
/// </remarks>
public sealed class InputDesktopWatch : IDisposable
{
    private readonly IEngineInbox _engine;
    private readonly Func<bool> _reachable;
    private readonly ITimer _recheck;
    private readonly Lock _sync = new();
    private int _attempt;
    private bool _waiting;
    private bool _disposed;

    /// <summary>Creates the watch.</summary>
    /// <param name="engine">The engine mailbox.</param>
    /// <param name="reachable">Whether the input desktop is Clícalo's (<see cref="InputDesktopIsReachable"/>).</param>
    /// <param name="time">The timers of the checks.</param>
    public InputDesktopWatch(IEngineInbox engine, Func<bool> reachable, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(reachable);
        ArgumentNullException.ThrowIfNull(time);
        _engine = engine;
        _reachable = reachable;
        _recheck = time.CreateTimer(
            static self => ((InputDesktopWatch)self!).Recheck(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan
        );
    }

    /// <summary>How many times the engine was told that the input desktop is back.</summary>
    public int Resumes { get; private set; }

    /// <summary>Whether a check is waiting on its timer.</summary>
    public bool IsWaiting
    {
        get
        {
            lock (_sync)
            {
                return _waiting;
            }
        }
    }

    /// <summary>
    /// Whether this process can open the input desktop: <see langword="false"/> while the secure desktop (UAC,
    /// Ctrl+Alt+Del, the lock screen) has the input, when <c>SendInput</c> is refused.
    /// </summary>
    public static bool InputDesktopIsReachable()
    {
        var desktop = PInvoke.OpenInputDesktop(
            default(DESKTOP_CONTROL_FLAGS),
            false,
            DESKTOP_ACCESS_FLAGS.DESKTOP_SWITCHDESKTOP
        );
        if (desktop.IsNull)
        {
            return false;
        }

        _ = PInvoke.CloseDesktop(desktop);
        return true;
    }

    /// <summary>The input desktop switched: checks it now and, while it is not Clícalo's, a bounded number of times more.</summary>
    public void OnDesktopSwitched()
    {
        lock (_sync)
        {
            _attempt = 0;
            Check();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _waiting = false;
        }

        _recheck.Dispose();
    }

    private void Recheck()
    {
        lock (_sync)
        {
            if (_waiting)
            {
                Check();
            }
        }
    }

    private void Check()
    {
        _waiting = false;
        if (_disposed)
        {
            return;
        }

        if (_reachable())
        {
            _ = _recheck.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            Resumes++;
            _ = _engine.Post(new EngineEvent.SessionResumed());
            return;
        }

        var waits = Timings.KeySafety.InputDesktopRecheck;
        if (_attempt >= waits.Length)
        {
            // Bounded: the switch back to Clícalo's desktop raises the next event.
            return;
        }

        _waiting = true;
        _ = _recheck.Change(waits[_attempt++], Timeout.InfiniteTimeSpan);
    }
}
