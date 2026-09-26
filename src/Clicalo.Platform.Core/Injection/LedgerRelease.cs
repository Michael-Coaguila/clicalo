using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.Core.Injection;

/// <summary>
/// The releases of everything a ledger records, shared by the emergency releaser and Sentinel (ADR-0004): key ups in
/// reverse slot order with the mode, vk and scan of each press (INV-12), the menu mask before Alt or Win, and the
/// mouse buttons up.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public static class LedgerRelease
{
    /// <summary>The batch that releases everything in <paramref name="snapshot"/>.</summary>
    /// <param name="snapshot">The ledger.</param>
    public static ImmutableArray<LowLevelInput> BuildReleaseBatch(LedgerSnapshot snapshot) =>
        throw new NotImplementedException();
}
