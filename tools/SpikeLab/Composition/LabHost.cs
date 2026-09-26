using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Touch;
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
/// The composition root of the laboratory: creates the real pieces of the product through their contracts, records
/// on the <see cref="ComponentBoard"/> which ones already work, and keeps going without the ones whose M1 package is
/// not integrated yet. <c>ForegroundOrchestrator</c> is found by name and built with <see cref="ConstructorBinder"/>,
/// so the laboratory compiles against the contracts only and picks the real orchestrator up as soon as the foreground
/// package is merged. Everything runs on the UI thread except the pieces that live on the SysEvents thread.
/// </summary>
internal sealed class LabHost : IAsyncDisposable
{
    /// <summary>Full name of the orchestrator the foreground package adds in M1.</summary>
    public const string OrchestratorTypeName =
        "Clicalo.Application.Foreground.ForegroundOrchestrator";

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
                    "Violación de REG-01 sin ForegroundOrchestrator: nadie devuelve el primer plano."
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

    /// <summary>Forwards <c>ActivationGuard</c> to the orchestrator once it exists.</summary>
    public ArbiterRelay Relay { get; }

    /// <summary>The real <see cref="ActivationGuard"/> (<c>reg01.violations</c>).</summary>
    public ActivationGuard Guard { get; }

    /// <summary>The real <see cref="OwnerAnchor"/>.</summary>
    public OwnerAnchor Anchor { get; }

    /// <summary>The real <see cref="SurfaceRegistry"/>.</summary>
    public SurfaceRegistry Registry { get; }

    /// <summary>The real SysEvents thread, when integrated.</summary>
    public SysEventsThread? SysEvents { get; private set; }

    /// <summary>The real <see cref="IForegroundMonitor"/>, when integrated.</summary>
    public ForegroundMonitor? Monitor { get; private set; }

    /// <summary>
    /// The real <see cref="IInternalRightsHotkey"/>, when integrated; check <c>IsRegistered</c> (another program may
    /// own the chord, and then step 2 of the ladder is skipped).
    /// </summary>
    public InternalRightsHotkey? RightsHotkey { get; private set; }

    /// <summary>The real <see cref="IForegroundControl"/>, when integrated.</summary>
    public ForegroundControl? Control { get; private set; }

    /// <summary>The real <see cref="ITouchKeyboard"/>, when integrated.</summary>
    public TouchKeyboard? TouchKeyboard { get; private set; }

    /// <summary>The real <see cref="IForegroundOrchestrator"/>, when the foreground package is integrated.</summary>
    public IForegroundOrchestrator? Orchestrator { get; private set; }

    /// <summary>The real <see cref="SurfaceIntegrityCheck"/>, when integrated.</summary>
    public SurfaceIntegrityCheck? Integrity { get; private set; }

    /// <summary>The real tray icon, when integrated.</summary>
    public TrayIcon? Tray { get; private set; }

    /// <summary>The real tray menu host, when integrated.</summary>
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

        // The pointer layer is attached by each surface; these probes say early whether its package is integrated.
        Board.Try(
            "GestureRecognizer",
            "Gestos del dominio (toque, filtro de TAC-002).",
            () =>
                new GestureRecognizer(
                    LabSurfaceContext.DefaultTouchSettings,
                    dpiScale: 1
                ).SetTargets([])
        );
        var pointerProbe = new Window();
        try
        {
            Board.Try(
                "PointerInputSource",
                "WM_POINTER propio en cada superficie (ADR-0006).",
                () =>
                {
                    using var source = new PointerInputSource(pointerProbe, new NoFrames(), Time);
                    _ = source.IsAttached;
                }
            );
        }
        finally
        {
            // Never shown, but a Window stays in Application.Windows until it is closed.
            pointerProbe.Close();
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
        switch (Orchestrator)
        {
            case IAsyncDisposable asyncDisposable:
                try
                {
                    await asyncDisposable.DisposeAsync();
                }
                catch (Exception ex) when (ComponentBoard.IsContained(ex))
                {
                    // Shutting down: see DisposeQuietly.
                }

                break;
            case IDisposable disposable:
                DisposeQuietly(disposable.Dispose);
                break;
        }

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
        var type = typeof(IForegroundOrchestrator).Assembly.GetType(
            OrchestratorTypeName,
            throwOnError: false
        );
        if (type is null)
        {
            Board.Pending(
                Name,
                "Pendiente de integrar: el paquete foreground aún no añadió "
                    + OrchestratorTypeName
                    + "."
            );
            return;
        }

        List<object> services = [Registry, KeyEffects, Time];
        if (Control is not null)
        {
            services.Add(Control);
        }

        if (Monitor is not null)
        {
            services.Add(Monitor);
        }

        if (RightsHotkey is not null)
        {
            services.Add(RightsHotkey);
        }

        try
        {
            var result = ConstructorBinder.Create(type, services);
            if (result.Instance is IForegroundOrchestrator orchestrator)
            {
                Orchestrator = orchestrator;
                if (orchestrator is IActivationArbiter arbiter)
                {
                    Relay.Target = arbiter;
                }

                Board.Ready(Name, "Concesiones tipadas y escalera por origen.");
            }
            else
            {
                Board.Fail(Name, result.Problem ?? "No se pudo crear.");
            }
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

    private sealed class NoFrames : IPointerFrameSink
    {
        public void OnFrame(in PointerFrame frame) { }

        public void OnHover(bool inside) { }
    }
}
