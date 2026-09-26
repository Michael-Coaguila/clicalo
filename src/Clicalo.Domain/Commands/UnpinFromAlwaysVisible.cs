using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// «Pin to Always visible» off (EDI-015): moves the shortcut to the end of the profile it came from if it still
/// exists, else of the profile shown, else of General, and forgets the origin.
/// </summary>
/// <param name="Id">The shortcut, in Always visible.</param>
/// <param name="ShownProfile">The profile the panel or the editor shows.</param>
public sealed record UnpinFromAlwaysVisible(ShortcutId Id, ProfileId ShownProfile)
    : IDocumentCommand
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

        if (location.List is not ListRef.AlwaysVisible)
        {
            return Changes.Fail(CommandFailures.NotInAlwaysVisible());
        }

        var target =
            shortcut.PinnedFrom is { } origin && library.TryGetProfile(origin, out _) ? origin
            : library.TryGetProfile(ShownProfile, out _) ? ShownProfile
            : ProfileId.General;
        return library
            .ReplaceShortcut(shortcut with { PinnedFrom = null })
            .Bind(unpinned =>
                unpinned.MoveShortcut(Id, new ListRef.InProfile(target), ListPosition.End)
            )
            .Bind(moved => Changes.Recorded(document with { Library = moved }, L.Saved, Id.Value));
    }
}
