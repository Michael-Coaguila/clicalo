using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Domain.Library;

/// <summary>
/// The completeness rule of ATJ-009, the only one: the editor marks with it, the panel refuses with it (EJE-015) and
/// the facet test requires a rule for every <see cref="ActionKind"/>.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the domain package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public static class ShortcutCompleteness
{
    /// <summary>What is missing in <paramref name="action"/>, or <see cref="CompletenessIssue.None"/>.</summary>
    /// <param name="action">The action to evaluate.</param>
    public static CompletenessIssue Evaluate(ShortcutAction action) =>
        throw new NotImplementedException();
}
