using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Measurement;

/// <summary>
/// Cuts the running measurements into repetitions. The evidence of a repetition is everything that happened since the
/// previous one closed (or since the step started), so an activation message that arrives BEFORE the tap is processed
/// (Windows activates on the pointer down) still belongs to it. An automatic repetition closes a settle time after
/// its trigger, to catch what follows it; a new trigger closes the pending one first.
/// </summary>
/// <remarks>Used from the UI thread; timer callbacks are marshalled back with the <c>post</c> delegate.</remarks>
internal sealed class RepetitionRecorder : IDisposable
{
    private readonly TimeProvider _time;
    private readonly Func<MeasurementCounters> _read;
    private readonly Action<Action> _post;
    private readonly Action<RepetitionEvidence> _closed;
    private MeasurementCounters _baseline;
    private RepetitionEvidence? _pending;
    private ITimer? _timer;
    private long _generation;

    /// <summary>Creates the recorder.</summary>
    /// <param name="time">Schedules the settle time.</param>
    /// <param name="read">Reads the running counters.</param>
    /// <param name="post">Runs an action on the UI thread.</param>
    /// <param name="closed">Receives every automatic repetition when it closes, with its counters.</param>
    public RepetitionRecorder(
        TimeProvider time,
        Func<MeasurementCounters> read,
        Action<Action> post,
        Action<RepetitionEvidence> closed
    )
    {
        _time = time;
        _read = read;
        _post = post;
        _closed = closed;
        _baseline = read();
    }

    /// <summary>True while an automatic repetition waits for its settle time.</summary>
    public bool HasPending => _pending is not null;

    /// <summary>
    /// Starts counting from now and drops a pending repetition without closing it (a new step, «Repetir», or the
    /// start of a lease cycle whose evidence must not include what happened between cycles).
    /// </summary>
    public void Rebase()
    {
        CancelTimer();
        _pending = null;
        _baseline = _read();
    }

    /// <summary>
    /// A trigger happened: the repetition described by <paramref name="partial"/> closes after
    /// <paramref name="settle"/>. A repetition still pending closes first.
    /// </summary>
    public void Trigger(RepetitionEvidence partial, TimeSpan settle)
    {
        ArgumentNullException.ThrowIfNull(partial);
        Flush();
        _pending = partial;
        var generation = ++_generation;
        _timer = _time.CreateTimer(
            _ => _post(() => Close(generation)),
            state: null,
            settle,
            Timeout.InfiniteTimeSpan
        );
    }

    /// <summary>Closes the pending repetition now, if any.</summary>
    public void Flush()
    {
        if (_pending is not null)
        {
            Close(_generation);
        }
    }

    /// <summary>
    /// For a manual mark: closes any pending repetition, then returns what the counters did since the previous
    /// repetition and starts counting again.
    /// </summary>
    public MeasurementCounters TakeManual()
    {
        Flush();
        var now = _read();
        var delta = now - _baseline;
        _baseline = now;
        return delta;
    }

    /// <inheritdoc />
    public void Dispose() => CancelTimer();

    private void Close(long generation)
    {
        if (generation != _generation || _pending is not { } pending)
        {
            return;
        }

        CancelTimer();
        _pending = null;
        var now = _read();
        var delta = now - _baseline;
        _baseline = now;
        _closed(pending with { Delta = delta });
    }

    private void CancelTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
