using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Platform.Windows.Tray;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Desktop;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The tray of M2 with InputProbe standing in for Notepad (blueprint §14, M2 exit criterion «prueba de bandeja»): with
/// the app in front, the menu opens under a <c>TrayMenu</c> lease, an entry is chosen the way Voice access or Narrator
/// choose it (UI Automation, no key injected), and the foreground goes back to the app before the command runs:
/// «Soltar todo» reaches the engine as <see cref="EngineEvent.ReleaseAll"/>, «Ocultar panel» hides the panel and
/// releases everything. The click on the notification area icon is replaced by a synthetic touch on the panel, which
/// gives the process the same foreground right (blueprint §3.6, ladder step 1).
/// </summary>
[Trait("Requires", "Desktop")]
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Req", "BUR-003")]
[Trait("Req", "REG-01")]
public sealed class TrayDesktopTests(PanelDesktopFixture fixture)
    : IClassFixture<PanelDesktopFixture>
{
    private const string PopupMenuClass = "#32768";

    [DesktopFact]
    [Trait("Req", "SEG-003")]
    public async Task Release_all_from_the_tray_menu_gives_the_foreground_back_to_the_probe()
    {
        await PrepareWithRightsAsync();
        await fixture.Tray.UpdateStateAsync(panelVisible: true, anythingHeld: true);

        var command = await ChooseAsync("Soltar todo");

        command.ShouldBe(TrayCommand.ReleaseAll);
        fixture
            .Engine.Events.ShouldHaveSingleItem()
            .ShouldBe(new EngineEvent.ReleaseAll(ReleaseReason.User));
        fixture.Probe.IsForeground.ShouldBeTrue(
            "the lease gives the foreground back before the command runs"
        );
        fixture.Lab.Arbiter.Violations.ShouldBeEmpty();
    }

    [DesktopFact]
    [Trait("Req", "SEG-007")]
    public async Task Hiding_from_the_tray_menu_hides_the_panel_releases_everything_and_keeps_the_app_in_front()
    {
        await PrepareWithRightsAsync();
        await fixture.Tray.UpdateStateAsync(panelVisible: true, anythingHeld: false);
        EventHandler toggle = (_, _) => WpfThread.Dispatcher.Invoke(fixture.Visibility.Toggle);
        fixture.Tray.ShowHideRequested += toggle;
        try
        {
            var command = await ChooseAsync("Ocultar panel", disabled: "Soltar todo");

            command.ShouldBe(TrayCommand.ShowHide);
            WpfThread.Invoke(WpfThread.DrainPendingWork);
            WpfThread.Invoke(() => fixture.Window.IsVisible).ShouldBeFalse();
            fixture
                .Engine.Events.ShouldHaveSingleItem()
                .ShouldBe(new EngineEvent.Terminal(TerminalReason.Hide));
            fixture.Probe.IsForeground.ShouldBeTrue();
        }
        finally
        {
            fixture.Tray.ShowHideRequested -= toggle;
            WpfThread.Invoke(fixture.Visibility.Show);
            WpfThread.Invoke(WpfThread.DrainPendingWork);
        }

        fixture.Probe.IsForeground.ShouldBeTrue();
    }

    /// <summary>The probe in front, and this process holding the foreground right a tray click would give it.</summary>
    private async Task PrepareWithRightsAsync()
    {
        _ = await fixture.PrepareAsync();
        var at = fixture.TileCenter(PanelTestData.Copy);
        using (var finger = PanelDesktopFixture.CreatePointer(SyntheticPointerKind.Finger))
        {
            finger.Tap(at.X, at.Y);
        }

        await PanelDesktopFixture.WaitUntilAsync(
            () => fixture.Engine.Count > 0,
            "the tap never reached the engine"
        );
        fixture.Engine.Clear();
        fixture.Probe.IsForeground.ShouldBeTrue();
    }

    /// <summary>
    /// Opens the menu, checks that <paramref name="disabled"/> (if any) is greyed out, invokes <paramref name="entry"/>
    /// through UI Automation and returns the command the tray ran. The menu never stays on the screen.
    /// </summary>
    private async Task<TrayCommand?> ChooseAsync(string entry, string? disabled = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler onOpened = (_, _) => opened.TrySetResult();
        fixture.Menu.MenuOpened += onOpened;
        var anchor = fixture.TileCenter(PanelTestData.Copy);
        var running = fixture.Tray.OpenMenuAsync(
            new PhysicalPoint(anchor.X, anchor.Y),
            cancellationToken
        );
        try
        {
            await opened.Task.WaitAsync(PanelDesktopFixture.EventTimeout, cancellationToken);
            using var automation = new UIA3Automation();
            AutomationElement? item = null;
            await PanelDesktopFixture.WaitUntilAsync(
                () => (item = FindEntry(automation, entry)) is not null,
                "the tray menu entry «"
                    + entry
                    + "» is not in the UI Automation tree; "
                    + ForegroundWindows.Describe()
            );
            if (disabled is not null)
            {
                FindEntry(automation, disabled)
                    .ShouldNotBeNull()
                    .IsEnabled.ShouldBeFalse(disabled + " is greyed out");
            }

            item!.Patterns.Invoke.Pattern.Invoke();
            return await running.WaitAsync(PanelDesktopFixture.EventTimeout, cancellationToken);
        }
        finally
        {
            fixture.Menu.MenuOpened -= onOpened;
            if (!running.IsCompleted)
            {
                fixture.Menu.DismissMenu();
                _ = await running.WaitAsync(PanelDesktopFixture.EventTimeout, cancellationToken);
            }
        }
    }

    private static AutomationElement? FindEntry(UIA3Automation automation, string name) =>
        automation
            .GetDesktop()
            .FindAllChildren(condition => condition.ByClassName(PopupMenuClass))
            .Select(menu => menu.FindFirstDescendant(condition => condition.ByName(name)))
            .FirstOrDefault(found => found is not null);
}
