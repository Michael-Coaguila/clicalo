using Clicalo.Application.Foreground;
using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Messages;
using Clicalo.Platform.Windows.Hotkeys;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>
/// The tray (blueprint §8.1, BUR-003, BUR-004) and the optional global shortcut (BUR-005): the icon and its menu with
/// «Mostrar u ocultar», «Centro de control», «Soltar todo», «Pausar» and «Salir». A click on the icon, or the global
/// shortcut, asks to show or hide the panel. The menu opens inside a <c>TrayMenu</c> lease on
/// <see cref="TrayMenuHost"/> (origin <see cref="LeaseOrigin.Tray"/>: the click gave Clícalo the foreground right),
/// and the foreground goes back to the app that had it BEFORE the chosen command runs, so the key releases of «Soltar
/// todo» reach that app (SEG-003) and nothing stays in Clícalo (REG-01).
/// </summary>
/// <remarks>
/// «Soltar todo» posts <see cref="EngineEvent.ReleaseAll"/> itself and, so it works with a hung engine too, raises
/// <see cref="ReleasePressedRequested"/>, which the composition answers by releasing whatever Windows reports down
/// (ADR-0023); showing, hiding and exiting belong to the Surfaces
/// role and the lifetime of the app, so they are raised as events, on the thread that ran the menu (never the UI
/// thread): the composition marshals them. Texts come from <c>data/i18n</c> in the current language.
/// <para>
/// «Pausar» (BUR-004) posts <see cref="EngineEvent.Terminal"/> with <see cref="TerminalReason.Pause"/>, which releases
/// everything and blocks every send; «Reanudar», a click on the icon, «Mostrar panel» and the global shortcut post
/// <see cref="EngineEvent.SetPaused"/> off. The panel hides and comes back by following the paused state of the
/// engine, and the text of the icon says «en pausa» meanwhile.
/// </para>
/// <para>
/// The global shortcut (BUR-005, user decision D10) is off until <see cref="SetHotkeyAsync"/> gives it a combination
/// of the closed list. <c>WM_HOTKEY</c> activates no window, and showing the panel is passive (REG-01).
/// </para>
/// </remarks>
public sealed class TrayController : IDisposable
{
    private readonly TrayIcon _icon;
    private readonly TrayMenuHost _menu;
    private readonly IForegroundOrchestrator _foreground;
    private readonly IEngineInbox _engine;
    private readonly ILocalizationContext _localization;
    private readonly GlobalPanelHotkey _hotkey;
    private volatile bool _panelVisible = true;
    private volatile bool _anythingHeld;
    private volatile bool _paused;
    private int _menuBusy;
    private int _disposed;

    /// <summary>Creates the tray; <see cref="StartAsync"/> shows it.</summary>
    /// <param name="icon">The notification area icon.</param>
    /// <param name="menu">The hidden host of the menu.</param>
    /// <param name="foreground">The only owner of foreground changes (§3.6).</param>
    /// <param name="engine">The engine mailbox, for «Soltar todo».</param>
    /// <param name="localization">The interface language.</param>
    public TrayController(
        TrayIcon icon,
        TrayMenuHost menu,
        IForegroundOrchestrator foreground,
        IEngineInbox engine,
        ILocalizationContext localization
    )
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(foreground);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(localization);
        _icon = icon;
        _menu = menu;
        _foreground = foreground;
        _engine = engine;
        _localization = localization;
        _hotkey = new GlobalPanelHotkey(icon.Thread);
        _hotkey.Pressed += OnHotkeyPressed;
    }

    /// <summary>
    /// A click on the icon, «Mostrar u ocultar» in the menu (BUR-003) or the global shortcut (BUR-005), while not
    /// paused.
    /// </summary>
    public event EventHandler? ShowHideRequested;

    /// <summary>«Pausar» or «Reanudar» was used (BUR-004); <see cref="IsPaused"/> has the new state.</summary>
    public event EventHandler? PauseChanged;

    /// <summary>Whether Clícalo is paused from the tray (BUR-004).</summary>
    public bool IsPaused => _paused;

    /// <summary>What the icon and the menu show now.</summary>
    public TrayState State => new(_panelVisible, _anythingHeld, _paused);

    /// <summary>
    /// «Soltar todo» in the menu, after <see cref="EngineEvent.ReleaseAll"/> was posted: the release that does not need
    /// the engine (ADR-0023).
    /// </summary>
    public event EventHandler? ReleasePressedRequested;

    /// <summary>«Salir» in the menu.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>«Centro de control» in the menu, once the foreground is back (blueprint §8.1).</summary>
    public event EventHandler? ControlCenterRequested;

    /// <summary>A menu lease was denied; the composition announces it.</summary>
    public event EventHandler? MenuDenied;

    /// <summary>Whether a menu is being shown.</summary>
    public bool IsMenuOpen => Volatile.Read(ref _menuBusy) != 0;

    /// <summary>Starts the menu host and shows the icon with its localized text.</summary>
    public async Task StartAsync()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _menu.StartAsync().ConfigureAwait(false);
        _icon.Invoked += OnIconInvoked;
        _icon.MenuRequested += OnMenuRequested;
        await _icon.ShowAsync(Tooltip()).ConfigureAwait(false);
    }

    /// <summary>
    /// What the menu and the icon show: the panel's presence (BUR-003: the hidden state is also in the icon's
    /// accessible text) and whether «Soltar todo» has anything to release.
    /// </summary>
    /// <param name="panelVisible">Whether the panel is on screen.</param>
    /// <param name="anythingHeld">Whether the engine holds anything.</param>
    /// <remarks>
    /// A panel that comes back while Clícalo is paused resumes it (BUR-004): a second start of Clícalo shows the panel
    /// without going through the tray (SIS-003), and a panel on screen that sends nothing and says nothing would be a
    /// dead end.
    /// </remarks>
    public Task UpdateStateAsync(bool panelVisible, bool anythingHeld)
    {
        var before = TrayMenuModel.Tooltip(State);
        var cameBack = panelVisible && !_panelVisible;
        _panelVisible = panelVisible;
        _anythingHeld = anythingHeld;
        if (cameBack && _paused)
        {
            _paused = false;
            _ = _engine.Post(new EngineEvent.SetPaused(false));
            PauseChanged?.Invoke(this, EventArgs.Empty);
        }

        return RefreshTooltipAsync(before);
    }

    /// <summary>
    /// Turns the global shortcut on with <paramref name="chord"/>, or off with <see langword="null"/> (BUR-005).
    /// Completes with <see langword="false"/> when the combination cannot be registered: another program owns it, or
    /// it is not one a global shortcut can use; the shortcut is then off.
    /// </summary>
    /// <param name="chord">A combination of <c>GlobalHotkeys</c>, or <see langword="null"/>.</param>
    public async Task<bool> SetHotkeyAsync(KeyChord? chord)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (chord is null)
        {
            return await _hotkey.ApplyAsync(null).ConfigureAwait(false);
        }

        if (HotkeyChord.From(chord) is not { } registration)
        {
            _ = await _hotkey.ApplyAsync(null).ConfigureAwait(false);
            return false;
        }

        return await _hotkey.ApplyAsync(registration).ConfigureAwait(false);
    }

    /// <summary>
    /// What a click on the icon and the global shortcut do: resume when paused, otherwise ask to show or hide the
    /// panel.
    /// </summary>
    public void TogglePanel()
    {
        if (_paused)
        {
            SetPaused(false);
        }
        else
        {
            ShowHideRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Formats the icon's text again in the current language (IDI-001).</summary>
    public Task RelocalizeAsync() =>
        _icon.IsShown ? _icon.SetTooltipAsync(Tooltip()) : Task.CompletedTask;

    /// <summary>The menu entries in the current state and language.</summary>
    public IReadOnlyList<TrayMenuItem> MenuItems()
    {
        var localizer = _localization.Current;
        return
        [
            .. TrayMenuModel
                .Entries(State)
                .Select(entry => new TrayMenuItem(
                    (int)entry.Command,
                    localizer.Format(entry.Text),
                    entry.IsEnabled
                )),
        ];
    }

    /// <summary>
    /// Opens the menu at <paramref name="anchor"/> under a <c>TrayMenu</c> lease, gives the foreground back and runs
    /// the chosen command. Completes with the command, or <see langword="null"/> when the menu was dismissed, denied
    /// or already open.
    /// </summary>
    /// <param name="anchor">Where the icon was used, in physical screen pixels.</param>
    /// <param name="cancellationToken">Abandons the lease wait (the app is exiting).</param>
    public async Task<TrayCommand?> OpenMenuAsync(
        PhysicalPoint anchor,
        CancellationToken cancellationToken
    )
    {
        if (Interlocked.Exchange(ref _menuBusy, 1) != 0)
        {
            return null;
        }

        try
        {
            var result = await _foreground
                .AcquireAsync(
                    new LeaseRequest(LeaseKind.TrayMenu, _menu.Window, LeaseOrigin.Tray, null),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (result is not LeaseResult.Granted granted)
            {
                MenuDenied?.Invoke(this, EventArgs.Empty);
                return null;
            }

            int? chosen;
            await using (granted.Lease.ConfigureAwait(false))
            {
                chosen = await _menu.ShowMenuAsync(MenuItems(), anchor).ConfigureAwait(false);
                _ = await granted.Lease.RestoreAsync(cancellationToken).ConfigureAwait(false);
            }

            var command =
                chosen is { } id && Enum.IsDefined((TrayCommand)id) ? (TrayCommand?)id : null;
            Run(command);
            return command;
        }
        finally
        {
            Volatile.Write(ref _menuBusy, 0);
        }
    }

    /// <summary>Removes the icon and closes an open menu.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _icon.Invoked -= OnIconInvoked;
        _icon.MenuRequested -= OnMenuRequested;
        _hotkey.Pressed -= OnHotkeyPressed;
        _hotkey.Dispose();
        _menu.DismissMenu();
        _icon.Dispose();
        _menu.Dispose();
    }

    private string Tooltip() => _localization.Current.Format(TrayMenuModel.Tooltip(State));

    private Task RefreshTooltipAsync(Message before) =>
        before != TrayMenuModel.Tooltip(State) && _icon.IsShown
            ? _icon.SetTooltipAsync(Tooltip())
            : Task.CompletedTask;

    /// <summary>
    /// Pauses or resumes (BUR-004). Pausing is a terminal event: the engine releases everything and sends nothing
    /// until it is told to resume.
    /// </summary>
    private void SetPaused(bool paused)
    {
        if (_paused == paused)
        {
            return;
        }

        var before = TrayMenuModel.Tooltip(State);
        _paused = paused;
        _ = _engine.Post(
            paused
                ? new EngineEvent.Terminal(TerminalReason.Pause)
                : new EngineEvent.SetPaused(false)
        );
        _ = RefreshTooltipAsync(before);
        PauseChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Runs a command of the menu, once the foreground is back with the app that had it.</summary>
    /// <param name="command">The command chosen, or <see langword="null"/> when the menu was dismissed.</param>
    internal void Run(TrayCommand? command)
    {
        switch (command)
        {
            case TrayCommand.ShowHide:
                TogglePanel();
                break;
            case TrayCommand.Pause:
                SetPaused(!_paused);
                break;
            case TrayCommand.ReleaseAll:
                _ = _engine.Post(new EngineEvent.ReleaseAll(ReleaseReason.User));
                ReleasePressedRequested?.Invoke(this, EventArgs.Empty);
                break;
            case TrayCommand.Exit:
                ExitRequested?.Invoke(this, EventArgs.Empty);
                break;
            case TrayCommand.ControlCenter:
                ControlCenterRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void OnIconInvoked(object? sender, TrayIconEventArgs click) => TogglePanel();

    private void OnHotkeyPressed(object? sender, EventArgs pressed) => TogglePanel();

    private void OnMenuRequested(object? sender, TrayIconEventArgs request) =>
        _ = OpenMenuSafelyAsync(request.Position);

    private async Task OpenMenuSafelyAsync(PhysicalPoint anchor)
    {
        try
        {
            _ = await OpenMenuAsync(anchor, CancellationToken.None).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The app is exiting while the menu opened.
        }
    }
}
