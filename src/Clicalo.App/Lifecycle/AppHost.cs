using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Clicalo.App.Composition;
using Clicalo.App.Localization;
using Clicalo.App.SingleInstance;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Application.Foreground;
using Clicalo.Application.Localization;
using Clicalo.Application.Persistence;
using Clicalo.Application.Ports;
using Clicalo.Application.Session;
using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Backup;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Windows.Foreground;
using Clicalo.Platform.Windows.Input;
using Clicalo.Platform.Windows.SysEvents;
using Clicalo.Platform.Windows.Tray;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// The life of the running instance (blueprint §3.1, §3.2, §7.6), on the UI thread of the Surfaces role:
/// <list type="number">
/// <item>a crash Sentinel reported goes to its journal; the document is read (persistence; on a new installation the
/// seed, or the v1 file of <c>--migrate-v1</c>) and the language files loaded;</item>
/// <item>the preventive release of the start (SEG-006), before the engine accepts anything;</item>
/// <item>autosave, and the engine thread: it accepts touches from the first frame, it does not wait for the guardian;
/// with key sending, the emergency release watches its heartbeat (§3.2 rule 6);</item>
/// <item>SysEvents follows the external foreground and the engine hears it (§7.9);</item>
/// <item>the panel is built and shown passively; Sentinel is launched from a background thread in parallel to the
/// first frame (or right after it with <c>--guardian after-first-frame</c>, spike S5);</item>
/// <item>then the surface checks, the tray and the single-instance pipe.</item>
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
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _exiting = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly TimeProvider _time = TimeProvider.System;
    private readonly List<Action> _teardown = [];
    private System.Windows.Application? _application;
    private ServiceProvider? _services;
    private EngineThread? _engine;
    private PanelWindow? _window;
    private PersistenceScheduler? _scheduler;
    private TrayController? _tray;
    private ShowPipeServer? _pipe;
    private Task _persistence = Task.CompletedTask;
    private Task _guardian = Task.CompletedTask;
    private Task? _exit;
    private bool _firstFrame;

    /// <summary>Creates the host of the instance that owns <paramref name="mutex"/>.</summary>
    public AppHost(
        AppOptions options,
        InstanceIdentity identity,
        InstanceMutex mutex,
        ILoggerFactory logs
    )
    {
        _options = options;
        _identity = identity;
        _mutex = mutex;
        _logs = logs;
        _logger = logs.CreateLogger<AppHost>();
    }

    /// <inheritdoc />
    public Task Exiting => _exiting.Task;

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
        if (_exit is not null)
        {
            return Task.CompletedTask;
        }

        _exiting.TrySetResult();
        return RunExitSequenceAsync(TerminalReason.SessionEnd);
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

        // 0. A crash Sentinel reported goes to the journal it reads for the crash loop (ADR-0018).
        if (_options.AfterCrash is { } crash)
        {
            await RecordCrashAsync(services, crash).ConfigureAwait(true);
        }

        if (_options.SafeMode)
        {
            LogSafeMode(_logger);
        }

        // 1. The document and the language it asks for.
        var i18n =
            LanguageFiles.Find(AppContext.BaseDirectory)
            ?? throw new FileNotFoundException("i18n was not found next to Clicalo.exe.");
        var windowsLanguage = LanguageFiles.WindowsLanguage();
        slot.Load = await services
            .GetRequiredService<StartupDocuments>()
            .LoadAsync(
                ContentFiles.Find(AppContext.BaseDirectory),
                LanguageFiles.Has(i18n, windowsLanguage) ? new LangCode(windowsLanguage) : null,
                _options.MigrateV1,
                _stop.Token
            )
            .ConfigureAwait(true);
        slot.Localization = LanguageFiles.Load(i18n, slot.Load.Document.Settings.Language.Value);
        var loadTime = _time.GetElapsedTime(started);
        LogDocumentLoaded(_logger, slot.Load.Outcome, loadTime);

        // 2. Nothing may be left down by a previous process that died before its guardian (SEG-006).
        var adapters = services.GetRequiredService<EngineAdapterSet>();
        Track(adapters.Resources.Dispose);
        var released = adapters.StartupRelease.ReleaseStuckModifiers();
        if (released > 0)
        {
            LogPreventiveRelease(_logger, released);
        }

        // 3. Autosave, and the engine on its own thread.
        var store = services.GetRequiredService<DocumentStore>();
        _scheduler = services.GetRequiredService<PersistenceScheduler>();
        store.Changed += _scheduler.OnDocumentChanged;
        var scheduler = _scheduler;
        _persistence = Task.Run(() => scheduler.RunAsync(_stop.Token));
        _engine = services.GetRequiredService<EngineThread>();
        _engine.Start(services.GetRequiredService<EngineHost>(), _stop.Token);
        if (adapters.Gate is { } gate)
        {
            // A hung engine is fenced and replaced, or the process ends so Sentinel releases (REG-03).
            var emergency = new EmergencyReleaser(
                gate,
                _time,
                RestartEngine,
                EmergencyReleaser.TerminateSelf
            );
            Track(emergency.Dispose);
            emergency.Start();
        }

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
                () => Shutdown.SuspendRelease.Wait(relay)
            )
            .ConfigureAwait(true);
        Track(session.Dispose);

        // 5. The panel, the orchestrator that answers its ActivationGuard (REG-01), and Sentinel in parallel.
        // The owner goes last: destroying it first would destroy its surfaces behind their backs.
        Track(services.GetRequiredService<OwnerAnchor>().Dispose);
        var window = BuildPanel(services, store, slot.Localization);
        _window = window;
        Track(window.Close);
        var orchestrator = services.GetRequiredService<ForegroundOrchestrator>();
        Track(orchestrator.Dispose);
        if (!_options.GuardianAfterFirstFrame)
        {
            _guardian = StartGuardian(adapters.Guardian);
        }

        window.ContentRendered += (_, _) => OnFirstFrame(adapters.Guardian);
        window.Present();

        // 6. The rest once the panel is up.
        _ = await registered.ConfigureAwait(true);
        var integrity = services.GetRequiredService<SurfaceIntegrityCheck>();
        Track(integrity.Dispose);
        integrity.Start();
        await StartTrayAsync(services).ConfigureAwait(true);
        StartPipe(services, ui);
    }

    private PanelWindow BuildPanel(
        IServiceProvider services,
        DocumentStore store,
        LocalizationContext localization
    )
    {
        var session = services.GetRequiredService<SessionStore>();
        var viewModel = services.GetRequiredService<PanelViewModel>();
        var relay = services.GetRequiredService<EngineObserverRelay>();
        Project(viewModel, store, session, localization);
        viewModel.ApplySession(session.Current);
        session.Changed += (_, change) =>
        {
            viewModel.ApplySession(change.Current);
            Project(viewModel, store, session, localization);
            UpdateTray(viewModel);
        };
        var engine = services.GetRequiredService<IEngineInbox>();
        store.Changed += (_, change) =>
        {
            if (!ReferenceEquals(change.Before.Settings, change.After.Settings))
            {
                // The engine obeys the settings it was built with until told otherwise (SEG-004, SEG-005, TAC-002).
                _ = engine.Post(
                    new EngineEvent.ConfigChanged(SettingsProjection.Engine(change.After.Settings))
                );
                if (change.Before.Settings.Language != change.After.Settings.Language)
                {
                    // IDI-001: the language switches in place; LanguageChanged repaints below.
                    _ = localization.TrySetLanguage(change.After.Settings.Language.Value);
                }
            }

            _ = _application!.Dispatcher.BeginInvoke(() =>
            {
                viewModel.ApplyTouch(SettingsProjection.Touch(store.Current.Settings));
                Project(viewModel, store, session, localization);
            });
        };
        localization.LanguageChanged += (_, _) =>
            _ = _application!.Dispatcher.BeginInvoke(() =>
            {
                viewModel.Relocalize();
                Project(viewModel, store, session, localization);
                _ = _tray?.RelocalizeAsync();
            });
        relay.SnapshotChanged += (_, change) =>
        {
            viewModel.ApplyEngine(change.Snapshot);
            UpdateTray(viewModel);
        };
        // Frequents count what ran (FRE-002); the store publishes usage changes without an undo step.
        relay.UsageCounted += (_, counted) => _ = store.Dispatch(new RecordUsage(counted.Shortcut));
        var window = services.GetRequiredService<PanelWindow>();
        relay.NoticeRaised += (_, notice) =>
            window.Announce(
                localization.Current.Format(notice.Text),
                notice.Urgency == NoticeUrgency.Polite
                    ? AnnouncementUrgency.Polite
                    : AnnouncementUrgency.Assertive
            );
        return window;
    }

    private static void Project(
        PanelViewModel viewModel,
        DocumentStore store,
        SessionStore session,
        LocalizationContext localization
    )
    {
        var document = store.Current;
        var profile = document.Library.TryGetProfile(session.Current.View, out var inView)
            ? inView
            : document.Library.General;
        viewModel.Apply(
            PanelProjector.Project(
                profile,
                new LangCode(localization.Current.Locale.Code),
                LangCode.Es
            )
        );
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

    /// <summary>
    /// The emergency's new engine (blueprint §3.2 rule 6), on the releaser's timer thread: everything recorded was
    /// released under the gate and the generation went up, so the hung host is fenced (INV-11). The new host gets the
    /// mailbox every piece holds and the current foreground, and the user hears that the keys were released.
    /// </summary>
    private void RestartEngine(EngineGeneration generation)
    {
        var services = _services;
        if (services is null || _exit is not null)
        {
            return;
        }

        var store = services.GetRequiredService<DocumentStore>();
        var host = new EngineHost(
            services.GetRequiredService<EngineHostPorts>(),
            generation,
            SettingsProjection.Engine(store.Current.Settings),
            _time,
            services.GetRequiredService<ILogger<EngineHost>>()
        );
        var thread = new EngineThread(services.GetRequiredService<ILogger<EngineThread>>());
        thread.Start(host, _stop.Token);
        services.GetRequiredService<EngineInboxRelay>().Target = host;
        Volatile.Write(ref _engine, thread);
        services.GetRequiredService<ForegroundChangeCoordinator>().Republish();
        LogEngineRestarted(_logger, generation.Value);
        var localization = services.GetRequiredService<ILocalizationContext>();
        _ = _application?.Dispatcher.BeginInvoke(() =>
            _window?.Announce(
                localization.Current.Format(L.ReleasedAll),
                AnnouncementUrgency.Assertive
            )
        );
    }

    /// <summary>Appends the crash of <c>--after-crash</c> to the journal Sentinel reads (ADR-0018).</summary>
    private async Task RecordCrashAsync(IServiceProvider services, DateTimeOffset crash)
    {
        var path = AppDataLocations.CrashJournal(services.GetRequiredService<DataLocations>());
        ImmutableArray<DateTimeOffset> previous = [];
        try
        {
            if (File.Exists(path))
            {
                previous = CrashJournal.Parse(
                    await File.ReadAllBytesAsync(path, _stop.Token).ConfigureAwait(true)
                );
            }
        }
        catch (IOException)
        {
            // An unreadable journal starts again: at worst one crash loop is detected later.
        }

        var written = await services
            .GetRequiredService<IAtomicFileWriter>()
            .WriteAsync(path, CrashJournal.Append(previous, crash), _stop.Token)
            .ConfigureAwait(true);
        if (written.IsFailure)
        {
            LogCrashJournalFailed(_logger, written.Failure.Code);
        }

        LogAfterCrash(_logger, previous.Length + 1);
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
        tray.ShowHideRequested += (_, _) => _ = ui.BeginInvoke(visibility.Toggle);
        tray.ExitRequested += (_, _) => _ = ui.BeginInvoke(() => _ = ExitAsync());
        Track(tray.Dispose);
        await tray.StartAsync().ConfigureAwait(true);
        _tray = tray;
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
        _exiting.TrySetResult();
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
        var backups = _services.GetRequiredService<BackupService>();
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
                token => FlushAsync(scheduler, backups, token),
                CancellationToken.None
            );
    }

    /// <summary>The document and the usage, then the snapshots queued for the backups (§6.5, DAT-002).</summary>
    private static async Task FlushAsync(
        PersistenceScheduler? scheduler,
        BackupService backups,
        CancellationToken cancellationToken
    )
    {
        if (scheduler is not null)
        {
            await scheduler.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        await backups.FlushSnapshotsAsync(cancellationToken).ConfigureAwait(false);
    }

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
        EventId = 10,
        Level = LogLevel.Warning,
        Message = "startup.after_crash {Count} crashes in the journal"
    )]
    private static partial void LogAfterCrash(ILogger logger, int count);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Error,
        Message = "startup.crash_journal_failed ({Code})"
    )]
    private static partial void LogCrashJournalFailed(ILogger logger, string code);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Critical,
        Message = "engine.restarted generation {Generation}"
    )]
    private static partial void LogEngineRestarted(ILogger logger, ulong generation);
}
