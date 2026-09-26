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
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the domain package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class DocumentStore
{
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
    ) => throw new NotImplementedException();

    /// <summary>Raised after every change, outside the lock, on the thread that caused it.</summary>
    public event EventHandler<DocumentChangedEventArgs>? Changed;

    /// <summary>The current document.</summary>
    public UserDocument Current => throw new NotImplementedException();

    /// <summary>Whether there is something to undo.</summary>
    public bool CanUndo => throw new NotImplementedException();

    /// <summary>What «Undo» names, or <see langword="null"/> when there is nothing to undo.</summary>
    public MessageKey? UndoLabel => throw new NotImplementedException();

    /// <summary>Applies a non-destructive command; a failure changes nothing.</summary>
    /// <param name="command">The command.</param>
    public Result<UserDocument> Dispatch(IDocumentCommand command) =>
        throw new NotImplementedException();

    /// <summary>Applies a destructive command confirmed with two taps (REG-04).</summary>
    /// <param name="command">The command.</param>
    /// <param name="token">Proof of the second tap, issued by <see cref="TwoStepConfirm"/>.</param>
    public Result<UserDocument> Dispatch(IDestructiveCommand command, ConfirmationToken token) =>
        throw new NotImplementedException();

    /// <summary>Undoes the newest step; fails when there is nothing to undo.</summary>
    public Result<UserDocument> Undo() => throw new NotImplementedException();

    /// <summary>Seals the open undo entry (the editor switched to another shortcut or profile, EDI-021).</summary>
    public void SealCoalescing() => throw new NotImplementedException();

    [SuppressMessage(
        "CodeQuality",
        "IDE0051:Remove unused private members",
        Justification = "Keeps the event raised in the contract; the implementation replaces it."
    )]
    private void OnChanged(DocumentChangedEventArgs change) => Changed?.Invoke(this, change);
}
