using System.Windows;
using System.Windows.Automation;
using Clicalo.Application.Coordinators;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Surfaces.TabView;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.Interactions;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.Theming;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Windowing.IntegrationTests.TabView;

/// <summary>
/// The surfaces M6 adds beside the bar of the Tab view, headless (built but never shown: no handle, no desktop, no
/// input): the notice surface (PES-014), Quick settings (PES-009), the menu of a shortcut (CUA-014, PES-010) and the
/// shadow of the handle (PES-001). Each is a non-activating surface of the registry (REG-01).
/// </summary>
public sealed class DockSideSurfacesTests
{
    [Fact]
    [Trait("Req", "PES-014")]
    [Trait("Req", "AVI-002")]
    [Trait("Req", "REG-06")]
    public void The_notice_surface_shows_the_notice_of_the_panel_with_undo_or_cancel()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            var theme = Theme();
            var localization = PanelTestData.Localization("es");
            var intents = new RecordingBodyIntents();
            var panel = Panel(time, intents);
            var notice = new DockNoticeWindow(
                panel,
                testMode: null,
                new DockLabels(localization),
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            try
            {
                // At rest the Tab view tells nothing: no «Listo» beside the handle.
                notice.Title.ShouldBe("Avisos");
                notice.Look.ShouldBe(SurfaceLook.SideWindow);
                notice.ShowsNotice.ShouldBeFalse();

                panel.ApplyContext(
                    new PanelBodyContext(
                        Notice: new PanelNotice(
                            L.Deleted,
                            new IconRef("delete"),
                            NoticeTone.Notice,
                            CanUndo: true
                        )
                    )
                );
                notice.ShowsNotice.ShouldBeTrue();
                notice.MessageText.Text.ShouldBe(localization.Current.Format(L.Deleted));
                notice.UndoButton.Visibility.ShouldBe(Visibility.Visible);
                notice.UndoButton.Content.ShouldBe("Deshacer");
                notice.CancelButton.Visibility.ShouldBe(Visibility.Collapsed);
                Click(notice.UndoButton);
                intents.Calls.ShouldBe([nameof(IPanelBodyIntents.Undo)]);

                // The capture of an app: a fixed notice with [cancel].
                panel.ApplyContext(
                    new PanelBodyContext(
                        Notice: new PanelNotice(
                            L.WaitingApp,
                            new IconRef("radar"),
                            NoticeTone.Notice,
                            CanCancel: true
                        )
                    )
                );
                notice.UndoButton.Visibility.ShouldBe(Visibility.Collapsed);
                notice.CancelButton.Visibility.ShouldBe(Visibility.Visible);
                Click(notice.CancelButton);
                intents.Calls.ShouldBe([
                    nameof(IPanelBodyIntents.Undo),
                    nameof(IPanelBodyIntents.CancelNotice),
                ]);

                // A surface that is not on screen announces nothing; it does when it appears.
                notice.Announcer.Announcements.ShouldBe(0);

                panel.ApplyContext(PanelBodyContext.Idle);
                notice.ShowsNotice.ShouldBeFalse();
            }
            finally
            {
                notice.Close();
                theme.Dispose();
            }
        });
    }

    [Fact]
    [Trait("Req", "PES-014")]
    [Trait("Req", "EJE-013")]
    public void The_notice_surface_shows_the_administrator_notice_with_its_button()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            var theme = Theme();
            var intents = new RecordingBodyIntents();
            var panel = Panel(time, intents);
            var notice = new DockNoticeWindow(
                panel,
                testMode: null,
                new DockLabels(PanelTestData.Localization("es")),
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            try
            {
                notice.AdminNotice.Visibility.ShouldBe(Visibility.Collapsed);

                panel.ApplyContext(new PanelBodyContext(ElevatedApp: "regedit"));

                notice.AdminNotice.Visibility.ShouldBe(Visibility.Visible);
                AutomationProperties.GetName(notice.AdminNotice).ShouldContain("regedit");
                notice.AdminNotice.TapTargets.ShouldHaveSingleItem().Tap();
                intents.Calls.ShouldBe([nameof(IPanelBodyIntents.RelaunchElevated)]);
                notice.ShowsNotice.ShouldBeFalse();
            }
            finally
            {
                notice.Close();
                theme.Dispose();
            }
        });
    }

    [Fact]
    [Trait("Req", "PES-009")]
    [Trait("Req", "AJR-005")]
    [Trait("Req", "REG-01")]
    public void Quick_settings_open_beside_the_bar_with_the_side_of_the_Tab_view()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            var theme = Theme();
            var world = new InteractionsWorld();
            var quick = new DockQuickWindow(
                world.QuickSettings,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            try
            {
                quick.Title.ShouldBe("Ajustes rápidos");
                quick.Look.ShouldBe(SurfaceLook.SideWindow);
                quick.ShowActivated.ShouldBeFalse();
                quick.Sheet.TapTargets.ShouldBeEmpty();

                // In the Tab view the sheet has the row «Lado de la pestaña» (DIS-22).
                world.QuickSettings.Views[2].Select();
                world.Settings.Density.ShouldBe(PanelDensity.Dock);
                world.QuickSettings.Open();
                world.QuickSettings.ShowsSides.ShouldBeTrue();
                quick.Sheet.TapTargets.ShouldNotBeEmpty();

                world.QuickSettings.Sides[0].Select();
                world.Settings.Dock.Side.ShouldBe(DockSide.Left);
            }
            finally
            {
                quick.Close();
                theme.Dispose();
            }
        });
    }

    [Fact]
    [Trait("Req", "CUA-014")]
    [Trait("Req", "PES-010")]
    [Trait("Req", "REG-01")]
    public void The_menu_of_a_shortcut_opens_in_a_window_of_its_own_with_its_rows()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            var theme = Theme();
            var world = new InteractionsWorld();
            var menu = new DockMenuWindow(
                world.Menu,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            try
            {
                menu.Look.ShouldBe(SurfaceLook.SideWindow);
                menu.ShowActivated.ShouldBeFalse();
                menu.Menu.TapTargets.ShouldBeEmpty();

                world.Menu.Open(
                    InteractionsWorld.Bold,
                    "Negrita",
                    "format_bold",
                    inFrequents: false
                );

                menu.Title.ShouldBe(world.Menu.AccessibleName);
                menu.Menu.TapTargets.Count().ShouldBe(world.Menu.Rows.Count);

                // [cancel], the last row, closes it.
                menu.Menu.TapTargets.Last().Tap();
                world.Menu.IsOpen.ShouldBeFalse();
                menu.Menu.TapTargets.ShouldBeEmpty();
            }
            finally
            {
                menu.Close();
                theme.Dispose();
            }
        });
    }

    [Theory]
    [InlineData(DockSide.Right, 12, 0, 0, 0)]
    [InlineData(DockSide.Left, 0, 0, 12, 0)]
    [InlineData(DockSide.Top, 0, 0, 0, 12)]
    [InlineData(DockSide.Bottom, 0, 12, 0, 0)]
    [Trait("Req", "PES-001")]
    [Trait("Req", "REG-02")]
    public void The_shadow_of_the_handle_follows_the_drawn_handle_not_its_touch_band(
        DockSide side,
        double left,
        double top,
        double right,
        double bottom
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            var theme = Theme();
            var controller = new PanelInteractionController(new PanelEngineInbox(), () => 1, time);
            var dock = new DockBarViewModel(
                controller,
                PanelTestData.Localization("es"),
                new NoIntents()
            );
            var handle = new DockHandleWindow(
                side,
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            try
            {
                // The window stays a plain 44-deep rectangle (REG-02); the shadow gets the shape of the handle, 32 deep
                // against the edge, with its shadow 0 6 20 at 35 %.
                handle.ShadowShape.ShouldBe(SurfaceLook.DockHandle(side));
                handle.ShadowShape!.Shadow.ShouldNotBeNull();
                handle.ShadowInset.ShouldBe(new Thickness(left, top, right, bottom));
            }
            finally
            {
                handle.Close();
                theme.Dispose();
            }
        });
    }

    private static ThemeService Theme() =>
        new(new FakeSystemTheme(), ThemeChoice.Dark, 100, reduceMotion: true);

    private static PanelViewModel Panel(TimeProvider time, IPanelBodyIntents intents)
    {
        var controller = new PanelInteractionController(new PanelEngineInbox(), () => 1, time);
        var panel = new PanelViewModel(
            controller,
            PanelTestData.Localization("es"),
            PanelDesktopFixture.Touch,
            _ => null,
            intents,
            keyLabelOf: null
        );
        panel.Apply(PanelProjector.Project(PanelTestData.Profile(), LangCode.Es, LangCode.Es));
        return panel;
    }

    private static void Click(System.Windows.Controls.Primitives.ButtonBase button) =>
        button.RaiseEvent(
            new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)
        );

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
