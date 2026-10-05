using System.Text.Json.Nodes;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// A document version reduced to a number, for the byte-level tests of the load chain and S11: the payload is
/// <c>{"n": n}</c> and <c>{"invalid": true}</c> stands for a document the Domain rejects.
/// </summary>
/// <param name="N">The version.</param>
internal sealed record Marker(int N)
{
    /// <summary>The envelope bytes of version <paramref name="n"/> written with <paramref name="seq"/>.</summary>
    public static byte[] Bytes(int n, long seq, string format = DocumentFormats.Document) =>
        EnvelopeCodec.Write(
            new DocumentEnvelope(
                format,
                new SchemaVersion(1, 0),
                "2.0.0",
                seq,
                TestTime.Epoch,
                string.Empty,
                new JsonObject { ["n"] = n }
            )
        );

    /// <summary>The bytes of a readable envelope whose payload is not a valid document.</summary>
    public static byte[] Invalid(long seq) =>
        EnvelopeCodec.Write(
            new DocumentEnvelope(
                DocumentFormats.Document,
                new SchemaVersion(1, 0),
                "2.0.0",
                seq,
                TestTime.Epoch,
                string.Empty,
                new JsonObject { ["invalid"] = true }
            )
        );

    /// <summary>The decoder of the chain.</summary>
    public static Marker? Decode(DocumentEnvelope envelope) =>
        envelope.Payload["n"] is JsonValue value && value.TryGetValue<int>(out var n)
            ? new Marker(n)
            : null;

    /// <summary>Runs the document chain over <paramref name="locations"/> with the real disk.</summary>
    public static Task<LoadChainResult<Marker>> LoadAsync(
        DataLocations locations,
        IAtomicFileSystem? files = null,
        TimeProvider? clock = null
    )
    {
        var disk = files ?? AtomicFile.Disk;
        var time = clock ?? TestTime.CreateProvider();
        return new DocumentLoadChain(
            new QuarantineStore(locations, time, disk),
            disk,
            time,
            NullLogger.Instance
        ).LoadAsync(
            locations.Document,
            locations.PendingDocument,
            DocumentFormats.Document,
            DocumentFormats.DocumentSchema,
            Decode,
            quarantineUnreadable: true,
            TestContext.Current.CancellationToken
        );
    }
}
