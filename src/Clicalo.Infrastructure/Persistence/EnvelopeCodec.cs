using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// Reads and writes the envelope (blueprint §6.5, ADR-0018), culture-invariant and deterministic. The payload hash is
/// the SHA-256 of its compact serialization, so it detects damage whatever the indentation of the file; it is not a
/// security measure.
/// </summary>
public static class EnvelopeCodec
{
    private const string WrittenAtFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>Reads an envelope of <paramref name="format"/> that this version supports up to <paramref name="supported"/>.</summary>
    /// <param name="utf8">The file bytes (a BOM is tolerated).</param>
    /// <param name="format">The expected format name.</param>
    /// <param name="supported">The greatest schema this version reads.</param>
    public static EnvelopeReadResult Read(
        ReadOnlySpan<byte> utf8,
        string format,
        SchemaVersion supported
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(format);
        var text = JsonText.WithoutBom(utf8);
        if (!Utf8.IsValid(text))
        {
            // A damaged byte (bit rot, a torn sector) is not valid UTF-8: System.Text.Json only notices when a string
            // is read, and then it throws InvalidOperationException instead of JsonException (DAT-003).
            return new EnvelopeReadResult.Unreadable("utf8");
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(
                text,
                nodeOptions: null,
                documentOptions: JsonText.Strict()
            );
        }
        catch (JsonException)
        {
            return new EnvelopeReadResult.Unreadable("json");
        }

        if (root is not JsonObject envelope)
        {
            return new EnvelopeReadResult.Unreadable("shape");
        }

        if (
            !TryString(envelope, "format", out var found)
            || !string.Equals(found, format, StringComparison.Ordinal)
        )
        {
            return new EnvelopeReadResult.Unreadable("format");
        }

        if (!TrySchema(envelope, out var schema))
        {
            return new EnvelopeReadResult.Unreadable("schema");
        }

        if (schema.Major > supported.Major)
        {
            return new EnvelopeReadResult.FutureMajor(schema);
        }

        if (
            !TryString(envelope, "writtenBy", out var writtenBy)
            || !TryLong(envelope, "seq", out var seq)
            || seq < 0
            || !TryString(envelope, "writtenAtUtc", out var writtenAtText)
            || !DateTimeOffset.TryParse(
                writtenAtText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var writtenAt
            )
            || !TryString(envelope, "payloadSha256", out var hash)
        )
        {
            return new EnvelopeReadResult.Unreadable("fields");
        }

        if (envelope["payload"] is not JsonObject payload)
        {
            return new EnvelopeReadResult.Unreadable("payload");
        }

        _ = envelope.Remove("payload");
        var matches = string.Equals(
            JsonText.Sha256(payload),
            hash,
            StringComparison.OrdinalIgnoreCase
        );
        return new EnvelopeReadResult.Readable(
            new DocumentEnvelope(format, schema, writtenBy, seq, writtenAt, hash, payload),
            matches
        );
    }

    /// <summary>Writes an envelope as UTF-8 without BOM, with the hash of its payload.</summary>
    /// <param name="envelope">The envelope; its hash is recomputed.</param>
    public static byte[] Write(DocumentEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, JsonText.Indented))
        {
            writer.WriteStartObject();
            writer.WriteString("format", envelope.Format);
            writer.WriteStartObject("schema");
            writer.WriteNumber("major", envelope.Schema.Major);
            writer.WriteNumber("minor", envelope.Schema.Minor);
            writer.WriteEndObject();
            writer.WriteString("writtenBy", envelope.WrittenBy);
            writer.WriteNumber("seq", envelope.Seq);
            writer.WriteString(
                "writtenAtUtc",
                envelope.WrittenAtUtc.UtcDateTime.ToString(
                    WrittenAtFormat,
                    CultureInfo.InvariantCulture
                )
            );
            writer.WriteString("payloadSha256", JsonText.Sha256(envelope.Payload));
            writer.WritePropertyName("payload");
            envelope.Payload.WriteTo(writer);
            writer.WriteEndObject();
        }

        var bytes = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(bytes);
        bytes[^1] = (byte)'\n';
        return bytes;
    }

    private static bool TryString(JsonObject node, string name, out string value)
    {
        if (
            node[name] is JsonValue json
            && json.GetValueKind() == JsonValueKind.String
            && json.TryGetValue<string>(out var text)
        )
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryLong(JsonObject node, string name, out long value)
    {
        value = 0;
        return node[name] is JsonValue json
            && json.GetValueKind() == JsonValueKind.Number
            && json.TryGetValue(out value);
    }

    private static bool TrySchema(JsonObject envelope, out SchemaVersion schema)
    {
        schema = default;
        if (
            envelope["schema"] is not JsonObject version
            || !TryLong(version, "major", out var major)
            || !TryLong(version, "minor", out var minor)
            || major is < 0 or > int.MaxValue
            || minor is < 0 or > int.MaxValue
        )
        {
            return false;
        }

        schema = new SchemaVersion((int)major, (int)minor);
        return true;
    }
}
