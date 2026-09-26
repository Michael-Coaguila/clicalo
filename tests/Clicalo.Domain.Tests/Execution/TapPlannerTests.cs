using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Tests.Execution.Support;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// Tap (EJE-003): press in the saved order, release in reverse order, one event per
/// <c>Timings.Injection.InterEventDelay</c>; all at once when the delay is zero (NFR-004).
/// </summary>
[Trait("Req", "EJE-003")]
public sealed class TapPlannerTests
{
    private static readonly InjectedKey Ctrl = new(0xA2, 0x1D, false, InjectionMode.VirtualKey);
    private static readonly InjectedKey Shift = new(0xA0, 0x2A, false, InjectionMode.VirtualKey);
    private static readonly InjectedKey S = new(0x53, 0x1F, false, InjectionMode.VirtualKey);

    [Fact]
    public void A_tap_presses_in_order_and_releases_in_reverse_order_one_event_per_pause()
    {
        var engine = new EngineHarness();
        engine.Foreground();
        var save = Shortcuts.Tap("save", "ctrl", "shift", "s");

        var first = engine.Tap(save);

        first.OfType<EngineEffect.Inject>().Single().Events.ShouldBe([InjectedEvent.KeyDown(Ctrl)]);
        engine.Timers.ShouldContainKey(new TimerKey("outbox"));
        engine.Settle();

        engine.Sent.ShouldBe([
            InjectedEvent.KeyDown(Ctrl),
            InjectedEvent.KeyDown(Shift),
            InjectedEvent.KeyDown(S),
            InjectedEvent.KeyUp(S),
            InjectedEvent.KeyUp(Shift),
            InjectedEvent.KeyUp(Ctrl),
        ]);
        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.State.IsQuiet.ShouldBeTrue();
        engine.Effects.OfType<EngineEffect.CountUsage>().Single().Shortcut.ShouldBe(save.Id);
        engine.Effects.OfType<EngineEffect.SetLastAction>().Single().Shortcut.ShouldBe(save.Id);
    }

    [Fact]
    [Trait("Req", "NFR-004")]
    public void The_events_are_spaced_by_the_inter_event_delay()
    {
        var engine = new EngineHarness();
        engine.Foreground();
        engine.Tap(Shortcuts.Tap("copy", "ctrl", "c"));

        engine
            .Timers[new TimerKey("outbox")]
            .ShouldBe(engine.Now + TimeSpan.FromMilliseconds(20).Ticks);
    }

    [Fact]
    [Trait("Req", "NFR-004")]
    public void With_no_delay_the_whole_tap_is_one_atomic_batch()
    {
        var engine = new EngineHarness(
            EngineHarness.DefaultConfig with
            {
                InterEventDelay = TimeSpan.Zero,
            }
        );
        engine.Foreground();

        var effects = engine.Tap(Shortcuts.Tap("copy", "ctrl", "c"));

        var batch = effects.OfType<EngineEffect.Inject>().ShouldHaveSingleItem();
        batch.Events.Length.ShouldBe(4);
        batch.IsRelease.ShouldBeFalse();
        batch.Epoch.ShouldBe(engine.Epoch);
        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.State.IsQuiet.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "EJE-006")]
    public void A_key_held_by_another_holder_is_neither_pressed_again_nor_released_under_it()
    {
        var engine = new EngineHarness(
            EngineHarness.DefaultConfig with
            {
                InterEventDelay = TimeSpan.Zero,
            }
        );
        engine.Foreground();
        engine.Press(Shortcuts.Hold("shift", "shift"), contact: 1);

        engine.Tap(Shortcuts.Tap("upper", "shift", "s"), contact: 2);

        engine.Sent.ShouldBe([
            InjectedEvent.KeyDown(Shift),
            InjectedEvent.KeyDown(S),
            InjectedEvent.KeyUp(S),
        ]);
        engine.Receiver.Keys.ShouldBe([Shift]);
        engine.Lift(1);
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    public void Compatible_mode_sends_scan_codes_and_releases_them_the_same_way()
    {
        var engine = new EngineHarness(mode: InjectionMode.ScanCode);
        engine.Foreground();

        engine.Tap(Shortcuts.Tap("copy", "ctrl", "c"));
        engine.Settle();

        engine.Sent.ShouldAllBe(e => e.Key.Mode == InjectionMode.ScanCode && e.Key.Vk == 0);
        engine.Receiver.Anomalies.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "EJE-008")]
    public void A_character_gets_the_shift_or_altgr_its_layout_needs_right_before_it()
    {
        var engine = new EngineHarness(
            EngineHarness.DefaultConfig with
            {
                InterEventDelay = TimeSpan.Zero,
            }
        );
        engine.Foreground(layout: Layouts.English);

        engine.Tap(Shortcuts.Tap("zoom", "ctrl", "char:+"));

        engine
            .Sent.Select(static e => (e.Kind, e.Key.Vk))
            .ShouldBe([
                (InjectedEventKind.KeyDown, (ushort)0xA2),
                (InjectedEventKind.KeyDown, (ushort)0xA0),
                (InjectedEventKind.KeyDown, (ushort)0xBB),
                (InjectedEventKind.KeyUp, (ushort)0xBB),
                (InjectedEventKind.KeyUp, (ushort)0xA0),
                (InjectedEventKind.KeyUp, (ushort)0xA2),
            ]);
    }

    [Fact]
    [Trait("Req", "EJE-003")]
    public void A_key_missing_from_the_layout_sends_nothing_and_warns()
    {
        var engine = new EngineHarness();
        engine.Foreground(layout: Layouts.English);

        var effects = engine.Tap(Shortcuts.Tap("enye", "char:ñ"));

        effects.OfType<EngineEffect.Inject>().ShouldBeEmpty();
        effects.OfType<EngineEffect.Notice>().ShouldHaveSingleItem();
    }

    [Fact]
    public void The_variant_of_the_apps_language_is_sent()
    {
        var engine = new EngineHarness(
            EngineHarness.DefaultConfig with
            {
                InterEventDelay = TimeSpan.Zero,
                AppsLanguage = Primitives.LangCode.En,
            }
        );
        engine.Foreground();
        var bold = Shortcuts.Of(
            "bold",
            new TapAction(
                Chords.Of("ctrl", "n"),
                [new ChordVariant(Primitives.LangCode.En, Chords.Of("ctrl", "b"))]
            )
        );

        engine.Tap(bold);

        engine.Sent.Select(static e => e.Key.Vk).ShouldContain((ushort)0x42);
        engine.Sent.Select(static e => e.Key.Vk).ShouldNotContain((ushort)0x4E);
    }

    [Fact]
    [Trait("Req", "SEG-008")]
    public void A_fifth_shift_in_one_second_waits()
    {
        var engine = new EngineHarness(
            EngineHarness.DefaultConfig with
            {
                InterEventDelay = TimeSpan.Zero,
            }
        );
        engine.Foreground();
        var shiftTap = Shortcuts.Tap("shift", "shift");

        for (var i = 0; i < 5; i++)
        {
            engine.Invoke(shiftTap);
        }

        engine.Sent.Count(static e => e.Kind == InjectedEventKind.KeyDown).ShouldBe(4);
        engine.Advance(TimeSpan.FromSeconds(1));
        engine.Sent.Count(static e => e.Kind == InjectedEventKind.KeyDown).ShouldBe(5);
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "SEG-007")]
    public void A_tap_planned_for_another_foreground_sends_nothing()
    {
        var engine = new EngineHarness();
        engine.Foreground();
        var stale = engine.Epoch;
        engine.Foreground("word", userSwitch: false);

        engine.Tap(Shortcuts.Tap("copy", "ctrl", "c"), epoch: stale);
        engine.Settle();

        engine.Sent.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "SEG-005")]
    public void A_real_app_switch_in_the_middle_of_a_tap_releases_what_it_pressed()
    {
        var engine = new EngineHarness();
        engine.Foreground();
        engine.Tap(Shortcuts.Tap("save", "ctrl", "shift", "s"));
        engine.Advance(TimeSpan.FromMilliseconds(25));
        engine.Receiver.Keys.Count.ShouldBe(2);

        engine.Foreground("word");
        engine.Settle();

        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.Receiver.Anomalies.ShouldBeEmpty();
        engine.Effects.OfType<EngineEffect.CountUsage>().ShouldBeEmpty();
    }
}
