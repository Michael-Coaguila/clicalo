using Clicalo.Domain.Errors;
using Clicalo.Domain.Frequents;

namespace Clicalo.Application.Ports;

/// <summary>
/// <c>usage.json</c> (blueprint §6.5): the only frequent writes, kept out of the document. A missing or unreadable
/// file, or one with another <c>usageEpoch</c>, starts empty (<c>usage.reset_on_load</c>) and is never quarantined.
/// </summary>
public interface IUsageRepository
{
    /// <summary>Loads the usage of <paramref name="expectedEpoch"/>, or empty.</summary>
    /// <param name="expectedEpoch">The document's <c>usageEpoch</c>.</param>
    /// <param name="cancellationToken">Cancels start-up.</param>
    Task<UsageHistory> LoadAsync(long expectedEpoch, CancellationToken cancellationToken);

    /// <summary>Writes the usage atomically.</summary>
    /// <param name="usageEpoch">The document's <c>usageEpoch</c>.</param>
    /// <param name="usage">The usage.</param>
    /// <param name="cancellationToken">Cancels before the write starts.</param>
    Task<Result<SaveReceipt>> SaveAsync(
        long usageEpoch,
        UsageHistory usage,
        CancellationToken cancellationToken
    );
}
