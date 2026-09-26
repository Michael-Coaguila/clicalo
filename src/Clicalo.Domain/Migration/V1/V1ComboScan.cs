using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// A v1 combination split by the grammar of catalog §7.3 before it becomes <see cref="KeyChord"/> values: the strokes
/// of each chord in the saved order, and what has no catalog equivalent. Never contains an empty token (MIG-003).
/// </summary>
/// <param name="Chords">One list of strokes per space-separated chord, in order; a chord is never empty.</param>
/// <param name="Unresolved">
/// Tokens without catalog equivalent, as written in lower case, and chords that end in a dangling <c>+</c>
/// («Revisar», MIG-005). Never an empty string.
/// </param>
public sealed record V1ComboScan(
    ValueList<ValueList<KeyStroke>> Chords,
    ValueList<string> Unresolved
)
{
    /// <summary>Whether every token of the combination has a catalog key.</summary>
    public bool IsResolved => Unresolved.IsEmpty;
}
