using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Application.Coordinators;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Surfaces.Panel;
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
    [Trait("Req", "VCO-001")]
    [Trait("Req", "AVI-002")]
    public void Under_a_Compact_panel_a_confirmation_lets_touches_through_and_an_undo_keeps_them()
    {
        // D21: the moment of «Ctrl+N enviado» never stands in the way of the app; [undo] keeps its button and a solid face.
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            var theme = Theme();
            var panel = Panel(time, new RecordingBodyIntents());
            var notice = new DockNoticeWindow(
                panel,
                testMode: null,
                new DockLabels(PanelTestData.Localization("es")),
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            )
            {
                ServesCompact = true,
            };
            try
            {
                panel.ApplyContext(
                    new PanelBodyContext(
                        Notice: new PanelNotice(
                            L.TapSent(keys: "Ctrl+N", app: "Word"),
                            new IconRef("check_circle"),
                            NoticeTone.Notice
                        )
                    )
                );
                notice.LetsTouchesThrough.ShouldBeTrue();

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
                notice.LetsTouchesThrough.ShouldBeFalse();

                notice.ServesCompact = false;
                notice.LetsTouchesThrough.ShouldBeFalse();
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
    [Trait("Req", "AVI-001")]
    public void Off_the_screen_only_a_confirmation_is_said_by_the_bar_so_nothing_is_said_twice()
    {
        // D21 review: [undo] was said by the bar while hidden and again by the notice window when it appeared.
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            var theme = Theme();
            var panel = Panel(time, new RecordingBodyIntents());
            var quiet = new LiveAnnouncer(new TextBlock());
            var notice = new DockNoticeWindow(
                panel,
                testMode: null,
                new DockLabels(PanelTestData.Localization("es")),
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            )
            {
                QuietAnnouncer = quiet,
            };
            try
            {
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
                quiet.Announcements.ShouldBe(0, "the window says it when it appears");

                panel.ApplyContext(
                    new PanelBodyContext(
                        Notice: new PanelNotice(
                            L.TapSent(keys: "Ctrl+N", app: "Word"),
                            new IconRef("check_circle"),
                            NoticeTone.Notice,
                            IsConfirmation: true
                        )
                    )
                );
                quiet.Announcements.ShouldBe(1, "a confirmation never shows in the Tab view");
                quiet.LastText.ShouldBe(notice.MessageText.Text);
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
    [Trait("Req", "ACC-001")]
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
                notice.AdminNotice.Message.ShouldContain("regedit");
                // ACC-001: its text is read as it is; a name on the card, a Border, never reached UI Automation.
                AutomationProperties.GetName(notice.AdminNotice).ShouldBeEmpty();
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
    [Trait("Req", "PES-009")]
    [Trait("Req", "AJR-001")]
    public void Quick_settings_beside_the_bar_has_the_same_size_closed_and_open()
    {
        // The window appears and goes away; a sheet that collapsed with the view model shrank it to its border for a
        // moment before it hid, and it was measured empty the first time (C:\dev\clicalo-qa\live).
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
                var closed = quick.MeasurePhysical(1.75);
                quick.Sheet.Visibility.ShouldBe(Visibility.Visible);
                quick.Sheet.TapTargets.ShouldBeEmpty("a closed sheet takes no touch");

                world.QuickSettings.Open();

                quick.MeasurePhysical(1.75).ShouldBe(closed);
            }
            finally
            {
                quick.Close();
                theme.Dispose();
            }
        });
    }

    [Fact]
    [Trait("Req", "PES-010")]
    [Trait("Req", "SEL-006")]
    public void The_profile_grid_beside_the_bar_is_laid_out_before_the_panel_opens_its_grid()
    {
        // The window beside the bar was measured with the grid of the panel still closed: it appeared 69 px high and
        // grew to 292 px a moment later (C:\dev\clicalo-qa\live).
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            var panel = Panel(time, new RecordingBodyIntents());
            panel.Picker.IsVisible.ShouldBeFalse();

            new PickerGridView(panel.Picker).Visibility.ShouldBe(Visibility.Collapsed);
            var beside = new PickerGridView(panel.Picker) { ShowsAlways = true };
            beside.Visibility.ShouldBe(Visibility.Visible);
            beside.TapTargets.ShouldNotBeEmpty();
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
    [InlineData(DockSide.Right)]
    [InlineData(DockSide.Left)]
    [InlineData(DockSide.Top)]
    [InlineData(DockSide.Bottom)]
    [Trait("Req", "PES-001")]
    [Trait("Req", "REG-02")]
    public void The_shadow_of_the_handle_follows_the_drawn_handle(DockSide side)
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
                // The window is 44 deep (REG-02) and, in direction C (D12), the handle is drawn as deep: the shadow
                // gets the shape of the handle, rounded toward the screen, with its shadow 0 6 20 at 35 %, and no inset.
                handle.ShadowShape.ShouldBe(SurfaceLook.DockHandle(side));
                handle.ShadowShape!.Shadow.ShouldNotBeNull();
                handle.ShadowInset.ShouldBe(new Thickness(0));
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

        public void ToggleArrows() { }

        public void TogglePinOpen() { }

        public void QuickSettings() { }

        public void ReleaseAll() { }
    }
}
