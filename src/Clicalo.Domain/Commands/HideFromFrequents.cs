using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// «Remove from Frequents» (docs/03 §8): the shortcut is hidden from the used list and unpinned. A value edit,
/// undoable without two taps (REG-04).
/// </summary>
/// <param name="Id">The shortcut.</param>
public sealed record HideFromFrequents(ShortcutId Id) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        var frequents = document.Frequents.WithHidden(Id);
        return Changes.Recorded(
            ReferenceEquals(frequents, document.Frequents)
                ? document
                : document with
                {
                    Frequents = frequents,
                },
            L.CtxHideT,
            null
        );
    }
}
