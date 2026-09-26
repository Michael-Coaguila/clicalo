using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// What Sentinel and the emergency release send (ADR-0004): every recorded key up in reverse slot order, in the mode of
/// its press (INV-12), the menu mask before Alt and Win, and the mouse buttons up.
/// </summary>
[Trait("Req", "SEG-006")]
[Trait("Req", "SEG-007")]
public sealed class LedgerReleaseTests
{
    private static readonly PhysicalKey Ctrl = new(0xA2, 0x1D, LedgerKeyAttributes.None);
    private static readonly PhysicalKey Shift = new(0xA0, 0x2A, LedgerKeyAttributes.None);
    private static readonly PhysicalKey WinScan = new(
        0,
        0x5B,
        LedgerKeyAttributes.ScanCodeMode | LedgerKeyAttributes.Extended
    );
    private static readonly PhysicalKey Alt = new(0xA4, 0x38, LedgerKeyAttributes.None);

    [Fact]
    public void An_empty_ledger_releases_nothing()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();

        LedgerRelease.BuildReleaseBatch(ledger.Snapshot()).ShouldBeEmpty();
    }

    [Fact]
    public void Keys_go_up_in_reverse_order_with_the_mask_before_alt_and_win_and_buttons_first()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        foreach (var key in new[] { Ctrl, Shift, WinScan, Alt })
        {
            ledger.TryBeginDown(key, out var slot);
            ledger.CommitDown(slot);
        }

        ledger.SetMouseButtons(LedgerMouseButtons.Left | LedgerMouseButtons.Right);

        LedgerRelease
            .BuildReleaseBatch(ledger.Snapshot())
            .ShouldBe([
                LowLevelInput.ButtonUp(LedgerMouseButtons.Right),
                LowLevelInput.ButtonUp(LedgerMouseButtons.Left),
                LowLevelInput.KeyDown(LedgerRelease.MenuMask),
                LowLevelInput.KeyUp(LedgerRelease.MenuMask),
                LowLevelInput.KeyUp(Alt),
                LowLevelInput.KeyDown(LedgerRelease.MenuMask),
                LowLevelInput.KeyUp(LedgerRelease.MenuMask),
                LowLevelInput.KeyUp(WinScan),
                LowLevelInput.KeyUp(Shift),
                LowLevelInput.KeyUp(Ctrl),
            ]);
    }

    [Fact]
    public void A_key_recorded_but_perhaps_never_sent_is_released_too()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        ledger.TryBeginDown(Shift, out _);

        LedgerRelease.BuildReleaseBatch(ledger.Snapshot()).ShouldBe([LowLevelInput.KeyUp(Shift)]);
    }

    [Fact]
    public void The_mask_is_an_unassigned_virtual_key() =>
        LedgerRelease.MenuMask.ShouldBe(new PhysicalKey(0xE8, 0, LedgerKeyAttributes.None));
}
