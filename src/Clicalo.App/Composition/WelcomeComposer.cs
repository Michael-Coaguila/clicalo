using System.Windows;
using System.Windows.Threading;
using Clicalo.Application.Foreground;
using Clicalo.Application.Localization;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases.Welcome;
using Clicalo.Domain.Document;
using Clicalo.Domain.Templates;
using Clicalo.Presentation.Welcome;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Workspace.Welcome;

namespace Clicalo.App.Composition;

/// <summary>
/// Opens the welcome (docs/06, BIE-001) on the UI thread, in the Workspace role: automatically while the document has
/// not finished it (a first start, or one where it was left open), and again from General › Ver la bienvenida otra
/// vez. The window comes to the front through a <see cref="LeaseKind.ControlCenter"/> lease and is destroyed when the
/// session ends; Alt+F4 is [Omitir]. <see cref="Ended"/> tells the composition root to show the panel unminimized
/// and, after [Empezar], the notice [welcome] (BIE-009).
/// </summary>
internal sealed class WelcomeComposer : IDisposable
{
    private readonly DocumentStore _store;
    private readonly ILocalizationContext _localization;
    private readonly IForegroundOrchestrator _foreground;
    private readonly ThemeService _theme;
    private readonly Dispatcher _ui;
    private readonly Func<StarterContent?> _content;
    private readonly Func<string>? _detectedLayout;
    private readonly WelcomeFreshStart? _fresh;
    private readonly TimeProvider _time;
    private WelcomeWindow? _window;
    private WelcomeViewModel? _viewModel;
    private ForegroundLease? _lease;

    /// <summary>Creates the composer; nothing is shown until <see cref="OpenAsync"/>.</summary>
    /// <param name="store">The document.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="foreground">The owner of foreground changes.</param>
    /// <param name="theme">The theme service of the UI thread.</param>
    /// <param name="ui">The UI dispatcher.</param>
    /// <param name="content">The starter kit read at start (<c>RuntimeCatalogs.Content</c>).</param>
    /// <param name="detectedLayout">The layout of the keyboard in use, for step 2 (BIE-006); null assumes the default.</param>
    /// <param name="fresh">How «Empezar de cero» builds the empty document (P6); null never offers it.</param>
    /// <param name="time">The clock of the armed «Empezar de cero».</param>
    public WelcomeComposer(
        DocumentStore store,
        ILocalizationContext localization,
        IForegroundOrchestrator foreground,
        ThemeService theme,
        Dispatcher ui,
        Func<StarterContent?> content,
        Func<string>? detectedLayout = null,
        WelcomeFreshStart? fresh = null,
        TimeProvider? time = null
    )
    {
        _store = store;
        _localization = localization;
        _foreground = foreground;
        _theme = theme;
        _ui = ui;
        _content = content;
        _detectedLayout = detectedLayout;
        _fresh = fresh;
        _time = time ?? TimeProvider.System;
        localization.LanguageChanged += (_, _) => _ = _ui.BeginInvoke(() => _viewModel?.Refresh());
    }

    /// <summary>Raised on the UI thread when the welcome ends.</summary>
    public event EventHandler<WelcomeEndedEventArgs>? Ended;

    /// <summary>Raised on the UI thread when <see cref="IsOpen"/> changes (DimExceptions.WelcomeOpen, SEG-002).</summary>
    public event EventHandler? StateChanged;

    /// <summary>Whether the welcome is open: the panel does not dim meanwhile (DimExceptions.WelcomeOpen).</summary>
    public bool IsOpen => _window is not null;

    /// <summary>Whether the start must open the welcome: the document has not finished it (BIE-001).</summary>
    /// <param name="document">The document of the start.</param>
    public static bool IsPending(UserDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return !document.Onboarding.Completed;
    }

    /// <summary>Opens the welcome on step 0; nothing if it is already open.</summary>
    /// <param name="repeat">Opened from General › Ver la bienvenida otra vez (BIE-010), not by a first start.</param>
    /// <param name="origin">What asked for it, for the foreground ladder.</param>
    /// <param name="reinstall">
    /// The first start of a new installation that found data from before (P6): step 0 also asks whether to keep the
    /// data, the default, or start from scratch.
    /// </param>
    public async Task OpenAsync(bool repeat, LeaseOrigin origin, bool reinstall = false)
    {
        _ui.VerifyAccess();
        if (_window is not null)
        {
            return;
        }

        var session = new WelcomeSession(_store, _content(), repeat, reinstall ? _fresh : null);
        var viewModel = new WelcomeViewModel(
            session,
            _localization,
            _detectedLayout,
            _time,
            work => _ = _ui.BeginInvoke(work)
        );
        var window = new WelcomeWindow(viewModel, _theme);
        _viewModel = viewModel;
        _window = window;
        session.Ended += (_, e) => _ = _ui.BeginInvoke(() => _ = EndAsync(e));
        window.CloseRequested += (_, _) => session.Skip();
        window.Show();
        window.Place(SystemParameters.WorkArea);
        StateChanged?.Invoke(this, EventArgs.Empty);
        var result = await _foreground
            .AcquireAsync(
                new LeaseRequest(LeaseKind.ControlCenter, window.Token, origin, null),
                CancellationToken.None
            )
            .ConfigureAwait(true);
        if (result is not LeaseResult.Granted granted)
        {
            return;
        }

        if (_window == window)
        {
            _lease = granted.Lease;
            return;
        }

        // The welcome ended while the lease was pending: give the foreground back at once.
        _ = await granted.Lease.RestoreAsync(CancellationToken.None).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _window?.Destroy();
        _window = null;
        _viewModel = null;
    }

    private async Task EndAsync(WelcomeEndedEventArgs e)
    {
        _window?.Destroy();
        _window = null;
        _viewModel = null;
        StateChanged?.Invoke(this, EventArgs.Empty);
        var lease = _lease;
        _lease = null;
        if (lease is not null)
        {
            _ = await lease.RestoreAsync(CancellationToken.None).ConfigureAwait(true);
        }

        Ended?.Invoke(this, e);
    }
}
