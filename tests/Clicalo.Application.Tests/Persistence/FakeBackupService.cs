using System.Collections.Immutable;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;

namespace Clicalo.Application.Tests.Persistence;

/// <summary>Backups in memory: records what was taken and when.</summary>
internal sealed class FakeBackupService(TimeProvider time) : IBackupService
{
    private readonly List<(DateTimeOffset At, BackupKind Kind, UserDocument Document)> _created =
    [];

    public IReadOnlyList<(DateTimeOffset At, BackupKind Kind, UserDocument Document)> Created
    {
        get
        {
            lock (_created)
            {
                return [.. _created];
            }
        }
    }

    public void SnapshotNow(UserDocument document, BackupKind kind)
    {
        lock (_created)
        {
            _created.Add((time.GetUtcNow(), kind, document));
        }
    }

    public Task<Result<BackupInfo>> CreateAsync(
        UserDocument document,
        BackupKind kind,
        CancellationToken cancellationToken
    )
    {
        SnapshotNow(document, kind);
        return Task.FromResult(
            Results.Ok(
                new BackupInfo(
                    new BackupId(kind + "/" + Created.Count),
                    kind,
                    time.GetUtcNow(),
                    1,
                    0
                )
            )
        );
    }

    public Task<Result<BackupInfo>> KeepV1OriginalAsync(
        ReadOnlyMemory<byte> original,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

    public Task<ImmutableArray<BackupInfo>> ListAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Result<UserDocument>> ReadAsync(BackupId id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
