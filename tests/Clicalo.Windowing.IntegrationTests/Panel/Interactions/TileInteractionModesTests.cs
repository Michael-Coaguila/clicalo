using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel;

namespace Clicalo.Windowing.IntegrationTests.Interactions;

/// <summary>
/// What a gesture on a tile does before the engine (EJE-001 steps 2 and 3, CUA-012, CUA-014, TAC-008): edit mode first,
/// then test mode; the long press only outside edit mode and never on a Hold.
/// </summary>
public sealed class TileInteractionModesTests
{
    private readonly InteractionsWorld _world = new();

    [Fact]
    [Trait("Req", "CUA-014")]
    [Trait("Req", "EJE-004")]
    [Trait("Req", "CUA-012")]
    public void A_tile_has_a_long_press_except_a_hold_and_everything_is_a_tap_in_edit_mode()
    {
        var tap = _world.Tile(InteractionsWorld.Bold);
        var hold = _world.Tile(InteractionsWorld.Dictation, TileBehavior.Hold);
        var toggle = _world.Tile(InteractionsWorld.Dictate, TileBehavior.Toggle);

        _world.Modes.KindOf(tap).ShouldBe(TouchTargetKind.TapOrLongPress);
        _world.Modes.KindOf(toggle).ShouldBe(TouchTargetKind.TapOrLongPress);
        _world.Modes.KindOf(hold).ShouldBe(TouchTargetKind.Hold);

        _world.EditMode.Enter();

        _world.Modes.KindOf(tap).ShouldBe(TouchTargetKind.Tap);
        _world.Modes.KindOf(hold).ShouldBe(TouchTargetKind.Tap);
    }

    [Fact]
    [Trait("Req", "EJE-001")]
    public void Outside_both_modes_the_tile_runs_and_edit_mode_comes_before_test_mode()
    {
        var tile = _world.Tile(InteractionsWorld.Bold);

        _world.Modes.Tapped(tile).ShouldBeFalse();

        _world.TestMode.Start();
        _world.EditMode.Enter();
        _world.Modes.Tapped(tile).ShouldBeTrue();
        _world.ControlCenter.Requests.ShouldBe(["editor:bold"]);
        _world.TestMode.MarkOf(tile.Id).ShouldBeNull();

        _world.Modes.Ignored(tile, IgnoreReason.TooShort);
        _world.TestMode.MarkOf(tile.Id).ShouldBeNull();

        _world.EditMode.Exit();
        _world.Modes.Tapped(tile).ShouldBeTrue();
        _world
            .Modes.HoldStarted(_world.Tile(InteractionsWorld.Dictation, TileBehavior.Hold))
            .ShouldBeTrue();
        _world.TestMode.MarkOf(tile.Id).ShouldBe(TestModeMark.Counted);
        _world.TestMode.MarkOf(InteractionsWorld.Dictation).ShouldBe(TestModeMark.Counted);
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    [Trait("Req", "CUA-015")]
    public void The_menu_opens_from_any_tile_outside_edit_mode_and_a_tap_closes_it()
    {
        var hold = _world.Tile(InteractionsWorld.Dictation, TileBehavior.Hold);

        _world.Modes.OpenMenu(hold, inFrequents: false).ShouldBeTrue();
        _world.Menu.Title.ShouldBe("Dictado");
        _world.Modes.Tapped(_world.Tile(InteractionsWorld.Bold));
        _world.Menu.IsOpen.ShouldBeFalse();

        _world.EditMode.Enter();
        _world.Modes.OpenMenu(hold, inFrequents: false).ShouldBeFalse();
        _world.Menu.IsOpen.ShouldBeFalse();
    }
}
