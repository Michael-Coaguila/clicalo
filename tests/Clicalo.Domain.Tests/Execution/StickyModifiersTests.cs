using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.StickyModifiers;
using Clicalo.Domain.Tests.Execution.Support;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// The sticky modifiers row (FIJ-005, FIJ-006, EC-EJE-06): three states per key, in the ledger while active, added
/// first and without repeats to the next Tap, Hold, Toggle or panel mouse click, never to texts, webs, apps or macros;
/// «once» is used up, «locked» stays.
/// </summary>
[Trait("Req", "FIJ-006")]
public sealed class StickyModifiersTests
{
    private static readonly InjectedKey Ctrl = new(0xA2, 0x1D, false, InjectionMode.VirtualKey);
    private static readonly InjectedKey Shift = new(0xA0, 0x2A, false, InjectionMode.VirtualKey);
    private static readonly InjectedKey C = new(0x43, 0x2E, false, InjectionMode.VirtualKey);

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
    public void Each_tap_moves_a_key_from_released_to_once_to_locked_and_back()
    {
        var sticky = StickyState.Empty.Advance(ModifierKind.Ctrl);
        sticky.Ctrl.ShouldBe(StickyLevel.Once);
        sticky = sticky.Advance(ModifierKind.Ctrl);
        sticky.Ctrl.ShouldBe(StickyLevel.Locked);
        sticky.Advance(ModifierKind.Ctrl).ShouldBe(StickyState.Empty);
    }

    [Fact]
    public void After_use_once_is_released_and_locked_stays() =>
        (StickyState.Empty with { Ctrl = StickyLevel.Once, Shift = StickyLevel.Locked })
            .AfterUse()
            .ShouldBe(StickyState.Empty with { Shift = StickyLevel.Locked });

    [Fact]
    public void The_active_modifiers_go_first_in_the_canonical_order_without_repeats()
    {
        var sticky = StickyState.Empty with
        {
            Win = StickyLevel.Once,
            Shift = StickyLevel.Locked,
            Ctrl = StickyLevel.Once,
        };

        StickyModifierRules
            .Compose([new KeyStroke(KeyIds.Shift), new KeyStroke(KeyIds.C)], sticky)
            .Select(static s => s.Key.Value)
            .ShouldBe(["ctrl", "shift", "win", "c"]);
        StickyModifierRules
            .Compose([new KeyStroke(KeyIds.Ctrl, KeySide.Right), new KeyStroke(KeyIds.C)], sticky)
            .Select(static s => (s.Key.Value, s.Side))
            .ShouldBe([
                ("ctrl", KeySide.Any),
                ("shift", KeySide.Any),
                ("win", KeySide.Any),
                ("ctrl", KeySide.Right),
                ("c", KeySide.Any),
            ]);
        StickyModifierRules.Compose([new KeyStroke(KeyIds.C)], StickyState.Empty).Count.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "SEG-001")]
    [Trait("Req", "FIJ-005")]
    public void An_active_sticky_key_is_in_the_ledger_without_pressing_anything()
    {
        var engine = Engine();

        var effects = engine.Apply(new EngineEvent.StickyTapped(ModifierKind.Ctrl));

        engine
            .State.Keys.Items[HolderId.ForSticky(ModifierKind.Ctrl)]
            .Origin.ShouldBe(HoldOrigin.Sticky);
        engine.Receiver.IsEmpty.ShouldBeTrue();
        effects.OfType<EngineEffect.Notice>().ShouldHaveSingleItem();
        engine.Apply(new EngineEvent.StickyTapped(ModifierKind.Ctrl));
        engine.Apply(new EngineEvent.StickyTapped(ModifierKind.Ctrl));
        engine.State.Keys.IsEmpty.ShouldBeTrue();
        engine.State.Sticky.ShouldBe(StickyState.Empty);
    }

    [Fact]
    public void A_tap_takes_a_sticky_key_once_and_then_it_is_released()
    {
        var engine = Engine();
        engine.Apply(new EngineEvent.StickyTapped(ModifierKind.Ctrl));

        engine.Tap(Shortcuts.Tap("c", "c"), contact: 1);

        engine.Sent.ShouldBe([
            InjectedEvent.KeyDown(Ctrl),
            InjectedEvent.KeyDown(C),
            InjectedEvent.KeyUp(C),
            InjectedEvent.KeyUp(Ctrl),
        ]);
        engine.State.Sticky.ShouldBe(StickyState.Empty);
        engine.State.Keys.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void A_locked_sticky_key_joins_every_tap_until_it_is_tapped_again()
    {
        var engine = Engine();
        engine.Apply(new EngineEvent.StickyTapped(ModifierKind.Shift));
        engine.Apply(new EngineEvent.StickyTapped(ModifierKind.Shift));

        engine.Tap(Shortcuts.Tap("c", "c"), contact: 1);
        engine.Advance(TimeSpan.FromSeconds(1));
        engine.Tap(Shortcuts.Tap("c", "c"), contact: 1);

        engine.Sent.Count(e => e == InjectedEvent.KeyDown(Shift)).ShouldBe(2);
        engine.State.Sticky.Shift.ShouldBe(StickyLevel.Locked);
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    public void A_panel_click_holds_the_sticky_keys_around_it()
    {
        var engine = Engine();
        engine.Apply(new EngineEvent.StickyTapped(ModifierKind.Ctrl));

        var effects = engine.Tap(Shortcuts.Mouse("right", MouseOp.RightClick));

        effects
            .OfType<EngineEffect.Inject>()
            .First()
            .Events.ShouldBe([InjectedEvent.KeyDown(Ctrl)]);
        effects
            .SkipWhile(static e => e is not EngineEffect.MouseAction)
            .OfType<EngineEffect.Inject>()
            .First()
            .Events.ShouldBe([InjectedEvent.KeyUp(Ctrl)]);
        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.State.Sticky.ShouldBe(StickyState.Empty);
    }

    [Fact]
    [Trait("Req", "EJE-004")]
    public void A_hold_keeps_the_sticky_keys_while_it_is_held()
    {
        var engine = Engine();
        engine.Apply(new EngineEvent.StickyTapped(ModifierKind.Ctrl));

        engine.Press(Shortcuts.Hold("shift", "shift"));
        engine.Receiver.Keys.ShouldBe([Ctrl, Shift], ignoreOrder: true);

        engine.Lift();
        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.State.Sticky.ShouldBe(StickyState.Empty);
    }

    [Fact]
    [Trait("Req", "EJE-008")]
    public void Text_and_macros_leave_the_sticky_keys_pending()
    {
        var engine = Engine();
        engine.Apply(new EngineEvent.StickyTapped(ModifierKind.Ctrl));

        engine.Tap(Shortcuts.Text("t", "hola"), contact: 1);
        engine.Tap(Shortcuts.Macro("m", new KeysStep(Chords.Of("c"))), contact: 2);

        engine.Sent.ShouldBe([InjectedEvent.KeyDown(C), InjectedEvent.KeyUp(C)]);
        engine.State.Sticky.Ctrl.ShouldBe(StickyLevel.Once);
    }

    [Fact]
    [Trait("Req", "SEG-003")]
    public void Release_all_and_switching_the_row_off_clear_the_sticky_keys()
    {
        var engine = Engine();
        engine.Apply(new EngineEvent.StickyTapped(ModifierKind.Alt));
        engine.Apply(new EngineEvent.ReleaseAll(ReleaseReason.User));
        engine.State.Sticky.ShouldBe(StickyState.Empty);
        engine.State.Keys.IsEmpty.ShouldBeTrue();

        engine.Apply(new EngineEvent.StickyTapped(ModifierKind.Win));
        engine.Apply(new EngineEvent.ClearSticky());
        engine.State.Sticky.ShouldBe(StickyState.Empty);
        engine.State.Keys.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "SEG-004")]
    public void A_forgotten_sticky_key_is_released_at_the_global_limit()
    {
        var engine = Engine();
        engine.Apply(new EngineEvent.StickyTapped(ModifierKind.Ctrl));

        engine.Advance(EngineHarness.DefaultConfig.MaxHold!.Value);

        engine.State.Sticky.ShouldBe(StickyState.Empty);
        engine.State.Keys.IsEmpty.ShouldBeTrue();
    }
}
