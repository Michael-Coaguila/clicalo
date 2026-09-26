using System.Collections.Immutable;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.Tests.Persistence;

/// <summary>
/// <c>clicalo.json</c> in memory. A lock «held by another process» until <see cref="LockedUntil"/> behaves like the real
/// writer: it retries at <c>Timings.Persistence.WriteRetryBackoff</c> on the fake clock and fails with
/// <c>persist.io.locked</c> if the lock outlives the backoff.
/// </summary>
internal sealed class FakeDocumentRepository(TimeProvider time) : IDocumentRepository
{
    private readonly List<(DateTimeOffset At, UserDocument Document)> _saves = [];
    private int _waiting;
    private long _dueTicks;

    /// <summary>Until when the file is locked.</summary>
    public DateTimeOffset LockedUntil { get; set; } = DateTimeOffset.MinValue;

    /// <summary>A failure returned at once by every save (read-only, invalid), or <see langword="null"/>.</summary>
    public Failure? Refuse { get; set; }

    /// <summary>Every successful save.</summary>
    public IReadOnlyList<(DateTimeOffset At, UserDocument Document)> Saves
    {
        get
        {
            lock (_saves)
            {
                return [.. _saves];
            }
        }
    }

    /// <summary>Every attempt, successful or not.</summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// Held while a wait is registered and while the test moves the clock, so the due time the test sees is exactly the
    /// one of the pending delay.
    /// </summary>
    public object ClockGate { get; } = new();

    /// <summary>Whether a save is waiting on the fake clock (the test must move it).</summary>
    public bool IsWaitingOnClock =>
        Volatile.Read(ref _waiting) > 0
        && time.GetUtcNow().UtcTicks < Interlocked.Read(ref _dueTicks);

    public Task<DocumentLoad> LoadAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("The scheduler never loads.");

    public async Task<Result<SaveReceipt>> SaveAsync(
        UserDocument document,
        CancellationToken cancellationToken
    )
    {
        Attempts++;
        if (Refuse is { } refused)
        {
            return Results.Fail<SaveReceipt>(refused);
        }

        foreach (var wait in Timings.Persistence.WriteRetryBackoff.Prepend(TimeSpan.Zero))
        {
            if (wait > TimeSpan.Zero)
            {
                Task delay;
                lock (ClockGate)
                {
                    delay = Task.Delay(wait, time, cancellationToken);
                    Interlocked.Exchange(ref _dueTicks, (time.GetUtcNow() + wait).UtcTicks);
                    Interlocked.Increment(ref _waiting);
                }

                try
                {
                    await delay;
                }
                finally
                {
                    Interlocked.Decrement(ref _waiting);
                }
            }

            if (time.GetUtcNow() >= LockedUntil)
            {
                lock (_saves)
                {
                    _saves.Add((time.GetUtcNow(), document));
                    return Results.Ok(new SaveReceipt(_saves.Count, time.GetUtcNow()));
                }
            }
        }

        return Results.Fail<SaveReceipt>(Locked);
    }

    public static Failure Locked { get; } =
        new(
            "persist.io.locked",
            L.TBug,
            FailureSeverity.Critical,
            FailureRecovery.Retry,
            FailureAnnouncement.Assertive
        );

    /// <summary>What the real writer returns at once, without a backoff, when the disk is full.</summary>
    public static Failure DiskFull { get; } =
        new(
            "persist.io.full",
            L.TBug,
            FailureSeverity.Critical,
            FailureRecovery.Retry,
            FailureAnnouncement.Assertive
        );

    public static Failure ReadOnly { get; } =
        new(
            "persist.readonly",
            L.TBug,
            FailureSeverity.Warning,
            FailureRecovery.RestoreBackup,
            FailureAnnouncement.Polite
        );

    public ImmutableArray<DateTimeOffset> SaveTimes => [.. Saves.Select(s => s.At)];
}
