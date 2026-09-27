using Clicalo.Application.Ports;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Frequents;

namespace Clicalo.Application.Tests.Persistence;

/// <summary>
/// <c>usage.json</c> in memory. While <see cref="Stall"/> is set, a save waits for it (a lock the writer is still
/// waiting on) and gives up when its token is cancelled, like the real writer.
/// </summary>
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

    /// <summary>What a save waits for before it writes, or <see langword="null"/> to write at once.</summary>
    public TaskCompletionSource? Stall { get; set; }

    public IReadOnlyList<DateTimeOffset> SaveTimes() => [.. Saves.Select(s => s.At)];

    public Task<UsageHistory> LoadAsync(long expectedEpoch, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The scheduler never loads.");

    public Task<Result<SaveReceipt>> SaveAsync(
        long usageEpoch,
        UsageHistory usage,
        CancellationToken cancellationToken
    ) =>
        Stall is { } stall
            ? SaveAfterAsync(stall.Task, usageEpoch, usage, cancellationToken)
            : Task.FromResult(Save(usageEpoch, usage));

    private async Task<Result<SaveReceipt>> SaveAfterAsync(
        Task stall,
        long usageEpoch,
        UsageHistory usage,
        CancellationToken cancellationToken
    )
    {
        await stall.WaitAsync(cancellationToken);
        return Save(usageEpoch, usage);
    }

    private Result<SaveReceipt> Save(long usageEpoch, UsageHistory usage)
    {
        lock (_saves)
        {
            _saves.Add((time.GetUtcNow(), usageEpoch, usage));
            return Results.Ok(new SaveReceipt(_saves.Count, time.GetUtcNow()));
        }
    }
}
