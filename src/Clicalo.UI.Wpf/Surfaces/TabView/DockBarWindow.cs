using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Shapes;
using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel.TestMode;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// The open bar of the Tab view (docs/04 «Vista pestaña», PES-005 to PES-009) in direction C (D12). Padding
/// <see cref="Space.S2"/>, radius <see cref="Radii.Large"/> and five zones, the shortcuts between two lines:
/// <list type="number">
/// <item>the bar itself: fold (the chevron toward the edge), expand, keep it open (the lock) and <c>tune</c> for Quick
/// settings (PES-009);</item>
/// <item>«What to see»: the track of ★ Frequents and the profile (one of the two is «elegido») and, under it, Auto/Fixed
/// with its word, which belongs to the profile;</item>
/// <item>the shortcuts that fit whole, with «▲ i/N ▼» under them on a side edge or ‹ › around them on the top and
/// bottom ones;</item>
/// <item>the tools: Search and Repeat, 📌 Pinned and Sticky keys when they apply;</item>
/// <item>the Mantener scroll up and down.</item>
/// </list>
/// <para>
/// The size of the panel is the size of the bar (D20, <c>sizes.json</c>): in S a side bar shows its shortcuts as icons,
/// two across, and «What to see» as icons; in M one shortcut across with its name; in L the targets are 52 and the
/// names two lines of 14, so the bar is wider. Across a side bar the controls go in rows of two, and a row with one
/// left is centered, so no row has an empty place. Every control is drawn at least 44 × 44 and never changes size:
/// what is open or on shows the one look of «elegido». The tools are icons with their label as their accessible name
/// (D19); Auto/Fixed keeps its word, so it is never taken for the lock of the bar.
/// </para>
/// Its shortcuts open their menu with a long press and show the marks of test mode (PES-010, PES-014). One window per
/// edge: its direction is fixed when it is created, and it composes its zones again when the size changes. It measures
/// what is left for the shortcuts along the edge and hands it to the view model (PES-007: they are counted, not
/// estimated).
/// </summary>
public sealed class DockBarWindow : TouchSurface
{
    private const double Pad = DockGeometry.BarPaddingPx;

    private readonly DockBarViewModel _viewModel;
    private readonly ThemeService _theme;
    private readonly bool _vertical;
    private readonly StackPanel _root;
    private readonly StackPanel _tiles;
    private readonly StackPanel _tools;
    private readonly List<(
        DockTileViewModel ViewModel,
        ShortcutTile Control,
        PropertyChangedEventHandler Handler
    )> _tileControls = [];
    private readonly List<TestMarkBadge> _badges = [];
    private readonly IconButton _close;
    private readonly IconButton _expand;
    private readonly IconButton _previous;
    private readonly IconButton _next;
    private readonly IconButton _search;
    private readonly IconButton _repeat;
    private readonly IconButton _tune;
    private readonly StyledText _pageLabel;
    private readonly SymbolIcon _profileCaret;
    private readonly Ellipse _activeDot;
    private BarShape _shape;
    private DockTool _frequents = null!;
    private DockTool _profile = null!;
    private DockTool _pill = null!;
    private DockTool _pinned = null!;
    private DockTool _sticky = null!;
    private DockTool _lock = null!;
    private ShortcutTile _scrollUp = null!;
    private ShortcutTile _scrollDown = null!;
    private PropertyChangedEventHandler? _scrollUpHandler;
    private PropertyChangedEventHandler? _scrollDownHandler;
    private FrameworkElement _verticalPager = null!;
    private Border _toolsDivider = null!;
    private (bool Pinned, bool Sticky)? _toolsShown;
    private bool _voiceNumbers;
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
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Panel));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);

        // The controls every size has; their size and the zones around them come with the size (Compose).
        _close = IconTool(CloseIcon(side), viewModel.CloseBar);
        _expand = IconTool("open_in_full", viewModel.Expand);

        // PES-009: Quick settings beside the bar, the side of the Tab view included; an icon, as in the panel header.
        _tune = IconTool("tune", viewModel.QuickSettings);
        _search = IconTool("search", viewModel.Search);
        _repeat = IconTool("replay", viewModel.Repeat);

        // The arrows of a pager are secondary, as in the panel (PagerView): one button for one role (D12), and a
        // disabled arrow (no line, muted) never looks more present than the enabled one beside it.
        _previous = IconTool(_vertical ? "keyboard_arrow_up" : "chevron_left", viewModel.Previous);
        _next = IconTool(_vertical ? "keyboard_arrow_down" : "chevron_right", viewModel.Next);
        _previous.Variant = ButtonVariant.Secondary;
        _next.Variant = ButtonVariant.Secondary;
        _pageLabel = new StyledText(string.Empty, TextStyleToken.LabelSmall, ColorToken.Muted)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
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

        _tiles = new StackPanel
        {
            Orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _tools = new StackPanel
        {
            Orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal,
        };
        _root = new StackPanel
        {
            Orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal,
            Margin = new Thickness(Pad),
        };
        Content = _root;
        Compose();

        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Labels.PropertyChanged += OnViewModelChanged;
        _viewModel.PageChanged += OnPageChanged;
        _theme.Changed += OnThemeChanged;
        LayoutUpdated += (_, _) => MeasureTileSpace();
    }

    /// <summary>The edge of this bar.</summary>
    public DockSide Side { get; }

    /// <summary>The monitor it is on: the longest it may be comes from its work area (PES-005).</summary>
    public DisplayMonitor? Monitor { get; set; }

    /// <summary>
    /// What a side bar shows across its edge, its width less its padding (<c>sizes.json</c> dock.barWidthPx): two
    /// targets side by side, <see cref="Space.S1"/> apart.
    /// </summary>
    public double PairWidth => _shape.Across;

    /// <summary>The side of a control of the bar: 44, or 52 in L (D20).</summary>
    public double ToolSize => _shape.Tool;

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

    /// <summary>
    /// PES-005, PES-007: takes <paramref name="monitor"/> and fits the page to its edge before the bar is measured and
    /// placed, so it appears at its size instead of showing shortcuts that do not fit and shrinking under the finger.
    /// The second pass takes the pager the first one may have brought.
    /// </summary>
    /// <param name="monitor">The monitor the bar goes to.</param>
    public void FitToEdge(DisplayMonitor monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        Monitor = monitor;

        // PES-005: never longer than its edge allows, whatever its content asks for.
        var longest = DockGeometry.MaxBarLength(Side, monitor) / monitor.Scale;
        if (_vertical)
        {
            MaxHeight = longest;
        }
        else
        {
            MaxWidth = longest;
        }

        if (_viewModel.Side != Side)
        {
            return;
        }

        for (var pass = 0; pass < 2; pass++)
        {
            _root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            ApplyTileSpace(monitor, monitor.Scale);
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets()
    {
        yield return SurfaceTarget.Button(_close, _viewModel.CloseBar);
        yield return SurfaceTarget.Button(_expand, _viewModel.Expand);
        yield return SurfaceTarget.Button(_lock.Button, _viewModel.TogglePinOpen);
        yield return SurfaceTarget.Button(_tune, _viewModel.QuickSettings);
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
        yield return SurfaceTarget.For(_scrollUp, _viewModel.ScrollUp);
        yield return SurfaceTarget.For(_scrollDown, _viewModel.ScrollDown);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Labels.PropertyChanged -= OnViewModelChanged;
        _viewModel.PageChanged -= OnPageChanged;
        _theme.Changed -= OnThemeChanged;
        DetachScroll();
        DetachTiles();
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

    /// <summary>Takes <paramref name="element"/> out of whatever holds it, so a zone composed again can hold it.</summary>
    private static void Release(FrameworkElement element)
    {
        switch (element.Parent)
        {
            case System.Windows.Controls.Panel panel:
                panel.Children.Remove(element);
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case ContentControl content:
                content.Content = null;
                break;
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
    /// Puts <paramref name="items"/> into <paramref name="zone"/>: along a top or bottom bar one row, across a side bar
    /// rows of two, <see cref="Space.S1"/> apart, the last one centered when one is left, so no row has an empty place.
    /// </summary>
    private void Flow(System.Windows.Controls.Panel zone, IReadOnlyList<FrameworkElement> items)
    {
        foreach (var item in items)
        {
            Release(item);
        }

        zone.Children.Clear();
        if (!_vertical)
        {
            for (var i = 0; i < items.Count; i++)
            {
                items[i].Margin = new Thickness(i == 0 ? 0 : Space.S1, 0, 0, 0);
                items[i].VerticalAlignment = VerticalAlignment.Center;
                _ = zone.Children.Add(items[i]);
            }

            return;
        }

        for (var i = 0; i < items.Count; i += _shape.Columns)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, i == 0 ? 0 : Space.S1, 0, 0),
            };
            for (var j = i; j < Math.Min(i + _shape.Columns, items.Count); j++)
            {
                items[j].Margin = new Thickness(j == i ? 0 : Space.S1, 0, 0, 0);
                _ = row.Children.Add(items[j]);
            }

            _ = zone.Children.Add(row);
        }
    }

    private StackPanel Zone(params FrameworkElement[] items)
    {
        var zone = new StackPanel
        {
            Orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal,
        };
        Flow(zone, items);
        return zone;
    }

    /// <summary>
    /// Builds the zones of the bar for the size of the panel (D20): the controls of its size, the shortcuts across or
    /// along it, and the tools in rows of two. Called when the bar is created and when the size changes.
    /// </summary>
    private void Compose()
    {
        _shape = BarShape.Of(_viewModel.Metrics, _vertical);
        DetachScroll();
        _root.Children.Clear();
        if (_vertical)
        {
            _root.ClearValue(HeightProperty);
            _root.Width = _shape.Across;
        }
        else
        {
            _root.ClearValue(WidthProperty);
            _root.Height = TileHeight(vertical: false);
        }

        foreach (var button in new[] { _close, _expand, _tune, _search, _repeat, _previous, _next })
        {
            button.Width = _shape.Tool;
            button.Height = _shape.Tool;
        }

        // 2. What to see: ★ Frequents and the profile in their track, with their names from M; Auto/Fixed after them.
        var choice = _shape.IconChoices ? DockToolShape.Icon : DockToolShape.Stacked;
        _frequents = Tool("star", choice, _viewModel.ShowFrequents);
        _profile = Tool(_viewModel.HandleIcon, choice, _viewModel.ProfileButton);
        Release(_profileCaret);
        Release(_activeDot);
        if (!_shape.IconChoices)
        {
            ProfileNameRow(_profile, _profileCaret);
        }

        // Beside the icon, inside the segment: a segment of an icon alone has no room past the icon (D12, D20).
        _activeDot.Margin = _shape.IconChoices
            ? new Thickness(0, -Space.S1, -Space.S2, 0)
            : new Thickness(0, 0, -Space.S3, 0);
        _profile.Lead.Children.Add(_activeDot);
        _pill = Tool(
            "autorenew",
            _shape.IconChoices ? DockToolShape.Icon : DockToolShape.Row,
            _viewModel.ToggleLock
        );

        // 4. The tools that switch a state are icons, as the icon buttons beside them (D19).
        _pinned = Tool("push_pin", DockToolShape.Icon, _viewModel.TogglePinned);
        _sticky = Tool("keyboard_command_key", DockToolShape.Icon, _viewModel.ToggleSticky);
        _lock = Tool("lock_open", DockToolShape.Icon, _viewModel.TogglePinOpen);

        // 5. Scroll up and down: Mantener tiles that look like the tools at rest (PES-008).
        (_scrollUp, _scrollUpHandler) = DockTileFactory.Create(
            _viewModel.ScrollUp,
            _shape.Tool,
            _shape.Tool,
            IconBox.Small,
            1
        );
        (_scrollDown, _scrollDownHandler) = DockTileFactory.Create(
            _viewModel.ScrollDown,
            _shape.Tool,
            _shape.Tool,
            IconBox.Small,
            1
        );
        _scrollUp.ShowsCard = false;
        _scrollDown.ShowsCard = false;
        HideLabel(_scrollUp);
        HideLabel(_scrollDown);

        // 1. The bar itself: fold, expand, keep open and Quick settings, the four that act on the bar or the panel.
        Add(Zone(_close, _expand, _lock.Button, _tune), Space.S2);
        Add(Track(_frequents.Button, _profile.Button), Space.S1);
        Add(_pill.Button, 0);
        Add(Divider(), 0, stretch: true);

        // 3. The shortcuts, with their pager: «i/N» above the arrows across a side bar, ‹ › around them along the others.
        Release(_tiles);
        Release(_pageLabel);
        if (_vertical)
        {
            Add(_tiles, 0);
            var pager = new StackPanel { Margin = new Thickness(0, Space.S2, 0, 0) };
            _pageLabel.Margin = new Thickness(0, 0, 0, Space.S1);
            _ = pager.Children.Add(_pageLabel);
            _ = pager.Children.Add(Zone(_previous, _next));
            _verticalPager = pager;
            Add(_verticalPager, 0);
        }
        else
        {
            Add(HorizontalTiles(), 0);
            _verticalPager = new StackPanel();
        }

        _toolsDivider = Divider();
        Add(_toolsDivider, 0, stretch: true);
        _toolsShown = null;
        Add(_tools, Space.S1);
        Add(Zone(_scrollUp, _scrollDown), 0);

        _viewModel.ApplyTileGap(_shape.Columns > 1 && _shape.IconTiles ? Space.S1 : Space.S2);
        _viewModel.ApplyTileColumns(_shape.IconTiles && _vertical ? _shape.Columns : 1);
        ApplyTextScale();
        RebuildTiles();
        Refresh();
    }

    /// <summary>A tool of the bar, as high as the controls of its size; an icon tool is as wide too.</summary>
    private DockTool Tool(string symbol, DockToolShape shape, Action tap)
    {
        var tool = new DockTool(symbol, ButtonVariant.Tertiary, shape, tap);
        if (shape == DockToolShape.Icon)
        {
            tool.Button.Width = _shape.Tool;
            tool.Button.Height = _shape.Tool;
        }
        else if (shape == DockToolShape.Row)
        {
            tool.Button.Height = _shape.Tool;
        }
        else
        {
            tool.Button.Height =
                _shape.Tool > ControlHeight.Small ? ControlHeight.Large : ControlHeight.Medium;
        }

        return tool;
    }

    /// <summary>
    /// Adds a zone to the bar, <paramref name="after"/> away from the next one along the bar: across a side bar it
    /// takes the whole width; along a top or bottom bar it is centered in the row, or takes its whole height when
    /// <paramref name="stretch"/>.
    /// </summary>
    private void Add(FrameworkElement zone, double after, bool stretch = false)
    {
        Release(zone);
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
    /// The track of «What to see» (PES-006): <c>side</c> with its line, radius <see cref="Radii.Large"/> around the two
    /// segments of radius <see cref="Radii.Medium"/>, <see cref="Space.S1"/> inside. Only the segment in view is drawn
    /// «elegido». Across a side bar the segments with names go one under the other, and the icons of S side by side.
    /// </summary>
    private Border Track(FrameworkElement frequents, FrameworkElement profile)
    {
        var stacked = _vertical && !_shape.IconChoices;
        var segments = new StackPanel
        {
            Orientation = stacked ? Orientation.Vertical : Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        frequents.Margin = stacked
            ? new Thickness(0, 0, 0, Space.S1)
            : new Thickness(0, 0, Space.S1, 0);
        if (!_vertical && !_shape.IconChoices)
        {
            profile.MaxWidth = _shape.Across;
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
        Release(_previous);
        Release(_next);
        _previous.Margin = new Thickness(0, 0, Space.S2, 0);
        _next.Margin = new Thickness(Space.S2, 0, 0, 0);
        _previous.VerticalAlignment = VerticalAlignment.Center;
        _next.VerticalAlignment = VerticalAlignment.Center;
        _ = row.Children.Add(_previous);
        _ = row.Children.Add(_tiles);
        _ = row.Children.Add(_next);
        return row;
    }

    /// <summary>
    /// The tools of zone 4 in rows of two: Search and Repeat, and Pinned and Sticky keys when they apply (PES-008), so a
    /// tool that is not there never leaves its place empty.
    /// </summary>
    private void FlowTools()
    {
        var shown = (_viewModel.ShowsPinned, _viewModel.ShowsSticky);
        if (_toolsShown == shown)
        {
            return;
        }

        _toolsShown = shown;
        var items = new List<FrameworkElement> { _search, _repeat };
        if (shown.ShowsPinned)
        {
            items.Add(_pinned.Button);
        }

        if (shown.ShowsSticky)
        {
            items.Add(_sticky.Button);
        }

        Flow(_tools, items);
    }

    private void OnPageChanged(object? sender, EventArgs e) => RebuildTiles();

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
        else if (
            string.Equals(
                e.PropertyName,
                nameof(DockBarViewModel.Metrics),
                StringComparison.Ordinal
            )
            && BarShape.Of(_viewModel.Metrics, _vertical) != _shape
        )
        {
            // Another size: another bar (D20).
            Compose();
        }
        else
        {
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
    /// The height of a shortcut (PES-007, D12, D20): an icon alone where the size draws icons (S, one touch target);
    /// otherwise its icon and two lines of the scaled name with <see cref="Space.S1"/> around and between them, never
    /// less than the size says. Along a side bar of size M, at 100 %, it is the 52 of the size with one line: the length
    /// of a side bar is what is scarce, and at 100 % the usual names fit on it.
    /// </summary>
    private double TileHeight(bool vertical)
    {
        var metrics = _viewModel.Metrics;
        var size = vertical ? metrics.DockVerticalTileHeightPx : metrics.DockHorizontalTileHeightPx;
        if (size <= ControlHeight.Small)
        {
            return size;
        }

        var twoLines = (3 * Space.S1) + metrics.DockTileIconPx + (2 * ScaledLine());
        return vertical && _theme.TextScalePercent <= TypeScale.MinScalePercent
            ? size
            : Math.Max(size, twoLines);
    }

    /// <summary>
    /// The width of a shortcut of a top or bottom bar: 44 for the icons of S, and 88/104 in M and L as much wider as its
    /// name grows with the text scale (CUA-011), so a word that fits on a line at 100 % still fits on it.
    /// </summary>
    private double HorizontalTileWidth()
    {
        var width = _viewModel.Metrics.DockHorizontalTileWidthPx;
        return width <= ControlHeight.Small ? width : width * _theme.TextScalePercent / 100d;
    }

    /// <summary>The lines the name of a shortcut gets: none for an icon, two while they fit under the icon, else one.</summary>
    private int NameLines(double tileHeight) =>
        tileHeight <= ControlHeight.Small ? 0
        : tileHeight - (3 * Space.S1) - _viewModel.Metrics.DockTileIconPx >= 2 * ScaledLine() ? 2
        : 1;

    private double ScaledLine() =>
        TextStyles.Get(_shape.Label).LineHeight * _theme.TextScalePercent / 100d;

    private void DetachScroll()
    {
        if (_scrollUpHandler is { } up)
        {
            DockTileFactory.Detach(_viewModel.ScrollUp, up);
            _scrollUpHandler = null;
        }

        if (_scrollDownHandler is { } down)
        {
            DockTileFactory.Detach(_viewModel.ScrollDown, down);
            _scrollDownHandler = null;
        }
    }

    private void DetachTiles()
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
    }

    private void RebuildTiles()
    {
        DetachTiles();
        _tiles.Children.Clear();
        var metrics = _viewModel.Metrics;
        var height = _vertical
            ? _shape.IconTiles
                ? ControlHeight.Small
                : _viewModel.TileLengthAlong(vertical: true)
            : TileHeight(vertical: false);
        var width = _vertical
            ? _shape.IconTiles
                ? ControlHeight.Small
                : double.NaN
            : _viewModel.TileLengthAlong(vertical: false);
        var lines = NameLines(height);
        var cells = new List<FrameworkElement>();
        foreach (var tile in _viewModel.PageTiles)
        {
            var (control, handler) = DockTileFactory.Create(
                tile,
                width,
                height,
                metrics.DockTileIconPx,
                Math.Max(1, lines),
                Modes,
                _shape.Label
            );
            if (lines == 0)
            {
                // An icon alone has no room beside the voice number: the number takes the top and the icon goes under
                // it, as in the row of icons of the panel (ACC-009).
                HideLabel(control);
                if (_voiceNumbers)
                {
                    control.Padding = new Thickness(Space.S1, Space.S6, Space.S1, 0);
                }
            }

            var (cell, badge) = DockTileFactory.Cell(control, tile, Modes);
            if (badge is not null)
            {
                _badges.Add(badge);
            }

            _tileControls.Add((tile, control, handler));
            cells.Add(cell);
        }

        if (_vertical && _shape.IconTiles)
        {
            // Two icons across, in rows that never leave a place empty.
            Flow(_tiles, cells);
        }
        else
        {
            for (var i = 0; i < cells.Count; i++)
            {
                cells[i].Margin =
                    i == 0 ? new Thickness(0)
                    : _vertical ? new Thickness(0, Space.S2, 0, 0)
                    : new Thickness(Space.S2, 0, 0, 0);
                _ = _tiles.Children.Add(cells[i]);
            }
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
        var voiceNumbers = state?.VoiceNumbers == true;
        if (voiceNumbers != _voiceNumbers)
        {
            _voiceNumbers = voiceNumbers;
            if (_shape.IconTiles)
            {
                RebuildTiles();
            }
        }

        Title = labels.BarName;
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
        FlowTools();

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
    /// <remarks>
    /// Only the bar of the edge in view measures: the bars of every edge share the view model, and the bar of the edge
    /// being left is still on screen while the new one is laid out, so two measures along two edges would page the
    /// shortcuts back and forth inside one layout pass and never let it end. The lengths are what each zone asks for
    /// (a stack measures its children without a limit along its direction), never what the window was given: a window
    /// cut to the longest the edge allows would otherwise count the cut part as room for the shortcuts.
    /// </remarks>
    private void MeasureTileSpace()
    {
        if (
            _measuring
            || Monitor is not { } monitor
            || !IsVisible
            || _viewModel.Side != Side
            || !_root.IsMeasureValid
        )
        {
            return;
        }

        ApplyTileSpace(monitor, DpiScale > 0 ? DpiScale : 1);
    }

    /// <summary>Hands the view model the longest the bar may be at <paramref name="scale"/>, less what it asks for besides its shortcuts.</summary>
    private void ApplyTileSpace(DisplayMonitor monitor, double scale)
    {
        _measuring = true;
        try
        {
            var maxLength = DockGeometry.MaxBarLength(Side, monitor) / scale;
            var border = BorderThickness;
            var rootLength = _vertical
                ? _root.Margin.Top + _root.Margin.Bottom + border.Top + border.Bottom
                : _root.Margin.Left + _root.Margin.Right + border.Left + border.Right;
            foreach (UIElement zone in _root.Children)
            {
                rootLength += _vertical ? zone.DesiredSize.Height : zone.DesiredSize.Width;
            }

            var tilesLength = _vertical ? _tiles.DesiredSize.Height : _tiles.DesiredSize.Width;
            var chrome = rootLength - tilesLength;
            _viewModel.ApplyTileSpace(Math.Max(0, maxLength - chrome));
        }
        finally
        {
            _measuring = false;
        }
    }

    /// <summary>
    /// What the size of the panel makes of the bar (<c>sizes.json</c> dock, D20): the width across a side bar (two
    /// controls and their gap), the side of a control, whether the shortcuts and the choices of «What to see» are icons
    /// alone (S) and the style of the names.
    /// </summary>
    /// <param name="Across">What a side bar shows across, its width less its padding.</param>
    /// <param name="Tool">The side of a control: half of <paramref name="Across"/> less the gap.</param>
    /// <param name="Columns">Controls in a row across a side bar.</param>
    /// <param name="IconTiles">Whether the shortcuts are icons alone, one touch target each.</param>
    /// <param name="IconChoices">Whether Frequents, the profile and Auto/Fixed are icons alone.</param>
    /// <param name="Label">The style of the names of the shortcuts.</param>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct BarShape(
        double Across,
        double Tool,
        int Columns,
        bool IconTiles,
        bool IconChoices,
        TextStyleToken Label
    )
    {
        public static BarShape Of(SizeMetrics metrics, bool vertical)
        {
            var across = metrics.DockBarWidthPx - (2 * Pad);
            var tile = vertical
                ? metrics.DockVerticalTileHeightPx
                : metrics.DockHorizontalTileHeightPx;
            var icons = tile <= ControlHeight.Small;
            return new BarShape(
                across,
                (across - Space.S1) / 2,
                2,
                icons,
                icons,
                metrics.DockTileLabelPx > TextStyles.LabelSmall.Size
                    ? TextStyleToken.Label
                    : TextStyleToken.LabelSmall
            );
        }
    }
}
