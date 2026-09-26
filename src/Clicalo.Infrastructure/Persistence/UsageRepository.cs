using System.Text.Json;
using Clicalo.Application.Ports;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Frequents;
using Clicalo.Infrastructure.Persistence.Dto;
using Clicalo.Infrastructure.Persistence.Mappers;
using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Persistence;

/// <summary><c>usage.json</c> (blueprint §6.5): same envelope and write protocol; never quarantined.</summary>
/// <remarks>
/// Loading takes the newest valid of <c>usage.json</c>, its <c>.tmp</c> and <c>.prev</c>; a missing or unreadable file,
/// or one of another <c>usageEpoch</c> (a «Reset Frequents» interrupted between the two writes), starts empty and logs
/// <c>usage.reset_on_load</c>. Usage is not critical: only a future major blocks saving, so a newer version's file is
/// never overwritten.
/// </remarks>
public sealed partial class UsageRepository : IUsageRepository
{
    private readonly DataLocations _locations;
    private readonly IAtomicFileWriter _writer;
    private readonly TimeProvider _time;
    private readonly ILogger<UsageRepository> _logger;
    private readonly DocumentLoadChain _chain;
    private IReadOnlyDictionary<string, JsonElement>? _extra;
    private long _seq;
    private volatile bool _readOnly;

    /// <summary>Creates the repository.</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="writer">Atomic writes.</param>
    /// <param name="time">Clock of the envelope.</param>
    /// <param name="logger">Logs codes, never content.</param>
    public UsageRepository(
        DataLocations locations,
        IAtomicFileWriter writer,
        TimeProvider time,
        ILogger<UsageRepository> logger
    )
        : this(locations, writer, time, logger, AtomicFile.Disk) { }

    /// <summary>Creates the repository over other file operations (S11).</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="writer">Atomic writes.</param>
    /// <param name="time">Clock of the envelope.</param>
    /// <param name="logger">Logs codes.</param>
    /// <param name="files">The file operations of reads.</param>
    internal UsageRepository(
        DataLocations locations,
        IAtomicFileWriter writer,
        TimeProvider time,
        ILogger<UsageRepository> logger,
        IAtomicFileSystem files
    )
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(files);
        _locations = locations;
        _writer = writer;
        _time = time;
        _logger = logger;
        _chain = new DocumentLoadChain(
            new QuarantineStore(locations, time, files),
            files,
            time,
            logger
        );
    }

    /// <inheritdoc />
    public async Task<UsageHistory> LoadAsync(
        long expectedEpoch,
        CancellationToken cancellationToken
    )
    {
        var chain = await _chain
            .LoadAsync(
                _locations.Usage,
                pending: null,
                DocumentFormats.Usage,
                DocumentFormats.UsageSchema,
                Decode,
                quarantineUnreadable: false,
                cancellationToken
            )
            .ConfigureAwait(false);
        _seq = chain.HighestSeq;
        _readOnly = chain.Source == LoadSource.FutureMajor;
        if (chain.Decoded is not { } usage)
        {
            LogReset(
                _logger,
                chain.MainMissing && !chain.SomethingExisted ? "missing" : "unreadable"
            );
            return UsageHistory.Empty;
        }

        _extra = usage.Extra;
        if (usage.Epoch != expectedEpoch)
        {
            LogReset(_logger, "epoch");
            return UsageHistory.Empty;
        }

        return usage.History;
    }

    /// <inheritdoc />
    public async Task<Result<SaveReceipt>> SaveAsync(
        long usageEpoch,
        UsageHistory usage,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(usage);
        cancellationToken.ThrowIfCancellationRequested();
        if (_readOnly)
        {
            return Results.Fail<SaveReceipt>(PersistenceFailures.ReadOnly());
        }

        var payload = JsonSerializer
            .SerializeToNode(
                new UsagePayloadDto
                {
                    UsageEpoch = usageEpoch,
                    Usage = UsageMapper.Encode(usage),
                    Extra = LibraryMapper.Copy(_extra),
                },
                DocumentJsonContext.Default.UsagePayloadDto
            )!
            .AsObject();
        var seq = _seq + 1;
        var now = _time.GetUtcNow();
        var bytes = EnvelopeCodec.Write(
            new DocumentEnvelope(
                DocumentFormats.Usage,
                DocumentFormats.UsageSchema,
                DocumentFormats.AppVersion,
                seq,
                now,
                string.Empty,
                payload
            )
        );
        if (
            EnvelopeCodec.Read(bytes, DocumentFormats.Usage, DocumentFormats.UsageSchema)
                is not EnvelopeReadResult.Readable { HashMatches: true } readable
            || Decode(readable.Envelope) is null
        )
        {
            return Results.Fail<SaveReceipt>(PersistenceFailures.Invalid());
        }

        var written = await _writer
            .WriteAsync(_locations.Usage, bytes, cancellationToken)
            .ConfigureAwait(false);
        if (written.IsFailure)
        {
            return Results.Fail<SaveReceipt>(written.Failure);
        }

        _seq = seq;
        return Results.Ok(new SaveReceipt(seq, now));
    }

    private static DecodedUsage? Decode(DocumentEnvelope envelope)
    {
        try
        {
            var dto = envelope.Payload.Deserialize(DocumentJsonContext.Default.UsagePayloadDto);
            return dto?.UsageEpoch is { } epoch
                ? new DecodedUsage(epoch, UsageMapper.Decode(dto.Usage), dto.Extra)
                : null;
        }
        catch (Exception ex) when (DocumentCodec.IsInvalidData(ex))
        {
            return null;
        }
    }

    [LoggerMessage(
        EventId = 5131,
        Level = LogLevel.Information,
        Message = "usage.reset_on_load: usage starts empty ({Reason})"
    )]
    private static partial void LogReset(ILogger logger, string reason);

    /// <summary>A readable usage file.</summary>
    private sealed record DecodedUsage(
        long Epoch,
        UsageHistory History,
        IReadOnlyDictionary<string, JsonElement>? Extra
    );
}
