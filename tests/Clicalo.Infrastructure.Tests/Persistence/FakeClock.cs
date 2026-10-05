using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// Drives code that waits on a <see cref="FakeTimeProvider"/> while real I/O happens in between: the clock moves in small
/// steps and the test thread yields to the continuations after each one.
/// </summary>
internal static class FakeClock
{
    /// <summary>The step of <see cref="RunAsync{T}"/>.</summary>
    public static TimeSpan Step { get; } = TimeSpan.FromMilliseconds(10);

    /// <summary>Advances <paramref name="time"/> until <paramref name="task"/> completes, calling <paramref name="onStep"/> after each step.</summary>
    /// <param name="time">The fake clock.</param>
    /// <param name="task">The work.</param>
    /// <param name="onStep">Called with the fake time elapsed so far.</param>
    /// <param name="limit">Fake time after which the test fails.</param>
    public static async Task<T> RunAsync<T>(
        FakeTimeProvider time,
        Task<T> task,
        Action<TimeSpan>? onStep = null,
        TimeSpan? limit = null
    )
    {
        var elapsed = TimeSpan.Zero;
        var max = limit ?? TimeSpan.FromMinutes(10);
        while (!task.IsCompleted)
        {
            await Task.Delay(1, TestContext.Current.CancellationToken);
            if (task.IsCompleted)
            {
                break;
            }

            time.Advance(Step);
            elapsed += Step;
            onStep?.Invoke(elapsed);
            (elapsed <= max).ShouldBeTrue(
                "The work did not finish within " + max + " of fake time."
            );
        }

        return await task;
    }

    /// <summary>Advances <paramref name="time"/> by <paramref name="total"/> in steps, yielding in between.</summary>
    /// <param name="time">The fake clock.</param>
    /// <param name="total">How much.</param>
    /// <param name="step">The step, 10 ms by default.</param>
    public static async Task AdvanceAsync(
        FakeTimeProvider time,
        TimeSpan total,
        TimeSpan? step = null
    )
    {
        var size = step ?? Step;
        for (var moved = TimeSpan.Zero; moved < total; moved += size)
        {
            time.Advance(size);
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Waits in real time (bounded) until <paramref name="condition"/> holds.</summary>
    /// <param name="condition">What to wait for.</param>
    /// <param name="because">The failure message.</param>
    public static async Task UntilAsync(Func<bool> condition, string because)
    {
        for (var i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        condition().ShouldBeTrue(because);
    }
}
