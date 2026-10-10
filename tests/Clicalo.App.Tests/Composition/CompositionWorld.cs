using System.Collections.Immutable;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Clicalo.App.Composition;
using Clicalo.App.Localization;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Foreground;
using Clicalo.Application.Interaction;
using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Application.Profiles;
using Clicalo.Application.Session;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.TestKit;
using Clicalo.UI.Wpf.Theming;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.App.Tests.Composition;

/// <summary>
/// The composers of the panel and of the Control Center over the real stores, coordinators and view models, with
/// doubles at the edges (the engine mailbox, the foreground, the clock, the backups): the composition of the app
/// without its executable, its windows on screen or any input. Build and use it inside <see cref="UiThread.Run"/>.
/// </summary>
internal sealed class CompositionWorld
{
    public static readonly ProfileId Word = new("word");
    public static readonly ProfileId Excel = new("excel");
    public static readonly ProcessName WordApp = new("winword.exe");
    public static readonly ProcessName ExcelApp = new("excel.exe");

    private readonly RuntimeCatalogs _catalogs = RuntimeCatalogs.Load(RepoPaths.Data);
    private readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;

    public CompositionWorld()
    {
        Store = new DocumentStore(
            UserDocument.Create(Library(), SettingsSchema.Defaults),
            new RandomIdGenerator(),
            new NoBackups(),
            Time
        );
        Session = new SessionStore(PanelSession.Initial(ProfileId.General));
        Interaction = new InteractionStore(InteractionState.Initial, Time);
        Profiles = new ProfileViewCoordinator(Store);
        Localization = LanguageFiles.Load(Path.Combine(RepoPaths.Data, "i18n"), "es", out _);
        Panel = new PanelComposer(
            Store,
            Session,
            Interaction,
            Profiles,
            new PanelInteractionController(Engine, static () => 1, Time),
            new EngineObserverRelay(work => _ = _ui.BeginInvoke(work)),
            Engine,
            Localization,
            _catalogs,
            new PanelSearch(Foreground, Engine, static () => 1, Time, keyboard: null),
            new RandomIdGenerator(),
            Time,
            _ui,
            selfElevated: false
        );

        // As the app does: the composer follows the document on the UI thread.
        Store.Changed += (_, change) => Panel.OnDocumentChanged(change);
    }

    public FakeTimeProvider Time { get; } =
        new(new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero));

    public DocumentStore Store { get; }

    public SessionStore Session { get; }

    public InteractionStore Interaction { get; }

    public ProfileViewCoordinator Profiles { get; }

    public LocalizationContext Localization { get; }

    public RecordingInbox Engine { get; } = new();

    public CountingForeground Foreground { get; } = new();

    public PanelComposer Panel { get; }

    /// <summary>The Control Center of this world, tied to nothing yet; no window of it is ever shown.</summary>
    public ControlCenterComposer ControlCenter(ThemeService theme) =>
        new(
            Store,
            Localization,
            Foreground,
            Engine,
            static () => 1,
            Profiles,
            theme,
            _ui,
            Time,
            _catalogs,
            keyboard: null,
            new NoOpenApps(),
            static () => default(Rect),
            selfElevated: false
        );

    /// <summary>Writes a setting as General does and lets the composer follow it.</summary>
    public void Set(string path, object value)
    {
        Store.Dispatch(new SetSetting(path, value)).IsSuccess.ShouldBeTrue();
        UiThread.Drain();
    }

    /// <summary><paramref name="message"/> in the language of the world.</summary>
    public string Text(Message message) => Localization.Current.Format(message);

    /// <summary>The process of a foreground of <see cref="ScriptedMonitor"/>.</summary>
    public static ForegroundDetails Describe(ExternalForeground foreground) =>
        new(
            foreground.ProcessId == ScriptedMonitor.WordId ? WordApp : ExcelApp,
            KeyboardLayoutSnapshot.Empty
        );

    /// <summary>A shortcut that is not complete: «Probar ahora» ends before it touches any window.</summary>
    public static Shortcut Shortcut(string id, string name) =>
        new(
            new ShortcutId(id),
            LocalizedText.Same(name, LangCode.Es, LangCode.En),
            new IconRef("bolt"),
            AutoIcon: false,
            new CategoryId("edit"),
            new TapAction(KeyChord.Empty, []),
            new ShortcutOptions(Confirm: false, new HoldLimit.InheritGlobal(), IsPrivate: false),
            Origin: null,
            PinnedFrom: null
        );

    private static ShortcutLibrary Library() =>
        ShortcutLibrary
            .CreateValidated(
                [],
                [
                    Profile(ProfileId.General, "General", null),
                    Profile(Word, "Word", WordApp, Shortcut("bold", "Negrita")),
                    Profile(Excel, "Excel", ExcelApp, Shortcut("sum", "Suma")),
                ]
            )
            .Value;

    private static Profile Profile(
        ProfileId id,
        string name,
        ProcessName? process,
        params Shortcut[] shortcuts
    ) =>
        new(
            id,
            LocalizedText.Same(name, LangCode.Es, LangCode.En),
            new IconRef("apps"),
            AutoIcon: false,
            process is { } app ? new AppBinding.Processes([app]) : new AppBinding.Manual(),
            InjectionMode.VirtualKey,
            [.. shortcuts],
            Origin: null
        );

    /// <summary>Keeps what the composition posts to the engine.</summary>
    internal sealed class RecordingInbox : IEngineInbox
    {
        public List<EngineEvent> Posted { get; } = [];

        public bool Post(EngineEvent engineEvent)
        {
            Posted.Add(engineEvent);
            return true;
        }
    }

    /// <summary>Counts the requests for the foreground and refuses them all.</summary>
    internal sealed class CountingForeground : IForegroundOrchestrator
    {
        public ForegroundSnapshot Current => ForegroundSnapshot.Empty;

        public int Requests { get; private set; }

        public ValueTask<LeaseResult> AcquireAsync(
            LeaseRequest request,
            CancellationToken cancellationToken
        )
        {
            Requests++;
            return ValueTask.FromResult<LeaseResult>(
                new LeaseResult.Denied(ForegroundDenialReason.RightsRefused)
            );
        }
    }

    /// <summary>The app in front, as the test says.</summary>
    internal sealed class ScriptedMonitor : IForegroundMonitor
    {
        public const uint WordId = 1;
        public const uint ExcelId = 2;

        public ExternalForeground? Current { get; private set; }

        public event EventHandler<ExternalForegroundChangedEventArgs>? ExternalForegroundChanged;

        public void Front(uint processId)
        {
            Current = new ExternalForeground(
                new WindowToken((nint)processId),
                processId,
                1,
                DateTimeOffset.UnixEpoch
            );
            ExternalForegroundChanged?.Invoke(
                this,
                new ExternalForegroundChangedEventArgs(Current)
            );
            UiThread.Drain();
        }
    }

    /// <summary>A system in the dark theme that never changes.</summary>
    internal sealed class DarkSystem : ISystemThemeSource
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public SystemThemeState Read() =>
            new(AppsUseLightTheme: false, HighContrast: false, ClientAreaAnimation: false);
    }

    private sealed class NoOpenApps : IOpenApps
    {
        public ValueTask<ImmutableArray<OpenApp>> ListAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(ImmutableArray<OpenApp>.Empty);
    }

    private sealed class NoBackups : IBackupService
    {
        public void SnapshotNow(UserDocument document, BackupKind kind) { }

        public Task<Result<int>> WriteSnapshotsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Results.Ok(0));

        public Task<Result<BackupInfo>> CreateAsync(
            UserDocument document,
            BackupKind kind,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public Task<ImmutableArray<BackupInfo>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ImmutableArray<BackupInfo>.Empty);

        public Task<Result<UserDocument>> ReadAsync(
            BackupId id,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();
    }
}
