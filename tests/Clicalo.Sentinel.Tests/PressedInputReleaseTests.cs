using Clicalo.Platform.Core.Injection;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// The release of whatever Windows reports down (ADR-0023), shared by Sentinel, the engine after an exception, «Soltar
/// todo» of the tray and the start: exactly the keys and buttons down go up, in virtual key mode with the extended flag
/// of their scan code, the modifiers last and Alt or Win after the menu mask.
/// </summary>
[Trait("Req", "SEG-006")]
[Trait("Req", "SEG-003")]
[Trait("Req", "REG-03")]
public sealed class PressedInputReleaseTests
{
    private const byte A = 0x41;
    private const byte LeftArrow = 0x25;
    private const byte LeftCtrl = 0xA2;
    private const byte RightCtrl = 0xA3;
    private const byte LeftShift = 0xA0;
    private const byte Shift = 0x10;
    private const byte Ctrl = 0x11;
    private const byte LeftAlt = 0xA4;
    private const byte LeftWin = 0x5B;

    [Fact]
    public void Exactly_the_keys_down_go_up_and_the_modifiers_last()
    {
        var state = new FakeKeyState { Down = { A, LeftCtrl, LeftArrow } };

        var batch = PressedInputRelease.BuildBatch(state);

        batch.ShouldAllBe(static input => input.Kind == LowLevelInputKind.KeyUp);
        batch.Select(static input => (byte)input.Key.Vk).ShouldBe([LeftArrow, A, LeftCtrl]);
    }

    [Fact]
    public void Alt_and_win_go_up_after_the_menu_mask()
    {
        var state = new FakeKeyState { Down = { LeftAlt, LeftWin } };

        var batch = PressedInputRelease.BuildBatch(state);

        var mask = PressedInputRelease.MenuMask;
        batch.ShouldBe([
            LowLevelInput.KeyDown(mask),
            LowLevelInput.KeyUp(mask),
            LowLevelInput.KeyUp(PressedInputRelease.KeyOf(state, LeftAlt)),
            LowLevelInput.KeyDown(mask),
            LowLevelInput.KeyUp(mask),
            LowLevelInput.KeyUp(PressedInputRelease.KeyOf(state, LeftWin)),
        ]);
        mask.Vk.ShouldBe(LowLevelInjector.MenuMaskVirtualKey);
    }

    [Fact]
    public void Keys_go_up_in_virtual_key_mode_with_the_extended_flag_of_their_scan_code()
    {
        var state = new FakeKeyState { Down = { A, LeftArrow, RightCtrl } };

        var keys = PressedInputRelease.BuildBatch(state).Select(static input => input.Key).ToList();

        keys.ShouldAllBe(static key =>
            (key.Attributes & PhysicalKeyAttributes.ScanCodeMode) == PhysicalKeyAttributes.None
        );
        keys.Single(static key => key.Vk == A).Attributes.ShouldBe(PhysicalKeyAttributes.None);
        keys.Single(static key => key.Vk == LeftArrow)
            .Attributes.ShouldBe(PhysicalKeyAttributes.Extended);
        var ctrl = keys.Single(static key => key.Vk == RightCtrl);
        ctrl.Attributes.ShouldBe(PhysicalKeyAttributes.Extended);
        ctrl.Scan.ShouldBe((ushort)(RightCtrl ^ 0x40));
    }

    [Fact]
    public void A_generic_modifier_goes_up_only_when_no_side_key_covers_it()
    {
        var covered = new FakeKeyState { Down = { Shift, LeftShift } };
        var alone = new FakeKeyState { Down = { Ctrl } };

        PressedInputRelease
            .BuildBatch(covered)
            .Select(static input => (byte)input.Key.Vk)
            .ShouldBe([LeftShift]);
        PressedInputRelease
            .BuildBatch(alone)
            .Select(static input => (byte)input.Key.Vk)
            .ShouldBe([Ctrl]);
    }

    [Fact]
    [Trait("Req", "EJE-007")]
    public void Buttons_go_up_first_and_either_main_button_releases_both()
    {
        var state = new FakeKeyState { Down = { A, 0x02, 0x04, 0x05 } };

        var batch = PressedInputRelease.BuildBatch(state);

        batch.ShouldBe([
            LowLevelInput.ButtonUp(LowLevelMouseButtons.Left),
            LowLevelInput.ButtonUp(LowLevelMouseButtons.Right),
            LowLevelInput.ButtonUp(LowLevelMouseButtons.Middle),
            LowLevelInput.ButtonUp(LowLevelMouseButtons.X1),
            LowLevelInput.KeyUp(PressedInputRelease.KeyOf(state, A)),
        ]);
    }

    [Fact]
    public void With_every_key_and_button_down_one_release_leaves_nothing_down()
    {
        var state = new FakeKeyState();
        for (int vk = 0x01; vk <= 0xFE; vk++)
        {
            if (vk != LowLevelInjector.MenuMaskVirtualKey)
            {
                state.Down.Add((byte)vk);
            }
        }

        var outcome = PressedInputRelease.ReleaseOnce(state, new FakeSender(state));

        outcome.Completed.ShouldBeTrue();
        state.Down.ShouldBeEmpty();
    }

    [Fact]
    public void Nothing_down_sends_nothing_and_an_unreadable_state_is_not_a_completed_release()
    {
        var state = new FakeKeyState();
        var sender = new FakeSender(state);

        PressedInputRelease.ReleaseOnce(state, sender).Completed.ShouldBeTrue();
        state.CanRead = false;
        var locked = PressedInputRelease.ReleaseOnce(state, sender);

        locked.Readable.ShouldBeFalse();
        locked.Completed.ShouldBeFalse();
        sender.Batches.ShouldBeEmpty();
    }

    [Fact]
    public void A_batch_taken_only_in_part_is_not_a_completed_release()
    {
        var state = new FakeKeyState { Down = { A, LeftCtrl } };
        var sender = new FakeSender(state) { TakeNext = 1 };

        var outcome = PressedInputRelease.ReleaseOnce(state, sender);

        outcome.Completed.ShouldBeFalse();
        state.Down.ShouldBe([LeftCtrl]);
    }
}
