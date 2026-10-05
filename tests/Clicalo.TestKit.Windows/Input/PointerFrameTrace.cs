using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Clicalo.TestKit.Windows.Input;

/// <summary>
/// The last frames every <see cref="SyntheticPointer"/> of this process injected, with the window the safety check saw
/// under the point and the foreground window at that moment, for the failure messages of the desktop tests. Timestamps
/// are <see cref="Stopwatch"/> ticks (QueryPerformanceCounter, the clock InputProbe stamps its events with).
/// </summary>
internal static class PointerFrameTrace
{
    private const int Capacity = 512;

    private static readonly ConcurrentQueue<Entry> Entries = new();

    /// <summary>Records one line of the trace at <paramref name="timestamp"/>.</summary>
    public static void Add(long timestamp, string line)
    {
        Entries.Enqueue(new Entry(timestamp, line));
        while (Entries.Count > Capacity && Entries.TryDequeue(out _)) { }
    }

    /// <summary>The lines recorded since <paramref name="since"/>, each with its milliseconds after it.</summary>
    public static IReadOnlyList<string> Since(long since) =>
        [
            .. Entries
                .Where(entry => entry.Timestamp >= since)
                .Select(entry =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"+{Stopwatch.GetElapsedTime(since, entry.Timestamp).TotalMilliseconds:0.0} ms {entry.Line}"
                    )
                ),
        ];

    private readonly record struct Entry(long Timestamp, string Line);
}
