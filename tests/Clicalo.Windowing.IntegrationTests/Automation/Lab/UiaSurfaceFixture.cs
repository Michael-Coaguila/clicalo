using System.Windows.Media;
using Clicalo.Application.Ports;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace Clicalo.Windowing.IntegrationTests.Automation.Lab;

/// <summary>
/// The S3 desktop setup, one per test class: InputProbe as the foreground application (the physical truth), a
/// <see cref="LabSurface"/> shown passively on the WPF thread with its own registry, anchor, guard and
/// <see cref="RecordingArbiter"/>, and a FlaUI UIA3 client. The client runs on the test threads and the surface on the
/// WPF thread, so every UI Automation call crosses to the surface's dispatcher as it does for Voice access. Nothing
/// here injects input: S3 only calls UI Automation patterns.
/// </summary>
public sealed class UiaSurfaceFixture : IAsyncLifetime
{
    /// <summary>How long a test waits for the probe to reach the foreground.</summary>
    public static readonly TimeSpan ForegroundTimeout = TimeSpan.FromSeconds(5);

    /// <summary>How long a test waits for an event (probe, tile or UI Automation).</summary>
    public static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private static int _nextInstance;

    private InputProbeSession? _probe;
    private UIA3Automation? _automation;
    private LabSurface? _surface;
    private OwnerAnchor? _anchor;
    private ActivationGuard? _guard;

    /// <summary>The foreground application.</summary>
    public InputProbeSession Probe =>
        _probe ?? throw new InvalidOperationException(DesktopTestEnvironment.SkipReason);

    /// <summary>The UI Automation client.</summary>
    public UIA3Automation Automation =>
        _automation ?? throw new InvalidOperationException(DesktopTestEnvironment.SkipReason);

    /// <summary>The surface (use it on the WPF thread).</summary>
    public LabSurface Surface =>
        _surface ?? throw new InvalidOperationException(DesktopTestEnvironment.SkipReason);

    /// <summary>The lab hosted by the surface (use it on the WPF thread, except its thread-safe log).</summary>
    public TileLab Lab => Surface.Lab;

    /// <summary>The guard that watches the surface (<c>reg01.violations</c>).</summary>
    public ActivationGuard Guard =>
        _guard ?? throw new InvalidOperationException(DesktopTestEnvironment.SkipReason);

    /// <summary>The arbiter of the guard: never leases, records violations.</summary>
    public RecordingArbiter Arbiter { get; } = new();

    /// <summary>The surface window handle.</summary>
    public nint SurfaceHandle { get; private set; }

    /// <summary>Device pixels per logical pixel of the surface's monitor.</summary>
    public double Scale { get; private set; } = 1;

    public async ValueTask InitializeAsync()
    {
        if (!DesktopTestEnvironment.IsEnabled)
        {
            return;
        }

        _probe = await InputProbeSession.StartAsync(TestContext.Current.CancellationToken);
        WpfThread.Invoke(() =>
        {
            _anchor = new OwnerAnchor();
            _guard = new ActivationGuard(Arbiter, TimeProvider.System);
            var registry = new SurfaceRegistry(_anchor, _guard);
            var id = new SurfaceId(SurfaceKind.Panel, Interlocked.Increment(ref _nextInstance));
            _surface = new LabSurface(id, registry, new TileLab());
            _surface.ShowPassive();
            WpfThread.DrainPendingWork();
            SurfaceHandle = _surface.SurfaceWindow.Handle;
            Scale = VisualTreeHelper.GetDpi(_surface).DpiScaleX;
        });
        _automation = new UIA3Automation();
    }

    /// <summary>
    /// Brings the probe to the foreground (failing with a diagnostic otherwise) and returns the probe cursor from
    /// which the calling test's events start.
    /// </summary>
    public async Task<int> PrepareAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await Probe.EnsureForegroundAsync(ForegroundTimeout, cancellationToken);
        await Probe.PingAsync(EventTimeout, cancellationToken);
        return Probe.Cursor;
    }

    /// <summary>The surface as a UI Automation element.</summary>
    public AutomationElement Window() => Automation.FromHandle(SurfaceHandle);

    /// <summary>The element with <paramref name="automationId"/> inside the surface.</summary>
    public AutomationElement Element(string automationId) =>
        Window().FindFirstDescendant(condition => condition.ByAutomationId(automationId))
        ?? throw new InvalidOperationException(
            "UI Automation did not find " + automationId + " in the surface."
        );

    /// <summary>
    /// UIA009 after one cycle: waits for the probe to answer a ping (so everything it received has arrived) and
    /// returns the violations of the foreground invariant since <paramref name="probeCursor"/>.
    /// </summary>
    public async Task<IReadOnlyList<UiaViolation>> ForegroundViolationsAsync(
        int probeCursor,
        string action
    )
    {
        await Probe.PingAsync(EventTimeout, TestContext.Current.CancellationToken);
        var surfaceActive = WpfThread.Invoke(() => Surface.IsActive);
        return ForegroundInvariant.Check(
            action,
            Probe.Window,
            SurfaceHandle,
            surfaceActive,
            Probe.EventsSince(probeCursor),
            Guard.Violations,
            Arbiter.Violations.Count
        );
    }

    public async ValueTask DisposeAsync()
    {
        _automation?.Dispose();
        if (_surface is not null)
        {
            WpfThread.Invoke(() =>
            {
                _surface.HidePassive();
                _surface.Close();
                _surface.Lab.Dispose();
                _anchor?.Dispose();
            });
        }

        if (_probe is not null)
        {
            await _probe.DisposeAsync();
        }
    }
}
