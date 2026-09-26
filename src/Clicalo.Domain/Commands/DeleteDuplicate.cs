using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Deletes one appearance of a repeated combination from the editor's card (REP-005, REG-04). Only a repeated
/// appearance can be deleted this way; the card then opens another one (<c>DuplicateIndex.NextAfterDeleting</c>).
/// </summary>
/// <param name="Id">The appearance.</param>
public sealed record DeleteDuplicate(ShortcutId Id) : IDestructiveCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!DuplicateIndex.Build(document.Library, document.Duplicates).IsRepeated(Id))
        {
            return Changes.Fail(CommandFailures.NotRepeated());
        }

        return document
            .Library.RemoveShortcut(Id)
            .Bind(library =>
                Changes.Recorded(
                    document with
                    {
                        Library = library,
                    },
                    L.Deleted,
                    null,
                    new ShortcutRemoved(Id)
                )
            );
    }
}
