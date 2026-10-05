using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Tests.Generators;

namespace Clicalo.Domain.Tests.Templates;

/// <summary>
/// The library of a new document from the starter kit (user decision D2 of 2026-10-03): General and Always visible
/// always exist, «Basics» fills them, each marked template becomes a profile bound to all its processes in the
/// programs language, and nothing marked starts empty.
/// </summary>
[Trait("Req", "CAT-003")]
[Trait("Req", "BIE-006")]
public sealed class StarterLibraryTests
{
    private static readonly StarterContent Content = StarterFixture.Content;

    [Fact]
    [Trait("Req", "BIE-003")]
    public void The_default_selection_is_basics_only()
    {
        Content.Kit.DefaultSelection.ShouldBe(StarterSelection.Of(["basics"]));

        var library = Build(Content.Kit.DefaultSelection);

        library.AlwaysVisible.Select(Item).ShouldBe(["dict", "desk"]);
        library.Profiles.ShouldHaveSingleItem().Id.ShouldBe(ProfileId.General);
        library.General.Shortcuts.Select(Item).ShouldBe(["copy", "close"]);
        library.General.Binding.ShouldBe(new AppBinding.Manual());
    }

    [Fact]
    public void Nothing_marked_starts_empty_with_general_and_always_visible()
    {
        var library = Build(StarterSelection.Empty);

        library.AlwaysVisible.ShouldBeEmpty();
        var general = library.Profiles.ShouldHaveSingleItem();
        general.Id.ShouldBe(ProfileId.General);
        general.Name.Get(LangCode.Es, LangCode.En).ShouldBe("General");
        general.Shortcuts.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "PLA-013")]
    [Trait("Req", "PER-002")]
    public void A_marked_template_is_a_profile_bound_to_every_process_after_general()
    {
        var library = Build(StarterSelection.Of(["browser"]));

        library.AlwaysVisible.ShouldBeEmpty("Basics was unmarked");
        library.General.Shortcuts.ShouldBeEmpty();
        library.Profiles.Count.ShouldBe(2);
        var browser = library.Profiles[1];
        browser.Name.Get(LangCode.En, LangCode.Es).ShouldBe("Browser");
        browser.Binding.ShouldBe(
            new AppBinding.Processes([new ProcessName("chrome.exe"), new ProcessName("msedge.exe")])
        );
        browser.Origin.ShouldBe(new CatalogRef("browser", "1", "browser"));
        library.ProfileFor(new ProcessName("MSEdge.exe")).ShouldBe(browser);
        library.ProfileFor(new ProcessName("chrome.exe")).ShouldBe(browser);
    }

    [Fact]
    public void Templates_follow_the_order_of_the_kit_not_of_the_selection()
    {
        var library = Build(StarterSelection.Of(["browser", "basics", "word"]));

        library
            .Profiles.Select(p => p.Origin?.Source ?? "general")
            .ShouldBe(["general", "word", "browser"]);
    }

    [Fact]
    [Trait("Req", "ATJ-007")]
    [Trait("Req", "DAT-005")]
    public void A_process_already_bound_by_an_earlier_template_is_left_out()
    {
        var library = Build(StarterSelection.Of(["browser", "chat"]));

        var chat = From(library, "chat");
        chat.Binding.ShouldBe(new AppBinding.Processes([new ProcessName("chat.exe")]));
        library.ProfileFor(new ProcessName("msedge.exe")).ShouldBe(From(library, "browser"));
    }

    [Theory]
    [Trait("Req", "CAT-005")]
    [Trait("Req", "PLA-009")]
    [Trait("Req", "EC-PLA-04")]
    [InlineData("es", KeyIdsName.N)]
    [InlineData("en", KeyIdsName.B)]
    [InlineData("pt", KeyIdsName.N)]
    public void A_tap_is_installed_with_the_combination_of_the_programs_language(
        string appsLanguage,
        KeyIdsName expected
    )
    {
        var library = ShortcutsOf("word", new LangCode(appsLanguage));

        var bold = (TapAction)Find(library, "bold").Action;
        bold.Chord.ShouldBe(
            StarterFixture.Chord(KeyIds.Ctrl, expected == KeyIdsName.N ? KeyIds.N : KeyIds.B)
        );
        bold.Variants.ShouldBeEmpty(
            "a later change of the programs language does not change installed shortcuts (EC-PLA-04)"
        );
        ((TapAction)Find(library, "spell").Action).Chord.ShouldBe(StarterFixture.Chord(KeyIds.F7));
    }

    [Fact]
    [Trait("Req", "DAT-004")]
    public void Every_installed_shortcut_and_profile_has_a_new_id_and_its_catalog_reference()
    {
        var library = Build(StarterSelection.Of(["basics", "word", "browser", "chat"]));

        var ids = library
            .AlwaysVisible.Select(s => s.Id.Value)
            .Concat(library.Profiles.SelectMany(p => p.Shortcuts).Select(s => s.Id.Value))
            .Concat(library.Profiles.Skip(1).Select(p => p.Id.Value))
            .ToList();
        ids.ShouldBeUnique(StringComparer.Ordinal);
        ids.ShouldAllBe(id => id.StartsWith('n'));
        library.General.Shortcuts[0].Origin.ShouldBe(new CatalogRef("seed", "3", "copy"));
        library.General.Shortcuts[1].Options.Confirm.ShouldBeTrue("Alt+F4 keeps its two taps");
        From(library, "word").Shortcuts[0].Origin.ShouldBe(new CatalogRef("word", "2", "bold"));
    }

    [Fact]
    public void An_option_the_kit_does_not_have_installs_nothing()
    {
        Build(StarterSelection.Of(["photoshop"])).ShouldBe(Build(StarterSelection.Empty));
    }

    [Fact]
    public void Toggling_a_chip_marks_and_unmarks_it()
    {
        var selection = Content.Kit.DefaultSelection.Toggle("word");

        selection.ShouldBe(StarterSelection.Of(["basics", "word"]));
        selection.Toggle("word").Toggle("basics").ShouldBe(StarterSelection.Empty);
    }

    [Fact]
    [Trait("Req", "PER-009")]
    [Trait("Req", "PLA-011")]
    public void A_template_is_found_by_any_of_its_processes_without_case()
    {
        Content.TemplateFor(new ProcessName("MSEDGE.exe"))!.Id.ShouldBe("browser");
        Content.TemplateFor(new ProcessName("winword.exe"))!.Id.ShouldBe("word");
        Content.TemplateFor(new ProcessName("notepad.exe")).ShouldBeNull();
        Content.TemplateFor(new ProcessName(string.Empty)).ShouldBeNull();
    }

    /// <summary>Which key the expected Bold ends with (xUnit data must be serializable).</summary>
    public enum KeyIdsName
    {
        /// <summary>Ctrl+N, Bold of Spanish Office.</summary>
        N,

        /// <summary>Ctrl+B, Bold of English Office.</summary>
        B,
    }

    private static ShortcutLibrary Build(StarterSelection selection) =>
        StarterLibrary.Build(Content, selection, LangCode.Es, new SequentialIds()).Value;

    private static ValueList<Shortcut> ShortcutsOf(string template, LangCode appsLanguage) =>
        From(
            StarterLibrary
                .Build(Content, StarterSelection.Of([template]), appsLanguage, new SequentialIds())
                .Value,
            template
        ).Shortcuts;

    private static Profile From(ShortcutLibrary library, string template) =>
        library.Profiles.Single(p =>
            string.Equals(p.Origin?.Source, template, StringComparison.Ordinal)
        );

    private static Shortcut Find(ValueList<Shortcut> shortcuts, string item) =>
        shortcuts.Single(s => string.Equals(Item(s), item, StringComparison.Ordinal));

    private static string Item(Shortcut shortcut) => shortcut.Origin!.Value.ItemId;
}
