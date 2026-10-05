using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Search;
using Clicalo.Domain.Tests.Library;

namespace Clicalo.Domain.Tests.Search;

[Trait("Req", "BUS-004")]
public sealed class ShortcutSearchTests
{
    /// <summary>The combination as a tile would show it, built from the key ids («ctrl + n»).</summary>
    private static string? Shown(Shortcut shortcut) =>
        shortcut.Action is TapAction tap
            ? string.Join(" + ", tap.Chord.Strokes.Select(static s => s.Key.Value))
            : null;

    private static ShortcutLibrary Library() =>
        LibraryBuilder.BuildLibrary(
            [
                LibraryBuilder.Named("dict", "Dictar", "Dictate", KeyIds.Win, KeyIds.H),
                LibraryBuilder.Named("num", "Número", "Number", KeyIds.Ctrl, KeyIds.F2),
            ],
            [LibraryBuilder.Named("undo", "Deshacer", "Undo", KeyIds.Ctrl, KeyIds.Z)],
            LibraryBuilder.Profile(
                "word",
                "Word",
                "winword.exe",
                LibraryBuilder.Named("bold", "Negrita", "Bold", KeyIds.Ctrl, KeyIds.N),
                LibraryBuilder.Named("dictw", "Dictado", "Dictation", KeyIds.Win, KeyIds.H),
                LibraryBuilder.Shortcut(
                    "sign",
                    "Firma",
                    new TextAction(SecretTextFor("Negrita secreta"), TextMethod.Unicode)
                )
            ),
            LibraryBuilder.Profile(
                "chrome",
                "Navegador",
                "chrome.exe",
                LibraryBuilder.Named("tab", "Pestaña nueva", "New tab", KeyIds.Ctrl, KeyIds.T)
            )
        );

    private static SecretText SecretTextFor(string text) => SecretText.From(text);

    private static IEnumerable<string> Ids(string query) =>
        ShortcutSearch.Find(Library(), query, Shown).Select(static hit => hit.Shortcut.Id.Value);

    [Fact]
    [Trait("Req", "BUS-005")]
    public void A_blank_query_finds_nothing_so_the_panel_looks_as_without_searching()
    {
        ShortcutSearch.IsActive("").ShouldBeFalse();
        ShortcutSearch.IsActive("   ").ShouldBeFalse();
        ShortcutSearch.IsActive(null).ShouldBeFalse();
        Ids("").ShouldBeEmpty();
        Ids("  \t ").ShouldBeEmpty();
    }

    [Fact]
    public void It_matches_the_spanish_name_without_case_or_accents()
    {
        Ids("negr").ShouldBe(["bold"]);
        Ids("NEGR").ShouldBe(["bold"]);
        Ids("numero").ShouldBe(["num"]);
        Ids("PESTANA").ShouldBe(["tab"]);
    }

    [Fact]
    public void It_matches_the_english_name()
    {
        Ids("bold").ShouldBe(["bold"]);
        Ids("new t").ShouldBe(["tab"]);
    }

    [Fact]
    public void It_matches_the_shown_combination()
    {
        Ids("ctrl + n").ShouldBe(["bold"]);
        Ids("win + h").ShouldBe(["dict", "dictw"]);
    }

    [Fact]
    [Trait("Req", "BUS-005")]
    public void Results_come_from_always_visible_first_and_then_from_every_profile_in_order()
    {
        var hits = ShortcutSearch.Find(Library(), " dict ", Shown);

        hits.Select(static hit => (hit.Shortcut.Id.Value, hit.Profile))
            .ShouldBe([("dict", null), ("dictw", new ProfileId("word"))]);
        hits[0].IsAlwaysVisible.ShouldBeTrue();
        hits[1].IsAlwaysVisible.ShouldBeFalse();
    }

    [Fact]
    public void General_is_searched_like_any_other_profile()
    {
        var hit = ShortcutSearch.Find(Library(), "undo", Shown).ShouldHaveSingleItem();

        hit.Profile.ShouldBe(ProfileId.General);
    }

    [Fact]
    public void The_content_of_a_text_shortcut_is_never_searched_only_its_name()
    {
        Ids("secreta").ShouldBeEmpty();
        Ids("firma").ShouldBe(["sign"]);
    }

    [Fact]
    public void Nothing_matches_a_word_that_is_nowhere()
    {
        Ids("zzz").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Número", "numero")]
    [InlineData("ÁÉÍÓÚÜÑ", "aeiouun")]
    [InlineData("Ctrl + N", "ctrl + n")]
    public void Folding_removes_case_and_accents(string text, string folded) =>
        SearchText.Fold(text).ShouldBe(folded);
}
