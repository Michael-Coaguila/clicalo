using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Discards a shortcut that became a blank draft again (ATJ-011, EC-EDI-04): no name, a Tap without keys. Its undo
/// entry is keyed by the shortcut, so it joins the open entry of its creation and edits, which the document store then
/// drops because it no longer changes anything: the draft leaves no trace. Anything but a blank draft is refused; only
/// <see cref="DeleteShortcut"/> deletes a real shortcut.
/// </summary>
/// <param name="Id">The draft.</param>
public sealed record DiscardDraft(ShortcutId Id) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.Library.TryGetShortcut(Id, out var shortcut))
        {
            return Changes.Fail(CommandFailures.ShortcutNotFound());
        }

        if (!ShortcutCompleteness.IsBlankDraft(shortcut))
        {
            return Changes.Fail(CommandFailures.NotBlankDraft());
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
                    Id.Value,
                    new ShortcutRemoved(Id)
                )
            );
    }
}
