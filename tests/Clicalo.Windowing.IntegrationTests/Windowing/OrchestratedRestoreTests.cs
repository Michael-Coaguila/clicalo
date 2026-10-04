using System.Diagnostics;
using System.Globalization;
using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// The negative test of blueprint §3.5 (spike S1) through the product's own restore path: InputProbe, in front, calls
/// <c>SetForegroundWindow</c> on the panel; <c>ActivationGuard</c> reports the violation to the real
/// <c>ForegroundOrchestrator</c>, which gives the foreground back from the thread pool to the last external window its
/// <c>ForegroundMonitor</c> verified, through <c>ForegroundControl</c>. The probe is back in front within
/// <c>Timings.Windowing.ViolationRestoreBudget</c> (measured by the probe itself), with exactly one violation per
/// activation. 20 of 20.
/// </summary>
/// <remarks>
/// <see cref="ActivationGuardNegativeTests"/> measures detection with a test arbiter that restores on its own; this
/// class measures the restore the product ships.
/// </remarks>
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
        var restores = new List<double>();
        using var failures = DebugFailures.Capture();

        long previousStart = 0;
        var previousCursor = 0;
        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            var cursor = await desktop.PrepareAsync();
            var cycleStart = Stopwatch.GetTimestamp();
            desktop.Timeline.Note(
                Say($"cycle {cycle}: the probe calls SetForegroundWindow on the panel")
            );
            var (traceStart, traceCursor) =
                previousStart == 0 ? (cycleStart, cursor) : (previousStart, previousCursor);
            try
            {
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
                    Say(
                        $"Cycle {cycle}: Windows refused the probe's SetForegroundWindow on the panel."
                    )
                );

                await SurfaceDesktopFixture.WaitUntilAsync(
                    () => desktop.Lab.Guard.Violations > before,
                    Say($"Cycle {cycle}: ActivationGuard did not detect the forced activation."),
                    Sequence
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
                    .First(activate =>
                        !IsDeactivation(activate) && activate.Sequence > lost.Sequence
                    );
                var restoredAfter = TimeSpan.FromSeconds(
                    (back.Timestamp - lost.Timestamp) / frequency
                );
                restores.Add(restoredAfter.TotalMilliseconds);

                restoredAfter.ShouldBeLessThanOrEqualTo(
                    Timings.Windowing.ViolationRestoreBudget,
                    Say(
                        $"Cycle {cycle}: the orchestrator gave the foreground back after {restoredAfter.TotalMilliseconds:F1} ms."
                    )
                );
                desktop.Lab.Guard.Violations.ShouldBe(
                    before + 1,
                    Say($"Cycle {cycle}: one activation, one violation.") + Sequence()
                );
                NativeSurface
                    .HasExStyle(panel.Handle, NativeSurface.ExNoActivate)
                    .ShouldBeTrue(Say($"Cycle {cycle}: WS_EX_NOACTIVATE is gone."));
                TestContext.Current.TestOutputHelper?.WriteLine(
                    Say($"Cycle {cycle}:") + Sequence()
                );
                var violation = desktop.Lab.Arbiter.Violations[^1];
                violation.Surface.ShouldBe(panel.Id);
                violation.ProbableCause.ShouldBe(ActivationCause.External);
            }
            catch (Exception)
                when (Explain(cycle, desktop.DescribeActivations(traceStart, traceCursor)))
            {
                throw;
            }

            if (
                string.Equals(
                    Environment.GetEnvironmentVariable("CLICALO_ACTIVATION_TRACE"),
                    "1",
                    StringComparison.Ordinal
                )
            )
            {
                _ = Explain(cycle, desktop.DescribeActivations(cycleStart, cursor));
            }

            previousStart = cycleStart;
            previousCursor = cursor;
        }

        desktop.Lab.Guard.Violations.ShouldBe(start + Cycles);
        failures.Messages.Count.ShouldBe(DebugFailures.AreLive ? Cycles : 0);
        restores.Sort();
        TestContext.Current.TestOutputHelper?.WriteLine(
            Say(
                $"Restore by ForegroundOrchestrator, measured by the probe: median {restores[restores.Count / 2]:F1} ms, maximum {restores[^1]:F1} ms."
            )
        );
    }

    /// <summary>Writes the timeline of a cycle to the test output; false, so a failure keeps propagating.</summary>
    private static bool Explain(int cycle, string timeline)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(
            Say($"Timeline up to cycle {cycle} (QPC, from the start of the cycle before it):")
                + Environment.NewLine
                + timeline
        );
        return false;
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
