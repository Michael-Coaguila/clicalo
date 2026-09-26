using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// «Pin to Frequents» (FRE-001, docs/03 §8): the shortcut goes to the end of the pins and is no longer hidden. Pins
/// beyond the tiles shown are kept (the menu warns, <c>FrequentsProjection.IsPinLimitReached</c>).
/// </summary>
/// <param name="Id">The shortcut.</param>
public sealed record PinToFrequents(ShortcutId Id) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.Library.TryLocate(Id, out _))
        {
            return Changes.Fail(CommandFailures.ShortcutNotFound());
        }

        var frequents = document.Frequents.WithPin(Id);
        return Changes.Recorded(
            ReferenceEquals(frequents, document.Frequents)
                ? document
                : document with
                {
                    Frequents = frequents,
                },
            L.CtxPinT,
            null
        );
    }
}
