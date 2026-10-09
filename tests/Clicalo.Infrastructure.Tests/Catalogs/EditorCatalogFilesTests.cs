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
