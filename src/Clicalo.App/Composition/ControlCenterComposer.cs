using System.IO;
using System.Windows;
using System.Windows.Threading;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Foreground;
using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Application.Profiles;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Catalogs;
using Clicalo.Presentation.ControlCenter;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Workspace;

namespace Clicalo.App.Composition;

/// <summary>
/// Opens and closes the Control Center (docs/05, blueprint §3.6 and §8.1) on the UI thread, in the Workspace role:
/// the window is created the first time and then hidden and shown again; it comes to the front through a
/// <see cref="LeaseKind.ControlCenter"/> lease and, when it closes, the foreground goes back to the app that was in
/// front before it opened (CCM-004). It wires «Atajos» to the document, the language, the foreground and «Probar
/// ahora», and keeps the status bar's message on show for its time (AVI-002, CCM-003).
/// </summary>
internal sealed class ControlCenterComposer : IDisposable, ITryNowWindow
{
    private readonly DocumentStore _store;
    private readonly ILocalizationContext _localization;
    private readonly IForegroundOrchestrator _foreground;
    private readonly ThemeService _theme;
    private readonly Dispatcher _ui;
    private readonly TimeProvider _time;
    private readonly RuntimeCatalogs _runtime;
    private readonly ITouchKeyboard? _keyboard;
    private readonly IOpenApps _openApps;
    private readonly ProfileViewCoordinator _profilesInView;
    private readonly TryNowRun _tryNow;
    private readonly Func<Rect> _panel;
    private readonly ShortcutsWorkspace _shortcuts;
    private readonly ProfileWorkspace _profiles;
    private EditorCatalogs _catalogs = EditorCatalogs.Empty;
    private Task? _catalogsLoad;
    private ControlCenterViewModel? _viewModel;
    private ControlCenterWindow? _window;
    private ForegroundLease? _lease;
    private ITimer? _noticeTimer;
    private ProcessName? _lastApp;
    private Size? _size;
    private bool _open;

    /// <summary>Creates the composer; nothing is shown until <see cref="OpenAsync(LeaseOrigin)"/>.</summary>
    /// <param name="store">The document.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="foreground">The owner of foreground changes.</param>
    /// <param name="engine">The engine mailbox, for «Probar ahora».</param>
    /// <param name="foregroundEpoch">The epoch of the external foreground.</param>
    /// <param name="profilesInView">Which profile the panel shows and the app in front.</param>
    /// <param name="theme">The theme service of the UI thread.</param>
    /// <param name="ui">The UI dispatcher.</param>
    /// <param name="time">The clock.</param>
    /// <param name="runtime">The key labels and the starter content the panel read.</param>
    /// <param name="keyboard">The touch keyboard and dictation; null where there is none.</param>
    /// <param name="openApps">The open apps.</param>
    /// <param name="panel">The rectangle of the panel, which the window does not cover (CCM-004).</param>
    /// <param name="selfElevated">Whether Clícalo runs elevated.</param>
    public ControlCenterComposer(
        DocumentStore store,
        ILocalizationContext localization,
        IForegroundOrchestrator foreground,
        IEngineInbox engine,
        Func<long> foregroundEpoch,
        ProfileViewCoordinator profilesInView,
        ThemeService theme,
        Dispatcher ui,
        TimeProvider time,
        RuntimeCatalogs runtime,
        ITouchKeyboard? keyboard,
        IOpenApps openApps,
        Func<Rect> panel,
        bool selfElevated
    )
    {
        _store = store;
        _localization = localization;
        _foreground = foreground;
        _theme = theme;
        _ui = ui;
        _time = time;
        _runtime = runtime;
        _keyboard = keyboard;
        _openApps = openApps;
        _profilesInView = profilesInView;
        _panel = panel;
        _tryNow = new TryNowRun(foreground, engine, foregroundEpoch, time, selfElevated);
        _catalogs = _catalogs with { KeyLabels = runtime.KeyLabels, Starter = runtime.Content };
        _shortcuts = new ShortcutsWorkspace(store, localization, () => _catalogs, ShownProfile);
        _profiles = new ProfileWorkspace(store, _shortcuts, () => _catalogs);
        _shortcuts.Noticed += (_, e) => Notify(e.Notice);
        _profiles.Noticed += (_, e) => Notify(e.Notice);
        _shortcuts.Changed += (_, _) => Invalidate();
        _profiles.Changed += (_, _) => Invalidate();
        store.Changed += (_, _) => _ = _ui.BeginInvoke(OnDocumentChanged);
        localization.LanguageChanged += (_, _) => _ = _ui.BeginInvoke(Relocalize);
    }

    /// <summary>Whether the window is open.</summary>
    public bool IsOpen => _open;

    /// <inheritdoc />
    WindowToken ITryNowWindow.Window => _window?.Token ?? WindowToken.None;

    /// <summary>
    /// The verified external foreground changed (from any thread): the default of «Probar en» (PRB-003) and the capture
    /// mode (ATJ-008), except the switches of «Probar ahora» (PRB-006).
    /// </summary>
    /// <param name="process">The process in front.</param>
    public void OnExternalForeground(ProcessName process) =>
        _ = _ui.BeginInvoke(() =>
        {
            if (process.IsEmpty || _tryNow.IsRunning)
            {
                return;
            }

            _lastApp = process;
            _profiles.OnForeground(process);
        });

    /// <summary>Opens the section «Atajos» on the profile in view (tray, panel).</summary>
    /// <param name="origin">What asked for it, for the foreground ladder.</param>
    public Task OpenAsync(LeaseOrigin origin) =>
        OpenAsync(ControlCenterSection.Shortcuts, null, null, false, origin);

    /// <summary>The panel's edit mode: the editor with <paramref name="shortcut"/> (docs/04, docs/05 §1).</summary>
    /// <param name="shortcut">The shortcut touched.</param>
    /// <param name="origin">What asked for it.</param>
    public Task OpenEditorAsync(ShortcutId shortcut, LeaseOrigin origin) =>
        OpenAsync(ControlCenterSection.Shortcuts, null, shortcut, false, origin);

    /// <summary>«+ Añadir» of the panel: «Añadir atajo» for <paramref name="profile"/> (ATJ-010).</summary>
    /// <param name="profile">The profile of the panel.</param>
    /// <param name="origin">What asked for it.</param>
    public Task OpenLibraryAsync(ProfileId profile, LeaseOrigin origin) =>
        OpenAsync(
            ControlCenterSection.Shortcuts,
            new ListRef.InProfile(profile),
            null,
            true,
            origin
        );

    /// <summary>The panel's «Plantillas» tile: the section Plantillas.</summary>
    /// <param name="origin">What asked for it.</param>
    public Task OpenTemplatesAsync(LeaseOrigin origin) =>
        OpenAsync(ControlCenterSection.Templates, null, null, false, origin);

    /// <summary>
    /// Closes the window (✕, Esc, Alt+F4): a blank draft is discarded (ATJ-011), the window hides and the foreground
    /// goes back to the app that was in front before it opened (CCM-004).
    /// </summary>
    public async Task CloseAsync()
    {
        if (!_open || _window is null)
        {
            return;
        }

        _open = false;
        _shortcuts.Close();
        _profiles.CancelCapture();
        _size = new Size(_window.ActualWidth, _window.ActualHeight);
        _window.Hide();
        var lease = _lease;
        _lease = null;
        if (lease is not null)
        {
            _ = await lease.RestoreAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _noticeTimer?.Dispose();
        _viewModel?.Shortcuts.Editor.Dispose();
        _window?.Destroy();
    }

    /// <inheritdoc />
    void ITryNowWindow.Hide() => _window?.Hide();

    /// <inheritdoc />
    void ITryNowWindow.Show() => _window?.Show();

    private ProfileId ShownProfile() => _profilesInView.ProfileButtonTarget;

    private async Task OpenAsync(
        ControlCenterSection section,
        ListRef? list,
        ShortcutId? shortcut,
        bool library,
        LeaseOrigin origin
    )
    {
        _ui.VerifyAccess();
        await LoadCatalogsAsync().ConfigureAwait(true);
        var window = EnsureWindow();
        var viewModel = _viewModel!;
        viewModel.Select(section);
        if (!_open || shortcut is not null || library || list is not null)
        {
            _shortcuts.Open(
                list ?? (_open ? null : new ListRef.InProfile(ShownProfile())),
                shortcut,
                library
            );
            viewModel.Shortcuts.OnOpened();
        }

        if (!_open)
        {
            _open = true;
            window.Place(SystemParameters.WorkArea, _panel(), _size);
            window.Show();
        }

        if (_lease is not { IsActive: true })
        {
            var result = await _foreground
                .AcquireAsync(
                    new LeaseRequest(LeaseKind.ControlCenter, window.Token, origin, null),
                    CancellationToken.None
                )
                .ConfigureAwait(true);
            _lease = result is LeaseResult.Granted granted ? granted.Lease : null;
        }
    }

    private Task LoadCatalogsAsync()
    {
        _catalogsLoad ??= LoadAsync();
        return _catalogsLoad;

        async Task LoadAsync()
        {
            var baseDirectory = AppContext.BaseDirectory;
            var loaded = await Task.Run(() =>
                    EditorCatalogFiles.Load(
                        Path.Combine(baseDirectory, RuntimeCatalogs.FolderName),
                        ContentFiles.Find(baseDirectory),
                        _runtime.Content,
                        _runtime.KeyLabels
                    )
                )
                .ConfigureAwait(true);
            _catalogs = loaded;
        }
    }

    private ControlCenterWindow EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        var services = new ControlCenterServices(
            _store,
            _shortcuts,
            _profiles,
            _localization,
            () => _catalogs,
            new TwoStepConfirm(_time),
            _time,
            action => _ = _ui.BeginInvoke(action),
            ActiveAppProfile,
            _openApps.ListAsync,
            () => _lastApp,
            DictateAsync,
            TryNowAsync,
            () => _viewModel?.Select(ControlCenterSection.Templates)
        );
        _viewModel = new ControlCenterViewModel(services, () => _ = CloseAsync());
        _window = new ControlCenterWindow(_viewModel, _theme);
        _window.CloseRequested += (_, _) => _ = CloseAsync();
        return _window;
    }

    private Profile? ActiveAppProfile() =>
        _profilesInView.ActiveAppProfile is { } id
        && _store.Current.Library.TryGetProfile(id, out var profile)
            ? profile
            : null;

    private async ValueTask<bool> DictateAsync(CancellationToken cancellationToken) =>
        _keyboard is not null
        && await _keyboard.StartDictationAsync(cancellationToken).ConfigureAwait(true);

    private async ValueTask<TryNowOutcome> TryNowAsync(
        Shortcut shortcut,
        OpenApp target,
        CancellationToken cancellationToken
    )
    {
        var library = _store.Current.Library;
        var origin =
            library.TryLocate(shortcut.Id, out var location)
            && location.List is ListRef.InProfile list
            && library.TryGetProfile(list.Id, out var profile)
                ? profile
                : null;
        var (outcome, lease) = await _tryNow
            .RunAsync(
                shortcut,
                origin?.Id,
                (origin ?? library.General).Injection,
                target,
                this,
                cancellationToken
            )
            .ConfigureAwait(true);
        if (lease is not null)
        {
            _lease = lease;
        }

        switch (outcome)
        {
            case TryNowOutcome.Elevated:
                Notify(
                    new WorkspaceNotice(
                        L.ElevatedRefused(app: target.Name),
                        "admin_panel_settings",
                        false,
                        true
                    )
                );
                break;
            case TryNowOutcome.NotActivated:
                Notify(new WorkspaceNotice(L.AppGone(app: target.Name), "warning", false, true));
                break;
            case TryNowOutcome.Blocked:
                Notify(new WorkspaceNotice(L.BlockedB, "block", false, true));
                break;
            case TryNowOutcome.Incomplete:
                Notify(new WorkspaceNotice(L.Incomplete, "warning", false, true));
                break;
        }

        return outcome;
    }

    private void Notify(WorkspaceNotice notice)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ShowNotice(notice);
        _noticeTimer?.Dispose();
        _noticeTimer = _time.CreateTimer(
            static state =>
            {
                var composer = (ControlCenterComposer)state!;
                _ = composer._ui.BeginInvoke(() => composer._viewModel?.ClearNotice());
            },
            this,
            notice.CanUndo ? Timings.Notices.UndoNoticeDuration : Timings.Notices.NoticeDuration,
            Timeout.InfiniteTimeSpan
        );
    }

    private void Invalidate()
    {
        _viewModel?.Shortcuts.Invalidate();
        _viewModel?.Refresh();
    }

    private void OnDocumentChanged()
    {
        _shortcuts.OnDocumentChanged();
        _profiles.OnDocumentChanged();
        Invalidate();
    }

    private void Relocalize() => Invalidate();
}
