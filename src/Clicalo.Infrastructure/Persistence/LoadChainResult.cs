using System.Collections.Immutable;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>What the load chain found (blueprint §6.5).</summary>
/// <typeparam name="T">What the caller decodes a payload into.</typeparam>
/// <param name="Source">Where the chosen version came from.</param>
/// <param name="Decoded">The decoded chosen version, or <see langword="null"/> when none was usable.</param>
/// <param name="Envelope">Its envelope.</param>
/// <param name="Bytes">Its bytes exactly as read (the <c>pre-repair</c> backup keeps them).</param>
/// <param name="HashMatches">Whether its payload hash matched (a mismatch is <c>doc.edited_externally</c>).</param>
/// <param name="MainUsable">Whether the file itself was readable and valid.</param>
/// <param name="MainLocked">Whether the file itself could not be read because another process held it.</param>
/// <param name="MainMissing">Whether the file itself did not exist.</param>
/// <param name="Future">The version found when it is a future major.</param>
/// <param name="HighestSeq">The greatest <c>seq</c> of every readable envelope met, so the next write goes above it.</param>
/// <param name="Quarantined">Files moved to <c>quarantine\</c>.</param>
/// <param name="SomethingExisted">Whether any of the files existed (otherwise this is a first run).</param>
internal sealed record LoadChainResult<T>(
    LoadSource Source,
    T? Decoded,
    DocumentEnvelope? Envelope,
    byte[]? Bytes,
    bool HashMatches,
    bool MainUsable,
    bool MainLocked,
    bool MainMissing,
    SchemaVersion? Future,
    long HighestSeq,
    ImmutableArray<string> Quarantined,
    bool SomethingExisted
)
    where T : class;
