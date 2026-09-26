using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Deletes a shortcut (REG-04: two taps and undo; EDI-019). Its pins and usage dangle on purpose, so undo puts it back
/// in its place among the pins with its usage (FRE-005).
/// </summary>
/// <param name="Id">The shortcut.</param>
public sealed record DeleteShortcut(ShortcutId Id) : IDestructiveCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
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
