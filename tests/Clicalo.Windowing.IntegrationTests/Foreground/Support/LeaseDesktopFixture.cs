using System.Diagnostics;
using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Platform.Windows.Foreground;
using Clicalo.Platform.Windows.SysEvents;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Foreground.Support;

/// <summary>
/// The desktop of the S4 lease tests, composed as the product composes it (blueprint §3.5, §3.6): InputProbe as the app
/// in front; a real <see cref="ForegroundOrchestrator"/> over the real adapters of Platform.Windows
/// (<see cref="ForegroundControl"/>, <see cref="ForegroundMonitor"/> and <see cref="InternalRightsHotkey"/> on their
/// own <see cref="SysEventsThread"/>) and the <c>SurfaceRegistry</c> of the WPF thread, which is also the arbiter of its
/// <c>ActivationGuard</c>; a panel (<see cref="LeasePanel"/>) and the search (<see cref="SearchSurface"/>) shown
/// passively; the real <see cref="TouchKeyboard"/> over <see cref="GuardedDictationKeys"/>. Started only when desktop
/// tests are enabled.
/// </summary>
/// <remarks>
/// The flows (<see cref="OpenSearchAsync"/>, <see cref="OpenControlCenterAsync"/> and their closing) run on the WPF
/// thread, as the gesture handlers of the product do: the orchestrator leaves that thread before it touches the
/// foreground (§3.2).
/// </remarks>
public sealed class LeaseDesktopFixture : IAsyncLifetime
{
    /// <summary>How long a test waits for the foreground, the monitor, a tap or a flow.</summary>
    public static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private const double Gap = 24;

    private InputProbeSession? _probe;
    private SysEventsThread? _thread;
    private ForegroundMonitor? _monitor;
    private InternalRightsHotkey? _hotkey;
    private bool _registered;
    private SurfaceLab? _lab;
    private LeasePanel? _panel;
    private SearchSurface? _search;
    private GuardedDictationKeys? _keys;
    private TouchKeyboard? _keyboard;
    private ForegroundOrchestrator? _orchestrator;
    private Func<Task>? _onTap;

    /// <summary>The app in front.</summary>
    public InputProbeSession Probe => Require(_probe);

    /// <summary>The real <c>SetForegroundWindow</c> adapter.</summary>
    public ForegroundControl Control { get; } = new();

    /// <summary>The real WinEvent monitor, started.</summary>
    public ForegroundMonitor Monitor => Require(_monitor);

    /// <summary>The reserved chord, registered when <see cref="RequireRegisteredHotkey"/> passes.</summary>
    public InternalRightsHotkey Hotkey => Require(_hotkey);

    /// <summary>The windowing of the WPF thread; its arbiter records every violation and forwards it to the orchestrator.</summary>
    public SurfaceLab Lab => Require(_lab);

    /// <summary>The panel whose tap opens the search or the Control Center.</summary>
    public LeasePanel Panel => Require(_panel);

    /// <summary>The search surface.</summary>
    public SearchSurface Search => Require(_search);

    /// <summary>The guarded key effects (Win+H).</summary>
    public GuardedDictationKeys Keys => Require(_keys);

    /// <summary>The real touch keyboard adapter, whose dictation goes through <see cref="Keys"/>.</summary>
    public TouchKeyboard Keyboard => Require(_keyboard);

    /// <summary>The real orchestrator, the arbiter behind the guard.</summary>
    public ForegroundOrchestrator Orchestrator => Require(_orchestrator);

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
        _hotkey = new InternalRightsHotkey(_thread, TimeProvider.System);
        _registered = await _hotkey.RegisterAsync();

        var arbiter = new RecordingArbiter();
        var lab = SurfaceLab.Create(arbiter);
        _lab = lab;
        _keys = new GuardedDictationKeys(IsOwnFocusedField);
        _keyboard = new TouchKeyboard(_keys);
        _orchestrator = CreateOrchestrator(Control);
        arbiter.Forward = _orchestrator;

        var (work, dpi) = NativeSurface.PrimaryWorkArea();
        var scale = dpi / 96.0;
        var firstFrame = WpfThread.Invoke(() =>
        {
            var panel = new LeasePanel(new SurfaceId(SurfaceKind.Panel, 0), lab.Registry, 200, 140);
            var search = new SearchSurface(
                new SurfaceId(SurfaceKind.SideWindow, 0),
                lab.Registry,
                360,
                80
            );
            panel.Tapped += (_, _) => OnPanelTapped();

            // Side by side in the middle of the primary work area, away from docked bars at its edges.
            var left = work.CenterX - ((panel.Width + Gap + search.Width) * scale / 2);
            var top = work.CenterY - (panel.Height * scale / 2);
            panel.MovePassive(Bounds(left, top, panel.Width, panel.Height, scale));
            search.MovePassive(
                Bounds(
                    left + ((panel.Width + Gap) * scale),
                    top,
                    search.Width,
                    search.Height,
                    scale
                )
            );
            var composed = FirstFrame.Watch(panel);
            panel.ShowPassive();
            _panel = panel;
            _search = search;
            return composed;
        });

        // The tests tap the panel first: a tap sent before it is composed falls through to the window below.
        await firstFrame.WaitAsync(EventTimeout, TestContext.Current.CancellationToken);
    }

    /// <summary>A new orchestrator over the real adapters, or <paramref name="control"/>; the caller disposes it.</summary>
    public ForegroundOrchestrator CreateOrchestrator(IForegroundControl control) =>
        new(
            new ForegroundPorts
            {
                Control = control,
                Monitor = Monitor,
                SurfaceStyle = Lab.Registry,
                Surfaces = Lab.Registry,
                RightsHotkey = Hotkey,
                KeyEffects = Keys,
            },
            TimeProvider.System
        );

    /// <summary>Fails with a diagnostic when another program owns Ctrl+Alt+Shift+F24.</summary>
    public void RequireRegisteredHotkey() =>
        _registered.ShouldBeTrue(
            "Ctrl+Alt+Shift+F24 could not be registered: another program (a running SpikeLab or Clícalo?) owns it."
        );

    /// <summary>
    /// Makes sure the probe owns the foreground and that the orchestrator has verified it (a lease returns to that
    /// window), and returns the cursor from which the calling test's probe events start.
    /// </summary>
    public async Task<int> PrepareAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await Probe.EnsureForegroundAsync(EventTimeout, cancellationToken);
        await WaitUntilAsync(
            () => Orchestrator.Current.Window == ProbeWindow,
            "The foreground monitor never verified InputProbe as the external foreground."
        );
        await Probe.PingAsync(EventTimeout, cancellationToken);
        return Probe.Cursor;
    }

    /// <summary>
    /// Taps the panel with a synthetic finger (guarded: only windows of this process and the probe) and returns what
    /// <paramref name="flow"/> returned: the flow starts on the WPF thread, inside the panel's window procedure.
    /// </summary>
    public async Task<T> TapPanelAsync<T>(Func<Task<T>> flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        var started = new TaskCompletionSource<Task<T>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Volatile.Write(
            ref _onTap,
            () =>
            {
                var running = flow();
                started.TrySetResult(running);
                return running;
            }
        );
        try
        {
            var bounds = NativeSurface.Bounds(Panel.Handle);
            using (var finger = new SyntheticPointer(SyntheticPointerKind.Finger, AllowedProcesses))
            {
                finger.Tap(bounds.CenterX, bounds.CenterY);
            }

            var running = await started.Task.WaitAsync(
                EventTimeout,
                TestContext.Current.CancellationToken
            );
            return await running.WaitAsync(EventTimeout, TestContext.Current.CancellationToken);
        }
        finally
        {
            Volatile.Write(ref _onTap, null);
        }
    }

    /// <summary>Runs <paramref name="flow"/> on the WPF thread and returns its result.</summary>
    public static async Task<T> OnUiThreadAsync<T>(Func<Task<T>> flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        var running = await WpfThread.Dispatcher.InvokeAsync(flow);
        return await running.WaitAsync(EventTimeout, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The search flow (UI thread): shows the search passively, asks for a <c>TextInput</c> lease with
    /// <paramref name="origin"/> through <paramref name="orchestrator"/> and, once granted, focuses its field. A denied
    /// lease hides the search again.
    /// </summary>
    public async Task<LeaseResult> OpenSearchAsync(
        LeaseOrigin origin,
        ForegroundOrchestrator? orchestrator = null
    )
    {
        var search = Search;
        search.ShowPassive();
        var result = await (orchestrator ?? Orchestrator).AcquireAsync(
            new LeaseRequest(
                LeaseKind.TextInput,
                search.SurfaceWindow,
                origin,
                Timeout.InfiniteTimeSpan
            ),
            TestContext.Current.CancellationToken
        );
        if (result is LeaseResult.Granted)
        {
            search.FocusField();
        }
        else
        {
            search.HidePassive();
        }

        return result;
    }

    /// <summary>Closes the search (UI thread): gives the foreground back, verified, then hides it.</summary>
    public async Task<RestoreOutcome> CloseSearchAsync(ForegroundLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var outcome = await lease.RestoreAsync(TestContext.Current.CancellationToken);
        Search.HidePassive();
        Search.Field.Clear();
        return outcome;
    }

    /// <summary>
    /// The Control Center flow (UI thread): shows <paramref name="window"/> without activating it and asks for a
    /// <c>ControlCenter</c> lease with <paramref name="origin"/>; once granted, focuses its field.
    /// </summary>
    public async Task<LeaseResult> OpenControlCenterAsync(
        TestControlCenter window,
        LeaseOrigin origin
    )
    {
        ArgumentNullException.ThrowIfNull(window);
        window.Show();
        var result = await Orchestrator.AcquireAsync(
            new LeaseRequest(LeaseKind.ControlCenter, window.Token, origin, null),
            TestContext.Current.CancellationToken
        );
        if (result is LeaseResult.Granted)
        {
            window.FocusField();
        }
        else
        {
            window.Close();
        }

        return result;
    }

    /// <summary>
    /// Closes the Control Center (UI thread) as CCM-004 asks: the foreground goes back first, verified, and only then does
    /// the window close, so Windows never activates another window of this process in between.
    /// </summary>
    public static async Task<RestoreOutcome> CloseControlCenterAsync(
        TestControlCenter window,
        ForegroundLease lease
    )
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(lease);
        var outcome = await lease.RestoreAsync(TestContext.Current.CancellationToken);
        window.Close();
        return outcome;
    }

    /// <summary>Waits until <paramref name="condition"/> holds, failing with <paramref name="because"/> on timeout.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var started = Stopwatch.GetTimestamp();
        while (!condition())
        {
            if (Stopwatch.GetElapsedTime(started) > EventTimeout)
            {
                condition().ShouldBeTrue(because + " (" + ForegroundWindows.Describe() + ")");
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _orchestrator?.Dispose();
        _keyboard?.Dispose();
        _lab?.Dispose();
        _hotkey?.Dispose();
        _monitor?.Dispose();
        _thread?.Dispose();
        if (_probe is not null)
        {
            await _probe.DisposeAsync();
        }
    }

    private int[] AllowedProcesses => [Environment.ProcessId, Probe.ProcessId];

    private static PhysicalRect Bounds(
        double left,
        double top,
        double width,
        double height,
        double scale
    ) =>
        new(
            (int)Math.Round(left),
            (int)Math.Round(top),
            (int)Math.Round(width * scale),
            (int)Math.Round(height * scale)
        );

    private static T Require<T>(T? value)
        where T : class =>
        value ?? throw new InvalidOperationException(DesktopTestEnvironment.SkipReason);

    /// <summary>
    /// The rule of Win+H (<see cref="GuardedDictationKeys"/>): <paramref name="foreground"/> is the search, active under
    /// its lease, and its field has the keyboard focus.
    /// </summary>
    private bool IsOwnFocusedField(nint foreground)
    {
        var search = _search;
        return search is not null
            && foreground != 0
            && foreground == search.Handle
            && WpfThread.Invoke(() => search.IsActive && search.FieldHasKeyboardFocus);
    }

    private void OnPanelTapped()
    {
        if (Interlocked.Exchange(ref _onTap, null) is { } flow)
        {
            _ = flow();
        }
    }
}
