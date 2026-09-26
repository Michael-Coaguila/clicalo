using System.Collections.Immutable;

namespace Clicalo.Domain.KeySafety;

/// <summary>A new logical ledger and the physical events that make the system match it.</summary>
/// <param name="Ledger">The ledger after the operation.</param>
/// <param name="Events">What to send, in order; empty when the physical state does not change.</param>
public sealed record LedgerTransition(KeyboardLedger Ledger, ImmutableArray<InjectedEvent> Events);
