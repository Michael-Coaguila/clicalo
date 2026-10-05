using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that says which timers the code under test has armed and not yet fired: the
/// orchestrator works on the thread pool, so a test waits for the delay it wants to move past to exist before it
/// advances the clock.
/// </summary>
/// <remarks>
/// A test must never move the clock before the timer it means to fire exists: a <c>Task.Delay</c> armed after the clock
/// moved is due later still, and nothing fires it. The local stress run of 2026-10-03 (six processes of this assembly
/// at once, run 93) hung exactly so: the dump shows the retry delay of
/// <c>The_restoration_is_retried_once_after_the_retry_delay</c> scheduled at +50 ms, when the test had already moved
/// the clock to +50 ms, because the previous wait polled the timer count, gave up after five seconds of a starved
/// thread pool without saying so, and the test went on. Here the wait is driven by the timers themselves and, when it
/// gives up after <see cref="Liveness"/>, it fails instead of letting the test move the clock.
/// </remarks>
internal sealed class WatchedTime(DateTimeOffset start) : FakeTimeProvider(start)
{
    /// <summary>
    /// Real time a test waits for the code under test to arm a timer or end before it fails. It bounds a liveness
    /// wait of the harness, never a duration of the product (those are fake time): the stress run above measured a
    /// thread-pool continuation of the orchestrator taking more than five seconds while other test processes ran.
    /// </summary>
    public static readonly TimeSpan Liveness = TimeSpan.FromSeconds(30);

    private readonly Lock _gate = new();
    private readonly List<Armed> _armed = [];
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Number of timers created so far (every <c>Task.Delay</c> on this clock creates one).</summary>
    public int TimersCreated
    {
        get
        {
            lock (_gate)
            {
                return _armed.Count;
            }
        }
    }

    /// <summary>
    /// True while a timer due <paramref name="dueTime"/> after it was (re)armed waits to fire: neither fired nor
    /// disposed.
    /// </summary>
    public bool IsWaiting(TimeSpan dueTime)
    {
        lock (_gate)
        {
            return _armed.Exists(armed => armed.IsWaiting && armed.DueTime == dueTime);
        }
    }

    /// <summary>
    /// True while a timer due <paramref name="dueTime"/> after it was (re)armed waits for its first firing since then:
    /// a periodic timer that already fired once counts no more. The orchestrator verifies each attempt with a periodic
    /// look (<c>RestoreVerifyInterval</c>), so a test that moves the clock past one verification must not take that
    /// same look, fired and not yet disposed by its continuation on the thread pool, for the next one.
    /// </summary>
    public bool IsArmedAndNotYetFired(TimeSpan dueTime)
    {
        lock (_gate)
        {
            return _armed.Exists(armed =>
                armed.IsWaiting && !armed.HasFired && armed.DueTime == dueTime
            );
        }
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        ArgumentNullException.ThrowIfNull(callback);
        var armed = new Armed(this);
        lock (_gate)
        {
            armed.Arm(dueTime, period);
            _armed.Add(armed);
        }

        var timer = base.CreateTimer(
            fired =>
            {
                armed.Fired();
                callback(fired);
            },
            state,
            dueTime,
            period
        );
        Changed();
        return new WatchedTimer(timer, armed);
    }

    /// <summary>
    /// Waits (in real time) until <paramref name="condition"/> holds or <paramref name="operation"/> ends, checking again
    /// whenever a timer of this clock is armed, fired or disposed.
    /// </summary>
    /// <returns>True when <paramref name="condition"/> holds; false when <paramref name="operation"/> ended first.</returns>
    /// <exception cref="TimeoutException">Neither happened within <see cref="Liveness"/>.</exception>
    public async Task<bool> WaitUntilAsync(Func<bool> condition, Task operation, string what)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(operation);
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                changed = _changed.Task;
            }

            if (condition())
            {
                return true;
            }

            if (operation.IsCompleted)
            {
                return false;
            }

            var remaining = Liveness - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The code under test neither {what} nor ended within {Liveness.TotalSeconds:0} s of real time ({TimersCreated} timers armed so far); the fake clock was not moved."
                    )
                );
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken
            );
            timeout.CancelAfter(remaining);
            var expired = Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token);
            _ = await Task.WhenAny(changed, operation, expired).ConfigureAwait(false);
            await timeout.CancelAsync().ConfigureAwait(false);
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        }
    }

    private void Changed()
    {
        TaskCompletionSource changed;
        lock (_gate)
        {
            changed = _changed;
            _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        changed.SetResult();
    }

    /// <summary>What the clock knows of one timer: its due time and whether it still waits to fire.</summary>
    private sealed class Armed(WatchedTime owner)
    {
        private TimeSpan _period;
        private bool _disposed;

        public TimeSpan DueTime { get; private set; }

        public bool IsWaiting { get; private set; }

        /// <summary>True once the timer fired after it was last (re)armed.</summary>
        public bool HasFired { get; private set; }

        /// <summary>Called under the owner's gate.</summary>
        public void Arm(TimeSpan dueTime, TimeSpan period)
        {
            HasFired = false;
            DueTime = dueTime;
            _period = period;
            IsWaiting = !_disposed && dueTime != Timeout.InfiniteTimeSpan;
        }

        public void Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                Arm(dueTime, period);
            }

            owner.Changed();
        }

        public void Fired()
        {
            lock (owner._gate)
            {
                // A periodic timer is armed again for its period; a one-shot timer is done.
                IsWaiting =
                    !_disposed && _period != Timeout.InfiniteTimeSpan && _period != TimeSpan.Zero;
                HasFired = true;
                DueTime = _period;
            }

            owner.Changed();
        }

        public void Disposed()
        {
            lock (owner._gate)
            {
                _disposed = true;
                IsWaiting = false;
            }

            owner.Changed();
        }
    }

    /// <summary>The fake timer, reporting changes and disposal to its <see cref="Armed"/> record.</summary>
    private sealed class WatchedTimer(ITimer timer, Armed armed) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            // Recorded first: the fake timer fires on this thread when the new due time is already reached.
            armed.Change(dueTime, period);
            return timer.Change(dueTime, period);
        }

        public void Dispose()
        {
            timer.Dispose();
            armed.Disposed();
        }

        public async ValueTask DisposeAsync()
        {
            await timer.DisposeAsync().ConfigureAwait(false);
            armed.Disposed();
        }
    }
}
