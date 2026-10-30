using Clicalo.Domain.Timing;

namespace Clicalo.Platform.Windows.SysEvents;

/// <summary>
/// Looks again for the app behind a Store app frame (PER-002, blueprint §7.9). When <c>ApplicationFrameHost.exe</c>
/// comes to the front while its app is still starting or resuming, the frame has no window of the app yet, so the
/// foreground is first published with the frame's own process. This watch asks again after each wait of
/// <c>Timings.Foreground.HostedAppRecheck</c> while that frame stays in front, and reports the app's process the first
/// time it is there, so the profile of the real app comes into view. After the last wait it gives up until the next
/// foreground change.
/// </summary>
/// <remarks>
/// Every member runs on the SysEvents thread: the timer only posts <see cref="Check"/> to it through
/// <c>post</c>. Nothing here blocks.
/// </remarks>
internal sealed class HostedAppWatch : IDisposable
{
    private readonly Action<Action> _post;
    private readonly Func<nint, uint> _hostedProcess;
    private readonly Func<nint> _foregroundWindow;
    private readonly Action<nint, uint> _resolved;
    private readonly ITimer _recheck;
    private nint _frame;
    private int _attempt;
    private volatile bool _disposed;

    /// <summary>Creates the watch.</summary>
    /// <param name="time">The timers of the checks.</param>
    /// <param name="post">Runs work on the SysEvents thread.</param>
    /// <param name="hostedProcess">The process of the app hosted by a frame, or zero while there is none.</param>
    /// <param name="foregroundWindow">The window in front now.</param>
    /// <param name="resolved">The frame and the process of its app, once found.</param>
    public HostedAppWatch(
        TimeProvider time,
        Action<Action> post,
        Func<nint, uint> hostedProcess,
        Func<nint> foregroundWindow,
        Action<nint, uint> resolved
    )
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(hostedProcess);
        ArgumentNullException.ThrowIfNull(foregroundWindow);
        ArgumentNullException.ThrowIfNull(resolved);
        _post = post;
        _hostedProcess = hostedProcess;
        _foregroundWindow = foregroundWindow;
        _resolved = resolved;
        _recheck = time.CreateTimer(
            static self => ((HostedAppWatch)self!).OnTimer(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan
        );
    }

    /// <summary>The frame whose app is still unknown, or zero.</summary>
    public nint Frame => _frame;

    /// <summary>Starts asking again for the app of <paramref name="frame"/>, which is in front without one.</summary>
    /// <param name="frame">The frame window.</param>
    public void Watch(nint frame)
    {
        _frame = frame;
        _attempt = 0;
        Schedule();
    }

    /// <summary>Stops asking: another window is in front, or the app is known.</summary>
    public void Cancel()
    {
        _frame = 0;
        _ = _recheck.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _recheck.Dispose();
    }

    private void OnTimer()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _post(Check);
        }
        catch (ObjectDisposedException)
        {
            // The SysEvents loop has ended.
        }
    }

    private void Check()
    {
        var frame = _frame;
        if (_disposed || frame == 0)
        {
            return;
        }

        if (_foregroundWindow() != frame)
        {
            // Another window came to the front: its own foreground event decides from here.
            Cancel();
            return;
        }

        var process = _hostedProcess(frame);
        if (process != 0)
        {
            Cancel();
            _resolved(frame, process);
            return;
        }

        Schedule();
    }

    private void Schedule()
    {
        var waits = Timings.Foreground.HostedAppRecheck;
        if (_attempt >= waits.Length)
        {
            // Bounded: the next foreground change starts over.
            _frame = 0;
            return;
        }

        _ = _recheck.Change(waits[_attempt++], Timeout.InfiniteTimeSpan);
    }
}
