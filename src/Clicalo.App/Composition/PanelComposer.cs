using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Threading;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Interaction;
using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Application.Profiles;
using Clicalo.Application.Session;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Timing;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.ContextMenu;
using Clicalo.Presentation.Panel.EditMode;
using Clicalo.Presentation.Panel.Header;
using Clicalo.Presentation.Panel.QuickSettings;
using Clicalo.Presentation.Panel.Search;
using Clicalo.Presentation.Panel.TestMode;
using Clicalo.UI.Wpf.Surfaces;

namespace Clicalo.App.Composition;

/// <summary>
/// Wires the full panel to the real document, session, engine and foreground (docs/04 §1–§11), on the UI thread of the
/// Surfaces role. It owns no rule: the view in front is the <see cref="ProfileViewCoordinator"/>'s, the profile grid is
/// the <see cref="SessionStore"/>'s, the search and the suggestion are their view models', and every product decision
/// is a call to the domain. It implements the intentions of the body (<see cref="IPanelBodyIntents"/>), keeps the notice
/// on show for its duration (AVI-002) and projects everything again, once per dispatcher turn, whenever an input
/// changes; while a finger rests on the panel the projection waits for it (PAN-009). It also owns the layers above
/// the tiles (Quick settings, edit mode, the tile menu and test mode): it is their notice sink, keeps one primary layer
/// open at a time (PAN-008) and passes the intentions for the control center on to <see cref="ControlCenter"/>.
/// </summary>
internal sealed class PanelComposer : IPanelBodyIntents, IPanelNoticeSink, IControlCenterIntents
{
    private const string NoticeIcon = "info";
    private const string WarningIcon = "warning";
    private const string UndoIcon = "undo";
    private const string LockedIcon = "lock";
    private const string FollowingIcon = "autorenew";

    private readonly DocumentStore _store;
    private readonly SessionStore _session;
    private readonly InteractionStore _interaction;
    private readonly ProfileViewCoordinator _profiles;
    private readonly PanelInteractionController _controller;
    private readonly EngineObserverRelay _relay;
    private readonly IEngineInbox _engine;
    private readonly ILocalizationContext _localization;
    private readonly RuntimeCatalogs _catalogs;
    private readonly TimeProvider _time;
    private readonly Dispatcher _ui;
    private readonly bool _selfElevated;
    private PanelWindow? _window;
    private PanelNotice? _notice;
    private object? _stickyOwner;
    private ITimer? _noticeTimer;

    // Kept referenced until they fire, so the collector cannot drop a flash before it ends.
    private readonly HashSet<ITimer> _flashTimers = [];
    private string? _elevatedApp;
    private bool _refreshQueued;
    private bool _resultsChanged = true;

    /// <summary>Creates the view models of the panel on the UI thread.</summary>
    /// <param name="store">The document.</param>
    /// <param name="session">The session of the Surfaces role.</param>
    /// <param name="interaction">The interaction state of the Surfaces role: «−» minimizes to the bubble there.</param>
    /// <param name="profiles">Which profile is in view.</param>
    /// <param name="controller">Where the tiles' intentions go.</param>
    /// <param name="relay">The engine's snapshots, notices and last action.</param>
    /// <param name="engine">The engine mailbox.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="catalogs">Key labels, common actions and starter content.</param>
    /// <param name="search">The keyboard lease and the runs of the search.</param>
    /// <param name="ids">The source of the ids of installed content.</param>
    /// <param name="time">The clock of the notices.</param>
    /// <param name="ui">The dispatcher of the UI thread.</param>
    /// <param name="selfElevated">Whether Clícalo runs elevated (EJE-013).</param>
    public PanelComposer(
        DocumentStore store,
        SessionStore session,
        InteractionStore interaction,
        ProfileViewCoordinator profiles,
        PanelInteractionController controller,
        EngineObserverRelay relay,
        IEngineInbox engine,
        ILocalizationContext localization,
        RuntimeCatalogs catalogs,
        PanelSearch search,
        IIdGenerator ids,
        TimeProvider time,
        Dispatcher ui,
        bool selfElevated
    )
    {
        _store = store;
        _session = session;
        _interaction = interaction;
        _profiles = profiles;
        _controller = controller;
        _relay = relay;
        _engine = engine;
        _localization = localization;
        _catalogs = catalogs;
        _time = time;
        _ui = ui;
        _selfElevated = selfElevated;

        var settings = store.Current.Settings;
        Panel = new PanelViewModel(
            controller,
            localization,
            SettingsProjection.Touch(settings),
            NameOf,
            this,
            modifier => KeyLabelOf(modifier)
        );
        Panel.ApplyLayout(PanelLayoutSettings.From(settings));

        // The layers above the tiles: one two-tap confirmation for the Surfaces role (REG-04), timers back on this thread.
        void Post(Action work) => _ = ui.BeginInvoke(work);
        TestMode = new TestModeViewModel(engine, localization, this, time, Post);
        EditMode = new EditModeViewModel(
            store,
            new TwoStepConfirm(time),
            localization,
            this,
            this,
            time,
            Post
        );
        Menu = new TileContextMenuViewModel(store, localization, this, this);
        QuickSettings = new QuickSettingsViewModel(
            store,
            localization,
            TestMode,
            this,
            () =>
                _profiles.State.View is ViewTarget.Frequents ? null : _profiles.ProfileButtonTarget
        );
        Layers = new PanelLayerModels(
            QuickSettings,
            EditMode,
            Menu,
            TestMode,
            new TileInteractionModes(EditMode, TestMode, Menu),
            () => _profiles.State.View is ViewTarget.Frequents
        );

        Search = new SearchViewModel(
            search,
            localization,
            () => _window?.SurfaceWindow ?? default,
            shortcut => KeyLines(forSearch: true).For(shortcut).Line,
            message => Notify(message, NoticeTone.Warning, WarningIcon)
        );
        Suggestion = new SuggestionViewModel(
            store,
            catalogs.Content,
            localization,
            ids,
            () => _profiles.State,
            OnSuggestionAccepted,
            message => Notify(message, NoticeTone.Warning, WarningIcon)
        );
        Header = new PanelHeaderViewModel(
            profiles,
            localization,
            new PanelHeaderActions(
                Search: () =>
                {
                    CloseLayers();
                    _ = Search.ToggleAsync(SearchTrigger.Touch);
                },
                Edit: () =>
                {
                    // AJR-001: edit mode closes Quick settings.
                    CloseLayers();
                    EditMode.Toggle();
                },
                QuickSettings: ToggleQuickSettings,
                // PAN-001 a: «−» turns the panel into the bubble; Quick settings close with it.
                Minimize: () =>
                {
                    CloseLayers();
                    _ = _interaction.Dispatch(new InteractionAction.Minimize());
                }
            )
        );

        _profiles.Changed += OnProfileViewChanged;
        _session.Changed += (_, _) => Invalidate();
        Search.PropertyChanged += OnSearchChanged;
        Search.Results.CollectionChanged += OnResultsChanged;
        Suggestion.PropertyChanged += (_, _) => Invalidate();
        EditMode.PropertyChanged += (_, _) => Invalidate();
        QuickSettings.PropertyChanged += (_, _) => Invalidate();
        Menu.PropertyChanged += (_, _) => Invalidate();
        TestMode.PropertyChanged += (_, _) => Invalidate();
        Search.ApplyLibrary(store.Current.Library);
        Refresh();
    }

    /// <summary>The body and the panic strip.</summary>
    public PanelViewModel Panel { get; }

    /// <summary>The header.</summary>
    public PanelHeaderViewModel Header { get; }

    /// <summary>The search.</summary>
    public SearchViewModel Search { get; }

    /// <summary>The profile suggestion.</summary>
    public SuggestionViewModel Suggestion { get; }

    /// <summary>Quick settings (AJR-001).</summary>
    public QuickSettingsViewModel QuickSettings { get; }

    /// <summary>Edit mode (CUA-012).</summary>
    public EditModeViewModel EditMode { get; }

    /// <summary>The tile menu (CUA-014).</summary>
    public TileContextMenuViewModel Menu { get; }

    /// <summary>Test mode (TAC-008).</summary>
    public TestModeViewModel TestMode { get; }

    /// <summary>The layers above the tiles, for the panel window to host.</summary>
    public PanelLayerModels Layers { get; }

    /// <summary>
    /// The control center (Workspace role) that opens the editor, the library and its pages for the panel (CCM-004).
    /// Until it exists the intentions go nowhere; it marshals them to its own thread.
    /// </summary>
    public IControlCenterIntents? ControlCenter { get; set; }

    /// <summary>Raised after every projection, on the UI thread: the Tab view follows the same shortcuts.</summary>
    public event EventHandler? Refreshed;

    /// <summary>The last projection of the view in front: the shortcuts of the grid and of Always visible.</summary>
    public PanelModel LastModel { get; private set; } = PanelModel.Empty;

    /// <summary>Whether ↻ Repeat has a last action (AVI-004).</summary>
    public bool CanRepeat => RepeatBinding() is not null;

    /// <summary>The window the view models are drawn in; the projection waits for its fingers (PAN-009).</summary>
    /// <param name="window">The panel window.</param>
    public void AttachWindow(PanelWindow window)
    {
        _window = window;
        window.ContactsEnded += (_, _) =>
        {
            if (_refreshQueued)
            {
                Refresh();
            }
        };
    }

    /// <summary>
    /// Follows the app in front (PER-003, EJE-013, SEL-003): from the SysEvents thread, marshalled here. The monitor
    /// already leaves out Clícalo's windows, the shell, the touch keyboard and Voice access.
    /// </summary>
    /// <param name="monitor">The verified external foreground.</param>
    /// <param name="describe">Resolves the process of a foreground.</param>
    public void Follow(
        IForegroundMonitor monitor,
        Func<ExternalForeground, ForegroundDetails> describe
    )
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentNullException.ThrowIfNull(describe);
        monitor.ExternalForegroundChanged += (_, change) => Report(change.Foreground);
        if (monitor.Current is { } current)
        {
            Report(current);
        }

        void Report(ExternalForeground foreground)
        {
            var process = describe(foreground).Process;
            var elevated =
                ForegroundChangeCoordinator.ElevationOf(foreground.Elevation, _selfElevated)
                == ElevationState.TargetElevated;
            _ = _ui.BeginInvoke(() => OnForeground(process, elevated));
        }
    }

    /// <summary>The document changed (on the UI thread): profiles, search, suggestion and layout follow it.</summary>
    /// <param name="change">The change.</param>
    public void OnDocumentChanged(DocumentChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        _profiles.OnDocumentChanged();
        Search.ApplyLibrary(change.After.Library);
        Suggestion.Apply(_profiles.ActiveApp ?? default, Search.IsSearching);
        var before = change.Before.Settings;
        var after = change.After.Settings;
        if (!ReferenceEquals(before, after))
        {
            Panel.ApplyLayout(PanelLayoutSettings.From(after));
            QuickSettings.Apply(after);
            if (before.StickyModifiersRow && !after.StickyModifiersRow)
            {
                // FIJ-005: turning the row off releases every sticky modifier.
                _ = _engine.Post(new EngineEvent.ClearSticky());
            }
        }

        Invalidate();
    }

    /// <summary>The interface language changed (IDI-001).</summary>
    public void Relocalize()
    {
        Panel.Relocalize();
        Header.Relocalize();
        Search.Relocalize();
        Suggestion.Relocalize();
        QuickSettings.Relocalize();
        EditMode.Relocalize();
        Menu.Relocalize();
        TestMode.Relocalize();
        _resultsChanged = true;
        Invalidate();
    }

    /// <summary>
    /// Shows a notice in the notice bar for <c>Timings.Notices.NoticeDuration</c> (or the undo duration when it offers
    /// [undo]), the newest replacing the one on show (AVI-001, AVI-002, AVI-003).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tone">Notice (polite) or warning (assertive).</param>
    /// <param name="icon">Its Material Symbols icon.</param>
    /// <param name="canUndo">Whether [undo] applies.</param>
    public void Notify(Message text, NoticeTone tone, string icon, bool canUndo = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        _stickyOwner = null;
        _notice = new PanelNotice(text, new IconRef(icon), tone, canUndo && _store.CanUndo);
        _noticeTimer?.Dispose();
        _noticeTimer = _time.CreateTimer(
            static state => ((PanelComposer)state!).QueueNoticeEnd(),
            this,
            canUndo ? Timings.Notices.UndoNoticeDuration : Timings.Notices.NoticeDuration,
            Timeout.InfiniteTimeSpan
        );
        Invalidate();
    }

    /// <inheritdoc />
    public void Notify(PanelNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        Notify(notice.Text, notice.Tone, notice.Icon.Name, notice.CanUndo);
    }

    /// <inheritdoc />
    public void ShowSticky(object owner, PanelNotice notice)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(notice);
        _noticeTimer?.Dispose();
        _noticeTimer = null;
        _notice = notice;
        _stickyOwner = owner;
        Invalidate();
    }

    /// <inheritdoc />
    public void ClearSticky(object owner)
    {
        if (!ReferenceEquals(_stickyOwner, owner))
        {
            return;
        }

        _stickyOwner = null;
        _notice = null;
        Invalidate();
    }

    /// <inheritdoc />
    public void OpenEditor(ShortcutId shortcut) => ControlCenter?.OpenEditor(shortcut);

    /// <inheritdoc />
    public void OpenLibrary(ProfileId profile) => ControlCenter?.OpenLibrary(profile);

    /// <inheritdoc />
    public void OpenControlCenter(ProfileId profile) => ControlCenter?.OpenControlCenter(profile);

    /// <summary>A notice of the engine (AVI-001): assertive ones are warnings.</summary>
    /// <param name="notice">The notice.</param>
    public void OnEngineNotice(EngineNoticeEventArgs notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        if (notice.Urgency == NoticeUrgency.Polite)
        {
            Notify(notice.Text, NoticeTone.Notice, NoticeIcon);
        }
        else
        {
            Notify(notice.Text, NoticeTone.Warning, WarningIcon);
        }
    }

    /// <summary>Something ran: Frequents and ↻ Repeat may change (FRE-002, AVI-004).</summary>
    public void OnActionRan() => Invalidate();

    /// <summary>
    /// The action of <paramref name="shortcut"/> ran: its tile flashes for <c>Timings.Notices.ExecutionFlash</c> when the
    /// settings ask for it (EJE-012, CUA-009).
    /// </summary>
    /// <param name="shortcut">The shortcut that ran.</param>
    public void FlashTile(ShortcutId shortcut)
    {
        if (!_store.Current.Settings.Feedback.Flash)
        {
            return;
        }

        var tiles = Panel
            .Tiles.Concat(Panel.Strip.Tiles)
            .Where(tile => tile.Id == shortcut)
            .ToArray();
        if (tiles.Length == 0)
        {
            return;
        }

        foreach (var tile in tiles)
        {
            tile.Flash(true);
        }

        ITimer? timer = null;
        timer = _time.CreateTimer(
            _ =>
                _ = _ui.BeginInvoke(() =>
                {
                    foreach (var tile in tiles)
                    {
                        tile.Flash(false);
                    }

                    if (timer is not null && _flashTimers.Remove(timer))
                    {
                        timer.Dispose();
                    }
                }),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan
        );
        _ = _flashTimers.Add(timer);
        _ = timer.Change(Timings.Notices.ExecutionFlash, Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public void ShowFrequents()
    {
        _ = _session.Dispatch(new SessionAction.ClosePicker());
        _profiles.ShowFrequents();
    }

    /// <inheritdoc />
    public void ReturnFromFrequents() => _ = _profiles.ReturnFromFrequents();

    /// <inheritdoc />
    public void TogglePicker()
    {
        _ = _session.Dispatch(new SessionAction.TogglePicker());
        if (_session.Current.PickerOpen)
        {
            // PAN-008: one primary layer at a time.
            _ = Search.CloseAsync();
            CloseLayers();
        }
    }

    /// <inheritdoc />
    public void ChooseProfile(ProfileId profile)
    {
        _ = _session.Dispatch(new SessionAction.ClosePicker());
        _ = Search.CloseAsync();
        _profiles.Choose(profile);
    }

    /// <inheritdoc />
    public void CreateProfileForActiveApp()
    {
        _ = _session.Dispatch(new SessionAction.ClosePicker());
        Suggestion.Create();
    }

    /// <inheritdoc />
    /// <remarks>The Control Center arrives in M4; until then the tile does nothing.</remarks>
    public void OpenTemplates() { }

    /// <inheritdoc />
    public void AdvanceSticky(ModifierKind modifier) =>
        _ = _engine.Post(new EngineEvent.StickyTapped(modifier));

    /// <inheritdoc />
    public void Undo()
    {
        if (_store.Undo().IsSuccess)
        {
            Notify(L.RestoredU, NoticeTone.Notice, UndoIcon);
        }
    }

    /// <inheritdoc />
    public void Repeat()
    {
        if (RepeatBinding() is { } binding)
        {
            _ = _controller.Invoked(binding);
        }
    }

    /// <inheritdoc />
    /// <remarks>The editor arrives with the Control Center in M4; until then the button does nothing.</remarks>
    public void AddShortcut(ProfileId profile) { }

    /// <inheritdoc />
    /// <remarks>The verified elevated relaunch (D-11) is not built yet; the notice still explains why nothing is sent.</remarks>
    public void RelaunchElevated() { }

    private void OnForeground(ProcessName process, bool elevated)
    {
        var name = process.IsEmpty ? null : DisplayName(process);
        var elevatedApp = elevated ? name : null;
        if (!string.Equals(elevatedApp, _elevatedApp, StringComparison.Ordinal))
        {
            _elevatedApp = elevatedApp;
            Invalidate();
        }

        if (_profiles.OnActiveApp(process))
        {
            Suggestion.Apply(process, Search.IsSearching);
        }
    }

    /// <summary>Opens or closes Quick settings; open, it is the only primary layer (PAN-008).</summary>
    private void ToggleQuickSettings()
    {
        Menu.Close();
        QuickSettings.Toggle();
        if (QuickSettings.IsOpen)
        {
            _ = Search.CloseAsync();
            _ = _session.Dispatch(new SessionAction.ClosePicker());
        }
    }

    /// <summary>Closes Quick settings and the tile menu (another layer opens or the panel goes away).</summary>
    private void CloseLayers()
    {
        QuickSettings.Close();
        Menu.Close();
    }

    private void OnProfileViewChanged(object? sender, ProfileViewChangedEventArgs change)
    {
        // CUA-014: another view closes the tile menu.
        Menu.Close();
        if (change.Current.View is ViewTarget.Profile shown)
        {
            _ = _session.Dispatch(new SessionAction.ShowProfile(shown.Id));
        }

        if (change.AppChanged)
        {
            // PER-003 step 5, BUS-001, PAN-008: another app closes the search and the profile grid.
            Search.OnAppChanged();
            _ = _session.Dispatch(new SessionAction.ClosePicker());
        }

        if (change.Notice is { } notice)
        {
            Notify(
                PanelHeaderProjection.NoticeMessage(
                    notice,
                    _store.Current.Library,
                    Language(),
                    LangCode.Es
                ),
                NoticeTone.Notice,
                notice is ProfileNotice.Locked ? LockedIcon : FollowingIcon
            );
        }

        Invalidate();
    }

    private void OnSearchChanged(object? sender, PropertyChangedEventArgs change)
    {
        switch (change.PropertyName)
        {
            case nameof(SearchViewModel.IsOpen):
                if (Search.IsOpen)
                {
                    // PAN-008: the search, the profile grid and Quick settings close each other.
                    _ = _session.Dispatch(new SessionAction.ClosePicker());
                    CloseLayers();
                }

                Invalidate();
                break;
            case nameof(SearchViewModel.IsSearching):
                Suggestion.Apply(_profiles.ActiveApp ?? default, Search.IsSearching);
                _resultsChanged = true;
                Invalidate();
                break;
            case nameof(SearchViewModel.NoResultsText):
                _resultsChanged = true;
                Invalidate();
                break;
        }
    }

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs change)
    {
        _resultsChanged = true;
        Invalidate();
    }

    private void OnSuggestionAccepted(SuggestionAccepted accepted)
    {
        // PER-007: in Auto and outside Frequents the new profile comes into view.
        _profiles.OnTemplateInstalled(accepted.Profile);
        Notify(accepted.Notice, NoticeTone.Notice, NoticeIcon, canUndo: true);
    }

    private void QueueNoticeEnd() =>
        _ = _ui.BeginInvoke(() =>
        {
            _notice = null;
            Invalidate();
        });

    /// <summary>Projects again once the current work of the dispatcher is done.</summary>
    private void Invalidate()
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        _ = _ui.BeginInvoke(DispatcherPriority.Normal, Refresh);
    }

    /// <summary>
    /// Projects the document, the view, the search and the context into the view models. While a finger rests on the
    /// panel it waits for <see cref="PanelWindow.ContactsEnded"/>, so no button moves under the finger (PAN-009).
    /// </summary>
    private void Refresh()
    {
        if (_window?.IsTouching == true)
        {
            _refreshQueued = true;
            return;
        }

        _refreshQueued = false;
        var document = _store.Current;
        var library = document.Library;
        var language = Language();
        LastModel = ProjectView(document, language);
        Panel.Apply(LastModel);
        if (_resultsChanged)
        {
            _resultsChanged = false;
            Panel.ApplySearch([.. Search.Results], Search.NoResultsText);
        }

        Header.Apply(
            PanelHeaderProjection.Project(
                _profiles.State,
                library,
                _profiles.ActiveAppProfile,
                Search.IsSearching,
                language,
                LangCode.Es
            )
        );
        Header.ApplyLayers(Search.IsOpen, EditMode.IsOn, QuickSettings.IsOpen);
        var frequents = _profiles.State.View is ViewTarget.Frequents;
        EditMode.ApplyView(frequents, Search.IsSearching, _profiles.ProfileButtonTarget);
        Panel.ApplyContext(
            new PanelBodyContext(
                Frequents: frequents,
                SearchingWithText: Search.IsSearching,
                PickerOpen: _session.Current.PickerOpen,
                ActiveAppProfile: _profiles.ActiveAppProfile,
                SuggestionApp: Suggestion.IsVisible ? Suggestion.AppName : null,
                ElevatedApp: _elevatedApp,
                Notice: _notice,
                CanRepeat: RepeatBinding() is not null,
                EditMode: EditMode.IsOn,
                AddTile: EditMode.ShowsAdd
            )
        );
        Refreshed?.Invoke(this, EventArgs.Empty);
    }

    private PanelModel ProjectView(Clicalo.Domain.Document.UserDocument document, LangCode language)
    {
        var library = document.Library;
        if (_profiles.State.View is ViewTarget.Frequents)
        {
            return PanelProjector.ProjectFrequents(
                library,
                FrequentsProjection.Compose(
                    library,
                    document.Frequents,
                    _time.GetUtcNow(),
                    document.Settings.ShowAlwaysVisibleRow
                ),
                _profiles.ProfileButtonTarget,
                _localization.Current.Format(L.Always),
                language,
                LangCode.Es
            );
        }

        var keys = KeyLines(forSearch: false);
        return PanelProjector.Project(
            library,
            _profiles.ProfileButtonTarget,
            language,
            LangCode.Es,
            keys.For
        );
    }

    /// <summary>
    /// The key lines of the tiles (EJE-018): the combination sent to the app in front. The search matches the full
    /// combination even when the tiles hide it.
    /// </summary>
    private TileKeyLines KeyLines(bool forSearch)
    {
        var settings = _store.Current.Settings;
        return new TileKeyLines(
            _catalogs.CommonActions,
            _catalogs.KeyLabels,
            _profiles.ActiveApp,
            settings.Keyboard.AppsLanguage,
            Language(),
            Shown: forSearch || (settings.ShowKeys && settings.Density != PanelDensity.Compact),
            Abbreviated: !forSearch && settings.Size == Clicalo.Domain.Settings.PanelSize.Small
        );
    }

    /// <summary>↻ Repeat (AVI-004): the last shortcut that ran, by id, with the profile of its list.</summary>
    private TileBinding? RepeatBinding()
    {
        var library = _store.Current.Library;
        if (
            _relay.LastAction is not { } id
            || !library.TryGetShortcut(id, out var shortcut)
            || !library.TryLocate(id, out var location)
        )
        {
            return null;
        }

        var origin =
            location.List is ListRef.InProfile list && library.TryGetProfile(list.Id, out var owner)
                ? owner
                : null;
        return new TileBinding(shortcut, origin?.Id, (origin ?? library.General).Injection);
    }

    private string? NameOf(ShortcutId id) =>
        _store.Current.Library.TryGetShortcut(id, out var shortcut)
            ? shortcut.Name.Get(Language(), LangCode.Es)
            : null;

    private string KeyLabelOf(ModifierKind modifier) =>
        KeyChordFormatter.KeyText(
            new KeyStroke(KeyOf(modifier)),
            _catalogs.KeyLabels,
            KeyLabelStyle.Full,
            Language(),
            LangCode.Es
        )
            is { Length: > 0 } label
            ? label
            : modifier.ToString();

    private static KeyId KeyOf(ModifierKind modifier) =>
        modifier switch
        {
            ModifierKind.Ctrl => new KeyId("ctrl"),
            ModifierKind.Alt => new KeyId("alt"),
            ModifierKind.Shift => new KeyId("shift"),
            _ => new KeyId("win"),
        };

    /// <summary>The executable without «.exe», as the administrator notice names the app.</summary>
    private static string DisplayName(ProcessName process) =>
        process.Value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? process.Value[..^4]
            : process.Value;

    private LangCode Language() => new(_localization.Current.Locale.Code);
}
