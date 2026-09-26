using System.Diagnostics;
using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Platform.IntegrationTests.Desktop;
using Clicalo.Platform.Windows.Foreground;
using Clicalo.Platform.Windows.SysEvents;
using Clicalo.Platform.Windows.Tray;
using Clicalo.TestKit.Windows;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// The real foreground adapters of Platform.Windows on one <see cref="SysEventsThread"/>, a <see cref="TestWindow"/>
/// standing in for a surface, the tray menu host and the guarded key effects, created only when desktop tests are
/// enabled. The InputProbe of the collection is the external app; the test sets <see cref="ProbeWindow"/>.
/// </summary>
public sealed class ForegroundDesktopFixture : IAsyncLifetime
{
    private SysEventsThread? _thread;
    private ForegroundMonitor? _monitor;
    private InternalRightsHotkey? _hotkey;
    private TrayMenuHost? _tray;
    private TestWindow? _window;
    private TestSurfaces? _surfaces;
    private GuardedInternalKeyEffects? _keys;
    private bool _registered;

    /// <summary>The SysEvents thread of the adapters.</summary>
    public SysEventsThread Thread => Require(_thread);

    /// <summary>The real <c>SetForegroundWindow</c> adapter.</summary>
    public ForegroundControl Control { get; } = new();

    /// <summary>The real WinEvent monitor, started.</summary>
    public ForegroundMonitor Monitor => Require(_monitor);

    /// <summary>The reserved chord, registered.</summary>
    public InternalRightsHotkey Hotkey => Require(_hotkey);

    /// <summary>The hidden tray menu host, started.</summary>
    public TrayMenuHost Tray => Require(_tray);

    /// <summary>The test surface.</summary>
    internal TestWindow Window => Require(_window);

    /// <summary>The surface ports over <see cref="Window"/>.</summary>
    internal TestSurfaces Surfaces => Require(_surfaces);

    /// <summary>The guarded injector of the reserved chord.</summary>
    internal GuardedInternalKeyEffects Keys => Require(_keys);

    /// <summary>The InputProbe window: the only foreign window the key effects may inject into.</summary>
    public nint ProbeWindow { get; set; }

    public async ValueTask InitializeAsync()
    {
        if (!DesktopTestEnvironment.IsEnabled)
        {
            return;
        }

        _thread = SysEventsThread.Start();
        _monitor = new ForegroundMonitor(_thread, TimeProvider.System);
        await _monitor.StartAsync();
        _hotkey = new InternalRightsHotkey(_thread, TimeProvider.System);
        _registered = await _hotkey.RegisterAsync();
        _tray = new TrayMenuHost(_thread);
        await _tray.StartAsync();
        _window = await TestWindow.CreateAsync(_thread);
        _surfaces = new TestSurfaces(_window);
        _keys = new GuardedInternalKeyEffects(
            _hotkey,
            window => window != 0 && (window == ProbeWindow || IsOwn(window)),
            IsOwn
        );
    }

    /// <summary>
    /// A new orchestrator over the real adapters (or <paramref name="control"/> wrapping the real one); the test
    /// disposes it.
    /// </summary>
    public ForegroundOrchestrator CreateOrchestrator(IForegroundControl? control = null) =>
        new(
            new ForegroundPorts
            {
                Control = control ?? Control,
                Monitor = Monitor,
                SurfaceStyle = Surfaces,
                Surfaces = Surfaces,
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
    /// Gives this process the foreground right the way a hotkey does: with the probe in front, the reserved chord is
    /// injected (guarded) and its <c>WM_HOTKEY</c> awaited. Stands in for the <c>WM_HOTKEY</c> of the global shortcut
    /// and for the click the shell forwards from the tray icon.
    /// </summary>
    public async Task GainRightsAsync(CancellationToken cancellationToken)
    {
        RequireRegisteredHotkey();
        var arrived = Hotkey.WaitForRightsAsync(cancellationToken).AsTask();
        (await Keys.SendRightsHotkeyAsync(cancellationToken)).ShouldBeTrue(
            "The chord was not injected (the probe was not in front?): "
                + ForegroundWindows.Describe()
        );
        (await arrived).ShouldBeTrue("WM_HOTKEY of the reserved chord did not arrive in time.");
    }

    /// <summary>
    /// <c>SetForegroundWindow</c> through the real adapter, then waits (up to <c>DesktopProbeFixture.EventTimeout</c>)
    /// until <c>GetForegroundWindow</c> confirms it: the thread that owns a window activates it asynchronously when the
    /// request comes from another thread (spike S4 finding).
    /// </summary>
    public async Task<bool> SetForegroundVerifiedAsync(
        WindowToken window,
        CancellationToken cancellationToken
    )
    {
        if (Control.TrySetForeground(window))
        {
            return true;
        }

        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < DesktopProbeFixture.EventTimeout)
        {
            await Task.Delay(MonitorPoll, cancellationToken);
            if (Control.GetForeground() == window)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Waits until the monitor has confirmed <paramref name="window"/> as the external foreground: WinEvents arrive
    /// asynchronously, and a lease returns to what the monitor last confirmed.
    /// </summary>
    public async Task WaitUntilMonitorSeesAsync(nint window, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        while (Monitor.Current?.Window.Handle != window)
        {
            if (Stopwatch.GetElapsedTime(started) > DesktopProbeFixture.EventTimeout)
            {
                throw new TimeoutException(
                    "The foreground monitor never confirmed the expected window: "
                        + ForegroundWindows.Describe()
                );
            }

            await Task.Delay(MonitorPoll, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_window is not null)
        {
            await _window.DisposeAsync();
        }

        _tray?.Dispose();
        _hotkey?.Dispose();
        _monitor?.Dispose();
        _thread?.Dispose();
    }

    private static TimeSpan MonitorPoll => TimeSpan.FromMilliseconds(20);

    private static T Require<T>(T? value)
        where T : class =>
        value ?? throw new InvalidOperationException(DesktopTestEnvironment.SkipReason);

    private bool IsOwn(nint window) =>
        window != 0 && (window == _window?.Handle || window == _tray?.Window.Handle);
}
