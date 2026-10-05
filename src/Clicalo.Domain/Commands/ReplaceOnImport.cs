using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Commands;

/// <summary>
/// «Replace» when importing (COP-002, REG-04): the library becomes the imported one, after a backup of the current
/// document that survives a restart (DAT-006). A last profile that no longer exists becomes General.
/// </summary>
/// <param name="Library">The imported library, already validated.</param>
public sealed record ReplaceOnImport(ShortcutLibrary Library) : IDestructiveCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        var settings =
            document.Settings.LastProfile is { } last && !Library.TryGetProfile(last, out _)
                ? document.Settings with
                {
                    LastProfile = ProfileId.General,
                }
                : document.Settings;
        return Changes.RecordedAfterBackup(
            document with
            {
                Library = Library,
                Settings = settings,
            },
            L.ImpReplaced,
            BackupKind.PreImportReplace,
            new LibraryReplaced()
        );
    }
}
