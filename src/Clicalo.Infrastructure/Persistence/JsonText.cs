using System.Buffers;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// The JSON text conventions of every persisted file (blueprint §6.5, ADR-0018): UTF-8 without BOM, readable accents,
/// two-space indentation with <c>\n</c> on disk, and a compact deterministic form for the payload hash.
/// </summary>
internal static class JsonText
{
    /// <summary>The deepest nesting accepted in a file this version wrote or reads back.</summary>
    public const int MaxDepth = 64;

    /// <summary>Accents and symbols stay readable in the file; it is never embedded in HTML.</summary>
    public static JavaScriptEncoder Encoder => JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    /// <summary>The on-disk form.</summary>
    public static JsonWriterOptions Indented { get; } =
        new()
        {
            Encoder = Encoder,
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            SkipValidation = false,
        };

    /// <summary>The canonical form hashed in <c>payloadSha256</c>.</summary>
    public static JsonWriterOptions Compact { get; } =
        new() { Encoder = Encoder, Indented = false };

    /// <summary>Strict parsing: no comments, no trailing commas, no duplicate properties.</summary>
    public static JsonDocumentOptions Strict(int maxDepth = MaxDepth) =>
        new()
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            AllowDuplicateProperties = false,
            MaxDepth = maxDepth,
        };

    /// <summary>The UTF-8 byte order mark, tolerated on read and never written.</summary>
    public static ReadOnlySpan<byte> ByteOrderMark => [0xEF, 0xBB, 0xBF];

    /// <summary>Lower-case hexadecimal SHA-256 of the compact serialization of <paramref name="node"/>.</summary>
    /// <param name="node">The payload.</param>
    public static string Sha256(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, Compact))
        {
            node.WriteTo(writer);
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    /// <summary>Removes a leading byte order mark.</summary>
    /// <param name="utf8">File bytes.</param>
    public static ReadOnlySpan<byte> WithoutBom(ReadOnlySpan<byte> utf8) =>
        utf8.StartsWith(ByteOrderMark) ? utf8[ByteOrderMark.Length..] : utf8;
}
