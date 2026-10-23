using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// A window beside the open bar (blueprint §8.1, a <see cref="NonActivatingWindow"/> child, never a <c>Popup</c>), in
/// direction C (D12) with the same pieces as the panel: an <see cref="Overline"/> header that stays while the content
/// scrolls in a <see cref="ThemedScrollViewer"/>, <see cref="Space.S3"/> inside, on <c>win</c> with its line.
/// <list type="bullet">
/// <item>📌 «Pinned» (PES-010): 230 wide, the header 📌 [always] and the shortcuts of Always visible in 2 columns of
/// <see cref="ControlHeight.Large"/>, as the row of Always visible of the panel, with the icon in
/// <see cref="IconBox.Small"/> so their names have two lines; with a larger text the tiles grow by what those two
/// lines grow (CUA-011), so a name is never cut where it would fit on two lines. After using one it closes, unless it
/// holds or latches;</item>
/// <item>the profile grid (PES-011): 236 wide, the header [pickProfile] and the same grid as SEL-003, without a box of
/// its own (two levels: the window and its tiles);</item>
/// <item>sticky keys (PES-008, AUD-13): Ctrl, Alt, Shift and Win with their three states;</item>
/// <item>the arrows (D21): ↑ above, ← and → beside it and ↓ below, as on a keyboard, each a key of
/// <see cref="ControlHeight.Large"/>; it stays open while they are used, since moving through a document takes many
/// taps, and the sticky keys go with them (Mayús fija + → selects).</item>
/// </list>
/// Its size comes from what it shows when it opens, up to the work area less 140; taller content scrolls, with the
/// finger: a contact that slides past the «cancel if you slide» distance scrolls and activates nothing (TAC-004), and
/// what is scrolled out of sight is no target. The shortcuts of «Pinned» open their menu with a long press and show the
/// marks of test mode (PES-010, PES-014). Where it goes is the surface set's (<c>DockGeometry.Beside</c>).
/// </summary>
public sealed class DockFlyoutWindow : TouchSurface
{
    private const double PinnedWidth = 230;
    private const double ProfilesWidth = 236;

    private readonly DockBarViewModel _viewModel;
    private readonly UniformGrid? _pinnedGrid;
    private readonly PickerGridView? _picker;
    private readonly StickyKeysRowView? _sticky;
    private readonly Grid? _arrows;
    private readonly Overline _header;
    private readonly ScrollViewer _scroller;
    private readonly TouchSurfaceScroll _scroll = new();
    private readonly List<(
        DockTileViewModel ViewModel,
        ShortcutTile Control,
        PropertyChangedEventHandler Handler
    )> _tiles = [];
    private readonly List<Clicalo.UI.Wpf.Surfaces.Panel.TestMode.TestMarkBadge> _badges = [];

    /// <summary>Creates the window of <paramref name="flyout"/> on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="flyout">Which window: Pinned, Profiles, Sticky or Arrows.</param>
    /// <param name="viewModel">The bar (its pinned shortcuts and its texts).</param>
    /// <param name="panel">The panel: its profile grid and its sticky keys.</param>
    /// <param name="registry">The surfaces of the process.</param>
    /// <param name="time">The clock of the pointer layer.</param>
    /// <param name="theme">The theme service.</param>
    /// <param name="touch">The touch filter.</param>
    /// <param name="holdEnded">Where the end of a hold goes, by contact (INV-9).</param>
    public DockFlyoutWindow(
        DockFlyout flyout,
        DockBarViewModel viewModel,
        PanelViewModel panel,
        SurfaceRegistry registry,
        TimeProvider time,
        ThemeService theme,
        TouchSettings touch,
        Action<uint, ContactSummary, HoldEndReason> holdEnded
    )
        : base(
            new SurfaceId(SurfaceKind.SideWindow, (int)flyout),
            registry,
            time,
            theme,
            touch,
            holdEnded,
            SurfaceLook.SideWindow
        )
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(panel);
        Flyout = flyout;
        _viewModel = viewModel;
        Modes = viewModel.Modes;
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        SetResourceReference(BorderThicknessProperty, ThemeScope.BorderThicknessKey);

        // One section label, as everywhere (D12): the Overline alone, as in the panel («SIEMPRE VISIBLE») and in every
        // window beside the bar, Profiles included.
        _header = new Overline { VerticalAlignment = VerticalAlignment.Center };
        var headerRow = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(Space.S3, Space.S3, Space.S3, Space.S2),
        };
        _ = headerRow.Children.Add(_header);
        FrameworkElement body;
        switch (flyout)
        {
            case DockFlyout.Pinned:
                _pinnedGrid = new UniformGrid { Columns = 2, Margin = new Thickness(-Space.S1) };
                body = _pinnedGrid;
                viewModel.PinnedTiles.CollectionChanged += OnPinnedChanged;
                Width = PinnedWidth;
                break;
            case DockFlyout.Profiles:
                _picker = new PickerGridView(panel.Picker) { ShowsAlways = true, IsInset = false };

                // The window is the surface and the profiles its tiles: no third box between them (D12).
                _picker.Margin = new Thickness(0);
                _picker.Padding = new Thickness(0);
                _picker.Background = Brushes.Transparent;
                _picker.BorderThickness = new Thickness(0);
                body = _picker;
                Width = ProfilesWidth;
                break;
            case DockFlyout.Arrows:
                _arrows = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
                for (var i = 0; i < 3; i++)
                {
                    _arrows.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    _arrows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                body = _arrows;

                // Three keys of 64 with their gaps, the insets and the channel of the thin bar: the → was cut at 230.
                Width = (3 * (ControlHeight.Large + (2 * Space.S1))) + (2 * Space.S3) + Space.S3;
                break;
            default:
                // Two columns and as many rows as the keys need: the row of the panel is a single row of four.
                _sticky = new StickyKeysRowView(panel.Sticky)
                {
                    Columns = 2,
                    Rows = 0,
                    // Each key keeps its half of the gap around it; this takes it back at the edges, as the grid of
                    // «Pinned» does, so the keys line up with the header.
                    Margin = new Thickness(-Space.S1),
                };
                body = _sticky;
                Width = PinnedWidth;
                break;
        }

        SizeToContent = SizeToContent.Height;
        FixedWidth = Width;

        // The channel of the thin bar sits in the right padding: the content is S3 from both edges.
        _scroller = new ThemedScrollViewer
        {
            PansWithMouse = false,
            Content = body,
            Margin = new Thickness(Space.S3, 0, Space.S1, Space.S3),
        };
        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(headerRow, Dock.Top);
        _ = root.Children.Add(headerRow);
        _ = root.Children.Add(_scroller);
        Content = root;
        viewModel.Labels.PropertyChanged += OnLabelsChanged;
        Theme.Changed += OnThemeChanged;
        RebuildPinned();
        Relabel();
    }

    /// <summary>Which window this is.</summary>
    public DockFlyout Flyout { get; }

    /// <summary>The tallest it may be, in logical pixels (PES-010, PES-011); taller content scrolls.</summary>
    public double MaxContentHeight
    {
        get => MaxHeight;
        set => MaxHeight = value;
    }

    /// <summary>The shortcut tiles of «Pinned», in order.</summary>
    public IReadOnlyList<ShortcutTile> TileControls => [.. _tiles.Select(static t => t.Control)];

    /// <summary>
    /// Where the tile of <paramref name="shortcut"/> is on screen, for its menu beside it (CUA-014, PES-010), or empty
    /// when the window does not show it.
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    public PhysicalRect TileBounds(ShortcutId shortcut)
    {
        foreach (var (viewModel, control, _) in _tiles)
        {
            if (viewModel.Id == shortcut)
            {
                return PhysicalBounds(control, inflate: false);
            }
        }

        return PhysicalRect.Empty;
    }

    /// <summary>The zone that scrolls when the window is taller than it may be (TAC-004).</summary>
    public ScrollViewer Scroller => _scroller;

    /// <inheritdoc />
    protected override IEnumerable<SurfaceTarget> CollectTargets()
    {
        // TAC-004, REG-02: what is scrolled out of sight is no target; its touch margin would take touches from
        // what shows.
        var view = PhysicalBounds(_scroller, inflate: false);

        // TAC-004, EJE-004: while «Pinned» scrolls, a Mantener waits to see that the finger is not scrolling.
        var scrolls = _scroller.ScrollableHeight > 0;
        foreach (var (viewModel, control, _) in _tiles)
        {
            if (InView(view, control))
            {
                // An arrow is a key of the bar, not a shortcut of the person: it has no menu.
                yield return SurfaceTarget.For(
                    control,
                    viewModel,
                    longPress: Modes is not null && !viewModel.IsBarTool
                ) with
                {
                    InScrollZone = scrolls,
                };
            }
        }

        foreach (var target in _picker?.TapTargets ?? [])
        {
            if (InView(view, target.Element))
            {
                yield return SurfaceTarget.Button(target.Element, target.Tap);
            }
        }

        foreach (var target in _sticky?.TapTargets ?? [])
        {
            if (InView(view, target.Element))
            {
                yield return SurfaceTarget.Button(target.Element, target.Tap);
            }
        }
    }

    /// <inheritdoc />
    protected override bool TrackContact(in PointerSample sample)
    {
        switch (sample.Phase)
        {
            case PointerPhase.Down:
                if (
                    _scroller.ScrollableHeight > 0
                    && PhysicalBounds(_scroller, inflate: false).Contains(sample.Position)
                )
                {
                    _scroll.Down(sample.PointerId, sample.Position.Y, _scroller.VerticalOffset);
                }

                return false;

            case PointerPhase.Move:
                if (
                    _scroll.Move(sample.PointerId, sample.Position.Y, DragThresholdPx, DpiScale)
                    is not { } offset
                )
                {
                    return false;
                }

                _scroller.ScrollToVerticalOffset(offset);
                return true;

            default:
                return _scroll.Up(sample.PointerId);
        }
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Labels.PropertyChanged -= OnLabelsChanged;
        _viewModel.PinnedTiles.CollectionChanged -= OnPinnedChanged;
        Theme.Changed -= OnThemeChanged;
        _picker?.Detach();
        _sticky?.Detach();
        foreach (var (viewModel, _, handler) in _tiles)
        {
            DockTileFactory.Detach(viewModel, handler);
        }

        foreach (var badge in _badges)
        {
            badge.Detach();
        }

        base.OnClosed(e);
    }

    /// <summary>
    /// Whether the center of <paramref name="element"/> shows inside <paramref name="view"/>, the scroller on screen;
    /// before the window has a handle nothing is filtered.
    /// </summary>
    private static bool InView(PhysicalRect view, FrameworkElement element) =>
        view.IsEmpty
        || (
            PhysicalBounds(element, inflate: false) is { IsEmpty: false } bounds
            && view.Contains(bounds.Center)
        );

    private void OnLabelsChanged(object? sender, PropertyChangedEventArgs e) => Relabel();

    private void OnPinnedChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RebuildPinned();

    private void OnThemeChanged(object? sender, EventArgs e) => RebuildPinned();

    /// <summary>
    /// The height of a shortcut of «Pinned»: <see cref="ControlHeight.Large"/>, which holds its icon and two lines of
    /// its name at 100 %, plus what those two lines grow with the text scale (CUA-011).
    /// </summary>
    private double PinnedTileHeight() =>
        ControlHeight.Large
        + (2 * TextStyles.LabelSmall.LineHeight * (Theme.TextScalePercent - 100) / 100d);

    private void Relabel()
    {
        var labels = _viewModel.Labels;
        _header.Text = Flyout switch
        {
            DockFlyout.Pinned => labels.Pinned,
            DockFlyout.Profiles => labels.PickProfile,
            DockFlyout.Arrows => labels.Arrows,
            _ => labels.Sticky,
        };
        Title = _header.Text;
    }

    /// <summary>The four arrows in their cross: ↑ in the middle of the top row, ← and → in the middle one, ↓ below.</summary>
    private void RebuildArrows(Grid cross)
    {
        foreach (var (viewModel, _, handler) in _tiles)
        {
            DockTileFactory.Detach(viewModel, handler);
        }

        _tiles.Clear();
        cross.Children.Clear();
        (int Row, int Column)[] places = [(0, 1), (1, 0), (1, 2), (2, 1)];
        for (var i = 0; i < _viewModel.ArrowTiles.Count && i < places.Length; i++)
        {
            var tile = _viewModel.ArrowTiles[i];
            // The arrow is the whole content of its key: drawn large, it reads at a glance (D21 review: thin at 24).
            var (control, handler) = DockTileFactory.Create(
                tile,
                ControlHeight.Large,
                ControlHeight.Large,
                IconBox.Large,
                1,
                Modes
            );
            DockTileFactory.ShowIconOnly(control);

            // Keys, not shortcuts of a category: the arrow is drawn in the text color, as the keys of Sticky keys.
            control.SetResourceReference(
                ShortcutTile.CategoryTintProperty,
                ThemeBrushKey.For(ColorToken.Text)
            );
            control.Margin = new Thickness(Space.S1);
            Grid.SetRow(control, places[i].Row);
            Grid.SetColumn(control, places[i].Column);
            _tiles.Add((tile, control, handler));
            _ = cross.Children.Add(control);
        }

        RefreshTargets();
    }

    private void RebuildPinned()
    {
        if (_arrows is not null)
        {
            RebuildArrows(_arrows);
            return;
        }

        if (_pinnedGrid is null)
        {
            return;
        }

        foreach (var (viewModel, _, handler) in _tiles)
        {
            DockTileFactory.Detach(viewModel, handler);
        }

        foreach (var badge in _badges)
        {
            badge.Detach();
        }

        _badges.Clear();
        _tiles.Clear();
        _pinnedGrid.Children.Clear();
        foreach (var tile in _viewModel.PinnedTiles)
        {
            var (control, handler) = DockTileFactory.Create(
                tile,
                double.NaN,
                PinnedTileHeight(),
                IconBox.Small,
                2,
                Modes
            );
            var (cell, badge) = DockTileFactory.Cell(control, tile, Modes);
            cell.Margin = new Thickness(Space.S1);
            if (badge is not null)
            {
                _badges.Add(badge);
            }

            _tiles.Add((tile, control, handler));
            _ = _pinnedGrid.Children.Add(cell);
        }

        RefreshTargets();
    }
}
