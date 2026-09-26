using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Counts one effective execution of a shortcut for Frequents (FRE-002) at <see cref="DomainContext.Now"/>, and purges
/// the marks older than the usage window. Never an undo step: it would flood the history and is saved apart in
/// <c>usage.json</c> (<c>undo-exemptions.json</c>).
/// </summary>
/// <param name="Id">The shortcut that ran.</param>
public sealed record RecordUsage(ShortcutId Id) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        if (!document.Library.TryLocate(Id, out _))
        {
            return Changes.Fail(CommandFailures.ShortcutNotFound());
        }

        var usage = document.Frequents.Usage.Record(Id, context.Now, Timings.Frequents.UsageWindow);
        return Changes.Transparent(
            document with
            {
                Frequents = document.Frequents with { Usage = usage },
            }
        );
    }
}
