using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Infrastructure.Persistence.Mappers;
using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// <c>clicalo.json</c> (blueprint §6.5): envelope, DTOs separated from the Domain, validation on read and write,
/// repair of the repairable, quarantine and the recovery chain <c>.prev</c> → newest valid backup → default in memory.
/// </summary>
/// <remarks>
/// <para>
/// Saving serializes, reads the bytes back and compares them with the document (an invalid or lossy document is never
/// written), then writes atomically. When the write keeps failing, the same bytes go to <c>pending\clicalo.json</c>
/// (the emergency copy) and the failure is returned so the user sees «not saved» (DAT-002).
/// </para>
/// <para>
/// It never writes when the document is read-only: a future major, a file another process kept locked at start-up
/// (never overwrite what could not be read), or a default in memory the user has not accepted yet. Only one save runs at
/// a time: the save scheduler is its single caller (blueprint §3.2, Persistence thread).
/// </para>
/// </remarks>
public sealed partial class DocumentRepository : IDocumentRepository
{
    private readonly DataLocations _locations;
    private readonly IAtomicFileWriter _writer;
    private readonly IBackupService _backups;
    private readonly TimeProvider _time;
    private readonly ILogger<DocumentRepository> _logger;
    private readonly DocumentCodec _codec;
    private readonly IAtomicFileSystem _files;
    private readonly DocumentLoadChain _chain;
    private readonly Func<UserDocument> _createDefault;
    private long _seq;
    private volatile bool _readOnly;
    private volatile bool _awaitingAcceptance;

    /// <summary>Creates the repository.</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="writer">Atomic writes.</param>
    /// <param name="quarantine">Where unreadable documents go.</param>
    /// <param name="backups">Recovery from backups and the <c>pre-repair</c> copy.</param>
    /// <param name="time">Clock of the envelope.</param>
    /// <param name="logger">Logs codes, never content.</param>
    public DocumentRepository(
        DataLocations locations,
        IAtomicFileWriter writer,
        QuarantineStore quarantine,
        IBackupService backups,
        TimeProvider time,
        ILogger<DocumentRepository> logger
    )
        : this(locations, writer, quarantine, backups, time, logger, new DocumentCodec()) { }

    /// <summary>Creates the repository sharing <paramref name="codec"/> with the backup service.</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="writer">Atomic writes.</param>
    /// <param name="quarantine">Where unreadable documents go.</param>
    /// <param name="backups">Recovery from backups and the <c>pre-repair</c> copy.</param>
    /// <param name="time">Clock of the envelope.</param>
    /// <param name="logger">Logs codes, never content.</param>
    /// <param name="codec">The payload codec, shared so backups keep what the live document carried.</param>
    public DocumentRepository(
        DataLocations locations,
        IAtomicFileWriter writer,
        QuarantineStore quarantine,
        IBackupService backups,
        TimeProvider time,
        ILogger<DocumentRepository> logger,
        DocumentCodec codec
    )
        : this(locations, writer, quarantine, backups, time, logger, codec, AtomicFile.Disk, null)
    { }

    /// <summary>Creates the repository over other file operations and default document (tests and S11).</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="writer">Atomic writes.</param>
    /// <param name="quarantine">Where unreadable documents go.</param>
    /// <param name="backups">Recovery from backups.</param>
    /// <param name="time">Clock of the envelope.</param>
    /// <param name="logger">Logs codes.</param>
    /// <param name="codec">The payload codec.</param>
    /// <param name="files">The file operations of reads and of the <c>pre-repair</c> copy.</param>
    /// <param name="createDefault">The document of a first run or of nothing usable; a minimal one when null.</param>
    internal DocumentRepository(
        DataLocations locations,
        IAtomicFileWriter writer,
        QuarantineStore quarantine,
        IBackupService backups,
        TimeProvider time,
        ILogger<DocumentRepository> logger,
        DocumentCodec codec,
        IAtomicFileSystem files,
        Func<UserDocument>? createDefault
    )
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(quarantine);
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(files);
        _locations = locations;
        _writer = writer;
        _backups = backups;
        _time = time;
        _logger = logger;
        _codec = codec;
        _files = files;
        _chain = new DocumentLoadChain(quarantine, files, time, logger);
        _createDefault = createDefault ?? MinimalDefault;
    }

    /// <summary>Whether saving is disabled (see the remarks).</summary>
    public bool IsReadOnly => _readOnly;

    /// <summary>
    /// Whether the document in memory is the default shown when nothing usable was found, not yet accepted by the user:
    /// nothing is written until <see cref="AcceptDefaultDocument"/> (§6.5, DAT-003).
    /// </summary>
    public bool IsAwaitingAcceptance => _awaitingAcceptance;

    /// <summary>
    /// The user accepted the default document shown when nothing usable was found (§6.5): saving is enabled. It does
    /// nothing in any other read-only case.
    /// </summary>
    public void AcceptDefaultDocument()
    {
        if (_awaitingAcceptance)
        {
            _awaitingAcceptance = false;
            _readOnly = false;
        }
    }

    /// <inheritdoc />
    public async Task<DocumentLoad> LoadAsync(CancellationToken cancellationToken)
    {
        var chain = await _chain
            .LoadAsync(
                _locations.Document,
                _locations.PendingDocument,
                DocumentFormats.Document,
                DocumentFormats.DocumentSchema,
                envelope =>
                    _codec.Decode(envelope.Payload, live: false).TryGetValue(out var d) ? d : null,
                quarantineUnreadable: true,
                cancellationToken
            )
            .ConfigureAwait(false);
        _seq = chain.HighestSeq;
        _awaitingAcceptance = false;
        DocumentLoad load;
        if (chain.Source == LoadSource.FutureMajor)
        {
            _readOnly = true;
            var shown = await NewestBackupAsync(BackupKind.PreUpdate, cancellationToken)
                .ConfigureAwait(false);
            load = new DocumentLoad(
                shown ?? _createDefault(),
                DocumentLoadOutcome.FutureMajorReadOnly,
                0,
                chain.Quarantined,
                true
            );
        }
        else if (chain.Decoded is { } decoded)
        {
            _codec.Remember(decoded.Preserved);
            var outcome = OutcomeOf(chain, decoded);
            if (outcome == DocumentLoadOutcome.Repaired && chain.Bytes is { } original)
            {
                await KeepPreRepairAsync(original, cancellationToken).ConfigureAwait(false);
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    LogRepaired(_logger, string.Join(",", decoded.Repairs));
                }
            }

            _readOnly = chain.MainLocked;
            load = new DocumentLoad(
                decoded.Document,
                outcome,
                chain.Envelope!.Seq,
                chain.Quarantined,
                _readOnly
            );
        }
        else
        {
            load = await FromBackupOrDefaultAsync(chain, cancellationToken).ConfigureAwait(false);
        }

        LogLoaded(_logger, load.Outcome, load.Seq, load.IsReadOnly, load.Quarantined.Length);
        return load;
    }

    /// <inheritdoc />
    public async Task<Result<SaveReceipt>> SaveAsync(
        UserDocument document,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        if (_readOnly)
        {
            return Results.Fail<SaveReceipt>(PersistenceFailures.ReadOnly());
        }

        var seq = _seq + 1;
        var now = _time.GetUtcNow();
        var bytes = EnvelopeCodec.Write(
            new DocumentEnvelope(
                DocumentFormats.Document,
                DocumentFormats.DocumentSchema,
                DocumentFormats.AppVersion,
                seq,
                now,
                string.Empty,
                _codec.Encode(document, includeUsage: false)
            )
        );
        if (!ReadsBackAs(bytes, document))
        {
            LogRefusedInvalid(_logger, seq);
            return Results.Fail<SaveReceipt>(PersistenceFailures.Invalid());
        }

        // The seq is spent even when the write fails: a failed attempt can leave a complete .tmp with this seq, and the
        // next attempt (and its emergency copy) must go above it, or the load chain could prefer that older .tmp.
        _seq = seq;
        var written = await _writer
            .WriteAsync(_locations.Document, bytes, cancellationToken)
            .ConfigureAwait(false);
        if (written.IsFailure)
        {
            await KeepEmergencyCopyAsync(bytes, seq).ConfigureAwait(false);
            return Results.Fail<SaveReceipt>(written.Failure);
        }

        DiscardEmergencyCopy();
        return Results.Ok(new SaveReceipt(seq, now));
    }

    /// <summary>The minimal document: default settings and an empty General profile.</summary>
    internal static UserDocument MinimalDefault()
    {
        var general = new Profile(
            ProfileId.General,
            new LocalizedText([]),
            new Domain.Catalog.IconRef("apps"),
            false,
            new AppBinding.Manual(),
            InjectionMode.VirtualKey,
            [],
            null
        );
        return new UserDocument(
            0,
            ShortcutLibrary.CreateValidated([], [general]).Value,
            FrequentsState.Empty,
            DuplicatePolicy.Empty,
            SettingsSchema.Defaults,
            new OnboardingState(false)
        );
    }

    private static DocumentLoadOutcome OutcomeOf(
        LoadChainResult<DecodedPayload> chain,
        DecodedPayload decoded
    ) =>
        chain.Source switch
        {
            _ when !decoded.Repairs.IsDefaultOrEmpty => DocumentLoadOutcome.Repaired,
            LoadSource.Main when !chain.HashMatches => DocumentLoadOutcome.EditedExternally,
            LoadSource.Pending => DocumentLoadOutcome.RecoveredFromPending,
            _ when chain.MainUsable => DocumentLoadOutcome.Loaded,
            _ => DocumentLoadOutcome.RecoveredFromPrevious,
        };

    /// <summary>Step 2 of the write protocol: the bytes must read back as exactly this document, valid.</summary>
    private bool ReadsBackAs(byte[] bytes, UserDocument document)
    {
        if (
            EnvelopeCodec.Read(bytes, DocumentFormats.Document, DocumentFormats.DocumentSchema)
            is not EnvelopeReadResult.Readable { HashMatches: true } readable
        )
        {
            return false;
        }

        var decoded = _codec.Decode(readable.Envelope.Payload, live: false);
        return decoded.TryGetValue(out var back)
            && back.Repairs.IsDefaultOrEmpty
            && back.Document.Equals(Comparable(document));
    }

    /// <summary>The document as the payload carries it: no revision and no usage (usage lives in usage.json).</summary>
    private static UserDocument Comparable(UserDocument document) =>
        document with
        {
            Revision = 0,
            Frequents = document.Frequents with { Usage = UsageHistory.Empty },
        };

    private async Task<DocumentLoad> FromBackupOrDefaultAsync(
        LoadChainResult<DecodedPayload> chain,
        CancellationToken cancellationToken
    )
    {
        var backup = await NewestBackupAsync(null, cancellationToken).ConfigureAwait(false);
        if (backup is not null)
        {
            _readOnly = chain.MainLocked;
            return new DocumentLoad(
                backup,
                DocumentLoadOutcome.RecoveredFromBackup,
                0,
                chain.Quarantined,
                _readOnly
            );
        }

        var anyBackup = BackupLayout.Scan(_locations, _files).Count > 0;
        if (!chain.SomethingExisted && !anyBackup)
        {
            _readOnly = false;
            return new DocumentLoad(_createDefault(), DocumentLoadOutcome.FirstRun, 0, [], false);
        }

        _readOnly = true;
        _awaitingAcceptance = !chain.MainLocked;
        return new DocumentLoad(
            _createDefault(),
            DocumentLoadOutcome.DefaultInMemory,
            0,
            chain.Quarantined,
            true
        );
    }

    private async Task<UserDocument?> NewestBackupAsync(
        BackupKind? preferred,
        CancellationToken cancellationToken
    )
    {
        var backups = await _backups.ListAsync(cancellationToken).ConfigureAwait(false);
        var ordered = preferred is { } kind
            ? backups.Where(b => b.Kind == kind).Concat(backups.Where(b => b.Kind != kind))
            : backups;
        foreach (var info in ordered)
        {
            var read = await _backups.ReadAsync(info.Id, cancellationToken).ConfigureAwait(false);
            if (read.TryGetValue(out var document))
            {
                LogFromBackup(_logger, info.Kind);
                return document;
            }
        }

        return null;
    }

    private async Task KeepPreRepairAsync(byte[] original, CancellationToken cancellationToken)
    {
        var entries = BackupLayout.Scan(_locations, _files);

        // The repair is only written with the next change, so every start before it repairs the same bytes again: one
        // copy of them is enough (otherwise pre-repair\ would grow by one file per start).
        var newest = entries
            .Where(e => e.Kind == BackupKind.PreRepair)
            .OrderByDescending(e => e.Seq)
            .Select(e => (BackupLayout.Entry?)e)
            .FirstOrDefault();
        if (
            newest is { } last
            && _files.ReadAllBytesOrNull(BackupLayout.PathOf(_locations, last)) is { } kept
            && kept.AsSpan().SequenceEqual(original)
        )
        {
            return;
        }

        var seq = entries.Select(e => e.Seq).DefaultIfEmpty(0).Max() + 1;
        var path = Path.Combine(
            BackupLayout.FolderPath(_locations, BackupKind.PreRepair),
            BackupLayout.FileName(_time.GetUtcNow(), seq)
        );
        var written = await _writer
            .WriteAsync(path, original, cancellationToken)
            .ConfigureAwait(false);
        if (written.IsFailure)
        {
            LogPreRepairFailed(_logger, written.Failure.Code);
        }
    }

    private async Task KeepEmergencyCopyAsync(byte[] bytes, long seq)
    {
        var copy = await _writer
            .WriteAsync(_locations.PendingDocument, bytes, CancellationToken.None)
            .ConfigureAwait(false);
        if (copy.IsSuccess)
        {
            LogEmergencyCopy(_logger, seq);
        }
        else
        {
            LogEmergencyCopyFailed(_logger, seq, copy.Failure.Code);
        }
    }

    /// <summary>A successful save is newer than any emergency copy (its seq started above every one read or written).</summary>
    private void DiscardEmergencyCopy()
    {
        try
        {
            if (_files.Exists(_locations.PendingDocument))
            {
                _files.Delete(_locations.PendingDocument);
            }
        }
        catch (Exception ex) when (TransientIo.IsIo(ex))
        {
            var kind = TransientIo.Classify(ex);
            LogEmergencyCopyKept(_logger, kind);
        }
    }

    [LoggerMessage(
        EventId = 5121,
        Level = LogLevel.Information,
        Message = "doc.loaded: {Outcome}, seq {Seq}, read-only {ReadOnly}, {Quarantined} quarantined"
    )]
    private static partial void LogLoaded(
        ILogger logger,
        DocumentLoadOutcome outcome,
        long seq,
        bool readOnly,
        int quarantined
    );

    [LoggerMessage(EventId = 5122, Level = LogLevel.Warning, Message = "doc.repaired: {Repairs}")]
    private static partial void LogRepaired(ILogger logger, string repairs);

    [LoggerMessage(
        EventId = 5123,
        Level = LogLevel.Error,
        Message = "persist.invalid: document seq {Seq} did not read back identical and valid; not written"
    )]
    private static partial void LogRefusedInvalid(ILogger logger, long seq);

    [LoggerMessage(
        EventId = 5124,
        Level = LogLevel.Warning,
        Message = "persist.emergency_copy: document seq {Seq} kept in pending"
    )]
    private static partial void LogEmergencyCopy(ILogger logger, long seq);

    [LoggerMessage(
        EventId = 5125,
        Level = LogLevel.Error,
        Message = "persist.emergency_copy_failed: document seq {Seq} ({Code})"
    )]
    private static partial void LogEmergencyCopyFailed(ILogger logger, long seq, string code);

    [LoggerMessage(
        EventId = 5126,
        Level = LogLevel.Information,
        Message = "persist.emergency_copy_kept: the superseded copy could not be removed ({Kind})"
    )]
    private static partial void LogEmergencyCopyKept(ILogger logger, IoFailureKind kind);

    [LoggerMessage(
        EventId = 5127,
        Level = LogLevel.Warning,
        Message = "doc.recovered_from_backup: {Kind}"
    )]
    private static partial void LogFromBackup(ILogger logger, BackupKind kind);

    [LoggerMessage(
        EventId = 5128,
        Level = LogLevel.Error,
        Message = "doc.pre_repair_failed: the original could not be kept ({Code})"
    )]
    private static partial void LogPreRepairFailed(ILogger logger, string code);
}
