using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Ports;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Store;

/// <summary>
/// The single writer of the user document (blueprint §6.4, ADR-0003): a short lock wraps only <c>Apply</c> and the
/// history, with no I/O inside; <see cref="Current"/> is published with <c>Volatile.Write</c> and read without the lock
/// from any thread. Undo restores only the slices the undone step touched, so undoing «delete shortcut» keeps the
/// usage recorded afterwards; «Reset Frequents» touches usage too, so its undo restores usage, pins and hidden
/// (FRE-004). Destructive commands need a <see cref="ConfirmationToken"/> (REG-04, CLC0010).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A change that touches no slice is not published: no revision, no event, no undo entry.</item>
/// <item><c>Record(label, key)</c> joins the open top entry with the same key (keeping its original <c>Before</c>);
/// otherwise it pushes a new entry and drops the oldest past 20 (DAT-006). An open entry that, once joined, would no
/// longer change anything is dropped, so a draft discarded after being created leaves no trace (ATJ-011).</item>
/// <item><c>Transparent</c> leaves the history alone (usage, presentation, positions); <c>Barrier</c> empties it.</item>
/// <item><c>BeforeApply(kind)</c> takes an in-memory snapshot through <see cref="IBackupService.SnapshotNow"/> inside
/// the lock, before publishing.</item>
/// </list>
/// Failures: the command's own, <c>store.destructive.unconfirmed</c>, <c>store.confirmation.mismatch</c> and
/// <c>store.undo.empty</c>. Exceptions thrown by a command are defects and propagate without any change.
/// </remarks>
public sealed class DocumentStore
{
    private readonly Lock _gate = new();
    private readonly UndoHistory _history = new();
    private readonly IIdGenerator _ids;
    private readonly IBackupService _backups;
    private readonly TimeProvider _time;
    private UserDocument _current;

    /// <summary>Creates the store with the document read at start-up.</summary>
    /// <param name="initial">The loaded document.</param>
    /// <param name="ids">Source of new ids for commands.</param>
    /// <param name="backups">Receives the in-memory snapshots of <c>BackupRequirement.BeforeApply</c>.</param>
    /// <param name="time">Clock of the <see cref="DomainContext"/>.</param>
    public DocumentStore(
        UserDocument initial,
        IIdGenerator ids,
        IBackupService backups,
        TimeProvider time
    )
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(time);
        _current = initial;
        _ids = ids;
        _backups = backups;
        _time = time;
    }

    /// <summary>Raised after every change, outside the lock, on the thread that caused it.</summary>
    public event EventHandler<DocumentChangedEventArgs>? Changed;

    /// <summary>The current document.</summary>
    public UserDocument Current => Volatile.Read(ref _current);

    /// <summary>Whether there is something to undo.</summary>
    public bool CanUndo
    {
        get
        {
            lock (_gate)
            {
                return _history.Count > 0;
            }
        }
    }

    /// <summary>What «Undo» names, or <see langword="null"/> when there is nothing to undo.</summary>
    public MessageKey? UndoLabel
    {
        get
        {
            lock (_gate)
            {
                return _history.Top?.Label;
            }
        }
    }

    /// <summary>
    /// Applies a non-destructive command; a failure changes nothing. A destructive command that reaches this overload
    /// through a base type is refused (<c>store.destructive.unconfirmed</c>).
    /// </summary>
    /// <param name="command">The command.</param>
    public Result<UserDocument> Dispatch(IDocumentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command is IDestructiveCommand
            ? Results.Fail<UserDocument>(StoreFailures.Unconfirmed())
            : Apply(command);
    }

    /// <summary>
    /// Applies a destructive command confirmed with two taps (REG-04). The token must have been confirmed for this
    /// operation: its <see cref="ConfirmationSubject.Operation"/> is the command's type name.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="token">Proof of the second tap, issued by <see cref="TwoStepConfirm"/>.</param>
    [SuppressMessage(
        "Clicalo.Safety",
        "CLC0010",
        Justification = "The token of this very call was checked above; this is the single place that runs a confirmed destructive command."
    )]
    public Result<UserDocument> Dispatch(IDestructiveCommand command, ConfirmationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(token);
        return string.Equals(
            token.Subject.Operation,
            command.GetType().Name,
            StringComparison.Ordinal
        )
            ? Apply(command)
            : Results.Fail<UserDocument>(StoreFailures.ConfirmationMismatch());
    }

    /// <summary>Undoes the newest step; fails when there is nothing to undo.</summary>
    public Result<UserDocument> Undo()
    {
        DocumentChangedEventArgs change;
        lock (_gate)
        {
            if (!_history.TryPop(out var entry))
            {
                return Results.Fail<UserDocument>(StoreFailures.NothingToUndo());
            }

            var before = _current;
            var restored = before.RestoreSlices(entry.Before, entry.Slices) with
            {
                Revision = before.Revision + 1,
            };
            Volatile.Write(ref _current, restored);
            change = new DocumentChangedEventArgs(
                before,
                restored,
                SliceDiff.Touched(before, restored),
                [],
                ChangeOrigin.Undo
            );
        }

        OnChanged(change);
        return Results.Ok(change.After);
    }

    /// <summary>Seals the open undo entry (the editor switched to another shortcut or profile, EDI-021).</summary>
    public void SealCoalescing()
    {
        lock (_gate)
        {
            _history.Seal();
        }
    }

    private Result<UserDocument> Apply(IDocumentCommand command)
    {
        DocumentChangedEventArgs? published = null;
        UserDocument result;
        lock (_gate)
        {
            var current = _current;
            var context = new DomainContext(
                _time.GetUtcNow(),
                _ids,
                current.Settings.Language,
                current.Settings.Keyboard.AppsLanguage
            );
            var applied = command.Apply(current, context);
            if (!applied.TryGetValue(out var change))
            {
                return Results.Fail<UserDocument>(applied.Failure);
            }

            var slices = SliceDiff.Touched(current, change.Next);
            if (slices == DocumentSlices.None)
            {
                return Results.Ok(current);
            }

            if (change.Backup is BackupRequirement.BeforeApply backup)
            {
                _backups.SnapshotNow(current, backup.Kind);
            }

            switch (change.Undo)
            {
                case UndoIntent.Record record:
                    RecordUndo(current, change.Next, slices, record);
                    break;
                case UndoIntent.Barrier:
                    _history.Clear();
                    break;
            }

            result = change.Next with { Revision = current.Revision + 1 };
            Volatile.Write(ref _current, result);
            published = new DocumentChangedEventArgs(
                current,
                result,
                slices,
                change.Events.IsDefault ? [] : change.Events,
                ChangeOrigin.Command
            );
        }

        OnChanged(published);
        return Results.Ok(result);
    }

    private void RecordUndo(
        UserDocument current,
        UserDocument next,
        DocumentSlices slices,
        UndoIntent.Record record
    )
    {
        var joins = _history.WouldJoin(record.CoalesceKey);
        _history.Record(
            new UndoEntry(current, slices, record.Label, record.CoalesceKey, Sealed: false)
        );
        if (
            joins
            && _history.Top is { } top
            && next.RestoreSlices(top.Before, top.Slices).Equals(next)
        )
        {
            // Undoing the joined entry would change nothing (a draft created and discarded): no trace (ATJ-011).
            _history.TryPop(out _);
        }
    }

    private void OnChanged(DocumentChangedEventArgs change) => Changed?.Invoke(this, change);
}
