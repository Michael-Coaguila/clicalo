using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Keys;

/// <summary>
/// A normalized combination in <b>press order</b> (EJE-003, EDI-010): sided catalog keys become a base key and a side
/// (EDI-009), repeated strokes are removed and the saved order is kept. The only way to build one is
/// <see cref="Create"/>, so every chord in the model is normalized. Compare combinations for repetition with
/// <see cref="CanonicalChord"/>, never with this order-sensitive value.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the domain package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed record KeyChord
{
    private KeyChord(ValueList<KeyStroke> strokes) => Strokes = strokes;

    /// <summary>The empty chord (a draft or a cleared combination; incomplete, ATJ-009).</summary>
    public static KeyChord Empty { get; } = new([]);

    /// <summary>The strokes in press order.</summary>
    public ValueList<KeyStroke> Strokes { get; }

    /// <summary>Whether the chord has no key.</summary>
    public bool IsEmpty => Strokes.IsEmpty;

    /// <summary>
    /// Whether every stroke is a modifier (<c>altright</c>, <c>ctrl+win</c>): valid and complete (catalog §7.3).
    /// </summary>
    public bool IsModifiersOnly => throw new NotImplementedException();

    /// <summary>
    /// Normalizes <paramref name="strokes"/>: sided keys of the catalog (<see cref="KeyDefinition.BaseKey"/>) become
    /// their base key with a side, duplicates are dropped (first occurrence wins) and the order is kept.
    /// </summary>
    /// <param name="strokes">Strokes in press order; keys outside the catalog are kept as given.</param>
    public static KeyChord Create(IEnumerable<KeyStroke> strokes) =>
        throw new NotImplementedException();
}
