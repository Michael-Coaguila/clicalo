using Clicalo.Application.Coordinators;
using Clicalo.Application.Interaction;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Bubble;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Theming;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.Theming;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
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
        var (set, panel, theme) = WpfThread.Invoke(() => Build(lab));
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

    private static SurfaceLayout Layout(PanelForm form) =>
        new(
            form,
            new DockSettings
            {
                Side = DockSide.Right,
                HandlePositions = new DockHandlePositions(50, 50, 50, 50),
                HandleLocked = false,
                PinOpen = false,
                Gutter = true,
                PerPage = 5,
                CoachDone = true,
            },
            [],
            PanelSizes.Get(SizeId.M),
            Panic: false,
            form == PanelForm.DockOpen ? DockFlyout.Pinned : DockFlyout.None,
            ShowsCoach: false
        );

    private static (SurfaceSet Set, PanelWindow Panel, ThemeService Theme) Build(SurfaceLab lab)
    {
        var time = TimeProvider.System;
        var theme = new ThemeService(
            new FakeSystemTheme(),
            ThemeChoice.Dark,
            100,
            reduceMotion: true
        );
        var localization = PanelTestData.Localization("es");
        var controller = new PanelInteractionController(new PanelEngineInbox(), () => 1, time);
        var viewModel = new PanelViewModel(
            controller,
            localization,
            PanelDesktopFixture.Touch,
            _ => null
        );
        var model = PanelProjector.Project(PanelTestData.Profile(), LangCode.Es, LangCode.Es);
        viewModel.Apply(model);
        var panel = new PanelWindow(
            viewModel,
            lab.Registry,
            time,
            theme,
            new DimSettings(AutoDim: false, Opacity: 1, DimTo: 1)
        );
        var store = new InteractionStore(InteractionState.Initial, time);
        var dock = new DockBarViewModel(controller, localization, new SilentIntents());
        dock.ApplyTiles(model.Tiles, [model.Tiles[0]]);
        var bubble = new BubbleViewModel(localization, () => { }, () => { });
        var set = new SurfaceSet(
            panel,
            viewModel,
            dock,
            bubble,
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
                () => { }
            )
        );
        return (set, panel, theme);
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

        public void TogglePinOpen() { }

        public void QuickSettings() { }

        public void CoachNext() { }

        public void CoachSkip() { }

        public void ReleaseAll() { }
    }
}
