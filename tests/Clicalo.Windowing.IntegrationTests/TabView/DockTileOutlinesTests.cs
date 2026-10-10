using System.Windows;
using System.Windows.Controls;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Surfaces.Panel;
using Clicalo.UI.Wpf.Surfaces.TabView;
using Clicalo.UI.Wpf.Theming;
using Clicalo.Windowing.IntegrationTests.Interactions;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.Theming;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
using Microsoft.Extensions.Time.Testing;
using SizeId = Clicalo.Domain.Catalog.PanelSize;

namespace Clicalo.Windowing.IntegrationTests.TabView;

/// <summary>
/// The bar of the Tab view and the windows beside it answer like the panel: the shortcut armed for its confirmation
/// tap shows its warn outline (EJE-002) and an ignored touch its slight one (TAC-003).
/// </summary>
public sealed class DockTileOutlinesTests
{
    private readonly InteractionsWorld _world = new();

    [Fact]
    [Trait("Req", "EJE-002")]
    [Trait("Req", "ACC-003")]
    public void The_armed_shortcut_of_the_bar_and_of_Pinned_is_marked_and_says_it_waits()
    {
        var dock = Dock();

        dock.ApplyEngine(
            EngineSnapshot.Empty with
            {
                Armed = new ArmedConfirmation(PanelTestData.Copy, DateTimeOffset.UnixEpoch),
                Version = 1,
            }
        );

        var inBar = dock.PageTiles.Single(static t => t.Id == PanelTestData.Copy);
        inBar.IsArmed.ShouldBeTrue();
        inBar.AccessibleState.ShouldBe("Toca otra vez para confirmar");
        var pinned = dock.PinnedTiles.Single(static t => t.Id == PanelTestData.Copy);
        pinned.IsArmed.ShouldBeTrue();
        dock.PageTiles.Where(static t => t.Id != PanelTestData.Copy)
            .ShouldAllBe(static t => !t.IsArmed);

        dock.ApplyEngine(EngineSnapshot.Empty with { Version = 2 });

        inBar.IsArmed.ShouldBeFalse();
        inBar.AccessibleState.ShouldBeEmpty();
        pinned.IsArmed.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "TAC-003")]
    public void An_ignored_touch_on_the_bar_asks_for_its_discreet_answer()
    {
        var dock = Dock();
        var modes = dock.Modes.ShouldNotBeNull();
        var answered = new List<ShortcutId>();
        modes.IgnoredFeedback = tile => answered.Add(tile.Id);
        var tile = dock.PageTiles[0];

        modes.Ignored(tile, IgnoreReason.Debounced);
        modes.Ignored(tile, IgnoreReason.Moved);
        modes.Ignored(tile, IgnoreReason.TooShort);

        answered.ShouldBe([tile.Id, tile.Id, tile.Id]);
    }

    [Fact]
    [Trait("Req", "TAC-003")]
    [Trait("Req", "TAC-008")]
    public void A_palm_and_test_mode_get_no_such_answer_on_the_bar()
    {
        var dock = Dock();
        var modes = dock.Modes.ShouldNotBeNull();
        var answered = 0;
        modes.IgnoredFeedback = _ => answered++;
        var tile = dock.PageTiles[0];

        modes.Ignored(tile, IgnoreReason.Palm);
        _world.TestMode.Start();
        modes.Ignored(tile, IgnoreReason.Debounced);

        answered.ShouldBe(0);
        _world.TestMode.MarkOf(tile.Id).ShouldNotBeNull();
    }

    [Fact]
    [Trait("Req", "EJE-002")]
    [Trait("Req", "TAC-003")]
    public void The_bar_draws_the_warn_outline_and_the_outline_of_an_ignored_touch()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var dock = Dock();
            var bar = new DockBarWindow(
                DockSide.Right,
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static (_, _, _) => { }
            );
            try
            {
                var copy = dock.PageTiles.Single(static t => t.Id == PanelTestData.Copy);
                var control = bar.TileControls[dock.PageTiles.IndexOf(copy)];
                control.IsArmed.ShouldBeFalse();
                var outline = OutlineOf(control);
                outline.Visibility.ShouldBe(Visibility.Collapsed);
                outline.IsHitTestVisible.ShouldBeFalse();

                dock.ApplyEngine(
                    EngineSnapshot.Empty with
                    {
                        Armed = new ArmedConfirmation(copy.Id, DateTimeOffset.UnixEpoch),
                        Version = 1,
                    }
                );
                control.IsArmed.ShouldBeTrue();
                bar.TileControls.Count(static t => t.IsArmed).ShouldBe(1);

                copy.ShowIgnored(true);
                outline.Visibility.ShouldBe(Visibility.Visible);
                copy.ShowIgnored(false);
                outline.Visibility.ShouldBe(Visibility.Collapsed);
            }
            finally
            {
                bar.Close();
            }
        });
    }

    [Fact]
    [Trait("Req", "EJE-002")]
    [Trait("Req", "TAC-003")]
    [Trait("Req", "PES-010")]
    public void The_Pinned_window_draws_both_outlines_too()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var dock = Dock(DockFlyout.Pinned);
            var panel = new PanelViewModel(
                new PanelInteractionController(_world.Engine, () => 1, _world.Time),
                _world.Localization,
                PanelDesktopFixture.Touch,
                _ => null
            );
            var window = new DockFlyoutWindow(
                DockFlyout.Pinned,
                dock,
                panel,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static (_, _, _) => { }
            );
            try
            {
                var pinned = dock.PinnedTiles.ShouldHaveSingleItem();
                var control = window.TileControls.ShouldHaveSingleItem();
                var outline = OutlineOf(control);

                dock.ApplyEngine(
                    EngineSnapshot.Empty with
                    {
                        Armed = new ArmedConfirmation(pinned.Id, DateTimeOffset.UnixEpoch),
                        Version = 1,
                    }
                );
                pinned.ShowIgnored(true);

                control.IsArmed.ShouldBeTrue();
                outline.Visibility.ShouldBe(Visibility.Visible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    [Trait("Req", "TAC-004")]
    [Trait("Req", "EJE-004")]
    public void While_Pinned_scrolls_its_shortcuts_wait_to_see_that_the_finger_is_not_scrolling()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var dock = Dock(DockFlyout.Pinned, pinEverything: true);
            var panel = new PanelViewModel(
                new PanelInteractionController(_world.Engine, () => 1, _world.Time),
                _world.Localization,
                PanelDesktopFixture.Touch,
                _ => null
            );
            var window = new DockFlyoutWindow(
                DockFlyout.Pinned,
                dock,
                panel,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static (_, _, _) => { }
            );
            try
            {
                // Everything fits: nothing scrolls, and a Mantener holds as anywhere else.
                Layout(window, height: 2_000);
                window.Scroller.ScrollableHeight.ShouldBe(0);
                var tiles = window.CurrentTargets().Where(static t => t.Tile is not null).ToList();
                tiles.Count.ShouldBe(4);
                tiles.ShouldContain(static t => t.Kind == TouchTargetKind.Hold);
                tiles.ShouldAllBe(static t => !t.InScrollZone);

                // Taller than it may be: the window scrolls under the finger.
                Layout(window, height: 60);
                window.Scroller.ScrollableHeight.ShouldBeGreaterThan(0);
                window
                    .CurrentTargets()
                    .Where(static t => t.Tile is not null)
                    .ShouldAllBe(static t => t.InScrollZone);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void Layout(DockFlyoutWindow window, double height)
    {
        window.Scroller.Measure(new Size(260, height));
        window.Scroller.Arrange(new Rect(0, 0, 260, height));
        window.Scroller.UpdateLayout();
    }

    private static IgnoredTouchOutline OutlineOf(FrameworkElement tile) =>
        tile
            .Parent.ShouldBeOfType<Grid>()
            .Children.OfType<IgnoredTouchOutline>()
            .ShouldHaveSingleItem();

    private static ThemeService Theme() =>
        new(new FakeSystemTheme(), ThemeChoice.Dark, 100, reduceMotion: true);

    private DockBarViewModel Dock(DockFlyout flyout = DockFlyout.None, bool pinEverything = false)
    {
        var dock = new DockBarViewModel(
            new PanelInteractionController(_world.Engine, () => 1, _world.Time),
            _world.Localization,
            new NoIntents(),
            new PanelLayerModels(
                _world.QuickSettings,
                _world.EditMode,
                _world.Menu,
                _world.TestMode,
                _world.Modes,
                static () => false
            )
        );
        var model = PanelProjector.Project(PanelTestData.Profile(), LangCode.Es, LangCode.Es);
        dock.ApplyTiles(model.Tiles, pinEverything ? model.Tiles : [model.Tiles[0]]);
        dock.Apply(
            new DockBarState(
                new DockSettings
                {
                    Side = DockSide.Right,
                    HandlePositions = new DockHandlePositions(50, 50, 50, 50),
                    HandleLocked = false,
                    PinOpen = false,
                    Gutter = false,
                    PerPage = 8,
                    CoachDone = true,
                },
                PanelSizes.Get(SizeId.M),
                ShowStripRow: true,
                StickyRow: false,
                VoiceNumbers: false,
                Frequents: false,
                ProfileName: "Word",
                ProfileIcon: "description",
                IsActiveApp: false,
                IsFixed: false,
                CanRepeat: false,
                flyout,
                CoachStep: 0,
                BarOpen: true,
                AnythingHeld: false
            )
        );
        return dock;
    }

    private sealed class NoIntents : IDockIntents
    {
        public void OpenBar() { }

        public void MoveHandle(int percent) { }

        public void CloseBar() { }

        public void Expand() { }

        public void ShowFrequents() { }

        public void ProfileButton() { }

        public void ToggleLock() { }

        public void Search() { }

        public void Repeat() { }

        public void TogglePinned() { }

        public void ToggleSticky() { }

        public void TogglePinOpen() { }

        public void QuickSettings() { }

        public void CoachNext() { }

        public void CoachSkip() { }

        public void ReleaseAll() { }
    }
}
