using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.PanelLayout;
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
/// A window beside the open bar (blueprint §8.1, a <see cref="NonActivatingWindow"/> child, never a <c>Popup</c>):
/// <list type="bullet">
/// <item>📌 «Pinned» (PES-010): 230 wide, the header 📌 [always] and the shortcuts of Always visible in 2 columns of 68
/// high; after using one it closes, unless it holds or latches;</item>
/// <item>the profile grid (PES-011): 236 wide, the header [pickProfile] and the same grid as SEL-003;</item>
/// <item>sticky keys (PES-008, AUD-13): Ctrl, Alt, Shift and Win with their three states.</item>
/// </list>
/// It scrolls when taller than the work area less 140, with the finger: a contact that slides past the «cancel if
/// you slide» distance scrolls and activates nothing (TAC-004), and what is scrolled out of sight is no target. The
/// shortcuts of «Pinned» open their menu with a long press and show the marks of test mode (PES-010, PES-014). Where it
/// goes is the surface set's (<c>DockGeometry.Beside</c>).
/// </summary>
public sealed class DockFlyoutWindow : TouchSurface
{
    private const double PinnedWidth = 230;
    private const double ProfilesWidth = 236;
    private const double HeaderPx = 12;
    private const double PinnedTileHeight = 68;
    private const double PinnedIconPx = 24;
    private const double PinnedLabelPx = 12;
    private const double Gap = 6;

    private readonly DockBarViewModel _viewModel;
    private readonly UniformGrid? _pinnedGrid;
    private readonly PickerGridView? _picker;
    private readonly StickyKeysRowView? _sticky;
    private readonly TextBlock _header;
    private readonly ScrollViewer _scroller;
    private readonly TouchSurfaceScroll _scroll = new();
    private readonly List<(
        DockTileViewModel ViewModel,
        ShortcutTile Control,
        PropertyChangedEventHandler Handler
    )> _tiles = [];
    private readonly List<Clicalo.UI.Wpf.Surfaces.Panel.TestMode.TestMarkBadge> _badges = [];

    /// <summary>Creates the window of <paramref name="flyout"/> on the UI thread of <paramref name="registry"/>.</summary>
    /// <param name="flyout">Which window: Pinned, Profiles or Sticky.</param>
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

        _header = SurfaceParts.Text(HeaderPx, ColorToken.Muted, bold: true);
        _header.HorizontalAlignment = HorizontalAlignment.Left;
        var headerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(6, 4, 6, 2),
        };
        if (flyout == DockFlyout.Pinned)
        {
            var pin = SurfaceParts.Icon("push_pin", 16, ColorToken.Muted);
            pin.Margin = new Thickness(0, 0, 6, 0);
            _ = headerRow.Children.Add(pin);
        }

        _ = headerRow.Children.Add(_header);
        var body = new StackPanel();
        _ = body.Children.Add(headerRow);
        switch (flyout)
        {
            case DockFlyout.Pinned:
                _pinnedGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, Gap, 0, 0) };
                _ = body.Children.Add(_pinnedGrid);
                viewModel.PinnedTiles.CollectionChanged += OnPinnedChanged;
                Width = PinnedWidth;
                break;
            case DockFlyout.Profiles:
                _picker = new PickerGridView(panel.Picker) { Margin = new Thickness(0, Gap, 0, 0) };
                _ = body.Children.Add(_picker);
                Width = ProfilesWidth;
                break;
            default:
                _sticky = new StickyKeysRowView(panel.Sticky)
                {
                    Columns = 2,
                    Margin = new Thickness(0, Gap, 0, 0),
                };
                _ = body.Children.Add(_sticky);
                Width = PinnedWidth;
                break;
        }

        SizeToContent = SizeToContent.Height;
        FixedWidth = Width;
        _scroller = new SurfaceScrollViewer { Content = body, Padding = new Thickness(8) };
        Content = _scroller;
        viewModel.Labels.PropertyChanged += OnLabelsChanged;
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
                yield return SurfaceTarget.For(
                    control,
                    viewModel,
                    longPress: Modes is not null
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

    private void Relabel()
    {
        var labels = _viewModel.Labels;
        _header.Text = Flyout switch
        {
            DockFlyout.Pinned => labels.Pinned,
            DockFlyout.Profiles => labels.PickProfile,
            _ => labels.Sticky,
        };
        Title = _header.Text;
    }

    private void RebuildPinned()
    {
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
                PinnedTileHeight,
                PinnedIconPx,
                PinnedLabelPx,
                Modes
            );
            var (cell, badge) = DockTileFactory.Cell(control, tile, Modes);
            cell.Margin = new Thickness(Gap / 2);
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
