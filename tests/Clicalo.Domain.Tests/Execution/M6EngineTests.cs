using Clicalo.Domain.Execution;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Tests.Execution.Support;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// What M6 closed in the engine: the held scroll is in the ledger (SEG-001), the notice of a release by lock comes
/// back with the session (SEG-006), a key the layout lacks is named (EC-EJE-10) and the confirmation window follows
/// the time multiplier (ACC-006).
/// </summary>
public sealed class M6EngineTests
{
    private static readonly Shortcut ScrollDown = Shortcuts.Mouse(
        "down",
        MouseOp.ScrollDown,
        ScrollSpeed.Normal
    );

    private static EngineHarness Engine(EngineConfig? config = null)
    {
        var engine = new EngineHarness(
            (config ?? EngineHarness.DefaultConfig) with
            {
                InterEventDelay = TimeSpan.Zero,
            }
        );
        engine.Foreground();
        return engine;
    }

    private static IEnumerable<Message> Notices(IEnumerable<EngineEffect> effects) =>
        effects.OfType<EngineEffect.Notice>().Select(static n => n.Text);

    [Fact]
    [Trait("Req", "SEG-001")]
    public void A_held_scroll_is_in_the_ledger_with_its_shortcut_its_contact_and_its_start()
    {
        var engine = Engine();

        engine.Press(ScrollDown, contact: 7);

        var item = engine.State.Keys.Items.Values.ShouldHaveSingleItem();
        item.Shortcut.ShouldBe(ScrollDown.Id);
        item.ContactId.ShouldBe(7);
        item.Origin.ShouldBe(HoldOrigin.Dock);
        item.SinceTicks.ShouldBe(engine.Now);
        item.Keys.IsEmpty.ShouldBeTrue();
        item.Buttons.ShouldBe(MouseButtons.None);
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "SEG-001")]
    public void Lifting_the_finger_takes_the_scroll_out_of_the_ledger_without_a_notice()
    {
        var engine = Engine();
        engine.Press(ScrollDown);

        var effects = engine.Lift();

        engine.State.Keys.IsEmpty.ShouldBeTrue();
        engine.State.Scroll.ShouldBeNull();
        engine.State.IsQuiet.ShouldBeTrue();
        Notices(effects).ShouldBeEmpty();
        effects.OfType<EngineEffect.Inject>().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "SEG-001")]
    [Trait("Req", "SEG-003")]
    public void Release_all_stops_a_held_scroll()
    {
        var engine = Engine();
        engine.Press(ScrollDown);

        engine.Apply(new EngineEvent.ReleaseAll(ReleaseReason.User));
        var steps = engine.Effects.OfType<EngineEffect.MouseAction>().Count();
        engine.Advance(TimeSpan.FromSeconds(2));

        engine.State.IsQuiet.ShouldBeTrue();
        engine.Effects.OfType<EngineEffect.MouseAction>().Count().ShouldBe(steps);
    }

    [Fact]
    [Trait("Req", "SEG-001")]
    [Trait("Req", "SEG-004")]
    public void The_global_limit_ends_a_held_scroll_and_says_so()
    {
        var engine = Engine(EngineHarness.DefaultConfig with { MaxHold = TimeSpan.FromSeconds(2) });
        engine.Press(ScrollDown);

        engine.Advance(TimeSpan.FromSeconds(3));

        engine.State.IsQuiet.ShouldBeTrue();
        Notices(engine.Effects).ShouldContain(L.ReleasedAuto(2));
        var steps = engine.Effects.OfType<EngineEffect.MouseAction>().Count();
        engine.Advance(TimeSpan.FromSeconds(2));
        engine.Effects.OfType<EngineEffect.MouseAction>().Count().ShouldBe(steps);
    }

    [Fact]
    [Trait("Req", "SEG-001")]
    [Trait("Req", "SEG-005")]
    public void A_real_app_switch_stops_a_held_scroll_and_leaves_the_ledger_empty()
    {
        var engine = Engine();
        engine.Press(ScrollDown);

        engine.Foreground("winword");

        engine.State.IsQuiet.ShouldBeTrue();
    }

    [Fact]
    // INV-9 of the blueprint: one holder per held input; it is an invariant, not a requirement of the catalog.
    [Trait("Req", "SEG-001")]
    public void A_second_finger_never_takes_a_held_scroll_from_the_first()
    {
        var engine = Engine();
        engine.Press(ScrollDown, contact: 1);

        var effects = engine.Press(Shortcuts.Mouse("up", MouseOp.ScrollUp), contact: 2);

        effects.OfType<EngineEffect.MouseAction>().ShouldBeEmpty();
        engine.State.Keys.Items.Values.ShouldHaveSingleItem().ContactId.ShouldBe(1);
        engine.State.Scroll.ShouldNotBeNull().Op.ShouldBe(MouseOp.ScrollDown);
        engine.Lift(contact: 2);
        engine.State.Scroll.ShouldNotBeNull();
        engine.Lift(contact: 1);
        engine.State.IsQuiet.ShouldBeTrue();
    }

    [Theory]
    [InlineData(TerminalReason.Lock)]
    [InlineData(TerminalReason.Suspend)]
    [Trait("Req", "SEG-006")]
    public void The_notice_of_a_release_by_lock_is_raised_again_when_the_session_is_back(
        TerminalReason reason
    )
    {
        var engine = Engine();
        engine.Press(Shortcuts.Hold("shift", "shift"));
        engine.Settle();

        Notices(engine.Apply(new EngineEvent.Terminal(reason))).ShouldContain(L.ReleasedOnLock);
        engine.State.ReleasedOnLock.ShouldBeTrue();
        engine.Receiver.IsEmpty.ShouldBeTrue();

        Notices(engine.Apply(new EngineEvent.SessionResumed())).ShouldBe([L.ReleasedOnLock]);
        engine.State.ReleasedOnLock.ShouldBeFalse();
        Notices(engine.Apply(new EngineEvent.SessionResumed())).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "SEG-006")]
    public void A_lock_with_nothing_held_says_nothing_when_the_session_is_back()
    {
        var engine = Engine();

        Notices(engine.Apply(new EngineEvent.Terminal(TerminalReason.Lock))).ShouldBeEmpty();

        Notices(engine.Apply(new EngineEvent.SessionResumed())).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "EC-EJE-10")]
    public void A_key_the_layout_lacks_sends_nothing_and_the_notice_names_the_key_and_the_app()
    {
        var engine = new EngineHarness(
            EngineHarness.DefaultConfig with
            {
                InterEventDelay = TimeSpan.Zero,
            }
        );
        engine.Foreground("notepad.exe", layout: Layouts.English);

        var effects = engine.Tap(Shortcuts.Tap("enye", "ctrl", "char:ñ"));

        engine.Sent.ShouldBeEmpty();
        var notice = effects.OfType<EngineEffect.Notice>().ShouldHaveSingleItem();
        notice.Urgency.ShouldBe(NoticeUrgency.Assertive);
        notice.Text.ShouldBe(L.KeyMissing(key: "Ñ", app: "notepad"));
        effects.OfType<EngineEffect.CountUsage>().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [Trait("Req", "ACC-006")]
    [Trait("Req", "EJE-002")]
    public void The_confirmation_window_lasts_as_many_times_as_the_multiplier_says(int multiplier)
    {
        var engine = Engine(EngineHarness.DefaultConfig with { TimeMultiplier = multiplier });
        var close = Shortcuts.Of(
            "close",
            new TapAction(Chords.Of("alt", "f4"), []),
            Shortcuts.Confirming
        );
        var window = Timings.Confirmation.ExecuteConfirmWindow * multiplier;

        engine.Tap(close);
        engine.State.Armed.ShouldNotBeNull().Until.ShouldBe(engine.Clock + window);

        engine.Advance(window - TimeSpan.FromMilliseconds(100));
        engine.State.Armed.ShouldNotBeNull();
        engine.Tap(close);
        engine.Settle();

        engine.State.Armed.ShouldBeNull();
        engine.Sent.ShouldNotBeEmpty();
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "ACC-006")]
    [Trait("Req", "EJE-002")]
    public void The_armed_shortcut_disarms_when_its_longer_window_ends()
    {
        var engine = Engine(EngineHarness.DefaultConfig with { TimeMultiplier = 2 });
        var close = Shortcuts.Of(
            "close",
            new TapAction(Chords.Of("alt", "f4"), []),
            Shortcuts.Confirming
        );

        engine.Tap(close);
        engine.Advance(Timings.Confirmation.ExecuteConfirmWindow + TimeSpan.FromMilliseconds(500));
        engine.State.Armed.ShouldNotBeNull();
        engine.Advance(Timings.Confirmation.ExecuteConfirmWindow);

        engine.State.Armed.ShouldBeNull();
        engine.Sent.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "BUR-004")]
    [Trait("Req", "SEG-007")]
    public void Pausing_releases_everything_and_nothing_is_sent_until_it_resumes()
    {
        var engine = Engine();
        var copy = Shortcuts.Tap("copy", "ctrl", "c");
        engine.Press(Shortcuts.Hold("shift", "shift"));
        engine.Settle();
        engine.Receiver.IsEmpty.ShouldBeFalse();

        engine.Apply(new EngineEvent.Terminal(TerminalReason.Pause));

        engine.State.Paused.ShouldBeTrue();
        engine.Receiver.IsEmpty.ShouldBeTrue();
        var sent = engine.Sent.Count();
        engine.Tap(copy, contact: 2);
        engine.Invoke(Shortcuts.Toggle("ctrl", "ctrl"));
        engine.Press(ScrollDown, contact: 3);
        engine.Settle();
        engine.Sent.Count().ShouldBe(sent);
        engine.State.IsQuiet.ShouldBeTrue();

        engine.Apply(new EngineEvent.SetPaused(false));
        engine.Advance(TimeSpan.FromSeconds(1));
        engine.Tap(copy, contact: 2);
        engine.Settle();

        engine.Sent.Count().ShouldBeGreaterThan(sent);
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "BUR-004")]
    [Trait("Req", "NFR-005")]
    public void An_engine_fault_while_paused_does_not_resume()
    {
        var engine = Engine();
        engine.Apply(new EngineEvent.Terminal(TerminalReason.Pause));

        engine.Apply(new EngineEvent.Terminal(TerminalReason.EngineFault));

        engine.State.Paused.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(9, 3)]
    [Trait("Req", "ACC-006")]
    public void A_scaled_time_stays_between_one_and_three_times_its_base(int multiplier, int times)
    {
        InteractionTime
            .Scale(TimeSpan.FromSeconds(3), multiplier)
            .ShouldBe(TimeSpan.FromSeconds(3 * times));
    }
}
