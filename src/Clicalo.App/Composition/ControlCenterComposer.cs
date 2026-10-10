using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Foreground;
using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Application.Profiles;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Catalogs;
using Clicalo.Platform.Windows.Launch.InstalledApps;
using Clicalo.Presentation.ControlCenter;
using Clicalo.Presentation.ControlCenter.About;
using Clicalo.Presentation.ControlCenter.SystemSection;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.UI.Wpf.Workspace;

namespace Clicalo.App.Composition;

/// <summary>
/// Opens and closes the Control Center (docs/05, blueprint §3.6 and §8.1) on the UI thread, in the Workspace role:
/// the window is created the first time and then hidden and shown again; it comes to the front through a
/// <see cref="LeaseKind.ControlCenter"/> lease and, when it closes, the foreground goes back to the app that was in
/// front before it opened (CCM-004). It wires «Atajos» to the document, the language, the foreground and «Probar
/// ahora», and keeps the status bar's message on show for its time (AVI-002, CCM-003). It opens where
/// <see cref="ControlCenterPlacer"/> says: beside the panel on any monitor, and where it was left the last time,
/// which it remembers in the document between restarts (CCM-001, D9).
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
    private readonly TemplatesComposition? _templates;
    private EditorCatalogs _catalogs = EditorCatalogs.Empty;
    private Task? _catalogsLoad;
    private ControlCenterViewModel? _viewModel;
    private ControlCenterWindow? _window;
    private ForegroundLease? _lease;
    private ITimer? _noticeTimer;
    private PanelNotice? _panelNotice;
    private bool _ownNotice;
    private ProcessName? _lastApp;
    private bool _maximize;
    private bool _open;
    private bool _capturing;

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
    /// <param name="templates">The pieces of Plantillas and of sharing a profile; null leaves the section a marker.</param>
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
        bool selfElevated,
        TemplatesComposition? templates = null
    )
    {
        _templates = templates;
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
        _profiles.Changed += (_, _) =>
        {
            Invalidate();
            if (_capturing != (_profiles.Capturing is not null))
            {
                _capturing = !_capturing;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        store.Changed += (_, _) => _ = _ui.BeginInvoke(OnDocumentChanged);
        localization.LanguageChanged += (_, _) => _ = _ui.BeginInvoke(Relocalize);
    }

    /// <summary>The services of «Sistema» (docs/05 §5), set before the window is first opened.</summary>
    public SystemServices? System { get; set; }

    /// <summary>
    /// Builds the services of «Acerca de y contacto» (docs/05 §6) from dictation and the status bar; set before the window
    /// is first opened.
    /// </summary>
    public Func<
        Func<CancellationToken, ValueTask<bool>>,
        Action<WorkspaceNotice>,
        AboutServices
    >? About { get; set; }

    /// <summary>«Ver la bienvenida otra vez» of General (GEN-014), set before the window is first opened.</summary>
    public Action? OpenWelcome { get; set; }

    /// <summary>
    /// Shows a fixed notice in the panel, or clears it with null: «Probar ahora» says [switching] there while the
    /// Control Center is hidden (PRB-004). Set before the window is first opened.
    /// </summary>
    public Action<Message?>? PanelNotice { get; set; }

    /// <summary>
    /// The notice the panel has on show, or null when its bars rest (CCM-003): the status bar shows it too, because
    /// the notices are one shared state. A notice of the Control Center itself stays until its time ends, and the
    /// newest notice of the panel replaces it. Called on the UI thread, also while the window does not exist yet.
    /// </summary>
    /// <param name="notice">The notice on show in the panel, or <see langword="null"/> at rest.</param>
    public void OnPanelNotice(PanelNotice? notice)
    {
        _panelNotice = notice;
        if (_viewModel is null || (notice is null && _ownNotice))
        {
            return;
        }

        ShowPanelNotice();
    }

    /// <summary>Whether «Probar ahora» is running: its app switches are not the user's (PRB-006). Any thread.</summary>
    public bool IsTrying => _tryNow.IsRunning;

    /// <summary>Whether the window is open.</summary>
    public bool IsOpen => _open;

    /// <summary>Whether the capture mode of a binding waits for the next app (ATJ-008).</summary>
    public bool IsCapturing => _capturing;

    /// <summary>
    /// Raised on the UI thread when <see cref="IsOpen"/> or <see cref="IsCapturing"/> changes: the panel does not dim
    /// while the window is open (CCM-004) and shows the capture notice with Cancelar (ATJ-008).
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>Cancelar of the capture notice in the panel (ATJ-008).</summary>
    public void CancelCapture() => _profiles.CancelCapture();

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

    /// <summary>The «Centro de control» card of Quick settings: «Atajos» on <paramref name="profile"/> (AJR-001).</summary>
    /// <param name="profile">The profile to show.</param>
    /// <param name="origin">What asked for it.</param>
    public Task OpenProfileAsync(ProfileId profile, LeaseOrigin origin) =>
        OpenAsync(
            ControlCenterSection.Shortcuts,
            new ListRef.InProfile(profile),
            null,
            false,
            origin
        );

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
        StateChanged?.Invoke(this, EventArgs.Empty);
        _viewModel?.Shortcuts.Editor.StopRecording();
        _shortcuts.Close();
        _profiles.CancelCapture();
        RememberPlacement(_window);
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
            var monitors = DisplayMonitors.Snapshot();
            var spot = ControlCenterPlacer.Plan(
                monitors,
                SurfaceToAvoid(monitors),
                _store.Current.Settings.ControlCenter
            );
            window.Place(spot);
            _maximize = spot.Maximized;
            window.Show();
            StateChanged?.Invoke(this, EventArgs.Empty);
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

        if (_maximize && _lease is { IsActive: true })
        {
            // D9: it was left maximized. Maximizing activates the window, so it waits for the lease.
            _maximize = false;
            window.Maximize();
        }
    }

    /// <summary>
    /// What the Control Center must not cover (CCM-004): the largest surface of Clícalo on screen now, which is the
    /// panel or, in the tab view, the open bar. Its rectangle is read from the window itself, in physical pixels, so
    /// it is right on any monitor and scale; a hidden panel is not avoided.
    /// </summary>
    private PhysicalRect? SurfaceToAvoid(IReadOnlyList<DisplayMonitor> monitors)
    {
        if (global::System.Windows.Application.Current is not { } app)
        {
            // No WPF application (a host that only passes the rectangle): the rectangle of the panel, as given.
            var panel = _panel();
            return panel.IsEmpty
                ? null
                : ControlCenterPlacer.FromWindowUnits(
                    monitors,
                    panel.Left,
                    panel.Top,
                    panel.Width,
                    panel.Height
                );
        }

        PhysicalRect? largest = null;
        foreach (var window in app.Windows.OfType<NonActivatingWindow>())
        {
            if (
                !window.IsVisible
                || window.ActualWidth <= 0
                || window.ActualHeight <= 0
                || PresentationSource.FromVisual(window) is null
            )
            {
                continue;
            }

            var origin = window.PointToScreen(new Point(0, 0));
            var dpi = VisualTreeHelper.GetDpi(window);
            var rect = new PhysicalRect(
                (int)Math.Round(origin.X),
                (int)Math.Round(origin.Y),
                (int)Math.Round(window.ActualWidth * dpi.DpiScaleX),
                (int)Math.Round(window.ActualHeight * dpi.DpiScaleY)
            );
            if (
                largest is not { } chosen
                || (long)rect.Width * rect.Height > (long)chosen.Width * chosen.Height
            )
            {
                largest = rect;
            }
        }

        return largest;
    }

    // CCM-001, D9: the size, the place and the monitor are remembered between restarts (document 1.1, ADR-0028).
    private void RememberPlacement(ControlCenterWindow window)
    {
        var (bounds, maximized) = window.ReadPlacement();
        if (bounds.IsEmpty)
        {
            return;
        }

        var placement = ControlCenterPlacer.Remember(DisplayMonitors.Snapshot(), bounds, maximized);
        if (_store.Current.Settings.ControlCenter != placement)
        {
            _ = _store.Dispatch(new SetSetting(SettingPaths.ControlCenter, placement));
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
            // ACC-006: the two-tap window lasts ×1, ×2 or ×3, as General says.
            new TwoStepConfirm(_time, () => _store.Current.Settings.TimeMultiplier),
            _time,
            action => _ = _ui.BeginInvoke(action),
            ActiveAppProfile,
            _openApps.ListAsync,
            () => _lastApp,
            DictateAsync,
            TryNowAsync,
            () => _viewModel?.Select(ControlCenterSection.Templates),
            System,
            _templates?.Services(Notify, () => _window),
            About?.Invoke(DictateAsync, Notify),
            OpenWelcome,
            Notify,
            InstalledAppsReader.ListAsync
        );
        _viewModel = new ControlCenterViewModel(services, () => _ = CloseAsync());
        if (_viewModel.System is { } system)
        {
            system.Noticed += (_, e) => Notify(e.Notice);
        }

        _viewModel.General.Noticed += (_, e) => Notify(e.Notice);
        _viewModel.TouchPrecision.Noticed += (_, e) => Notify(e.Notice);

        _window = new ControlCenterWindow(_viewModel, _theme);
        _window.CloseRequested += (_, _) => _ = CloseAsync();
        if (_panelNotice is not null)
        {
            // CCM-003: a notice the panel already shows is in the status bar from the first frame.
            ShowPanelNotice();
        }

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
        // PRB-004: the fixed notice [switching] shows in the panel while the Control Center is hidden.
        PanelNotice?.Invoke(L.Switching(app: target.Name));
        TryNowOutcome outcome;
        ForegroundLease? lease;
        try
        {
            (outcome, lease) = await _tryNow
                .RunAsync(
                    shortcut,
                    origin?.Id,
                    (origin ?? library.General).Injection,
                    target,
                    this,
                    cancellationToken
                )
                .ConfigureAwait(true);
        }
        finally
        {
            PanelNotice?.Invoke(null);
        }

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

        _ownNotice = true;
        _viewModel.ShowNotice(notice);
        _noticeTimer?.Dispose();
        _noticeTimer = _time.CreateTimer(
            static state =>
            {
                var composer = (ControlCenterComposer)state!;
                _ = composer._ui.BeginInvoke(composer.EndOwnNotice);
            },
            this,
            // ACC-006: the notices stay ×1, ×2 or ×3 as long, as General says.
            InteractionTime.Scale(
                notice.CanUndo
                    ? Timings.Notices.UndoNoticeDuration
                    : Timings.Notices.NoticeDuration,
                _store.Current.Settings.TimeMultiplier
            ),
            Timeout.InfiniteTimeSpan
        );
    }

    /// <summary>The notice of the Control Center ended: the bar goes back to the panel's notice, or rests.</summary>
    private void EndOwnNotice()
    {
        if (_ownNotice)
        {
            ShowPanelNotice();
        }
    }

    /// <summary>
    /// Paints the notice of the panel in the status bar, or [saved] when the panel rests (CCM-003). The queue of the
    /// panel times it (AVI-002), so no timer runs here.
    /// </summary>
    private void ShowPanelNotice()
    {
        _ownNotice = false;
        _noticeTimer?.Dispose();
        _noticeTimer = null;
        if (_panelNotice is { } notice)
        {
            _viewModel?.ShowNotice(
                new WorkspaceNotice(
                    notice.Text,
                    notice.Icon.Name,
                    notice.CanUndo,
                    notice.Tone == NoticeTone.Warning
                )
            );
        }
        else
        {
            _viewModel?.ClearNotice();
        }
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
