using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Moves a shortcut to another position or list, atomically (I2): reordering in the grid (ATJ-009) and the position
/// buttons of the editor (EDI-016). Consecutive moves of the same shortcut form one undo step.
/// </summary>
/// <param name="Id">The shortcut.</param>
/// <param name="To">Target list.</param>
/// <param name="At">Position in the target list once the shortcut has left its place.</param>
public sealed record MoveShortcut(ShortcutId Id, ListRef To, ListPosition At) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document
            .Library.MoveShortcut(Id, To, At)
            .Bind(library =>
                Changes.Recorded(document with { Library = library }, L.Saved, Id.Value)
            );
    }
}
