namespace Clicalo.DevCli.Trace;

/// <summary>What the catalog says about an entry (catalog §0.2), plus the two kinds that carry no priority.</summary>
internal enum RequirementPriority
{
    /// <summary>MUST: needed to publish.</summary>
    Must,

    /// <summary>SHOULD: expected in the first public version.</summary>
    Should,

    /// <summary>COULD: can wait.</summary>
    Could,

    /// <summary>«Retirado»: withdrawn by a decision of the user; it needs no test.</summary>
    Retired,

    /// <summary>An edge case of §4 (<c>EC-…</c>): it has no priority of its own.</summary>
    EdgeCase,
}
