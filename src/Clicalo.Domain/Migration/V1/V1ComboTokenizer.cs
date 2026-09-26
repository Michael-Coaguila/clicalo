using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// The v1 combination grammar (catalog §7.3, MIG-003): chords separated by spaces, <c>+</c> as a key after another
/// <c>+</c> (<c>ctrl++</c>) or after <c>num</c> (<c>num+</c>), aliases and sides, U+2212 as minus, saved order kept.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the migration package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public static class V1ComboTokenizer
{
    /// <summary>Tokenizes a v1 combination; never produces an empty token (MIG-003).</summary>
    /// <param name="text">The <c>hotkey</c> text.</param>
    public static V1ComboParse Tokenize(string text) => throw new NotImplementedException();
}
