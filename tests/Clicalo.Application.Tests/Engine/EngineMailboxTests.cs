using Clicalo.Application.Engine;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

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
    [Trait("Req", "REG-03")]
    [Trait("Req", "EJE-004")]
    public void The_end_of_a_contact_never_overtakes_the_start_of_its_own_hold()
    {
        using var mailbox = new EngineMailbox();
        var start = Start(contact: 7);
        var otherLift = new EngineEvent.ContactEnded(8, default, false);
        var lift = new EngineEvent.ContactEnded(7, default, false);

        // The engine was busy: the start of the hold and its end both wait. The end goes after the start, so the start
        // is released instead of pressing a key that nothing releases; the end of another contact is not delayed.
        mailbox.Post(start);
        mailbox.Post(otherLift);
        mailbox.Post(lift);

        mailbox.TryTake(out var first).ShouldBeTrue();
        first.ShouldBe(otherLift);
        mailbox.TryTake(out var second).ShouldBeTrue();
        second.ShouldBe(start);
        mailbox.TryTake(out var third).ShouldBeTrue();
        third.ShouldBe(lift);

        // Once the start was taken, an end goes first again.
        mailbox.Post(new EngineEvent.TimerFired(new TimerKey("macro")));
        mailbox.Post(lift);
        mailbox.TryTake(out var next).ShouldBeTrue();
        next.ShouldBe(lift);
    }

    private static EngineEvent.Activation Start(int contact) =>
        new(
            new ActivationRequest(
                ActivationPhase.ContactStarted,
                ActivationOrigin.Touch,
                contact,
                null,
                DateTimeOffset.UnixEpoch
            ),
            new Shortcut(
                new ShortcutId("ptt"),
                LocalizedText.Same("ptt", LangCode.Es, LangCode.En),
                new IconRef("keyboard"),
                AutoIcon: false,
                new CategoryId("edit"),
                new HoldAction(KeyChord.Empty),
                new ShortcutOptions(
                    Confirm: false,
                    new HoldLimit.InheritGlobal(),
                    IsPrivate: false
                ),
                Origin: null,
                PinnedFrom: null
            ),
            OriginProfile: null,
            InjectionMode.ScanCode,
            LastExternalPointer: null,
            EditMode: false,
            Epoch: 0,
            RequiredForeground: null
        );

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

        Should.Throw<OperationCanceledException>(() =>
            mailbox.WaitForEvent(Timeout.InfiniteTimeSpan, stop.Token)
        );
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
