using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Clicalo.TestKit;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// One tap of the panel latency test split in its legs (milliseconds): Windows records the lift → the panel's window
/// procedure receives <c>WM_POINTERUP</c> (<see cref="QueueMs"/>, the time the message waited for the UI thread) → the
/// activation is in the engine mailbox (<see cref="HandlingMs"/>, the pointer layer, the recognizer, the tile and the
/// controller).
/// </summary>
/// <param name="Tap">The tap's number, from 1.</param>
/// <param name="Device">Finger, pen or mouse.</param>
/// <param name="TotalMs">Lift → mailbox, as the test asserts it.</param>
/// <param name="QueueMs">Lift → <c>WM_POINTERUP</c> received.</param>
/// <param name="HandlingMs"><c>WM_POINTERUP</c> received → mailbox.</param>
/// <param name="GestureMs">How long the synthetic gesture took to inject (down, frames, up).</param>
/// <param name="RecordedToReturnMs">Lift recorded → the injection call returned (negative if recorded later).</param>
/// <param name="Runtime">GC and JIT activity between the tap and the mailbox.</param>
/// <param name="UiBusy">Dispatcher work of the UI thread while the lift waited, by priority.</param>
/// <param name="Timeline">The UI thread around the tap (only for slow taps).</param>
internal sealed record TapSegments(
    int Tap,
    string Device,
    double TotalMs,
    double QueueMs,
    double HandlingMs,
    double GestureMs,
    double RecordedToReturnMs,
    RuntimeCounters Runtime,
    string UiBusy,
    IReadOnlyList<string> Timeline
)
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>
    /// Writes the taps to the test output and to <c>artifacts/cl/test-results/panel-tap-latency-*.json</c> (one file
    /// per run, so repeated runs in one job keep every sample).
    /// </summary>
    public static void Record(IReadOnlyList<TapSegments> taps)
    {
        ArgumentNullException.ThrowIfNull(taps);
        var output = TestContext.Current.TestOutputHelper;
        foreach (var tap in taps)
        {
            output?.WriteLine(tap.Line());
            foreach (var line in tap.Timeline)
            {
                output?.WriteLine("    " + line);
            }
        }

        var folder = RepoPaths.Combine("artifacts", "cl", "test-results");
        Directory.CreateDirectory(folder);
        var name = string.Create(
            CultureInfo.InvariantCulture,
            $"panel-tap-latency-{Environment.ProcessId}-{Stopwatch.GetTimestamp()}.json"
        );
        File.WriteAllText(
            Path.Combine(folder, name),
            JsonSerializer.Serialize(
                taps.Select(static tap => new
                {
                    tap.Tap,
                    tap.Device,
                    tap.TotalMs,
                    tap.QueueMs,
                    tap.HandlingMs,
                    tap.GestureMs,
                    tap.RecordedToReturnMs,
                    Runtime = tap.Runtime.ToString(),
                    tap.UiBusy,
                    tap.Timeline,
                }),
                Indented
            )
        );
    }

    /// <summary>One line for the test output.</summary>
    public string Line() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"tap {Tap} {Device}: total {TotalMs:0.00} ms = queue {QueueMs:0.00} + handling {HandlingMs:0.00}; gesture {GestureMs:0.0} ms, recorded→returned {RecordedToReturnMs:0.00} ms; {Runtime}; UI busy while queued: {UiBusy}"
        );
}
