using System.Collections.ObjectModel;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Foreground;
using Clicalo.Application.Localization;
using Clicalo.Application.Ports;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Search;
using Clicalo.Domain.Touch;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel.Search;

/// <summary>
/// The search of the panel (BUS-001 to BUS-005, docs/04 §4): the 44 px field under the header with 🎤 «Dictar» next to
/// it, and the results that replace the grid while it has text. It keeps no product rule: what matches is
/// <see cref="ShortcutSearch"/>, and taking and giving back the keyboard is <see cref="PanelSearch"/>. It lives on the
/// UI thread of the Surfaces role, like the panel.
/// </summary>
/// <remarks>
/// The view focuses the field on <see cref="FocusFieldRequested"/> (only the view can) and reports every gesture: the
/// field tapped, 🎤, a result tapped or invoked, Esc. The panel calls <see cref="OnAppChanged"/> on every app switch,
/// which closes and empties the search (BUS-001, PER-003).
/// </remarks>
public sealed class SearchViewModel : ObservableObject
{
    private readonly PanelSearch _search;
    private readonly ILocalizationContext _localization;
    private readonly Func<WindowToken> _panel;
    private readonly Func<Shortcut, string?> _combination;
    private readonly Action<Message> _notify;
    private ShortcutLibrary? _library;
    private string _query = string.Empty;
    private bool _isOpen;
    private bool _showNoResults;
    private string _placeholder = string.Empty;
    private string _searchName = string.Empty;
    private string _dictateName = string.Empty;
    private string _noResultsText = string.Empty;
    private readonly Func<InjectionMode?>? _appMode;
    private int _generation;

    /// <summary>Creates the search.</summary>
    /// <param name="search">The keyboard lease and the runs of results.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="panel">The panel window, once it exists (<c>NonActivatingWindow.SurfaceWindow</c>).</param>
    /// <param name="combination">
    /// The combination a tile shows for a shortcut, with the key names of the interface (null or empty for none): the
    /// search finds what the user sees (BUS-004).
    /// </param>
    /// <param name="notify">Shows a notice in the notice bar (assertive: the search only reports failures).</param>
    /// <param name="appMode">
    /// The mode of the profile of the app in front, which the results of General and Always visible inherit (D24);
    /// <see langword="null"/> uses the mode of General.
    /// </param>
    public SearchViewModel(
        PanelSearch search,
        ILocalizationContext localization,
        Func<WindowToken> panel,
        Func<Shortcut, string?> combination,
        Action<Message> notify,
        Func<InjectionMode?>? appMode = null
    )
    {
        _appMode = appMode;
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(combination);
        ArgumentNullException.ThrowIfNull(notify);
        _search = search;
        _localization = localization;
        _panel = panel;
        _combination = combination;
        _notify = notify;
        Relocalize();
    }

    /// <summary>Raised when the field must take the keyboard focus (the lease was granted, BUS-002 b).</summary>
    public event EventHandler? FocusFieldRequested;

    /// <summary>Whether the field is shown (🔍 has background cardHi while it is, CAB-001).</summary>
    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (SetProperty(ref _isOpen, value))
            {
                OnPropertyChanged(nameof(IsSearching));
            }
        }
    }

    /// <summary>
    /// Whether a search with text is shown: the results replace the grid, the header says «Buscar» and the layers of
    /// PAN-008 hide. With the field open and empty the panel looks as without searching (BUS-005).
    /// </summary>
    public bool IsSearching => IsOpen && ShortcutSearch.IsActive(_query);

    /// <summary>The text of the field. The view writes it on every change (typing, dictation, UI Automation Value).</summary>
    public string Query
    {
        get => _query;
        set
        {
            value ??= string.Empty;
            if (!SetProperty(ref _query, value))
            {
                return;
            }

            _search.KeepAlive();
            OnPropertyChanged(nameof(IsSearching));
            Refresh();
        }
    }

    /// <summary>The results in display order (Always visible first, then each profile in order).</summary>
    public ObservableCollection<SearchResultViewModel> Results { get; } = [];

    /// <summary>Whether [noResults] is shown in place of the grid.</summary>
    public bool ShowNoResults
    {
        get => _showNoResults;
        private set => SetProperty(ref _showNoResults, value);
    }

    /// <summary>The placeholder of the field, also its accessible name ([search]).</summary>
    public string Placeholder
    {
        get => _placeholder;
        private set => SetProperty(ref _placeholder, value);
    }

    /// <summary>«Buscar» ([searchA]): the name of 🔍 and the header title while searching with text (CAB-002).</summary>
    public string SearchName
    {
        get => _searchName;
        private set => SetProperty(ref _searchName, value);
    }

    /// <summary>The name of 🎤 next to the field (BUS-003).</summary>
    public string DictateName
    {
        get => _dictateName;
        private set => SetProperty(ref _dictateName, value);
    }

    /// <summary>[noResults].</summary>
    public string NoResultsText
    {
        get => _noResultsText;
        private set => SetProperty(ref _noResultsText, value);
    }

    /// <summary>Takes the current shortcuts (after every document change) and searches them again.</summary>
    /// <param name="library">The library of the document.</param>
    public void ApplyLibrary(ShortcutLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (ReferenceEquals(library, _library))
        {
            return;
        }

        _library = library;
        Refresh();
    }

    /// <summary>🔍: opens the search when closed and closes it when open (BUS-001).</summary>
    /// <param name="origin">Touch, or UI Automation when invoked by voice or a switch.</param>
    public Task ToggleAsync(SearchTrigger origin) => IsOpen ? CloseAsync() : OpenAsync(origin);

    /// <summary>
    /// Opens the search with an empty field and lets it take the keyboard (BUS-001, BUS-002 a and b). If Windows refuses,
    /// the field stays open without the keyboard and the panel says why; dictation by UI Automation still fills it.
    /// </summary>
    /// <param name="origin">What opened it; it selects the rights ladder.</param>
    public async Task OpenAsync(SearchTrigger origin)
    {
        if (!IsOpen)
        {
            _generation++;
            SetQuery(string.Empty);
            IsOpen = true;
        }

        await TakeKeyboardAsync(origin).ConfigureAwait(true);
    }

    /// <summary>
    /// Closes the search, empties it and gives the foreground back to the app, verified (BUS-001, BUS-002 c).
    /// </summary>
    public async Task CloseAsync()
    {
        if (!IsOpen)
        {
            return;
        }

        _generation++;
        IsOpen = false;
        SetQuery(string.Empty);
        _ = await _search.ReleaseKeyboardAsync(CancellationToken.None).ConfigureAwait(true);
    }

    /// <summary>
    /// The field was tapped: it takes the keyboard again if it had given it back (after a result ran or the idle
    /// timeout) and shows the touch keyboard (ACC-007: the only control of the panel that opens it).
    /// </summary>
    /// <param name="origin">Touch, or UI Automation.</param>
    public async Task FieldTappedAsync(SearchTrigger origin)
    {
        if (!IsOpen || !await TakeKeyboardAsync(origin).ConfigureAwait(true))
        {
            return;
        }

        _ = await _search
            .ShowTouchKeyboardAsync(_panel(), CancellationToken.None)
            .ConfigureAwait(true);
    }

    /// <summary>🎤 «Dictar» (BUS-003): the field takes the focus and Windows dictation starts (Win+H).</summary>
    /// <param name="origin">Touch, or UI Automation.</param>
    public async Task DictateAsync(SearchTrigger origin)
    {
        if (!IsOpen)
        {
            await OpenAsync(origin).ConfigureAwait(true);
        }
        else if (!await TakeKeyboardAsync(origin).ConfigureAwait(true))
        {
            return;
        }

        if (_search.HasKeyboard)
        {
            _ = await _search.DictateAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Esc in the field (PAN-010 step 2): it leaves the field, so the foreground goes back to the app; the search and its
    /// results stay.
    /// </summary>
    public async Task LeaveFieldAsync() =>
        _ = await _search.ReleaseKeyboardAsync(CancellationToken.None).ConfigureAwait(true);

    /// <summary>The app in front changed: the search closes and empties (BUS-001, PER-003 step 5).</summary>
    public void OnAppChanged() => _ = CloseAsync();

    /// <summary>Formats every text again in the current language (IDI-001) and the origins of the results.</summary>
    public void Relocalize()
    {
        var localizer = _localization.Current;
        Placeholder = localizer.Format(L.Search);
        SearchName = localizer.Format(L.SearchA);
        DictateName = localizer.Format(L.SearchDictate);
        NoResultsText = localizer.Format(L.NoResults);
        Refresh();
    }

    /// <summary>Runs a tapped result after giving the foreground back; says so when it could not (BUS-002 d).</summary>
    internal async Task RunTappedAsync(
        SearchResultViewModel result,
        uint contactId,
        PointerKind device,
        ContactSummary summary,
        DateTimeOffset at
    ) =>
        Report(
            await _search
                .RunTappedAsync(
                    result.Binding,
                    contactId,
                    device,
                    summary,
                    at,
                    CancellationToken.None
                )
                .ConfigureAwait(true)
        );

    /// <summary>Runs an invoked result after giving the foreground back.</summary>
    internal async Task RunInvokedAsync(SearchResultViewModel result) =>
        Report(
            await _search
                .RunInvokedAsync(result.Binding, CancellationToken.None)
                .ConfigureAwait(true)
        );

    private async Task<bool> TakeKeyboardAsync(SearchTrigger origin)
    {
        if (_search.HasKeyboard)
        {
            FocusFieldRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        var generation = _generation;
        var granted = await _search
            .TakeKeyboardAsync(_panel(), LeaseOriginOf(origin), CancellationToken.None)
            .ConfigureAwait(true);
        if (!granted)
        {
            _notify(L.SearchDenied);
            return false;
        }

        if (generation != _generation || !IsOpen)
        {
            // Closed while Windows was asked: give the keyboard straight back.
            _ = await _search.ReleaseKeyboardAsync(CancellationToken.None).ConfigureAwait(true);
            return false;
        }

        FocusFieldRequested?.Invoke(this, EventArgs.Empty);
        if (_search.KeyboardEnded is { } ended)
        {
            _ = FollowLeaseAsync(ended, generation);
        }

        return true;
    }

    /// <summary>The user switched apps while the field held the keyboard: the search closes (BUS-001).</summary>
    private async Task FollowLeaseAsync(Task<LeaseEndReason> ended, int generation)
    {
        var reason = await ended.ConfigureAwait(true);
        if (reason == LeaseEndReason.ForegroundChanged && generation == _generation)
        {
            await CloseAsync().ConfigureAwait(true);
        }
    }

    private static LeaseOrigin LeaseOriginOf(SearchTrigger trigger) =>
        trigger == SearchTrigger.Automation ? LeaseOrigin.UiaInvoke : LeaseOrigin.Touch;

    private void Report(SearchRunOutcome outcome)
    {
        if (outcome == SearchRunOutcome.NotSent)
        {
            _notify(L.SearchNoReturn);
        }
    }

    private void SetQuery(string query)
    {
        if (SetProperty(ref _query, query, nameof(Query)))
        {
            OnPropertyChanged(nameof(IsSearching));
            Refresh();
        }
    }

    private void Refresh()
    {
        Results.Clear();
        if (_library is not { } library || !IsSearching)
        {
            ShowNoResults = false;
            return;
        }

        var localizer = _localization.Current;
        var language = new LangCode(localizer.Locale.Code);
        var always = localizer.Format(L.Always);
        var appMode = _appMode?.Invoke();
        foreach (var hit in ShortcutSearch.Find(library, _query, _combination))
        {
            var profile =
                hit.Profile is { } id && library.TryGetProfile(id, out var found) ? found : null;
            Results.Add(
                new SearchResultViewModel(
                    this,
                    new TileBinding(
                        hit.Shortcut,
                        hit.Profile,
                        PanelProjector.InheritedMode(library, profile, appMode)
                    ),
                    hit.Shortcut.Name.Get(language, LangCode.Es),
                    profile?.Name.Get(language, LangCode.Es) ?? always
                )
            );
        }

        ShowNoResults = Results.Count == 0;
    }
}
