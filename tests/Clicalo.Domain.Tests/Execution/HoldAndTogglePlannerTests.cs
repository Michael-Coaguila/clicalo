using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Tests.Execution.Support;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>Hold (EJE-004 to EJE-006), Toggle (EJE-007) and the automatic release (SEG-004).</summary>
[Trait("Req", "EJE-004")]
public sealed class HoldAndTogglePlannerTests
{
    private static readonly InjectedKey Shift = new(0xA0, 0x2A, false, InjectionMode.VirtualKey);
    private static readonly InjectedKey Ctrl = new(0xA2, 0x1D, false, InjectionMode.VirtualKey);

    private static EngineHarness Engine(TimeSpan? maxHold = null)
    {
        var engine = new EngineHarness(
            EngineHarness.DefaultConfig with
            {
                InterEventDelay = TimeSpan.Zero,
                MaxHold = maxHold ?? TimeSpan.FromSeconds(60),
            }
        );
        engine.Foreground();
        return engine;
    }

    [Fact]
    public void A_hold_presses_when_the_finger_rests_and_releases_when_it_lifts()
    {
        var engine = Engine();
        var hold = Shortcuts.Hold("shift", "shift");

        engine.Press(hold);
        engine.Receiver.Keys.ShouldBe([Shift]);
        engine.State.Keys.Items[HolderId.ForContact(1)].Origin.ShouldBe(HoldOrigin.Contact);

        engine.Lift();
        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.State.IsQuiet.ShouldBeTrue();
    }

    [Fact]
    public void A_tap_on_a_hold_tile_does_nothing_on_its_own()
    {
        var engine = Engine();

        engine.Tap(Shortcuts.Hold("shift", "shift"));

        engine.Sent.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "EJE-006")]
    public void Each_contact_releases_only_its_own_hold()
    {
        var engine = Engine();
        engine.Press(Shortcuts.Hold("shift", "shift"), contact: 1);
        engine.Press(Shortcuts.Hold("ctrl", "ctrl"), contact: 2);

        engine.Lift(1);

        engine.Receiver.Keys.ShouldBe([Ctrl]);
        engine.Lift(2);
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "EJE-005")]
    public void A_hold_invoked_without_contact_behaves_as_a_toggle()
    {
        var engine = Engine();
        var hold = Shortcuts.Hold("shift", "shift");

        engine.Invoke(hold);
        engine.Receiver.Keys.ShouldBe([Shift]);
        engine.State.Keys.Items[HolderId.ForToggle(hold.Id)].Origin.ShouldBe(HoldOrigin.Invoke);

        engine.Invoke(hold);
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "EJE-007")]
    public void A_toggle_latches_on_one_tap_and_releases_on_the_next()
    {
        var engine = Engine();
        var toggle = Shortcuts.Toggle("ctrl", "ctrl");

        engine.Tap(toggle);
        engine.Receiver.Keys.ShouldBe([Ctrl]);
        engine.Advance(TimeSpan.FromSeconds(1));

        engine.Tap(toggle);
        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.Effects.OfType<EngineEffect.Notice>().Count().ShouldBe(2);
    }

    [Fact]
    [Trait("Req", "SEG-004")]
    public void A_held_item_is_released_at_its_deadline_and_says_so()
    {
        var engine = Engine(TimeSpan.FromSeconds(30));
        engine.Press(Shortcuts.Hold("shift", "shift"));

        engine.Advance(TimeSpan.FromSeconds(29));
        engine.Receiver.Keys.ShouldBe([Shift]);
        engine.Advance(TimeSpan.FromSeconds(1));

        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.LastEffects.OfType<EngineEffect.Notice>().ShouldHaveSingleItem();
    }

    [Fact]
    [Trait("Req", "SEG-004")]
    public void An_item_with_its_own_limit_keeps_it_when_the_global_limit_changes()
    {
        var engine = Engine(TimeSpan.FromSeconds(60));
        engine.Invoke(
            Shortcuts.Of(
                "own",
                new ToggleAction(Chords.Of("ctrl")),
                Shortcuts.Limited(TimeSpan.FromSeconds(5))
            )
        );
        engine.Press(Shortcuts.Hold("shift", "shift"));

        engine.Apply(
            new EngineEvent.ConfigChanged(engine.Config with { MaxHold = TimeSpan.FromSeconds(2) })
        );
        engine.Advance(TimeSpan.FromSeconds(2));
        engine.Receiver.Keys.ShouldBe([Ctrl]);

        engine.Advance(TimeSpan.FromSeconds(3));
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "SEG-004")]
    public void An_item_that_never_expires_is_still_released_by_release_all()
    {
        var engine = Engine();
        engine.Invoke(
            Shortcuts.Of("never", new ToggleAction(Chords.Of("ctrl")), Shortcuts.Unlimited)
        );

        engine.Advance(TimeSpan.FromHours(1));
        engine.Receiver.Keys.ShouldBe([Ctrl]);

        engine.Apply(new EngineEvent.ReleaseAll(ReleaseReason.User));
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "SEG-003")]
    public void Release_all_releases_every_holder_and_announces_it()
    {
        var engine = Engine();
        engine.Press(Shortcuts.Hold("shift", "shift"), contact: 1);
        engine.Tap(Shortcuts.Toggle("ctrl", "ctrl"), contact: 2);

        var effects = engine.Apply(new EngineEvent.ReleaseAll(ReleaseReason.User));

        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.State.IsQuiet.ShouldBeTrue();
        effects.OfType<EngineEffect.Inject>().ShouldHaveSingleItem().IsRelease.ShouldBeTrue();
        effects
            .OfType<EngineEffect.Notice>()
            .ShouldHaveSingleItem()
            .Urgency.ShouldBe(NoticeUrgency.Assertive);
    }

    [Fact]
    [Trait("Req", "EJE-013")]
    public void A_target_that_becomes_elevated_releases_what_was_held()
    {
        var engine = Engine();
        engine.Press(Shortcuts.Hold("shift", "shift"));

        engine.Foreground("regedit", ElevationState.TargetElevated, userSwitch: false);

        engine.Receiver.IsEmpty.ShouldBeTrue();
    }
}
