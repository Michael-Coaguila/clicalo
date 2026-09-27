using System.IO;
using Clicalo.App.Composition;
using Clicalo.App.Lifecycle;
using Clicalo.Application.Ports;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Backup;
using Clicalo.Infrastructure.Migration;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.App.Tests;

/// <summary>
/// The document a start hands to the store, against a real temporary data folder (blueprint §6.5, §6.6): a new
/// installation gets the seed in the language of Windows, or the user's v1 file converted over it with the original
/// kept first (EC-MIG-01, MIG-004), written at once so it never repeats; a failed migration writes nothing.
/// </summary>
public sealed class StartupDocumentsTests : IDisposable
{
    private static readonly string ContentFolder = RepoPaths.Combine("data", "content");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "clicalo-app-tests",
        Guid.NewGuid().ToString("N")
    );

    private readonly DataLocations _locations;

    public StartupDocumentsTests()
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
    [Trait("Req", "CAT-003")]
    public async Task A_new_installation_gets_the_seed_in_the_language_of_windows_and_keeps_it()
    {
        var load = await LoadAsync(ContentFolder, LangCode.En, null, Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);
        load.Document.Settings.Language.ShouldBe(LangCode.En);
        var library = load.Document.Library;
        library.AlwaysVisible.Count.ShouldBe(4);
        library.Profiles.ShouldHaveSingleItem().Id.ShouldBe(ProfileId.General);
        var copy = library.General.Shortcuts[0];
        copy.Id.ShouldBe(new ShortcutId("copy"));
        copy.Action.ShouldBe(
            new TapAction(
                KeyChord.Create([new KeyStroke(KeyIds.Ctrl), new KeyStroke(KeyIds.C)]),
                []
            )
        );

        var again = await LoadAsync(ContentFolder, LangCode.Es, null, Token);

        again.Outcome.ShouldBe(DocumentLoadOutcome.Loaded, "the seed was written at once");
        again.Document.Library.ShouldBe(library);
        again.Document.Settings.Language.ShouldBe(
            LangCode.En,
            "only a new installation follows Windows"
        );
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    [Trait("Req", "MIG-001")]
    public async Task The_v1_file_in_use_migrates_210_to_210_once_with_its_original_kept()
    {
        var v1 = Fixture("profiles.dist-app.json");

        var load = await LoadAsync(ContentFolder, LangCode.Es, v1, Token);

        var library = load.Document.Library;
        library.Profiles.Count.ShouldBe(14);
        library.Profiles.Sum(static p => p.Shortcuts.Count).ShouldBe(210);
        load.Document.Validate().ShouldBeEmpty();
        Directory
            .GetFiles(_locations.Backups, "v1-original-*")
            .ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(path =>
                File.ReadAllBytes(path).ShouldBe(File.ReadAllBytes(v1))
            );

        var again = await LoadAsync(ContentFolder, LangCode.Es, v1, Token);

        again.Outcome.ShouldBe(
            DocumentLoadOutcome.Loaded,
            "a migrated document is never migrated again"
        );
        again.Document.Library.ShouldBe(library);
        Directory.GetFiles(_locations.Backups, "v1-original-*").Length.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public async Task A_damaged_v1_file_starts_with_the_seed_and_writes_no_document()
    {
        var damaged = Path.Combine(_root, "profiles.json");
        await File.WriteAllTextAsync(damaged, """{"profiles": [ {"name": "Gen""", Token);

        var load = await LoadAsync(ContentFolder, LangCode.Es, damaged, Token);

        load.Document.Library.AlwaysVisible.Count.ShouldBe(4, "the example data");
        File.Exists(_locations.Document).ShouldBeFalse("so the migration can be tried again");
        var again = await LoadAsync(ContentFolder, LangCode.Es, null, Token);
        again.Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);
    }

    [Fact]
    public async Task Without_a_seed_a_new_installation_still_starts_with_general()
    {
        var load = await LoadAsync(null, null, null, Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);
        load.Document.Library.Profiles.ShouldHaveSingleItem().Id.ShouldBe(ProfileId.General);
        load.Document.Validate().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    [Trait("Req", "REG-08")]
    public async Task A_failed_migration_is_tried_again_after_the_seed_was_saved_and_the_seed_is_kept_as_pre_migrate()
    {
        var damaged = Path.Combine(_root, "profiles.json");
        await File.WriteAllTextAsync(damaged, """{"profiles": [ {"name": "Gen""", Token);
        var failed = await LoadAsync(ContentFolder, LangCode.Es, damaged, Token);
        File.Exists(PendingMigration).ShouldBeTrue("the failure is remembered");

        // The first change of that session writes the seed: the next start is no longer a new installation.
        (await Repository().SaveAsync(failed.Document, Token)).IsSuccess.ShouldBeTrue();
        File.Copy(Fixture("profiles.dist-app.json"), damaged, overwrite: true);
        var retried = await Start().LoadAsync(ContentFolder, LangCode.Es, damaged, Token);

        retried.Load.Document.Library.Profiles.Sum(static p => p.Shortcuts.Count).ShouldBe(210);
        retried.SavePending.ShouldBeFalse();
        File.Exists(PendingMigration).ShouldBeFalse();
        Directory
            .GetFiles(Path.Combine(_locations.Backups, "pre-migrate"))
            .ShouldHaveSingleItem("the document it replaced is kept first");
        var again = await LoadAsync(ContentFolder, LangCode.Es, damaged, Token);
        again.Outcome.ShouldBe(DocumentLoadOutcome.Loaded);
        again.Document.Library.ShouldBe(retried.Load.Document.Library, "never migrated twice");
        Directory.GetFiles(Path.Combine(_locations.Backups, "pre-migrate")).Length.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public async Task Without_a_failed_migration_an_existing_document_is_never_migrated_again()
    {
        _ = await LoadAsync(ContentFolder, LangCode.Es, null, Token);

        var load = await Start()
            .LoadAsync(ContentFolder, LangCode.Es, Fixture("profiles.dist-app.json"), Token);

        load.Load.Outcome.ShouldBe(DocumentLoadOutcome.Loaded);
        load.Load.Document.Library.AlwaysVisible.Count.ShouldBe(4, "still the seed");
        Directory.Exists(Path.Combine(_locations.Backups, "pre-migrate")).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "DAT-002")]
    public async Task A_new_installation_that_cannot_be_written_is_handed_to_the_autosave()
    {
        var load = await Start(new UnwritableDocuments(Repository()))
            .LoadAsync(ContentFolder, LangCode.Es, null, Token);

        load.Load.Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);
        load.SavePending.ShouldBeTrue();
        load.Load.Document.Library.AlwaysVisible.Count.ShouldBe(4);
    }

    [Fact]
    [Trait("Req", "DAT-002")]
    public async Task A_new_installation_written_at_once_has_nothing_pending() =>
        (
            await Start().LoadAsync(ContentFolder, LangCode.Es, null, Token)
        ).SavePending.ShouldBeFalse();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string PendingMigration => AppDataLocations.PendingMigration(_locations);

    private async Task<DocumentLoad> LoadAsync(
        string? contentFolder,
        LangCode? language,
        string? migrateV1,
        CancellationToken cancellationToken
    ) => (await Start().LoadAsync(contentFolder, language, migrateV1, cancellationToken)).Load;

    private static string Fixture(string name) =>
        RepoPaths.Combine("tests", "Clicalo.Infrastructure.Tests", "Fixtures", "v1", name);

    private DocumentRepository Repository()
    {
        var time = TimeProvider.System;
        var writer = new AtomicFile(time, NullLogger<AtomicFile>.Instance);
        return new DocumentRepository(
            _locations,
            writer,
            new QuarantineStore(_locations, time),
            Backups(writer),
            time,
            NullLogger<DocumentRepository>.Instance,
            new DocumentCodec()
        );
    }

    private BackupService Backups(IAtomicFileWriter writer) =>
        new(_locations, writer, TimeProvider.System, NullLogger<BackupService>.Instance);

    private StartupDocuments Start(IDocumentRepository? documents = null)
    {
        var time = TimeProvider.System;
        var writer = new AtomicFile(time, NullLogger<AtomicFile>.Instance);
        return new StartupDocuments(
            documents ?? Repository(),
            new UsageRepository(_locations, writer, time, NullLogger<UsageRepository>.Instance),
            Backups(writer),
            new V1Importer(
                new SafeZipReader(SafeZipLimits.Default),
                NullLogger<V1Importer>.Instance
            ),
            writer,
            PendingMigration,
            new RandomIdGenerator(),
            time,
            NullLogger<StartupDocuments>.Instance
        );
    }

    /// <summary>The real repository, whose writes all fail like a full disk.</summary>
    private sealed class UnwritableDocuments(IDocumentRepository inner) : IDocumentRepository
    {
        public Task<DocumentLoad> LoadAsync(CancellationToken cancellationToken) =>
            inner.LoadAsync(cancellationToken);

        public Task<Domain.Errors.Result<SaveReceipt>> SaveAsync(
            Domain.Document.UserDocument document,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                Domain.Errors.Results.Fail<SaveReceipt>(
                    new Domain.Errors.Failure(
                        "persist.io.full",
                        Domain.Messages.L.SaveFailT,
                        Domain.Errors.FailureSeverity.Critical,
                        Domain.Errors.FailureRecovery.Retry,
                        Domain.Errors.FailureAnnouncement.Assertive
                    )
                )
            );
    }
}
