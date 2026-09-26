using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Tests.KeySafety;

/// <summary>
/// The logical ledger (SEG-001, blueprint §7.4): reference counts per physical key, releases only when the last holder
/// lets go, reverse order, the menu mask on safety releases and deadlines per item.
/// </summary>
[Trait("Req", "SEG-001")]
public sealed class KeyboardLedgerTests
{
    private static readonly InjectedKey Shift = new(0xA0, 0x2A, false, InjectionMode.VirtualKey);
    private static readonly InjectedKey Ctrl = new(0xA2, 0x1D, false, InjectionMode.VirtualKey);
    private static readonly InjectedKey Alt = new(0xA4, 0x38, false, InjectionMode.VirtualKey);
    private static readonly InjectedKey Win = new(0x5B, 0x5B, true, InjectionMode.VirtualKey);
    private static readonly InjectedKey A = new(0x41, 0x1E, false, InjectionMode.VirtualKey);
    private static readonly InjectedKey ShiftScan = new(0, 0x2A, false, InjectionMode.ScanCode);

    private static PressedItem Item(string holder, long since, params InjectedKey[] keys) =>
        new(
            new HolderId(holder),
            HoldOrigin.Contact,
            new ShortcutId(holder),
            ContactId: null,
            [.. keys],
            MouseButtons.None,
            since,
            DeadlineTicks: null
        );

    [Fact]
    public void Acquiring_presses_each_key_once_in_order()
    {
        var transition = KeyboardLedger.Empty.Acquire(Item("h", 0, Ctrl, Shift, A));

        transition.Events.ShouldBe([
            InjectedEvent.KeyDown(Ctrl),
            InjectedEvent.KeyDown(Shift),
            InjectedEvent.KeyDown(A),
        ]);
        transition.Ledger.IsDown(Shift).ShouldBeTrue();
        transition.Ledger.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "EJE-006")]
    public void A_key_shared_by_two_holders_goes_up_only_with_the_last_one()
    {
        var first = KeyboardLedger.Empty.Acquire(Item("hold", 0, Shift));
        var second = first.Ledger.Acquire(Item("sticky", 1, Shift, A));

        second.Events.ShouldBe([InjectedEvent.KeyDown(A)]);

        var releaseFirst = second.Ledger.Release(new HolderId("hold"));
        releaseFirst.Events.ShouldBeEmpty();
        releaseFirst.Ledger.IsDown(Shift).ShouldBeTrue();

        var releaseSecond = releaseFirst.Ledger.Release(new HolderId("sticky"));
        releaseSecond.Events.ShouldBe([InjectedEvent.KeyUp(A), InjectedEvent.KeyUp(Shift)]);
        releaseSecond.Ledger.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "SEG-003")]
    public void A_safety_release_masks_Alt_and_Win_and_goes_in_reverse_order()
    {
        var ledger = KeyboardLedger.Empty.Acquire(Item("h", 0, Ctrl, Alt, Win)).Ledger;

        ledger
            .Release(new HolderId("h"))
            .Events.ShouldBe([
                InjectedEvent.MenuMask(InjectionMode.VirtualKey),
                InjectedEvent.KeyUp(Win),
                InjectedEvent.MenuMask(InjectionMode.VirtualKey),
                InjectedEvent.KeyUp(Alt),
                InjectedEvent.KeyUp(Ctrl),
            ]);
    }

    [Fact]
    [Trait("Req", "EJE-003")]
    public void The_planned_release_of_a_tap_never_masks_so_a_Win_tap_opens_Start()
    {
        var template = Item("tap", 0);
        var pressed = KeyboardLedger.Empty.Press(template, Win);

        pressed.Events.ShouldBe([InjectedEvent.KeyDown(Win)]);
        var lifted = pressed.Ledger.Lift(template.Holder, Win);
        lifted.Events.ShouldBe([InjectedEvent.KeyUp(Win)]);
        lifted.Ledger.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "SEG-003")]
    public void Release_all_releases_the_newest_item_first_and_every_key_once()
    {
        var ledger = KeyboardLedger
            .Empty.Acquire(Item("old", 1, Shift))
            .Ledger.Acquire(Item("new", 2, Ctrl, Shift, A))
            .Ledger;

        var all = ledger.ReleaseAll();

        all.Events.ShouldBe([
            InjectedEvent.KeyUp(A),
            InjectedEvent.KeyUp(Shift),
            InjectedEvent.KeyUp(Ctrl),
        ]);
        all.Ledger.ShouldBeSameAs(KeyboardLedger.Empty);
    }

    [Fact]
    [Trait("Req", "SEG-003")]
    public void Release_all_of_an_empty_ledger_sends_nothing() =>
        KeyboardLedger.Empty.ReleaseAll().Events.ShouldBeEmpty();

    [Fact]
    [Trait("Req", "EJE-007")]
    public void Mouse_buttons_are_counted_by_the_items_that_hold_them()
    {
        var drag = Item("drag", 0) with { Buttons = MouseButtons.Left };
        var macro = Item("macro", 1) with { Buttons = MouseButtons.Left };
        var first = KeyboardLedger.Empty.Acquire(drag);
        var second = first.Ledger.Acquire(macro);

        first.Events.ShouldBe([InjectedEvent.MouseDown(MouseButtons.Left)]);
        second.Events.ShouldBeEmpty();
        second.Ledger.Release(drag.Holder).Events.ShouldBeEmpty();
        second
            .Ledger.Release(drag.Holder)
            .Ledger.Release(macro.Holder)
            .Events.ShouldBe([InjectedEvent.MouseUp(MouseButtons.Left)]);
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    public void The_same_key_in_two_modes_is_two_holdings_released_each_in_its_mode()
    {
        var ledger = KeyboardLedger
            .Empty.Acquire(Item("vk", 0, Shift))
            .Ledger.Acquire(Item("scan", 1, ShiftScan))
            .Ledger;

        ledger.Holders.Count.ShouldBe(2);
        ledger.Release(new HolderId("scan")).Events.ShouldBe([InjectedEvent.KeyUp(ShiftScan)]);
        ledger.Release(new HolderId("vk")).Events.ShouldBe([InjectedEvent.KeyUp(Shift)]);
    }

    [Fact]
    [Trait("Req", "SEG-004")]
    public void Changing_the_global_limit_recomputes_only_the_items_that_follow_it()
    {
        var inherited = Item("inherited", 100) with
        {
            DeadlineTicks = 160,
            InheritsGlobalLimit = true,
        };
        var own = Item("own", 100) with { DeadlineTicks = 130 };
        var ledger = KeyboardLedger.Empty.Acquire(inherited).Ledger.Acquire(own).Ledger;

        var shorter = ledger.WithGlobalLimit(TimeSpan.FromSeconds(2), ticksPerSecond: 10);

        shorter.Items[inherited.Holder].DeadlineTicks.ShouldBe(120);
        shorter.Items[own.Holder].DeadlineTicks.ShouldBe(130);
        shorter.NextDeadline().ShouldBe(120);
        ledger.WithGlobalLimit(null, 10).Items[inherited.Holder].DeadlineTicks.ShouldBeNull();
        shorter.ExpiredAt(125).ShouldBe([inherited.Holder]);
    }

    [Fact]
    public void Acquiring_twice_for_one_holder_is_a_defect() =>
        Should.Throw<InvalidOperationException>(() =>
            KeyboardLedger.Empty.Acquire(Item("h", 0, A)).Ledger.Acquire(Item("h", 1, Shift))
        );

    [Fact]
    public void Releasing_a_holder_that_holds_nothing_changes_nothing()
    {
        var ledger = KeyboardLedger.Empty.Acquire(Item("h", 0, A)).Ledger;

        var transition = ledger.Release(new HolderId("other"));

        transition.Ledger.ShouldBeSameAs(ledger);
        transition.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Ticks_convert_with_the_time_source_frequency() =>
        KeyboardLedger.ToTicks(TimeSpan.FromMilliseconds(1500), 1000).ShouldBe(1500);
}
