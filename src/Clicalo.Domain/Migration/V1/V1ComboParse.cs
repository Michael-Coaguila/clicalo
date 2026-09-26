using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>A v1 combination tokenized (MIG-003): one chord per space-separated group, and the tokens without equivalent.</summary>
/// <param name="Chords">The chords, in order; more than one becomes a macro.</param>
/// <param name="Unresolved">Tokens without catalog equivalent, as written («Revisar», MIG-005).</param>
public sealed record V1ComboParse(ValueList<KeyChord> Chords, ValueList<string> Unresolved);
