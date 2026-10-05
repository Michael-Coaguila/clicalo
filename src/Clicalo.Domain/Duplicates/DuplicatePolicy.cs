using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Duplicates;

/// <summary>
/// Repeated combinations the user marked «It's fine» (REP-002, REP-005, docs/02 <c>dupIgnored</c>).
/// </summary>
/// <param name="Ignored">Canonical keys not flagged as repeated.</param>
public sealed record DuplicatePolicy(ValueList<CanonicalChord> Ignored)
{
    /// <summary>Nothing ignored.</summary>
    public static DuplicatePolicy Empty { get; } = new([]);
}
