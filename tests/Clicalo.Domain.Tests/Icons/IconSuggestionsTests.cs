using Clicalo.Domain.Catalog;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Tests.Icons;

/// <summary>
/// The algorithm <c>suggestIcons</c> of EDI-005 and the icon search of EDI-004, on a small library in the order of the
/// prototype's <c>ICONLIB</c>.
/// </summary>
public sealed class IconSuggestionsTests
{
    private static readonly IconCatalog Catalog = new(
        [
            Entry("content_copy", "copiar", "duplicar", "copy", "duplicate"),
            Entry("save", "guardar", "save"),
            Entry("picture_as_pdf", "pdf", "exportar", "export"),
            Entry("title", "título", "encabezado", "heading", "title"),
            Entry("mic", "dictar", "dictado", "voz", "dictate", "mic"),
            Entry("record_voice_over", "hablar", "voz", "talk"),
            Entry("format_bold", "negrita", "bold"),
            Entry("folder", "carpeta", "folder", "explorador"),
            Entry("description", "documento", "archivo", "file", "document"),
            Entry("bolt", "acción", "rápida", "quick"),
        ],
        [new IconRef("bolt"), new IconRef("save")],
        [new IconRef("apps")]
    );

    private static readonly ComboIconTable Combos = new([
        (LangCode.Es, KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.G), new IconRef("save")),
        (LangCode.En, KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.S), new IconRef("save")),
        (LangCode.Es, KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.N), new IconRef("format_bold")),
    ]);

    [Fact]
    [Trait("Req", "EDI-005")]
    public void Words_match_tags_that_start_with_them_without_case_or_diacritics()
    {
        Suggest("Guardar como PDF").ShouldBe(["save", "picture_as_pdf"]);
        Suggest("TITULO 1").ShouldBe(["title"]);
        Suggest("Dictar con la voz").ShouldBe(["mic", "record_voice_over"]);
    }

    [Fact]
    [Trait("Req", "EDI-005")]
    public void Words_of_two_letters_or_fewer_are_ignored() => Suggest("de la PC").ShouldBeEmpty();

    [Fact]
    [Trait("Req", "EDI-005")]
    public void A_word_may_start_with_a_tag_only_when_the_tag_has_four_letters_or_more()
    {
        Suggest("Carpetas").ShouldBe(["folder"], "«carpetas» starts with «carpeta»");
        Suggest("pdfs").ShouldBeEmpty("«pdf» has only three letters");
    }

    [Fact]
    [Trait("Req", "EDI-005")]
    public void The_icon_of_the_combination_in_the_programs_language_comes_first()
    {
        Suggest("Guardar", KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.G), LangCode.Es)
            .ShouldBe(["save"], "the combination icon is not repeated");
        Suggest("Negrita", KeyChord.FromKeys(KeyIds.LeftCtrl, KeyIds.N), LangCode.Es)
            .ShouldBe(["format_bold"], "the side of a modifier does not matter");
        Suggest("Archivo", KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.G), LangCode.En)
            .ShouldBe(["description"], "Ctrl+G has no icon for English programs");
        Suggest(string.Empty, KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.S), LangCode.En)
            .ShouldBe(["save"]);
    }

    [Fact]
    [Trait("Req", "EDI-005")]
    public void At_most_six_icons_are_suggested()
    {
        var many = new IconCatalog(
            Enumerable.Range(0, 10).Select(i => Entry("icon" + i, "copiar")),
            [],
            []
        );

        IconSuggestions
            .Suggest("copiar", null, LangCode.Es, many, ComboIconTable.Empty)
            .Count.ShouldBe(6);
    }

    [Fact]
    [Trait("Req", "EDI-004")]
    public void The_search_looks_in_the_tags_in_both_languages_and_in_the_icon_name()
    {
        Names(Catalog.Search("voz")).ShouldBe(["mic", "record_voice_over"]);
        Names(Catalog.Search("DOCUMENT")).ShouldBe(["description"]);
        Names(Catalog.Search("titulo")).ShouldBe(["title"], "without diacritics");
        Names(Catalog.Search("pdf")).ShouldBe(["picture_as_pdf"]);
        Names(Catalog.Search("format_b")).ShouldBe(["format_bold"]);
    }

    [Fact]
    [Trait("Req", "EDI-004")]
    public void Without_a_search_the_featured_icons_come_first_and_then_the_rest_once()
    {
        var all = Names(Catalog.Search("  "));

        all.Take(2).ShouldBe(["bolt", "save"]);
        all.Count.ShouldBe(10);
        all.Distinct(StringComparer.Ordinal).Count().ShouldBe(10);
    }

    private static IconEntry Entry(string icon, params string[] tags) =>
        new(new IconRef(icon), [.. tags]);

    private static List<string> Suggest(
        string name,
        KeyChord? chord = null,
        LangCode? language = null
    ) => Names(IconSuggestions.Suggest(name, chord, language ?? LangCode.Es, Catalog, Combos));

    private static List<string> Names(ValueList<IconRef> icons) => [.. icons.Select(i => i.Name)];
}
