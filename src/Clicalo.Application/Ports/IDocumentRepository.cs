using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;

namespace Clicalo.Application.Ports;

/// <summary>
/// <c>clicalo.json</c> (blueprint §6.5, ADR-0007): the versioned envelope, validation on read and on write, quarantine
/// and recovery. Used only by the Persistence thread through the save scheduler.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>Loads the document following the recovery chain; never throws for bad data and never overwrites it.</summary>
    /// <param name="cancellationToken">Cancels start-up.</param>
    Task<DocumentLoad> LoadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Serializes, validates the bytes by reading them back, and writes atomically. An invalid document is never saved:
    /// the last valid one is kept and the failure is returned.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels before the write starts.</param>
    Task<Result<SaveReceipt>> SaveAsync(UserDocument document, CancellationToken cancellationToken);
}
