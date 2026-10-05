using System.Diagnostics;
using System.Globalization;
using Clicalo.Domain.Timing;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// The negative test of blueprint §3.5 (ADR-0005, ADR-0024, spike S1), run nightly and before every release: InputProbe,
/// in front, calls <c>SetForegroundWindow</c> on the panel. <c>ActivationGuard</c> counts it, the foreground is back on
/// the probe within <c>Timings.Windowing.ViolationRestoreBudget</c> (measured on the probe's clock, see
/// <see cref="ProbeReactivation"/>) and <c>WS_EX_NOACTIVATE</c> is still there.
/// </summary>
/// <remarks>
/// This package's arbiter does what the orchestrator does on a violation, on the thread pool:
/// <c>SetForegroundWindow</c> back to the last external foreground. <see cref="OrchestratedRestoreTests"/> measures the
/// restore the product ships.
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
            var cycleStart = Stopwatch.GetTimestamp();
            desktop.Timeline.Note(
                Say($"cycle {cycle}: the probe calls SetForegroundWindow on the panel")
            );
            try
            {
                var before = desktop.Lab.Guard.Violations;
                var requestedAt = Stopwatch.GetTimestamp();
                var answer = await desktop.Probe.RequestForegroundAsync(
                    panel.Handle,
                    SurfaceDesktopFixture.EventTimeout,
                    cancellationToken
                );
                answer.Succeeded.ShouldBeTrue(
                    Say(
                        $"Cycle {cycle}: Windows refused the probe's SetForegroundWindow on the panel."
                    )
                );

                await SurfaceDesktopFixture.WaitUntilAsync(
                    () => desktop.Lab.Guard.Violations > before,
                    Say($"Cycle {cycle}: ActivationGuard did not detect the forced activation."),
                    () => string.Join(", ", panel.ActivationSequence)
                );
                var events = await desktop.Probe.WaitForAsync(
                    cursor,
                    received =>
                        ProbeReactivation.Reactivated(received, desktop.Probe.Window, requestedAt),
                    SurfaceDesktopFixture.EventTimeout,
                    cancellationToken
                );
                var restoredAfter = ProbeReactivation.RestoredAfter(
                    events,
                    desktop.Probe.Window,
                    requestedAt,
                    frequency
                );

                restoredAfter.ShouldBeLessThanOrEqualTo(
                    Timings.Windowing.ViolationRestoreBudget,
                    Say(
                        $"Cycle {cycle}: the probe got the foreground back after {restoredAfter.TotalMilliseconds:F1} ms."
                    )
                );
                NativeSurface
                    .HasExStyle(panel.Handle, NativeSurface.ExNoActivate)
                    .ShouldBeTrue(Say($"Cycle {cycle}: WS_EX_NOACTIVATE is gone."));
                desktop.Lab.Arbiter.Violations[^1].Surface.ShouldBe(panel.Id);
            }
            catch (Exception) when (Explain(cycle, desktop.DescribeActivations(cycleStart, cursor)))
            {
                throw;
            }
        }

        failures.Messages.Count.ShouldBe(
            DebugFailures.AreLive ? (int)(desktop.Lab.Guard.Violations - start) : 0
        );
    }

    /// <summary>Writes the timeline of a cycle to the test output; false, so a failure keeps propagating.</summary>
    private static bool Explain(int cycle, string timeline)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(
            Say($"Timeline of cycle {cycle} (QPC, from its start):")
                + Environment.NewLine
                + timeline
        );
        return false;
    }

    private static string Say(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}
