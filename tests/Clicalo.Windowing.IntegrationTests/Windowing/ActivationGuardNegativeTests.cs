using System.Globalization;
using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// The mandatory negative test of blueprint §3.5 (ADR-0005, spike S1): InputProbe, in front, calls
/// <c>SetForegroundWindow</c> on the panel. <c>reg01.violations</c> rises by exactly one, the foreground is back on
/// the probe within <c>Timings.Windowing.ViolationRestoreBudget</c> (measured by the probe itself, from losing the
/// activation to getting it back) and <c>WS_EX_NOACTIVATE</c> is still there. 20 of 20.
/// </summary>
/// <remarks>
/// The restore is the job of <c>ForegroundOrchestrator</c> (package foreground); this package's arbiter does what the
/// orchestrator does on a violation, on the thread pool: <c>SetForegroundWindow</c> back to the last external
/// foreground, which this process may do because the violation made it the foreground process.
/// </remarks>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-01")]
public sealed class ActivationGuardNegativeTests(SurfaceDesktopFixture desktop)
    : IClassFixture<SurfaceDesktopFixture>
{
    private const int Cycles = 20;

    [DesktopFact]
    public async Task A_forced_activation_is_detected_and_reverted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var panel = desktop.Panel;
        var frequency = (double)desktop.Probe.Ready.TimestampFrequency;
        var start = desktop.Lab.Guard.Violations;
        using var failures = DebugFailures.Capture();

        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            var cursor = await desktop.PrepareAsync();
            var before = desktop.Lab.Guard.Violations;
            var sequenceStart = panel.ActivationSequence.Count;
            string Sequence() =>
                " Panel messages this cycle: "
                + string.Join(", ", panel.ActivationSequence.Skip(sequenceStart))
                + ".";

            var answer = await desktop.Probe.RequestForegroundAsync(
                panel.Handle,
                SurfaceDesktopFixture.EventTimeout,
                cancellationToken
            );
            answer.Succeeded.ShouldBeTrue(
                Say($"Cycle {cycle}: Windows refused the probe's SetForegroundWindow on the panel.")
            );

            await SurfaceDesktopFixture.WaitUntilAsync(
                () => desktop.Lab.Guard.Violations > before,
                Say($"Cycle {cycle}: ActivationGuard did not detect the forced activation.")
                    + Sequence()
            );
            var events = await desktop.Probe.WaitForAsync(
                cursor,
                Reactivated,
                SurfaceDesktopFixture.EventTimeout,
                cancellationToken
            );
            var lost = events.OfType<ActivateEvent>().First(IsDeactivation);
            var back = events
                .OfType<ActivateEvent>()
                .First(activate => !IsDeactivation(activate) && activate.Sequence > lost.Sequence);
            var restoredAfter = TimeSpan.FromSeconds((back.Timestamp - lost.Timestamp) / frequency);

            restoredAfter.ShouldBeLessThanOrEqualTo(
                Timings.Windowing.ViolationRestoreBudget,
                Say(
                    $"Cycle {cycle}: the probe got the foreground back after {restoredAfter.TotalMilliseconds:F1} ms."
                )
            );
            desktop.Lab.Guard.Violations.ShouldBe(
                before + 1,
                Say($"Cycle {cycle}: one activation, one violation.") + Sequence()
            );
            NativeSurface
                .HasExStyle(panel.Handle, NativeSurface.ExNoActivate)
                .ShouldBeTrue(Say($"Cycle {cycle}: WS_EX_NOACTIVATE is gone."));
            var violation = desktop.Lab.Arbiter.Violations[^1];
            violation.Surface.ShouldBe(panel.Id);
            violation.Window.ShouldBe(panel.SurfaceWindow);
            violation.ProbableCause.ShouldBe(ActivationCause.External);
        }

        desktop.Lab.Guard.Violations.ShouldBe(
            start + Cycles,
            "late activation messages must not count: "
                + string.Join(", ", panel.ActivationSequence)
        );
        failures.Messages.Count.ShouldBe(DebugFailures.AreLive ? Cycles : 0);
    }

    private static bool IsDeactivation(ActivateEvent activate) =>
        activate.State == ActivationState.Inactive;

    private static bool Reactivated(IReadOnlyList<ProbeEvent> events)
    {
        var lost = events.OfType<ActivateEvent>().FirstOrDefault(IsDeactivation);
        return lost is not null
            && events
                .OfType<ActivateEvent>()
                .Any(activate => !IsDeactivation(activate) && activate.Sequence > lost.Sequence);
    }

    private static string Say(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}
