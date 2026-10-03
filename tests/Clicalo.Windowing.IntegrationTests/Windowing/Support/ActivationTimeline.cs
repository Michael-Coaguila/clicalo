using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// One timeline of a forced activation, on the clock both processes share (<c>QueryPerformanceCounter</c>, which is
/// what <see cref="Stopwatch"/> and InputProbe read): the steps the test and the restore noted, the activation
/// messages of each surface with the foreground window at that moment, the foreground changes and the probe's events.
/// The diagnostic of the negative tests of <c>ActivationGuard</c> when a cycle fails.
/// </summary>
public sealed class ActivationTimeline
{
    private readonly ConcurrentQueue<(long Timestamp, string Line)> _notes = new();

    /// <summary>Notes a step of the test, the restore or the guard, stamped now.</summary>
    public void Note(string line) => _notes.Enqueue((Stopwatch.GetTimestamp(), line));

    /// <summary>
    /// Everything since <paramref name="since"/> (<see cref="Stopwatch"/> ticks), in order, each line with its
    /// milliseconds after it: the notes, the activation messages of <paramref name="surfaces"/>, the changes in
    /// <paramref name="foreground"/> and the probe's events after <paramref name="probeCursor"/>.
    /// </summary>
    public string Render(
        long since,
        InputProbeSession probe,
        int probeCursor,
        ForegroundLog? foreground,
        IEnumerable<TestSurface> surfaces
    )
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(surfaces);
        var entries = new List<(long Timestamp, string Line)>();
        entries.AddRange(
            _notes
                .Where(note => note.Timestamp >= since)
                .Select(note => (note.Timestamp, "test " + note.Line))
        );
        foreach (var surface in surfaces)
        {
            entries.AddRange(surface.ActivationLogSince(since));
        }

        if (foreground is not null)
        {
            entries.AddRange(foreground.Since(since));
        }

        entries.AddRange(
            probe
                .EventsSince(probeCursor)
                .Where(received => received.Timestamp >= since)
                .Select(received => (received.Timestamp, "probe " + received.Json))
        );
        return string.Join(
            Environment.NewLine,
            entries
                .OrderBy(entry => entry.Timestamp)
                .Select(entry =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"  +{Stopwatch.GetElapsedTime(since, entry.Timestamp).TotalMilliseconds:0.00} ms {entry.Line}"
                    )
                )
        );
    }
}
