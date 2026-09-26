using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Keys;

/// <summary>
/// The canonical key of a combination (REP-001): modifiers as a set with their side, main keys by canonical identity
/// and in order (aliases are already one <see cref="KeyId"/>). Used for repeated shortcuts, «already added», «already
/// there» and blocked combinations; persisted in <c>dupIgnored</c> through <see cref="ToStableString"/>.
/// </summary>
/// <param name="Modifiers">The modifiers as a set.</param>
/// <param name="Main">The non-modifier keys, in order.</param>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the domain package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public readonly record struct CanonicalChord(ChordModifiers Modifiers, ValueList<KeyId> Main)
{
    /// <summary>
    /// The canonical key of <paramref name="chord"/>, or <see langword="false"/> when it has no key at all (only
    /// shortcuts with at least one key have a key, REP-001).
    /// </summary>
    /// <param name="chord">A normalized chord.</param>
    /// <param name="canonical">The canonical key.</param>
    public static bool TryFrom(KeyChord chord, out CanonicalChord canonical) =>
        throw new NotImplementedException();

    /// <summary>Parses the text written by <see cref="ToStableString"/>.</summary>
    /// <param name="text">The persisted text.</param>
    /// <param name="canonical">The canonical key.</param>
    public static bool TryParse(string text, out CanonicalChord canonical) =>
        throw new NotImplementedException();

    /// <summary>
    /// A stable, culture-independent text (<c>ctrl+shift+s</c>, modifiers first in a fixed order) that round-trips
    /// through <see cref="TryParse"/>. It is a persisted format (<c>dupIgnored</c>, ADR-0007).
    /// </summary>
    public string ToStableString() => throw new NotImplementedException();

    /// <summary>
    /// The form compared with blocked combinations: a modifier without side counts as its left key (REP-001).
    /// </summary>
    public CanonicalChord ForBlockedComparison() => throw new NotImplementedException();
}
