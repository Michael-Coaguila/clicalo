using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>Reads and writes the envelope (blueprint §6.5), culture-invariant and deterministic.</summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the persistence package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public static class EnvelopeCodec
{
    /// <summary>Reads an envelope of <paramref name="format"/> that this version supports up to <paramref name="supported"/>.</summary>
    /// <param name="utf8">The file bytes (a BOM is tolerated).</param>
    /// <param name="format">The expected format name.</param>
    /// <param name="supported">The greatest schema this version reads.</param>
    public static EnvelopeReadResult Read(
        ReadOnlySpan<byte> utf8,
        string format,
        SchemaVersion supported
    ) => throw new NotImplementedException();

    /// <summary>Writes an envelope as UTF-8 without BOM, with the hash of its payload.</summary>
    /// <param name="envelope">The envelope; its hash is recomputed.</param>
    public static byte[] Write(DocumentEnvelope envelope) => throw new NotImplementedException();
}
