using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Sentinel.Tests;

/// <summary>The ledger v2 layout shared by the engine and Sentinel is the one of blueprint §7.4.</summary>
[Trait("Req", "SEG-006")]
public sealed class KeyLedgerLayoutTests
{
    [Fact]
    public void The_slots_and_the_mouse_byte_fit_in_the_4_KiB_section()
    {
        (
            KeyLedgerLayout.SlotsOffset + (KeyLedgerLayout.SlotCount * KeyLedgerLayout.SlotSize)
        ).ShouldBe(KeyLedgerLayout.MouseButtonsOffset);
        KeyLedgerLayout.MouseButtonsOffset.ShouldBeLessThan(KeyLedgerLayout.SectionSize);
        KeyLedgerLayout.SectionSize.ShouldBe(4096);
    }

    [Fact]
    public void The_header_is_the_one_of_the_blueprint()
    {
        KeyLedgerLayout.Magic.ShouldBe(BitConverter.ToUInt32("CLKL"u8));
        KeyLedgerLayout.LayoutVersion.ShouldBe((ushort)2);
        KeyLedgerLayout.MarksOffset.ShouldBe(0x006);
        KeyLedgerLayout.SequenceOffset.ShouldBe(0x008);
        KeyLedgerLayout.HeartbeatOffset.ShouldBe(0x010);
        KeyLedgerLayout.GenerationOffset.ShouldBe(0x018);
    }

    [Fact]
    public void Sentinel_inherits_exactly_three_handles() =>
        SentinelStartInfo.InheritedHandleCount.ShouldBe(3);
}
