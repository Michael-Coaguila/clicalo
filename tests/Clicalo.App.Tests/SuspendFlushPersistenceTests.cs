using System.IO;
using Clicalo.App.Lifecycle;
using Clicalo.App.Shutdown;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Persistence;
using Clicalo.Application.Store;
using Clicalo.Domain.Document;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Backup;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.App.Tests;

/// <summary>
/// The suspend of the running instance against a real temporary data folder (blueprint §6.4, §7.6): what the host
/// hands to <c>PBT_APMSUSPEND</c> releases through the engine's observer and then writes the pending document and
/// usage through the single Persistence consumer, before the message is answered.
/// </summary>
[Trait("Req", "DAT-002")]
[Trait("Req", "REG-08")]
public sealed class SuspendFlushPersistenceTests : IDisposable
{
    private static readonly string ContentFolder = RepoPaths.Combine("data", "content");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "clicalo-app-tests",
        Guid.NewGuid().ToString("N")
    );

    private readonly DataLocations _locations;
    private readonly AtomicFile _writer = new(TimeProvider.System, NullLogger<AtomicFile>.Instance);

    public SuspendFlushPersistenceTests()
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
    public async Task The_suspend_writes_the_pending_document_and_usage_before_it_is_answered()
    {
        var token = TestContext.Current.CancellationToken;
        var start = await Start().LoadAsync(ContentFolder, LangCode.Es, token);
        var before = start.Load.Document;
        var after = before with
        {
            Revision = before.Revision + 1,
            Onboarding = new OnboardingState(Completed: true),
            Frequents = before.Frequents with
            {
                Usage = before.Frequents.Usage.Record(
                    new ShortcutId("copy"),
                    DateTimeOffset.UnixEpoch,
                    TimeSpan.FromDays(30)
                ),
            },
        };
        using var scheduler = new PersistenceScheduler(
            Documents(),
            Usage(),
            Backups(),
            TimeProvider.System,
            NullLogger<PersistenceScheduler>.Instance
        );
        scheduler.OnDocumentChanged(
            this,
            new DocumentChangedEventArgs(
                before,
                after,
                DocumentSlices.Onboarding | DocumentSlices.FrequentsUsage,
                [],
                ChangeOrigin.Command
            )
        );

        // Nothing is held: the release is confirmed at once, and no autosave loop runs, so only the suspend writes.
        SuspendFlush
            .Run(new EngineObserverRelay(static _ => { }), scheduler, TimeProvider.System)
            .ShouldBeTrue();

        (await Documents().LoadAsync(token)).Document.Onboarding.Completed.ShouldBeTrue();
        (await Usage().LoadAsync(after.Frequents.UsageEpoch, token)).Entries.ShouldContainKey(
            new ShortcutId("copy")
        );
        scheduler.Status.ShouldBe(SaveStatus.Saved);
    }

    private DocumentRepository Documents() =>
        new(
            _locations,
            _writer,
            new QuarantineStore(_locations, TimeProvider.System),
            Backups(),
            TimeProvider.System,
            NullLogger<DocumentRepository>.Instance,
            new DocumentCodec()
        );

    private UsageRepository Usage() =>
        new(_locations, _writer, TimeProvider.System, NullLogger<UsageRepository>.Instance);

    private BackupService Backups() =>
        new(_locations, _writer, TimeProvider.System, NullLogger<BackupService>.Instance);

    private StartupDocuments Start() =>
        new(Documents(), Usage(), TimeProvider.System, NullLogger<StartupDocuments>.Instance);
}
