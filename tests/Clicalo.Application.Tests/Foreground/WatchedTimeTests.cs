namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// The harness clock of the orchestrator tests: it knows which timers wait to fire, and a test that waits for one
/// returns only once it exists (or the operation ended), so the clock never moves before the delay is armed.
/// </summary>
public sealed class WatchedTimeTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(50);

    private readonly WatchedTime _time = new(Clicalo.TestKit.Time.TestTime.Epoch);

    [Fact]
    public async Task A_delay_waits_until_it_fires()
    {
        var delay = Task.Delay(Delay, _time, TestContext.Current.CancellationToken);
        _time.IsWaiting(Delay).ShouldBeTrue();

        _time.Advance(Delay);

        await delay;
        _time.IsWaiting(Delay).ShouldBeFalse();
    }

    [Fact]
    public async Task A_cancelled_delay_no_longer_waits()
    {
        using var cancel = new CancellationTokenSource();
        var delay = Task.Delay(Delay, _time, cancel.Token);

        await cancel.CancelAsync();

        await Should.ThrowAsync<TaskCanceledException>(delay);
        _time.IsWaiting(Delay).ShouldBeFalse();
    }

    [Fact]
    public void A_periodic_look_counts_as_armed_only_until_its_first_firing()
    {
        var fired = 0;
        using var look = _time.CreateTimer(_ => fired++, null, Delay, Delay);
        _time.IsArmedAndNotYetFired(Delay).ShouldBeTrue();

        _time.Advance(Delay);

        fired.ShouldBe(1);
        _time.IsWaiting(Delay).ShouldBeTrue("a periodic timer waits for its next period");
        _time.IsArmedAndNotYetFired(Delay).ShouldBeFalse("that look is not the next verification");

        _ = look.Change(Delay, Delay);
        _time.IsArmedAndNotYetFired(Delay).ShouldBeTrue("armed again");
    }

    [Fact]
    public async Task Waiting_for_a_delay_returns_once_another_thread_arms_it_however_late()
    {
        using var release = new SemaphoreSlim(0);
        var operation = Task.Run(
            async () =>
            {
                await release.WaitAsync(TestContext.Current.CancellationToken);
                await Task.Delay(Delay, _time, TestContext.Current.CancellationToken);
            },
            TestContext.Current.CancellationToken
        );
        var waiting = _time.WaitUntilAsync(() => _time.IsWaiting(Delay), operation, "armed it");
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        waiting.IsCompleted.ShouldBeFalse("nothing is armed yet");

        release.Release();

        (await waiting).ShouldBeTrue();
        _time.Advance(Delay);
        await operation.WaitAsync(WatchedTime.Liveness, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Waiting_for_a_delay_returns_false_when_the_operation_ends_without_one()
    {
        var operation = Task.CompletedTask;

        (
            await _time.WaitUntilAsync(() => _time.IsWaiting(Delay), operation, "armed it")
        ).ShouldBeFalse();
    }
}
