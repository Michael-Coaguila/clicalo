using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Creates a profile with a new id, and new ids for all its shortcuts (DAT-004): a blank profile or a template (the
/// template's own ids and names never become ids, EC-PLA-02).
/// </summary>
/// <param name="Profile">The content; its ids are replaced by new ones.</param>
/// <param name="At">Position among the profiles.</param>
public sealed record CreateProfile(Profile Profile, ListPosition At) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        var library = document.Library;
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var id = NewIds.Profile(library, context.Ids, taken);
        if (!id.TryGetValue(out var profileId))
        {
            return Changes.Fail(id.Failure);
        }

        var shortcuts = new List<Shortcut>(Profile.Shortcuts.Count);
        foreach (var shortcut in Profile.Shortcuts)
        {
            var shortcutId = NewIds.Shortcut(library, context.Ids, taken);
            if (!shortcutId.TryGetValue(out var newId))
            {
                return Changes.Fail(shortcutId.Failure);
            }

            shortcuts.Add(shortcut with { Id = newId });
        }

        var created = Profile with { Id = profileId, Shortcuts = [.. shortcuts] };
        return library
            .AddProfile(created, At)
            .Bind(next =>
                Changes.Recorded(
                    document with
                    {
                        Library = next,
                    },
                    L.ProfCreated,
                    null,
                    new ProfileCreated(profileId)
                )
            );
    }
}
