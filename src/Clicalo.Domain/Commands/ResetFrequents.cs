using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Commands;

/// <summary>
/// «Reset Frequents» (FRE-004, REG-04): empties usage, pins and hidden and raises the usage epoch, after a backup of
/// the current document; undo restores the three (it touches both Frequents slices).
/// </summary>
public sealed record ResetFrequents : IDestructiveCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Changes.RecordedAfterBackup(
            document with
            {
                Frequents = document.Frequents.Reset(),
            },
            L.ResetFreqT,
            BackupKind.PreResetFrequents
        );
    }
}
