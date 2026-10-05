using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Inserts a copy right after a shortcut, with a new id and the given name (EDI-019: the name with the localized
/// «[copySuffix]» in each language, built by the caller). The copy may be a repetition at once (EC-REP-02).
/// </summary>
/// <param name="Source">The shortcut to copy.</param>
/// <param name="Name">The name of the copy.</param>
public sealed record DuplicateShortcut(ShortcutId Source, LocalizedText Name) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        var library = document.Library;
        if (
            !library.TryLocate(Source, out var location)
            || !library.TryGetShortcut(Source, out var source)
        )
        {
            return Changes.Fail(CommandFailures.ShortcutNotFound());
        }

        var id = NewIds.Shortcut(library, context.Ids);
        if (!id.TryGetValue(out var newId))
        {
            return Changes.Fail(id.Failure);
        }

        var copy = source with { Id = newId, Name = Name };
        return library
            .AddShortcut(location.List, copy, ListPosition.At(location.Index + 1))
            .Bind(next =>
                Changes.Recorded(
                    document with
                    {
                        Library = next,
                    },
                    L.NewCreated,
                    newId.Value,
                    new ShortcutCreated(newId, location.List)
                )
            );
    }
}
