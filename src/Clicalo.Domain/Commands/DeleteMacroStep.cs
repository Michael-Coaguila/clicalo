using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>Deletes one step of a macro (EDI-013, REG-04: two taps and undo).</summary>
/// <param name="Id">The macro shortcut.</param>
/// <param name="Index">The zero-based step.</param>
public sealed record DeleteMacroStep(ShortcutId Id, int Index) : IDestructiveCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.Library.TryGetShortcut(Id, out var shortcut))
        {
            return Changes.Fail(CommandFailures.ShortcutNotFound());
        }

        if (shortcut.Action is not MacroAction macro)
        {
            return Changes.Fail(CommandFailures.NotMacro());
        }

        if (Index < 0 || Index >= macro.Steps.Count)
        {
            return Changes.Fail(CommandFailures.StepNotFound());
        }

        var edited = shortcut with
        {
            Action = new MacroAction(new(macro.Steps.Items.RemoveAt(Index))),
        };
        return document
            .Library.ReplaceShortcut(edited)
            .Bind(library => Changes.Recorded(document with { Library = library }, L.Saved, null));
    }
}
