using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using Clicalo.Domain.Touch;

namespace Clicalo.UI.Wpf.Pointer;

/// <summary>
/// The gesture pipeline of ONE surface on its UI thread (blueprint §3.2, §7.1): it receives the frames of the
/// surface's <see cref="PointerInputSource"/>, feeds its <see cref="GestureRecognizer"/>, runs
/// <see cref="GestureRecognizer.OnTick"/> at <see cref="GestureRecognizer.NextDeadline"/> with a single
/// <see cref="TimeProvider"/> timer marshalled to the dispatcher, and hands every gesture to the surface, in order.
/// </summary>
/// <remarks>
/// Feeding a frame does not allocate: the gesture list is reused and the timer is only moved when the deadline
/// changes. The recognizer and this host belong to the dispatcher's thread; only the timer callback runs elsewhere, and
/// it just queues the tick. Frames must be stamped on the same <see cref="Clock"/> (the one given to the
/// <see cref="PointerInputSource"/>).
/// </remarks>
public sealed class GestureHost : IPointerFrameSink, IDisposable
{
    private readonly List<GestureEvent> _pending = new(capacity: 16);
    private readonly Action<GestureEvent> _onGesture;
    private readonly Action<bool>? _onHover;
    private readonly Action _tick;
    private readonly ITimer _timer;
    private DateTimeOffset? _scheduled;
    private bool _flushing;
    private bool _disposed;

    /// <summary>Creates the pipeline of a surface.</summary>
    /// <param name="recognizer">The recognizer of the surface, with its targets and settings.</param>
    /// <param name="dispatcher">The dispatcher of the surface's UI thread.</param>
    /// <param name="timeProvider">The clock of the frames and of the deadline timer.</param>
    /// <param name="onGesture">Receives every gesture, on the UI thread, in order.</param>
    /// <param name="onHover">Receives the pointer entering and leaving the surface; optional.</param>
    public GestureHost(
        GestureRecognizer recognizer,
        Dispatcher dispatcher,
        TimeProvider timeProvider,
        Action<GestureEvent> onGesture,
        Action<bool>? onHover = null
    )
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(onGesture);
        Recognizer = recognizer;
        Dispatcher = dispatcher;
        Clock = timeProvider;
        _onGesture = onGesture;
        _onHover = onHover;
        _tick = Tick;
        _timer = timeProvider.CreateTimer(
            static state => ((GestureHost)state!).OnTimer(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan
        );
    }

    /// <summary>The recognizer of the surface.</summary>
    public GestureRecognizer Recognizer { get; }

    /// <summary>The dispatcher of the surface's UI thread.</summary>
    public Dispatcher Dispatcher { get; }

    /// <summary>The clock of the frames and of the deadline timer.</summary>
    public TimeProvider Clock { get; }

    /// <inheritdoc />
    public void OnFrame(in PointerFrame frame)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            Recognizer.Feed(frame, _pending);
            Flush();
        }
        finally
        {
            Schedule();
        }
    }

    /// <inheritdoc />
    public void OnHover(bool inside) => _onHover?.Invoke(inside);

    /// <summary>
    /// Forgets every contact now (surface hidden, session locked): active holds end with
    /// <see cref="HoldEndReason.Reset"/> before this returns (REG-03). Must run on the UI thread.
    /// </summary>
    public void Reset()
    {
        Dispatcher.VerifyAccess();
        try
        {
            Recognizer.Reset(Clock.GetUtcNow(), _pending);
            Flush();
        }
        finally
        {
            Schedule();
        }
    }

    /// <summary>
    /// Stops the deadline timer; later frames and ticks are ignored. On the UI thread it first forgets every contact
    /// like <see cref="Reset"/>, so a hold that is still active ends with <see cref="HoldEndReason.Reset"/> and no key
    /// stays down (REG-03); from another thread nothing is delivered, so the surface must reset first.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            Reset();
        }

        _disposed = true;
        _timer.Dispose();
    }

    private void OnTimer()
    {
        if (!_disposed)
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Input, _tick);
        }
    }

    private void Tick()
    {
        if (_disposed)
        {
            return;
        }

        _scheduled = null;
        try
        {
            Recognizer.OnTick(Clock.GetUtcNow(), _pending);
            Flush();
        }
        finally
        {
            Schedule();
        }
    }

    /// <summary>
    /// Hands the pending gestures to the surface. A gesture handler that feeds the recognizer again (a reset from a
    /// gesture) appends to the same list, which this loop then delivers in order.
    /// </summary>
    /// <remarks>
    /// Every gesture is delivered even when a handler throws: the recognizer has already let go of those contacts, so a
    /// <see cref="GestureKind.HoldEnd"/> left undelivered would leave its key down for good (REG-03). The failures are
    /// thrown again once the list is empty: a single one as it was, several together in an
    /// <see cref="AggregateException"/>.
    /// </remarks>
    private void Flush()
    {
        if (_flushing)
        {
            return;
        }

        _flushing = true;
        List<ExceptionDispatchInfo>? failures = null;
        try
        {
            for (var i = 0; i < _pending.Count; i++)
            {
                try
                {
                    _onGesture(_pending[i]);
                }
                catch (Exception ex)
                {
                    (failures ??= []).Add(ExceptionDispatchInfo.Capture(ex));
                }
            }
        }
        finally
        {
            _pending.Clear();
            _flushing = false;
        }

        if (failures is null)
        {
            return;
        }

        if (failures.Count == 1)
        {
            failures[0].Throw();
        }

        throw new AggregateException(failures.Select(failure => failure.SourceException));
    }

    private void Schedule()
    {
        if (_disposed)
        {
            return;
        }

        var next = Recognizer.NextDeadline;
        if (next == _scheduled)
        {
            return;
        }

        _scheduled = next;
        if (next is not { } deadline)
        {
            _ = _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        var due = deadline - Clock.GetUtcNow();
        _ = _timer.Change(due > TimeSpan.Zero ? due : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }
}
