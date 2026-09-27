using System.Collections.Concurrent;
using System.IO;
using Clicalo.App.Composition;
using Clicalo.App.Lifecycle;
using Clicalo.Application.Ports;
using Clicalo.Domain.Errors;
using Clicalo.Infrastructure.Backup;
using Clicalo.Infrastructure.Migration;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.App.Tests;

/// <summary>
/// The start reads the disk off the UI thread (blueprint §3.2: the UI never does I/O; NFR-001): the crash journal, the
/// document and the language files are read on the thread pool, whatever thread asks.
/// </summary>
[Trait("Req", "NFR-001")]
public sealed class StartupReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "clicalo-app-tests",
        Guid.NewGuid().ToString("N")
    );

    private readonly DataLocations _locations;

    public StartupReaderTests()
    {
        _locations = new DataLocations(Path.Combine(_root, "roaming"))
        {
            LocalRoot = Path.Combine(_root, "local"),
        };
        Directory.CreateDirectory(_locations.Root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A handle still open by a failed test; the temporary folder is cleaned by the system.
        }
    }

    [Fact]
    public async Task Nothing_is_read_or_written_on_the_thread_that_starts_the_app()
    {
        var threads = new ConcurrentBag<int>();
        var writer = new ThreadRecordingWriter(
            new AtomicFile(TimeProvider.System, NullLogger<AtomicFile>.Instance),
            threads
        );
        var reader = new StartupReader(
            Documents(writer, threads),
            writer,
            _locations,
            NullLogger<StartupReader>.Instance
        );

        // A dedicated thread plays the UI thread: the thread pool can never run work on it.
        var ui = 0;
        Task<StartupRead>? pending = null;
        var thread = new Thread(() =>
        {
            ui = Environment.CurrentManagedThreadId;
            pending = reader.ReadAsync(
                new StartupRequest(
                    RepoPaths.Data,
                    "es",
                    null,
                    new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero)
                ),
                TestContext.Current.CancellationToken
            );
        });
        thread.Start();
        thread.Join();
        var read = await pending!;

        threads.ShouldNotBeEmpty();
        threads.ShouldNotContain(ui);
        read.Documents.Load.Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);
        read.Localization.Current.Locale.Code.ShouldBe("es");
        File.Exists(AppDataLocations.CrashJournal(_locations)).ShouldBeTrue();
    }

    private StartupDocuments Documents(IAtomicFileWriter writer, ConcurrentBag<int> threads)
    {
        var time = TimeProvider.System;
        var backups = new BackupService(
            _locations,
            writer,
            time,
            NullLogger<BackupService>.Instance
        );
        var repository = new DocumentRepository(
            _locations,
            writer,
            new QuarantineStore(_locations, time),
            backups,
            time,
            NullLogger<DocumentRepository>.Instance
        );
        return new StartupDocuments(
            new ThreadRecordingDocuments(repository, threads),
            new UsageRepository(_locations, writer, time, NullLogger<UsageRepository>.Instance),
            backups,
            new V1Importer(
                new SafeZipReader(SafeZipLimits.Default),
                NullLogger<V1Importer>.Instance
            ),
            writer,
            AppDataLocations.PendingMigration(_locations),
            new RandomIdGenerator(),
            time,
            NullLogger<StartupDocuments>.Instance
        );
    }

    /// <summary>The real repository, recording the thread of every call.</summary>
    private sealed class ThreadRecordingDocuments(
        IDocumentRepository inner,
        ConcurrentBag<int> threads
    ) : IDocumentRepository
    {
        public Task<DocumentLoad> LoadAsync(CancellationToken cancellationToken)
        {
            threads.Add(Environment.CurrentManagedThreadId);
            return inner.LoadAsync(cancellationToken);
        }

        public Task<Result<SaveReceipt>> SaveAsync(
            Domain.Document.UserDocument document,
            CancellationToken cancellationToken
        )
        {
            threads.Add(Environment.CurrentManagedThreadId);
            return inner.SaveAsync(document, cancellationToken);
        }
    }

    /// <summary>The real writer, recording the thread of every write.</summary>
    private sealed class ThreadRecordingWriter(IAtomicFileWriter inner, ConcurrentBag<int> threads)
        : IAtomicFileWriter
    {
        public Task<Result<AtomicWriteReceipt>> WriteAsync(
            string path,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken
        )
        {
            threads.Add(Environment.CurrentManagedThreadId);
            return inner.WriteAsync(path, content, cancellationToken);
        }
    }
}
