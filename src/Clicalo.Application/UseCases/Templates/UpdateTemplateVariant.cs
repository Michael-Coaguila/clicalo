using System.Collections.Immutable;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.UseCases.Templates;

/// <summary>
/// «Actualizar a la variante {idioma}» as one document command (EC-PLA-04): the installed shortcuts of a template that
/// still have the keys of another programs language get the ones of the current language. All of them change in one
/// step, so one [undo] brings every one back (REG-07). Nothing is added or removed, and ids, names, icons and options
/// stay.
/// </summary>
/// <param name="Shortcuts">The shortcuts as they will be: the same ids, with the action of the current variant.</param>
public sealed record UpdateTemplateVariant(ImmutableArray<Shortcut> Shortcuts) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        var library = document.Library;
        foreach (var shortcut in Shortcuts.IsDefault ? [] : Shortcuts)
        {
            var replaced = library.ReplaceShortcut(shortcut);
            if (!replaced.TryGetValue(out var next))
            {
                return Results.Fail<DocumentChange>(replaced.Failure);
            }

            library = next;
        }

        return Results.Ok(
            new DocumentChange(
                document with
                {
                    Library = library,
                },
                [],
                new UndoIntent.Record(L.Saved.Key, null),
                new BackupRequirement.None()
            )
        );
    }
}
