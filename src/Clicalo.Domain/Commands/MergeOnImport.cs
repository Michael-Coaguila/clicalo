using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Commands;

/// <summary>
/// «Combinar» when importing (COP-002): the library becomes the merge the import planned (what was missing is added;
/// on a matching id the existing one wins; a repeated id gets a new one), after a backup of the current document, and
/// the step can be undone. It adds and never removes, so one tap is enough.
/// </summary>
/// <param name="Library">The merged library, already validated.</param>
public sealed record MergeOnImport(ShortcutLibrary Library) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Changes.RecordedAfterBackup(
            document with
            {
                Library = Library,
            },
            L.ImpMerged,
            BackupKind.PreImportReplace,
            new LibraryReplaced()
        );
    }
}
