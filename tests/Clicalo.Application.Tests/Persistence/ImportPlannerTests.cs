using System.Globalization;
using Clicalo.Application.Persistence;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Tests.Persistence;

/// <summary>
/// Import plans (COP-002, DAT-004, DAT-007): Merge adds what is missing and never loses an imported shortcut; Replace
/// swaps the library. They build the result with <c>ShortcutLibrary.CreateValidated</c>, so they run once the domain
/// package is integrated.
/// </summary>
[Trait("Req", "COP-002")]
public sealed class ImportPlannerTests
{
    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    [Trait("Req", "DAT-004")]
    public void Merge_adds_missing_profiles_and_shortcuts_and_the_existing_one_wins_on_an_id()
    {
        var current = Document([
            PersistenceDocuments.Profile("general", [Shortcut("copy", "Copy")]),
            Word([Shortcut("bold", "Bold")]),
        ]);
        var imported = Document([
            PersistenceDocuments.Profile(
                "general",
                [Shortcut("copy", "Copy"), Shortcut("paste", "Paste")]
            ),
            Word([Shortcut("bold", "Negrita"), Shortcut("italic", "Italic")]),
            PersistenceDocuments.Profile("excel", [Shortcut("sum", "Sum")]),
        ]);

        var plan = ImportPlanner.Merge(current, imported, new SequentialIds()).Value;

        var profiles = plan.Next.Library.Profiles;
        profiles.Select(p => p.Id.Value).ShouldBe(["general", "word", "excel"]);
        profiles[0].Shortcuts.Select(s => s.Id.Value).ShouldBe(["copy", "paste"]);
        profiles[1].Shortcuts.Select(s => s.Id.Value).ShouldBe(["bold", "s1", "italic"]);
        profiles[1].Shortcuts[0].Name.Get(LangCode.Es, LangCode.En).ShouldBe("Bold");
        profiles[1].Shortcuts[1].Name.Get(LangCode.Es, LangCode.En).ShouldBe("Negrita");
        plan.Summary.ShouldBe(new ImportSummary(1, 4, 1, 1, 0));
        plan.Next.Settings.ShouldBe(current.Settings);
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    [Trait("Req", "PER-002")]
    public void Merge_keeps_a_process_bound_to_its_current_profile()
    {
        var current = Document([PersistenceDocuments.Profile("general", []), Word([])]);
        var imported = Document([
            PersistenceDocuments.Profile("general", []),
            PersistenceDocuments.Profile("docs", [], "Docs", "WINWORD.EXE", "notepad.exe"),
        ]);

        var plan = ImportPlanner.Merge(current, imported, new SequentialIds()).Value;

        plan.Next.Library.Profiles[2]
            .Binding.ShouldBeOfType<AppBinding.Processes>()
            .Names.Select(n => n.Value)
            .ShouldBe(["notepad.exe"]);
        plan.Summary.BindingsDropped.ShouldBe(1);
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    [Trait("Req", "REG-04")]
    public void Replace_swaps_the_library_and_keeps_settings_and_frequents()
    {
        var current = Document([
            PersistenceDocuments.Profile("general", [Shortcut("copy", "Copy")]),
        ]);
        var imported = Document([PersistenceDocuments.Profile("general", [Shortcut("x", "X")])]);

        var plan = ImportPlanner.Replace(current, imported).Value;

        plan.Next.Library.ShouldBe(imported.Library);
        plan.Next.Frequents.ShouldBe(current.Frequents);
        plan.Next.Settings.ShouldBe(current.Settings);
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    [Trait("Req", "DAT-007")]
    public void A_shared_profile_is_added_after_the_others()
    {
        var current = Document([
            PersistenceDocuments.Profile("general", [Shortcut("copy", "Copy")]),
        ]);
        var shared = PersistenceDocuments.Profile("p9", [Shortcut("s9", "Shared")]);

        var plan = ImportPlanner.AddProfile(current, shared, new SequentialIds()).Value;

        plan.Next.Library.Profiles.Select(p => p.Id.Value).ShouldBe(["general", "p9"]);
        plan.Summary.ProfilesAdded.ShouldBe(1);
    }

    private static Domain.Document.UserDocument Document(IEnumerable<Profile> profiles) =>
        PersistenceDocuments.Document(1) with
        {
            Library = PersistenceDocuments.Library([], profiles),
        };

    private static Profile Word(IEnumerable<Shortcut> shortcuts) =>
        PersistenceDocuments.Profile("word", shortcuts, "Word", "winword.exe");

    private static Shortcut Shortcut(string id, string name) =>
        PersistenceDocuments.Shortcut(id, name);

    /// <summary>Ids p1, p2… and s1, s2… in order.</summary>
    private sealed class SequentialIds : IIdGenerator
    {
        private int _profiles;
        private int _shortcuts;

        public ProfileId NewProfileId() =>
            new("p" + (++_profiles).ToString(CultureInfo.InvariantCulture));

        public ShortcutId NewShortcutId() =>
            new("s" + (++_shortcuts).ToString(CultureInfo.InvariantCulture));
    }
}
