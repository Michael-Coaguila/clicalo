using System.Collections.ObjectModel;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Application.Localization;
using Clicalo.Application.Session;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Messages;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.StickyModifiers;
using Clicalo.Domain.Touch;
using Clicalo.Domain.VoiceNumbering;
using Clicalo.Presentation.Panel.Search;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The panel (blueprint §8.2): the tiles of the page in view, the Always visible row, the sticky modifiers, the profile
/// selector and its grid, the pager, the notice bar, the empty profile card, the administrator notice and the panic
/// strip, all applied from immutable inputs (<see cref="PanelModel"/>, <see cref="PanelLayoutSettings"/>,
/// <see cref="PanelBodyContext"/>, <see cref="EngineSnapshot"/> and the measured space of the grid) on the UI thread of
/// the Surfaces role. It keeps no rule of its own: rows, pages, the hiding for lack of space, the visibility of every
/// part and the voice numbers come from <c>Clicalo.Domain.PanelLayout</c> and <c>Clicalo.Domain.VoiceNumbering</c>;
/// tiles forward intentions to the <see cref="PanelInteractionController"/> and the rest to
/// <see cref="IPanelBodyIntents"/>. Every visible text is formatted from <c>data/i18n</c> when applied, so a language
/// change only needs <see cref="Relocalize"/> (IDI-001).
/// </summary>
public sealed class PanelViewModel : ObservableObject
{
    /// <summary>The icon of the notice bar at rest (AVI-001).</summary>
    private const string RestIcon = "info";

    /// <summary>The page context of Frequents and of the search (they are not profiles).</summary>
    private const string FrequentsView = "frequents";

    private const string SearchView = "search";

    private readonly PanelInteractionController _controller;
    private readonly ILocalizationContext _localization;
    private readonly Func<ShortcutId, string?> _nameOfShortcut;
    private readonly Dictionary<ShortcutId, TileViewModel> _listById = [];
    private readonly Dictionary<ShortcutId, TileViewModel> _stripById = [];
    private List<TileViewModel> _list = [];
    private List<TileViewModel> _strip = [];
    private List<TileViewModel> _results = [];
    private string _noResultsText = string.Empty;
    private bool _showsNoResults;
    private PanelModel _model = PanelModel.Empty;
    private EngineSnapshot _engine = EngineSnapshot.Empty;
    private PanelLayoutSettings _layout = PanelLayoutSettings.Default;
    private PanelBodyContext _context = PanelBodyContext.Idle;
    private TouchSettings _touch;
    private double? _gridSpace;
    private int _page;
    private PageContext? _pageContext;
    private StripWindow _stripWindow;
    private bool _cramped;
    private GridShape _shape;
    private BodyLayers _layers = BodyLayerRules.Evaluate(
        new BodyLayerInputs(
            PanelLayoutSettings.Default,
            false,
            false,
            false,
            false,
            0,
            1,
            false,
            false,
            false,
            false
        )
    );
    private string _accessibleName = string.Empty;
    private bool _isVisible;

    /// <summary>Creates the panel with no destination for the intentions of its body (M2 composition).</summary>
    /// <param name="controller">Where the intentions of the tiles go.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="touch">The touch filter of the surfaces (TAC-001, TAC-002).</param>
    /// <param name="nameOfShortcut">
    /// The name of a shortcut that is not on the panel (the panic strip names every held shortcut, SEG-002), in the
    /// interface language, or <see langword="null"/> when it no longer exists.
    /// </param>
    public PanelViewModel(
        PanelInteractionController controller,
        ILocalizationContext localization,
        TouchSettings touch,
        Func<ShortcutId, string?> nameOfShortcut
    )
        : this(controller, localization, touch, nameOfShortcut, NoBodyIntents.Instance, null) { }

    /// <summary>Creates the panel.</summary>
    /// <param name="controller">Where the intentions of the tiles go.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="touch">The touch filter of the surfaces (TAC-001, TAC-002).</param>
    /// <param name="nameOfShortcut">The name of a shortcut that is not on the panel, or <see langword="null"/>.</param>
    /// <param name="intents">Where the intentions of the body go (selector, profile grid, notices, empty card…).</param>
    /// <param name="keyLabelOf">
    /// The label of a sticky modifier as <c>data/catalogs/keys.json</c> shows it; <see langword="null"/> uses the names
    /// of <see cref="ModifierKind"/> (Ctrl, Alt, Shift, Win, the same in every language today).
    /// </param>
    public PanelViewModel(
        PanelInteractionController controller,
        ILocalizationContext localization,
        TouchSettings touch,
        Func<ShortcutId, string?> nameOfShortcut,
        IPanelBodyIntents intents,
        Func<ModifierKind, string>? keyLabelOf
    )
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(nameOfShortcut);
        ArgumentNullException.ThrowIfNull(intents);
        _controller = controller;
        _localization = localization;
        _touch = touch;
        _nameOfShortcut = nameOfShortcut;
        _shape = GridMetrics.Shape(_layout, null);
        Panic = new PanicStripViewModel(controller);
        Pager = new PagerViewModel(GoToPage);
        Strip = new AlwaysVisibleRowViewModel(NextStripPage);
        Sticky = new StickyKeysRowViewModel(
            keyLabelOf ?? (static modifier => modifier.ToString()),
            intents.AdvanceSticky
        );
        Selector = new SelectorRowViewModel(intents);
        Picker = new PickerGridViewModel(intents);
        Notices = new NoticeBarViewModel(intents);
        Admin = new AdminNoticeViewModel(intents);
        Empty = new EmptyStateViewModel(intents);
        Relocalize();
    }

    /// <summary>
    /// The tiles of the page in view, in display order (CUA-004); a tile keeps its view model while its shortcut stays
    /// in the list.
    /// </summary>
    public ObservableCollection<TileViewModel> Tiles { get; } = [];

    /// <summary>The panic strip (SEG-002).</summary>
    public PanicStripViewModel Panic { get; }

    /// <summary>◀, the page dots and ▶ (CUA-004, CUA-005).</summary>
    public PagerViewModel Pager { get; }

    /// <summary>The Always visible row (FIJ-001 to FIJ-004).</summary>
    public AlwaysVisibleRowViewModel Strip { get; }

    /// <summary>The sticky modifiers row (FIJ-005).</summary>
    public StickyKeysRowViewModel Sticky { get; }

    /// <summary>The profile selector (SEL-001, SEL-002).</summary>
    public SelectorRowViewModel Selector { get; }

    /// <summary>The profile grid (SEL-003).</summary>
    public PickerGridViewModel Picker { get; }

    /// <summary>The notice bar (AVI-001, AVI-003, AVI-004).</summary>
    public NoticeBarViewModel Notices { get; }

    /// <summary>The administrator notice (EJE-013).</summary>
    public AdminNoticeViewModel Admin { get; }

    /// <summary>The empty profile card (CUA-010).</summary>
    public EmptyStateViewModel Empty { get; }

    /// <summary>The shape of the grid: columns, visible rows and tile height (CUA-001).</summary>
    public GridShape Shape
    {
        get => _shape;
        private set => SetProperty(ref _shape, value);
    }

    /// <summary>Which parts of the body show (PAN-007, PAN-008).</summary>
    public BodyLayers Layers
    {
        get => _layers;
        private set => SetProperty(ref _layers, value);
    }

    /// <summary>[noResults], shown in place of the grid while a search with text finds nothing (BUS-005).</summary>
    public string NoResultsText
    {
        get => _noResultsText;
        private set => SetProperty(ref _noResultsText, value);
    }

    /// <summary>Whether the search with text found nothing: <see cref="NoResultsText"/> replaces the grid.</summary>
    public bool ShowsNoResults
    {
        get => _showsNoResults;
        private set => SetProperty(ref _showsNoResults, value);
    }

    /// <summary>The layout settings in use.</summary>
    public PanelLayoutSettings Layout => _layout;

    /// <summary>The profile in view.</summary>
    public ProfileId Profile => _model.Profile;

    /// <summary>The panel's own name for UI Automation.</summary>
    public string AccessibleName
    {
        get => _accessibleName;
        private set => SetProperty(ref _accessibleName, value);
    }

    /// <summary>Whether the panel is on screen.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>The touch filter the surface's gesture recognizer uses.</summary>
    public TouchSettings Touch
    {
        get => _touch;
        private set => SetProperty(ref _touch, value);
    }

    /// <summary>The latest engine state applied.</summary>
    public EngineSnapshot Engine => _engine;

    /// <summary>
    /// Applies a projection: tiles whose shortcut stays keep their view model (and its UI Automation element), new
    /// ones are added and removed ones dropped, in the new order; then the page in view is composed again.
    /// </summary>
    /// <param name="model">The projection.</param>
    public void Apply(PanelModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _list = SyncById(_listById, model.Tiles);
        _strip = SyncById(_stripById, model.StripTiles);
        OnPropertyChanged(nameof(Profile));
        ApplyEngine(_engine);
    }

    /// <summary>
    /// Applies the results of the search (BUS-005): while <see cref="PanelBodyContext.SearchingWithText"/> they replace
    /// the list in view, paginated and numbered like it, each showing its origin under its name.
    /// </summary>
    /// <param name="results">The results, in order.</param>
    /// <param name="noResultsText">[noResults] in the interface language.</param>
    public void ApplySearch(IReadOnlyList<SearchResultViewModel> results, string noResultsText)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(noResultsText);
        NoResultsText = noResultsText;
        _results =
        [
            .. results.Select(result => new TileViewModel(
                new TileModel(
                    result.Id,
                    result.AccessibleName,
                    result.Behavior,
                    result.Binding,
                    result.Binding.Shortcut.Icon,
                    result.Binding.Shortcut.Category,
                    result.Origin,
                    result.Origin
                ),
                _controller,
                result
            )),
        ];
        ApplyEngine(_engine);
    }

    /// <summary>Applies the layout settings (size, view, columns, rows, text scale and the optional rows).</summary>
    /// <param name="layout">The settings.</param>
    public void ApplyLayout(PanelLayoutSettings layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _layout = layout;
        OnPropertyChanged(nameof(Layout));
        Recompose();
    }

    /// <summary>Applies what the session, the interaction and the foreground say (<see cref="PanelBodyContext"/>).</summary>
    /// <param name="context">The context.</param>
    public void ApplyContext(PanelBodyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        Recompose();
    }

    /// <summary>
    /// The space the surface <b>measured</b> for the grid: the height from the top of the grid area down to the bottom
    /// of the work area, less the bottom margin and what goes below the grid (CUA-001, CUA-002, CUA-003). Rows that
    /// fit and the hiding for lack of space are decided from it.
    /// </summary>
    /// <param name="gridSpacePx">The measured height, in device-independent pixels.</param>
    public void ApplyGridSpace(double gridSpacePx)
    {
        if (
            double.IsNaN(gridSpacePx)
            || (_gridSpace is { } known && Math.Abs(known - gridSpacePx) < 0.5)
        )
        {
            return;
        }

        _gridSpace = gridSpacePx;
        Recompose();
    }

    /// <summary>Applies the session: the presence of the panel.</summary>
    /// <param name="session">The session.</param>
    public void ApplySession(PanelSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        IsVisible = session.Presence == PanelPresence.Visible;
    }

    /// <summary>Applies the touch filter settings.</summary>
    /// <param name="touch">The touch filter.</param>
    public void ApplyTouch(TouchSettings touch) => Touch = touch;

    /// <summary>
    /// Applies what the engine holds: the panic strip appears while anything is held (SEG-002) and names every held
    /// shortcut; each tile shows whether it holds («Manteniendo») or is latched; the sticky modifiers show their level.
    /// </summary>
    /// <param name="snapshot">The engine snapshot.</param>
    public void ApplyEngine(EngineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _engine = snapshot;
        var localizer = _localization.Current;
        var held = new Dictionary<ShortcutId, PressedItem>();
        foreach (var item in snapshot.Held)
        {
            if (item.Shortcut is { } shortcut)
            {
                held.TryAdd(shortcut, item);
            }
        }

        foreach (var tile in _list.Concat(_strip).Concat(_results))
        {
            var isHeld = held.TryGetValue(tile.Id, out var item);
            tile.ApplyState(
                isHeld,
                isHeld ? localizer.Format(StateOf(item!)) : string.Empty,
                tile.SpokenKeys.Length > 0
                    ? tile.SpokenKeys
                    : localizer.Format(HelpOf(tile.Behavior)),
                // The Always visible row is too low for a type badge (docs/04 §7); its type stays in the help text.
                BadgeOf(tile.Behavior) is { } badge
                && !ReferenceEquals(_stripById.GetValueOrDefault(tile.Id), tile)
                    ? localizer.Format(badge)
                    : string.Empty
            );
        }

        var names = held
            .Keys.Select(NameOf)
            .OfType<string>()
            .Where(static name => name.Length > 0)
            .ToList();

        // SEG-002: what is held without a shortcut of this panel (a sticky modifier, a macro's keys) is still said,
        // never as an empty «Held: ».
        Panic.Apply(
            visible: !snapshot.Held.IsEmpty,
            localizer.Format(
                names.Count > 0 ? L.PanicMsg(keys: string.Join(", ", names)) : L.Holding
            ),
            localizer.Format(L.ReleaseAll)
        );
        Recompose();
    }

    /// <summary>
    /// The contact of a hold ended (EJE-004, EJE-006): lifted, cancelled, out of the extra hit area or reset. It goes to
    /// the engine by contact, not through a tile (INV-9), so the keys are released even when the tile that started the
    /// hold was rebuilt, moved or removed while the finger rested on it.
    /// </summary>
    /// <param name="contactId">The pointer id that owns the hold.</param>
    /// <param name="summary">Duration, displacement and palm.</param>
    /// <param name="reason">Why it ended.</param>
    public void HoldEnded(uint contactId, ContactSummary summary, HoldEndReason reason) =>
        _ = _controller.HoldEnded(contactId, summary, reason);

    /// <summary>Formats every text again in the current language (IDI-001).</summary>
    public void Relocalize()
    {
        AccessibleName = _localization.Current.Format(L.AppName);
        ApplyEngine(_engine);
    }

    /// <summary>Makes <paramref name="target"/> hold <paramref name="items"/>, in order, touching it only when they differ.</summary>
    internal static void Sync(
        ObservableCollection<TileViewModel> target,
        IReadOnlyList<TileViewModel> items
    )
    {
        if (target.Count == items.Count && target.SequenceEqual(items))
        {
            return;
        }

        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static Message StateOf(PressedItem item) =>
        item.ContactId is null ? L.Latched : L.Holding;

    private static Message HelpOf(TileBehavior behavior) =>
        behavior switch
        {
            TileBehavior.Hold => L.THold,
            TileBehavior.Toggle => L.TToggle,
            _ => L.TTap,
        };

    private static Message? BadgeOf(TileBehavior behavior) =>
        behavior switch
        {
            TileBehavior.Hold => L.BHold,
            TileBehavior.Toggle => L.BToggle,
            _ => null,
        };

    private static Message StickyStateOf(StickyLevel level) =>
        level switch
        {
            StickyLevel.Once => L.ModOnce,
            StickyLevel.Locked => L.ModLock,
            _ => L.ModOff,
        };

    /// <summary>
    /// Keeps the view model of every tile whose shortcut stays (a tile whose behavior changed counts as new: the
    /// surface picks its UI Automation pattern and touch target kind when it builds the control).
    /// </summary>
    private List<TileViewModel> SyncById(
        Dictionary<ShortcutId, TileViewModel> byId,
        IReadOnlyList<TileModel> tiles
    )
    {
        var next = new List<TileViewModel>(tiles.Count);
        var kept = new Dictionary<ShortcutId, TileViewModel>();
        foreach (var tile in tiles)
        {
            if (byId.TryGetValue(tile.Id, out var existing) && existing.Behavior == tile.Behavior)
            {
                existing.Update(tile);
            }
            else
            {
                existing = new TileViewModel(tile, _controller);
            }

            kept.TryAdd(tile.Id, existing);
            next.Add(existing);
        }

        byId.Clear();
        foreach (var pair in kept)
        {
            byId.Add(pair.Key, pair.Value);
        }

        return next;
    }

    private string? NameOf(ShortcutId shortcut) =>
        (
            _listById.GetValueOrDefault(shortcut) ?? _stripById.GetValueOrDefault(shortcut)
        )?.AccessibleName
        ?? _nameOfShortcut(shortcut);

    private void GoToPage(int page)
    {
        _page = page;
        Recompose();
    }

    private void NextStripPage()
    {
        _stripWindow = StripLayout.Window(
            _strip.Count,
            StripLayout.Capacity(_layout),
            _stripWindow.NextPage
        );
        Recompose();
    }

    private string ViewKey() =>
        _context.SearchingWithText ? SearchView
        : _context.Frequents ? FrequentsView
        : _model.Profile.Value;

    /// <summary>Composes the body from the latest inputs; every decision is a call to the domain rules.</summary>
    private void Recompose()
    {
        var shape = GridMetrics.Shape(_layout, _gridSpace);
        var alert = Panic.IsVisible || _context.ElevatedApp is not null;
        _cramped = CrampedRule.Evaluate(_cramped, alert, _gridSpace, shape.TileHeightPx);

        var list = _context.SearchingWithText ? _results : _list;
        ShowsNoResults = _context.SearchingWithText && _results.Count == 0;
        var count = list.Count;
        var pageContext = new PageContext(ViewKey(), shape.Columns, shape.Rows, _layout.Compact);
        _page = Paging.Reconcile(
            _page,
            _pageContext,
            pageContext,
            Paging.PageCount(count, shape.PerPage)
        );
        _pageContext = pageContext;
        var window = Paging.Window(count, shape.PerPage, _page);
        _stripWindow = StripLayout.Window(
            _strip.Count,
            StripLayout.Capacity(_layout),
            _stripWindow.Page
        );
        var layers = BodyLayerRules.Evaluate(
            new BodyLayerInputs(
                _layout,
                _context.SearchingWithText,
                _cramped,
                _context.Frequents,
                _context.PickerOpen,
                count,
                window.PageCount,
                _context.Notice is not null,
                _context.CanRepeat,
                _context.EditMode,
                _context.ElevatedApp is not null
            )
        );
        Layers = layers;
        Shape = shape;

        for (var i = 0; i < list.Count; i++)
        {
            list[i].ApplyVoiceNumber(_layout.VoiceNumbers ? VoiceNumbers.ForList(i) : null);
        }

        for (var i = 0; i < _strip.Count; i++)
        {
            _strip[i]
                .ApplyVoiceNumber(_layout.VoiceNumbers ? VoiceNumbers.ForStrip(count, i) : null);
        }

        Sync(Tiles, list.GetRange(window.Start, window.Count));
        ApplyParts(window, layers);
    }

    private void ApplyParts(PageWindow window, BodyLayers layers)
    {
        var l = _localization.Current;
        Pager.Apply(
            window,
            layers.Pager,
            l.Format(L.PrevPage),
            l.Format(L.NextPage),
            l.Format(L.PageN),
            l.Format(L.On)
        );
        Strip.Apply(
            _strip.GetRange(_stripWindow.Start, _stripWindow.Count),
            layers.AlwaysVisibleRow,
            layers.AlwaysVisibleLabel,
            StripLayout.ShowsNames(_layout),
            l.Format(L.Always),
            _stripWindow.HasMore,
            _stripWindow.MoreLabel,
            l.Format(L.StripMoreA),
            StripLayout.TileHeight(_layout)
        );
        Sticky.Apply(
            layers.StickyRow,
            _engine.Sticky,
            l.Format(L.StickyMods),
            level => l.Format(StickyStateOf(level))
        );
        Selector.Apply(
            layers.Selector,
            _context.Frequents,
            _context.PickerOpen,
            _context.ActiveAppProfile == _model.Profile,
            _model.ProfileName,
            _model.ProfileIcon?.Name ?? string.Empty,
            l.Format(L.Freq),
            l.Format(L.ActiveApp),
            l.Format(L.SwitchProf),
            l.Format(L.On)
        );
        Picker.Apply(
            _model.PickerEntries,
            layers.PickerGrid,
            _context.Frequents ? null : _model.Profile,
            _context.ActiveAppProfile,
            _context.SuggestionApp is { } app ? l.Format(L.CreateFor(app)) : null,
            l.Format(L.MorePf),
            l.Format(L.ActiveLegend),
            l.Format(L.On),
            l.Format(L.ActiveApp)
        );
        var notice = _context.Notice;
        Notices.Apply(
            layers.NoticeBar,
            notice is null ? l.Format(L.Ready) : l.Format(notice.Text),
            notice?.Icon.Name ?? RestIcon,
            notice?.Tone ?? NoticeTone.Rest,
            notice?.CanUndo ?? false,
            layers.Repeat,
            l.Format(L.Undo),
            l.Format(L.Repeat)
        );
        Admin.Apply(
            layers.AdminNotice,
            _context.ElevatedApp is { } elevated ? l.Format(L.AdminMsg(elevated)) : string.Empty,
            l.Format(L.AdminBtn)
        );
        Empty.Apply(
            layers.EmptyProfile,
            _model.Profile,
            l.Format(L.EmptyProfT),
            l.Format(L.EmptyProfS(_model.ProfileName)),
            l.Format(L.AddShortcut)
        );
    }

    /// <summary>The intentions of a panel composed without them (M2): nothing happens.</summary>
    private sealed class NoBodyIntents : IPanelBodyIntents
    {
        public static NoBodyIntents Instance { get; } = new();

        public void ShowFrequents() { }

        public void ReturnFromFrequents() { }

        public void TogglePicker() { }

        public void ChooseProfile(ProfileId profile) { }

        public void CreateProfileForActiveApp() { }

        public void OpenTemplates() { }

        public void AdvanceSticky(ModifierKind modifier) { }

        public void Undo() { }

        public void Repeat() { }

        public void AddShortcut(ProfileId profile) { }

        public void RelaunchElevated() { }
    }
}
