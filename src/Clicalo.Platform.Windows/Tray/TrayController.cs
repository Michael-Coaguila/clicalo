using Clicalo.Application.Foreground;
using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Messages;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>
/// The tray of M2 (blueprint §8.1, BUR-003): the icon and its menu with «Mostrar u ocultar», «Soltar todo» and
/// «Salir». A click on the icon asks to show or hide the panel. The menu opens inside a <c>TrayMenu</c> lease on
/// <see cref="TrayMenuHost"/> (origin <see cref="LeaseOrigin.Tray"/>: the click gave Clícalo the foreground right),
/// and the foreground goes back to the app that had it BEFORE the chosen command runs, so the key releases of «Soltar
/// todo» reach that app (SEG-003) and nothing stays in Clícalo (REG-01).
/// </summary>
/// <remarks>
/// «Soltar todo» posts <see cref="EngineEvent.ReleaseAll"/> itself; showing, hiding and exiting belong to the Surfaces
/// role and the lifetime of the app, so they are raised as events, on the thread that ran the menu (never the UI
/// thread): the composition marshals them. Texts come from <c>data/i18n</c> in the current language.
/// </remarks>
public sealed class TrayController : IDisposable
{
    private readonly TrayIcon _icon;
    private readonly TrayMenuHost _menu;
    private readonly IForegroundOrchestrator _foreground;
    private readonly IEngineInbox _engine;
    private readonly ILocalizationContext _localization;
    private volatile bool _panelVisible = true;
    private volatile bool _anythingHeld;
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
    }

    /// <summary>A click on the icon, or «Mostrar u ocultar» in the menu (BUR-003).</summary>
    public event EventHandler? ShowHideRequested;

    /// <summary>«Salir» in the menu.</summary>
    public event EventHandler? ExitRequested;

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
    public Task UpdateStateAsync(bool panelVisible, bool anythingHeld)
    {
        var tooltipChanged = _panelVisible != panelVisible;
        _panelVisible = panelVisible;
        _anythingHeld = anythingHeld;
        return tooltipChanged && _icon.IsShown
            ? _icon.SetTooltipAsync(Tooltip())
            : Task.CompletedTask;
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
            new(
                (int)TrayCommand.ShowHide,
                localizer.Format(_panelVisible ? L.HidePanel : L.Restore)
            ),
            new(
                (int)TrayCommand.ReleaseAll,
                localizer.Format(L.ReleaseAll),
                IsEnabled: _anythingHeld
            ),
            new((int)TrayCommand.Exit, localizer.Format(L.ExitApp)),
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
        _menu.DismissMenu();
        _icon.Dispose();
        _menu.Dispose();
    }

    private string Tooltip() =>
        _localization.Current.Format(_panelVisible ? L.AppName : L.TrayHidden);

    private void Run(TrayCommand? command)
    {
        switch (command)
        {
            case TrayCommand.ShowHide:
                ShowHideRequested?.Invoke(this, EventArgs.Empty);
                break;
            case TrayCommand.ReleaseAll:
                _ = _engine.Post(new EngineEvent.ReleaseAll(ReleaseReason.User));
                break;
            case TrayCommand.Exit:
                ExitRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void OnIconInvoked(object? sender, TrayIconEventArgs click) =>
        ShowHideRequested?.Invoke(this, EventArgs.Empty);

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
