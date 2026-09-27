using System.Windows;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Foreground;
using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Application.Session;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;
using Clicalo.Platform.Windows.Foreground;
using Clicalo.Platform.Windows.SysEvents;
using Clicalo.Platform.Windows.Tray;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Theming;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The M2 panel composed as <c>Clicalo.App</c> composes it (blueprint §3.5, §3.6, §8.2), on a real desktop: InputProbe
/// as the app in front; the real <see cref="PanelWindow"/> over <see cref="PanelViewModel"/>, the
/// <see cref="PanelInteractionController"/> and the <see cref="SessionStore"/> of the WPF thread; the real
/// <see cref="ForegroundOrchestrator"/> over the Platform.Windows adapters, also the arbiter of the panel's
/// <c>ActivationGuard</c>; and the real <see cref="TrayController"/> with its hidden <see cref="TrayMenuHost"/> (the
/// notification area icon is never shown). The engine is <see cref="PanelEngineInbox"/>: it records what the panel
/// posts and sends no key, so nothing reaches any app. Started only when desktop tests are enabled.
/// </summary>
public sealed class PanelDesktopFixture : IAsyncLifetime
{
    /// <summary>How long a test waits for the foreground, a tap or a menu.</summary>
    public static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The touch filter of the panel: the default preset (TAC-001).</summary>
    public static readonly TouchSettings Touch = new(
        TouchPresets.Default.Debounce,
        TouchPresets.Default.HitSlopPx,
        TouchPresets.Default.CancelMovePx,
        TouchPresets.Default.MinContact
    );

    private InputProbeSession? _probe;
    private SysEventsThread? _thread;
    private ForegroundMonitor? _monitor;
    private InternalRightsHotkey? _hotkey;
    private SurfaceLab? _lab;
    private ForegroundOrchestrator? _orchestrator;
    private TrayMenuHost? _menu;
    private TrayIcon? _icon;
    private TrayController? _tray;
    private SessionStore? _session;
    private PanelViewModel? _viewModel;
    private PanelWindow? _window;
    private PanelVisibilityCoordinator? _visibility;

    /// <summary>The engine: every event the panel and the tray post.</summary>
    internal PanelEngineInbox Engine { get; } = new();

    /// <summary>The app in front.</summary>
    public InputProbeSession Probe => Require(_probe);

    /// <summary>The windowing of the WPF thread; its arbiter records violations and forwards them.</summary>
    public SurfaceLab Lab => Require(_lab);

    /// <summary>The real orchestrator.</summary>
    public ForegroundOrchestrator Orchestrator => Require(_orchestrator);

    /// <summary>The hidden tray menu host.</summary>
    public TrayMenuHost Menu => Require(_menu);

    /// <summary>The tray, without its icon.</summary>
    public TrayController Tray => Require(_tray);

    /// <summary>The session of the WPF thread.</summary>
    public SessionStore Session => Require(_session);

    /// <summary>The panel's view model (WPF thread).</summary>
    public PanelViewModel ViewModel => Require(_viewModel);

    /// <summary>The panel (WPF thread).</summary>
    public PanelWindow Window => Require(_window);

    /// <summary>Shows and hides the panel as the tray does (WPF thread).</summary>
    public PanelVisibilityCoordinator Visibility => Require(_visibility);

    /// <summary>The probe as a window token.</summary>
    public WindowToken ProbeWindow => new(Probe.Window);

    public async ValueTask InitializeAsync()
    {
        if (!DesktopTestEnvironment.IsEnabled)
        {
            return;
        }

        _probe = await InputProbeSession.StartAsync(TestContext.Current.CancellationToken);
        _thread = SysEventsThread.Start();
        _monitor = new ForegroundMonitor(_thread, TimeProvider.System);
        await _monitor.StartAsync();

        // Never registered: the ladder's step 2 (the internal chord) is not needed by the tray origin and no key is
        // ever injected by these tests.
        _hotkey = new InternalRightsHotkey(_thread, TimeProvider.System);
        var arbiter = new RecordingArbiter();
        _lab = SurfaceLab.Create(arbiter);
        _orchestrator = new ForegroundOrchestrator(
            new ForegroundPorts
            {
                Control = new ForegroundControl(),
                Monitor = _monitor,
                SurfaceStyle = _lab.Registry,
                Surfaces = _lab.Registry,
                RightsHotkey = _hotkey,
                KeyEffects = new NoKeyEffects(),
            },
            TimeProvider.System
        );
        arbiter.Forward = _orchestrator;
        _menu = new TrayMenuHost(_thread);
        await _menu.StartAsync();
        _icon = new TrayIcon(_thread);
        LocalizationContext localization = PanelTestData.Localization("es");
        _tray = new TrayController(_icon, _menu, _orchestrator, Engine, localization);

        WpfThread.Invoke(() =>
        {
            var session = new SessionStore(PanelSession.Initial(PanelTestData.Word));
            var controller = new PanelInteractionController(Engine, () => 1, TimeProvider.System);
            var viewModel = new PanelViewModel(controller, localization, Touch, _ => null);
            viewModel.Apply(
                PanelProjector.Project(PanelTestData.Profile(), LangCode.Es, LangCode.Es)
            );
            viewModel.ApplySession(session.Current);
            session.Changed += (_, change) => viewModel.ApplySession(change.Current);
            var window = new PanelWindow(
                viewModel,
                _lab.Registry,
                TimeProvider.System,
                PanelSizes.M,
                columns: 4,
                ThemeId.Dark
            );
            window.Present();
            _session = session;
            _viewModel = viewModel;
            _window = window;
            _visibility = new PanelVisibilityCoordinator(session, Engine);
        });
        WpfThread.Invoke(WpfThread.DrainPendingWork);
    }

    /// <summary>
    /// Makes sure the probe owns the foreground, the orchestrator verified it, no contact is down and the filter memory
    /// of every tile has expired; then forgets the events already posted. Returns the probe cursor.
    /// </summary>
    public async Task<int> PrepareAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await Probe.EnsureForegroundAsync(EventTimeout, cancellationToken);
        await WaitUntilAsync(
            () => Orchestrator.Current.Window == ProbeWindow,
            "The foreground monitor never verified InputProbe as the external foreground."
        );
        await Task.Delay(
            Touch.Debounce + Clicalo.Domain.Timing.Timings.Touch.PostSwipeLock,
            cancellationToken
        );
        WpfThread.Invoke(WpfThread.DrainPendingWork);
        Engine.Clear();
        await Probe.PingAsync(EventTimeout, cancellationToken);
        return Probe.Cursor;
    }

    /// <summary>A pointer that may only touch this process's windows.</summary>
    public static SyntheticPointer CreatePointer(SyntheticPointerKind kind) =>
        new(kind, [Environment.ProcessId]);

    /// <summary>The center of the tile of <paramref name="shortcut"/>, in physical screen pixels.</summary>
    public PhysicalPoint TileCenter(ShortcutId shortcut) =>
        WpfThread.Invoke(() =>
        {
            var index = ViewModel.Tiles.Select(static tile => tile.Id).ToList().IndexOf(shortcut);
            index.ShouldBeGreaterThanOrEqualTo(0, "the tile is on the panel");
            return CenterOf(Window.TileControls[index]);
        });

    /// <summary>The center of «Release all», in physical screen pixels (the strip must be visible).</summary>
    public PhysicalPoint ReleaseAllCenter() =>
        WpfThread.Invoke(() => CenterOf(Window.ReleaseAllButton));

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
                condition().ShouldBeTrue(because);
                return;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _tray?.Dispose();
        if (_window is not null)
        {
            WpfThread.Invoke(_window.Close);
        }

        _lab?.Dispose();
        _orchestrator?.Dispose();
        _hotkey?.Dispose();
        _monitor?.Dispose();
        _thread?.Dispose();
        if (_probe is not null)
        {
            await _probe.DisposeAsync();
        }
    }

    private static PhysicalPoint CenterOf(FrameworkElement element)
    {
        var center = element.PointToScreen(
            new Point(element.ActualWidth / 2, element.ActualHeight / 2)
        );
        return new PhysicalPoint((int)Math.Round(center.X), (int)Math.Round(center.Y));
    }

    private static T Require<T>(T? value)
        where T : class =>
        value
        ?? throw new InvalidOperationException(
            "The panel desktop is only composed when desktop tests are enabled (CLICALO_DESKTOP_TESTS=1)."
        );

    private sealed class NoKeyEffects : IInternalKeyEffects
    {
        public ValueTask<bool> SendRightsHotkeyAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);

        public ValueTask<bool> SendDictationChordAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);
    }
}
