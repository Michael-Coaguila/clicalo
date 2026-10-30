using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Clicalo.Application.Persistence;
using Clicalo.Application.Ports;
using Clicalo.Application.Store;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// S11 · hostile persistence, lock half, end to end: the save scheduler and the real atomic writer against a real
/// <c>clicalo.json</c> that «another process» (a handle that shares nothing, as an antivirus, the indexer or a locking
/// monitor holds it) keeps locked. A short lock is absorbed silently; a lock longer than 3 s is visible by then; nothing
/// is lost and the save completes once the lock is gone (docs/testing/spikes/S11.md).
/// </summary>
/// <remarks>
/// The clock is fake and the file operations are real. The clock moves only while the scheduler has nothing in flight
/// or the writer is waiting on the clock between two attempts (<see cref="ClockWaiters"/>), never while a file
/// operation is running: the fake time a save takes is then exactly the writer's backoff, whatever the load of the
/// machine. Before, the clock ran ahead of a slow disk and the notice of a long lock looked late (issue 4: 5,24 s of
/// fake time on a shared runner for a notice that is due 3 s after the first attempt).
/// </remarks>
[Trait("Req", "DAT-002")]
[Trait("Req", "NFR-006")]
public sealed class S11LockTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly FakeTimeProvider _time = TestTime.CreateProvider();
    private readonly BoundedTestToken _bounded = new();

    public void Dispose()
    {
        _bounded.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public async Task A_one_second_lock_is_absorbed_without_an_error()
    {
        await using var run = await StartAsync();
        var holder = run.Lock();

        run.Change(1);
        await run.AdvanceAsync(
            TimeSpan.FromSeconds(10),
            elapsed =>
            {
                if (elapsed < TimeSpan.FromSeconds(1.5))
                {
                    return false;
                }

                holder.Dispose();
                return true;
            }
        );

        holder.Dispose();
        run.Statuses.ShouldNotContain(SaveStatus.Failing);
        run.Scheduler.Status.ShouldBe(SaveStatus.Saved);
        File.ReadAllText(_folder.Locations.Document).ShouldBe("1");
    }

    [Fact]
    [Trait("Req", "REG-08")]
    public async Task A_ten_second_lock_is_visible_within_3_s_and_the_save_completes_afterwards()
    {
        await using var run = await StartAsync();
        var holder = run.Lock();
        TimeSpan? visibleAt = null;

        run.Change(2);
        await run.AdvanceAsync(
            TimeSpan.FromSeconds(60),
            elapsed =>
            {
                if (visibleAt is null && run.Scheduler.Status == SaveStatus.Failing)
                {
                    visibleAt = elapsed;
                }

                if (elapsed < TimeSpan.FromSeconds(10))
                {
                    return false;
                }

                holder.Dispose();
                return true;
            }
        );

        holder.Dispose();
        visibleAt.ShouldBe(
            Timings().DocumentSaveDebounce + Timings().UnsavedNoticeAfter,
            "3 s after the first attempt, which starts when the debounce ends"
        );
        run.Scheduler.Status.ShouldBe(SaveStatus.Saved);
        File.ReadAllText(_folder.Locations.Document).ShouldBe("2");
        File.ReadAllText(_folder.Locations.DocumentPrevious).ShouldBe("0");
    }

    private static (TimeSpan DocumentSaveDebounce, TimeSpan UnsavedNoticeAfter) Timings() =>
        (
            Domain.Timing.Timings.Persistence.DocumentSaveDebounce,
            Domain.Timing.Timings.Persistence.UnsavedNoticeAfter
        );

    private async Task<Run> StartAsync()
    {
        var clock = new ClockWaiters(_time);
        var writer = new AtomicFile(clock, NullLogger<AtomicFile>.Instance);
        (
            await writer.WriteAsync(_folder.Locations.Document, "0"u8.ToArray(), _bounded.Token)
        ).IsSuccess.ShouldBeTrue();
        return new Run(_time, clock, _folder.Locations.Document, writer, _bounded.Token);
    }

    /// <summary>The scheduler loop over a repository that writes the document version as its bytes.</summary>
    private sealed class Run : IAsyncDisposable
    {
        private readonly FakeTimeProvider _time;
        private readonly ClockWaiters _clock;
        private readonly CancellationToken _limit;
        private readonly string _path;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;
        private readonly List<SaveStatus> _statuses = [];
        private UserDocument _current = TestDocuments.Document(0) with { Revision = 0 };

        public Run(
            FakeTimeProvider time,
            ClockWaiters clock,
            string path,
            IAtomicFileWriter writer,
            CancellationToken limit
        )
        {
            _time = time;
            _clock = clock;
            _limit = limit;
            _path = path;
            Scheduler = new PersistenceScheduler(
                new BytesRepository(path, writer),
                new NoUsage(),
                new NoBackups(),
                clock,
                NullLogger<PersistenceScheduler>.Instance
            );
            Scheduler.StatusChanged += (_, _) =>
            {
                lock (_statuses)
                {
                    _statuses.Add(Scheduler.Status);
                }
            };
            // The loop ends with _stop, not with the limit of the test: DisposeAsync awaits its final flush.
            _loop = Task.Run(() => Scheduler.RunAsync(_stop.Token), CancellationToken.None);
        }

        public PersistenceScheduler Scheduler { get; }

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

        public FileStream Lock() => new(_path, FileMode.Open, FileAccess.Read, FileShare.None);

        public void Change(int version)
        {
            var next = _current with { Revision = version };
            Scheduler.OnDocumentChanged(
                this,
                new DocumentChangedEventArgs(
                    _current,
                    next,
                    DocumentSlices.Library,
                    [],
                    ChangeOrigin.Command
                )
            );
            _current = next;
        }

        /// <summary>
        /// Moves the clock in 10 ms steps, each one only once the file operations in flight are done, until
        /// <paramref name="total"/> or until the document is saved after <paramref name="onStep"/> released the lock.
        /// </summary>
        public async Task AdvanceAsync(TimeSpan total, Func<TimeSpan, bool> onStep)
        {
            var step = TimeSpan.FromMilliseconds(10);
            var released = false;
            for (var elapsed = step; elapsed <= total; elapsed += step)
            {
                await SettleAsync();
                if (released && IsSaved())
                {
                    return;
                }

                _time.Advance(step);
                released |= onStep(elapsed);
            }

            await SettleAsync();
        }

        private bool IsSaved() =>
            Scheduler.Status == SaveStatus.Saved && File.Exists(_path + ".prev");

        /// <summary>
        /// Waits, in real time, until nothing is left to do before the clock moves: the scheduler has no signal queued
        /// or in process, or its writer is waiting on the clock between two attempts.
        /// </summary>
        private async Task SettleAsync()
        {
            for (var spin = 0; !(IsIdle(Scheduler) || _clock.Any); spin++)
            {
                if (spin < 1000)
                {
                    await Task.Yield();
                }
                else
                {
                    // A file operation that takes long: the limit of the test (real time) ends a wait that never does.
                    await Task.Delay(1, _limit);
                }
            }
        }

        /// <summary>
        /// <c>PersistenceScheduler.IsIdle</c>, which the scheduler keeps internal for the tests that drive a fake clock
        /// (Application.Tests reads it directly; this project sees only its public API).
        /// </summary>
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_IsIdle")]
        private static extern bool IsIdle(PersistenceScheduler scheduler);

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            await _loop;
            Scheduler.Dispose();
            _stop.Dispose();
        }
    }

    /// <summary>Writes the document version as the whole file, through the real atomic writer.</summary>
    private sealed class BytesRepository(string path, IAtomicFileWriter writer)
        : IDocumentRepository
    {
        public Task<DocumentLoad> LoadAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task<Result<SaveReceipt>> SaveAsync(
            UserDocument document,
            CancellationToken cancellationToken
        )
        {
            var bytes = Encoding.UTF8.GetBytes(
                document.Revision.ToString(CultureInfo.InvariantCulture)
            );
            var written = await writer.WriteAsync(path, bytes, cancellationToken);
            return written.Map(_ => new SaveReceipt(document.Revision, DateTimeOffset.UnixEpoch));
        }
    }

    private sealed class NoUsage : IUsageRepository
    {
        public Task<Domain.Frequents.UsageHistory> LoadAsync(
            long expectedEpoch,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public Task<Result<SaveReceipt>> SaveAsync(
            long usageEpoch,
            Domain.Frequents.UsageHistory usage,
            CancellationToken cancellationToken
        ) => Task.FromResult(Results.Ok(new SaveReceipt(0, DateTimeOffset.UnixEpoch)));
    }

    private sealed class NoBackups : IBackupService
    {
        public void SnapshotNow(UserDocument document, BackupKind kind) { }

        public Task<Result<int>> WriteSnapshotsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Results.Ok(0));

        public Task<Result<BackupInfo>> CreateAsync(
            UserDocument document,
            BackupKind kind,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                Results.Ok(new BackupInfo(new BackupId("x"), kind, DateTimeOffset.UnixEpoch, 0, 0))
            );

        public Task<System.Collections.Immutable.ImmutableArray<BackupInfo>> ListAsync(
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public Task<Result<UserDocument>> ReadAsync(
            BackupId id,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();
    }
}
