using Clicalo.App.Composition;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit;

namespace Clicalo.App.Tests;

/// <summary>
/// The key line of the tiles with the real catalogs the app copies next to the executable: the combination a tap sends
/// to the app in front, written with the labels of <c>keys.json</c>, abbreviated in S and hidden in Compact or with
/// «Mostrar teclas» off.
/// </summary>
[Trait("Req", "CUA-007")]
public sealed class TileKeyLinesTests
{
    private static readonly RuntimeCatalogs Catalogs = RuntimeCatalogs.Load(RepoPaths.Data);

    [Fact]
    [Trait("Req", "EJE-018")]
    public void A_common_action_shows_the_combination_sent_to_the_app_in_front()
    {
        var save = Save();

        Lines(new ProcessName("winword.exe")).For(save).Line.ShouldBe("Ctrl + G");
        Lines(new ProcessName("chrome.exe")).For(save).Line.ShouldBe("Ctrl + S");
        Lines(null).For(save).Line.ShouldBe("Ctrl + S");
    }

    [Fact]
    [Trait("Req", "CUA-008")]
    public void Size_S_abbreviates_the_line_and_screen_readers_get_the_full_names()
    {
        var line = Lines(null, abbreviated: true).For(Save());

        line.Line.ShouldBe("Ctl+S");
        line.Spoken.ShouldBe("Ctrl + S");
    }

    [Fact]
    public void The_line_hides_when_keys_are_not_shown_and_for_actions_without_keys()
    {
        Lines(null, shown: false).For(Save()).ShouldBe(TileKeyLine.None);
        Lines(null)
            .For(Save() with { Action = new MouseAction(MouseOp.RightClick, ScrollSpeed.Normal) })
            .ShouldBe(TileKeyLine.None);
    }

    [Fact]
    public void The_catalogs_and_the_starter_content_load_from_the_data_folder()
    {
        Catalogs.KeyLabels.ShouldNotBeSameAs(KeyLabelCatalog.Empty);
        Catalogs.CommonActions.Actions.ShouldNotBeEmpty();
        Catalogs.Content.ShouldNotBeNull();
    }

    private static TileKeyLines Lines(
        ProcessName? app,
        bool shown = true,
        bool abbreviated = false
    ) =>
        new(
            Catalogs.CommonActions,
            Catalogs.KeyLabels,
            app,
            LangCode.Es,
            LangCode.Es,
            shown,
            abbreviated
        );

    private static Shortcut Save() =>
        new(
            new ShortcutId("s1"),
            LocalizedText.Same("Guardar", LangCode.Es, LangCode.En),
            new IconRef("save"),
            AutoIcon: false,
            new CategoryId("file"),
            new TapAction(
                KeyChord.Create([new KeyStroke(new KeyId("ctrl")), new KeyStroke(new KeyId("s"))]),
                []
            ),
            new ShortcutOptions(Confirm: false, new HoldLimit.InheritGlobal(), IsPrivate: false),
            Origin: new CatalogRef("seed", "1", "save"),
            PinnedFrom: null
        );
}
