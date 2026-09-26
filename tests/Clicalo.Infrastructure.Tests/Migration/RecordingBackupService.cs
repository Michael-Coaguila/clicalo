using System.Collections.Immutable;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;

namespace Clicalo.Infrastructure.Tests.Migration;

/// <summary>A backup port that records the v1 originals it is asked to keep, and can refuse them.</summary>
internal sealed class RecordingBackupService(bool fail = false) : IBackupService
{
    public static Failure Refused { get; } =
        new(
            "backup.io.full",
            L.Retry,
            FailureSeverity.Critical,
            FailureRecovery.Retry,
            FailureAnnouncement.Assertive
        );

    public List<byte[]> KeptOriginals { get; } = [];

    public void SnapshotNow(UserDocument document, BackupKind kind) =>
        throw new InvalidOperationException("The v1 import never snapshots the document.");

    public Task<Result<BackupInfo>> CreateAsync(
        UserDocument document,
        BackupKind kind,
        CancellationToken cancellationToken
    ) => throw new InvalidOperationException("The v1 import never writes a document backup.");

    public Task<Result<BackupInfo>> KeepV1OriginalAsync(
        ReadOnlyMemory<byte> original,
        CancellationToken cancellationToken
    )
    {
        KeptOriginals.Add(original.ToArray());
        return Task.FromResult(
            fail
                ? Results.Fail<BackupInfo>(Refused)
                : Results.Ok(
                    new BackupInfo(
                        new BackupId("v1-original"),
                        BackupKind.V1Original,
                        DateTimeOffset.UnixEpoch,
                        0,
                        0
                    )
                )
        );
    }

    public Task<ImmutableArray<BackupInfo>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ImmutableArray<BackupInfo>.Empty);

    public Task<Result<UserDocument>> ReadAsync(BackupId id, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The v1 import never reads a backup.");
}
