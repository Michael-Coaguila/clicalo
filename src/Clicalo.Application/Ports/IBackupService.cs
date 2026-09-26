using System.Collections.Immutable;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;

namespace Clicalo.Application.Ports;

/// <summary>
/// Backups (blueprint §6.8): complete documents with their envelope and the usage of that moment, retained per kind.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Takes a snapshot in memory now and queues its write (the document store calls it inside its lock before a
    /// change with <c>BackupRequirement.BeforeApply</c>). Never blocks and never does I/O on the caller's thread.
    /// </summary>
    /// <param name="document">The document before the change.</param>
    /// <param name="kind">The kind of backup.</param>
    void SnapshotNow(UserDocument document, BackupKind kind);

    /// <summary>Writes a backup now (the manual button, the automatic one 30 s after a significant change).</summary>
    /// <param name="document">The document.</param>
    /// <param name="kind">The kind of backup.</param>
    /// <param name="cancellationToken">Cancels before the write starts.</param>
    Task<Result<BackupInfo>> CreateAsync(
        UserDocument document,
        BackupKind kind,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Keeps a byte-for-byte copy of an imported v1 file as <c>backups\v1-original-&lt;date&gt;.json</c>, never deleted
    /// automatically (MIG-004).
    /// </summary>
    /// <param name="original">The bytes exactly as read.</param>
    /// <param name="cancellationToken">Cancels before the write starts.</param>
    Task<Result<BackupInfo>> KeepV1OriginalAsync(
        ReadOnlyMemory<byte> original,
        CancellationToken cancellationToken
    );

    /// <summary>Every backup, newest first.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    Task<ImmutableArray<BackupInfo>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Reads and validates a backup (restoring it is a destructive use case).</summary>
    /// <param name="id">The backup.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<Result<UserDocument>> ReadAsync(BackupId id, CancellationToken cancellationToken);
}
