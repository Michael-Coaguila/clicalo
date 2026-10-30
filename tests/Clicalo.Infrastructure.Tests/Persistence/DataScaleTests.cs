using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Library;
using Clicalo.Domain.Search;
using Clicalo.Infrastructure.Backup;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// NFR-016: Clícalo works with 50 profiles and 2000 shortcuts (the real user has 14 and 210) without a perceptible
/// slowdown. The document of that size is saved, loaded and searched on a real temporary disk, each within a budget
/// far below what a person notices, and it stays under the 2 MB the blueprint assumes (§6.5). The budgets are loose
/// on purpose (a busy build machine) and each time is the best of several runs, so the test does not depend on the
/// load of the moment.
/// </summary>
[Trait("Req", "NFR-016")]
public sealed class DataScaleTests : IDisposable
{
    private const int Profiles = 50;
    private const int ShortcutsPerProfile = 40;
    private const int Runs = 5;
    private const long MaxDocumentBytes = 2 * 1024 * 1024;

    private static readonly TimeSpan SaveBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LoadBudget = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SearchBudget = TimeSpan.FromMilliseconds(100);

    private readonly BoundedTestToken _bounded = new();
    private readonly TempFolder _folder = new();
    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    public void Dispose()
    {
        _bounded.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public async Task Fifty_profiles_and_two_thousand_shortcuts_save_load_and_search_within_budget()
    {
        var document = Large();
        document.Library.Profiles.Count.ShouldBe(Profiles);
        document.Library.EnumerateShortcuts().Count().ShouldBe(Profiles * ShortcutsPerProfile);
        document.Validate().ShouldBeEmpty();
        var repository = Repository();
        (await repository.LoadAsync(_bounded.Token)).Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);

        var save = TimeSpan.MaxValue;
        for (var run = 0; run < Runs; run++)
        {
            var started = TimeProvider.System.GetTimestamp();
            var saved = await repository.SaveAsync(document, _bounded.Token);
            save = Min(save, TimeProvider.System.GetElapsedTime(started));
            saved.IsSuccess.ShouldBeTrue();
        }

        new FileInfo(_folder.Locations.Document).Length.ShouldBeLessThan(MaxDocumentBytes);

        var load = TimeSpan.MaxValue;
        DocumentLoad? loaded = null;
        for (var run = 0; run < Runs; run++)
        {
            var started = TimeProvider.System.GetTimestamp();
            loaded = await Repository().LoadAsync(_bounded.Token);
            load = Min(load, TimeProvider.System.GetElapsedTime(started));
        }

        loaded!.Outcome.ShouldBe(DocumentLoadOutcome.Loaded);
        loaded.Document.Library.ShouldBe(document.Library);

        var search = TimeSpan.MaxValue;
        var hits = 0;
        for (var run = 0; run < Runs; run++)
        {
            var started = TimeProvider.System.GetTimestamp();
            // One profile's worth of results, and the worst case: a query that matches nothing.
            hits = ShortcutSearch.Find(loaded.Document.Library, "perfil 37", NoCombination).Length;
            _ = ShortcutSearch.Find(loaded.Document.Library, "no existe", NoCombination);
            search = Min(search, TimeProvider.System.GetElapsedTime(started));
        }

        hits.ShouldBe(ShortcutsPerProfile);
        TestContext.Current.TestOutputHelper?.WriteLine(
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"save {save.TotalMilliseconds:F1} ms, load {load.TotalMilliseconds:F1} ms, search {search.TotalMilliseconds:F2} ms"
            )
        );
        save.ShouldBeLessThan(SaveBudget, "saving the document");
        load.ShouldBeLessThan(LoadBudget, "loading the document");
        search.ShouldBeLessThan(SearchBudget, "two searches over every shortcut");
    }

    [Fact]
    [Trait("Req", "COP-002")]
    public async Task A_document_of_that_size_is_within_what_an_import_accepts()
    {
        var repository = Repository();
        _ = await repository.LoadAsync(_bounded.Token);
        (await repository.SaveAsync(Large(), _bounded.Token)).IsSuccess.ShouldBeTrue();

        var imported = DocumentImportReader.Read(
            await File.ReadAllBytesAsync(_folder.Locations.Document, _bounded.Token)
        );

        imported.IsSuccess.ShouldBeTrue();
        imported.Value.Profiles.ShouldBe(Profiles);
        imported.Value.Shortcuts.ShouldBe(Profiles * ShortcutsPerProfile);
    }

    private static string? NoCombination(Shortcut shortcut) => null;

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;

    /// <summary>General and 49 app profiles, 40 shortcuts each, with names a search can tell apart.</summary>
    private static UserDocument Large()
    {
        string[][] chords =
        [
            ["ctrl", "c"],
            ["ctrl", "shift", "s"],
            ["alt", "f4"],
            ["f5"],
        ];
        var profiles = Enumerable
            .Range(0, Profiles)
            .Select(p =>
            {
                var id = p == 0 ? "general" : "perfil" + p;
                var shortcuts = Enumerable
                    .Range(0, ShortcutsPerProfile)
                    .Select(s =>
                        TestDocuments.Tap("p" + p + "s" + s, chords[s % chords.Length]) with
                        {
                            Name = Domain.Primitives.LocalizedText.Same(
                                "Perfil " + p + " atajo " + s,
                                Domain.Primitives.LangCode.Es,
                                Domain.Primitives.LangCode.En
                            ),
                        }
                    );
                return p == 0
                    ? TestDocuments.Profile(id, shortcuts)
                    : TestDocuments.Profile(id, shortcuts, "app" + p + ".exe");
            });
        return new UserDocument(
            0,
            TestDocuments.Library([], profiles),
            FrequentsState.Empty,
            DuplicatePolicy.Empty,
            TestDocuments.Settings,
            new OnboardingState(true)
        );
    }

    private DocumentRepository Repository()
    {
        var data = _folder.Locations;
        var writer = new AtomicFile(_time, NullLogger<AtomicFile>.Instance);
        return new DocumentRepository(
            data,
            writer,
            new QuarantineStore(data, _time),
            new BackupService(data, writer, _time, NullLogger<BackupService>.Instance),
            _time,
            NullLogger<DocumentRepository>.Instance
        );
    }
}
