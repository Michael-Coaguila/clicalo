using System.Collections.Immutable;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Tests.Execution.Support;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// The terminal events of blueprint §7.6 (SEG-006, SEG-007, INV-3): whatever is held, queued, repeating or running,
/// every terminal event and «Release all» leaves nothing down and nothing about to be pressed.
/// </summary>
[Trait("Req", "SEG-007")]
public sealed class TerminalEventsTests
{
    public static TheoryData<TerminalReason> Reasons => [.. Enum.GetValues<TerminalReason>()];

    private static EngineHarness Busy()
    {
        var engine = new EngineHarness();
        engine.Foreground();
        engine.Press(Shortcuts.Hold("shift", "ctrl", "shift"), contact: 1);
        engine.Settle();
        engine.Invoke(Shortcuts.Toggle("alt", "alt"));
        engine.Invoke(Shortcuts.Mouse("drag", MouseOp.Drag));
        engine.Press(Shortcuts.Mouse("scroll", MouseOp.ScrollUp), contact: 2);
        engine.Invoke(
            Shortcuts.Macro(
                "macro",
                new KeysStep(Chords.Of("win", "d")),
                new WaitStep(TimeSpan.FromSeconds(2))
            )
        );
        engine.Tap(Shortcuts.Tap("save", "ctrl", "s"), contact: 3);
        engine.Advance(TimeSpan.FromMilliseconds(30));
        return engine;
    }

    [Theory]
    [MemberData(nameof(Reasons))]
    [Trait("Req", "SEG-006")]
    public void Every_terminal_event_releases_everything_and_cancels_everything(
        TerminalReason reason
    )
    {
        var engine = Busy();
        engine.Receiver.IsEmpty.ShouldBeFalse();

        engine.Apply(new EngineEvent.Terminal(reason));
        engine.Advance(TimeSpan.FromSeconds(10));

        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.Receiver.Anomalies.ShouldBeEmpty();
        engine.State.IsQuiet.ShouldBeTrue();
        engine.Timers.ShouldNotContainKey(new TimerKey("outbox"));
    }

    [Fact]
    [Trait("Req", "SEG-003")]
    public void Release_all_masks_the_release_of_alt_and_win()
    {
        var engine = Busy();

        var release = engine
            .Apply(new EngineEvent.ReleaseAll(ReleaseReason.User))
            .OfType<EngineEffect.Inject>()
            .Single();

        var altUp = release.Events.IndexOf(
            InjectedEvent.KeyUp(new InjectedKey(0xA4, 0x38, false, InjectionMode.VirtualKey))
        );
        altUp.ShouldBeGreaterThan(0);
        release.Events[altUp - 1].Kind.ShouldBe(InjectedEventKind.MenuMask);
        release.Epoch.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "SEG-006")]
    public void Pausing_releases_and_then_sends_nothing()
    {
        var engine = Busy();

        engine.Apply(new EngineEvent.SetPaused(On: true));
        var before = engine.Sent.Count();
        engine.Tap(Shortcuts.Tap("copy", "ctrl", "c"), contact: 9);
        engine.Invoke(Shortcuts.Url("web", "https://example.com"));
        engine.Advance(TimeSpan.FromSeconds(1));

        engine.Sent.Count().ShouldBe(before);
        engine.Effects.OfType<EngineEffect.Launch>().ShouldBeEmpty();
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "TAC-008")]
    public void Test_mode_releases_and_then_sends_nothing()
    {
        var engine = Busy();

        engine.Apply(new EngineEvent.SetTestMode(On: true));
        var before = engine.Sent.Count();
        engine.Invoke(Shortcuts.Tap("copy", "ctrl", "c"));
        engine.Invoke(Shortcuts.Text("t", "hola"));
        engine.Advance(TimeSpan.FromSeconds(1));

        engine.Sent.Count().ShouldBe(before);
        engine.Effects.OfType<EngineEffect.TypeText>().ShouldBeEmpty();
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Theory]
    [Trait("Req", "SEG-006")]
    [InlineData(TerminalReason.Lock)]
    [InlineData(TerminalReason.Suspend)]
    public void Locking_or_suspending_says_why_the_keys_were_released(TerminalReason reason) =>
        Busy()
            .Apply(new EngineEvent.Terminal(reason))
            .OfType<EngineEffect.Notice>()
            .ShouldHaveSingleItem()
            .Text.ShouldBe(L.ReleasedOnLock);

    [Fact]
    [Trait("Req", "SEG-006")]
    public void Releases_refused_by_the_secure_desktop_are_sent_again_when_the_session_comes_back()
    {
        var engine = Busy();
        var release = engine
            .Apply(new EngineEvent.Terminal(TerminalReason.Lock))
            .OfType<EngineEffect.Inject>()
            .Single();

        engine.Apply(new EngineEvent.ReleasesBlocked(release.Events));
        engine.State.BlockedReleases.Count.ShouldBe(release.Events.Length);

        var again = engine
            .Apply(new EngineEvent.SessionResumed())
            .OfType<EngineEffect.Inject>()
            .Single();

        again.Events.ShouldBe(release.Events);
        again.IsRelease.ShouldBeTrue();
        engine.State.BlockedReleases.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "SEG-007")]
    public void A_failed_press_batch_releases_what_its_holder_pressed()
    {
        var engine = new EngineHarness(
            EngineHarness.DefaultConfig with
            {
                InterEventDelay = TimeSpan.Zero,
            }
        );
        engine.Foreground();
        var press = engine
            .Press(Shortcuts.Hold("save", "ctrl", "shift"))
            .OfType<EngineEffect.Inject>()
            .Single();

        engine.Apply(new EngineEvent.InjectFailed(press.Effect, Win32Error: 5));

        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.State.IsQuiet.ShouldBeTrue();
        engine.State.PressEffects.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "NFR-005")]
    public void An_engine_fault_leaves_an_empty_state_that_keeps_the_foreground()
    {
        var engine = Busy();

        engine.Apply(new EngineEvent.Terminal(TerminalReason.EngineFault));

        engine.State.Keys.ShouldBe(KeyboardLedger.Empty);
        engine.State.Foreground.ShouldNotBeNull();
        engine.State.PendingExternal.ShouldBe(ImmutableDictionary<EffectId, PendingExternal>.Empty);
    }
}
