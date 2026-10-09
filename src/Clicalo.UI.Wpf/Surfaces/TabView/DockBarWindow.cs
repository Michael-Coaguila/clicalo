using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
/// The open bar of the Tab view (docs/04 «Vista pestaña», PES-005 to PES-008): radius 18, padding 8 and gap 6, with four
/// zones separated by dividers: (1) close and expand; (2) «What to see», the segmented ★ Frequents / profile and the
/// Auto/Fixed pill; (3) the shortcuts that fit whole, with «▲ i/N ▼» on a side edge or ◀ ▶ on the top and bottom ones;
/// (4) the tools: Search, Repeat, 📌 Pinned, Sticky keys, the lock of the bar and the Mantener scroll up and down. One
/// window per edge: its shape and its direction are fixed when it is created. It measures what is left for the
/// shortcuts along the edge and hands it to the view model (PES-007: they are counted, not estimated).
/// </summary>
public sealed class DockBarWindow : TouchSurface
{
    private const double Gap = 6;
    private const double Pad = 8;
    private const double ControlsLength = 30;
    private const double FrequentsLength = 42;
    private const double ProfileLength = 54;
    private const double PillLength = 28;
    private const double ToolLength = 40;
    private const double PinnedLength = 38;
    private const double LockLength = 34;
    private const double PagerLength = 30;
    private const double HorizontalArrowWidth = 26;
    private const double HorizontalPairWidth = 40;
    private const double SmallLabelPx = 11;
    private const double ProfileIconPx = 22;
    private const double ActiveDotPx = 8;

    private readonly DockBarViewModel _viewModel;
    private readonly bool _vertical;
    private readonly StackPanel _root;
    private readonly StackPanel _tiles;
    private readonly List<(
        DockTileViewModel ViewModel,
        ShortcutTile Control,
        PropertyChangedEventHandler Handler
    )> _tileControls = [];
    private readonly ShortcutTile _close;
    private readonly ShortcutTile _expand;
    private readonly TouchButton _frequents;
    private readonly TextBlock _frequentsLabel;
    private readonly TextBlock _stickyLabel;
    private readonly TouchButton _profile;
    private readonly TextBlock _profileName;
    private readonly SymbolIcon _profileIcon;
    private readonly SymbolIcon _profileCaret;
    private readonly Ellipse _activeDot;
    private readonly TouchButton _pill;
    private readonly ShortcutTile _previous;
    private readonly ShortcutTile _next;
    private readonly TextBlock _pageLabel;
    private readonly FrameworkElement _verticalPager;
    private readonly ShortcutTile _search;
    private readonly ShortcutTile _repeat;
    private readonly TouchButton _pinned;
    private readonly TouchButton _sticky;
    private readonly TouchButton _lock;
    private readonly ShortcutTile _scrollUp;
    private readonly ShortcutTile _scrollDown;
    private readonly PropertyChangedEventHandler _scrollUpHandler;
    private readonly PropertyChangedEventHandler _scrollDownHandler;
    private readonly double _inner;
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
        _vertical = DockGeometry.IsVertical(side);
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Panel));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        var metrics = viewModel.Metrics;
        var thickness = (_vertical ? metrics.DockBarWidthPx : metrics.DockBarHeightPx) - (2 * Pad);
        _inner = thickness;
        var crossTile = _vertical ? double.NaN : metrics.DockHorizontalTileHeightPx;

        // 1. Controls: close (chevron toward the edge) and expand.
        _close = SurfaceParts.SmallButton(CloseIcon(side), 20, viewModel.CloseBar);
        _expand = SurfaceParts.SmallButton("open_in_full", 18, viewModel.Expand);
        var controls = Pair(_close, _expand, ControlsLength, crossTile);

        // 2. What to see: ★ Frequents / profile, and the Auto/Fixed pill under it.
        _frequents = SurfaceParts.Button(
            "star",
            20,
            ButtonAppearance.Ghost,
            viewModel.ShowFrequents
        );
        _frequents.FontSize = SmallLabelPx;
        _frequentsLabel = SurfaceParts.Stack(_frequents, "star", 20);
        _profileIcon = SurfaceParts.Icon(viewModel.HandleIcon, ProfileIconPx, ColorToken.Text);
        _profileName = SurfaceParts.Text(SmallLabelPx, ColorToken.Text, bold: true);
        _profileCaret = SurfaceParts.Icon("expand_more", 14, ColorToken.Text);
        _activeDot = new Ellipse
        {
            Width = ActiveDotPx,
            Height = ActiveDotPx,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _activeDot.SetResourceReference(Shape.FillProperty, ThemeBrushKey.For(ColorToken.Accent));
        var nameRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _ = nameRow.Children.Add(_profileName);
        _ = nameRow.Children.Add(_profileCaret);
        var profileContent = new Grid();
        var profileStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _ = profileStack.Children.Add(_profileIcon);
        _ = profileStack.Children.Add(nameRow);
        profileContent.Children.Add(profileStack);
        profileContent.Children.Add(_activeDot);
        _profile = new TouchButton
        {
            Appearance = ButtonAppearance.Ghost,
            Content = profileContent,
            Padding = new Thickness(2, 2, 6, 2),
            MinWidth = 0,
            MinHeight = 0,
            Focusable = false,
            IsTabStop = false,
        };
        _profile.Click += (_, _) => viewModel.ProfileButton();
        var segmented = new StackPanel
        {
            Orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal,
        };
        Size(_frequents, FrequentsLength, crossTile, horizontalWidth: 60);
        Size(_profile, ProfileLength, crossTile, horizontalWidth: 84);
        _ = segmented.Children.Add(_frequents);
        _ = segmented.Children.Add(SurfaceParts.Divider(_vertical));
        _ = segmented.Children.Add(_profile);
        var segmentBox = new Border
        {
            Child = segmented,
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
        };
        segmentBox.SetResourceReference(
            Border.BackgroundProperty,
            ThemeBrushKey.For(ColorToken.Card)
        );
        segmentBox.SetResourceReference(
            Border.BorderBrushProperty,
            ThemeBrushKey.For(ColorToken.Line)
        );
        segmentBox.SetResourceReference(
            Border.BorderThicknessProperty,
            ThemeScope.BorderThicknessKey
        );
        _pill = SurfaceParts.Button(
            "autorenew",
            16,
            ButtonAppearance.Outline,
            viewModel.ToggleLock
        );
        _pill.FontSize = SmallLabelPx;
        Size(_pill, PillLength, crossTile, horizontalWidth: 46);

        // 3. The shortcuts, with their pager.
        _tiles = new StackPanel
        {
            Orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal,
        };
        _previous = SurfaceParts.SmallButton(
            _vertical ? "keyboard_arrow_up" : "chevron_left",
            22,
            viewModel.Previous,
            ghost: true
        );
        _next = SurfaceParts.SmallButton(
            _vertical ? "keyboard_arrow_down" : "chevron_right",
            22,
            viewModel.Next,
            ghost: true
        );
        _pageLabel = SurfaceParts.Text(SmallLabelPx, ColorToken.Muted);
        _pageLabel.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.MonoFont);
        var pagerGrid = new Grid { Height = PagerLength };
        pagerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        pagerGrid.ColumnDefinitions.Add(new ColumnDefinition());
        pagerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        if (_vertical)
        {
            Overlap(_previous, 24, PagerLength);
            Overlap(_next, 24, PagerLength);
            Grid.SetColumn(_pageLabel, 1);
            Grid.SetColumn(_next, 2);
            pagerGrid.Children.Add(_previous);
            pagerGrid.Children.Add(_pageLabel);
            pagerGrid.Children.Add(_next);
        }
        else
        {
            _previous.Width = HorizontalArrowWidth;
            _next.Width = HorizontalArrowWidth;
            _previous.Height = crossTile;
            _next.Height = crossTile;
        }

        _verticalPager = pagerGrid;

        // 4. The tools.
        _search = SurfaceParts.SmallButton("search", 20, viewModel.Search);
        _repeat = SurfaceParts.SmallButton("replay", 20, viewModel.Repeat);
        _pinned = SurfaceParts.Button(
            "push_pin",
            18,
            ButtonAppearance.Neutral,
            viewModel.TogglePinned
        );
        _pinned.FontSize = SmallLabelPx;
        _sticky = SurfaceParts.Button(
            "keyboard_command_key",
            18,
            ButtonAppearance.Neutral,
            viewModel.ToggleSticky
        );
        _sticky.FontSize = SmallLabelPx;
        _stickyLabel = SurfaceParts.Stack(_sticky, "keyboard_command_key", 18);
        _lock = SurfaceParts.Button(
            "lock_open",
            18,
            ButtonAppearance.Neutral,
            viewModel.TogglePinOpen
        );
        _lock.FontSize = SmallLabelPx;
        Size(_pinned, PinnedLength, crossTile, horizontalWidth: 56);
        Size(_sticky, PinnedLength, crossTile, horizontalWidth: 56);
        Size(_lock, LockLength, crossTile, horizontalWidth: 56);
        (_scrollUp, _scrollUpHandler) = DockTileFactory.Create(
            viewModel.ScrollUp,
            double.NaN,
            double.NaN,
            22,
            SmallLabelPx
        );
        (_scrollDown, _scrollDownHandler) = DockTileFactory.Create(
            viewModel.ScrollDown,
            double.NaN,
            double.NaN,
            22,
            SmallLabelPx
        );
        HideLabel(_scrollUp);
        HideLabel(_scrollDown);
        var tools = new StackPanel
        {
            Orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal,
        };
        _ = tools.Children.Add(Pair(_search, _repeat, ToolLength, crossTile));
        _ = tools.Children.Add(_pinned);
        _ = tools.Children.Add(_sticky);
        _ = tools.Children.Add(_lock);
        _ = tools.Children.Add(Pair(_scrollUp, _scrollDown, ToolLength, crossTile));
        foreach (FrameworkElement tool in tools.Children)
        {
            tool.Margin = _vertical ? new Thickness(0, 0, 0, 4) : new Thickness(0, 0, 4, 0);
        }

        _root = new StackPanel
        {
            Orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal,
            Margin = new Thickness(Pad),
        };
        if (_vertical)
        {
            _root.Width = thickness;
        }
        else
        {
            _root.Height = thickness;
        }

        foreach (
            var zone in new FrameworkElement[]
            {
                controls,
                segmentBox,
                _pill,
                SurfaceParts.Divider(_vertical),
                _vertical ? _tiles : HorizontalTiles(),
                _vertical ? _verticalPager : new Border(),
                SurfaceParts.Divider(_vertical),
                tools,
            }
        )
        {
            zone.Margin = _vertical ? new Thickness(0, 0, 0, Gap) : new Thickness(0, 0, Gap, 0);
            _ = _root.Children.Add(zone);
        }

        Content = _root;
        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Labels.PropertyChanged += OnViewModelChanged;
        _viewModel.PageTiles.CollectionChanged += OnPageChanged;
        LayoutUpdated += (_, _) => MeasureTileSpace();
        RebuildTiles();
        Refresh();
    }

    /// <summary>The edge of this bar.</summary>
    public DockSide Side { get; }

    /// <summary>The monitor it is on: the longest it may be comes from its work area (PES-005).</summary>
    public DisplayMonitor? Monitor { get; set; }

    /// <summary>📌 Pinned, where its window goes beside (PES-010).</summary>
    public PhysicalRect PinnedButtonBounds => PhysicalBounds(_pinned, inflate: false);

    /// <summary>Sticky keys, where its window goes beside.</summary>
    public PhysicalRect StickyButtonBounds => PhysicalBounds(_sticky, inflate: false);

    /// <summary>The shortcut tiles of the page, in order.</summary>
    public IReadOnlyList<ShortcutTile> TileControls =>
        [.. _tileControls.Select(static t => t.Control)];

    /// <summary>The buttons of the bar, in order: close, expand, Frequents, profile, Auto/Fixed, search, repeat, pinned, sticky, lock.</summary>
    public IReadOnlyList<Control> Buttons =>
        [_close, _expand, _frequents, _profile, _pill, _search, _repeat, _pinned, _sticky, _lock];

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets()
    {
        yield return SurfaceTarget.Button(_close, _viewModel.CloseBar);
        yield return SurfaceTarget.Button(_expand, _viewModel.Expand);
        yield return SurfaceTarget.Button(_frequents, _viewModel.ShowFrequents);
        yield return SurfaceTarget.Button(_profile, _viewModel.ProfileButton);
        yield return SurfaceTarget.Button(_pill, _viewModel.ToggleLock);
        foreach (var (viewModel, control, _) in _tileControls)
        {
            yield return SurfaceTarget.For(control, viewModel);
        }

        yield return SurfaceTarget.Button(_previous, _viewModel.Previous);
        yield return SurfaceTarget.Button(_next, _viewModel.Next);
        yield return SurfaceTarget.Button(_search, _viewModel.Search);
        yield return SurfaceTarget.Button(_repeat, _viewModel.Repeat);
        yield return SurfaceTarget.Button(_pinned, _viewModel.TogglePinned);
        yield return SurfaceTarget.Button(_sticky, _viewModel.ToggleSticky);
        yield return SurfaceTarget.Button(_lock, _viewModel.TogglePinOpen);
        yield return SurfaceTarget.For(_scrollUp, _viewModel.ScrollUp);
        yield return SurfaceTarget.For(_scrollDown, _viewModel.ScrollDown);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Labels.PropertyChanged -= OnViewModelChanged;
        _viewModel.PageTiles.CollectionChanged -= OnPageChanged;
        DockTileFactory.Detach(_viewModel.ScrollUp, _scrollUpHandler);
        DockTileFactory.Detach(_viewModel.ScrollDown, _scrollDownHandler);
        foreach (var (viewModel, _, handler) in _tileControls)
        {
            DockTileFactory.Detach(viewModel, handler);
        }

        base.OnClosed(e);
    }

    private static void Dimmed(Control control, bool enabled) =>
        control.SetResourceReference(
            ForegroundProperty,
            ThemeBrushKey.For(enabled ? ColorToken.Text : ColorToken.Line)
        );

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

    private void Size(FrameworkElement element, double length, double cross, double horizontalWidth)
    {
        if (_vertical)
        {
            element.Height = length;
        }
        else
        {
            element.Width = horizontalWidth;
            element.Height = cross;
        }
    }

    /// <summary>
    /// Two buttons side by side on a side edge and one above the other on the top and bottom ones, as in the prototype;
    /// each answers on 44 × 44 around its center (REG-02: the targets overlap and the nearest center wins).
    /// </summary>
    private UniformGrid Pair(
        FrameworkElement first,
        FrameworkElement second,
        double length,
        double cross
    )
    {
        var height = _vertical ? length : cross;
        var width = _vertical ? _inner : HorizontalPairWidth;
        var grid = new UniformGrid
        {
            Rows = _vertical ? 1 : 2,
            Columns = _vertical ? 2 : 1,
            Height = height,
            Width = width,
        };
        var cellWidth = _vertical ? width / 2 : width;
        var cellHeight = _vertical ? height : height / 2;
        Overlap(first, cellWidth, cellHeight);
        Overlap(second, cellWidth, cellHeight);
        _ = grid.Children.Add(first);
        _ = grid.Children.Add(second);
        return grid;
    }

    /// <summary>
    /// Draws <paramref name="element"/> in its cell less a gap of 2 while its 44 × 44 target overflows the cell around
    /// its center (REG-02), as the header of the panel does: the negative margins keep the cell's measure.
    /// </summary>
    private static void Overlap(FrameworkElement element, double cellWidth, double cellHeight)
    {
        var minimum = Clicalo.UI.Wpf.Controls.TouchTarget.MinimumSize;
        element.Width = Math.Max(1, cellWidth - 2);
        element.Height = Math.Max(1, cellHeight - 2);
        var x = Math.Max(0, (minimum - cellWidth) / 2);
        var y = Math.Max(0, (minimum - cellHeight) / 2);
        element.Margin = new Thickness(-x, -y, -x, -y);
    }

    private StackPanel HorizontalTiles()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        _ = row.Children.Add(_previous);
        _ = row.Children.Add(_tiles);
        _ = row.Children.Add(_next);
        return row;
    }

    private void OnPageChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RebuildTiles();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void RebuildTiles()
    {
        foreach (var (viewModel, _, handler) in _tileControls)
        {
            DockTileFactory.Detach(viewModel, handler);
        }

        _tileControls.Clear();
        _tiles.Children.Clear();
        var metrics = _viewModel.Metrics;
        foreach (var tile in _viewModel.PageTiles)
        {
            var (control, handler) = DockTileFactory.Create(
                tile,
                _vertical ? double.NaN : metrics.DockHorizontalTileWidthPx,
                _vertical ? metrics.DockVerticalTileHeightPx : metrics.DockHorizontalTileHeightPx,
                metrics.DockTileIconPx,
                Math.Max(TypeScale.Minimum, metrics.DockTileLabelPx)
            );
            control.Margin =
                _tiles.Children.Count == 0 ? new Thickness(0)
                : _vertical ? new Thickness(0, Gap, 0, 0)
                : new Thickness(Gap, 0, 0, 0);
            _tileControls.Add((tile, control, handler));
            _ = _tiles.Children.Add(control);
        }

        RefreshTargets();
    }

    private void Refresh()
    {
        var vm = _viewModel;
        var labels = vm.Labels;
        var state = vm.State;
        Title = labels.OpenBar;
        _close.AccessibleName = labels.HideBar;
        _expand.AccessibleName = labels.Expand;
        _frequentsLabel.Text = labels.Frequents;
        SurfaceParts.Name(_frequents, labels.Frequents);
        _frequents.Appearance =
            state?.Frequents == true ? ButtonAppearance.Accent : ButtonAppearance.Ghost;
        _profileIcon.Symbol = state is { ProfileIcon.Length: > 0 } ? state.ProfileIcon : "apps";
        _profileName.Text = state?.ProfileName ?? string.Empty;
        _profileCaret.Symbol = vm.ProfileCaret;
        _activeDot.Visibility = SurfaceParts.Shown(state?.IsActiveApp == true);
        var pickerOpen = state?.Flyout == DockFlyout.Profiles;
        _profile.Appearance =
            pickerOpen ? ButtonAppearance.Neutral
            : state?.Frequents == true ? ButtonAppearance.Ghost
            : ButtonAppearance.Accent;
        SurfaceParts.Name(_profile, labels.SwitchProfile);
        _pill.Symbol = vm.AutoFixedIcon;
        _pill.Content = vm.AutoFixedLabel;
        _pill.Appearance =
            state?.IsFixed == true ? ButtonAppearance.Danger : ButtonAppearance.Outline;
        SurfaceParts.Name(_pill, vm.AutoFixedName);
        _pill.ToolTip = vm.AutoFixedName;

        _previous.IsEnabled = vm.CanGoPrevious;
        _next.IsEnabled = vm.CanGoNext;
        _previous.AccessibleName = labels.PreviousPage;
        _next.AccessibleName = labels.NextPage;
        _pageLabel.Text = vm.PageLabel;
        _verticalPager.Visibility = SurfaceParts.Shown(_vertical && vm.HasPages);
        _previous.Visibility = SurfaceParts.Shown(vm.HasPages);
        _next.Visibility = SurfaceParts.Shown(vm.HasPages);

        _search.AccessibleName = labels.Search;
        _repeat.AccessibleName = labels.Repeat;
        _repeat.IsEnabled = state?.CanRepeat == true;

        // CUA-004, AVI-004: what leads nowhere dims, and UI Automation hears it as not enabled, not by color alone.
        Dimmed(_repeat, _repeat.IsEnabled);
        Dimmed(_previous, vm.CanGoPrevious);
        Dimmed(_next, vm.CanGoNext);
        _pinned.Content = labels.PinnedShort;
        SurfaceParts.Name(_pinned, labels.Pinned);
        _pinned.Visibility = SurfaceParts.Shown(vm.ShowsPinned);
        _pinned.Appearance =
            state?.Flyout == DockFlyout.Pinned
                ? ButtonAppearance.Outline
                : ButtonAppearance.Neutral;
        _stickyLabel.Text = labels.Sticky;
        SurfaceParts.Name(_sticky, labels.Sticky);
        _sticky.Visibility = SurfaceParts.Shown(vm.ShowsSticky);
        _sticky.Appearance =
            state?.Flyout == DockFlyout.Sticky
                ? ButtonAppearance.Outline
                : ButtonAppearance.Neutral;
        var pinOpen = state?.Dock.PinOpen == true;
        _lock.Symbol = pinOpen ? "lock" : "lock_open";
        _lock.Content = vm.PinLabel;
        _lock.Appearance = pinOpen ? ButtonAppearance.Accent : ButtonAppearance.Neutral;
        SurfaceParts.Name(_lock, vm.PinName);
        _lock.ToolTip = vm.PinName;
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
