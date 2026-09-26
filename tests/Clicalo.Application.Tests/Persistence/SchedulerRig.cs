using Clicalo.Application.Persistence;
using Clicalo.Application.Store;
using Clicalo.Domain.Document;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Application.Tests.Persistence;

/// <summary>
/// The save scheduler running its Persistence loop over in-memory repositories and a fake clock. The clock moves only
/// when the test says so, and after each step the rig waits until the loop has nothing left to do (or is itself
/// waiting on the clock), so every assertion sees a settled state.
/// </summary>
internal sealed class SchedulerRig : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private readonly List<SaveStatus> _statuses = [];

    public SchedulerRig()
    {
        Documents = new FakeDocumentRepository(Time);
        Usage = new FakeUsageRepository(Time);
        Backups = new FakeBackupService(Time);
        Scheduler = new PersistenceScheduler(
            Documents,
            Usage,
            Backups,
            Time,
            NullLogger<PersistenceScheduler>.Instance
        );
        Scheduler.StatusChanged += (_, _) =>
        {
            lock (_statuses)
            {
                _statuses.Add(Scheduler.Status);
            }
        };
        Start = Time.GetUtcNow();
        _loop = Task.Run(() => Scheduler.RunAsync(_stop.Token));
    }

    public FakeTimeProvider Time { get; } = TestTime.CreateProvider();

    public DateTimeOffset Start { get; }

    public TimeSpan Elapsed => Time.GetUtcNow() - Start;

    public FakeDocumentRepository Documents { get; }

    public FakeUsageRepository Usage { get; }

    public FakeBackupService Backups { get; }

    public PersistenceScheduler Scheduler { get; }

    public UserDocument Current { get; private set; } = PersistenceDocuments.Document(0);

    /// <summary>Every status the scheduler published, in order.</summary>
    public IReadOnlyList<SaveStatus> Statuses
    {
        get
        {
            lock (_statuses)
            {
                return [.. _statuses];
            }
        }
    }

    /// <summary>Publishes a change of <paramref name="slices"/> to <paramref name="next"/> (a new version by default).</summary>
    public Task ChangeAsync(DocumentSlices slices, UserDocument? next = null)
    {
        var after =
            next
            ?? PersistenceDocuments.Document(
                (int)Current.Revision + 1,
                Current.Frequents.UsageEpoch
            );
        Scheduler.OnDocumentChanged(
            this,
            new DocumentChangedEventArgs(Current, after, slices, [], ChangeOrigin.Command)
        );
        Current = after;
        return SettleAsync();
    }

    /// <summary>Moves the clock by <paramref name="total"/> in steps of <paramref name="step"/> (10 ms by default).</summary>
    public async Task AdvanceAsync(TimeSpan total, TimeSpan? step = null)
    {
        var size = step ?? TimeSpan.FromMilliseconds(10);
        for (var moved = TimeSpan.Zero; moved < total; moved += size)
        {
            lock (Documents.ClockGate)
            {
                Time.Advance(size < total - moved ? size : total - moved);
            }

            await SettleAsync();
        }
    }

    /// <summary>Moves the clock to <paramref name="elapsed"/> after the start, in steps.</summary>
    public Task AdvanceToAsync(TimeSpan elapsed, TimeSpan? step = null) =>
        AdvanceAsync(elapsed - Elapsed, step);

    /// <summary>Waits until the loop is idle or waiting on the fake clock.</summary>
    public async Task SettleAsync()
    {
        for (var spin = 0; ; spin++)
        {
            if (Scheduler.IsIdle || Documents.IsWaitingOnClock)
            {
                return;
            }

            (spin < 100_000).ShouldBeTrue("The save loop never settled.");
            if (spin < 1000)
            {
                await Task.Yield();
            }
            else
            {
                await Task.Delay(1, TestContext.Current.CancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Documents.LockedUntil = DateTimeOffset.MinValue;
        Documents.Refuse = null;
        await _stop.CancelAsync();
        await _loop;
        Scheduler.Dispose();
        _stop.Dispose();
    }
}
