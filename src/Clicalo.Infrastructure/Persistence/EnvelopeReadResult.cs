namespace Clicalo.Infrastructure.Persistence;

/// <summary>What reading an envelope found (the first branch of the load chain, blueprint §6.5).</summary>
public abstract record EnvelopeReadResult
{
    private EnvelopeReadResult() { }

    /// <summary>A readable envelope of a supported major.</summary>
    /// <param name="Envelope">The envelope.</param>
    /// <param name="HashMatches">Whether the payload hash matched (a mismatch is logged as <c>doc.edited_externally</c>).</param>
    public sealed record Readable(DocumentEnvelope Envelope, bool HashMatches) : EnvelopeReadResult;

    /// <summary>A greater major than this version supports: read-only, never written.</summary>
    /// <param name="Found">The version found.</param>
    public sealed record FutureMajor(SchemaVersion Found) : EnvelopeReadResult;

    /// <summary>Unreadable or truncated: moved to quarantine, never deleted.</summary>
    /// <param name="Reason">A short code for the log (no content).</param>
    public sealed record Unreadable(string Reason) : EnvelopeReadResult;
}
