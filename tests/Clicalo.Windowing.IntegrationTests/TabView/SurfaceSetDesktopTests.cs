using System.Collections.Immutable;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Interaction;
using Clicalo.Application.Session;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Library;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Bubble;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Guides;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Theming;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Interactions;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.Theming;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
using Microsoft.Extensions.Time.Testing;
using SizeId = Clicalo.Domain.Catalog.PanelSize;

namespace Clicalo.Windowing.IntegrationTests.TabView;

/// <summary>
/// Every form of the panel on a real desktop (PAN-001, PAN-002, PAN-006, REG-01): the panel, the bubble, the handle and
/// the open bar with its side window appear passively, each whole inside the work area of its monitor, and none takes
/// the foreground. Nothing is injected: the forms change through the layout the composition would give.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
public sealed class SurfaceSetDesktopTests
{
    [DesktopFact]
    [Trait("Req", "PAN-001")]
    [Trait("Req", "PAN-006")]
    [Trait("Req", "REG-01")]
    public void Every_form_shows_passively_inside_the_work_area()
    {
        using var lab = SurfaceLab.Create();
        var (set, panel, theme, _, _) = WpfThread.Invoke(() => Build(lab));
        try
        {
            foreach (
                var form in new[]
                {
                    PanelForm.Full,
                    PanelForm.Bubble,
                    PanelForm.DockClosed,
                    PanelForm.DockOpen,
                }
            )
            {
                WpfThread.Invoke(() =>
                {
                    set.Apply(Layout(form));
                    if (form == PanelForm.Full)
                    {
                        panel.Present();
                    }
                });
                WpfThread.Invoke(WpfThread.DrainPendingWork);

                WpfThread.Invoke(() =>
                {
                    var shown = form switch
                    {
                        PanelForm.Full => (System.Windows.Window)panel,
                        PanelForm.Bubble => set.Bubble,
                        PanelForm.DockClosed => set.Handle(DockSide.Right),
                        _ => set.Bar(DockSide.Right),
                    };
                    shown.IsVisible.ShouldBeTrue(form.ToString());
                    var bounds = shown is TouchSurface surface
                        ? surface.ScreenBounds
                        : panel.ScreenBounds;
                    var monitor = PanelGeometry.MonitorOf(bounds, set.Monitors);
                    bounds.Left.ShouldBeGreaterThanOrEqualTo(
                        monitor.WorkArea.Left,
                        form.ToString()
                    );
                    bounds.Right.ShouldBeLessThanOrEqualTo(monitor.WorkArea.Right, form.ToString());
                    bounds.Top.ShouldBeGreaterThanOrEqualTo(monitor.WorkArea.Top, form.ToString());
                    bounds.Bottom.ShouldBeLessThanOrEqualTo(
                        monitor.WorkArea.Bottom,
                        form.ToString()
                    );
                });
            }

            lab.Guard.Violations.ShouldBe(0);
        }
        finally
        {
            WpfThread.Invoke(() =>
            {
                panel.Close();
                theme.Dispose();
            });
        }
    }

    [DesktopFact]
    [Trait("Req", "PES-014")]
    [Trait("Req", "PES-001")]
    [Trait("Req", "REG-01")]
    public void The_notice_surface_shows_beside_the_handle_or_the_bar_clear_of_the_other_windows()
    {
        using var lab = SurfaceLab.Create();
        var (set, panel, theme, model, _) = WpfThread.Invoke(() => Build(lab));
        try
        {
            WpfThread.Invoke(() =>
                model.ApplyContext(
                    new PanelBodyContext(
                        Notice: new PanelNotice(
                            Clicalo.Domain.Messages.L.ReleasedSwitch,
                            new IconRef("warning"),
                            NoticeTone.Warning
                        )
                    )
                )
            );
            WpfThread.Invoke(() =>
            {
                set.Apply(Layout(PanelForm.Full));
                panel.Present();
            });
            WpfThread.Invoke(WpfThread.DrainPendingWork);
            foreach (var form in new[] { PanelForm.DockClosed, PanelForm.DockOpen })
            {
                WpfThread.Invoke(() => set.Apply(Layout(form) with { ShowsNotice = true }));
                WpfThread.Invoke(WpfThread.DrainPendingWork);

                WpfThread.Invoke(() =>
                {
                    set.Notice.IsVisible.ShouldBeTrue(form.ToString());
                    var notice = set.Notice.ScreenBounds;
                    var anchor =
                        form == PanelForm.DockClosed
                            ? set.Handle(DockSide.Right).ScreenBounds
                            : set.Bar(DockSide.Right).ScreenBounds;
                    notice.Right.ShouldBeLessThanOrEqualTo(anchor.Left, form.ToString());
                    var monitor = PanelGeometry.MonitorOf(notice, set.Monitors);
                    notice.Left.ShouldBeGreaterThanOrEqualTo(monitor.WorkArea.Left);
                    notice.Bottom.ShouldBeLessThanOrEqualTo(monitor.WorkArea.Bottom);

                    // It keeps its 300 px and its text wraps inside them, whatever the notice says.
                    notice.Width.ShouldBe((int)Math.Ceiling(300 * monitor.Scale));
                    if (form == PanelForm.DockOpen)
                    {
                        var pinned = set.Flyout(DockFlyout.Pinned).ScreenBounds;
                        pinned.Width.ShouldBe((int)Math.Ceiling(230 * monitor.Scale));
                        var apart =
                            notice.Right <= pinned.Left
                            || pinned.Right <= notice.Left
                            || notice.Bottom <= pinned.Top
                            || pinned.Bottom <= notice.Top;
                        apart.ShouldBeTrue("the notice covers «Pinned»");
                    }
                    else
                    {
                        // PES-001: the handle has its shadow, around the 32 px it draws.
                        set.Handle(DockSide.Right).Shadow.ShouldNotBeNull();
                    }
                });
            }

            WpfThread.Invoke(() => set.Apply(Layout(PanelForm.DockClosed)));
            WpfThread.Invoke(WpfThread.DrainPendingWork);
            WpfThread.Invoke(() => set.Notice.IsVisible.ShouldBeFalse());
            lab.Guard.Violations.ShouldBe(0);
        }
        finally
        {
            WpfThread.Invoke(() =>
            {
                panel.Close();
                theme.Dispose();
            });
        }
    }

    [DesktopFact]
    [Trait("Req", "GEN-010")]
    [Trait("Req", "PES-005")]
    [Trait("Req", "PES-007")]
    public void Moving_the_open_bar_to_another_edge_ends_its_layout_with_one_bar_on_screen()
    {
        using var lab = SurfaceLab.Create();
        var (set, panel, theme, _, dock) = WpfThread.Invoke(() => Build(lab));
        try
        {
            // More shortcuts than fit along any edge, so the bar pages them by what it measures.
            WpfThread.Invoke(() =>
            {
                dock.ApplyTiles(ManyTiles(), []);
                set.Apply(Layout(PanelForm.Full));
                panel.Present();
            });
            WpfThread.Invoke(WpfThread.DrainPendingWork);
            foreach (
                var side in new[]
                {
                    DockSide.Right,
                    DockSide.Left,
                    DockSide.Top,
                    DockSide.Bottom,
                    DockSide.Left,
                    DockSide.Top,
                }
            )
            {
                // The order of the composition: the bar takes its new edge, then the surfaces are placed for it.
                var moved = WpfThread.Dispatcher.InvokeAsync(() =>
                {
                    dock.Apply(BarState(side));
                    set.Apply(
                        Layout(PanelForm.DockOpen, side, perPage: 8) with
                        {
                            Flyout = DockFlyout.None,
                        }
                    );
                });
                moved
                    .Wait(TimeSpan.FromSeconds(10))
                    .ShouldBe(
                        System.Windows.Threading.DispatcherOperationStatus.Completed,
                        $"the layout never ended on the {side} edge"
                    );
                WpfThread.Invoke(WpfThread.DrainPendingWork);

                WpfThread.Invoke(() =>
                {
                    var bar = set.Bar(side);
                    bar.IsVisible.ShouldBeTrue(side.ToString());
                    foreach (var other in Enum.GetValues<DockSide>().Where(edge => edge != side))
                    {
                        set.Bar(other).IsVisible.ShouldBeFalse($"{other} while on {side}");
                    }

                    // PES-005, PES-007: the shortcuts that fit whole, so the bar is never longer than its edge allows.
                    var bounds = bar.ScreenBounds;
                    var monitor = PanelGeometry.MonitorOf(bounds, set.Monitors);
                    var length = DockGeometry.IsVertical(side) ? bounds.Height : bounds.Width;
                    length.ShouldBeLessThanOrEqualTo(
                        DockGeometry.MaxBarLength(side, monitor),
                        side.ToString()
                    );
                    dock.HasPages.ShouldBeTrue(side.ToString());
                });
            }

            lab.Guard.Violations.ShouldBe(0);
        }
        finally
        {
            // Bounded: a layout that never ends must fail the test, not hang it.
            _ = WpfThread
                .Dispatcher.InvokeAsync(() =>
                {
                    panel.Close();
                    theme.Dispose();
                })
                .Wait(TimeSpan.FromSeconds(10));
        }
    }

    [DesktopFact]
    [Trait("Req", "PES-008")]
    [Trait("Req", "PES-009")]
    [Trait("Req", "PAN-001")]
    [Trait("Req", "REG-01")]
    public void The_arrows_after_Quick_settings_send_their_key_fold_and_leave_with_the_view()
    {
        // D21: the arrows had the id of Quick settings. Opened after it, as when the view was changed from Quick
        // settings, they threw inside the layout, showed without being a surface, took no tap and stayed on screen
        // in every other view.
        using var lab = SurfaceLab.Create();
        var world = new InteractionsWorld();
        var (set, panel, theme, _, dock) = WpfThread.Invoke(() => Build(lab, world));
        try
        {
            WpfThread.Invoke(() =>
            {
                set.Apply(Layout(PanelForm.Full));
                panel.Present();
            });
            Apply(
                set,
                Layout(PanelForm.DockOpen) with
                {
                    Flyout = DockFlyout.None,
                    ShowsQuick = true,
                }
            );
            var quick = WpfThread.Invoke(() => set.Quick.ShouldNotBeNull());
            WpfThread.Invoke(() => quick.IsVisible.ShouldBeTrue());

            Apply(set, Layout(PanelForm.DockOpen) with { Flyout = DockFlyout.Arrows });
            var arrows = WpfThread.Invoke(() => set.Flyout(DockFlyout.Arrows));
            WpfThread.Invoke(() =>
            {
                arrows.IsVisible.ShouldBeTrue();
                quick.IsVisible.ShouldBeFalse();
                lab.Registry.Surfaces.ShouldContain(arrows);
                lab.Registry.Surfaces.ShouldContain(quick);
            });

            // A tap on ↑ reaches the engine with its key, as a tap on a shortcut does.
            var up = WpfThread.Invoke(() =>
            {
                var key = arrows.TileControls[0];
                var center = key.PointToScreen(
                    new System.Windows.Point(key.ActualWidth / 2, key.ActualHeight / 2)
                );
                return new PhysicalPoint((int)center.X, (int)center.Y);
            });
            Tap(arrows, world.Time, up);
            var activation = world
                .Engine.Events.OfType<EngineEvent.Activation>()
                .ShouldHaveSingleItem();
            activation.Shortcut.Id.ShouldBe(dock.ArrowTiles[0].Id);
            activation
                .Shortcut.Action.ShouldBeOfType<TapAction>()
                .Chord.Strokes.ShouldHaveSingleItem()
                .Key.Value.ShouldBe("up");
            WpfThread.Invoke(() => arrows.IsVisible.ShouldBeTrue("the arrows stay while used"));

            // «Flechas» again folds them; open, any other view takes them away.
            Apply(set, Layout(PanelForm.DockOpen) with { Flyout = DockFlyout.None });
            WpfThread.Invoke(() => arrows.IsVisible.ShouldBeFalse("folded"));
            foreach (
                var form in new[]
                {
                    PanelForm.Full,
                    PanelForm.Compact,
                    PanelForm.DockClosed,
                    PanelForm.Bubble,
                }
            )
            {
                Apply(set, Layout(PanelForm.DockOpen) with { Flyout = DockFlyout.Arrows });
                WpfThread.Invoke(() => arrows.IsVisible.ShouldBeTrue(form.ToString()));
                Apply(set, Layout(form));
                WpfThread.Invoke(() => arrows.IsVisible.ShouldBeFalse(form.ToString()));
            }

            // And Quick settings still open beside the bar after them.
            Apply(
                set,
                Layout(PanelForm.DockOpen) with
                {
                    Flyout = DockFlyout.None,
                    ShowsQuick = true,
                }
            );
            WpfThread.Invoke(() => quick.IsVisible.ShouldBeTrue());
        }
        finally
        {
            WpfThread.Invoke(() =>
            {
                panel.Close();
                theme.Dispose();
            });
        }
    }

    /// <summary>Applies <paramref name="layout"/> and lets every pass it queued run.</summary>
    private static void Apply(SurfaceSet set, SurfaceLayout layout)
    {
        WpfThread.Invoke(() => set.Apply(layout));
        WpfThread.Invoke(WpfThread.DrainPendingWork);
    }

    /// <summary>A still finger down and up on <paramref name="at"/>, through the pointer layer of the surface.</summary>
    private static void Tap(TouchSurface surface, FakeTimeProvider time, PhysicalPoint at)
    {
        foreach (var phase in new[] { PointerPhase.Down, PointerPhase.Up })
        {
            var now = time.GetUtcNow();
            ImmutableArray<PointerSample> samples =
            [
                new PointerSample(
                    1,
                    PointerKind.Finger,
                    phase,
                    at,
                    new PhysicalRect(at.X - 5, at.Y - 5, 10, 10),
                    now,
                    PointerInputOrigin.Injected
                ),
            ];
            WpfThread.Invoke(() => surface.OnFrame(new PointerFrame(1, now, samples)));
            time.Advance(TimeSpan.FromMilliseconds(120));
        }

        time.Advance(TimeSpan.FromMilliseconds(500));
        WpfThread.Invoke(WpfThread.DrainPendingWork);
    }

    private static ImmutableArray<TileModel> ManyTiles()
    {
        var tile = PanelProjector.Project(PanelTestData.Profile(), LangCode.Es, LangCode.Es).Tiles[
            0
        ];
        return
        [
            .. Enumerable
                .Range(0, 24)
                .Select(i =>
                    tile with
                    {
                        Id = new ShortcutId(FormattableString.Invariant($"many-{i}")),
                        Name = FormattableString.Invariant($"Atajo {i}"),
                    }
                ),
        ];
    }

    [DesktopFact]
    [Trait("Req", "PES-008")]
    [Trait("Req", "PES-001")]
    public void The_arrows_open_beside_the_bar_and_go_away_with_it_whatever_view_comes_next()
    {
        // D21 review: the window of the arrows stayed on screen after the view changed, and the bar did not fold.
        using var lab = SurfaceLab.Create();
        var (set, panel, theme, _, dock) = WpfThread.Invoke(() => Build(lab));
        try
        {
            WpfThread.Invoke(() =>
            {
                set.Apply(Layout(PanelForm.Full));
                panel.Present();
            });
            WpfThread.Invoke(WpfThread.DrainPendingWork);
            foreach (
                var next in new[]
                {
                    PanelForm.Full,
                    PanelForm.Compact,
                    PanelForm.DockClosed,
                    PanelForm.Bubble,
                }
            )
            {
                WpfThread.Invoke(() =>
                {
                    dock.Apply(BarState(DockSide.Right) with { Flyout = DockFlyout.Arrows });
                    set.Apply(Layout(PanelForm.DockOpen) with { Flyout = DockFlyout.Arrows });
                });
                WpfThread.Invoke(WpfThread.DrainPendingWork);
                WpfThread.Invoke(() =>
                {
                    var arrows = set.Flyout(DockFlyout.Arrows);
                    arrows.IsVisible.ShouldBeTrue("the arrows open");
                    arrows.TileControls.Count.ShouldBe(4);
                    arrows
                        .CurrentTargets()
                        .Count.ShouldBeGreaterThanOrEqualTo(4, "every arrow takes a touch");
                });

                WpfThread.Invoke(() =>
                {
                    dock.Apply(BarState(DockSide.Right) with { Flyout = DockFlyout.None });
                    set.Apply(Layout(next));
                });
                WpfThread.Invoke(WpfThread.DrainPendingWork);
                WpfThread.Invoke(() =>
                    set.Flyout(DockFlyout.Arrows).IsVisible.ShouldBeFalse(next.ToString())
                );
            }

            lab.Guard.Violations.ShouldBe(0);
        }
        finally
        {
            WpfThread.Invoke(() =>
            {
                panel.Close();
                theme.Dispose();
            });
        }
    }

    private static DockBarState BarState(DockSide side) =>
        new(
            Layout(PanelForm.DockOpen, side, perPage: 8).Dock,
            PanelSizes.Get(SizeId.M),
            ShowStripRow: false,
            StickyRow: false,
            VoiceNumbers: false,
            Frequents: false,
            ProfileName: "Word",
            ProfileIcon: "description",
            IsActiveApp: false,
            IsFixed: false,
            CanRepeat: false,
            DockFlyout.None,
            BarOpen: true,
            AnythingHeld: false
        );

    private static SurfaceLayout Layout(
        PanelForm form,
        DockSide side = DockSide.Right,
        int perPage = 5
    ) =>
        new(
            form,
            new DockSettings
            {
                Side = side,
                HandlePositions = new DockHandlePositions(50, 50, 50, 50),
                HandleLocked = false,
                PinOpen = false,
                Gutter = true,
                PerPage = perPage,
                CoachDone = true,
            },
            [],
            PanelSizes.Get(SizeId.M),
            Panic: false,
            form == PanelForm.DockOpen ? DockFlyout.Pinned : DockFlyout.None,
            ShowsCoach: false
        );

    private static (
        SurfaceSet Set,
        PanelWindow Panel,
        ThemeService Theme,
        PanelViewModel Model,
        DockBarViewModel Dock
    ) Build(SurfaceLab lab, InteractionsWorld? world = null)
    {
        // With a world: its clock and its engine, and the layers beside the bar (Quick settings, the menu, test mode).
        TimeProvider time = world is null ? TimeProvider.System : world.Time;
        var theme = new ThemeService(
            new FakeSystemTheme(),
            ThemeChoice.Dark,
            100,
            reduceMotion: true
        );
        var localization = PanelTestData.Localization("es");
        var controller = new PanelInteractionController(
            world?.Engine ?? new PanelEngineInbox(),
            () => 1,
            time
        );
        var viewModel = new PanelViewModel(
            controller,
            localization,
            PanelDesktopFixture.Touch,
            _ => null
        );
        var model = PanelProjector.Project(PanelTestData.Profile(), LangCode.Es, LangCode.Es);
        viewModel.Apply(model);

        // The panel is not hidden from the tray: its window shows in the Full view.
        viewModel.ApplySession(new PanelSession(PanelPresence.Visible, PanelTestData.Word, 1));
        var panel = new PanelWindow(
            viewModel,
            lab.Registry,
            time,
            theme,
            new DimSettings(AutoDim: false, Opacity: 1, DimTo: 1)
        );
        var store = new InteractionStore(InteractionState.Initial, time);
        var layers = world is null
            ? null
            : new PanelLayerModels(
                world.QuickSettings,
                world.EditMode,
                world.Menu,
                world.TestMode,
                world.Modes,
                static () => false
            );
        var dock = new DockBarViewModel(controller, localization, new SilentIntents(), layers);
        dock.ApplyTiles(model.Tiles, [model.Tiles[0]]);
        var bubble = new BubbleViewModel(localization, () => { }, () => { });
        var set = new SurfaceSet(
            panel,
            viewModel,
            dock,
            bubble,
            new GuideViewModel(PanelTestData.Localization(), static _ => { }),
            new SurfaceDimming(store, panel.DimSettings),
            lab.Registry,
            theme,
            time,
            PanelDesktopFixture.Touch,
            new SurfaceCallbacks(
                () => { },
                static (_, _, _) => { },
                _ => { },
                (_, _, _) => { },
                (_, _) => { },
                _ => { }
            ),
            layers
        );
        return (set, panel, theme, viewModel, dock);
    }

    private sealed class SilentIntents : IDockIntents
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

        public void ToggleArrows() { }

        public void TogglePinOpen() { }

        public void QuickSettings() { }

        public void ReleaseAll() { }
    }
}
