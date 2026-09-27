using System.Collections.Immutable;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.Core.Injection;

/// <summary>
/// The releases of everything a ledger records, shared by the emergency releaser and Sentinel (ADR-0004): key ups in
/// reverse slot order with the mode, vk and scan of each press (INV-12), the menu mask before Alt or Win, and the
/// mouse buttons up.
/// </summary>
/// <remarks>
/// A slot in <see cref="LedgerSlotState.DownPending"/> may never have reached <c>SendInput</c>: its release is one too
/// many, which is harmless (and the menu mask keeps an extra Alt or Win release from opening anything).
/// </remarks>
public static class LedgerRelease
{
    /// <summary>The menu mask key: an unassigned virtual key (<c>0xE8</c>) pressed and released.</summary>
    public static PhysicalKey MenuMask { get; } =
        new(LowLevelInjector.MenuMaskVirtualKey, 0, LedgerKeyAttributes.None);

    private static readonly LedgerMouseButtons[] ButtonsInReleaseOrder =
    [
        LedgerMouseButtons.X2,
        LedgerMouseButtons.X1,
        LedgerMouseButtons.Middle,
        LedgerMouseButtons.Right,
        LedgerMouseButtons.Left,
    ];

    /// <summary>The batch that releases everything in <paramref name="snapshot"/>.</summary>
    /// <param name="snapshot">The ledger.</param>
    public static ImmutableArray<LowLevelInput> BuildReleaseBatch(LedgerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var batch = ImmutableArray.CreateBuilder<LowLevelInput>();
        foreach (var button in ButtonsInReleaseOrder)
        {
            if ((snapshot.MouseButtons & button) != LedgerMouseButtons.None)
            {
                batch.Add(LowLevelInput.ButtonUp(button));
            }
        }

        AppendKeyUps(batch, snapshot, onlyPending: false);
        return batch.ToImmutable();
    }

    /// <summary>
    /// The key ups of the slots in <see cref="LedgerSlotState.ReleasePending"/>: the releases the secure desktop refused,
    /// sent again when the input desktop is back (blueprint §7.6, INV-3). Buttons are not included: the ledger keeps no
    /// pending state for them, and the engine keeps their refused releases itself.
    /// </summary>
    /// <param name="snapshot">The ledger.</param>
    public static ImmutableArray<LowLevelInput> BuildPendingReleaseBatch(LedgerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var batch = ImmutableArray.CreateBuilder<LowLevelInput>();
        AppendKeyUps(batch, snapshot, onlyPending: true);
        return batch.ToImmutable();
    }

    private static void AppendKeyUps(
        ImmutableArray<LowLevelInput>.Builder batch,
        LedgerSnapshot snapshot,
        bool onlyPending
    )
    {
        for (var i = snapshot.Slots.Length - 1; i >= 0; i--)
        {
            var slot = snapshot.Slots[i];
            if (onlyPending && slot.State != LedgerSlotState.ReleasePending)
            {
                continue;
            }

            if (PhysicalKeyKinds.IsAltOrWin(slot.Key))
            {
                batch.Add(LowLevelInput.KeyDown(MenuMask));
                batch.Add(LowLevelInput.KeyUp(MenuMask));
            }

            batch.Add(LowLevelInput.KeyUp(slot.Key));
        }
    }
}
