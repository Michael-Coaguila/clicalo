using Clicalo.Application.Engine;
using Clicalo.Domain.Execution;

namespace Clicalo.Application.Tests.Engine;

/// <summary>The two-lane mailbox (blueprint §3.2, rule 3): releasing never waits behind a macro.</summary>
[Trait("Req", "SEG-003")]
public sealed class EngineMailboxTests
{
    [Fact]
    public void The_oldest_priority_event_comes_before_any_normal_one()
    {
        using var mailbox = new EngineMailbox();
        var timer = new EngineEvent.TimerFired(new TimerKey("macro"));
        var release = new EngineEvent.ReleaseAll(ReleaseReason.User);
        var lift = new EngineEvent.ContactEnded(1, default, false);

        mailbox.Post(timer);
        mailbox.Post(release);
        mailbox.Post(lift);

        mailbox.Count.ShouldBe(3);
        mailbox.TryTake(out var first).ShouldBeTrue();
        first.ShouldBe(release);
        mailbox.TryTake(out var second).ShouldBeTrue();
        second.ShouldBe(lift);
        mailbox.TryTake(out var third).ShouldBeTrue();
        third.ShouldBe(timer);
        mailbox.TryTake(out _).ShouldBeFalse();
    }

    [Fact]
    public void Waiting_returns_at_once_when_an_event_is_queued_and_after_the_timeout_otherwise()
    {
        using var mailbox = new EngineMailbox();

        mailbox.WaitForEvent(TimeSpan.Zero, CancellationToken.None).ShouldBeFalse();
        mailbox.Post(new EngineEvent.SessionResumed());
        mailbox.WaitForEvent(TimeSpan.Zero, CancellationToken.None).ShouldBeTrue();
    }

    [Fact]
    public void Waiting_stops_when_the_engine_stops()
    {
        using var mailbox = new EngineMailbox();
        using var stop = new CancellationTokenSource();
        stop.Cancel();

        Should.Throw<OperationCanceledException>(() => mailbox.WaitForEvent(Timeout.InfiniteTimeSpan, stop.Token));
    }

    [Fact]
    public void A_completed_mailbox_refuses_events()
    {
        using var mailbox = new EngineMailbox();

        mailbox.Complete();

        mailbox.IsCompleted.ShouldBeTrue();
        mailbox.Post(new EngineEvent.SessionResumed()).ShouldBeFalse();
    }
}
