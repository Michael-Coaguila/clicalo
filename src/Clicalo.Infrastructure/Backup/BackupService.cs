using System.Collections.Immutable;
using System.Threading.Channels;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Backup;

/// <summary>Backups with their envelope and retention per kind (blueprint §6.8).</summary>
/// <remarks>
/// <para>
/// Each backup is a complete document with its envelope and the usage of that moment, in
/// <c>backups\&lt;kind&gt;\</c> (see <c>BackupLayout</c>): the last 12 automatic ones, every manual one, 10 of each
/// <c>pre-*</c> kind and the v1 original forever. Listing reads each backup's own counts (COP-004).
/// </para>
/// <para>
/// <see cref="SnapshotNow"/> only queues (the document store calls it inside its lock); a single background writer
/// writes the queue in order. <see cref="FlushSnapshotsAsync"/> waits for it (exit, tests).
/// </para>
/// </remarks>
public sealed partial class BackupService : IBackupService, IDisposable
{
    private readonly DataLocations _locations;
    private readonly IAtomicFileWriter _writer;
    private readonly TimeProvider _time;
    private readonly ILogger<BackupService> _logger;
    private readonly DocumentCodec _codec;
    private readonly IAtomicFileSystem _files;
    private readonly Channel<Snapshot> _snapshots = Channel.CreateUnbounded<Snapshot>(
        new UnboundedChannelOptions { SingleReader = true }
    );
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _gate = new();
    private Task? _consumer;
    private long _seq = -1;
    private TaskCompletionSource _drained = CompletedSource();
    private int _queued;

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
    )
        : this(locations, writer, time, logger, new DocumentCodec()) { }

    /// <summary>Creates the service sharing <paramref name="codec"/> with the document repository.</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="writer">Atomic writes.</param>
    /// <param name="time">Clock of the file names and retention.</param>
    /// <param name="logger">Logs codes, never content.</param>
    /// <param name="codec">The payload codec of the live document.</param>
    public BackupService(
        DataLocations locations,
        IAtomicFileWriter writer,
        TimeProvider time,
        ILogger<BackupService> logger,
        DocumentCodec codec
    )
        : this(locations, writer, time, logger, codec, AtomicFile.Disk) { }

    /// <summary>Creates the service over other file operations (tests and S11).</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="writer">Atomic writes.</param>
    /// <param name="time">Clock of the file names and retention.</param>
    /// <param name="logger">Logs codes.</param>
    /// <param name="codec">The payload codec.</param>
    /// <param name="files">The file operations of reads, listing and rotation.</param>
    internal BackupService(
        DataLocations locations,
        IAtomicFileWriter writer,
        TimeProvider time,
        ILogger<BackupService> logger,
        DocumentCodec codec,
        IAtomicFileSystem files
    )
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(files);
        _locations = locations;
        _writer = writer;
        _time = time;
        _logger = logger;
        _codec = codec;
        _files = files;
    }

    /// <inheritdoc />
    public void SnapshotNow(UserDocument document, BackupKind kind)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (kind == BackupKind.V1Original)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "The v1 original is kept byte for byte."
            );
        }

        lock (_gate)
        {
            if (_queued++ == 0)
            {
                _drained = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
            }

            _consumer ??= Task.Run(ConsumeAsync);
        }

        if (!_snapshots.Writer.TryWrite(new Snapshot(document, kind)))
        {
            Settle();
        }
    }

    /// <summary>Waits until every queued snapshot has been written (or has failed and been logged).</summary>
    /// <param name="cancellationToken">Stops waiting, not the writes.</param>
    public Task FlushSnapshotsAsync(CancellationToken cancellationToken)
    {
        Task drained;
        lock (_gate)
        {
            drained = _drained.Task;
        }

        return drained.WaitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<BackupInfo>> CreateAsync(
        UserDocument document,
        BackupKind kind,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        if (kind == BackupKind.V1Original)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Use KeepV1OriginalAsync.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var now = _time.GetUtcNow();
        var seq = NextSeq();
        var payload = _codec.Encode(document, includeUsage: true);
        var bytes = EnvelopeCodec.Write(
            new DocumentEnvelope(
                DocumentFormats.Document,
                DocumentFormats.DocumentSchema,
                DocumentFormats.AppVersion,
                seq,
                now,
                string.Empty,
                payload
            )
        );
        var fileName = BackupLayout.FileName(now, seq);
        var path = Path.Combine(BackupLayout.FolderPath(_locations, kind), fileName);
        var written = await _writer
            .WriteAsync(path, bytes, cancellationToken)
            .ConfigureAwait(false);
        if (written.IsFailure)
        {
            LogBackupFailed(_logger, kind, written.Failure.Code);
            return Results.Fail<BackupInfo>(written.Failure);
        }

        Rotate(kind);
        LogBackupWritten(_logger, kind, seq);
        return Results.Ok(
            new BackupInfo(
                new BackupId(BackupLayout.Id(kind, fileName)),
                kind,
                now,
                document.Library.Profiles.Count,
                ShortcutCount(document)
            )
        );
    }

    /// <inheritdoc />
    public async Task<Result<BackupInfo>> KeepV1OriginalAsync(
        ReadOnlyMemory<byte> original,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _time.GetUtcNow();
        string fileName;
        var copy = 0;
        do
        {
            fileName = BackupLayout.V1FileName(now, copy++);
        } while (_files.Exists(Path.Combine(_locations.Backups, fileName)));

        var written = await _writer
            .WriteAsync(Path.Combine(_locations.Backups, fileName), original, cancellationToken)
            .ConfigureAwait(false);
        if (written.IsFailure)
        {
            LogBackupFailed(_logger, BackupKind.V1Original, written.Failure.Code);
            return Results.Fail<BackupInfo>(written.Failure);
        }

        LogBackupWritten(_logger, BackupKind.V1Original, 0);
        return Results.Ok(new BackupInfo(new BackupId(fileName), BackupKind.V1Original, now, 0, 0));
    }

    /// <inheritdoc />
    /// <remarks>The v1 original is not listed: it is not a Clícalo document and is never restored from here.</remarks>
    public Task<ImmutableArray<BackupInfo>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = ImmutableArray.CreateBuilder<BackupInfo>();
        foreach (var entry in BackupLayout.Scan(_locations, _files).OrderByDescending(e => e.Seq))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryCount(entry, out var profiles, out var shortcuts))
            {
                result.Add(
                    new BackupInfo(
                        new BackupId(entry.Id),
                        entry.Kind,
                        entry.CreatedAt,
                        profiles,
                        shortcuts
                    )
                );
            }
        }

        return Task.FromResult(result.ToImmutable());
    }

    /// <inheritdoc />
    public Task<Result<UserDocument>> ReadAsync(BackupId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!BackupLayout.TryParse(id.Value, out var entry))
        {
            return Task.FromResult(
                Results.Fail<UserDocument>(
                    PersistenceFailures.Warning(PersistenceFailures.BackupNotFoundCode)
                )
            );
        }

        byte[]? bytes;
        try
        {
            bytes = _files.ReadAllBytesOrNull(BackupLayout.PathOf(_locations, entry));
        }
        catch (Exception ex) when (TransientIo.IsIo(ex))
        {
            return Task.FromResult(
                Results.Fail<UserDocument>(PersistenceFailures.Io(TransientIo.Classify(ex)))
            );
        }

        if (bytes is null)
        {
            return Task.FromResult(
                Results.Fail<UserDocument>(
                    PersistenceFailures.Warning(PersistenceFailures.BackupNotFoundCode)
                )
            );
        }

        return Task.FromResult(Decode(bytes));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _snapshots.Writer.TryComplete();
        _stopping.Cancel();
        _stopping.Dispose();
    }

    /// <summary>A document backup read from bytes (restore, and the recovery chain).</summary>
    /// <param name="bytes">The file.</param>
    internal Result<UserDocument> Decode(ReadOnlySpan<byte> bytes) =>
        EnvelopeCodec.Read(bytes, DocumentFormats.Document, DocumentFormats.DocumentSchema) switch
        {
            EnvelopeReadResult.Readable readable => _codec
                .Decode(readable.Envelope.Payload, live: false)
                .Map(decoded => decoded.Document),
            EnvelopeReadResult.FutureMajor => Results.Fail<UserDocument>(
                PersistenceFailures.Warning(PersistenceFailures.SchemaNewerCode)
            ),
            _ => Results.Fail<UserDocument>(
                PersistenceFailures.Warning(PersistenceFailures.BackupUnreadableCode)
            ),
        };

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource();
        source.SetResult();
        return source;
    }

    private static int ShortcutCount(UserDocument document) =>
        document.Library.AlwaysVisible.Count
        + document.Library.Profiles.Sum(p => p.Shortcuts.Count);

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (
                var snapshot in _snapshots
                    .Reader.ReadAllAsync(_stopping.Token)
                    .ConfigureAwait(false)
            )
            {
                try
                {
                    _ = await CreateAsync(snapshot.Document, snapshot.Kind, _stopping.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogSnapshotFault(_logger, snapshot.Kind, ex);
                }
                finally
                {
                    Settle();
                }
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // Disposed: the remaining snapshots are abandoned with the process.
        }
    }

    private void Settle()
    {
        lock (_gate)
        {
            if (--_queued == 0)
            {
                _drained.TrySetResult();
            }
        }
    }

    private long NextSeq()
    {
        lock (_gate)
        {
            if (_seq < 0)
            {
                _seq = BackupLayout
                    .Scan(_locations, _files)
                    .Select(e => e.Seq)
                    .DefaultIfEmpty(0)
                    .Max();
            }

            return ++_seq;
        }
    }

    /// <summary>Keeps the newest <c>Retention(kind)</c> backups of <paramref name="kind"/>.</summary>
    private void Rotate(BackupKind kind)
    {
        if (BackupLayout.Retention(kind) is not { } keep)
        {
            return;
        }

        var old = BackupLayout
            .Scan(_locations, _files)
            .Where(e => e.Kind == kind)
            .OrderByDescending(e => e.Seq)
            .Skip(keep);
        foreach (var entry in old)
        {
            try
            {
                _files.Delete(BackupLayout.PathOf(_locations, entry));
            }
            catch (Exception ex) when (TransientIo.IsIo(ex))
            {
                var failure = TransientIo.Classify(ex);
                LogRotationFailed(_logger, kind, failure);
            }
        }
    }

    private bool TryCount(BackupLayout.Entry entry, out int profiles, out int shortcuts)
    {
        profiles = 0;
        shortcuts = 0;
        byte[]? bytes;
        try
        {
            bytes = _files.ReadAllBytesOrNull(BackupLayout.PathOf(_locations, entry));
        }
        catch (Exception ex) when (TransientIo.IsIo(ex))
        {
            return false;
        }

        if (
            bytes is null
            || EnvelopeCodec.Read(bytes, DocumentFormats.Document, DocumentFormats.DocumentSchema)
                is not EnvelopeReadResult.Readable readable
        )
        {
            return false;
        }

        (profiles, shortcuts) = DocumentImportReader.Count(readable.Envelope.Payload);
        return true;
    }

    [LoggerMessage(
        EventId = 5141,
        Level = LogLevel.Information,
        Message = "backup.written: {Kind} seq {Seq}"
    )]
    private static partial void LogBackupWritten(ILogger logger, BackupKind kind, long seq);

    [LoggerMessage(
        EventId = 5142,
        Level = LogLevel.Warning,
        Message = "backup.failed: {Kind} ({Code})"
    )]
    private static partial void LogBackupFailed(ILogger logger, BackupKind kind, string code);

    [LoggerMessage(
        EventId = 5143,
        Level = LogLevel.Information,
        Message = "backup.rotation_failed: an old {Kind} backup could not be removed ({Failure})"
    )]
    private static partial void LogRotationFailed(
        ILogger logger,
        BackupKind kind,
        IoFailureKind failure
    );

    [LoggerMessage(
        EventId = 5144,
        Level = LogLevel.Error,
        Message = "backup.snapshot_fault: {Kind}"
    )]
    private static partial void LogSnapshotFault(
        ILogger logger,
        BackupKind kind,
        Exception exception
    );

    /// <summary>A queued snapshot.</summary>
    private sealed record Snapshot(UserDocument Document, BackupKind Kind);
}
