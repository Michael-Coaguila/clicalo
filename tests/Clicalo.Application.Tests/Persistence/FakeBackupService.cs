using System.Collections.Immutable;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;

namespace Clicalo.Application.Tests.Persistence;

/// <summary>
/// Backups in memory: records what was written and when. The snapshots of <see cref="SnapshotNow"/> wait in a queue
/// until the scheduler writes them, and <see cref="RefuseSnapshots"/> makes that write fail like a full disk.
/// </summary>
internal sealed class FakeBackupService(TimeProvider time) : IBackupService
{
    private readonly List<(DateTimeOffset At, BackupKind Kind, UserDocument Document)> _created =
    [];
    private readonly Queue<(UserDocument Document, BackupKind Kind)> _snapshots = new();

    /// <summary>A failure returned by every write of a queued snapshot, or <see langword="null"/>.</summary>
    public Failure? RefuseSnapshots { get; set; }

    /// <summary>Every write of a queued snapshot, successful or not.</summary>
    public int SnapshotAttempts { get; private set; }

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

    public int Queued
    {
        get
        {
            lock (_snapshots)
            {
                return _snapshots.Count;
            }
        }
    }

    public void SnapshotNow(UserDocument document, BackupKind kind)
    {
        lock (_snapshots)
        {
            _snapshots.Enqueue((document, kind));
        }
    }

    public Task<Result<int>> WriteSnapshotsAsync(CancellationToken cancellationToken)
    {
        var written = 0;
        lock (_snapshots)
        {
            while (_snapshots.TryPeek(out var next))
            {
                SnapshotAttempts++;
                if (RefuseSnapshots is { } refused)
                {
                    return Task.FromResult(Results.Fail<int>(refused));
                }

                Record(next.Kind, next.Document);
                _ = _snapshots.Dequeue();
                written++;
            }
        }

        return Task.FromResult(Results.Ok(written));
    }

    public Task<Result<BackupInfo>> CreateAsync(
        UserDocument document,
        BackupKind kind,
        CancellationToken cancellationToken
    )
    {
        Record(kind, document);
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

    public Task<ImmutableArray<BackupInfo>> ListAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Result<UserDocument>> ReadAsync(BackupId id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    private void Record(BackupKind kind, UserDocument document)
    {
        lock (_created)
        {
            _created.Add((time.GetUtcNow(), kind, document));
        }
    }
}
