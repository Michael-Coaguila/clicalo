using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using Clicalo.App.Composition;
using Clicalo.App.Localization;
using Clicalo.App.SingleInstance;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Application.Foreground;
using Clicalo.Application.Interaction;
using Clicalo.Application.Localization;
using Clicalo.Application.Persistence;
using Clicalo.Application.Ports;
using Clicalo.Application.Session;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases.Welcome;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Platform.Windows.Foreground;
using Clicalo.Platform.Windows.Input;
using Clicalo.Platform.Windows.Launch;
using Clicalo.Platform.Windows.SysEvents;
using Clicalo.Platform.Windows.Tray;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// The life of the running instance (blueprint §3.1, §3.2, §7.6), on the UI thread of the Surfaces role:
/// <list type="number">
/// <item>off the UI thread (<see cref="StartupReader"/>): a crash Sentinel reported goes to its journal; the document
/// is read (persistence; on a new installation the seed) and the language files loaded;</item>
/// <item>the preventive release of the start (SEG-006), before the engine accepts anything;</item>
/// <item>autosave, and the engine thread: it accepts touches from the first frame, it does not wait for the
/// guardian;</item>
/// <item>SysEvents follows the external foreground and the engine hears it (§7.9);</item>
/// <item>the panel is built and shown passively; Sentinel is launched from a background thread in parallel to the
/// first frame (or right after it with <c>--guardian after-first-frame</c>, spike S5);</item>
/// <item>then, once the panel is presented and the rights hotkey registered (without waiting for the first frame), the
/// surface checks, the tray and the single-instance pipe.</item>
/// </list>
/// <see cref="ExitAsync"/> is the only way out (<see cref="IAppLifetime"/>): the <see cref="ExitSequence"/> releases
/// everything and flushes, then what was started is disposed in reverse order and the WPF application ends. The end of
/// the Windows session runs the same sequence synchronously (<c>App/Shutdown</c>).
/// </summary>
internal sealed partial class AppHost : IAppLifetime, IDisposable
{
    private readonly AppOptions _options;
    private readonly InstanceIdentity _identity;
    private readonly InstanceMutex _mutex;
    private readonly ILoggerFactory _logs;
    private readonly ILogger _logger;
    private readonly bool _firstRunAfterInstall;
    private readonly CancellationTokenSource _stop = new();
    private readonly TimeProvider _time = TimeProvider.System;
    private readonly List<Action> _teardown = [];
    private readonly object _recoveryNotice = new();
    private System.Windows.Application? _application;
    private ServiceProvider? _services;
    private EngineThread? _engine;
    private PersistenceScheduler? _scheduler;
    private TrayController? _tray;
    private ShowPipeServer? _pipe;
    private ControlCenterComposer? _controlCenter;
    private WelcomeComposer? _welcome;
    private DocumentStore? _store;
    private Task _persistence = Task.CompletedTask;
    private Task _guardian = Task.CompletedTask;
    private Task? _exit;
    private bool _firstFrame;
    private bool _askReinstall;
    private Action? _beforeEnd;

    /// <summary>Creates the host of the instance that owns <paramref name="mutex"/>.</summary>
    /// <param name="options">The command line.</param>
    /// <param name="identity">The session and user of this instance.</param>
    /// <param name="mutex">The single-instance mutex, owned.</param>
    /// <param name="logs">The product log.</param>
    /// <param name="firstRunAfterInstall">
    /// The installer started this process right after installing (Velopack's first-run hook): with data from before,
    /// the welcome asks whether to keep it or start from scratch (NFR-010, P6).
    /// </param>
    public AppHost(
        AppOptions options,
        InstanceIdentity identity,
        InstanceMutex mutex,
        ILoggerFactory logs,
        bool firstRunAfterInstall = false
    )
    {
        _options = options;
        _identity = identity;
        _mutex = mutex;
        _logs = logs;
        _logger = logs.CreateLogger<AppHost>();
        // A start Sentinel relaunched, or one with isolated data, is never the first run of an installation.
        _firstRunAfterInstall =
            firstRunAfterInstall && options.AfterCrash is null && !options.IsolatedData;
    }

    /// <summary>Whether the first frame of the panel is on screen.</summary>
    public bool HasFirstFrame => _firstFrame;

    /// <summary>Runs the WPF application on the calling STA thread until <see cref="ExitAsync"/> ends it.</summary>
    /// <returns>The exit code of the process.</returns>
    public int Run()
    {
        _application = new System.Windows.Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };
        _application.Startup += OnStartup;
        _application.SessionEnding += (_, _) => Shutdown.SessionEnd.Flush(this);
        _application.DispatcherUnhandledException += OnUnhandledException;
        return _application.Run();
    }

    /// <inheritdoc />
    public Task ExitAsync()
    {
        _application?.Dispatcher.VerifyAccess();
        return _exit ??= ExitCoreAsync(AppExitCode.Ok);
    }

    /// <summary>
    /// <see cref="ExitAsync"/> with one last action once every key is released and the document flushed, right before
    /// the process leaves: «Desinstalar Clícalo» starts the uninstaller there (NFR-010, ADR-0029). Ignored when the
    /// exit had already begun.
    /// </summary>
    /// <param name="last">The action; a failure in it never keeps the process alive.</param>
    internal Task ExitThenAsync(Action last)
    {
        ArgumentNullException.ThrowIfNull(last);
        _application?.Dispatcher.VerifyAccess();
        if (_exit is null)
        {
            _beforeEnd = last;
        }

        return ExitAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Dispose();
        _services?.Dispose();
    }

    /// <summary>
    /// The exit sequence of the end of the session (WM_QUERYENDSESSION), without the teardown: Windows ends the process
    /// right after. Runs on the UI thread; nothing it awaits needs that thread.
    /// </summary>
    internal Task EndSessionAsync()
    {
        // Not the start of the exit: another app may still cancel the end of the session.
        return _exit is not null
            ? Task.CompletedTask
            : RunExitSequenceAsync(TerminalReason.SessionEnd);
    }

    // A WPF event handler (the only kind of async void CLC0009 accepts): every exception is caught and ends the start.
    private async void OnStartup(object? sender, StartupEventArgs e)
    {
        try
        {
            await StartAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // By type only (LOG-001): the message of an I/O failure carries the data path, with the user name.
            var failure = ex.GetType().Name;
            LogStartupFailed(_logger, failure);
            _exit ??= ExitCoreAsync(AppExitCode.StartupFailed);
            await _exit;
        }
    }

    private async Task StartAsync()
    {
        var ui = Dispatcher.CurrentDispatcher;
        _services = AppServices.Build(_options, _identity, ui, _logs);
        var services = _services;
        var slot = services.GetRequiredService<StartupSlot>();
        var started = _time.GetTimestamp();

        if (_options.SafeMode)
        {
            LogSafeMode(_logger);
        }

        // 0 and 1. Off the UI thread (§3.2): a crash Sentinel reported goes to its journal (ADR-0018), then the document
        // and the language it asks for.
        var read = await services
            .GetRequiredService<StartupReader>()
            .ReadAsync(
                new StartupRequest(
                    AppContext.BaseDirectory,
                    LanguageFiles.WindowsLanguage(),
                    _options.AfterCrash
                ),
                _stop.Token
            )
            .ConfigureAwait(true);
        slot.Load = read.Documents.Load;
        _askReinstall = WelcomeFreshStart.ShouldAsk(
            _firstRunAfterInstall,
            newData: read.Documents.Load.Outcome == DocumentLoadOutcome.FirstRun,
            read.Documents.Load.Document
        );
        slot.Localization = read.Localization;
        slot.Catalogs = read.Catalogs;
        var loadTime = _time.GetElapsedTime(started);
        LogDocumentLoaded(_logger, slot.Load.Outcome, loadTime);

        // 2. Nothing may be left down by a previous process that died before its guardian (SEG-006).
        var adapters = services.GetRequiredService<EngineAdapterSet>();
        Track(adapters.Resources.Dispose);
        var released = adapters.PressedRelease.ReleasePressed();
        if (released > 0)
        {
            LogPreventiveRelease(_logger, released);
        }

        // 3. Autosave, and the engine on its own thread.
        var store = services.GetRequiredService<DocumentStore>();
        _scheduler = services.GetRequiredService<PersistenceScheduler>();
        var repository = services.GetRequiredService<DocumentRepository>();
        var awaitingAcceptance = repository.IsAwaitingAcceptance;
        if (awaitingAcceptance)
        {
            // DAT-003: the default document shown when nothing could be read is only written once the person uses
            // it. Subscribed before the autosave, so the change that accepts it is the first one saved.
            store.Changed += (_, change) =>
            {
                if (repository.IsAwaitingAcceptance && StartupRecovery.Accepts(change))
                {
                    repository.AcceptDefaultDocument();
                    LogDefaultAccepted(_logger);
                    _ = ui.BeginInvoke(() =>
                        services.GetRequiredService<PanelComposer>().ClearSticky(_recoveryNotice)
                    );
                }
            };
        }

        store.Changed += _scheduler.OnDocumentChanged;
        var scheduler = _scheduler;
        if (read.Documents.SavePending)
        {
            // The seed of a new installation could not be written: saved at once and retried (DAT-002).
            scheduler.MarkUnsaved(store.Current);
        }

        scheduler.StatusChanged += (_, _) =>
        {
            // DAT-002: a save that keeps failing is said once, when it becomes visible; the retries stay silent.
            if (scheduler.Status == SaveStatus.Failing)
            {
                _ = ui.BeginInvoke(() =>
                    services
                        .GetRequiredService<PanelComposer>()
                        .Notify(L.SaveFailT, NoticeTone.Warning, "warning")
                );
            }
        };
        _persistence = Task.Run(() => scheduler.RunAsync(_stop.Token));
        _engine = services.GetRequiredService<EngineThread>();
        _engine.Start(services.GetRequiredService<EngineHost>(), _stop.Token);
        var unstable = new GuardianUnstableNotice(
            adapters.Guardian,
            services.GetRequiredService<EngineObserverRelay>(),
            _logger
        );
        Track(unstable.Dispose);

        // 4. SysEvents: the external foreground reaches the engine before the first touch can.
        var sysEvents = services.GetRequiredService<SysEventsThread>();
        Track(sysEvents.Dispose);
        var monitor = services.GetRequiredService<ForegroundMonitor>();
        Track(monitor.Dispose);
        await monitor.StartAsync().ConfigureAwait(true);
        var foreground = services.GetRequiredService<ForegroundChangeCoordinator>();
        Track(foreground.Dispose);
        foreground.Start();
        var rights = services.GetRequiredService<InternalRightsHotkey>();
        Track(rights.Dispose);
        var registered = rights.RegisterAsync();
        var relay = services.GetRequiredService<EngineObserverRelay>();
        var session = await SessionKeyRelease
            .StartAsync(
                sysEvents,
                services.GetRequiredService<IEngineInbox>(),
                () => BeforeSuspend(relay, scheduler),
                _time
            )
            .ConfigureAwait(true);
        Track(session.Dispose);

        // 5. The panel, the orchestrator that answers its ActivationGuard (REG-01), and Sentinel in parallel.
        // The owner goes last: destroying it first would destroy its surfaces behind their backs.
        Track(services.GetRequiredService<OwnerAnchor>().Dispose);
        var window = BuildPanel(services, store, slot.Localization, slot.Catalogs);
        Track(window.Close);
        var orchestrator = services.GetRequiredService<ForegroundOrchestrator>();
        Track(orchestrator.Dispose);
        if (!_options.GuardianAfterFirstFrame)
        {
            _guardian = StartGuardian(adapters.Guardian);
        }

        window.ContentRendered += (_, _) => OnFirstFrame(adapters.Guardian);
        window.Present();
        if (StartupRecovery.NoticeFor(slot.Load.Outcome, awaitingAcceptance) is { } recovery)
        {
            // SIS-004, DAT-003: said in the panel and kept there until another notice takes its place.
            services
                .GetRequiredService<PanelComposer>()
                .ShowSticky(
                    _recoveryNotice,
                    new PanelNotice(
                        recovery,
                        new Domain.Catalog.IconRef("warning"),
                        NoticeTone.Warning
                    )
                );
        }

        _controlCenter = BuildControlCenter(services, store, slot, ui, foreground, monitor, window);
        Track(_controlCenter.Dispose);
        var system = SystemLifecycle.Start(services, this, _options, ui, _time, ExitThenAsync);
        Track(system.Dispose);
        _controlCenter.System = system.Services;
        _controlCenter.About = (dictate, notify) =>
            AboutServicesFactory.Create(
                slot.Localization,
                services.GetRequiredService<DataLocations>(),
                _options.SendInput
                    ? services.GetRequiredService<ShellExecutor>()
                    : new DeferredShellExecutor(),
                services.GetRequiredService<IAtomicFileWriter>(),
                ui,
                dictate,
                notify,
                // ACE-004, ADR-0029: the email app opens only towards the contact address of the project.
                _options.SendInput
                    ? (address, cancellationToken) =>
                        services
                            .GetRequiredService<ShellExecutor>()
                            .OpenMailAsync(
                                address,
                                Presentation.ControlCenter.About.AboutLinks.Current.Email,
                                cancellationToken
                            )
                    : null
            );
        _store = store;
        _welcome = BuildWelcome(services, store, slot, ui, _time);
        Track(_welcome.Dispose);
        _controlCenter.OpenWelcome = () => _ = OpenWelcomeAgainAsync();
        if (_firstFrame)
        {
            OpenWelcomeIfPending();
        }

        // 6. The rest once the panel is up.
        _ = await registered.ConfigureAwait(true);
        var integrity = services.GetRequiredService<SurfaceIntegrityCheck>();
        Track(integrity.Dispose);
        integrity.Start();
        await StartTrayAsync(services).ConfigureAwait(true);
        StartPipe(services, ui);
    }

    /// <summary>
    /// <c>PBT_APMSUSPEND</c>, answered synchronously on the SysEvents thread (blueprint §6.4, §7.6): first the release
    /// the engine confirms (<c>Timings.KeySafety.SuspendReleaseWait</c>), then the flush of the document, the usage and
    /// the queued backups (<c>Timings.App.SuspendFlushTimeout</c>), since the machine may sleep as soon as the message
    /// returns and the battery may run out while it sleeps.
    /// </summary>
    private void BeforeSuspend(EngineObserverRelay relay, PersistenceScheduler scheduler)
    {
        if (!Shutdown.SuspendFlush.Run(relay, scheduler, _time))
        {
            LogSuspendFlushLate(_logger);
        }
    }

    private PanelWindow BuildPanel(
        IServiceProvider services,
        DocumentStore store,
        LocalizationContext localization,
        RuntimeCatalogs catalogs
    )
    {
        var session = services.GetRequiredService<SessionStore>();
        var composer = services.GetRequiredService<PanelComposer>();
        var viewModel = composer.Panel;
        var relay = services.GetRequiredService<EngineObserverRelay>();
        viewModel.ApplySession(session.Current);
        session.Changed += (_, change) =>
        {
            viewModel.ApplySession(change.Current);
            UpdateTray(viewModel);
        };
        var engine = services.GetRequiredService<IEngineInbox>();
        var theme = services.GetRequiredService<ThemeService>();
        var window = services.GetRequiredService<PanelWindow>();
        store.Changed += (_, change) =>
        {
            if (!ReferenceEquals(change.Before.Settings, change.After.Settings))
            {
                // The engine obeys the settings it was built with until told otherwise (SEG-004, SEG-005, TAC-002).
                _ = engine.Post(
                    new EngineEvent.ConfigChanged(
                        SettingsProjection.Engine(
                            change.After.Settings,
                            catalogs.CommonActions,
                            catalogs.KeyLabels
                        )
                    )
                );
            }

            _ = _application!.Dispatcher.BeginInvoke(() =>
            {
                var settings = store.Current.Settings;
                viewModel.ApplyTouch(SettingsProjection.Touch(settings));

                // AJR-004: the theme, the text scale, reduce motion and the opacity apply at once, in place.
                SettingsProjection.Theme(theme, settings);
                window.ApplyDimSettings(SettingsProjection.Dim(settings));
                composer.OnDocumentChanged(change);
            });
        };
        // IDI-001: the lang setting is the single source; every window repaints its texts in place on its own thread.
        var language = new LanguageFollower(localization, store);
        Track(language.Dispose);
        void OnUiThread(Action work) => _ = _application!.Dispatcher.BeginInvoke(work);
        _ = language.Register(composer.Relocalize, OnUiThread);
        _ = language.Register(() => _ = _tray?.RelocalizeAsync(), OnUiThread);
        relay.SnapshotChanged += (_, change) =>
        {
            viewModel.ApplyEngine(change.Snapshot);
            UpdateTray(viewModel);
        };
        // Frequents count what ran (FRE-002); the store publishes usage changes without an undo step.
        relay.UsageCounted += (_, counted) =>
        {
            _ = store.Dispatch(new RecordUsage(counted.Shortcut));
            composer.FlashTile(counted.Shortcut);
            composer.OnActionRan();
        };
        relay.NoticeRaised += (_, notice) => composer.OnEngineNotice(notice);

        // PER-003, EJE-013, SEL-003: the panel follows the app in front.
        composer.Follow(
            services.GetRequiredService<IForegroundMonitor>(),
            services.GetRequiredService<ForegroundDescriber>().Describe
        );
        return window;
    }

    /// <summary>
    /// The Control Center (docs/05): opened from the tray and from the panel, it follows the external foreground for
    /// «Probar en» and the capture mode, and never covers the panel (CCM-004).
    /// </summary>
    private ControlCenterComposer BuildControlCenter(
        IServiceProvider services,
        DocumentStore store,
        StartupSlot slot,
        Dispatcher ui,
        ForegroundChangeCoordinator foreground,
        ForegroundMonitor monitor,
        PanelWindow window
    )
    {
        var controlCenter = new ControlCenterComposer(
            store,
            slot.Localization,
            services.GetRequiredService<IForegroundOrchestrator>(),
            services.GetRequiredService<IEngineInbox>(),
            () => foreground.CurrentEpoch,
            services.GetRequiredService<Clicalo.Application.Profiles.ProfileViewCoordinator>(),
            services.GetRequiredService<ThemeService>(),
            ui,
            _time,
            slot.Catalogs,
            services.GetRequiredService<ITouchKeyboard>(),
            new ControlCenterOpenApps(),
            () => new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight),
            Environment.IsPrivilegedProcess,
            new TemplatesComposition(
                store,
                slot.Localization,
                _time,
                services.GetRequiredService<IIdGenerator>(),
                services.GetRequiredService<IAtomicFileWriter>(),
                _options.IsolatedData
            )
        );
        var describer = services.GetRequiredService<ForegroundDescriber>();
        monitor.ExternalForegroundChanged += (_, change) =>
            controlCenter.OnExternalForeground(describer.Describe(change.Foreground).Process);
        // CCM-003, CCM-004, ATJ-008, PRB-004: the notices, the dimming and the capture are shared with the panel.
        PanelLinks.Connect(
            services.GetRequiredService<PanelComposer>(),
            controlCenter,
            services.GetRequiredService<InteractionStore>()
        );
        // PRB-006: the switches of «Probar ahora» never release what is held (SEG-005).
        foreground.IsTrying = () => controlCenter.IsTrying;
        return controlCenter;
    }

    /// <summary>
    /// The welcome (docs/06): it opens after the first frame while the document has not finished it (BIE-001), and again
    /// from General › Ver la bienvenida otra vez (GEN-014). Nothing dims while it is open; when it ends the panel shows
    /// unminimized, with the notice [welcome] after [Empezar] (BIE-009).
    /// </summary>
    private static WelcomeComposer BuildWelcome(
        IServiceProvider services,
        DocumentStore store,
        StartupSlot slot,
        Dispatcher ui,
        TimeProvider time
    )
    {
        var welcome = new WelcomeComposer(
            store,
            slot.Localization,
            services.GetRequiredService<IForegroundOrchestrator>(),
            services.GetRequiredService<ThemeService>(),
            ui,
            () => slot.Catalogs.Content,
            // BIE-006: the keyboard Windows reports, as Plantillas shows it (PLA-009).
            () =>
                Domain.Templates.KeyboardLayouts.Detect(
                    System.Windows.Input.InputLanguageManager.Current?.CurrentInputLanguage?.Name
                ),
            WelcomeFreshStart.Of(
                () => slot.Catalogs.Content,
                services.GetRequiredService<IIdGenerator>(),
                time
            ),
            time
        );
        var interaction = services.GetRequiredService<InteractionStore>();
        var visibility = services.GetRequiredService<PanelVisibilityCoordinator>();
        var panel = services.GetRequiredService<PanelComposer>();
        welcome.StateChanged += (_, _) =>
            _ = interaction.Dispatch(
                new InteractionAction.SetOpen(DimExceptions.WelcomeOpen, welcome.IsOpen)
            );
        welcome.Ended += (_, e) =>
        {
            if (interaction.Current.Minimized)
            {
                _ = interaction.Dispatch(new InteractionAction.Restore());
            }

            visibility.Show();
            if (e.End == WelcomeEnd.Finished)
            {
                panel.Notify(L.Welcome, NoticeTone.Notice, "celebration");
            }
        };
        return welcome;
    }

    private void OpenWelcomeIfPending()
    {
        if (_welcome is not { } welcome || _store is not { } store)
        {
            return;
        }

        if (WelcomeComposer.IsPending(store.Current))
        {
            _ = welcome.OpenAsync(repeat: false, LeaseOrigin.Internal);
        }
        else if (_askReinstall)
        {
            // NFR-010, P6: a reinstallation that found data from before asks once; keeping it is the default.
            _askReinstall = false;
            _ = welcome.OpenAsync(repeat: true, LeaseOrigin.Internal, reinstall: true);
        }
    }

    /// <summary>«Ver la bienvenida otra vez» (GEN-014): the Control Center closes, then the welcome opens on step 0.</summary>
    private async Task OpenWelcomeAgainAsync()
    {
        if (_controlCenter is { } controlCenter)
        {
            await controlCenter.CloseAsync().ConfigureAwait(true);
        }

        if (_welcome is { } welcome)
        {
            await welcome.OpenAsync(repeat: true, LeaseOrigin.Touch).ConfigureAwait(true);
        }
    }

    private void UpdateTray(PanelViewModel viewModel) =>
        _ = _tray?.UpdateStateAsync(viewModel.IsVisible, !viewModel.Engine.Held.IsEmpty);

    private void OnFirstFrame(IGuardian guardian)
    {
        if (_firstFrame)
        {
            return;
        }

        _firstFrame = true;
        FirstFrameSignal.Raise();
        OpenWelcomeIfPending();
        using (var process = Process.GetCurrentProcess())
        {
            var sinceStart = _time.GetUtcNow() - new DateTimeOffset(process.StartTime);
            LogFirstFrame(_logger, sinceStart);
        }

        if (_options.GuardianAfterFirstFrame)
        {
            _guardian = StartGuardian(guardian);
        }

        if (_options.SendInput)
        {
            // The accepted gap without a guardian (§3.1, §15.2): logged when it lasts longer than planned.
            _ = CheckGuardianLateAsync(guardian);
        }

        if (_options.ExitAfter is { } delay)
        {
            _ = ExitAfterAsync(delay);
        }
    }

    /// <summary><c>--exit-after</c>: the whole exit sequence, unattended, once the start is complete.</summary>
    private async Task ExitAfterAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _time, _stop.Token).ConfigureAwait(true);
            await ExitAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Already exiting.
        }
    }

    private Task StartGuardian(IGuardian guardian) =>
        Task.Run(async () =>
        {
            try
            {
                await guardian.StartAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                var failure = ex.GetType().Name;
                LogGuardianFailed(_logger, failure);
            }
        });

    private async Task CheckGuardianLateAsync(IGuardian guardian)
    {
        try
        {
            await Task.Delay(Timings.App.SentinelLateThreshold, _time, _stop.Token)
                .ConfigureAwait(true);
            if (!guardian.IsRunning)
            {
                LogSentinelLate(_logger);
            }
        }
        catch (OperationCanceledException)
        {
            // Exiting.
        }
    }

    private async Task StartTrayAsync(IServiceProvider services)
    {
        var tray = services.GetRequiredService<TrayController>();
        var visibility = services.GetRequiredService<PanelVisibilityCoordinator>();
        var ui = _application!.Dispatcher;
        var interaction = services.GetRequiredService<InteractionStore>();
        // BUR-003, BUR-005: a click on the tray and the global shortcut show or hide the panel.
        tray.ShowHideRequested += (_, _) =>
            _ = ui.BeginInvoke(() => PanelLinks.ToggleFromTray(visibility, interaction));
        tray.ExitRequested += (_, _) => _ = ui.BeginInvoke(() => _ = ExitAsync());
        tray.ControlCenterRequested += (_, _) =>
            _ = ui.BeginInvoke(() => _ = _controlCenter?.OpenAsync(LeaseOrigin.Tray));
        // «Soltar todo» works with a hung engine too: what Windows reports down goes up without the engine (ADR-0023).
        var release = services.GetRequiredService<EngineAdapterSet>().PressedRelease;
        tray.ReleasePressedRequested += (_, _) => _ = Task.Run(release.ReleasePressed);
        Track(tray.Dispose);
        await tray.StartAsync().ConfigureAwait(true);
        _tray = tray;
        // BUR-005: the optional global shortcut follows the settings from here on (off by default, D10).
        services.GetRequiredService<PanelComposer>().AttachHotkey(tray.SetHotkeyAsync);
        UpdateTray(services.GetRequiredService<PanelViewModel>());
    }

    private void StartPipe(IServiceProvider services, Dispatcher ui)
    {
        var pipe = services.GetRequiredService<ShowPipeServer>();
        var visibility = services.GetRequiredService<PanelVisibilityCoordinator>();
        pipe.ShowRequested += (_, _) => _ = ui.BeginInvoke(visibility.Show);
        pipe.Start();
        _pipe = pipe;
    }

    private void Track(Action dispose) => _teardown.Add(dispose);

    private async Task ExitCoreAsync(AppExitCode code)
    {
        _ = await RunExitSequenceAsync(TerminalReason.Exit).ConfigureAwait(true);
        await _stop.CancelAsync().ConfigureAwait(true);
        if (_pipe is not null)
        {
            await _pipe.DisposeAsync().ConfigureAwait(true);
        }

        for (var i = _teardown.Count - 1; i >= 0; i--)
        {
            DisposeQuietly(_teardown[i]);
        }

        await WaitQuietlyAsync(Task.WhenAll(_persistence, _guardian)).ConfigureAwait(true);
        _mutex.Dispose();
        if (_beforeEnd is { } last)
        {
            DisposeQuietly(last);
        }

        EndApplication(code);
    }

    /// <summary>The exit sequence, when the engine was started; otherwise there is nothing to release.</summary>
    private Task<ExitReport> RunExitSequenceAsync(TerminalReason reason)
    {
        if (_services is null || _engine is null)
        {
            return Task.FromResult(new ExitReport(false, false, TimeSpan.Zero));
        }

        var engine = Volatile.Read(ref _engine)!;
        var scheduler = _scheduler;
        var relay = _services.GetRequiredService<EngineObserverRelay>();

        // The engine loop ends only with Terminal(Exit); after Terminal(SessionEnd) it goes on (another app may cancel
        // the end of the session), so the release is confirmed by its first snapshot with nothing held.
        Func<CancellationToken, Task> released =
            reason == TerminalReason.Exit ? _ => engine.Stopped : relay.WhenNothingHeldAsync;
        return _services
            .GetRequiredService<ExitSequence>()
            .RunAsync(
                reason,
                released,
                token => FlushAsync(scheduler, token),
                CancellationToken.None
            );
    }

    /// <summary>
    /// The copies queued before a destructive change, then the document and the usage, all by the Persistence consumer
    /// (§6.4, §6.5, DAT-002, DAT-006).
    /// </summary>
    private static Task FlushAsync(
        PersistenceScheduler? scheduler,
        CancellationToken cancellationToken
    ) => scheduler?.FlushAsync(cancellationToken) ?? Task.CompletedTask;

    private async Task WaitQuietlyAsync(Task pending)
    {
        try
        {
            await pending
                .WaitAsync(Timings.App.ExitFlushTimeout, _time, CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Ending: a background loop that does not stop in time never keeps the process alive.
            var failure = ex.GetType().Name;
            LogTeardownFailed(_logger, failure);
        }
    }

    private void DisposeQuietly(Action dispose)
    {
        try
        {
            dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Ending: one piece that cannot dispose itself never keeps the process alive.
            var failure = ex.GetType().Name;
            LogTeardownFailed(_logger, failure);
        }
    }

    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "IAppLifetime.ExitAsync ends the process here, after the exit sequence released every key."
    )]
    private void EndApplication(AppExitCode code) => _application?.Shutdown((int)code);

    private void OnUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs unhandled
    )
    {
        // A defect on the UI thread: log it by type and keep the panel alive; the engine and Sentinel are unaffected.
        var failure = unhandled.Exception.GetType().Name;
        LogUiException(_logger, failure);
        unhandled.Handled = true;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "startup.document {Outcome} in {Elapsed}"
    )]
    private static partial void LogDocumentLoaded(
        ILogger logger,
        DocumentLoadOutcome outcome,
        TimeSpan elapsed
    );

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "startup.preventive_release {Count} modifiers"
    )]
    private static partial void LogPreventiveRelease(ILogger logger, int count);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "startup.first_frame.ms {Elapsed}"
    )]
    private static partial void LogFirstFrame(ILogger logger, TimeSpan elapsed);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "startup.sentinel_late")]
    private static partial void LogSentinelLate(ILogger logger);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "startup.guardian_failed ({Exception})"
    )]
    private static partial void LogGuardianFailed(ILogger logger, string exception);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Critical,
        Message = "startup.failed ({Exception})"
    )]
    private static partial void LogStartupFailed(ILogger logger, string exception);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "app.teardown_failed ({Exception})"
    )]
    private static partial void LogTeardownFailed(ILogger logger, string exception);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "ui.unhandled ({Exception})")]
    private static partial void LogUiException(ILogger logger, string exception);

    [LoggerMessage(EventId = 9, Level = LogLevel.Warning, Message = "startup.safe_mode")]
    private static partial void LogSafeMode(ILogger logger);

    [LoggerMessage(
        EventId = 15,
        Level = LogLevel.Warning,
        Message = "suspend.flush_late: the flush did not finish before the computer suspended"
    )]
    private static partial void LogSuspendFlushLate(ILogger logger);

    [LoggerMessage(
        EventId = 16,
        Level = LogLevel.Information,
        Message = "doc.default_accepted: the default document is saved from now on"
    )]
    private static partial void LogDefaultAccepted(ILogger logger);
}
