using Clicalo.Domain.Errors;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Execution.Internal;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Tests.Execution.Support;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>Text (EJE-008), mouse (EJE-009), macros (EJE-010) and the Shell actions (EJE-011, EJE-016).</summary>
public sealed class OtherPlannersTests
{
    private static EngineHarness Engine()
    {
        var engine = new EngineHarness(
            EngineHarness.DefaultConfig with
            {
                InterEventDelay = TimeSpan.Zero,
            }
        );
        engine.Foreground();
        return engine;
    }

    [Fact]
    [Trait("Req", "EJE-008")]
    public void Unicode_text_is_one_effect_with_the_secret_and_counts_as_a_use()
    {
        var engine = Engine();

        var effects = engine.Tap(Shortcuts.Text("sign", "Saludos, Michael"));

        var typed = effects.OfType<EngineEffect.TypeText>().ShouldHaveSingleItem();
        typed.Text.Length.ShouldBe(16);
        typed.Epoch.ShouldBe(engine.Epoch);
        effects.OfType<EngineEffect.CountUsage>().ShouldHaveSingleItem();
        engine.State.ToString().ShouldNotContain("Saludos");
    }

    [Fact]
    [Trait("Req", "EJE-008")]
    public void Paste_puts_the_text_on_the_clipboard_and_sends_ctrl_v_when_it_is_ready()
    {
        var engine = Engine();

        var paste = engine
            .Tap(Shortcuts.Text("sign", "hola", TextMethod.Paste))
            .OfType<EngineEffect.ClipboardPaste>()
            .Single();
        engine.Sent.ShouldBeEmpty();

        engine.Apply(new EngineEvent.ClipboardReady(paste.Effect));

        engine
            .Sent.Select(static e => (e.Kind, e.Key.Vk))
            .ShouldBe([
                (InjectedEventKind.KeyDown, (ushort)0xA2),
                (InjectedEventKind.KeyDown, (ushort)0x56),
                (InjectedEventKind.KeyUp, (ushort)0x56),
                (InjectedEventKind.KeyUp, (ushort)0xA2),
            ]);
        engine.State.PendingExternal.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "SEG-007")]
    public void A_paste_that_is_ready_after_release_all_sends_nothing()
    {
        var engine = Engine();
        var paste = engine
            .Tap(Shortcuts.Text("sign", "hola", TextMethod.Paste))
            .OfType<EngineEffect.ClipboardPaste>()
            .Single();

        engine.Apply(new EngineEvent.ReleaseAll(ReleaseReason.User));
        engine.Apply(new EngineEvent.ClipboardReady(paste.Effect));

        engine.Sent.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    public void A_click_acts_at_the_last_pointer_outside_clicalo()
    {
        var engine = Engine();

        var effect = engine
            .Tap(Shortcuts.Mouse("right", MouseOp.RightClick))
            .OfType<EngineEffect.MouseAction>()
            .Single();

        effect.Op.ShouldBe(MouseOp.RightClick);
        effect.Target.ShouldBe(engine.Pointer);
    }

    [Fact]
    [Trait("Req", "EJE-007")]
    [Trait("Req", "EJE-009")]
    public void A_drag_moves_first_then_holds_the_left_button_until_the_next_tap()
    {
        var engine = Engine();
        var drag = Shortcuts.Mouse("drag", MouseOp.Drag);

        var effects = engine.Tap(drag);

        effects.OfType<EngineEffect.MouseAction>().Single().Op.ShouldBe(MouseOp.Drag);
        engine.Receiver.Buttons.ShouldBe(MouseButtons.Left);
        engine.Advance(TimeSpan.FromSeconds(1));
        engine.Tap(drag);
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    public void A_scroll_repeats_while_its_contact_lasts_and_speeds_up()
    {
        var engine = Engine();

        engine.Press(Shortcuts.Mouse("down", MouseOp.ScrollDown, ScrollSpeed.Normal));
        engine.Advance(TimeSpan.FromMilliseconds(1_000));
        var steps = engine.Effects.OfType<EngineEffect.MouseAction>().Count();
        engine.Lift();
        engine.Advance(TimeSpan.FromSeconds(1));

        steps.ShouldBeGreaterThan(
            (int)(1_000 / Timings.Mouse.ScrollRepeatNormal.TotalMilliseconds)
        );
        engine.Effects.OfType<EngineEffect.MouseAction>().Count().ShouldBe(steps);
        engine.State.Scroll.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    public void The_scroll_interval_shrinks_down_to_its_floor()
    {
        MousePlanner.Interval(ScrollSpeed.Slow, 1).ShouldBe(Timings.Mouse.ScrollRepeatSlow);
        MousePlanner
            .Interval(ScrollSpeed.Fast, Timings.Engine.ScrollAccelerationEvery)
            .ShouldBeLessThan(Timings.Mouse.ScrollRepeatFast);
        MousePlanner.Interval(ScrollSpeed.Fast, 10_000).ShouldBe(Timings.Engine.ScrollRepeatFloor);
    }

    [Fact]
    [Trait("Req", "EJE-010")]
    public void A_macro_runs_its_steps_in_order_with_timer_waits()
    {
        var engine = Engine();
        var macro = Shortcuts.Macro(
            "m",
            new KeysStep(Chords.Of("ctrl", "c")),
            new WaitStep(TimeSpan.FromMilliseconds(500)),
            new TextStep(SecretText.From("x")),
            new MouseStep(MouseOp.RightClick)
        );

        engine.Tap(macro);
        engine.Sent.Count().ShouldBe(4);
        engine.State.Macro.ShouldNotBeNull().WaitingUntilTicks.ShouldNotBeNull();
        engine.Effects.OfType<EngineEffect.TypeText>().ShouldBeEmpty();

        engine.Advance(TimeSpan.FromMilliseconds(500));

        engine.Effects.OfType<EngineEffect.TypeText>().ShouldHaveSingleItem();
        engine.Effects.OfType<EngineEffect.MouseAction>().ShouldHaveSingleItem();
        engine.State.Macro.ShouldBeNull();
        engine.Effects.OfType<EngineEffect.CountUsage>().ShouldHaveSingleItem();
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "EJE-010")]
    public void A_second_tap_cancels_the_macro_and_releases_what_it_holds()
    {
        var engine = Engine();
        var macro = Shortcuts.Macro(
            "m",
            new MouseStep(MouseOp.Drag),
            new WaitStep(TimeSpan.FromSeconds(5)),
            new MouseStep(MouseOp.Drag)
        );
        engine.Tap(macro);
        engine.Receiver.Buttons.ShouldBe(MouseButtons.Left);
        engine.Advance(TimeSpan.FromSeconds(1));

        engine.Tap(macro);

        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.State.IsQuiet.ShouldBeTrue();
        engine.Advance(TimeSpan.FromSeconds(10));
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "EJE-010")]
    public void A_real_app_switch_cancels_the_macro()
    {
        var engine = Engine();
        engine.Tap(
            Shortcuts.Macro(
                "m",
                new WaitStep(TimeSpan.FromSeconds(1)),
                new KeysStep(Chords.Of("ctrl", "c"))
            )
        );

        engine.Foreground("word");
        engine.Advance(TimeSpan.FromSeconds(2));

        engine.Sent.ShouldBeEmpty();
        engine.State.Macro.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "EJE-011")]
    [Trait("Req", "EJE-016")]
    public void Shell_actions_leave_the_engine_and_their_results_may_come_back_in_any_order()
    {
        var engine = Engine();
        var url = engine
            .Tap(Shortcuts.Url("web", "https://example.com"), contact: 1)
            .OfType<EngineEffect.Launch>()
            .Single();
        var app = engine
            .Tap(Shortcuts.App("app", @"C:\Windows\notepad.exe"), contact: 2)
            .OfType<EngineEffect.Launch>()
            .Single();
        var lockPc = engine
            .Tap(Shortcuts.System("lock", "lock"), contact: 3)
            .OfType<EngineEffect.SystemCommand>()
            .Single();

        engine.State.PendingExternal.Count.ShouldBe(3);
        engine.Apply(new EngineEvent.SystemCommandCompleted(lockPc.Effect, Succeeded: true));
        engine
            .Apply(
                new EngineEvent.LaunchFailed(
                    app.Effect,
                    new Failure(
                        "launch.failed",
                        L.Incomplete,
                        FailureSeverity.Warning,
                        FailureRecovery.None,
                        FailureAnnouncement.Assertive
                    )
                )
            )
            .OfType<EngineEffect.Notice>()
            .ShouldHaveSingleItem();
        engine
            .Apply(new EngineEvent.LaunchCompleted(url.Effect))
            .OfType<EngineEffect.Notice>()
            .ShouldHaveSingleItem();
        engine.State.PendingExternal.ShouldBeEmpty();

        // A late result of an unknown effect (another engine's) is ignored.
        engine
            .Apply(new EngineEvent.LaunchCompleted(url.Effect))
            .ShouldAllBe(e => e is EngineEffect.Schedule || e is EngineEffect.CancelTimer);
        engine.Sent.ShouldBeEmpty();
    }
}
