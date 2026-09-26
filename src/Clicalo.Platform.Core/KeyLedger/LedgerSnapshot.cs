using System.Collections.Immutable;

namespace Clicalo.Platform.Core.KeyLedger;

/// <summary>A consistent copy of the ledger (the guardian's input, and the tests' «death at every step»).</summary>
/// <param name="LayoutVersion">Layout version read; anything but 2 is unreadable for this version.</param>
/// <param name="Marks">Header marks.</param>
/// <param name="Sequence">Sequence number.</param>
/// <param name="LastHeartbeatTicks">Last engine heartbeat.</param>
/// <param name="Generation">Engine generation.</param>
/// <param name="Slots">Slots that are not <see cref="LedgerSlotState.Free"/>.</param>
/// <param name="MouseButtons">Mouse buttons down.</param>
public sealed record LedgerSnapshot(
    ushort LayoutVersion,
    LedgerMarks Marks,
    ulong Sequence,
    long LastHeartbeatTicks,
    ulong Generation,
    ImmutableArray<LedgerSlot> Slots,
    LedgerMouseButtons MouseButtons
);
