using System.Diagnostics;
using System.Globalization;
using Clicalo.Domain.Timing;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// The negative test of blueprint §3.5 (spike S1) through the product's own restore path, run nightly and before every
/// release: InputProbe, in front, calls <c>SetForegroundWindow</c> on the panel; <c>ActivationGuard</c> asks the real
/// <c>ForegroundOrchestrator</c> for one restore, which gives the foreground back from the thread pool to the last
/// external window its <c>ForegroundMonitor</c> verified, through <c>ForegroundControl</c>. The probe is back in front
/// within <c>Timings.Windowing.ViolationRestoreBudget</c> (measured on the probe's clock, see
/// <see cref="ProbeReactivation"/>).
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-01")]
public sealed class OrchestratedRestoreTests(OrchestratedSurfaceFixture desktop)
    : IClassFixture<OrchestratedSurfaceFixture>
{
    private const int Cycles = 20;

    [DesktopFact]
    public async Task The_orchestrator_gives_the_foreground_back_after_a_forced_activation()
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
                        $"Cycle {cycle}: the orchestrator gave the foreground back after {restoredAfter.TotalMilliseconds:F1} ms."
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
