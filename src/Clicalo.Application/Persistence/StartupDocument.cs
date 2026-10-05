using Clicalo.Domain.Document;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.Persistence;

/// <summary>
/// What the start does to the document it read before anyone sees it (blueprint §6.5): the usage lives apart in
/// <c>usage.json</c>, so it is merged back here, without expired marks and without the marks of shortcuts that no
/// longer exist (FRE-002).
/// </summary>
public static class StartupDocument
{
    /// <summary>
    /// <paramref name="document"/> with <paramref name="usage"/> as its usage, purged at <paramref name="now"/>.
    /// </summary>
    /// <param name="document">The loaded document.</param>
    /// <param name="usage">The usage read for the document's <c>usageEpoch</c>.</param>
    /// <param name="now">The current time.</param>
    public static UserDocument WithUsage(
        UserDocument document,
        UsageHistory usage,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(usage);
        var library = document.Library;
        var purged = usage.Purge(
            now,
            Timings.Frequents.UsageWindow,
            id => library.TryLocate(id, out _)
        );
        return ReferenceEquals(purged, document.Frequents.Usage)
            ? document
            : document with
            {
                Frequents = document.Frequents with { Usage = purged },
            };
    }
}
