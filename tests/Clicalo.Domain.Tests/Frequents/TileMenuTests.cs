using Clicalo.Domain.Frequents;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Tests.Frequents;

/// <summary>
/// The context menu of a tile (CUA-014, CUA-015) and the × of edit mode (CUA-012, CUA-013): when they apply and what they
/// offer depend on the tile, the view and the pins.
/// </summary>
public sealed class TileMenuTests
{
    private static readonly ShortcutId Copy = new("copy");

    [Fact]
    [Trait("Req", "CUA-014")]
    public void A_long_press_opens_the_menu_except_on_a_hold_tile_and_in_edit_mode()
    {
        TileMenu.OpensOnLongPress(holdTile: false, editMode: false).ShouldBeTrue();
        TileMenu.OpensOnLongPress(holdTile: true, editMode: false).ShouldBeFalse();
        TileMenu.OpensOnLongPress(holdTile: false, editMode: true).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    public void Outside_frequents_the_menu_pins_edits_and_cancels()
    {
        TileMenu
            .Items(FrequentsState.Empty, Copy, inFrequents: false)
            .ShouldBe([TileMenuItem.Pin, TileMenuItem.Edit, TileMenuItem.Cancel]);
        TileMenu
            .Items(FrequentsState.Empty.WithPin(Copy), Copy, inFrequents: false)
            .ShouldBe([TileMenuItem.Unpin, TileMenuItem.Edit, TileMenuItem.Cancel]);
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    [Trait("Req", "FRE-001")]
    public void In_frequents_an_unpinned_tile_can_also_be_removed()
    {
        TileMenu
            .Items(FrequentsState.Empty, Copy, inFrequents: true)
            .ShouldBe([
                TileMenuItem.Pin,
                TileMenuItem.Hide,
                TileMenuItem.Edit,
                TileMenuItem.Cancel,
            ]);
        TileMenu
            .Items(FrequentsState.Empty.WithPin(Copy), Copy, inFrequents: true)
            .ShouldBe([TileMenuItem.Unpin, TileMenuItem.Edit, TileMenuItem.Cancel]);
    }

    [Fact]
    [Trait("Req", "CUA-012")]
    [Trait("Req", "CUA-013")]
    public void The_cross_deletes_in_a_profile_hides_in_frequents_and_is_absent_from_the_search()
    {
        EditModeRules.RemovalIn(frequents: false, searching: false).ShouldBe(TileRemoval.Delete);
        EditModeRules
            .RemovalIn(frequents: true, searching: false)
            .ShouldBe(TileRemoval.HideFromFrequents);
        EditModeRules.RemovalIn(frequents: false, searching: true).ShouldBe(TileRemoval.None);
        EditModeRules.RemovalIn(frequents: true, searching: true).ShouldBe(TileRemoval.None);
    }

    [Fact]
    [Trait("Req", "CUA-012")]
    [Trait("Req", "FRE-003")]
    public void The_add_tile_is_offered_only_in_a_profile()
    {
        EditModeRules.OffersAdd(frequents: false, searching: false).ShouldBeTrue();
        EditModeRules.OffersAdd(frequents: true, searching: false).ShouldBeFalse();
        EditModeRules.OffersAdd(frequents: false, searching: true).ShouldBeFalse();
    }
}
