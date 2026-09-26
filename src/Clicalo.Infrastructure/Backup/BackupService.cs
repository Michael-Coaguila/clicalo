using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Backup;

/// <summary>Backups with their envelope and retention per kind (blueprint §6.8).</summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the persistence package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class BackupService : IBackupService
{
    /// <summary>Creates the service.</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="writer">Atomic writes.</param>
    /// <param name="time">Clock of the file names and retention.</param>
    /// <param name="logger">Logs codes, never content.</param>
    public BackupService(
        DataLocations locations,
        IAtomicFileWriter writer,
        TimeProvider time,
        ILogger<BackupService> logger
    ) => throw new NotImplementedException();

    /// <inheritdoc />
    public void SnapshotNow(UserDocument document, BackupKind kind) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<BackupInfo>> CreateAsync(
        UserDocument document,
        BackupKind kind,
        CancellationToken cancellationToken
    ) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<BackupInfo>> KeepV1OriginalAsync(
        ReadOnlyMemory<byte> original,
        CancellationToken cancellationToken
    ) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<ImmutableArray<BackupInfo>> ListAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<UserDocument>> ReadAsync(BackupId id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
