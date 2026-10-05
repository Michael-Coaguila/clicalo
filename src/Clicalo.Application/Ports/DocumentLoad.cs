using System.Collections.Immutable;
using Clicalo.Domain.Document;

namespace Clicalo.Application.Ports;

/// <summary>The document read at start-up and how (blueprint §6.5).</summary>
/// <param name="Document">The document, always valid.</param>
/// <param name="Outcome">How it was obtained.</param>
/// <param name="Seq">The envelope sequence number read (0 when none).</param>
/// <param name="Quarantined">Files moved to <c>quarantine\</c>, never deleted.</param>
/// <param name="IsReadOnly">Whether saving is disabled (future major, or a default not yet accepted).</param>
public sealed record DocumentLoad(
    UserDocument Document,
    DocumentLoadOutcome Outcome,
    long Seq,
    ImmutableArray<string> Quarantined,
    bool IsReadOnly
);
