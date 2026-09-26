using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>«Unpin» (docs/03 §8): a value edit, undoable without two taps (REG-04).</summary>
/// <param name="Id">The shortcut; a pin of a deleted shortcut can be removed too.</param>
public sealed record UnpinFromFrequents(ShortcutId Id) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        var frequents = document.Frequents.WithoutPin(Id);
        return Changes.Recorded(
            ReferenceEquals(frequents, document.Frequents)
                ? document
                : document with
                {
                    Frequents = frequents,
                },
            L.CtxUnpinT,
            null
        );
    }
}
