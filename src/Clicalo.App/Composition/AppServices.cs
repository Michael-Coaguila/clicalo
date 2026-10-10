using System.Windows.Threading;
using Clicalo.App.Lifecycle;
using Clicalo.App.SingleInstance;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Application.Foreground;
using Clicalo.Application.Interaction;
using Clicalo.Application.Localization;
using Clicalo.Application.Persistence;
using Clicalo.Application.Ports;
using Clicalo.Application.Profiles;
using Clicalo.Application.Session;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Document;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Backup;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Platform.Windows.Clipboard;
using Clicalo.Platform.Windows.Feedback;
using Clicalo.Platform.Windows.Foreground;
using Clicalo.Platform.Windows.Launch;
using Clicalo.Platform.Windows.PointerTracking;
using Clicalo.Platform.Windows.SysEvents;
using Clicalo.Platform.Windows.Tray;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Composition;

/// <summary>
/// The composition root of M2 (blueprint §4.1, §5): one <see cref="ServiceProvider"/> of
/// Microsoft.Extensions.DependencyInjection, without the Generic Host, where every registration is a lazy singleton
/// factory. Nothing is built until the lifecycle asks for it, in the order of the start (§3.1): persistence reads the
/// document off the UI thread, then the engine, SysEvents, the surfaces on the UI thread, and the tray and the pipe
/// once the panel has been presented (without waiting for its first frame). Pieces that depend on the document read it
/// from <see cref="StartupSlot"/>.
/// </summary>
internal static class AppServices
{
    /// <summary>Builds the container.</summary>
    /// <param name="options">The command line.</param>
    /// <param name="identity">The names of the single instance.</param>
    /// <param name="ui">The dispatcher of the UI thread (Surfaces role).</param>
    /// <param name="logs">The loggers.</param>
    public static ServiceProvider Build(
        AppOptions options,
        InstanceIdentity identity,
        Dispatcher ui,
        ILoggerFactory logs
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton(identity);
        services.AddSingleton(ui);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(logs);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton<StartupSlot>();
        services.AddSingleton<ILocalizationContext>(sp => sp.Slot().Localization);
        AddPersistence(services);
        AddEngine(services);
        AddForeground(services);
        AddSurfaces(services);
        AddTray(services);
        services.AddSingleton<ShowPipeServer>();
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true }
        );
    }

    /// <summary>The profile the panel shows first: the last one used, or General (PER-001).</summary>
    /// <param name="document">The document.</param>
    public static Profile ProfileInView(UserDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return
            document.Settings.LastProfile is { } last
            && document.Library.TryGetProfile(last, out var profile)
            ? profile
            : document.Library.General;
    }

    private static void AddPersistence(ServiceCollection services)
    {
        services.AddSingleton(sp => AppDataLocations.For(sp.Get<AppOptions>()));
        // One codec for the live document and its backups, so both keep unknown fields and texts of another machine.
        services.AddSingleton<DocumentCodec>();
        services.AddSingleton<IAtomicFileWriter>(sp => new AtomicFile(
            sp.Time(),
            sp.Log<AtomicFile>()
        ));
        services.AddSingleton(sp => new QuarantineStore(sp.Get<DataLocations>(), sp.Time()));
        services.AddSingleton(sp => new BackupService(
            sp.Get<DataLocations>(),
            sp.Get<IAtomicFileWriter>(),
            sp.Time(),
            sp.Log<BackupService>(),
            sp.Get<DocumentCodec>()
        ));
        services.AddSingleton<IBackupService>(sp => sp.Get<BackupService>());
        services.AddSingleton(sp => new DocumentRepository(
            sp.Get<DataLocations>(),
            sp.Get<IAtomicFileWriter>(),
            sp.Get<QuarantineStore>(),
            sp.Get<IBackupService>(),
            sp.Time(),
            sp.Log<DocumentRepository>(),
            sp.Get<DocumentCodec>()
        ));
        services.AddSingleton<IDocumentRepository>(sp => sp.Get<DocumentRepository>());
        services.AddSingleton<IUsageRepository>(sp => new UsageRepository(
            sp.Get<DataLocations>(),
            sp.Get<IAtomicFileWriter>(),
            sp.Time(),
            sp.Log<UsageRepository>()
        ));
        services.AddSingleton(sp => new PersistenceScheduler(
            sp.Get<IDocumentRepository>(),
            sp.Get<IUsageRepository>(),
            sp.Get<IBackupService>(),
            sp.Time(),
            sp.Log<PersistenceScheduler>()
        ));
        services.AddSingleton<IIdGenerator, RandomIdGenerator>();
        services.AddSingleton(sp => new StartupDocuments(
            sp.Get<IDocumentRepository>(),
            sp.Get<IUsageRepository>(),
            sp.Get<IIdGenerator>(),
            sp.Time(),
            sp.Log<StartupDocuments>()
        ));
        services.AddSingleton(sp => new StartupReader(
            sp.Get<StartupDocuments>(),
            sp.Get<IAtomicFileWriter>(),
            sp.Get<DataLocations>(),
            sp.Log<StartupReader>()
        ));
        services.AddSingleton(sp => new DocumentStore(
            sp.Slot().Load.Document,
            sp.Get<IIdGenerator>(),
            sp.Get<IBackupService>(),
            sp.Time()
        ));
    }

    private static void AddEngine(ServiceCollection services)
    {
        services.AddSingleton(EngineAdapters.Create);
        services.AddSingleton(sp =>
        {
            var ui = sp.Get<Dispatcher>();
            return new EngineObserverRelay(work => _ = ui.BeginInvoke(work));
        });
        services.AddSingleton<EngineInboxRelay>();
        services.AddSingleton<InternalChordReplies>();
        // The Shell thread and the paste reach outside Clícalo; with --no-input nothing does (EJE-011, EJE-008).
        services.AddSingleton(_ => new ShellExecutor(Environment.IsPrivilegedProcess));
        services.AddSingleton(sp => new ClipboardPaster(sp.Get<SysEventsThread>(), sp.Time()));
        services.AddSingleton(sp => new PointerPositionTracker(sp.Get<SysEventsThread>()));
        services.AddSingleton(sp =>
        {
            var adapters = sp.Get<EngineAdapterSet>();
            var sends = sp.Get<AppOptions>().SendInput;
            return new EngineHostPorts(
                adapters.Injector,
                sends ? sp.Get<ShellExecutor>() : new DeferredShellExecutor(),
                sends ? sp.Get<ClipboardPaster>() : new DeferredClipboardPaster(),
                sp.Get<EngineObserverRelay>()
            )
            {
                // The internal chords go through the engine (§3.6, D-22).
                ChordReplies = sp.Get<InternalChordReplies>(),
                // EJE-009: the mouse actions act at the last pointer position outside Clícalo.
                PointerPosition = sp.Get<PointerPositionTracker>(),
                // EJE-012: the soft sound, when the settings turn it on.
                Sound = new FeedbackSound(),
            };
        });
        services.AddSingleton(sp =>
        {
            var host = new EngineHost(
                sp.Get<EngineHostPorts>(),
                SettingsProjection.Engine(
                    sp.Slot().Load.Document.Settings,
                    sp.Slot().Catalogs.CommonActions,
                    sp.Slot().Catalogs.KeyLabels
                ),
                sp.Time(),
                sp.Log<EngineHost>()
            );
            sp.Get<EngineInboxRelay>().Target = host;
            return host;
        });
        services.AddSingleton<IEngineInbox>(sp => sp.Get<EngineInboxRelay>());
        services.AddSingleton<EngineThread>();
        services.AddSingleton(sp => new ExitSequence(
            sp.Get<IEngineInbox>(),
            sp.Time(),
            sp.Log<ExitSequence>()
        ));
    }

    private static void AddForeground(ServiceCollection services)
    {
        services.AddSingleton(_ => SysEventsThread.Start());
        services.AddSingleton(sp => new ForegroundMonitor(sp.Get<SysEventsThread>(), sp.Time()));
        services.AddSingleton<IForegroundMonitor>(sp => sp.Get<ForegroundMonitor>());
        services.AddSingleton(sp => new InternalRightsHotkey(sp.Get<SysEventsThread>(), sp.Time()));
        services.AddSingleton<ForegroundControl>();
        services.AddSingleton(_ => new ForegroundDescriber(EngineAdapters.Layouts()));
        services.AddSingleton(sp => new ForegroundChangeCoordinator(
            sp.Get<IForegroundMonitor>(),
            sp.Get<IEngineInbox>(),
            sp.Get<ForegroundDescriber>().Describe,
            Environment.IsPrivilegedProcess
        ));
        services.AddSingleton<ActivationArbiterRelay>();
        services.AddSingleton(sp =>
        {
            var registry = sp.Get<SurfaceRegistry>();
            var orchestrator = new ForegroundOrchestrator(
                new ForegroundPorts
                {
                    Control = sp.Get<ForegroundControl>(),
                    Monitor = sp.Get<IForegroundMonitor>(),
                    SurfaceStyle = registry,
                    Surfaces = registry,
                    RightsHotkey = sp.Get<InternalRightsHotkey>(),
                    KeyEffects = sp.Get<EngineAdapterSet>().KeyEffects,
                },
                sp.Time(),
                sp.Log<ForegroundOrchestrator>()
            );
            sp.Get<ActivationArbiterRelay>().Target = orchestrator;
            return orchestrator;
        });
        services.AddSingleton<IForegroundOrchestrator>(sp => sp.Get<ForegroundOrchestrator>());
    }

    private static void AddSurfaces(ServiceCollection services)
    {
        // Created on the UI thread: the surfaces, their owner and their registry live on one thread (§3.2 rule 2).
        services.AddSingleton(sp => new ActivationGuard(
            sp.Get<ActivationArbiterRelay>(),
            sp.Time()
        ));
        services.AddSingleton<OwnerAnchor>();
        services.AddSingleton(sp => new SurfaceRegistry(
            sp.Get<OwnerAnchor>(),
            sp.Get<ActivationGuard>()
        ));
        services.AddSingleton(sp => new SurfaceIntegrityCheck(
            sp.Get<SurfaceRegistry>(),
            sp.Time()
        ));
        services.AddSingleton(sp => new SessionStore(
            PanelSession.Initial(ProfileInView(sp.Slot().Load.Document).Id)
        ));
        services.AddSingleton(sp => new InteractionStore(InteractionState.Initial, sp.Time()));
        services.AddSingleton(sp =>
        {
            var coordinator = sp.Get<ForegroundChangeCoordinator>();
            return new PanelInteractionController(
                sp.Get<IEngineInbox>(),
                () => coordinator.CurrentEpoch,
                sp.Time()
            );
        });
        services.AddSingleton(sp => new PanelVisibilityCoordinator(
            sp.Get<SessionStore>(),
            sp.Get<IEngineInbox>()
        ));
        services.AddSingleton(sp => new ProfileViewCoordinator(sp.Get<DocumentStore>()));
        services.AddSingleton<ITouchKeyboard>(sp => new TouchKeyboard(
            sp.Get<EngineAdapterSet>().KeyEffects
        ));
        services.AddSingleton(sp =>
        {
            var coordinator = sp.Get<ForegroundChangeCoordinator>();
            return new PanelSearch(
                sp.Get<IForegroundOrchestrator>(),
                sp.Get<IEngineInbox>(),
                () => coordinator.CurrentEpoch,
                sp.Time(),
                sp.Get<ITouchKeyboard>()
            );
        });
        // The full panel (docs/04): its view models and the wiring to the document, the session and the foreground.
        services.AddSingleton(sp => new PanelComposer(
            sp.Get<DocumentStore>(),
            sp.Get<SessionStore>(),
            sp.Get<InteractionStore>(),
            sp.Get<ProfileViewCoordinator>(),
            sp.Get<PanelInteractionController>(),
            sp.Get<EngineObserverRelay>(),
            sp.Get<IEngineInbox>(),
            sp.Get<ILocalizationContext>(),
            sp.Slot().Catalogs,
            sp.Get<PanelSearch>(),
            sp.Get<IIdGenerator>(),
            sp.Time(),
            sp.Get<Dispatcher>(),
            Environment.IsPrivilegedProcess
        ));
        services.AddSingleton(sp => sp.Get<PanelComposer>().Panel);
        // The bubble, the Tab view, the dimming and the positions of every surface (docs/04).
        services.AddSingleton(sp => new SurfacesComposer(
            sp.Get<DocumentStore>(),
            sp.Get<SessionStore>(),
            sp.Get<InteractionStore>(),
            sp.Get<ProfileViewCoordinator>(),
            sp.Get<PanelComposer>(),
            sp.Get<PanelInteractionController>(),
            sp.Get<EngineObserverRelay>(),
            sp.Get<ILocalizationContext>(),
            sp.Time(),
            sp.Get<Dispatcher>(),
            sp.Get<ITouchKeyboard>()
        ));
        // One theme service for the UI thread (blueprint §8.4): every surface of the thread attaches to it. The
        // container disposes both at the end, after the surfaces are closed.
        services.AddSingleton<WindowsSystemThemeSource>();
        services.AddSingleton(sp =>
        {
            var settings = sp.Get<DocumentStore>().Current.Settings;
            return new ThemeService(
                sp.Get<WindowsSystemThemeSource>(),
                settings.Theme,
                settings.TextScalePercent,
                settings.ReduceMotion
            );
        });
        services.AddSingleton(sp =>
        {
            var settings = sp.Get<DocumentStore>().Current.Settings;
            var composer = sp.Get<PanelComposer>();
            var window = new PanelWindow(
                composer.Panel,
                sp.Get<SurfaceRegistry>(),
                sp.Time(),
                sp.Get<ThemeService>(),
                SettingsProjection.Dim(settings),
                composer.Header,
                composer.Search,
                composer.Suggestion,
                composer.Layers
            );
            composer.AttachWindow(window);
            sp.Get<SurfacesComposer>()
                .Attach(window, sp.Get<SurfaceRegistry>(), sp.Get<ThemeService>());
            return window;
        });
    }

    private static void AddTray(ServiceCollection services)
    {
        services.AddSingleton(sp => new TrayIcon(sp.Get<SysEventsThread>()));
        services.AddSingleton(sp => new TrayMenuHost(sp.Get<SysEventsThread>()));
        services.AddSingleton(sp => new TrayController(
            sp.Get<TrayIcon>(),
            sp.Get<TrayMenuHost>(),
            sp.Get<IForegroundOrchestrator>(),
            sp.Get<IEngineInbox>(),
            sp.Get<ILocalizationContext>()
        ));
    }

    private static T Get<T>(this IServiceProvider services)
        where T : notnull => services.GetRequiredService<T>();

    private static TimeProvider Time(this IServiceProvider services) =>
        services.Get<TimeProvider>();

    private static StartupSlot Slot(this IServiceProvider services) => services.Get<StartupSlot>();

    private static ILogger<T> Log<T>(this IServiceProvider services) => services.Get<ILogger<T>>();
}
