using System.Diagnostics;
using System.Globalization;

namespace Clicalo.Application.Tests.Engine;

/// <summary>
/// Waits, in real time, for something the engine thread does: polls a condition and, once <see cref="Liveness"/> has
/// passed, looks at it one last time before failing with what it saw.
/// </summary>
/// <remarks>
/// The previous wait put its deadline in the cancellation token of each <c>Task.Delay</c>. When the thread pool is
/// starved, the timer queue can run the overdue poll and the overdue deadline in the same pass, in list order: the
/// deadline then cancels the poll's delay and throws <see cref="TaskCanceledException"/> without the condition being
/// checked again, whatever the engine had done by then. That is the likely cause, not a proven one, of the
/// <see cref="TaskCanceledException"/> of <c>Release_all_posted_from_another_thread_reaches_the_running_engine</c> in
/// 2 of 93 runs of the local stress run of 2026-10-03 (six processes of this assembly at once): the condition was not
/// recorded when it failed. Here the deadline lives on <see cref="Stopwatch"/> and a failure says what it saw.
/// </remarks>
internal static class Eventually
{
    /// <summary>
    /// How long a test waits for the engine thread: a liveness bound of the harness, not a duration of the product
    /// (the engine runs on a fake clock).
    /// </summary>
    public static readonly TimeSpan Liveness = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(5);

    /// <summary>Returns once <paramref name="condition"/> holds; fails after <see cref="Liveness"/> otherwise.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var started = Stopwatch.GetTimestamp();
        while (!condition())
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            if (elapsed >= Liveness)
            {
                condition()
                    .ShouldBeTrue(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{what} did not happen within {elapsed.TotalSeconds:0.0} s of real time (thread pool: {ThreadPool.ThreadCount} threads, {ThreadPool.PendingWorkItemCount} work items pending)"
                        )
                    );
                return;
            }

            await Task.Delay(Poll, TestContext.Current.CancellationToken);
        }
    }
}
