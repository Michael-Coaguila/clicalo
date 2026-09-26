using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// <c>clicalo.json</c> (blueprint §6.5): envelope, DTOs separated from the Domain, validation on read and write,
/// repair of the repairable, quarantine and the recovery chain <c>.prev</c> → newest valid backup → default in memory.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the persistence package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class DocumentRepository : IDocumentRepository
{
    /// <summary>Creates the repository.</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="writer">Atomic writes.</param>
    /// <param name="quarantine">Where unreadable documents go.</param>
    /// <param name="backups">Recovery from backups and the <c>pre-repair</c> copy.</param>
    /// <param name="time">Clock of the envelope.</param>
    /// <param name="logger">Logs codes, never content.</param>
    public DocumentRepository(
        DataLocations locations,
        IAtomicFileWriter writer,
        QuarantineStore quarantine,
        IBackupService backups,
        TimeProvider time,
        ILogger<DocumentRepository> logger
    ) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<DocumentLoad> LoadAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<SaveReceipt>> SaveAsync(
        UserDocument document,
        CancellationToken cancellationToken
    ) => throw new NotImplementedException();
}
