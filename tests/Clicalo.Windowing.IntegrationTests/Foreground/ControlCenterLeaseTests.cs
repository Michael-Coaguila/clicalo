using System.Globalization;
using Clicalo.Application.Foreground;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Foreground.Support;

namespace Clicalo.Windowing.IntegrationTests.Foreground;

/// <summary>
/// Spike S4 and CCM-004: the Control Center, a normal activatable window, opened from the panel with a touch under a
/// <c>ControlCenter</c> lease of the real <c>ForegroundOrchestrator</c>, comes to the front with its field focused; closing
/// it gives the foreground back to the app that was in front before (InputProbe), verified, and no other window of this
/// process takes it when the window closes. 20 cycles.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "CCM-004")]
[Trait("Req", "REG-01")]
public sealed class ControlCenterLeaseTests(LeaseDesktopFixture desktop)
    : IClassFixture<LeaseDesktopFixture>
{
    private const int Cycles = 20;

    /// <summary>How long the test watches the foreground after the window closed.</summary>
    private static readonly TimeSpan AfterClose = TimeSpan.FromMilliseconds(150);

    [DesktopFact]
    public async Task Closing_the_control_center_returns_the_foreground()
    {
        var violations = desktop.Lab.Guard.Violations;
        var outcomes = new List<RestoreOutcome>();

        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            await desktop.PrepareAsync();
            var window = WpfThread.Invoke(() => new TestControlCenter());

            var result = await desktop.TapPanelAsync(() =>
                desktop.OpenControlCenterAsync(window, LeaseOrigin.Touch)
            );

            var lease = result.ShouldBeOfType<LeaseResult.Granted>(Say($"Cycle {cycle}")).Lease;
            lease.PreviousForeground.ShouldBe(desktop.ProbeWindow, Say($"Cycle {cycle}"));
            var token = WpfThread.Invoke(() => window.Token);
            ForegroundWindows
                .IsForeground(token.Handle)
                .ShouldBeTrue(
                    Say(
                        $"Cycle {cycle}: the Control Center is not in front: {ForegroundWindows.Describe()}"
                    )
                );
            await LeaseDesktopFixture.WaitUntilAsync(
                () => WpfThread.Invoke(() => window.IsActive && window.FieldHasKeyboardFocus),
                Say($"Cycle {cycle}: the Control Center field does not have the keyboard focus.")
            );

            var outcome = await LeaseDesktopFixture.OnUiThreadAsync(() =>
                LeaseDesktopFixture.CloseControlCenterAsync(window, lease)
            );

            outcomes.Add(outcome);
            outcome.ShouldBeOneOf(RestoreOutcome.Restored, RestoreOutcome.RestoredAfterRetry);
            await Task.Delay(AfterClose, TestContext.Current.CancellationToken);
            ForegroundWindows
                .IsForeground(desktop.Probe.Window)
                .ShouldBeTrue(
                    Say(
                        $"Cycle {cycle}: the foreground did not stay with the probe after the Control Center closed: {ForegroundWindows.Describe()}"
                    )
                );
            WpfThread.Invoke(() => window.IsVisible).ShouldBeFalse();
            desktop.Orchestrator.ActiveLease.ShouldBeNull();
        }

        desktop.Lab.Guard.Violations.ShouldBe(
            violations,
            "activating the Control Center activates no surface"
        );
        TestContext.Current.TestOutputHelper?.WriteLine(
            "Restore outcomes: "
                + string.Join(
                    ", ",
                    outcomes
                        .GroupBy(outcome => outcome)
                        .Select(group => group.Key + " × " + group.Count())
                )
        );
    }

    private static string Say(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}
