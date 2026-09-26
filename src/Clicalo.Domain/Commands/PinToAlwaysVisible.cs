using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// «Pin to Always visible» on (EDI-015): moves the shortcut to the end of Always visible and remembers its profile.
/// Pinning moves, never copies (I2). Already in Always visible, nothing changes.
/// </summary>
/// <param name="Id">The shortcut.</param>
public sealed record PinToAlwaysVisible(ShortcutId Id) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        var library = document.Library;
        if (
            !library.TryLocate(Id, out var location)
            || !library.TryGetShortcut(Id, out var shortcut)
        )
        {
            return Changes.Fail(CommandFailures.ShortcutNotFound());
        }

        if (location.List is not ListRef.InProfile origin)
        {
            return Changes.Recorded(document, L.Saved, Id.Value);
        }

        return library
            .ReplaceShortcut(shortcut with { PinnedFrom = origin.Id })
            .Bind(pinned => pinned.MoveShortcut(Id, new ListRef.AlwaysVisible(), ListPosition.End))
            .Bind(moved => Changes.Recorded(document with { Library = moved }, L.Saved, Id.Value));
    }
}
