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

        _probe = await InputProbeSession.StartAsync(TestContext.Current.CancellationToken);
        var probe = _probe;
        var arbiter = new RecordingArbiter
        {
            Restore = _ => probe.TryBringToForegroundAsync(ForegroundTimeout),
        };
        var lab = SurfaceLab.Create(arbiter);
        _lab = lab;
        _panel = lab.CreateSurface(SurfaceKind.Panel, 0, 360, 240);
        _dock = lab.CreateSurface(SurfaceKind.Dock, 0, 48, 240);
        _side = lab.CreateSurface(SurfaceKind.SideWindow, 0, 240, 240);
        _bubble = lab.CreateSurface(SurfaceKind.Bubble, 0, 64, 64);

        // Side by side in the middle of the primary work area, away from docked bars at its edges.
        var (work, dpi) = NativeSurface.PrimaryWorkArea();
        var scale = dpi / 96.0;
        TestSurface[] surfaces = [_panel, _dock, _side, _bubble];
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
                surface.ShowPassive();
                left += (surface.Width + Gap) * scale;
            }

            lab.Integrity.Start();
        });
    }

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
    public static async Task WaitUntilAsync(Func<bool> condition, string because)
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
                condition().ShouldBeTrue(because + " (" + ForegroundWindows.Describe() + ")");
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
        _lab?.Dispose();
        if (_probe is not null)
        {
            await _probe.DisposeAsync();
        }
    }

    private static InvalidOperationException NotStarted() => new(DesktopTestEnvironment.SkipReason);
}
