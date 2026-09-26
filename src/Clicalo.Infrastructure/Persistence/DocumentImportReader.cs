using System.Text.Json.Nodes;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Timing;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// Reads a document to import (COP-002): untrusted content (LOG-006), so it is size-limited, parsed strictly, counted
/// against <c>Timings.Import</c> before anything is decoded, validated by the Domain and never executed. A newer major
/// is refused with a warning (COP-005); texts of another user or machine arrive as unavailable.
/// </summary>
public static class DocumentImportReader
{
    /// <summary>Reads and validates <paramref name="utf8"/>.</summary>
    /// <param name="utf8">The file bytes.</param>
    public static Result<ImportedDocument> Read(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > Timings.Import.ShareMaxBytes)
        {
            return Fail(PersistenceFailures.ImportTooLargeCode);
        }

        switch (EnvelopeCodec.Read(utf8, DocumentFormats.Document, DocumentFormats.DocumentSchema))
        {
            case EnvelopeReadResult.FutureMajor:
                return Fail(PersistenceFailures.SchemaNewerCode);
            case EnvelopeReadResult.Readable readable:
                var payload = readable.Envelope.Payload;
                var (profiles, shortcuts) = Count(payload);
                if (
                    profiles > Timings.Import.ShareMaxProfiles
                    || shortcuts > Timings.Import.ShareMaxShortcuts
                    || Depth(payload) > Timings.Import.ShareMaxDepth
                )
                {
                    return Fail(PersistenceFailures.ImportInvalidCode);
                }

                var decoded = new DocumentCodec().Decode(payload, live: false);
                if (!decoded.TryGetValue(out var document))
                {
                    return Fail(PersistenceFailures.ImportInvalidCode);
                }

                var envelope = readable.Envelope;
                return Results.Ok(
                    new ImportedDocument(
                        document.Document,
                        envelope.Schema,
                        envelope.WrittenBy,
                        envelope.WrittenAtUtc,
                        document.Document.Library.Profiles.Count,
                        document.Document.Library.AlwaysVisible.Count
                            + document.Document.Library.Profiles.Sum(p => p.Shortcuts.Count),
                        document.UnavailableTexts,
                        document.Repairs
                    )
                );
            default:
                return Fail(PersistenceFailures.ImportUnreadableCode);
        }
    }

    /// <summary>Profiles and shortcuts of a payload, counted on the JSON before decoding.</summary>
    /// <param name="payload">A document payload.</param>
    internal static (int Profiles, int Shortcuts) Count(JsonObject payload)
    {
        var profiles = payload["profiles"] as JsonArray;
        var always = ((payload["always"] as JsonObject)?["shortcuts"] as JsonArray)?.Count ?? 0;
        var inProfiles =
            profiles?.Sum(p => ((p as JsonObject)?["shortcuts"] as JsonArray)?.Count ?? 0) ?? 0;
        var shortcuts = always + inProfiles;
        return (profiles?.Count ?? 0, shortcuts);
    }

    /// <summary>The nesting depth of <paramref name="node"/>.</summary>
    /// <param name="node">A JSON node.</param>
    internal static int Depth(JsonNode? node) =>
        node switch
        {
            JsonObject obj => 1 + obj.Select(p => Depth(p.Value)).DefaultIfEmpty(0).Max(),
            JsonArray array => 1 + array.Select(Depth).DefaultIfEmpty(0).Max(),
            _ => 0,
        };

    private static Result<ImportedDocument> Fail(string code) =>
        Results.Fail<ImportedDocument>(PersistenceFailures.Warning(code));
}
