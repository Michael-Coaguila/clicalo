using System.Collections.Immutable;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Adds several shortcuts at the end of a profile in one undoable step, each with a new id (DAT-004): «Añadir N que
/// faltan» of the preview of Plantillas (PLA-013, PLA-017).
/// </summary>
/// <param name="Profile">The profile.</param>
/// <param name="Shortcuts">The content; their ids are replaced by new ones.</param>
/// <param name="ProfileName">The name of the profile for the notice, in the interface language.</param>
public sealed record AddShortcuts(
    ProfileId Profile,
    ImmutableArray<Shortcut> Shortcuts,
    string ProfileName
) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        var shortcuts = Shortcuts.IsDefault ? ImmutableArray<Shortcut>.Empty : Shortcuts;
        if (shortcuts.Any(ShortcutCompleteness.IsBlankDraft))
        {
            return Changes.Fail(CommandFailures.BlankDraft());
        }

        var library = document.Library;
        var list = new ListRef.InProfile(Profile);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var events = ImmutableArray.CreateBuilder<DomainEvent>();
        foreach (var shortcut in shortcuts)
        {
            var id = NewIds.Shortcut(library, context.Ids, taken);
            if (!id.TryGetValue(out var newId))
            {
                return Changes.Fail(id.Failure);
            }

            var added = library.AddShortcut(list, shortcut with { Id = newId }, ListPosition.End);
            if (!added.TryGetValue(out var next))
            {
                return Changes.Fail(added.Failure);
            }

            library = next;
            events.Add(new ShortcutCreated(newId, list));
        }

        return Changes.Recorded(
            document with
            {
                Library = library,
            },
            // IDI-004: the count is an argument with its plural forms, never a number in the place of a name.
            L.AddedCountToProf(profile: ProfileName ?? string.Empty, count: events.Count),
            null,
            events.ToImmutable()
        );
    }
}
