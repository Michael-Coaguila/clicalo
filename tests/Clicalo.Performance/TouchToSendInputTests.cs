using System.Diagnostics;
using System.Globalization;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Performance;

/// <summary>
/// The M2 exit criterion «tocar → SendInput en InputProbe con p95 ≤ 50 ms» (blueprint §7.1, §10.3, §14; NFR-001), end
/// to end on the published app: InputProbe in front, a synthetic finger taps the first tile of the running
/// <c>Clicalo.exe</c> (the seed's «Copiar», Ctrl+C: harmless in the probe) and the probe records when the first key
/// arrives. The tap and the probe use the same performance counter. It sends real keys, so it runs only in continuous
/// integration; the finger may only touch Clicalo.exe's windows. On a hosted runner the p95 is a trend; with
/// <c>CLICALO_PERF_GATE=1</c> (the touch laboratory) it gates.
/// </summary>
[Trait("Requires", "Desktop")]
[Trait("Category", "Perf")]
[Trait("Req", "NFR-001")]
[Trait("Req", "EJE-003")]
public sealed class TouchToSendInputTests
{
    private const int Taps = 20;
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Longer than the debounce of every touch preset, so no tap of the series is ignored as a double.</summary>
    private static readonly TimeSpan BetweenTaps = TimeSpan.FromMilliseconds(700);

    [PerfFact]
    public async Task A_tap_on_the_panel_reaches_the_app_in_front_within_the_budget()
    {
        Assert.SkipUnless(
            PerfEnvironment.SendInput,
            "It sends real keys (Ctrl+C) to InputProbe: only in continuous integration, never on a developer's machine."
        );
        var cancellationToken = TestContext.Current.CancellationToken;
        var variant =
            PerfEnvironment.Variants.FirstOrDefault(static v =>
                string.Equals(v.Name, "sc-r2r", StringComparison.Ordinal)
            ) ?? PerfEnvironment.Variants[0];
        await using var probe = await InputProbeSession.StartAsync(cancellationToken);
        using var app = AppLaunch.Start(variant, sendInput: true);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        var panel = PanelWindows
            .Find(app.Process.Id)
            .ShouldNotBeNull("the panel of Clicalo.exe is on screen");
        var (x, y) = panel.FirstTile;
        var latencies = new List<TimeSpan>();
        using (var finger = new SyntheticPointer(SyntheticPointerKind.Finger, [app.Process.Id]))
        {
            for (var tap = 0; tap < Taps; tap++)
            {
                await probe.EnsureForegroundAsync(EventTimeout, cancellationToken);
                var cursor = probe.Cursor;

                // A tap activates when the finger lifts (EJE-001): the clock starts when the lift was handed to
                // Windows, which is when Tap returns. Starting it before Tap would add the synthetic gesture's own
                // frames (a sleep of SyntheticPointer.FrameInterval between the down and the up) to Clícalo's time.
                finger.Tap(x, y);
                var lifted = Stopwatch.GetTimestamp();
                var events = await probe.WaitForAsync(
                    cursor,
                    static received =>
                        received.OfType<KeyMessageEvent>().Count(static key => key.IsRelease) >= 2,
                    EventTimeout,
                    cancellationToken
                );
                var keys = events.OfType<KeyMessageEvent>().ToList();
                var latency = Stopwatch.GetElapsedTime(
                    lifted,
                    keys.First(static key => key.IsPress).Timestamp
                );
                latencies.Add(latency > TimeSpan.Zero ? latency : TimeSpan.Zero);
                probe.IsForeground.ShouldBeTrue("the tap never takes the foreground (REG-01)");
                foreach (var key in keys.GroupBy(static key => key.VirtualKey))
                {
                    key.Count(static message => message.IsRelease)
                        .ShouldBe(
                            key.Count(static message => message.IsPress),
                            "every key sent is released"
                        );
                }

                await Task.Delay(BetweenTaps, cancellationToken);
            }
        }

        var p95 = Percentiles.Of(latencies, 0.95);
        var summary = string.Create(
            CultureInfo.InvariantCulture,
            $"Touch → SendInput over {latencies.Count} taps ({variant.Name}): p50 {Percentiles.Of(latencies, 0.5).TotalMilliseconds:0.0} ms, p95 {p95.TotalMilliseconds:0.0} ms, max {latencies.Max().TotalMilliseconds:0.0} ms."
        );
        await File.WriteAllTextAsync(
            Path.Combine(PerfEnvironment.ResultsDirectory, "touch-to-sendinput.txt"),
            summary + "\n",
            cancellationToken
        );
        TestContext.Current.TestOutputHelper?.WriteLine(summary);
        if (PerfEnvironment.Gate)
        {
            p95.ShouldBeLessThanOrEqualTo(StartupBudgets.TouchToSendInputP95, summary);
        }
    }
}
