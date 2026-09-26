using System.Runtime.InteropServices;

namespace Clicalo.Platform.Core.KeyLedger;

/// <summary>One key slot of the ledger as read.</summary>
/// <param name="Key">The key.</param>
/// <param name="State">Its state.</param>
/// <param name="RefCount">How many presses hold it.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct LedgerSlot(PhysicalKey Key, LedgerSlotState State, ushort RefCount);
