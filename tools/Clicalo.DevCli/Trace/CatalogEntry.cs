namespace Clicalo.DevCli.Trace;

/// <summary>One requirement or edge case declared in <c>docs/requirements/catalog.md</c>.</summary>
/// <param name="Id">The identifier, for example <c>EJE-003</c>, <c>REG-01</c> or <c>EC-EJE-10</c>.</param>
/// <param name="Priority">Its priority, or what it is instead.</param>
/// <param name="Title">The title in the catalog, without the final period; the first words of an edge case.</param>
/// <param name="Deferred">Whether a decision of the user put it off until after this version («Aplazado»).</param>
/// <param name="Line">The 1-based line of the catalog that declares it.</param>
internal sealed record CatalogEntry(
    string Id,
    RequirementPriority Priority,
    string Title,
    bool Deferred,
    int Line
)
{
    /// <summary>The module prefix: <c>EJE</c> for <c>EJE-003</c>, <c>EC</c> for every edge case.</summary>
    public string Module => Id[..Id.IndexOf('-', StringComparison.Ordinal)];
}
