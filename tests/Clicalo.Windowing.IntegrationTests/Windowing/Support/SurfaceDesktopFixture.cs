using System.Diagnostics;
using System.Globalization;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.TestKit.Windows.Rendering;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// The desktop of one S1 test class: InputProbe as the application in front (the physical truth), and the lab
/// surfaces (panel, edge bar with its side window, bubble) shown passively, topmost, in the middle of the primary
/// work area. A violation is given back to the probe on the thread pool, as the orchestrator would do. Started only
/// when desktop tests are enabled.
/// </summary>
public sealed class SurfaceDesktopFixture : IAsyncLifetime
{
    /// <summary>How long a test waits for the probe to own the foreground before failing with a diagnostic.</summary>
    public static readonly TimeSpan ForegroundTimeout = TimeSpan.FromSeconds(5);

    /// <summary>How long a test waits for the events or the state it expects.</summary>
    public static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private const double Gap = 12;

    private readonly System.Collections.Concurrent.ConcurrentQueue<(
        long Timestamp,
        string Line
    )> _timeline = new();
    private ForegroundLog? _foreground;
    private InputProbeSession? _probe;
    private SurfaceLab? _lab;
    private TestSurface? _panel;
    private TestSurface? _dock;
    private TestSurface? _side;
    private TestSurface? _bubble;

    /// <summary>The probe in front.</summary>
    public InputProbeSession Probe => _probe ?? throw NotStarted();

    /// <summary>The windowing under test.</summary>
    public SurfaceLab Lab => _lab ?? throw NotStarted();

    /// <summary>What the forced activations did: the notes of the tests, the restore and the guard.</summary>
    public ActivationTimeline Timeline { get; } = new();

    public TestSurface Panel => _panel ?? throw NotStarted();

    public TestSurface Dock => _dock ?? throw NotStarted();

    public TestSurface Side => _side ?? throw NotStarted();

    public TestSurface Bubble => _bubble ?? throw NotStarted();

    public async ValueTask InitializeAsync()
    {
        if (!DesktopTestEnvironment.IsEnabled)
        {
            return;
        }

        _foreground = ForegroundLog.Start();
        Note("fixture starting");
        _probe = await InputProbeSession.StartAsync(TestContext.Current.CancellationToken);
        Note(
            string.Create(
                CultureInfo.InvariantCulture,
                $"probe started: window 0x{_probe.Window:X}, bounds {Describe(NativeSurface.Bounds(_probe.Window))}"
            )
        );
        var probe = _probe;
        var timeline = Timeline;
        var arbiter = new RecordingArbiter
        {
            Trace = timeline.Note,
            Restore = async _ =>
            {
                timeline.Note(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"restore starts, foreground 0x{ForegroundWindows.Current:X}"
                    )
                );
                var restored = await probe.TryBringToForegroundAsync(ForegroundTimeout);
                timeline.Note(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"restore ends: {restored}, foreground 0x{ForegroundWindows.Current:X}"
                    )
                );
            },
        };
        var lab = SurfaceLab.Create(arbiter);
        _lab = lab;
        lab.Guard.ViolationDetected += (_, e) =>
            timeline.Note(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"ActivationGuard counted a violation of {e.Violation.Surface} ({e.Violation.Message}, {e.Violation.ProbableCause})"
                )
            );
        _panel = lab.CreateSurface(SurfaceKind.Panel, 0, 360, 240);
        _dock = lab.CreateSurface(SurfaceKind.Dock, 0, 48, 240);
        _side = lab.CreateSurface(SurfaceKind.SideWindow, 0, 240, 240);
        _bubble = lab.CreateSurface(SurfaceKind.Bubble, 0, 64, 64);

        // Side by side in the middle of the primary work area, away from docked bars at its edges.
        var (work, dpi) = NativeSurface.PrimaryWorkArea();
        var scale = dpi / 96.0;
        TestSurface[] surfaces = [_panel, _dock, _side, _bubble];
        var frames = new List<Task>();
        WpfThread.Invoke(() =>
        {
            var totalWidth = surfaces.Sum(surface => surface.Width) + (Gap * (surfaces.Length - 1));
            var left = work.CenterX - (totalWidth * scale / 2);
            var top = work.CenterY - (surfaces.Max(surface => surface.Height) * scale / 2);
            foreach (var surface in surfaces)
            {
                surface.MovePassive(
                    new PhysicalRect(
                        (int)Math.Round(left),
                        (int)Math.Round(top),
                        (int)Math.Round(surface.Width * scale),
                        (int)Math.Round(surface.Height * scale)
                    )
                );
                frames.Add(FirstFrame.Watch(surface));
                surface.ShowPassive();
                Note(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{surface.Id} shown: window 0x{surface.Handle:X}, bounds {Describe(NativeSurface.Bounds(surface.Handle))}"
                    )
                );
                left += (surface.Width + Gap) * scale;
            }

            lab.Integrity.Start();
        });

        // A gesture sent before the surfaces are composed falls through to the window below (FirstFrame).
        await Task.WhenAll(frames).WaitAsync(EventTimeout, TestContext.Current.CancellationToken);
        Note("first frames composed; fixture ready");
    }

    /// <summary>
    /// What happened since <paramref name="since"/> (<see cref="Stopwatch"/> ticks), in order: the fixture's own steps,
    /// the injected frames, the pointer messages of every surface, the foreground changes and the probe's events since
    /// <paramref name="probeCursor"/>; then the top-level windows under <paramref name="x"/>, <paramref name="y"/>.
    /// </summary>
    public string Diagnose(long since, int probeCursor, int x, int y)
    {
        var lines = new List<(double Ms, string Line)>();
        void Add(IEnumerable<string> entries)
        {
            foreach (var entry in entries)
            {
                var ms = entry.StartsWith('+')
                    ? double.Parse(
                        entry[1..entry.IndexOf(' ', StringComparison.Ordinal)],
                        CultureInfo.InvariantCulture
                    )
                    : double.MaxValue;
                lines.Add((ms, entry));
            }
        }

        Add(_timeline.Select(entry => Stamp(since, entry.Timestamp, entry.Line)));
        Add(PointerFrameTrace.Since(since));
        foreach (var surface in new[] { _panel, _dock, _side, _bubble }.OfType<TestSurface>())
        {
            Add(surface.PointerLogSince(since));
        }

        if (_foreground is not null)
        {
            Add(_foreground.Relative(since));
        }

        if (_probe is not null)
        {
            Add(
                _probe
                    .EventsSince(probeCursor)
                    .Where(received => received.Timestamp >= since)
                    .Select(received => Stamp(since, received.Timestamp, "probe " + received.Json))
            );
        }

        return string.Join(
                Environment.NewLine,
                lines.OrderBy(line => line.Ms).Select(line => line.Line)
            )
            + Environment.NewLine
            + "Top-level windows under the point, from the top: "
            + string.Join(" > ", WindowsAt.Describe(x, y));
    }

    /// <summary>
    /// The <see cref="Timeline"/> since <paramref name="since"/> (<see cref="Stopwatch"/> ticks), with the activation
    /// messages of every surface, the foreground changes and the probe's events after <paramref name="probeCursor"/>.
    /// </summary>
    public string DescribeActivations(long since, int probeCursor) =>
        Timeline.Render(
            since,
            Probe,
            probeCursor,
            _foreground,
            new[] { _panel, _dock, _side, _bubble }.OfType<TestSurface>()
        );

    /// <summary>Notes a step of the fixture or of a test for <see cref="Diagnose"/>.</summary>
    public void Note(string line) => _timeline.Enqueue((Stopwatch.GetTimestamp(), line));

    /// <summary>The windows of <paramref name="surface"/> (the Tab view has two).</summary>
    public IReadOnlyList<TestSurface> WindowsOf(LabSurface surface) =>
        surface switch
        {
            LabSurface.Panel => [Panel],
            LabSurface.TabWithSide => [Dock, Side],
            LabSurface.Bubble => [Bubble],
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
        };

    /// <summary>
    /// Makes sure the probe owns the foreground (failing with a diagnostic otherwise) and returns the cursor from which
    /// the calling test's probe events start.
    /// </summary>
    public async Task<int> PrepareAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await Probe.EnsureForegroundAsync(ForegroundTimeout, cancellationToken);
        await Probe.PingAsync(EventTimeout, cancellationToken);
        return Probe.Cursor;
    }

    /// <summary>A pointer that may only touch this process's surfaces and the probe.</summary>
    public SyntheticPointer CreatePointer(SyntheticPointerKind kind) =>
        new(kind, [Environment.ProcessId, Probe.ProcessId]);

    /// <summary>The center of <paramref name="surface"/>, in physical pixels.</summary>
    public static (int X, int Y) CenterOf(TestSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var bounds = NativeSurface.Bounds(surface.Handle);
        return (bounds.CenterX, bounds.CenterY);
    }

    /// <summary>Waits until <paramref name="condition"/> holds, failing with <paramref name="because"/> on timeout.</summary>
    public static Task WaitUntilAsync(Func<bool> condition, string because) =>
        WaitUntilAsync(condition, because, diagnostics: null);

    /// <summary>
    /// Waits until <paramref name="condition"/> holds, failing with <paramref name="because"/> and
    /// <paramref name="diagnostics"/> on timeout.
    /// </summary>
    public static async Task WaitUntilAsync(
        Func<bool> condition,
        string because,
        Func<string>? diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(condition);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(EventTimeout);
        while (!condition())
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5), deadline.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                condition()
                    .ShouldBeTrue(
                        because
                            + " ("
                            + ForegroundWindows.Describe()
                            + ")"
                            + (diagnostics is null ? "" : Environment.NewLine + diagnostics())
                    );
                return;
            }
        }
    }

    /// <summary>
    /// The probe kept the foreground and the keyboard focus since <paramref name="cursor"/>: it received no
    /// deactivation (<c>WM_ACTIVATE(WA_INACTIVE)</c>, <c>WM_ACTIVATEAPP(FALSE)</c>, <c>WM_NCACTIVATE(FALSE)</c>), no
    /// <c>WM_KILLFOCUS</c>, and every event saw it as the foreground window.
    /// </summary>
    public async Task ShouldHaveKeptTheForegroundAsync(int cursor)
    {
        await Probe.PingAsync(EventTimeout, TestContext.Current.CancellationToken);
        var events = Probe.EventsSince(cursor);
        events
            .OfType<ActivateEvent>()
            .Where(activate => activate.State == ActivationState.Inactive)
            .ShouldBeEmpty("The probe was deactivated.");
        events
            .OfType<AppActivateEvent>()
            .Where(activate => !activate.IsActive)
            .ShouldBeEmpty("The probe's application was deactivated.");
        events
            .OfType<FocusEvent>()
            .Where(focus => !focus.IsGained)
            .ShouldBeEmpty("The probe lost the keyboard focus.");
        events
            .OfType<WindowMessageEvent>()
            .Where(message =>
                string.Equals(message.MessageName, "WM_NCACTIVATE", StringComparison.Ordinal)
                && message.WParam == 0
            )
            .ShouldBeEmpty("The probe's frame was drawn inactive.");
        events
            .Where(received => received.ForegroundWindow != Probe.Window)
            .Select(received => received.Json)
            .ShouldBeEmpty(
                "Another window owned the foreground while the probe handled these events."
            );
    }

    public async ValueTask DisposeAsync()
    {
        _foreground?.Dispose();
        _lab?.Dispose();
        if (_probe is not null)
        {
            await _probe.DisposeAsync();
        }
    }

    private static string Stamp(long since, long timestamp, string line) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"+{Stopwatch.GetElapsedTime(since, timestamp).TotalMilliseconds:0.0} ms {line}"
        );

    private static string Describe(NativeSurface.Rect bounds) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"({bounds.Left}, {bounds.Top}, {bounds.Right}, {bounds.Bottom})"
        );

    private static InvalidOperationException NotStarted() => new(DesktopTestEnvironment.SkipReason);
}
