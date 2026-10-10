using Clicalo.Application.Coordinators;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.Windowing.IntegrationTests.Interactions;
using Clicalo.Windowing.IntegrationTests.SearchPanel;

namespace Clicalo.Windowing.IntegrationTests.TabView;

/// <summary>
/// <see cref="DockTileModes"/>: what a gesture on a shortcut of the bar or of «Pinned» does before the engine. The Tab
/// view follows the same rules as the tiles of the panel: the long press and the menu (CUA-014, PES-010) and the marks
/// of test mode (PES-014, TAC-008).
/// </summary>
public sealed class DockTileModesTests
{
    private readonly InteractionsWorld _world = new();
    private readonly DockTileModes _modes;
    private bool _inFrequents;

    public DockTileModesTests() =>
        _modes = new DockTileModes(_world.TestMode, _world.Menu, () => _inFrequents);

    [Fact]
    [Trait("Req", "CUA-014")]
    [Trait("Req", "PES-010")]
    [Trait("Req", "EJE-004")]
    public void A_shortcut_of_the_bar_has_a_long_press_except_a_Mantener_which_holds()
    {
        DockTileModes.KindOf(Tile(InteractionsWorld.Bold)).ShouldBe(TouchTargetKind.TapOrLongPress);
        DockTileModes
            .KindOf(Tile(InteractionsWorld.Dictate, TileBehavior.Toggle))
            .ShouldBe(TouchTargetKind.TapOrLongPress);
        DockTileModes
            .KindOf(Tile(InteractionsWorld.Dictation, TileBehavior.Hold))
            .ShouldBe(TouchTargetKind.Hold);
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    [Trait("Req", "PES-010")]
    public void A_long_press_opens_the_menu_of_the_shortcut_and_runs_nothing()
    {
        var tile = Tile(InteractionsWorld.Bold);

        _modes.LongPressed(tile).ShouldBeTrue();

        _world.Menu.IsOpen.ShouldBeTrue();
        _world.Menu.Shortcut.ShouldBe(InteractionsWorld.Bold);
        _world.Menu.Title.ShouldBe(tile.AccessibleName);
        _world.Engine.Events.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    public void In_Frequents_the_menu_offers_to_hide_the_shortcut()
    {
        _modes.OpenMenu(Tile(InteractionsWorld.Bold)).ShouldBeTrue();
        var rows = _world.Menu.Rows.Count;
        _world.Menu.Close();

        _inFrequents = true;
        _modes.OpenMenu(Tile(InteractionsWorld.Bold)).ShouldBeTrue();

        _world.Menu.Rows.Count.ShouldBe(rows + 1);
    }

    [Fact]
    [Trait("Req", "CUA-015")]
    public void The_secondary_action_opens_the_menu_of_a_Mantener_too()
    {
        _modes.OpenMenu(Tile(InteractionsWorld.Dictation, TileBehavior.Hold)).ShouldBeTrue();

        _world.Menu.IsOpen.ShouldBeTrue();
        _world.Menu.Shortcut.ShouldBe(InteractionsWorld.Dictation);
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    public void With_the_menu_open_a_tap_on_a_shortcut_only_closes_it()
    {
        var tile = Tile(InteractionsWorld.Bold);
        _ = _modes.OpenMenu(tile);

        _modes.Tapped(Tile(InteractionsWorld.Dictate)).ShouldBeTrue();

        _world.Menu.IsOpen.ShouldBeFalse();
        _modes.Tapped(tile).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    public void With_the_menu_open_a_tap_on_another_button_of_the_bar_only_closes_it()
    {
        _modes.TappedElsewhere().ShouldBeFalse();
        _ = _modes.OpenMenu(Tile(InteractionsWorld.Bold));

        _modes.TappedElsewhere().ShouldBeTrue();

        _world.Menu.IsOpen.ShouldBeFalse();
        _modes.TappedElsewhere().ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    public void Only_a_long_press_of_a_finger_lets_a_touch_outside_cancel_the_menu()
    {
        var tile = Tile(InteractionsWorld.Bold);
        _modes.MenuOpenedByFinger.ShouldBeFalse();

        _modes.LongPressed(tile, PointerKind.Finger).ShouldBeTrue();
        _modes.MenuOpenedByFinger.ShouldBeTrue();
        _world.Menu.Close();
        _modes.MenuOpenedByFinger.ShouldBeFalse();

        _modes.LongPressed(tile, PointerKind.Pen).ShouldBeTrue();
        _modes.MenuOpenedByFinger.ShouldBeFalse();
        _world.Menu.Close();

        _modes.LongPressed(tile, PointerKind.Finger).ShouldBeTrue();
        // A right click or the accessible secondary action opens it again: the pointer no longer cancels it.
        _modes.OpenMenu(tile).ShouldBeTrue();
        _modes.MenuOpenedByFinger.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PES-014")]
    [Trait("Req", "TAC-008")]
    public void In_test_mode_the_shortcuts_of_the_bar_are_marked_and_nothing_runs()
    {
        var tap = Tile(InteractionsWorld.Bold);
        var hold = Tile(InteractionsWorld.Dictation, TileBehavior.Hold);
        _modes.Tapped(tap).ShouldBeFalse();
        _modes.HoldStarted(hold).ShouldBeFalse();

        _world.TestMode.Start();

        _modes.Tapped(tap).ShouldBeTrue();
        _modes.HoldStarted(hold).ShouldBeTrue();
        _world.TestMode.MarkOf(tap.Id).ShouldBe(TestModeMark.Counted);
        _world.TestMode.MarkOf(hold.Id).ShouldBe(TestModeMark.Counted);

        var ignored = Tile(InteractionsWorld.Dictate);
        _modes.Ignored(ignored, IgnoreReason.TooShort);
        _world.TestMode.MarkOf(ignored.Id)!.Value.Accepted.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PES-014")]
    [Trait("Req", "TAC-008")]
    [Trait("Req", "CUA-014")]
    public void In_test_mode_a_long_press_is_marked_like_a_tap_and_opens_no_menu()
    {
        var tile = Tile(InteractionsWorld.Bold);
        _world.TestMode.Start();

        _modes.LongPressed(tile).ShouldBeFalse();

        _world.Menu.IsOpen.ShouldBeFalse();
        _world.TestMode.MarkOf(tile.Id).ShouldBe(TestModeMark.Counted);
    }

    private DockTileViewModel Tile(ShortcutId id, TileBehavior behavior = TileBehavior.Tap)
    {
        _world.Store.Current.Library.TryGetShortcut(id, out var shortcut).ShouldBeTrue();
        return new DockTileViewModel(
            new TileModel(
                id,
                shortcut.Name.Get(LangCode.Es, LangCode.Es),
                behavior,
                new TileBinding(shortcut, SearchTestWorld.Word, InjectionMode.VirtualKey),
                shortcut.Icon,
                shortcut.Category
            ),
            new PanelInteractionController(_world.Engine, () => 1, _world.Time)
        );
    }
}
