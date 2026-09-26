using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// The physical ledger v2 (blueprint §7.4, ADR-0004, ADR-0018): write-ahead slots with reference counts, marks,
/// generation and heartbeat, the same bytes in memory and in the unnamed section Sentinel maps read-only.
/// </summary>
[Trait("Req", "SEG-006")]
[Trait("Req", "SEG-001")]
public sealed class KeyLedgerSectionTests
{
    private static readonly PhysicalKey Ctrl = new(0xA2, 0x1D, LedgerKeyAttributes.None);
    private static readonly PhysicalKey CtrlScan = new(0, 0x1D, LedgerKeyAttributes.ScanCodeMode);
    private static readonly PhysicalKey A = new(0x41, 0x1E, LedgerKeyAttributes.None);

    [Fact]
    public void A_new_section_has_the_header_and_generation_one()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();

        var snapshot = ledger.Snapshot();

        snapshot.LayoutVersion.ShouldBe(KeyLedgerLayout.LayoutVersion);
        snapshot.Generation.ShouldBe(1UL);
        snapshot.Marks.ShouldBe(LedgerMarks.None);
        snapshot.Slots.ShouldBeEmpty();
        ledger.IsReadOnly.ShouldBeFalse();
    }

    [Fact]
    public void A_key_goes_through_down_pending_down_and_free()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();

        ledger.TryBeginDown(Ctrl, out var slot).ShouldBeTrue();
        ledger.Snapshot().Slots.ShouldBe([new LedgerSlot(Ctrl, LedgerSlotState.DownPending, 1)]);

        ledger.CommitDown(slot);
        ledger.TryFindDown(Ctrl, out var found).ShouldBeTrue();
        found.ShouldBe(slot);
        ledger.Snapshot().Slots.ShouldBe([new LedgerSlot(Ctrl, LedgerSlotState.Down, 1)]);

        ledger.CommitUp(slot);
        ledger.Snapshot().Slots.ShouldBeEmpty();
        ledger.TryFindDown(Ctrl, out _).ShouldBeFalse();
    }

    [Fact]
    public void A_second_press_adds_a_reference_and_the_slot_frees_with_the_last_release()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        ledger.TryBeginDown(Ctrl, out var first).ShouldBeTrue();
        ledger.CommitDown(first);

        ledger.TryBeginDown(Ctrl, out var second).ShouldBeTrue();

        second.ShouldBe(first);
        ledger.Snapshot().Slots.Single().ShouldBe(new LedgerSlot(Ctrl, LedgerSlotState.Down, 2));
        ledger.CommitUp(first);
        ledger.Snapshot().Slots.Single().RefCount.ShouldBe((ushort)1);
        ledger.CommitUp(first);
        ledger.Snapshot().Slots.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    public void The_same_key_in_scan_code_mode_is_another_slot()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();

        ledger.TryBeginDown(Ctrl, out var vk).ShouldBeTrue();
        ledger.TryBeginDown(CtrlScan, out var scan).ShouldBeTrue();

        scan.ShouldNotBe(vk);
        ledger.Snapshot().Slots.Select(static s => s.Key).ShouldBe([Ctrl, CtrlScan]);
    }

    [Fact]
    public void A_full_ledger_refuses_the_press()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        for (var i = 0; i < KeyLedgerLayout.SlotCount; i++)
        {
            ledger
                .TryBeginDown(
                    new PhysicalKey((ushort)(0x100 + i), 0, LedgerKeyAttributes.None),
                    out _
                )
                .ShouldBeTrue();
        }

        ledger.TryBeginDown(A, out var slot).ShouldBeFalse();
        slot.ShouldBe(-1);
    }

    [Fact]
    [Trait("Req", "SEG-007")]
    public void A_release_the_secure_desktop_refused_stays_recorded_until_it_goes()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        ledger.TryBeginDown(A, out var slot);
        ledger.CommitDown(slot);

        ledger.MarkReleasePending(slot);

        ledger.Snapshot().Slots.Single().State.ShouldBe(LedgerSlotState.ReleasePending);
        ledger.CommitUp(slot);
        ledger.Snapshot().Slots.ShouldBeEmpty();
    }

    [Fact]
    public void Marks_generation_heartbeat_and_buttons_are_kept_apart()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();

        ledger.SetMarks(LedgerMarks.EngineAlive | LedgerMarks.NoRelaunch);
        ledger.ClearMarks(LedgerMarks.NoRelaunch);
        ledger.IncrementGeneration().ShouldBe(2UL);
        ledger.WriteHeartbeat(123_456);
        ledger.SetMouseButtons(LedgerMouseButtons.Left);

        var snapshot = ledger.Snapshot();
        snapshot.Marks.ShouldBe(LedgerMarks.EngineAlive);
        snapshot.LayoutVersion.ShouldBe(KeyLedgerLayout.LayoutVersion);
        snapshot.Generation.ShouldBe(2UL);
        ledger.Generation.ShouldBe(2UL);
        snapshot.LastHeartbeatTicks.ShouldBe(123_456);
        snapshot.MouseButtons.ShouldBe(LedgerMouseButtons.Left);
    }

    [Fact]
    public void A_read_only_view_sees_every_write_and_refuses_to_write()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        using var view = ledger.ReadOnlyView();

        ledger.TryBeginDown(A, out _);

        view.IsReadOnly.ShouldBeTrue();
        view.Snapshot().Slots.Single().Key.ShouldBe(A);
        Should.Throw<InvalidOperationException>(() => view.TryBeginDown(Ctrl, out _));
        Should.Throw<InvalidOperationException>(() => view.SetMarks(LedgerMarks.CleanShutdown));
        Should.Throw<InvalidOperationException>(() => view.IncrementGeneration());
    }

    [Fact]
    public void The_unnamed_section_duplicated_for_the_guardian_opens_read_only_with_the_same_bytes()
    {
        using var ledger = KeyLedgerSection.CreateForEngine();
        ledger.TryBeginDown(Ctrl, out var slot);
        ledger.CommitDown(slot);
        ledger.SetMarks(LedgerMarks.EngineAlive);
        var handle = ledger.DuplicateForGuardian();

        using var guardian = KeyLedgerSection.OpenInherited(handle);

        guardian.IsReadOnly.ShouldBeTrue();
        guardian.Snapshot().Slots.ShouldBe([new LedgerSlot(Ctrl, LedgerSlotState.Down, 1)]);
        guardian.Marks.ShouldBe(LedgerMarks.EngineAlive);
        ledger.CommitUp(slot);
        guardian.Snapshot().Slots.ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => guardian.WriteHeartbeat(1));
    }

    [Fact]
    public void A_handle_that_is_not_a_ledger_does_not_open() =>
        KeyLedgerSection.TryOpenInherited(0x7FFF_FFF0, out _).ShouldBeFalse();

    [Fact]
    public void A_disposed_section_refuses_everything()
    {
        var ledger = KeyLedgerSection.CreateInMemory();
        ledger.Dispose();

        Should.Throw<ObjectDisposedException>(() => ledger.Snapshot());
        Should.Throw<ObjectDisposedException>(() => ledger.TryBeginDown(A, out _));
    }
}
