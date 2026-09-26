using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Restores a backup (COP-004, REG-04): the whole document becomes the backup, after a backup of the current one, and
/// the step can be undone. A backup that breaks an invariant is refused.
/// </summary>
/// <param name="Backup">The document read from the backup.</param>
public sealed record RestoreBackup(UserDocument Backup) : IDestructiveCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!Backup.Validate().IsEmpty)
        {
            return Changes.Fail(CommandFailures.InvalidBackup());
        }

        return Changes.RecordedAfterBackup(
            Backup with
            {
                Revision = document.Revision,
            },
            L.RestoredT,
            BackupKind.PreRestore,
            new DocumentRestored()
        );
    }
}
