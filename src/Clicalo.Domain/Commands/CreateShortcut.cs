using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Creates a shortcut with a new id (DAT-004): the first meaningful edit of a draft, or an element added from the
/// library (ATJ-010, ATJ-011). Its undo entry is keyed by the new id, so the edits that follow join it until the editor
/// moves to another shortcut (EDI-021). A blank draft is never created: it stays in the editor session.
/// </summary>
/// <param name="List">Where to create it.</param>
/// <param name="Shortcut">The content; its id is replaced by a new one.</param>
/// <param name="At">Position in the list.</param>
public sealed record CreateShortcut(ListRef List, Shortcut Shortcut, ListPosition At)
    : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        if (ShortcutCompleteness.IsBlankDraft(Shortcut))
        {
            return Changes.Fail(CommandFailures.BlankDraft());
        }

        var id = NewIds.Shortcut(document.Library, context.Ids);
        if (!id.TryGetValue(out var newId))
        {
            return Changes.Fail(id.Failure);
        }

        var added = document.Library.AddShortcut(List, Shortcut with { Id = newId }, At);
        return added.Bind(library =>
            Changes.Recorded(
                document with
                {
                    Library = library,
                },
                L.NewCreated,
                newId.Value,
                new ShortcutCreated(newId, List)
            )
        );
    }
}
