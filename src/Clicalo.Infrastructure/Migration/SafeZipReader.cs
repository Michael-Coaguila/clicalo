using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Errors;

namespace Clicalo.Infrastructure.Migration;

/// <summary>
/// Reads the JSON entries of an untrusted <c>.zip</c> (blueprint §6.6, MIG-009): streaming with counters that abort as
/// soon as a limit is passed, no <c>..</c>, absolute or drive paths, no nested zips followed. Every imported content is
/// untrusted and never executed (AGENTS.md).
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the migration package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class SafeZipReader
{
    /// <summary>Creates a reader.</summary>
    /// <param name="limits">The limits.</param>
    public SafeZipReader(SafeZipLimits limits) => throw new NotImplementedException();

    /// <summary>Reads every <c>.json</c> entry, or fails at the first broken limit or unsafe path.</summary>
    /// <param name="zip">The zip stream, read once.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public Result<ImmutableArray<SafeZipEntry>> ReadJsonEntries(
        Stream zip,
        CancellationToken cancellationToken
    ) => throw new NotImplementedException();
}
