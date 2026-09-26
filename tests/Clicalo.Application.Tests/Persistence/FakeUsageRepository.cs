using Clicalo.Application.Ports;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Frequents;

namespace Clicalo.Application.Tests.Persistence;

/// <summary><c>usage.json</c> in memory.</summary>
internal sealed class FakeUsageRepository(TimeProvider time) : IUsageRepository
{
    private readonly List<(DateTimeOffset At, long Epoch, UsageHistory Usage)> _saves = [];

    public IReadOnlyList<(DateTimeOffset At, long Epoch, UsageHistory Usage)> Saves
    {
        get
        {
            lock (_saves)
            {
                return [.. _saves];
            }
        }
    }

    public IReadOnlyList<DateTimeOffset> SaveTimes() => [.. Saves.Select(s => s.At)];

    public Task<UsageHistory> LoadAsync(long expectedEpoch, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The scheduler never loads.");

    public Task<Result<SaveReceipt>> SaveAsync(
        long usageEpoch,
        UsageHistory usage,
        CancellationToken cancellationToken
    )
    {
        lock (_saves)
        {
            _saves.Add((time.GetUtcNow(), usageEpoch, usage));
            return Task.FromResult(Results.Ok(new SaveReceipt(_saves.Count, time.GetUtcNow())));
        }
    }
}
