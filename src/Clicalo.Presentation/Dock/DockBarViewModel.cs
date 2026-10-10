using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Application.Localization;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.VoiceNumbering;
using Clicalo.Presentation.Panel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Dock;

/// <summary>
/// The handle and the bar of the Tab view (docs/04 «Vista pestaña», PES-001 to PES-015) and the floating «Release all»
/// beside them: the shortcuts of the view, paged by what fits whole (PES-007), «What to see» (PES-006), the tools
/// (PES-008, PES-009), the «Pinned» window (PES-010) and the first-time guide (PES-015). Every rule comes from the domain
/// (<see cref="DockRules"/>, <see cref="DockGeometry"/>, <see cref="Paging"/>); every text from <c>data/i18n</c>,
/// formatted when applied. What the buttons do goes to <see cref="IDockIntents"/>.
/// </summary>
public sealed class DockBarViewModel : ObservableObject
{
    private const string FrequentsIcon = "star";
    private const string NoProfileIcon = "keyboard";
    private const string ScrollUpIcon = "keyboard_double_arrow_up";
    private const string ScrollDownIcon = "keyboard_double_arrow_down";
    private const string ScrollCategory = "nav";

    private readonly PanelInteractionController _controller;
    private readonly ILocalizationContext _localization;
    private readonly IDockIntents _intents;
    private readonly Dictionary<ShortcutId, DockTileViewModel> _listById = [];
    private readonly Dictionary<ShortcutId, DockTileViewModel> _pinnedById = [];
    private List<DockTileViewModel> _list = [];
    private List<DockTileViewModel> _pinned = [];
    private DockBarState? _state;
    private EngineSnapshot _engine = EngineSnapshot.Empty;
    private PageWindow _window = Paging.Window(0, 1, 0);
    private int _page;
    private double _tileSpace = double.NaN;
    private int _perPage = 1;
    private bool _isVertical = true;
    private string _pageLabel = string.Empty;
    private string _handleIcon = NoProfileIcon;
    private string _profileCaret = string.Empty;
    private string _autoFixedIcon = string.Empty;
    private string _autoFixedLabel = string.Empty;
    private string _autoFixedName = string.Empty;
    private string _pinLabel = string.Empty;
    private string _pinName = string.Empty;
    private string _coachStepLabel = string.Empty;
    private string _coachTitle = string.Empty;
    private string _coachText = string.Empty;
    private string _coachNextLabel = string.Empty;
    private bool _showsCoach;

    /// <summary>Creates the bar.</summary>
    /// <param name="controller">Where the gestures of its shortcuts go.</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="intents">What its buttons ask for.</param>
    /// <param name="layers">
    /// Test mode and the menu of a shortcut, shared with the panel (PES-010, PES-014); <see langword="null"/> for a bar
    /// without them.
    /// </param>
    public DockBarViewModel(
        PanelInteractionController controller,
        ILocalizationContext localization,
        IDockIntents intents,
        PanelLayerModels? layers = null
    )
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(intents);
        _controller = controller;
        _localization = localization;
        _intents = intents;
        ScrollUp = ScrollTile(
            "dock.scrollUp",
            MouseOp.ScrollUp,
            ScrollUpIcon,
            localization.Current.Format(L.DockScrollUp)
        );
        ScrollDown = ScrollTile(
            "dock.scrollDown",
            MouseOp.ScrollDown,
            ScrollDownIcon,
            localization.Current.Format(L.DockScrollDown)
        );
        Labels = new DockLabels(localization);
        Modes = layers is null
            ? null
            : new DockTileModes(layers.TestMode, layers.Menu, layers.InFrequents);
    }

    /// <summary>
    /// What a gesture on a shortcut does before it runs: test mode and the menu (PES-010, PES-014); <see langword="null"/>
    /// for a bar without them.
    /// </summary>
    public DockTileModes? Modes { get; }

    /// <summary>The texts of the handle and the bar in the interface language.</summary>
    public DockLabels Labels { get; }

    /// <summary>The settings, the session and the engine as last applied.</summary>
    public DockBarState? State => _state;

    /// <summary>The edge of the Tab view.</summary>
    public DockSide Side => _state?.Dock.Side ?? DockSide.Right;

    /// <summary>Whether the handle and the bar stand on the left or the right edge.</summary>
    public bool IsVertical
    {
        get => _isVertical;
        private set => SetProperty(ref _isVertical, value);
    }

    /// <summary>The measures of the panel size.</summary>
    public SizeMetrics Metrics => _state?.Metrics ?? PanelLayoutSettings.Default.Metrics;

    /// <summary>The shortcuts of the page of the bar, in order (PES-007).</summary>
    public ObservableCollection<DockTileViewModel> PageTiles { get; } = [];

    /// <summary>The shortcuts of Always visible, for the «Pinned» window (PES-010).</summary>
    public ObservableCollection<DockTileViewModel> PinnedTiles { get; } = [];

    /// <summary>⏶ Scroll up: a Mantener of the mouse wheel (PES-008).</summary>
    public DockTileViewModel ScrollUp { get; }

    /// <summary>⏷ Scroll down: a Mantener of the mouse wheel (PES-008).</summary>
    public DockTileViewModel ScrollDown { get; }

    /// <summary>Shortcuts per page: what fits whole, at most the preference of General (PES-007).</summary>
    public int PerPage => _perPage;

    /// <summary>Whether the bar has more than one page (the pager shows).</summary>
    public bool HasPages => _window.IsPaged;

    /// <summary>Whether ▲/◀ leads somewhere.</summary>
    public bool CanGoPrevious => _window.HasPrevious;

    /// <summary>Whether ▼/▶ leads somewhere.</summary>
    public bool CanGoNext => _window.HasNext;

    /// <summary>«i/N» between ▲ and ▼ (PES-007).</summary>
    public string PageLabel
    {
        get => _pageLabel;
        private set => SetProperty(ref _pageLabel, value);
    }

    /// <summary>The icon of the handle: ★ in Frequents, the profile's, or keyboard without a profile (PES-001).</summary>
    public string HandleIcon
    {
        get => _handleIcon;
        private set => SetProperty(ref _handleIcon, value);
    }

    /// <summary>After the profile name: ▾ (expand_more), ✕ with its grid open, ↶ (undo) in Frequents (PES-006).</summary>
    public string ProfileCaret
    {
        get => _profileCaret;
        private set => SetProperty(ref _profileCaret, value);
    }

    /// <summary>The icon of the Auto/Fixed pill.</summary>
    public string AutoFixedIcon
    {
        get => _autoFixedIcon;
        private set => SetProperty(ref _autoFixedIcon, value);
    }

    /// <summary>«Auto» or «Fijo» on the pill.</summary>
    public string AutoFixedLabel
    {
        get => _autoFixedLabel;
        private set => SetProperty(ref _autoFixedLabel, value);
    }

    /// <summary>[autoA] or [lockA]: the accessible name of the pill.</summary>
    public string AutoFixedName
    {
        get => _autoFixedName;
        private set => SetProperty(ref _autoFixedName, value);
    }

    /// <summary>[pinOff] or [pinOn] on the lock of the bar.</summary>
    public string PinLabel
    {
        get => _pinLabel;
        private set => SetProperty(ref _pinLabel, value);
    }

    /// <summary>[pinOffA] or [pinOnA]: the accessible name of the lock.</summary>
    public string PinName
    {
        get => _pinName;
        private set => SetProperty(ref _pinName, value);
    }

    /// <summary>Whether the first-time guide shows (PES-015).</summary>
    public bool ShowsCoach
    {
        get => _showsCoach;
        private set => SetProperty(ref _showsCoach, value);
    }

    /// <summary>«i / 3» of the guide.</summary>
    public string CoachStepLabel
    {
        get => _coachStepLabel;
        private set => SetProperty(ref _coachStepLabel, value);
    }

    /// <summary>The title of the step: [co1t], [co2t] or [co3t].</summary>
    public string CoachTitle
    {
        get => _coachTitle;
        private set => SetProperty(ref _coachTitle, value);
    }

    /// <summary>The text of the step: [co1d], [co2d] or [co3d].</summary>
    public string CoachText
    {
        get => _coachText;
        private set => SetProperty(ref _coachText, value);
    }

    /// <summary>[next], or [understood] on the last step.</summary>
    public string CoachNextLabel
    {
        get => _coachNextLabel;
        private set => SetProperty(ref _coachNextLabel, value);
    }

    /// <summary>Whether 📌 «Pinned» shows (PES-008).</summary>
    public bool ShowsPinned =>
        _state is { } state && DockRules.ShowsPinned(state.ShowStripRow, _pinned.Count);

    /// <summary>Whether the sticky keys button shows (PES-008).</summary>
    public bool ShowsSticky => _state?.StickyRow == true;

    /// <summary>Applies the settings, the view and what is open.</summary>
    /// <param name="state">The state.</param>
    public void Apply(DockBarState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        IsVertical = DockGeometry.IsVertical(state.Dock.Side);
        HandleIcon =
            state.Frequents ? FrequentsIcon
            : state.ProfileIcon.Length > 0 ? state.ProfileIcon
            : NoProfileIcon;
        ProfileCaret =
            state.Frequents ? "undo"
            : state.Flyout == DockFlyout.Profiles ? "close"
            : "expand_more";
        AutoFixedIcon = state.IsFixed ? "lock" : "autorenew";
        ShowsCoach = DockRules.ShowsCoach(
            state.BarOpen,
            state.Dock.CoachDone,
            state.Flyout != DockFlyout.None
        );
        Format();
        Repage();
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Side));
        OnPropertyChanged(nameof(Metrics));
        OnPropertyChanged(nameof(ShowsPinned));
        OnPropertyChanged(nameof(ShowsSticky));
    }

    /// <summary>
    /// Applies the shortcuts of the view and of Always visible (the same the grid and the Always visible row show,
    /// PES-007, PES-010). Shortcuts that stay keep their view model.
    /// </summary>
    /// <param name="list">The shortcuts of the view, in order.</param>
    /// <param name="pinned">The shortcuts of Always visible, in order.</param>
    public void ApplyTiles(ImmutableArray<TileModel> list, ImmutableArray<TileModel> pinned)
    {
        _list = SyncById(_listById, list.IsDefault ? [] : list);
        _pinned = SyncById(_pinnedById, pinned.IsDefault ? [] : pinned);
        Sync(PinnedTiles, _pinned);
        ApplyEngine(_engine);
        OnPropertyChanged(nameof(ShowsPinned));
    }

    /// <summary>Applies what the engine holds: the state of every shortcut (PES-007) and the dot of the handle.</summary>
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

        foreach (var tile in _list.Concat(_pinned).Append(ScrollUp).Append(ScrollDown))
        {
            var isHeld = held.TryGetValue(tile.Id, out var item);
            tile.ApplyState(
                isHeld,
                isHeld
                    ? localizer.Format(item!.ContactId is null ? L.Latched : L.Holding)
                    : string.Empty,
                localizer.Format(
                    tile.Behavior switch
                    {
                        TileBehavior.Hold => L.THold,
                        TileBehavior.Toggle => L.TToggle,
                        _ => L.TTap,
                    }
                )
            );
        }
    }

    /// <summary>
    /// The space the bar <b>measured</b> for its shortcuts along its edge (PES-007), in logical pixels; the shortcuts per
    /// page follow it.
    /// </summary>
    /// <param name="availablePx">The measured length.</param>
    public void ApplyTileSpace(double availablePx)
    {
        if (double.IsNaN(availablePx) || Math.Abs(availablePx - _tileSpace) < 0.5)
        {
            return;
        }

        _tileSpace = availablePx;
        Repage();
    }

    /// <summary>Back to the first page (a view change, a profile change: SEL-004).</summary>
    public void FirstPage()
    {
        _page = 0;
        Repage();
    }

    /// <summary>▲ or ◀.</summary>
    public void Previous()
    {
        _page = Paging.Step(_window, -1);
        Repage();
    }

    /// <summary>▼ or ▶.</summary>
    public void Next()
    {
        _page = Paging.Step(_window, 1);
        Repage();
    }

    /// <summary>Formats every text again in the current language (IDI-001).</summary>
    public void Relocalize()
    {
        Labels.Relocalize();
        ScrollUp.Update(ScrollModel(ScrollUp, _localization.Current.Format(L.DockScrollUp)));
        ScrollDown.Update(ScrollModel(ScrollDown, _localization.Current.Format(L.DockScrollDown)));
        Format();
        Repage();
        ApplyEngine(_engine);
    }

    /// <summary>The length of one shortcut along the bar: 50/58/70 high (vertical) or 62/72/88 wide (PES-007).</summary>
    public double TileLength =>
        IsVertical ? Metrics.DockVerticalTileHeightPx : Metrics.DockHorizontalTileWidthPx;

    /// <summary>The handle was tapped.</summary>
    public void OpenBar() => _intents.OpenBar();

    /// <summary>The handle was dragged to <paramref name="percent"/>.</summary>
    /// <param name="percent">The new position on its edge.</param>
    public void MoveHandle(int percent) => _intents.MoveHandle(percent);

    /// <summary>The chevron: the bar folds.</summary>
    public void CloseBar() => _intents.CloseBar();

    /// <summary>Expand.</summary>
    public void Expand() => _intents.Expand();

    /// <summary>★ Frequents.</summary>
    public void ShowFrequents() => _intents.ShowFrequents();

    /// <summary>The profile button.</summary>
    public void ProfileButton() => _intents.ProfileButton();

    /// <summary>The Auto/Fixed pill.</summary>
    public void ToggleLock() => _intents.ToggleLock();

    /// <summary>🔍 Search.</summary>
    public void Search() => _intents.Search();

    /// <summary>↻ Repeat.</summary>
    public void Repeat() => _intents.Repeat();

    /// <summary>📌 Pinned.</summary>
    public void TogglePinned() => _intents.TogglePinned();

    /// <summary>Sticky keys.</summary>
    public void ToggleSticky() => _intents.ToggleSticky();

    /// <summary>The lock of the bar.</summary>
    public void TogglePinOpen() => _intents.TogglePinOpen();

    /// <summary>The <c>tune</c> button: Quick settings beside the bar (PES-009).</summary>
    public void QuickSettings() => _intents.QuickSettings();

    /// <summary>[next] or [understood].</summary>
    public void CoachNext() => _intents.CoachNext();

    /// <summary>[coachSkip].</summary>
    public void CoachSkip() => _intents.CoachSkip();

    /// <summary>The floating «Release all».</summary>
    public void ReleaseAll() => _intents.ReleaseAll();

    private DockTileViewModel ScrollTile(string id, MouseOp op, string icon, string name)
    {
        var shortcutId = new ShortcutId(id);
        var shortcut = new Shortcut(
            shortcutId,
            LocalizedText.Same(id, LangCode.Es),
            new IconRef(icon),
            AutoIcon: false,
            new CategoryId(ScrollCategory),
            new MouseAction(op, ScrollSpeed.Normal),
            new ShortcutOptions(Confirm: false, new HoldLimit.InheritGlobal(), IsPrivate: false),
            Origin: null,
            PinnedFrom: null
        );
        return new DockTileViewModel(
            new TileModel(
                shortcutId,
                name,
                TileBehavior.Hold,
                new TileBinding(shortcut, null, InjectionMode.VirtualKey),
                new IconRef(icon),
                new CategoryId(ScrollCategory)
            ),
            _controller
        );
    }

    private void Format()
    {
        var l = _localization.Current;
        var state = _state;
        var isFixed = state?.IsFixed == true;
        AutoFixedLabel = l.Format(isFixed ? L.LockOn2 : L.Auto2);
        AutoFixedName = l.Format(isFixed ? L.LockA : L.AutoA);
        var pinOpen = state?.Dock.PinOpen == true;
        PinLabel = l.Format(pinOpen ? L.PinOn : L.PinFolds);
        PinName = l.Format(pinOpen ? L.PinOnA : L.PinOffA);
        var step = Math.Clamp(state?.CoachStep ?? 0, 0, DockRules.CoachSteps - 1);
        CoachStepLabel = l.Format(L.CoachStep(index: step + 1, total: DockRules.CoachSteps));
        CoachTitle = l.Format(
            step switch
            {
                0 => L.Co1t,
                1 => L.Co2t,
                _ => L.Co3t,
            }
        );
        CoachText = l.Format(
            step switch
            {
                0 => L.Co1d,
                1 => L.Co2d,
                _ => L.Co3d,
            }
        );
        CoachNextLabel = l.Format(DockRules.NextCoachStep(step) is null ? L.Understood : L.Next);
    }

    private void Repage()
    {
        var preference = _state?.Dock.PerPage ?? SettingsSchema.DockPerPageChoices[0];
        _perPage = DockGeometry.TilesPerPage(
            preference,
            double.IsNaN(_tileSpace) ? double.PositiveInfinity : _tileSpace,
            TileLength,
            Metrics.GapPx
        );
        _window = Paging.Window(_list.Count, _perPage, _page);
        _page = _window.Page;
        var page = _list.Skip(_window.Start).Take(_window.Count).ToList();
        var numbers = _state?.VoiceNumbers == true;
        for (var i = 0; i < page.Count; i++)
        {
            page[i].ApplyVoiceNumber(numbers ? VoiceNumbers.ForListPage(_window, i) : null);
        }

        for (var i = 0; i < _pinned.Count; i++)
        {
            _pinned[i].ApplyVoiceNumber(numbers ? VoiceNumbers.ForStrip(_list.Count, i) : null);
        }

        Sync(PageTiles, page);
        PageLabel = _localization.Current.Format(
            L.DockPageOf(index: _window.Page + 1, total: _window.PageCount)
        );
        OnPropertyChanged(nameof(PerPage));
        OnPropertyChanged(nameof(HasPages));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
    }

    /// <summary>Makes <paramref name="target"/> hold <paramref name="items"/>, in order, touching it only when they differ.</summary>
    private static void Sync(
        ObservableCollection<DockTileViewModel> target,
        IReadOnlyList<DockTileViewModel> items
    )
    {
        if (target.SequenceEqual(items))
        {
            return;
        }

        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static TileModel ScrollModel(DockTileViewModel tile, string name) =>
        new(
            tile.Id,
            name,
            tile.Behavior,
            tile.Binding,
            new IconRef(tile.Icon),
            new CategoryId(tile.Category)
        );

    private List<DockTileViewModel> SyncById(
        Dictionary<ShortcutId, DockTileViewModel> byId,
        ImmutableArray<TileModel> models
    )
    {
        var next = new List<DockTileViewModel>(models.Length);
        var seen = new HashSet<ShortcutId>();
        foreach (var model in models)
        {
            if (!seen.Add(model.Id))
            {
                continue;
            }

            if (byId.TryGetValue(model.Id, out var tile))
            {
                tile.Update(model);
            }
            else
            {
                tile = new DockTileViewModel(model, _controller);
                byId[model.Id] = tile;
            }

            next.Add(tile);
        }

        foreach (var gone in byId.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            byId.Remove(gone);
        }

        return next;
    }
}
