using Clicalo.Domain.Frequents;
using Clicalo.Domain.Primitives;

namespace Clicalo.Windowing.IntegrationTests.Interactions;

/// <summary>
/// The context menu of a tile headless (CUA-014, FRE-001, AVI-005): its header and rows, pinning, unpinning and removing
/// from Frequents with undo, the pin limit, [edit] and [cancel].
/// </summary>
[Trait("Req", "CUA-014")]
public sealed class TileContextMenuViewModelTests
{
    private readonly InteractionsWorld _world = new();

    [Fact]
    public void Outside_frequents_it_offers_pin_edit_and_cancel_under_the_tile_name()
    {
        var menu = _world.Menu;

        menu.Open(InteractionsWorld.Bold, "Negrita", "format_bold", inFrequents: false);

        menu.IsOpen.ShouldBeTrue();
        menu.Title.ShouldBe("Negrita");
        menu.Icon.ShouldBe("format_bold");
        menu.AccessibleName.ShouldBe("Más opciones");
        menu.Rows.Select(static r => r.Label)
            .ShouldBe(["Fijar en Frecuentes", "Editar", "Cancelar"]);
        menu.Rows.Select(static r => r.Icon).ShouldBe(["keep", "edit", "close"]);
    }

    [Fact]
    [Trait("Req", "FRE-001")]
    [Trait("Req", "AVI-005")]
    public void Pinning_saves_with_undo_and_the_next_menu_offers_to_unpin()
    {
        var menu = _world.Menu;
        menu.Open(InteractionsWorld.Bold, "Negrita", "bolt", inFrequents: false);

        menu.Rows[0].Activate();

        menu.IsOpen.ShouldBeFalse();
        _world.Store.Current.Frequents.Pins.ShouldBe([InteractionsWorld.Bold]);
        var notice = _world.Notices.Timed.Single();
        _world.Text(notice).ShouldBe("Fijado en Frecuentes");
        notice.CanUndo.ShouldBeTrue();

        menu.Open(InteractionsWorld.Bold, "Negrita", "bolt", inFrequents: true);
        menu.Rows.Select(static r => r.Item)
            .ShouldBe([TileMenuItem.Unpin, TileMenuItem.Edit, TileMenuItem.Cancel]);
        menu.Rows[0].Activate();
        _world.Store.Current.Frequents.Pins.ShouldBeEmpty();
        _world.Text(_world.Notices.Timed[^1]).ShouldBe("Ya no está fijado");
    }

    [Fact]
    [Trait("Req", "FRE-001")]
    public void In_frequents_an_unpinned_tile_can_be_removed_with_undo()
    {
        var menu = _world.Menu;
        menu.Open(InteractionsWorld.Bold, "Negrita", "bolt", inFrequents: true);

        menu.Rows[1].Label.ShouldBe("Quitar de Frecuentes");
        menu.Rows[1].Activate();

        _world.Store.Current.Frequents.Hidden.ShouldBe([InteractionsWorld.Bold]);
        _world.Text(_world.Notices.Timed.Single()).ShouldBe("Quitado de Frecuentes");
        _world.Store.Undo().IsSuccess.ShouldBeTrue();
        _world.Store.Current.Frequents.Hidden.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "FRE-001")]
    public void Pinning_past_the_tiles_frequents_shows_warns_and_still_pins()
    {
        var world = new InteractionsWorld(document: InteractionsWorld.Crowded(10));
        for (var i = 1; i <= 9; i++)
        {
            world.Menu.Open(new ShortcutId("x" + i), "x" + i, "bolt", inFrequents: false);
            world.Menu.Rows[0].Activate();
        }

        world.Menu.Open(new ShortcutId("x10"), "x10", "bolt", inFrequents: false);
        world.Menu.Rows[0].Activate();

        world.Store.Current.Frequents.Pins.Count.ShouldBe(10);
        world.Text(world.Notices.Timed[^2]).ShouldBe("Fijado en Frecuentes");
        world
            .Text(world.Notices.Timed[^1])
            .ShouldBe("Fijado. Frecuentes solo muestra los 9 primeros fijados");
    }

    [Fact]
    [Trait("Req", "CUA-012")]
    public void Edit_opens_the_editor_and_cancel_only_closes()
    {
        var menu = _world.Menu;
        menu.Open(InteractionsWorld.Bold, "Negrita", "bolt", inFrequents: false);
        menu.Rows[1].Activate();

        menu.Open(InteractionsWorld.Bold, "Negrita", "bolt", inFrequents: false);
        menu.Rows[2].Activate();

        menu.IsOpen.ShouldBeFalse();
        menu.Rows.ShouldBeEmpty();
        _world.ControlCenter.Requests.ShouldBe(["editor:bold"]);
        _world.Notices.Timed.ShouldBeEmpty();
        _world.Store.CanUndo.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "IDI-001")]
    public void An_open_menu_changes_language_in_place()
    {
        var menu = _world.Menu;
        menu.Open(InteractionsWorld.Bold, "Negrita", "bolt", inFrequents: true);

        _world.Localization.TrySetLanguage("en");
        menu.Relocalize();

        menu.Rows.Select(static r => r.Label)
            .ShouldBe(["Pin to Frequent", "Remove from Frequent", "Edit", "Cancel"]);
        menu.AccessibleName.ShouldBe("More options");
    }
}
