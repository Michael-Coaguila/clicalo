using System.IO;
using Clicalo.App.Shutdown;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.App.Tests;

/// <summary>
/// The suspend of the computer (<c>PBT_APMSUSPEND</c>; blueprint §6.4, §7.6): the message is answered only after the
/// release and then the flush of the document, the usage and the backups, bounded by
/// <c>Timings.App.SuspendFlushTimeout</c>, since the machine may sleep as soon as it returns.
/// </summary>
[Trait("Req", "DAT-002")]
[Trait("Req", "REG-08")]
[Trait("Req", "SEG-006")]
[Trait("Category", "Quarantine")]
[Trait("Issue", "4")]
public sealed class SuspendFlushTests
{
    [Fact]
    public void The_suspend_is_answered_after_the_release_and_the_flush()
    {
        var steps = new List<string>();

        var flushed = SuspendFlush.Run(
            () => steps.Add("release"),
            async token =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), token);
                lock (steps)
                {
                    steps.Add("flush");
                }
            },
            TimeProvider.System
        );

        flushed.ShouldBeTrue();
        steps.ShouldBe(["release", "flush"]);
    }

    [Fact]
    public async Task A_flush_that_does_not_end_never_keeps_the_suspend_waiting_past_the_limit()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var suspend = Task.Run(
            () =>
                SuspendFlush.Run(
                    static () => { },
                    async token =>
                    {
                        started.SetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    },
                    time
                ),
            TestContext.Current.CancellationToken
        );
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken
        );

        time.Advance(Timings.App.SuspendFlushTimeout);

        (
            await suspend.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)
        ).ShouldBeFalse();
    }

    [Fact]
    public void A_flush_that_fails_never_keeps_the_suspend_from_being_answered() =>
        SuspendFlush
            .Run(
                static () => { },
                static _ => Task.FromException(new IOException("disk")),
                TimeProvider.System
            )
            .ShouldBeFalse();
}
