using System.Collections.Immutable;
using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// «[moveAlways]» on a repeated combination (REP-005): keeps the appearance in Always visible, or moves the open one
/// there (remembering its profile), and deletes from the profiles the appearances with the same combination and the
/// same name in some language. Shortcuts with another name are kept (decision DIS-49). It removes shortcuts, so it is
/// destructive: two taps and undo (REG-04). One undo step; the editor then opens <see cref="KeptIn"/>.
/// </summary>
/// <param name="Id">The appearance open in the editor.</param>
public sealed record KeepOnlyInAlwaysVisible(ShortcutId Id) : IDestructiveCommand
{
    /// <summary>The appearance that stays in Always visible after applying the command to <paramref name="library"/>.</summary>
    /// <param name="library">The library before the command.</param>
    /// <param name="id">The appearance open in the editor.</param>
    public static ShortcutId? KeptIn(ShortcutLibrary library, ShortcutId id)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (
            !library.TryGetShortcut(id, out var current)
            || !DuplicateIndex.TryGetKey(current, out var key)
        )
        {
            return null;
        }

        return library
                .AlwaysVisible.Items.FirstOrDefault(s =>
                    DuplicateIndex.TryGetKey(s, out var other) && other == key
                )
                ?.Id
            ?? id;
    }

    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        var library = document.Library;
        if (
            !library.TryLocate(Id, out var location)
            || !library.TryGetShortcut(Id, out var current)
            || !DuplicateIndex.TryGetKey(current, out var key)
        )
        {
            return Changes.Fail(CommandFailures.ShortcutNotFound());
        }

        var keptId = KeptIn(library, Id) ?? Id;
        var next = library;
        if (keptId == Id && location.List is ListRef.InProfile origin)
        {
            var moved = next.ReplaceShortcut(current with { PinnedFrom = origin.Id })
                .Bind(pinned =>
                    pinned.MoveShortcut(Id, new ListRef.AlwaysVisible(), ListPosition.End)
                );
            if (!moved.TryGetValue(out next))
            {
                return Changes.Fail(moved.Failure);
            }
        }

        next.TryGetShortcut(keptId, out var kept);
        var removed = ImmutableArray.CreateBuilder<DomainEvent>();
        foreach (var profile in library.Profiles)
        {
            foreach (var shortcut in profile.Shortcuts)
            {
                if (
                    shortcut.Id != keptId
                    && DuplicateIndex.TryGetKey(shortcut, out var other)
                    && other == key
                    && ShortcutNames.ShareAnyLanguage(shortcut.Name, kept!.Name)
                )
                {
                    var without = next.RemoveShortcut(shortcut.Id);
                    if (!without.TryGetValue(out next))
                    {
                        return Changes.Fail(without.Failure);
                    }

                    removed.Add(new ShortcutRemoved(shortcut.Id));
                }
            }
        }

        return Changes.Recorded(
            document with
            {
                Library = next,
            },
            L.Moved,
            null,
            removed.ToImmutable()
        );
    }
}
