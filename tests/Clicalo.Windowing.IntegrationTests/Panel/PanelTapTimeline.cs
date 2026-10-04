using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// What the panel's UI thread did around each tap, on the performance counter (<see cref="Stopwatch"/>): the pointer
/// messages of the panel as its window procedure receives them (before the product's own hook), every message the
/// dispatcher's loop takes from the queue, and every dispatcher operation with its priority and duration. It only
/// observes: nothing is handled, delayed or changed. Attached and detached on the WPF thread.
/// </summary>
internal sealed class PanelTapTimeline : IDisposable
{
    private const int WmPointerUpdate = 0x0245;
    private const int WmPointerDown = 0x0246;
    private const int WmPointerUp = 0x0247;

    private static readonly PropertyInfo? OperationName = typeof(DispatcherOperation).GetProperty(
        "Name",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    /// <summary><c>GetCurrentThread()</c>.</summary>
    private static readonly nint CurrentThreadPseudoHandle = -2;

    private readonly Lock _gate = new();
    private readonly List<Entry> _entries = new(capacity: 8192);
    private readonly Stack<(DispatcherOperation Operation, long Started)> _running = new();
    private readonly Dispatcher _dispatcher;
    private readonly HwndSource _source;
    private readonly HwndSourceHook _hook;
    private long _upAt;
    private DateTimeOffset _upClock;

    private PanelTapTimeline(Window window)
    {
        _dispatcher = window.Dispatcher;
        _source =
            HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)
            ?? throw new InvalidOperationException("The panel has no HwndSource.");
        _hook = WndProc;

        // HwndSource calls the hook added last first: this one sees each message before the product's pointer layer.
        _source.AddHook(_hook);
        _dispatcher.Hooks.OperationStarted += OnOperationStarted;
        _dispatcher.Hooks.OperationCompleted += OnOperationCompleted;
        _dispatcher.Hooks.DispatcherInactive += OnInactive;
        ComponentDispatcher.ThreadFilterMessage += OnThreadMessage;
    }

    /// <summary>Starts observing <paramref name="window"/>; on its thread.</summary>
    public static PanelTapTimeline Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.VerifyAccess();
        return new PanelTapTimeline(window);
    }

    /// <summary>When the window procedure last received a <c>WM_POINTERUP</c>, on both clocks.</summary>
    public (long Timestamp, DateTimeOffset Clock) LastUp
    {
        get
        {
            lock (_gate)
            {
                return (_upAt, _upClock);
            }
        }
    }

    /// <summary>Forgets everything recorded so far.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _upAt = 0;
        }
    }

    /// <summary>
    /// The entries between <paramref name="from"/> and <paramref name="to"/> (performance counter), one per line with
    /// its milliseconds after <paramref name="from"/>; operations that started before and ended after are included.
    /// </summary>
    public IReadOnlyList<string> Describe(long from, long to)
    {
        lock (_gate)
        {
            var lines = new List<string>();
            ulong previous = 0;
            foreach (var entry in _entries.Where(entry => entry.End >= from && entry.Start <= to))
            {
                var cpu =
                    previous != 0 && entry.Cycles >= previous
                        ? string.Create(
                            CultureInfo.InvariantCulture,
                            $" [UI thread +{(entry.Cycles - previous) / 1e6:0.00} Mcycles]"
                        )
                        : string.Empty;
                if (entry.Cycles != 0)
                {
                    previous = entry.Cycles;
                }

                var duration =
                    entry.End > entry.Start
                        ? string.Create(
                            CultureInfo.InvariantCulture,
                            $" ({Stopwatch.GetElapsedTime(entry.Start, entry.End).TotalMilliseconds:0.00} ms)"
                        )
                        : string.Empty;
                lines.Add(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Stopwatch.GetElapsedTime(from, entry.Start).TotalMilliseconds:+0.00;-0.00} ms {entry.What}{duration}{cpu}"
                    )
                );
            }

            return lines;
        }
    }

    /// <summary>
    /// The milliseconds the UI thread spent between <paramref name="from"/> and <paramref name="to"/> inside dispatcher
    /// operations, by priority (top-level operations only).
    /// </summary>
    public string Busy(long from, long to)
    {
        lock (_gate)
        {
            var busy = _entries
                .Where(entry =>
                    entry.Priority is not null && entry.End >= from && entry.Start <= to
                )
                .GroupBy(static entry => entry.Priority!.Value)
                .Select(group =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{group.Key} {group.Sum(entry => Stopwatch.GetElapsedTime(Math.Max(entry.Start, from), Math.Min(entry.End, to)).TotalMilliseconds):0.00} ms"
                    )
                );
            return string.Join(", ", busy);
        }
    }

    public void Dispose()
    {
        _dispatcher.VerifyAccess();
        ComponentDispatcher.ThreadFilterMessage -= OnThreadMessage;
        _dispatcher.Hooks.OperationStarted -= OnOperationStarted;
        _dispatcher.Hooks.OperationCompleted -= OnOperationCompleted;
        _dispatcher.Hooks.DispatcherInactive -= OnInactive;
        _source.RemoveHook(_hook);
    }

    private static string NameOf(DispatcherOperation operation)
    {
        try
        {
            return OperationName?.GetValue(operation) as string ?? "operation";
        }
        catch (TargetInvocationException)
        {
            return "operation";
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        var now = Stopwatch.GetTimestamp();
        var cycles = Cycles();
        var clock = TimeProvider.System.GetUtcNow();
        var name = msg switch
        {
            WmPointerDown => "WM_POINTERDOWN",
            WmPointerUp => "WM_POINTERUP",
            WmPointerUpdate => "WM_POINTERUPDATE",
            _ => string.Create(CultureInfo.InvariantCulture, $"0x{msg:X4}"),
        };
        lock (_gate)
        {
            if (msg == WmPointerUp)
            {
                _upAt = now;
                _upClock = clock;
            }

            _entries.Add(
                new Entry(
                    now,
                    now,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{name} received by the panel (thread priority {Thread.CurrentThread.Priority})"
                    ),
                    null,
                    cycles
                )
            );
        }

        return 0;
    }

    private void OnThreadMessage(ref MSG msg, ref bool handled)
    {
        var now = Stopwatch.GetTimestamp();
        var message = msg.message;
        lock (_gate)
        {
            _entries.Add(
                new Entry(
                    now,
                    now,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"message 0x{message:X4} taken from the queue"
                    ),
                    null,
                    Cycles()
                )
            );
        }
    }

    private void OnOperationStarted(object? sender, DispatcherHookEventArgs e) =>
        _running.Push((e.Operation, Stopwatch.GetTimestamp()));

    private void OnOperationCompleted(object? sender, DispatcherHookEventArgs e)
    {
        var end = Stopwatch.GetTimestamp();
        if (_running.Count == 0)
        {
            return;
        }

        var (operation, started) = _running.Pop();
        if (!ReferenceEquals(operation, e.Operation))
        {
            return;
        }

        lock (_gate)
        {
            _entries.Add(
                new Entry(
                    started,
                    end,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{operation.Priority} {NameOf(operation)}"
                    ),
                    _running.Count == 0 ? operation.Priority : null,
                    Cycles()
                )
            );
        }
    }

    private void OnInactive(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            _entries.Add(new Entry(now, now, "dispatcher inactive", null, Cycles()));
        }
    }

    /// <summary>CPU cycles the current thread has used (0 when unknown).</summary>
    private static ulong Cycles() =>
        QueryThreadCycleTime(CurrentThreadPseudoHandle, out var cycles) ? cycles : 0;

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryThreadCycleTime(nint thread, out ulong cycles);

    private readonly record struct Entry(
        long Start,
        long End,
        string What,
        DispatcherPriority? Priority,
        ulong Cycles
    );
}
