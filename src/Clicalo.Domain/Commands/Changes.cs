using System.Collections.Immutable;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Commands;

/// <summary>Builds the <see cref="DocumentChange"/> of a command.</summary>
internal static class Changes
{
    private static readonly BackupRequirement NoBackup = new BackupRequirement.None();

    /// <summary>An undoable step (REG-07).</summary>
    public static Result<DocumentChange> Recorded(
        UserDocument next,
        Message label,
        string? coalesceKey,
        params ImmutableArray<DomainEvent> events
    ) =>
        Results.Ok(
            new DocumentChange(
                next,
                events.IsDefault ? [] : events,
                new UndoIntent.Record(label.Key, coalesceKey),
                NoBackup
            )
        );

    /// <summary>An undoable step taken after a backup of the current document (§6.8, DAT-006).</summary>
    public static Result<DocumentChange> RecordedAfterBackup(
        UserDocument next,
        Message label,
        BackupKind backup,
        params ImmutableArray<DomainEvent> events
    ) =>
        Results.Ok(
            new DocumentChange(
                next,
                events.IsDefault ? [] : events,
                new UndoIntent.Record(label.Key, null),
                new BackupRequirement.BeforeApply(backup)
            )
        );

    /// <summary>A change that does not enter the undo history (the exemptions of <c>undo-exemptions.json</c>).</summary>
    public static Result<DocumentChange> Transparent(UserDocument next) =>
        Results.Ok(new DocumentChange(next, [], new UndoIntent.Transparent(), NoBackup));

    /// <summary>A failure of the command.</summary>
    public static Result<DocumentChange> Fail(Failure failure) =>
        Results.Fail<DocumentChange>(failure);
}
