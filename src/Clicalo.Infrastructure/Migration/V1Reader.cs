using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Migration.V1;

namespace Clicalo.Infrastructure.Migration;

/// <summary>
/// Reads a v1 <c>profiles.json</c> into <see cref="V1Document"/> (blueprint §6.6, MIG-002): tolerates unknown keys,
/// a BOM, missing keys and truncated files as far as they parse; never executes anything.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the migration package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public static class V1Reader
{
    /// <summary>Reads the file bytes.</summary>
    /// <param name="utf8">The bytes exactly as on disk.</param>
    public static Result<V1Document> Read(ReadOnlySpan<byte> utf8) =>
        throw new NotImplementedException();
}
