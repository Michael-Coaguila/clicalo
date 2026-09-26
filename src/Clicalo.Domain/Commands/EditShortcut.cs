using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Replaces a shortcut in place (name, icon, type, keys, text or options). Consecutive edits of the same shortcut
/// form one undo step until the editor moves to another one (EDI-021).
/// </summary>
/// <param name="Shortcut">The new version, with the id of the edited shortcut.</param>
public sealed record EditShortcut(Shortcut Shortcut) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document
            .Library.ReplaceShortcut(Shortcut)
            .Bind(library =>
                Changes.Recorded(document with { Library = library }, L.Saved, Shortcut.Id.Value)
            );
    }
}
