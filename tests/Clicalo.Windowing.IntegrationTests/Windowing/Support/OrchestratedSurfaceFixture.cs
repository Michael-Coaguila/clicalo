using System.Diagnostics;
using System.Globalization;
using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Platform.Windows.Foreground;
using Clicalo.Platform.Windows.SysEvents;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.TestKit.Windows.Rendering;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// REG-01 composed as the product composes it (blueprint §3.5, §3.6): <c>ActivationGuard</c> reports to a real
/// <see cref="ForegroundOrchestrator"/> over the real adapters of Platform.Windows (<see cref="ForegroundControl"/>, the
/// <see cref="ForegroundMonitor"/> on its own <see cref="SysEventsThread"/>) and the <c>SurfaceRegistry</c> as its
/// surface ports. InputProbe is the app in front and one panel is shown passively. No test code gives the foreground
/// back: the orchestrator does, to the last external window its monitor verified. Started only when desktop tests are
/// enabled.
/// </summary>
public sealed class OrchestratedSurfaceFixture : IAsyncLifetime
{
    private readonly CancellationTokenSource _stopping = new();
    private InputProbeSession? _probe;
    private ForegroundLog? _foreground;
    private SysEventsThread? _thread;
    private ForegroundMonitor? _monitor;
    private InternalRightsHotkey? _hotkey;
    private ForegroundOrchestrator? _orchestrator;
    private SurfaceLab? _lab;
    private TestSurface? _panel;

    /// <summary>The probe in front.</summary>
    public InputProbeSession Probe => Require(_probe);

    /// <summary>The windowing under test; its arbiter records every violation and forwards it to the orchestrator.</summary>
    public SurfaceLab Lab => Require(_lab);

    /// <summary>The panel.</summary>
    public TestSurface Panel => Require(_panel);

    /// <summary>The real orchestrator, the arbiter behind the guard.</summary>
    public ForegroundOrchestrator Orchestrator => Require(_orchestrator);

    /// <summary>What the forced activations did: the notes of the tests, the orchestrator's calls and the guard.</summary>
    public ActivationTimeline Timeline { get; } = new();

    public async ValueTask InitializeAsync()
    {
        if (!DesktopTestEnvironment.IsEnabled)
        {
            return;
        }

        _foreground = ForegroundLog.Start();
        _probe = await InputProbeSession.StartAsync(TestContext.Current.CancellationToken);
        _thread = SysEventsThread.Start();
        _monitor = new ForegroundMonitor(_thread, TimeProvider.System);
        await _monitor.StartAsync();

        // Never registered: a violation is restored by step 1 alone, and no lease here climbs the ladder.
        _hotkey = new InternalRightsHotkey(_thread, TimeProvider.System);
        var arbiter = new RecordingArbiter { Trace = Timeline.Note };
        Timeline.WatchStalls(_stopping.Token);
        var lab = SurfaceLab.Create(arbiter);
        _lab = lab;
        _orchestrator = new ForegroundOrchestrator(
            new ForegroundPorts
            {
                Control = new TracedForegroundControl(new ForegroundControl(), Timeline),
                Monitor = _monitor,
                SurfaceStyle = lab.Registry,
                Surfaces = lab.Registry,
                RightsHotkey = _hotkey,
                KeyEffects = new NoKeyEffects(),
            },
            TimeProvider.System
        );
        arbiter.Forward = _orchestrator;
        var timeline = Timeline;
        lab.Guard.ViolationDetected += (_, e) =>
            timeline.Note(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"ActivationGuard counted a violation of {e.Violation.Surface} ({e.Violation.Message}, {e.Violation.ProbableCause})"
                )
            );

        var panel = lab.CreateSurface(SurfaceKind.Panel, 0, 360, 240);
        _panel = panel;
        var (work, dpi) = NativeSurface.PrimaryWorkArea();
        var scale = dpi / 96.0;
        WpfThread.Invoke(() =>
        {
            var width = (int)Math.Round(panel.Width * scale);
            var height = (int)Math.Round(panel.Height * scale);
            panel.MovePassive(
                new PhysicalRect(
                    work.CenterX - (width / 2),
                    work.CenterY - (height / 2),
                    width,
                    height
                )
            );
            panel.ShowPassive();
        });
    }

    /// <summary>
    /// Makes sure the probe owns the foreground and that the orchestrator's monitor has verified it (a violation is
    /// restored to that window), and returns the cursor from which the calling test's probe events start.
    /// </summary>
    public async Task<int> PrepareAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await Probe.EnsureForegroundAsync(
            SurfaceDesktopFixture.ForegroundTimeout,
            cancellationToken
        );
        var started = Stopwatch.GetTimestamp();
        while (Orchestrator.Current.Window.Handle != Probe.Window)
        {
            if (Stopwatch.GetElapsedTime(started) > SurfaceDesktopFixture.EventTimeout)
            {
                throw new TimeoutException(
                    "The foreground monitor never verified InputProbe as the external foreground: "
                        + ForegroundWindows.Describe()
                );
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }

        await Probe.PingAsync(SurfaceDesktopFixture.EventTimeout, cancellationToken);
        return Probe.Cursor;
    }

    /// <summary>
    /// The <see cref="Timeline"/> since <paramref name="since"/> (<see cref="Stopwatch"/> ticks), with the panel's
    /// activation messages, the foreground changes and the probe's events after <paramref name="probeCursor"/>.
    /// </summary>
    public string DescribeActivations(long since, int probeCursor) =>
        Timeline.Render(since, Probe, probeCursor, _foreground, [Panel]);

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _stopping.Dispose();
        _foreground?.Dispose();
        _orchestrator?.Dispose();
        _lab?.Dispose();
        _hotkey?.Dispose();
        _monitor?.Dispose();
        _thread?.Dispose();
        if (_probe is not null)
        {
            await _probe.DisposeAsync();
        }
    }

    private static T Require<T>(T? value)
        where T : class =>
        value ?? throw new InvalidOperationException(DesktopTestEnvironment.SkipReason);

    /// <summary>The internal key effects of a composition that never injects: every request is refused.</summary>
    private sealed class NoKeyEffects : IInternalKeyEffects
    {
        public ValueTask<bool> SendRightsHotkeyAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);

        public ValueTask<bool> SendDictationChordAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);
    }
}
