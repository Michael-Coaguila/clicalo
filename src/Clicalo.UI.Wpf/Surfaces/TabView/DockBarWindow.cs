using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Shapes;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// The open bar of the Tab view (docs/04 «Vista pestaña», PES-005 to PES-009) in direction C (D12). Padding
/// <see cref="Space.S2"/>, radius <see cref="Radii.Large"/> and four zones separated by a line:
/// <list type="number">
/// <item>close (the chevron toward the edge) and expand, two icon buttons of 44;</item>
/// <item>«What to see»: the track of ★ Frequents and the profile (one of the two is «elegido») and Auto/Fixed;</item>
/// <item>the shortcuts that fit whole, with «▲ i/N ▼» under them on a side edge or ‹ › around them on the top and
/// bottom ones;</item>
/// <item>the tools: Search and Repeat, 📌 Pinned, Sticky keys, the lock of the bar, <c>tune</c> for Quick settings
/// (PES-009) and the Mantener scroll up and down.</item>
/// </list>
/// Every control is drawn at least 44 × 44 and never changes size: what is open or on shows the one look of «elegido».
/// A side bar is as wide as two targets of 44 side by side (<see cref="PairWidth"/>), so the pairs are never drawn
/// smaller than they answer; its labeled tools are rows of 44 with the icon before the label (two lines at most),
/// while a top or bottom bar is a row of <see cref="ControlHeight.Large"/> whose labeled tools are cards of 52 with the
/// icon above, as in the prototype. Its shortcuts open their menu with a long press and show the marks of test mode
/// (PES-010, PES-014). One window per edge: its shape and its direction are fixed when it is created. It measures what
/// is left for the shortcuts along the edge and hands it to the view model (PES-007: they are counted, not estimated).
/// </summary>
public sealed class DockBarWindow : TouchSurface
{
    /// <summary>
    /// What a side bar shows across its edge: two targets of 44 side by side, <see cref="Space.S1"/> apart (the bar
    /// is this plus its padding, <c>sizes.json</c> dock.barWidthPx).
    /// </summary>
    public const double PairWidth = (ControlHeight.Small * 2) + Space.S1;

    private const double Pad = DockGeometry.BarPaddingPx;

    private readonly DockBarViewModel _viewModel;
    private readonly ThemeService _theme;
    private readonly bool _vertical;
    private readonly StackPanel _root;
    private readonly StackPanel _tiles;
    private readonly List<(
        DockTileViewModel ViewModel,
        ShortcutTile Control,
        PropertyChangedEventHandler Handler
    )> _tileControls = [];
    private readonly List<Clicalo.UI.Wpf.Surfaces.Panel.TestMode.TestMarkBadge> _badges = [];
    private readonly IconButton _close;
    private readonly IconButton _expand;
    private readonly DockTool _frequents;
    private readonly DockTool _profile;
    private readonly SymbolIcon _profileCaret;
    private readonly Ellipse _activeDot;
    private readonly DockTool _pill;
    private readonly IconButton _previous;
    private readonly IconButton _next;
    private readonly StyledText _pageLabel;
    private readonly FrameworkElement _verticalPager;
    private readonly Border _toolsDivider;
    private readonly IconButton _search;
    private readonly IconButton _repeat;
    private readonly DockTool _pinned;
    private readonly DockTool _sticky;
    private readonly DockTool _lock;
    private readonly IconButton _tune;
    private readonly ShortcutTile _scrollUp;
    private readonly ShortcutTile _scrollDown;
    private readonly PropertyChangedEventHandler _scrollUpHandler;
    private readonly PropertyChangedEventHandler _scrollDownHandler;
    private bool _measuring;

    /// <summary>Creates the bar of <paramref name="side"/> on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="side">Its edge.</param>
    /// <param name="viewModel">What it shows and where its buttons go.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer layer.</param>
    /// <param name="theme">The theme service.</param>
    /// <param name="touch">The touch filter.</param>
    /// <param name="holdEnded">Where the end of a hold goes, by contact (INV-9).</param>
    public DockBarWindow(
        DockSide side,
        DockBarViewModel viewModel,
        SurfaceRegistry registry,
        TimeProvider time,
        ThemeService theme,
        TouchSettings touch,
        Action<uint, ContactSummary, HoldEndReason> holdEnded
    )
        : base(
            new SurfaceId(SurfaceKind.Dock, (int)side),
            registry,
            time,
            theme,
            touch,
            holdEnded,
            SurfaceLook.Panel
        )
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        Side = side;
        _viewModel = viewModel;
        _theme = theme;
        Modes = viewModel.Modes;
        _vertical = DockGeometry.IsVertical(side);
        ApplyTextScale();
        viewModel.ApplyTileGap(Space.S2);
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Panel));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        var shape = _vertical ? DockToolShape.Row : DockToolShape.Stacked;

        // 1. Controls: close (chevron toward the edge) and expand.
        _close = IconTool(CloseIcon(side), viewModel.CloseBar);
        _expand = IconTool("open_in_full", viewModel.Expand);

        // 2. What to see: ★ Frequents and the profile in their track, and Auto/Fixed.
        _frequents = new DockTool(
            "star",
            ButtonVariant.Tertiary,
            DockToolShape.Stacked,
            viewModel.ShowFrequents
        );
        _profile = new DockTool(
            viewModel.HandleIcon,
            ButtonVariant.Tertiary,
            DockToolShape.Stacked,
            viewModel.ProfileButton
        );
        _profileCaret = new SymbolIcon
        {
            Size = IconBox.Badge,
            Margin = new Thickness(Space.S1, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _profileCaret.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        ProfileNameRow(_profile, _profileCaret);

        // The dot of the active app goes in a slot of its own beside the icon, never over it (D12).
        _activeDot = new Ellipse
        {
            Width = Space.S2,
            Height = Space.S2,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, -Space.S3, 0),
        };
        _activeDot.SetResourceReference(Shape.FillProperty, ThemeBrushKey.For(ColorToken.Accent));
        _profile.Lead.Children.Add(_activeDot);
        var track = Track(_frequents.Button, _profile.Button);
        _pill = new DockTool("autorenew", ButtonVariant.Secondary, shape, viewModel.ToggleLock);

        // 3. The shortcuts, with their pager.
        _tiles = new StackPanel
        {
            Orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // The arrows of a pager are secondary, as in the panel (PagerView): one button for one role (D12), and a
        // disabled arrow (dashed) never looks more present than the enabled one beside it.
        _previous = IconTool(_vertical ? "keyboard_arrow_up" : "chevron_left", viewModel.Previous);
        _next = IconTool(_vertical ? "keyboard_arrow_down" : "chevron_right", viewModel.Next);
        _previous.Variant = ButtonVariant.Secondary;
        _next.Variant = ButtonVariant.Secondary;
        _pageLabel = new StyledText(string.Empty, TextStyleToken.LabelSmall, ColorToken.Muted)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // «▲ i/N ▼» (PES-007): across a side bar the two arrows take the whole width, so «i/N» goes above them.
        var pager = new StackPanel { Margin = new Thickness(0, Space.S2, 0, 0) };
        if (_vertical)
        {
            _pageLabel.Margin = new Thickness(0, 0, 0, Space.S1);
            _ = pager.Children.Add(_pageLabel);
            _ = pager.Children.Add(Pair(_previous, _next));
        }

        _verticalPager = pager;

        // 4. The tools.
        _search = IconTool("search", viewModel.Search);
        _repeat = IconTool("replay", viewModel.Repeat);
        _pinned = new DockTool("push_pin", ButtonVariant.Secondary, shape, viewModel.TogglePinned);
        _sticky = new DockTool(
            "keyboard_command_key",
            ButtonVariant.Secondary,
            shape,
            viewModel.ToggleSticky
        );
        _lock = new DockTool("lock_open", ButtonVariant.Secondary, shape, viewModel.TogglePinOpen);

        // PES-009: Quick settings beside the bar, the side of the Tab view included; an icon, as in the panel header.
        _tune = IconTool("tune", viewModel.QuickSettings);
        (_scrollUp, _scrollUpHandler) = DockTileFactory.Create(
            viewModel.ScrollUp,
            ControlHeight.Small,
            ControlHeight.Small,
            IconBox.Small,
            1
        );
        (_scrollDown, _scrollDownHandler) = DockTileFactory.Create(
            viewModel.ScrollDown,
            ControlHeight.Small,
            ControlHeight.Small,
            IconBox.Small,
            1
        );
        HideLabel(_scrollUp);
        HideLabel(_scrollDown);

        _root = new StackPanel
        {
            Orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal,
            Margin = new Thickness(Pad),
        };
        if (_vertical)
        {
            _root.Width = PairWidth;
        }
        else
        {
            _root.Height = TileHeight(vertical: false);
        }

        Add(Pair(_close, _expand), Space.S2);
        Add(track, Space.S1);
        Add(_pill.Button, 0);
        Add(Divider(), 0, stretch: true);
        Add(_vertical ? _tiles : HorizontalTiles(), 0);
        if (_vertical)
        {
            Add(_verticalPager, 0);
        }

        _toolsDivider = Divider();
        Add(_toolsDivider, 0, stretch: true);
        Add(Pair(_search, _repeat), Space.S1);
        Add(_pinned.Button, Space.S1);
        Add(_sticky.Button, Space.S1);
        Add(_lock.Button, Space.S1);
        Add(_tune, Space.S1);
        Add(Pair(_scrollUp, _scrollDown), 0);

        Content = _root;
        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Labels.PropertyChanged += OnViewModelChanged;
        _viewModel.PageTiles.CollectionChanged += OnPageChanged;
        _theme.Changed += OnThemeChanged;
        LayoutUpdated += (_, _) => MeasureTileSpace();
        RebuildTiles();
        Refresh();
    }

    /// <summary>The edge of this bar.</summary>
    public DockSide Side { get; }

    /// <summary>The monitor it is on: the longest it may be comes from its work area (PES-005).</summary>
    public DisplayMonitor? Monitor { get; set; }

    /// <summary>📌 Pinned, where its window goes beside (PES-010).</summary>
    public PhysicalRect PinnedButtonBounds => PhysicalBounds(_pinned.Button, inflate: false);

    /// <summary>Sticky keys, where its window goes beside.</summary>
    public PhysicalRect StickyButtonBounds => PhysicalBounds(_sticky.Button, inflate: false);

    /// <summary>The <c>tune</c> button, where Quick settings go beside (PES-009).</summary>
    public PhysicalRect TuneButtonBounds => PhysicalBounds(_tune, inflate: false);

    /// <summary>The <c>tune</c> button (PES-009).</summary>
    public TouchButton TuneButton => _tune;

    /// <summary>The shortcut tiles of the page, in order.</summary>
    public IReadOnlyList<ShortcutTile> TileControls =>
        [.. _tileControls.Select(static t => t.Control)];

    /// <summary>
    /// The buttons of the bar, in order: close, expand, Frequents, profile, Auto/Fixed, search, repeat, pinned, sticky,
    /// lock, tune.
    /// </summary>
    public IReadOnlyList<Control> Buttons =>
        [
            _close,
            _expand,
            _frequents.Button,
            _profile.Button,
            _pill.Button,
            _search,
            _repeat,
            _pinned.Button,
            _sticky.Button,
            _lock.Button,
            _tune,
        ];

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets()
    {
        yield return SurfaceTarget.Button(_close, _viewModel.CloseBar);
        yield return SurfaceTarget.Button(_expand, _viewModel.Expand);
        yield return SurfaceTarget.Button(_frequents.Button, _viewModel.ShowFrequents);
        yield return SurfaceTarget.Button(_profile.Button, _viewModel.ProfileButton);
        yield return SurfaceTarget.Button(_pill.Button, _viewModel.ToggleLock);
        foreach (var (viewModel, control, _) in _tileControls)
        {
            yield return SurfaceTarget.For(control, viewModel, longPress: Modes is not null);
        }

        yield return SurfaceTarget.Button(_previous, _viewModel.Previous);
        yield return SurfaceTarget.Button(_next, _viewModel.Next);
        yield return SurfaceTarget.Button(_search, _viewModel.Search);
        yield return SurfaceTarget.Button(_repeat, _viewModel.Repeat);
        yield return SurfaceTarget.Button(_pinned.Button, _viewModel.TogglePinned);
        yield return SurfaceTarget.Button(_sticky.Button, _viewModel.ToggleSticky);
        yield return SurfaceTarget.Button(_lock.Button, _viewModel.TogglePinOpen);
        yield return SurfaceTarget.Button(_tune, _viewModel.QuickSettings);
        yield return SurfaceTarget.For(_scrollUp, _viewModel.ScrollUp);
        yield return SurfaceTarget.For(_scrollDown, _viewModel.ScrollDown);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Labels.PropertyChanged -= OnViewModelChanged;
        _viewModel.PageTiles.CollectionChanged -= OnPageChanged;
        _theme.Changed -= OnThemeChanged;
        DockTileFactory.Detach(_viewModel.ScrollUp, _scrollUpHandler);
        DockTileFactory.Detach(_viewModel.ScrollDown, _scrollDownHandler);
        foreach (var (viewModel, _, handler) in _tileControls)
        {
            DockTileFactory.Detach(viewModel, handler);
        }

        foreach (var badge in _badges)
        {
            badge.Detach();
        }

        base.OnClosed(e);
    }

    private static IconButton IconTool(string symbol, Action tap)
    {
        var button = new IconButton
        {
            Symbol = symbol,
            Focusable = false,
            IsTabStop = false,
        };
        button.Click += (_, _) => tap();
        return button;
    }

    private static string CloseIcon(DockSide side) =>
        side switch
        {
            DockSide.Left => "chevron_left",
            DockSide.Top => "expand_less",
            DockSide.Bottom => "expand_more",
            _ => "chevron_right",
        };

    private static void HideLabel(ShortcutTile tile)
    {
        tile.ApplyTemplate();
        if (tile.Template?.FindName(ShortcutTileTemplate.LabelPart, tile) is FrameworkElement label)
        {
            label.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// The name of the profile with its ▾, ✕ or ↶ after it (PES-006), in place of the plain label of the tool: the name
    /// ends in «…» and the mark keeps its place.
    /// </summary>
    private static void ProfileNameRow(DockTool tool, SymbolIcon caret)
    {
        var parts = (DockPanel)tool.Button.Content;
        parts.Children.Remove(tool.Label);
        var row = new DockPanel
        {
            LastChildFill = true,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        DockPanel.SetDock(caret, Dock.Right);
        _ = row.Children.Add(caret);
        _ = row.Children.Add(tool.Label);
        _ = parts.Children.Add(row);
    }

    /// <summary>
    /// Adds a zone to the bar, <paramref name="after"/> away from the next one along the bar: across a side bar it
    /// takes the whole width; along a top or bottom bar it is centered in the row, or takes its whole height when
    /// <paramref name="stretch"/>.
    /// </summary>
    private void Add(FrameworkElement zone, double after, bool stretch = false)
    {
        zone.Margin = _vertical ? new Thickness(0, 0, 0, after) : new Thickness(0, 0, after, 0);
        if (_vertical)
        {
            zone.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            zone.VerticalAlignment = stretch ? VerticalAlignment.Stretch : VerticalAlignment.Center;
        }

        _ = _root.Children.Add(zone);
    }

    /// <summary>
    /// Two controls of 44 side by side, <see cref="Space.S1"/> apart: across a side bar they fill
    /// <see cref="PairWidth"/>, and along a top or bottom bar they sit in its row.
    /// </summary>
    private static StackPanel Pair(FrameworkElement first, FrameworkElement second)
    {
        first.Margin = new Thickness(0, 0, Space.S1, 0);
        var pair = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _ = pair.Children.Add(first);
        _ = pair.Children.Add(second);
        return pair;
    }

    /// <summary>
    /// The track of «What to see» (PES-006): <c>side</c> with its line, radius <see cref="Radii.Large"/> around the two
    /// segments of radius <see cref="Radii.Medium"/>, <see cref="Space.S1"/> inside. Only the segment in view is drawn
    /// «elegido».
    /// </summary>
    private Border Track(FrameworkElement frequents, FrameworkElement profile)
    {
        var segments = new StackPanel
        {
            Orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal,
        };
        frequents.Margin = _vertical
            ? new Thickness(0, 0, 0, Space.S1)
            : new Thickness(0, 0, Space.S1, 0);
        if (!_vertical)
        {
            profile.MaxWidth = PairWidth;
        }

        _ = segments.Children.Add(frequents);
        _ = segments.Children.Add(profile);
        var track = new Border
        {
            Child = segments,
            CornerRadius = new CornerRadius(Radii.Large),
            Padding = new Thickness(Space.S1),
        };
        track.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Side));
        track.SetResourceReference(
            Border.BorderBrushProperty,
            ThemeBrushKey.For(ColorToken.Border)
        );
        track.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        return track;
    }

    /// <summary>
    /// The line between two zones (<c>border</c>, the decorative line), <see cref="Space.S2"/> away from both and from
    /// the edges of the bar.
    /// </summary>
    private Border Divider()
    {
        var line = new Border();
        line.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Border));
        line.SetResourceReference(
            _vertical ? HeightProperty : WidthProperty,
            ThemeKeys.LineThickness
        );
        return new Border { Padding = new Thickness(Space.S2), Child = line };
    }

    private StackPanel HorizontalTiles()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        _previous.Margin = new Thickness(0, 0, Space.S2, 0);
        _next.Margin = new Thickness(Space.S2, 0, 0, 0);
        _previous.VerticalAlignment = VerticalAlignment.Center;
        _next.VerticalAlignment = VerticalAlignment.Center;
        _ = row.Children.Add(_previous);
        _ = row.Children.Add(_tiles);
        _ = row.Children.Add(_next);
        return row;
    }

    private void OnPageChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RebuildTiles();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            string.Equals(
                e.PropertyName,
                nameof(DockBarViewModel.TileLength),
                StringComparison.Ordinal
            )
        )
        {
            RebuildTiles();
        }
        else
        {
            if (
                string.Equals(
                    e.PropertyName,
                    nameof(DockBarViewModel.Metrics),
                    StringComparison.Ordinal
                )
            )
            {
                // Another size: its shortcuts have another height, and a top or bottom bar another thickness.
                ApplyTextScale();
                if (!_vertical)
                {
                    _root.Height = TileHeight(vertical: false);
                }
            }

            Refresh();
        }
    }

    /// <summary>
    /// CUA-011: the shortcuts grow with the text scale by what the lines of their name grow, so a name is never cut
    /// where it would fit on two lines; a top or bottom bar grows across with them.
    /// </summary>
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ApplyTextScale();
        if (!_vertical)
        {
            _root.Height = TileHeight(vertical: false);
            RebuildTiles();
        }
    }

    /// <summary>Gives the view model the length of a shortcut along the bar, which its pages follow.</summary>
    private void ApplyTextScale() =>
        _viewModel.ApplyTileLengths(TileHeight(vertical: true), HorizontalTileWidth());

    /// <summary>
    /// The height of a shortcut (PES-007, D12): its icon and two lines of the scaled
    /// <see cref="TextStyleToken.LabelSmall"/> with <see cref="Space.S1"/> around and between them (64 at 100 %, 80 at
    /// 150 %), the same anatomy as «Pinned» (PES-010). Along a side bar of size S or M, at 100 %, it is the 52 of the
    /// size with one line: the length of a side bar is what is scarce, and at 100 % the usual names fit on it.
    /// </summary>
    private double TileHeight(bool vertical)
    {
        var metrics = _viewModel.Metrics;
        var twoLines = (3 * Space.S1) + metrics.DockTileIconPx + (2 * ScaledLine());
        return vertical && _theme.TextScalePercent <= TypeScale.MinScalePercent
            ? metrics.DockVerticalTileHeightPx
            : Math.Max(
                vertical ? metrics.DockVerticalTileHeightPx : metrics.DockHorizontalTileHeightPx,
                twoLines
            );
    }

    /// <summary>
    /// The width of a shortcut of a top or bottom bar: 80/88/96, as much wider as its name grows with the text scale
    /// (CUA-011), so a word that fits on a line at 100 % still fits on it and is never split.
    /// </summary>
    private double HorizontalTileWidth() =>
        _viewModel.Metrics.DockHorizontalTileWidthPx * _theme.TextScalePercent / 100d;

    /// <summary>The lines the name of a shortcut gets: two while they fit under the icon, so a name is never cut short.</summary>
    private int NameLines(double tileHeight) =>
        tileHeight - (3 * Space.S1) - _viewModel.Metrics.DockTileIconPx >= 2 * ScaledLine() ? 2 : 1;

    private double ScaledLine() =>
        TextStyles.LabelSmall.LineHeight * _theme.TextScalePercent / 100d;

    private void RebuildTiles()
    {
        foreach (var (viewModel, _, handler) in _tileControls)
        {
            DockTileFactory.Detach(viewModel, handler);
        }

        foreach (var badge in _badges)
        {
            badge.Detach();
        }

        _badges.Clear();
        _tileControls.Clear();
        _tiles.Children.Clear();
        var metrics = _viewModel.Metrics;
        var height = _vertical ? _viewModel.TileLength : TileHeight(vertical: false);
        foreach (var tile in _viewModel.PageTiles)
        {
            var (control, handler) = DockTileFactory.Create(
                tile,
                _vertical ? double.NaN : _viewModel.TileLength,
                height,
                metrics.DockTileIconPx,
                NameLines(height),
                Modes
            );
            var (cell, badge) = DockTileFactory.Cell(control, tile, Modes);
            cell.Margin =
                _tiles.Children.Count == 0 ? new Thickness(0)
                : _vertical ? new Thickness(0, Space.S2, 0, 0)
                : new Thickness(Space.S2, 0, 0, 0);
            if (badge is not null)
            {
                _badges.Add(badge);
            }

            _tileControls.Add((tile, control, handler));
            _ = _tiles.Children.Add(cell);
        }

        // Without shortcuts (Frequents still empty) one line separates «What to see» from the tools, not two.
        _toolsDivider.Visibility = SurfaceParts.Shown(_tileControls.Count > 0);

        RefreshTargets();
    }

    private void Refresh()
    {
        var vm = _viewModel;
        var labels = vm.Labels;
        var state = vm.State;
        Title = labels.OpenBar;
        AutomationProperties.SetName(_close, labels.HideBar);
        AutomationProperties.SetName(_expand, labels.Expand);

        var frequents = state?.Frequents == true;
        _frequents.Say(labels.Frequents, labels.Frequents);
        _frequents.Button.IsActive = frequents;
        _frequents.Paint(frequents);

        var pickerOpen = state?.Flyout == DockFlyout.Profiles;
        _profile.Icon.Symbol = state is { ProfileIcon.Length: > 0 } ? state.ProfileIcon : "apps";
        _profile.Say(state?.ProfileName ?? string.Empty, labels.SwitchProfile);
        _profileCaret.Symbol = vm.ProfileCaret;
        _activeDot.Visibility = SurfaceParts.Shown(state?.IsActiveApp == true);
        _profile.Button.IsActive = !frequents;
        _profile.Paint(!frequents);

        // ACC-001: what opens a window beside the bar is an ExpandCollapse with its state, and what switches a state
        // (Auto/Fixed, the lock) a Toggle, so UI Automation hears it and not only the look tells it.
        _profile.Button.IsExpanded = pickerOpen;
        _pinned.Button.IsExpanded = state?.Flyout == DockFlyout.Pinned;
        _sticky.Button.IsExpanded = state?.Flyout == DockFlyout.Sticky;
        _tune.IsExpanded = state?.QuickOpen == true;

        var isFixed = state?.IsFixed == true;
        _pill.Button.IsOn = isFixed;
        _pill.Icon.Symbol = vm.AutoFixedIcon;
        _pill.Say(vm.AutoFixedLabel, vm.AutoFixedName);
        _pill.Paint(isFixed);

        _previous.IsEnabled = vm.CanGoPrevious;
        _next.IsEnabled = vm.CanGoNext;
        AutomationProperties.SetName(_previous, labels.PreviousPage);
        AutomationProperties.SetName(_next, labels.NextPage);
        _pageLabel.Text = vm.PageLabel;
        _verticalPager.Visibility = SurfaceParts.Shown(_vertical && vm.HasPages);
        _previous.Visibility = SurfaceParts.Shown(vm.HasPages);
        _next.Visibility = SurfaceParts.Shown(vm.HasPages);

        // CUA-004, AVI-004: what leads nowhere is disabled, and UI Automation hears it, not by color alone.
        AutomationProperties.SetName(_search, labels.Search);
        AutomationProperties.SetName(_repeat, labels.Repeat);
        _repeat.IsEnabled = state?.CanRepeat == true;

        var pinnedOpen = state?.Flyout == DockFlyout.Pinned;
        _pinned.Say(labels.PinnedShort, labels.Pinned);
        _pinned.Button.Visibility = SurfaceParts.Shown(vm.ShowsPinned);
        _pinned.Button.IsActive = pinnedOpen;
        _pinned.Paint(pinnedOpen);

        var stickyOpen = state?.Flyout == DockFlyout.Sticky;
        _sticky.Say(labels.Sticky, labels.Sticky);
        _sticky.Button.Visibility = SurfaceParts.Shown(vm.ShowsSticky);
        _sticky.Button.IsActive = stickyOpen;
        _sticky.Paint(stickyOpen);

        var pinOpen = state?.Dock.PinOpen == true;
        _lock.Button.IsOn = pinOpen;
        _lock.Icon.Symbol = pinOpen ? "lock" : "lock_open";
        _lock.Say(vm.PinLabel, vm.PinName);
        _lock.Paint(pinOpen);

        AutomationProperties.SetName(_tune, labels.QuickSettings);
        _tune.IsActive = state?.QuickOpen == true;
        RefreshTargets();
    }

    /// <summary>
    /// PES-007: what is left for the shortcuts along the edge, measured: the longest the bar may be, less everything
    /// else it shows. The view model counts the shortcuts that fit whole in it.
    /// </summary>
    private void MeasureTileSpace()
    {
        if (_measuring || Monitor is not { } monitor || !IsVisible || _root.ActualWidth <= 0)
        {
            return;
        }

        _measuring = true;
        try
        {
            var scale = DpiScale > 0 ? DpiScale : 1;
            var maxLength = DockGeometry.MaxBarLength(Side, monitor) / scale;
            var border = BorderThickness;
            var rootLength = _vertical
                ? _root.ActualHeight
                    + _root.Margin.Top
                    + _root.Margin.Bottom
                    + border.Top
                    + border.Bottom
                : _root.ActualWidth
                    + _root.Margin.Left
                    + _root.Margin.Right
                    + border.Left
                    + border.Right;
            var tilesLength = _vertical ? _tiles.ActualHeight : _tiles.ActualWidth;
            var chrome = rootLength - tilesLength;
            _viewModel.ApplyTileSpace(Math.Max(0, maxLength - chrome));
        }
        finally
        {
            _measuring = false;
        }
    }
}
