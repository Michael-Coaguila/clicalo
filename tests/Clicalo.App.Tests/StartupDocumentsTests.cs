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
        var load = await Start().LoadAsync(ContentFolder, LangCode.En, null, Token);

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

        var again = await Start().LoadAsync(ContentFolder, LangCode.Es, null, Token);

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

        var load = await Start().LoadAsync(ContentFolder, LangCode.Es, v1, Token);

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

        var again = await Start().LoadAsync(ContentFolder, LangCode.Es, v1, Token);

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

        var load = await Start().LoadAsync(ContentFolder, LangCode.Es, damaged, Token);

        load.Document.Library.AlwaysVisible.Count.ShouldBe(4, "the example data");
        File.Exists(_locations.Document).ShouldBeFalse("so the migration can be tried again");
        var again = await Start().LoadAsync(ContentFolder, LangCode.Es, null, Token);
        again.Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);
    }

    [Fact]
    public async Task Without_a_seed_a_new_installation_still_starts_with_general()
    {
        var load = await Start().LoadAsync(null, null, null, Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);
        load.Document.Library.Profiles.ShouldHaveSingleItem().Id.ShouldBe(ProfileId.General);
        load.Document.Validate().ShouldBeEmpty();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Fixture(string name) =>
        RepoPaths.Combine("tests", "Clicalo.Infrastructure.Tests", "Fixtures", "v1", name);

    private StartupDocuments Start()
    {
        var time = TimeProvider.System;
        var writer = new AtomicFile(time, NullLogger<AtomicFile>.Instance);
        var codec = new DocumentCodec();
        var backups = new BackupService(
            _locations,
            writer,
            time,
            NullLogger<BackupService>.Instance,
            codec
        );
        var documents = new DocumentRepository(
            _locations,
            writer,
            new QuarantineStore(_locations, time),
            backups,
            time,
            NullLogger<DocumentRepository>.Instance,
            codec
        );
        return new StartupDocuments(
            documents,
            new UsageRepository(_locations, writer, time, NullLogger<UsageRepository>.Instance),
            backups,
            new V1Importer(
                new SafeZipReader(SafeZipLimits.Default),
                NullLogger<V1Importer>.Instance
            ),
            new RandomIdGenerator(),
            time,
            NullLogger<StartupDocuments>.Instance
        );
    }
}
