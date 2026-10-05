using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// The only file writer of the product besides the log sink (blueprint §4.4): <c>*.tmp</c> with
/// <c>FILE_FLAG_WRITE_THROUGH</c> and <c>FlushFileBuffers</c>, then <c>ReplaceFileW(target, tmp, target.prev)</c>, or
/// <c>MoveFileEx(MOVEFILE_WRITE_THROUGH)</c> the first time. <c>SHARING_VIOLATION</c>, <c>LOCK_VIOLATION</c> and a
/// transient <c>ACCESS_DENIED</c> are retried at <c>Timings.Persistence.WriteRetryBackoff</c> (S11: Defender, the
/// indexer, a locking monitor and sync clients).
/// </summary>
/// <remarks>
/// After a crash at any step the target is the old version whole or the new one whole; in the one window where
/// <c>ReplaceFileW</c> has renamed the target but not yet the replacement, the old version is <c>.prev</c> and the new
/// one is <c>.tmp</c>, and the load chain recovers the newest of the two (DAT-002, S11).
/// </remarks>
public sealed partial class AtomicFile : IAtomicFileWriter
{
    private readonly TimeProvider _time;
    private readonly ILogger<AtomicFile> _logger;
    private readonly IAtomicFileSystem _files;

    /// <summary>Creates the writer.</summary>
    /// <param name="time">Clock of the retries.</param>
    /// <param name="logger">Logs codes and attempts, never content or user paths.</param>
    public AtomicFile(TimeProvider time, ILogger<AtomicFile> logger)
        : this(time, logger, Disk) { }

    /// <summary>Creates the writer over other file operations (the crashing and locking ones of S11).</summary>
    /// <param name="time">Clock of the retries.</param>
    /// <param name="logger">Logs codes and attempts.</param>
    /// <param name="files">The file operations.</param>
    internal AtomicFile(TimeProvider time, ILogger<AtomicFile> logger, IAtomicFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(files);
        _time = time;
        _logger = logger;
        _files = files;
    }

    /// <summary>The real disk.</summary>
    internal static IAtomicFileSystem Disk { get; } = new DiskFileSystem();

    /// <summary>The temporary file of <paramref name="path"/>.</summary>
    /// <param name="path">A target file.</param>
    internal static string TemporaryOf(string path) => path + ".tmp";

    /// <summary>The previous version of <paramref name="path"/>, kept by <c>ReplaceFileW</c>.</summary>
    /// <param name="path">A target file.</param>
    internal static string PreviousOf(string path) => path + ".prev";

    /// <inheritdoc />
    public async Task<Result<AtomicWriteReceipt>> WriteAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        cancellationToken.ThrowIfCancellationRequested();
        var backoff = Timings.Persistence.WriteRetryBackoff;
        var name = Path.GetFileName(path);
        for (var attempt = 1; ; attempt++)
        {
            IoFailureKind kind;
            try
            {
                var replaced = WriteOnce(path, content.Span);
                return Results.Ok(new AtomicWriteReceipt(path, attempt, replaced));
            }
            catch (Exception ex) when (TransientIo.IsIo(ex))
            {
                kind = TransientIo.Classify(ex);
            }

            if (!TransientIo.IsTransient(kind) || attempt > backoff.Length)
            {
                LogWriteFailed(_logger, name, attempt, kind);
                return Results.Fail<AtomicWriteReceipt>(PersistenceFailures.Io(kind));
            }

            LogWriteRetry(_logger, name, attempt, kind);
            await Task.Delay(backoff[attempt - 1], _time, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool WriteOnce(string path, ReadOnlySpan<byte> content)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            _files.CreateDirectory(folder);
        }

        var temporary = TemporaryOf(path);
        _files.WriteThrough(temporary, content);
        if (_files.Exists(path))
        {
            _files.Replace(path, temporary, PreviousOf(path));
            return true;
        }

        _files.Move(temporary, path);
        return false;
    }

    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Information,
        Message = "persist.write.retry: {File} attempt {Attempt} failed ({Kind}); retrying"
    )]
    private static partial void LogWriteRetry(
        ILogger logger,
        string file,
        int attempt,
        IoFailureKind kind
    );

    [LoggerMessage(
        EventId = 5102,
        Level = LogLevel.Warning,
        Message = "persist.write.failed: {File} after {Attempts} attempts ({Kind})"
    )]
    private static partial void LogWriteFailed(
        ILogger logger,
        string file,
        int attempts,
        IoFailureKind kind
    );

    /// <summary>The disk: the only place where the product opens a data file for writing (RS0030 exception «atomic-file»).</summary>
    private sealed class DiskFileSystem : IAtomicFileSystem
    {
        public bool Exists(string path) => File.Exists(path);

        public byte[]? ReadAllBytesOrNull(string path)
        {
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return null;
            }
        }

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        [SuppressMessage(
            "ApiDesign",
            "RS0030:Do not use banned APIs",
            Justification = "IAtomicFileWriter itself: the temporary file of the atomic write, with write-through and a flush to disk (ADR-0007)."
        )]
        public void WriteThrough(string path, ReadOnlySpan<byte> content)
        {
            using var stream = new FileStream(
                path,
                new FileStreamOptions
                {
                    Mode = FileMode.Create,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    Options = FileOptions.WriteThrough,
                    BufferSize = 0,
                }
            );
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        }

        public void Replace(string target, string replacement, string backup) =>
            File.Replace(replacement, target, backup, ignoreMetadataErrors: true);

        public void Move(string source, string target) =>
            File.Move(source, target, overwrite: false);

        public void Delete(string path) => File.Delete(path);

        public IReadOnlyList<string> Files(string directory, string pattern) =>
            Directory.Exists(directory) ? Directory.GetFiles(directory, pattern) : [];
    }
}
