using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Commands;

/// <summary>
/// «It's fine» on a repeated combination (REP-005): its key joins <c>dupIgnored</c> and is no longer flagged. Undoable
/// like any data change (REG-07).
/// </summary>
/// <param name="Key">The canonical key.</param>
public sealed record MarkDuplicateAccepted(CanonicalChord Key) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        var ignored = document.Duplicates.Ignored;
        return Changes.Recorded(
            ignored.Contains(Key)
                ? document
                : document with
                {
                    Duplicates = new DuplicatePolicy(new(ignored.Items.Add(Key))),
                },
            L.DupKept,
            null
        );
    }
}
