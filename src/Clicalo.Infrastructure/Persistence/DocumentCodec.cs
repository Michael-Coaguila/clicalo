using System.Text.Json;
using System.Text.Json.Nodes;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Infrastructure.Persistence.Dto;
using Clicalo.Infrastructure.Persistence.Mappers;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// The document payload ↔ <see cref="UserDocument"/> (blueprint §6.5, §6.7): DTOs with source generation, repair,
/// Domain validation and DPAPI texts. One instance is shared by <see cref="DocumentRepository"/> and the backup service
/// so both keep what the live document carried and this version does not model: the members of a later minor and the
/// blob of every text that cannot be decrypted here (REG-08).
/// </summary>
public sealed class DocumentCodec
{
    private PreservedFields _preserved = PreservedFields.None;

    /// <summary>What the live document carried that the Domain does not model.</summary>
    internal PreservedFields Preserved => Volatile.Read(ref _preserved);

    /// <summary>Reads a payload; an unreadable or invalid one is a failure, never an exception.</summary>
    /// <param name="payload">The envelope payload.</param>
    /// <param name="live">Whether this is the live document, whose preserved members later writes keep.</param>
    internal Result<DecodedPayload> Decode(JsonObject payload, bool live)
    {
        ArgumentNullException.ThrowIfNull(payload);
        try
        {
            var dto =
                payload.Deserialize(DocumentJsonContext.Default.PayloadDto)
                ?? throw new InvalidDataException("payload");
            var (repaired, repairs) = DocumentRepair.Apply(dto);
            var decoded = DocumentMapper.Decode(repaired, repairs);
            if (live)
            {
                Volatile.Write(ref _preserved, decoded.Preserved);
            }

            return Results.Ok(decoded);
        }
        catch (Exception ex) when (IsInvalidData(ex))
        {
            return Results.Fail<DecodedPayload>(PersistenceFailures.Invalid());
        }
    }

    /// <summary>Keeps what the live document carried, from now on.</summary>
    /// <param name="preserved">The preserved members of the document just loaded.</param>
    internal void Remember(PreservedFields preserved)
    {
        ArgumentNullException.ThrowIfNull(preserved);
        Volatile.Write(ref _preserved, preserved);
    }

    /// <summary>The payload of <paramref name="document"/>, texts encrypted, with the preserved members.</summary>
    /// <param name="document">The document.</param>
    /// <param name="includeUsage">Whether the usage goes in the payload (backups).</param>
    internal JsonObject Encode(UserDocument document, bool includeUsage)
    {
        var dto = DocumentMapper.Encode(document, Preserved, includeUsage);
        return JsonSerializer
            .SerializeToNode(dto, DocumentJsonContext.Default.PayloadDto)!
            .AsObject();
    }

    /// <summary>Whether <paramref name="exception"/> means «this data is not a valid document» (not a defect).</summary>
    /// <param name="exception">Any exception.</param>
    internal static bool IsInvalidData(Exception exception) =>
        exception
            is JsonException
                or InvalidDataException
                or ArgumentException
                or FormatException
                or OverflowException;
}
