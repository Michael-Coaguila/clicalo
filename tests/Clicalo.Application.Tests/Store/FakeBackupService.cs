using System.Collections.Concurrent;
using System.Collections.Immutable;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;

namespace Clicalo.Application.Tests.Store;

/// <summary>Records the in-memory snapshots the document store asks for; the rest is not used by the store.</summary>
internal sealed class FakeBackupService : IBackupService
{
    public ConcurrentQueue<(UserDocument Document, BackupKind Kind)> Snapshots { get; } = new();

    public void SnapshotNow(UserDocument document, BackupKind kind) =>
        Snapshots.Enqueue((document, kind));

    public Task<Result<int>> WriteSnapshotsAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Result<BackupInfo>> CreateAsync(
        UserDocument document,
        BackupKind kind,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

    public Task<Result<BackupInfo>> KeepV1OriginalAsync(
        ReadOnlyMemory<byte> original,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

    public Task<ImmutableArray<BackupInfo>> ListAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Result<UserDocument>> ReadAsync(BackupId id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
