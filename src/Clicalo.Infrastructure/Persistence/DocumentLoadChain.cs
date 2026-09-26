using System.Collections.Immutable;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// The file part of the load chain (blueprint §6.5, DAT-003, S11), over bytes:
/// <list type="number">
/// <item>the file itself: a future major stops here (read-only); an unreadable or invalid file is <b>moved</b> to
/// <c>quarantine\</c>, never deleted or overwritten;</item>
/// <item>the newest valid of <c>.tmp</c> (complete, hash verified), <c>.prev</c> and the emergency copy of
/// <c>pending\</c> — the only files a crash at any step of the write protocol can leave the newest version in;</item>
/// <item>otherwise nothing: the caller tries the backups and then a default in memory.</item>
/// </list>
/// A file held by another process is retried at <c>Timings.Persistence.WriteRetryBackoff</c>; if it stays locked it is
/// never quarantined, and the caller must not overwrite what it could not read.
/// </summary>
internal sealed partial class DocumentLoadChain
{
    private readonly QuarantineStore _quarantine;
    private readonly IAtomicFileSystem _files;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    /// <summary>Creates the chain.</summary>
    /// <param name="quarantine">Where unreadable files go.</param>
    /// <param name="files">The file operations.</param>
    /// <param name="time">Clock of the read retries.</param>
    /// <param name="logger">Logs codes and file names, never content or user paths.</param>
    public DocumentLoadChain(
        QuarantineStore quarantine,
        IAtomicFileSystem files,
        TimeProvider time,
        ILogger logger
    )
    {
        _quarantine = quarantine;
        _files = files;
        _time = time;
        _logger = logger;
    }

    /// <summary>Runs the chain for <paramref name="main"/>.</summary>
    /// <typeparam name="T">What a payload decodes into.</typeparam>
    /// <param name="main">The file (its <c>.tmp</c> and <c>.prev</c> are derived from it).</param>
    /// <param name="pending">The emergency copy, or <see langword="null"/> when the file has none.</param>
    /// <param name="format">The expected <c>format</c>.</param>
    /// <param name="supported">The greatest schema this version reads.</param>
    /// <param name="decode">Decodes a readable envelope, or returns <see langword="null"/> when it is not valid.</param>
    /// <param name="quarantineUnreadable">
    /// Whether an unreadable file is moved to quarantine: the document is; <c>usage.json</c> is not (§6.5).
    /// </param>
    /// <param name="cancellationToken">Cancels between read retries.</param>
    public async Task<LoadChainResult<T>> LoadAsync<T>(
        string main,
        string? pending,
        string format,
        SchemaVersion supported,
        Func<DocumentEnvelope, T?> decode,
        bool quarantineUnreadable,
        CancellationToken cancellationToken
    )
        where T : class
    {
        var name = Path.GetFileName(main);
        var quarantined = ImmutableArray.CreateBuilder<string>();
        var highestSeq = 0L;
        var existed = false;
        Candidate<T>? chosen = null;
        var mainUsable = false;
        var mainLocked = false;
        var mainMissing = false;

        var read = await ReadAsync(main, cancellationToken).ConfigureAwait(false);
        switch (read)
        {
            case ReadOutcome.Missing:
                mainMissing = true;
                break;
            case ReadOutcome.Locked:
                existed = true;
                mainLocked = true;
                LogLocked(_logger, name);
                break;
            case ReadOutcome.Bytes bytes:
                existed = true;
                switch (EnvelopeCodec.Read(bytes.Content, format, supported))
                {
                    case EnvelopeReadResult.FutureMajor future:
                        LogFutureMajor(_logger, name, future.Found);
                        return new LoadChainResult<T>(
                            LoadSource.FutureMajor,
                            null,
                            null,
                            null,
                            false,
                            false,
                            false,
                            false,
                            future.Found,
                            0,
                            [],
                            true
                        );
                    case EnvelopeReadResult.Readable readable:
                        highestSeq = Math.Max(highestSeq, readable.Envelope.Seq);
                        var decoded = decode(readable.Envelope);
                        if (decoded is not null)
                        {
                            mainUsable = true;
                            chosen = new Candidate<T>(
                                LoadSource.Main,
                                decoded,
                                readable.Envelope,
                                bytes.Content,
                                readable.HashMatches
                            );
                            if (!readable.HashMatches)
                            {
                                LogEditedExternally(_logger, name);
                            }
                        }
                        else if (quarantineUnreadable)
                        {
                            Quarantine(main, "invalid", quarantined);
                        }

                        break;
                    case EnvelopeReadResult.Unreadable unreadable when quarantineUnreadable:
                        Quarantine(main, unreadable.Reason, quarantined);
                        break;
                }

                break;
        }

        var others = new List<(
            string Path,
            LoadSource Source,
            bool RequireHash,
            bool QuarantineIfBad
        )>
        {
            (AtomicFile.TemporaryOf(main), LoadSource.Temporary, true, false),
        };
        if (!mainUsable)
        {
            others.Add(
                (AtomicFile.PreviousOf(main), LoadSource.Previous, false, quarantineUnreadable)
            );
        }

        if (pending is not null)
        {
            others.Add((pending, LoadSource.Pending, true, false));
        }

        foreach (var (path, source, requireHash, quarantineIfBad) in others)
        {
            var candidate = await CandidateAsync(
                    path,
                    source,
                    format,
                    supported,
                    decode,
                    requireHash,
                    quarantineIfBad,
                    quarantined,
                    cancellationToken
                )
                .ConfigureAwait(false);
            existed |= candidate.Existed;
            highestSeq = Math.Max(highestSeq, candidate.Seq);
            if (
                candidate.Value is { } value
                && (chosen is null || value.Envelope.Seq > chosen.Envelope.Seq)
            )
            {
                chosen = value;
            }
        }

        if (chosen is not null && chosen.Source != LoadSource.Main)
        {
            LogRecovered(_logger, name, chosen.Source, chosen.Envelope.Seq);
        }

        return new LoadChainResult<T>(
            chosen?.Source ?? LoadSource.None,
            chosen?.Decoded,
            chosen?.Envelope,
            chosen?.Bytes,
            chosen?.HashMatches ?? false,
            mainUsable,
            mainLocked,
            mainMissing,
            null,
            highestSeq,
            quarantined.ToImmutable(),
            existed || quarantined.Count > 0
        );
    }

    /// <summary>Reads a file, retrying while another process holds it.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels between retries.</param>
    internal async Task<ReadOutcome> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var backoff = Timings.Persistence.WriteRetryBackoff;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var bytes = _files.ReadAllBytesOrNull(path);
                return bytes is null ? new ReadOutcome.Missing() : new ReadOutcome.Bytes(bytes);
            }
            catch (Exception ex) when (TransientIo.IsIo(ex))
            {
                var kind = TransientIo.Classify(ex);
                if (kind == IoFailureKind.Missing)
                {
                    return new ReadOutcome.Missing();
                }

                if (!TransientIo.IsTransient(kind) || attempt >= backoff.Length)
                {
                    return new ReadOutcome.Locked(kind);
                }
            }

            await Task.Delay(backoff[attempt], _time, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<CandidateRead<T>> CandidateAsync<T>(
        string path,
        LoadSource source,
        string format,
        SchemaVersion supported,
        Func<DocumentEnvelope, T?> decode,
        bool requireHash,
        bool quarantineIfBad,
        ImmutableArray<string>.Builder quarantined,
        CancellationToken cancellationToken
    )
        where T : class
    {
        var read = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (read is not ReadOutcome.Bytes bytes)
        {
            return new CandidateRead<T>(read is ReadOutcome.Locked, 0, null);
        }

        switch (EnvelopeCodec.Read(bytes.Content, format, supported))
        {
            case EnvelopeReadResult.Readable readable when !requireHash || readable.HashMatches:
                var decoded = decode(readable.Envelope);
                if (decoded is not null)
                {
                    return new CandidateRead<T>(
                        true,
                        readable.Envelope.Seq,
                        new Candidate<T>(
                            source,
                            decoded,
                            readable.Envelope,
                            bytes.Content,
                            readable.HashMatches
                        )
                    );
                }

                if (quarantineIfBad)
                {
                    Quarantine(path, "invalid", quarantined);
                }

                return new CandidateRead<T>(true, readable.Envelope.Seq, null);
            case EnvelopeReadResult.Unreadable unreadable when quarantineIfBad:
                Quarantine(path, unreadable.Reason, quarantined);
                return new CandidateRead<T>(true, 0, null);
            default:
                return new CandidateRead<T>(true, 0, null);
        }
    }

    private void Quarantine(string path, string reason, ImmutableArray<string>.Builder quarantined)
    {
        var name = Path.GetFileName(path);
        var moved = _quarantine.Move(path);
        if (moved.TryGetValue(out var target))
        {
            quarantined.Add(target);
            var targetName = Path.GetFileName(target);
            LogQuarantined(_logger, name, reason, targetName);
        }
        else
        {
            LogQuarantineFailed(_logger, name, reason);
        }
    }

    [LoggerMessage(
        EventId = 5111,
        Level = LogLevel.Warning,
        Message = "doc.quarantined: {File} ({Reason}) moved to quarantine as {Target}"
    )]
    private static partial void LogQuarantined(
        ILogger logger,
        string file,
        string reason,
        string target
    );

    [LoggerMessage(
        EventId = 5112,
        Level = LogLevel.Error,
        Message = "doc.quarantine_failed: {File} ({Reason}) could not be moved; it stays untouched"
    )]
    private static partial void LogQuarantineFailed(ILogger logger, string file, string reason);

    [LoggerMessage(
        EventId = 5113,
        Level = LogLevel.Warning,
        Message = "doc.future_major: {File} has schema {Schema}; read-only, never written"
    )]
    private static partial void LogFutureMajor(ILogger logger, string file, SchemaVersion schema);

    [LoggerMessage(
        EventId = 5114,
        Level = LogLevel.Information,
        Message = "doc.edited_externally: {File} hash differs but the document is valid"
    )]
    private static partial void LogEditedExternally(ILogger logger, string file);

    [LoggerMessage(
        EventId = 5115,
        Level = LogLevel.Warning,
        Message = "doc.recovered: {File} taken from {Source} (seq {Seq})"
    )]
    private static partial void LogRecovered(
        ILogger logger,
        string file,
        LoadSource source,
        long seq
    );

    [LoggerMessage(
        EventId = 5116,
        Level = LogLevel.Warning,
        Message = "doc.locked_on_load: {File} held by another process; not overwritten this session"
    )]
    private static partial void LogLocked(ILogger logger, string file);

    /// <summary>A version that can be chosen.</summary>
    private sealed record Candidate<T>(
        LoadSource Source,
        T Decoded,
        DocumentEnvelope Envelope,
        byte[] Bytes,
        bool HashMatches
    )
        where T : class;

    /// <summary>What reading one candidate file found.</summary>
    private sealed record CandidateRead<T>(bool Existed, long Seq, Candidate<T>? Value)
        where T : class;

    /// <summary>The result of reading a file.</summary>
    internal abstract record ReadOutcome
    {
        private ReadOutcome() { }

        /// <summary>The file does not exist.</summary>
        public sealed record Missing : ReadOutcome;

        /// <summary>Another process kept it through every retry.</summary>
        /// <param name="Kind">The error.</param>
        public sealed record Locked(IoFailureKind Kind) : ReadOutcome;

        /// <summary>Its bytes.</summary>
        /// <param name="Content">The bytes.</param>
        public sealed record Bytes(byte[] Content) : ReadOutcome;
    }
}
