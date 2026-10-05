using System.IO;
using Clicalo.App.Composition;
using Clicalo.App.Lifecycle;
using Clicalo.Application.Ports;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Backup;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.App.Tests;

/// <summary>
/// The document a start hands to the store, against a real temporary data folder (blueprint §6.5): a new installation
/// gets the starter kit marked by default («Basics», user decision D2) in the language of Windows, written at once;
/// files of Macro Quick Access are never read (ADR-0020).
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
    [Trait("Req", "BIE-003")]
    public async Task A_new_installation_gets_the_default_starter_kit_in_the_language_of_windows_and_keeps_it()
    {
        var load = await LoadAsync(ContentFolder, LangCode.En, Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);
        load.Document.Settings.Language.ShouldBe(LangCode.En);
        var library = load.Document.Library;
        library.AlwaysVisible.Count.ShouldBe(4, "«Basics» is marked by default (user decision D2)");
        library
            .Profiles.ShouldHaveSingleItem("no template is marked by default")
            .Id.ShouldBe(ProfileId.General);
        var copy = library.General.Shortcuts[0];
        copy.Origin.ShouldBe(new CatalogRef("seed", "1", "copy"));
        copy.Id.ShouldNotBe(new ShortcutId("copy"), "installed content gets new ids (DAT-004)");
        copy.Action.ShouldBe(
            new TapAction(
                KeyChord.Create([new KeyStroke(KeyIds.Ctrl), new KeyStroke(KeyIds.C)]),
                []
            )
        );

        var again = await LoadAsync(ContentFolder, LangCode.Es, Token);

        again.Outcome.ShouldBe(DocumentLoadOutcome.Loaded, "the seed was written at once");
        again.Document.Library.ShouldBe(library);
        again.Document.Settings.Language.ShouldBe(
            LangCode.En,
            "only a new installation follows Windows"
        );
    }

    [Fact]
    [Trait("Req", "REG-08")]
    public async Task A_profiles_json_of_the_previous_app_is_never_read_nor_touched()
    {
        var previous = Path.Combine(_locations.Root, "profiles.json");
        var bytes = """{"profiles": [ {"name": "Invented", "buttons": []} ]}"""u8.ToArray();
        await File.WriteAllBytesAsync(previous, bytes, Token);

        var load = await LoadAsync(ContentFolder, LangCode.Es, Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);
        load.Document.Library.Profiles.ShouldHaveSingleItem().Id.ShouldBe(ProfileId.General);
        load.Document.Library.AlwaysVisible.Count.ShouldBe(4, "the seed, nothing converted");
        (await File.ReadAllBytesAsync(previous, Token)).ShouldBe(bytes);
    }

    [Fact]
    public async Task Without_a_seed_a_new_installation_still_starts_with_general()
    {
        var load = await LoadAsync(null, null, Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);
        load.Document.Library.Profiles.ShouldHaveSingleItem().Id.ShouldBe(ProfileId.General);
        load.Document.Validate().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "DAT-002")]
    public async Task A_new_installation_that_cannot_be_written_is_handed_to_the_autosave()
    {
        var load = await Start(new UnwritableDocuments(Repository()))
            .LoadAsync(ContentFolder, LangCode.Es, Token);

        load.Load.Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);
        load.SavePending.ShouldBeTrue();
        load.Load.Document.Library.AlwaysVisible.Count.ShouldBe(4);
    }

    [Fact]
    [Trait("Req", "DAT-002")]
    public async Task A_new_installation_written_at_once_has_nothing_pending() =>
        (await Start().LoadAsync(ContentFolder, LangCode.Es, Token)).SavePending.ShouldBeFalse();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<DocumentLoad> LoadAsync(
        string? contentFolder,
        LangCode? language,
        CancellationToken cancellationToken
    ) => (await Start().LoadAsync(contentFolder, language, cancellationToken)).Load;

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
