using Clicalo.Application.UseCases.Library;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Catalogs;
using Clicalo.TestKit;

namespace Clicalo.Infrastructure.Tests.Catalogs;

/// <summary>
/// The data of the shortcut editor read from the shipped folders (D17): icons and their combination icons (EDI-004,
/// EDI-005), the mouse actions (EDI-012) and the «Añadir atajo» library (ATJ-010). Untrusted data: a missing file gives
/// the empty form.
/// </summary>
public sealed class EditorCatalogFilesTests
{
    private static readonly string Catalogs = Path.Combine(RepoPaths.Data, "catalogs");
    private static readonly string Content = Path.Combine(RepoPaths.Data, "content");

    [Fact]
    [Trait("Req", "EDI-004")]
    [Trait("Req", "EDI-005")]
    [Trait("Req", "EDI-012")]
    [Trait("Req", "ATJ-010")]
    public void The_shipped_data_gives_icons_combos_mouse_actions_and_the_library()
    {
        var catalogs = EditorCatalogFiles.Load(Catalogs, Content, null, KeyLabelCatalog.Empty);

        catalogs.Icons.Featured.Count.ShouldBe(24);
        catalogs.Icons.ProfileFeatured.Count.ShouldBe(28);
        catalogs.Icons.Search("guardar").ShouldContain(new IconRef("save"));
        IconSuggestions
            .Suggest(
                "Guardar",
                KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.G),
                LangCode.Es,
                catalogs.Icons,
                catalogs.Combos
            )[0]
            .ShouldBe(new IconRef("save"));
        catalogs.MouseActions.Select(m => m.Op).ShouldBe(Enum.GetValues<MouseOp>());
        catalogs
            .Library.Sections.Select(s => s.Id)
            .ShouldBe(["edit", "win", "mouse", "voice", "text", "sys"]);
        catalogs
            .Library.Sections.Single(s => string.Equals(s.Id, "text", StringComparison.Ordinal))
            .Shortcuts.Count.ShouldBe(3, "texts start empty");
        catalogs
            .Library.Sections.Single(s => string.Equals(s.Id, "sys", StringComparison.Ordinal))
            .Shortcuts.Items.Single(s => string.Equals(s.ItemId, "lock", StringComparison.Ordinal))
            .Action.ShouldBeOfType<SystemAction>();
    }

    [Fact]
    [Trait("Req", "EDI-004")]
    [Trait("Req", "CAT-002")]
    public void The_shipped_library_has_every_icon_by_category_and_finds_them_in_both_languages()
    {
        var icons = EditorCatalogFiles.Load(Catalogs, null, null, KeyLabelCatalog.Empty).Icons;

        icons.Entries.Count.ShouldBeGreaterThan(3800);
        icons.Categories.Count.ShouldBe(17);
        icons
            .Categories.Items.Where(c => icons.InCategory(c.Id).IsEmpty)
            .ShouldBeEmpty("every category has icons");
        icons.Categories[0].Label.Values[LangCode.Es].ShouldBe("Interfaz");
        icons.Entries.Items.ShouldAllBe(e => e.Category.Length > 0);
        icons.Search("auriculares").ShouldContain(new IconRef("headphones"));
        icons.Search("headset").ShouldContain(new IconRef("headphones"), "an official keyword");
        icons.Search("melody").ShouldContain(new IconRef("music_note"));
        icons.Search("pelicula")[0].ShouldBe(new IconRef("movie"), "the curated icons come first");
    }

    [Fact]
    [Trait("Req", "EDI-004")]
    [Trait("Req", "EDI-005")]
    [Trait("Req", "ACC-001")]
    public void An_icon_is_named_with_all_its_words_and_suggested_only_from_them()
    {
        var catalogs = EditorCatalogFiles.Load(Catalogs, null, null, KeyLabelCatalog.Empty);
        var icons = catalogs.Icons;

        // Every hand-written word of the language names the cell: 115 icons shared their first Spanish word.
        icons.NameOf("content_copy", LangCode.Es).ShouldBe("copiar, duplicar");
        icons.NameOf("content_copy", LangCode.En).ShouldBe("copy, duplicate");
        icons
            .NameOf("pin_drop", LangCode.Es)
            .ShouldBe("pin drop", "no hand-written word: its name");

        // The keywords of Material find icons, but they do not suggest them: «Word» is not bold, «Zoom» no magnifier.
        Suggest("Word").ShouldNotContain(new IconRef("format_bold"));
        Suggest("Zoom").ShouldNotContain(new IconRef("search"));
        Suggest("Zoom")[0].ShouldBe(new IconRef("zoom_in"));
        icons.Search("zoom").ShouldContain(new IconRef("search"), "the search reads them");

        ValueList<IconRef> Suggest(string name) =>
            IconSuggestions.Suggest(name, null, LangCode.Es, icons, catalogs.Combos);
    }

    [Fact]
    [Trait("Req", "ATJ-010")]
    public void Missing_folders_give_the_empty_forms()
    {
        var missing = Path.Combine(Path.GetTempPath(), "clicalo-missing-" + Environment.ProcessId);

        var catalogs = EditorCatalogFiles.Load(missing, null, null, KeyLabelCatalog.Empty);

        catalogs.Icons.Entries.ShouldBeEmpty();
        catalogs.Library.ShouldBe(LibraryContent.Empty);
        catalogs.MouseActions.ShouldBeEmpty();
    }
}
