namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// A clock that knows whether somebody is waiting on it: it counts the waits that start with a due time
/// (<c>Task.Delay(delay, clock)</c>, as the atomic writer waits between two attempts on a locked file) and forgets each
/// one when it fires or is cancelled. A test that drives a fake clock next to real file operations moves the clock only
/// while <see cref="Any"/> is true, so the clock never runs ahead of the disk (issue 4). Timers created without a due
/// time and armed later (the save scheduler's own) are not counted.
/// </summary>
/// <param name="inner">The clock that keeps the time, normally a <c>FakeTimeProvider</c>.</param>
internal sealed class ClockWaiters(TimeProvider inner) : TimeProvider
{
    private int _pending;

    /// <summary>Whether a wait with a due time has not fired yet.</summary>
    public bool Any => Volatile.Read(ref _pending) > 0;

    /// <inheritdoc />
    public override long TimestampFrequency => inner.TimestampFrequency;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

    /// <inheritdoc />
    public override long GetTimestamp() => inner.GetTimestamp();

    /// <inheritdoc />
    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (dueTime == Timeout.InfiniteTimeSpan)
        {
            return inner.CreateTimer(callback, state, dueTime, period);
        }

        var wait = new Wait(this);
        _ = Interlocked.Increment(ref _pending);
        wait.Timer = inner.CreateTimer(
            fired =>
            {
                // Forgotten before the waiter continues: what it does next (real file work) holds the clock again.
                wait.End();
                callback(fired);
            },
            state,
            dueTime,
            period
        );
        return wait;
    }

    private sealed class Wait(ClockWaiters owner) : ITimer
    {
        private int _ended;

        public ITimer? Timer { get; set; }

        public void End()
        {
            if (Interlocked.Exchange(ref _ended, 1) == 0)
            {
                _ = Interlocked.Decrement(ref owner._pending);
            }
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) =>
            Timer?.Change(dueTime, period) ?? false;

        public void Dispose()
        {
            End();
            Timer?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            End();
            return Timer?.DisposeAsync() ?? ValueTask.CompletedTask;
        }
    }
}
