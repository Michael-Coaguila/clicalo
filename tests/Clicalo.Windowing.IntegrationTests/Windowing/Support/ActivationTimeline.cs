using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
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
    private static readonly TimeSpan StallThreshold = TimeSpan.FromMilliseconds(25);

    private readonly ConcurrentQueue<(long Timestamp, string Line)> _notes = new();

    /// <summary>
    /// Starts a dedicated thread that notes every time it woke up much later than it asked to: a stall of the whole
    /// process or of the machine (a garbage collection, a starved runner), told apart from a late thread pool.
    /// Stops with <paramref name="cancellationToken"/>.
    /// </summary>
    public void WatchStalls(CancellationToken cancellationToken)
    {
        var thread = new Thread(() =>
        {
            var last = Stopwatch.GetTimestamp();
            var pauses = GC.GetTotalPauseDuration();
            while (!cancellationToken.WaitHandle.WaitOne(2))
            {
                var now = Stopwatch.GetTimestamp();
                var gap = Stopwatch.GetElapsedTime(last, now);
                if (gap > StallThreshold)
                {
                    var gc = GC.GetTotalPauseDuration();
                    _notes.Enqueue(
                        (
                            now,
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"stall: a thread that waits 2 ms woke after {gap.TotalMilliseconds:0.0} ms (GC pauses meanwhile {(gc - pauses).TotalMilliseconds:0.0} ms)"
                            )
                        )
                    );
                    pauses = gc;
                }

                last = now;
            }
        })
        {
            IsBackground = true,
            Name = "Activation stall watch",
            Priority = ThreadPriority.Highest,
        };
        thread.Start();
    }

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
        entries.Add(
            (
                Stopwatch.GetTimestamp(),
                "now: "
                    + DescribeThreadOf(probe.Window)
                    + string.Create(
                        CultureInfo.InvariantCulture,
                        $"; thread pool {ThreadPool.ThreadCount} threads, {ThreadPool.PendingWorkItemCount} items queued; GC pauses {GC.GetTotalPauseDuration().TotalMilliseconds:0.0} ms in total"
                    )
            )
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

    /// <summary>
    /// The activation state of the thread that owns <paramref name="window"/> (<c>GetGUIThreadInfo</c>): its active
    /// window, its focus window and <c>GetForegroundWindow</c>. The foreground window can be a window whose thread
    /// lost its focus.
    /// </summary>
    public static string DescribeThreadOf(nint window)
    {
        var thread = GetWindowThreadProcessId(window, out _);
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        return GetGUIThreadInfo(thread, ref info)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"thread of 0x{window:X}: active 0x{info.Active:X}, focus 0x{info.Focus:X}, flags 0x{info.Flags:X}; foreground 0x{GetForegroundWindow():X}"
            )
            : string.Create(
                CultureInfo.InvariantCulture,
                $"thread of 0x{window:X}: GetGUIThreadInfo failed ({Marshal.GetLastPInvokeError()})"
            );
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public nint Active;
        public nint Focus;
        public nint Capture;
        public nint MenuOwner;
        public nint MoveSize;
        public nint Caret;
        public int CaretLeft;
        public int CaretTop;
        public int CaretRight;
        public int CaretBottom;
    }
}
