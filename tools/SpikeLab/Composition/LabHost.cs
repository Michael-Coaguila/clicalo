using System.Windows.Controls;
using System.Windows.Threading;
using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Platform.Windows.Foreground;
using Clicalo.Platform.Windows.SysEvents;
using Clicalo.Platform.Windows.Tray;
using Clicalo.Tools.SpikeLab.Input;
using Clicalo.Tools.SpikeLab.Measurement;
using Clicalo.Tools.SpikeLab.Surfaces;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Pointer;
using Clicalo.UI.Wpf.Windowing;
using Windows.Win32;

namespace Clicalo.Tools.SpikeLab.Composition;

/// <summary>
/// The composition root of the laboratory: creates the real pieces of the product (windowing, pointer layer, UI
/// Automation, the SysEvents adapters and <see cref="ForegroundOrchestrator"/>) and records on the
/// <see cref="ComponentBoard"/> whether each one started; a piece that fails (a shortcut another program owns, a
/// broken Explorer) is shown in red and the laboratory goes on with the rest. Everything runs on the UI thread except
/// the pieces that live on the SysEvents thread.
/// </summary>
internal sealed class LabHost : IAsyncDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private LabHotkey? _hotkey;
    private bool _disposed;

    /// <summary>Creates the host on the UI thread of <paramref name="dispatcher"/>.</summary>
    public LabHost(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        Time = TimeProvider.System;
        Log = new LabEventLog(Time);
        Relay = new ArbiterRelay();
        Guard = new ActivationGuard(Relay, Time);
        Anchor = new OwnerAnchor();
        Registry = new SurfaceRegistry(Anchor, Guard);
        Measurements = new LabMeasurements(() => Guard.Violations, Log, Post);
        Probe = new ProbeMonitor(Measurements);
        Watcher = new ForegroundWatcher(Time, Directory.NameOf, () => Probe.Window, OnForeground);
        Injector = new LabKeyInjector();
        Latch = new ModifierLatch();
        KeyEffects = new LabInternalKeyEffects(
            () => RightsHotkey,
            () => PInvoke.GetForegroundWindow(),
            IsOwnOrProbe,
            LabInternalKeyEffects.SendGuarded,
            Measurements.OnRefused,
            Measurements.OnRightsChord
        );
        Guard.ViolationDetected += (_, args) =>
            Measurements.OnViolation(
                args.Violation.Surface
                    + " se activó sin concesión ("
                    + args.Violation.Message
                    + ", causa probable "
                    + args.Violation.ProbableCause
                    + ")."
            );
        Relay.ViolationReported += (_, args) =>
        {
            if (!args.Forwarded)
            {
                Measurements.Notice(
                    "Violación de REG-01 sin ForegroundOrchestrator (ver piezas): nadie devuelve el primer plano."
                );
            }
        };
    }

    /// <summary>The clock of every real piece.</summary>
    public TimeProvider Time { get; }

    /// <summary>Which pieces work.</summary>
    public ComponentBoard Board { get; } = new();

    /// <summary>The timeline.</summary>
    public LabEventLog Log { get; }

    /// <summary>The windows of the lab surfaces.</summary>
    public SurfaceDirectory Directory { get; } = new();

    /// <summary>The automatic measurements.</summary>
    public LabMeasurements Measurements { get; }

    /// <summary>The laboratory's own view of the foreground.</summary>
    public ForegroundWatcher Watcher { get; }

    /// <summary>InputProbe, opened on demand.</summary>
    public ProbeMonitor Probe { get; }

    /// <summary>The panel's injector (only while «Enviar teclas» is on).</summary>
    public LabKeyInjector Injector { get; }

    /// <summary>The latched modifiers of «Mayús» and «Mantener Ctrl».</summary>
    public ModifierLatch Latch { get; }

    /// <summary>The guarded implementation of <see cref="IInternalKeyEffects"/>.</summary>
    public LabInternalKeyEffects KeyEffects { get; }

    /// <summary>Forwards <c>ActivationGuard</c> to the orchestrator, created after the guard.</summary>
    public ArbiterRelay Relay { get; }

    /// <summary>The real <see cref="ActivationGuard"/> (<c>reg01.violations</c>).</summary>
    public ActivationGuard Guard { get; }

    /// <summary>The real <see cref="OwnerAnchor"/>.</summary>
    public OwnerAnchor Anchor { get; }

    /// <summary>The real <see cref="SurfaceRegistry"/>.</summary>
    public SurfaceRegistry Registry { get; }

    /// <summary>The real SysEvents thread, once started.</summary>
    public SysEventsThread? SysEvents { get; private set; }

    /// <summary>The real <see cref="IForegroundMonitor"/>, once started.</summary>
    public ForegroundMonitor? Monitor { get; private set; }

    /// <summary>
    /// The real <see cref="IInternalRightsHotkey"/>, once it answered; check <c>IsRegistered</c> (another program may
    /// own the chord, and then step 2 of the ladder is skipped).
    /// </summary>
    public InternalRightsHotkey? RightsHotkey { get; private set; }

    /// <summary>The real <see cref="IForegroundControl"/>.</summary>
    public ForegroundControl? Control { get; private set; }

    /// <summary>The real <see cref="ITouchKeyboard"/>.</summary>
    public TouchKeyboard? TouchKeyboard { get; private set; }

    /// <summary>
    /// The real <see cref="ForegroundOrchestrator"/>, once its ports started; null when one of them failed (see the
    /// board).
    /// </summary>
    public ForegroundOrchestrator? Orchestrator { get; private set; }

    /// <summary>The real <see cref="SurfaceIntegrityCheck"/>, once started.</summary>
    public SurfaceIntegrityCheck? Integrity { get; private set; }

    /// <summary>The real tray icon, once shown.</summary>
    public TrayIcon? Tray { get; private set; }

    /// <summary>The real tray menu host, once started.</summary>
    public TrayMenuHost? TrayMenu { get; private set; }

    /// <summary>Called on the UI thread when the tray icon asks for its menu (anchor in physical pixels).</summary>
    public Action<PhysicalPoint>? TrayMenuRequested { get; set; }

    /// <summary>Called on the UI thread when the lab global shortcut is pressed.</summary>
    public Action? GlobalHotkeyPressed { get; set; }

    /// <summary>Called on the UI thread after every foreground change.</summary>
    public Action<ForegroundChange>? ForegroundChanged { get; set; }

    /// <summary>Runs <paramref name="action"/> on the UI thread.</summary>
    public void Post(Action action) => _ = _dispatcher.BeginInvoke(action);

    /// <summary>
    /// Composes every piece. With <paramref name="quiet"/> (<c>--check</c>) nothing visible is created: no tray icon
    /// and no global shortcut.
    /// </summary>
    public async Task StartAsync(bool quiet)
    {
        Board.Try(
            "ForegroundWatcher (SpikeLab)",
            "Observa el primer plano, también el propio.",
            Watcher.Start
        );
        Board.Try("ActivationGuard", "Contador reg01.violations.", () => _ = Guard.Violations);
        Board.Try(
            "OwnerAnchor",
            "Propietaria oculta de las superficies.",
            () => Anchor.EnsureCreated()
        );
        Board.Try(
            "SurfaceRegistry",
            "Registro de superficies y estilo de activación.",
            () => _ = Registry.Surfaces
        );

        // The surfaces attach PointerInputSource and GestureHost themselves (and report them); the process routes the
        // mouse through the pointer messages before the first window (Program), so a click is a pointer frame too.
        if (PointerSetup.IsStylusAndTouchSupportDisabled && PointerSetup.IsMouseInPointerEnabled)
        {
            Board.Ready(
                "PointerSetup",
                "Pila táctil de WPF apagada y mouse como WM_POINTER (blueprint §8.3)."
            );
        }
        else
        {
            Board.Fail(
                "PointerSetup",
                "La pila táctil de WPF sigue activa o el mouse no llega como WM_POINTER: los toques no se medirán bien."
            );
        }

        Board.Try(
            "LiveAnnouncer",
            "Región live y RaiseNotificationEvent (ACC-001).",
            () => _ = new LiveAnnouncer(new TextBlock()).LastText
        );

        var control = new ForegroundControl();
        if (
            Board.Try(
                "ForegroundControl",
                "El único SetForegroundWindow.",
                () => _ = control.GetForeground()
            )
        )
        {
            Control = control;
        }

        var keyboard = new TouchKeyboard(KeyEffects);
        if (Board.Try("TouchKeyboard", "Teclado táctil y Win+H.", () => _ = keyboard.OccludedArea))
        {
            TouchKeyboard = keyboard;
        }

        await StartSysEventsAsync(quiet);
        CreateOrchestrator();

        var integrity = new SurfaceIntegrityCheck(Registry, Time);
        if (
            Board.Try(
                "SurfaceIntegrityCheck",
                "Repara WS_EX_NOACTIVATE y el orden Z.",
                integrity.Start
            )
        )
        {
            Integrity = integrity;
        }

        if (!quiet)
        {
            var hotkey = new LabHotkey(() => GlobalHotkeyPressed?.Invoke());
            if (
                Board.Try(
                    "Atajo global de laboratorio",
                    LabHotkey.Description + " abre la búsqueda (S4).",
                    hotkey.Register
                )
            )
            {
                _hotkey = hotkey;
            }
            else
            {
                hotkey.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _hotkey?.Dispose();

        // The orchestrator first: it may still hold a lease or a restore on the pieces disposed below.
        DisposeQuietly(() => Orchestrator?.Dispose());

        DisposeQuietly(() => Integrity?.Dispose());
        DisposeQuietly(() => Tray?.Dispose());
        DisposeQuietly(() => TrayMenu?.Dispose());
        DisposeQuietly(() => RightsHotkey?.Dispose());
        DisposeQuietly(() => Monitor?.Dispose());
        DisposeQuietly(() => SysEvents?.Dispose());
        await Probe.DisposeAsync();
        Watcher.Dispose();
        Anchor.Dispose();
    }

    private static void DisposeQuietly(Action dispose)
    {
        try
        {
            dispose();
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            // Shutting down: a piece that cannot dispose itself must not keep the laboratory open.
        }
    }

    private async Task StartSysEventsAsync(bool quiet)
    {
        SysEventsThread? thread = null;
        if (
            !Board.Try(
                "SysEventsThread",
                "Hilo SysEvents con su ventana de mensajes.",
                () => thread = SysEventsThread.Start()
            )
        )
        {
            const string Waiting = "Espera a SysEventsThread.";
            Board.Pending("ForegroundMonitor", Waiting);
            Board.Pending("InternalRightsHotkey", Waiting);
            Board.Pending("TrayMenuHost", Waiting);
            if (!quiet)
            {
                Board.Pending("TrayIcon", Waiting);
            }

            return;
        }

        SysEvents = thread!;
        var monitor = new ForegroundMonitor(SysEvents, Time);
        if (
            await Board.TryAsync(
                "ForegroundMonitor",
                "Primer plano externo verificado.",
                monitor.StartAsync
            )
        )
        {
            Monitor = monitor;
        }
        else
        {
            DisposeQuietly(monitor.Dispose);
        }

        var rights = new InternalRightsHotkey(SysEvents, Time);
        var answered = false;
        await Board.TryAsync(
            "InternalRightsHotkey",
            "Ctrl+Alt+Mayús+F24 registrado (paso 2 de la escalera).",
            async () =>
            {
                var registered = await rights.RegisterAsync();
                answered = true;
                if (!registered)
                {
                    throw new InvalidOperationException(
                        "Otro programa ya registró Ctrl+Alt+Mayús+F24: el paso 2 de la escalera no se usará."
                    );
                }
            }
        );
        if (answered)
        {
            // Registered or not, it is the real piece: the orchestrator reads IsRegistered and skips step 2 without
            // it, and LabInternalKeyEffects never injects an unregistered chord.
            RightsHotkey = rights;
        }
        else
        {
            DisposeQuietly(rights.Dispose);
        }

        var menu = new TrayMenuHost(SysEvents);
        if (
            await Board.TryAsync(
                "TrayMenuHost",
                "Menú de la bandeja con concesión TrayMenu.",
                menu.StartAsync
            )
        )
        {
            TrayMenu = menu;
        }
        else
        {
            DisposeQuietly(menu.Dispose);
        }

        if (quiet)
        {
            return;
        }

        var tray = new TrayIcon(SysEvents);
        tray.Invoked += (_, args) => Post(() => TrayMenuRequested?.Invoke(args.Position));
        tray.MenuRequested += (_, args) => Post(() => TrayMenuRequested?.Invoke(args.Position));
        if (
            await Board.TryAsync(
                "TrayIcon",
                "Icono de bandeja de SpikeLab.",
                () => tray.ShowAsync("Clícalo SpikeLab")
            )
        )
        {
            Tray = tray;
        }
        else
        {
            DisposeQuietly(tray.Dispose);
        }
    }

    private void CreateOrchestrator()
    {
        const string Name = "ForegroundOrchestrator";
        if (Control is null || Monitor is null || RightsHotkey is null)
        {
            Board.Pending(
                Name,
                "Espera a ForegroundControl, ForegroundMonitor e InternalRightsHotkey (ver arriba)."
            );
            return;
        }

        try
        {
            // SurfaceRegistry is both the activation style and the lookup of the surfaces (blueprint §3.5, §3.6).
            var orchestrator = new ForegroundOrchestrator(
                new ForegroundPorts
                {
                    Control = Control,
                    Monitor = Monitor,
                    SurfaceStyle = Registry,
                    Surfaces = Registry,
                    RightsHotkey = RightsHotkey,
                    KeyEffects = KeyEffects,
                },
                Time
            );
            Orchestrator = orchestrator;
            Relay.Target = orchestrator;
            Board.Ready(Name, "Concesiones tipadas y escalera por origen.");
        }
        catch (Exception ex) when (ComponentBoard.IsContained(ex))
        {
            Board.Fail(Name, ex);
        }
    }

    private void OnForeground(ForegroundChange change)
    {
        Measurements.OnForeground(change);
        ForegroundChanged?.Invoke(change);
    }

    private bool IsOwnOrProbe(nint window)
    {
        var (processId, _) = ProcessNames.Of(window);
        return LabTargetPolicy.IsOwnOrProbe(window, processId, _ownProcessId, Probe.Window);
    }
}
