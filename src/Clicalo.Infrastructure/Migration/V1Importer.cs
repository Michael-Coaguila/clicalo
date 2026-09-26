using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Migration.V1;
using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Migration;

/// <summary>
/// The v1 import stage (blueprint §6.6): reads a <c>profiles.json</c>, a language backup or the zip backup (through
/// <see cref="SafeZipReader"/>), then <see cref="V1Reader"/> and the pure <see cref="V1Converter"/>. Writes nothing.
/// Criterion: 210 → 210 shortcuts, no empty token (MIG-003, MIG-004).
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the migration package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class V1Importer
{
    /// <summary>Creates the importer.</summary>
    /// <param name="zipReader">Reads the zip backup.</param>
    /// <param name="logger">Logs codes and counts, never labels or combinations.</param>
    public V1Importer(SafeZipReader zipReader, ILogger<V1Importer> logger) =>
        throw new NotImplementedException();

    /// <summary>Reads and converts a v1 file without applying it.</summary>
    /// <param name="path">The file.</param>
    /// <param name="context">Ids, defaults, monitors and clock of the conversion.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public Task<Result<V1ImportPreview>> PreviewAsync(
        string path,
        V1ConversionContext context,
        CancellationToken cancellationToken
    ) => throw new NotImplementedException();
}
