using System.Text.Json.Nodes;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// The envelope of <c>clicalo.json</c>, <c>usage.json</c> and every backup (blueprint §6.5):
/// <code>
/// { "format": "clicalo.document", "schema": { "major": 1, "minor": 0 }, "writtenBy": "2.0.0",
///   "seq": 1842, "writtenAtUtc": "2026-09-25T10:31:02Z", "payloadSha256": "…", "payload": { … } }
/// </code>
/// The hash detects damage, it is not security: a mismatch with a valid payload is accepted and logged.
/// </summary>
/// <param name="Format">The format name (<see cref="DocumentFormats"/>).</param>
/// <param name="Schema">The schema version of the payload.</param>
/// <param name="WrittenBy">The app version that wrote it.</param>
/// <param name="Seq">Raised on every write.</param>
/// <param name="WrittenAtUtc">When it was written.</param>
/// <param name="PayloadSha256">Lower-case hex SHA-256 of the canonical payload bytes.</param>
/// <param name="Payload">The payload, unknown fields included (kept when rewriting, so going back to N−1 loses nothing).</param>
public sealed record DocumentEnvelope(
    string Format,
    SchemaVersion Schema,
    string WrittenBy,
    long Seq,
    DateTimeOffset WrittenAtUtc,
    string PayloadSha256,
    JsonObject Payload
);
