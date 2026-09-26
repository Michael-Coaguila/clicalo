using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Frequents;
using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Persistence;

/// <summary><c>usage.json</c> (blueprint §6.5): same envelope and write protocol; never quarantined.</summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the persistence package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class UsageRepository : IUsageRepository
{
    /// <summary>Creates the repository.</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="writer">Atomic writes.</param>
    /// <param name="time">Clock of the envelope.</param>
    /// <param name="logger">Logs codes, never content.</param>
    public UsageRepository(
        DataLocations locations,
        IAtomicFileWriter writer,
        TimeProvider time,
        ILogger<UsageRepository> logger
    ) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<UsageHistory> LoadAsync(long expectedEpoch, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<SaveReceipt>> SaveAsync(
        long usageEpoch,
        UsageHistory usage,
        CancellationToken cancellationToken
    ) => throw new NotImplementedException();
}
