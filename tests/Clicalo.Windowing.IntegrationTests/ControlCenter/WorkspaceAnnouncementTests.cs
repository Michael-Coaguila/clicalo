using Clicalo.Application.Confirmation;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Library;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.ControlCenter;
using Clicalo.Presentation.ControlCenter.General;
using Clicalo.Presentation.ControlCenter.TouchPrecision;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Workspace;
using Clicalo.UI.Wpf.Workspace.General;
using Clicalo.UI.Wpf.Workspace.TouchPrecision;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// What the views of the Control Center say to a screen reader, headless (ACC-001, REG-06): a line that keeps its
/// element is a live region that announces each change, and a line drawn again on every render is said by the window,
/// once each time it appears or changes. That a UIA client hears it is <c>LiveRegionTests</c> (desktop).
/// </summary>
public sealed class WorkspaceAnnouncementTests
{
    private readonly ControlCenterTestWorld _world = new();

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "TAC-006")]
    public void The_line_of_the_touch_test_reads_out_each_new_result()
    {
        var section = new TouchPrecisionViewModel(
            new TouchPrecisionServices(
                _world.Store,
                _world.Localization,
                _world.Time,
                action => action()
            )
        );
        WpfThread.Invoke(() =>
        {
            var view = new TouchPrecisionView(section);
            section.PlaceTargets(
                [new PhysicalRect(0, 0, 100, 200), new PhysicalRect(124, 0, 100, 200)],
                1
            );
            var announcer = view.MessageAnnouncer;
            announcer.Announcements.ShouldBe(0, "the line at rest is not read out");

            Tap(section, new PhysicalPoint(50, 100), 50);
            announcer.Announcements.ShouldBe(1);
            announcer.LastText.ShouldBe(section.Screen.Test.Message);
            announcer.LastUrgency.ShouldBe(AnnouncementUrgency.Polite);

            _world.Time.Advance(TimeSpan.FromSeconds(1));
            var at = new PhysicalPoint(50, 50);
            section.TestContact(1, PointerPhase.Down, at);
            _world.Time.Advance(TimeSpan.FromMilliseconds(50));
            section.TestContact(1, PointerPhase.Move, at with { Y = 100 });
            section.TestContact(1, PointerPhase.Up, at with { Y = 100 });
            announcer.Announcements.ShouldBe(2);
            announcer.LastText.ShouldBe("Ignorado: deslizaste");
            view.Detach();
        });
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "REG-04")]
    [Trait("Req", "GEN-013")]
    public void A_button_armed_by_its_first_tap_says_once_that_it_waits_for_the_second()
    {
        var section = new GeneralSectionViewModel(
            new GeneralServices(
                _world.Store,
                _world.Localization,
                new TwoStepConfirm(_world.Time),
                _world.Time,
                action => action(),
                () => { }
            )
        );
        var said = new List<(string Text, AnnouncementUrgency Urgency)>();
        WpfThread.Invoke(() =>
        {
            var view = new GeneralSectionView(
                section,
                (text, urgency) => said.Add((text, urgency))
            );
            said.ShouldBeEmpty("nothing is armed at rest");

            section.ResetFrequents();
            WpfThread.DrainPendingWork();
            var access = section.Screen.Access;
            access.ResetArmed.ShouldBeTrue();
            said.ShouldBe([(access.ResetTitle, AnnouncementUrgency.Polite)]);

            // Drawn again while it stays armed: said once.
            section.Refresh();
            WpfThread.DrainPendingWork();
            said.Count.ShouldBe(1);
            view.Detach();
        });
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "REG-04")]
    public void The_window_says_the_lines_of_the_section_on_show_only()
    {
        var (window, theme, viewModel) = WpfThread.Invoke(() =>
        {
            var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                100,
                reduceMotion: true
            );
            _world.Shortcuts.Open(new ListRef.InProfile(ControlCenterTestWorld.Word), null, false);
            var viewModel = new ControlCenterViewModel(_world.Services, () => { });
            var window = new ControlCenterWindow(viewModel, theme);
            WpfThread.DrainPendingWork();
            return (window, theme, viewModel);
        });
        try
        {
            WpfThread.Invoke(() =>
            {
                var announcer = window.Announcer;
                announcer.Region.ShouldBeSameAs(window);
                var before = announcer.Announcements;

                // «General y panel» is not on show: its armed button is drawn, but a section out of view says nothing.
                viewModel.Select(ControlCenterSection.Shortcuts);
                viewModel.General.ResetFrequents();
                WpfThread.DrainPendingWork();
                viewModel.General.Screen.Access.ResetArmed.ShouldBeTrue();
                announcer.Announcements.ShouldBe(before);

                viewModel.Select(ControlCenterSection.Panel);
                _world.Time.Advance(TimeSpan.FromMinutes(1));
                WpfThread.DrainPendingWork();
                viewModel.General.Screen.Access.ResetArmed.ShouldBeFalse("it disarms by itself");
                viewModel.General.ResetFrequents();
                WpfThread.DrainPendingWork();
                announcer.Announcements.ShouldBe(before + 1);
                announcer.LastText.ShouldBe(viewModel.General.Screen.Access.ResetTitle);
            });
        }
        finally
        {
            WpfThread.Invoke(() =>
            {
                window.Destroy();
                theme.Dispose();
            });
        }
    }

    private void Tap(TouchPrecisionViewModel section, PhysicalPoint at, int holdMilliseconds)
    {
        section.TestContact(1, PointerPhase.Down, at);
        _world.Time.Advance(TimeSpan.FromMilliseconds(holdMilliseconds));
        section.TestContact(1, PointerPhase.Up, at);
    }
}
