using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that says when code under test has armed a timer: the orchestrator works on the
/// thread pool, so a test waits for the delay it wants to move past to exist before it advances the clock.
/// </summary>
internal sealed class WatchedTime(DateTimeOffset start) : FakeTimeProvider(start)
{
    private static readonly TimeSpan ArmTimeout = TimeSpan.FromSeconds(5);

    private int _created;

    /// <summary>Number of timers created so far (every <c>Task.Delay</c> on this clock creates one).</summary>
    public int TimersCreated => Volatile.Read(ref _created);

    /// <inheritdoc />
    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        Interlocked.Increment(ref _created);
        return timer;
    }

    /// <summary>Waits (in real time) until at least <paramref name="count"/> timers exist.</summary>
    public async Task WhenTimersAsync(int count)
    {
        using var deadline = new CancellationTokenSource(ArmTimeout);
        while (TimersCreated < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1), deadline.Token);
        }
    }
}
