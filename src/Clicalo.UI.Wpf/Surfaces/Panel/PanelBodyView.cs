using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.PanelLayout;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel.ContextMenu;
using Clicalo.UI.Wpf.Surfaces.Panel.TestMode;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The body of the Full panel, in the order of PAN-007 from the Always visible row down: the Always visible row, the
/// sticky modifiers, the profile selector, the profile grid, the grid area (the shortcut grid or the empty profile
/// card), the pager and the notice bar. The administrator notice goes higher (under the panic strip), so it is
/// exposed apart as <see cref="AdminNotice"/> for the surface to place. The body is as wide as PAN-002 says and only
/// projects <see cref="PanelViewModel"/>; the surface feeds back the space it measured for the grid
/// (<see cref="MeasureGridSpace"/>) and the swipes (<see cref="Swiped"/>).
/// </summary>
public sealed class PanelBodyView : StackPanel
{
    private const double NoResultsPx = 14;

    private readonly PanelViewModel _viewModel;

    /// <summary>Creates the body of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The panel.</param>
    public PanelBodyView(PanelViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Orientation = Orientation.Vertical;
        AdminNotice = new AdminNoticeView(viewModel.Admin);
        Strip = new AlwaysVisibleRowView(viewModel);
        Sticky = new StickyKeysRowView(viewModel.Sticky);
        Selector = new SelectorRowView(viewModel.Selector);
        Picker = new PickerGridView(viewModel.Picker);
        ShortcutGrid = new ShortcutGridView(viewModel);
        EmptyState = new EmptyStateView(viewModel.Empty);
        Pager = new PagerView(viewModel.Pager);
        CompactRow = new CompactRowView(
            viewModel.Selector,
            viewModel.Pager,
            () => _viewModel.Layers.EmptyProfile
        );
        Notices = new NoticeBarView(viewModel.Notices);
        NoResults = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(12, 16, 12, 16),
            Visibility = Visibility.Collapsed,
        };
        NoResults.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(NoResultsPx));
        NoResults.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        GridArea = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        GridArea.Children.Add(ShortcutGrid);
        GridArea.Children.Add(EmptyState);
        GridArea.Children.Add(NoResults);

        Children.Add(Strip);
        Children.Add(Sticky);
        Children.Add(Selector);
        Children.Add(Picker);
        Children.Add(GridArea);
        Children.Add(Pager);
        Children.Add(CompactRow);
        Children.Add(Notices);

        viewModel.PropertyChanged += OnPanelChanged;
        ApplyWidth();
    }

    /// <summary>The administrator notice, for the surface to place under the panic strip (PAN-007).</summary>
    public AdminNoticeView AdminNotice { get; }

    /// <summary>The Always visible row.</summary>
    public AlwaysVisibleRowView Strip { get; }

    /// <summary>The sticky modifiers row.</summary>
    public StickyKeysRowView Sticky { get; }

    /// <summary>The profile selector.</summary>
    public SelectorRowView Selector { get; }

    /// <summary>The profile grid.</summary>
    public PickerGridView Picker { get; }

    /// <summary>The shortcut grid.</summary>
    public ShortcutGridView ShortcutGrid { get; }

    /// <summary>The empty profile card.</summary>
    public EmptyStateView EmptyState { get; }

    /// <summary>The pager.</summary>
    public PagerView Pager { get; }

    /// <summary>The bottom row of the Compact view (VCO-002).</summary>
    public CompactRowView CompactRow { get; }

    /// <summary>The notice bar.</summary>
    public NoticeBarView Notices { get; }

    /// <summary>[noResults], in place of the grid while a search with text finds nothing (BUS-005).</summary>
    public TextBlock NoResults { get; }

    /// <summary>The area of the grid: the only part that shrinks when vertical space runs out (CUA-002).</summary>
    public Grid GridArea { get; }

    /// <summary>The tile menu just above the grid area (CUA-014), once the layers are attached.</summary>
    public TileContextMenuView? TileMenu { get; private set; }

    /// <summary>The test mode indicator just above the notice bar (TAC-008), once the layers are attached.</summary>
    public TestModeIndicator? TestModeIndicator { get; private set; }

    /// <summary>Every shortcut tile on screen: the page of the grid, then the Always visible row (FIJ-004).</summary>
    public IReadOnlyList<PanelTileControl> TileControls =>
        [.. ShortcutGrid.TileControls, .. Strip.TileControls];

    /// <summary>Every other tappable element on screen, the administrator notice included.</summary>
    public IEnumerable<PanelTapTarget> TapTargets =>
        AdminNotice
            .TapTargets.Concat(Strip.TapTargets)
            .Concat(Sticky.TapTargets)
            .Concat(Selector.TapTargets)
            .Concat(Picker.TapTargets)
            .Concat(EmptyState.TapTargets)
            .Concat(ShortcutGrid.TapTargets)
            .Concat(Pager.TapTargets)
            .Concat(CompactRow.TapTargets)
            .Concat(Notices.TapTargets);

    /// <summary>
    /// Measures the space left for the grid and hands it to the view model (CUA-001: the rows that fit are
    /// <b>measured</b>, never estimated; CUA-003 decides from the same measure): from the top of the grid area down to
    /// 16 px above the bottom of the work area, less everything the window shows below the grid area. Call it after
    /// every layout pass, move, resize or change of work area.
    /// </summary>
    /// <param name="window">The window that holds the body.</param>
    /// <param name="workAreaBottomDip">The bottom edge of the work area of its monitor, in the window's DIPs.</param>
    /// <returns>The space measured, in device-independent pixels.</returns>
    public double MeasureGridSpace(Window window, double workAreaBottomDip)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!GridArea.IsVisible || PresentationSource.FromVisual(GridArea) is null)
        {
            return double.NaN;
        }

        var top = GridArea.TranslatePoint(new Point(0, 0), window).Y + window.Top;
        var below =
            window.ActualHeight
            - GridArea.TranslatePoint(new Point(0, GridArea.ActualHeight), window).Y;
        // The grid draws each row with its gap (tile margins of gap / 2), so one gap is taken off before the rule counts
        // rows as tile + gap each (GridMetrics.RowsThatFit).
        var space =
            workAreaBottomDip
            - PanelSizes.Layout.PanelBottomMarginPx
            - top
            - below
            - _viewModel.Shape.GapPx;
        _viewModel.ApplyGridSpace(space);
        return space;
    }

    /// <summary>
    /// Shows or hides the bottom row of the Compact view (VCO-002) and puts the profile grid where the view wants it:
    /// under the selector in Full, above the bottom row in Compact (SEL-003).
    /// </summary>
    /// <param name="shown">Whether the row shows.</param>
    public void ApplyCompactRow(bool shown)
    {
        CompactRow.Show(shown);
        var below = _viewModel.Layout.Compact;
        var placed = below
            ? Children.IndexOf(Picker) == Children.IndexOf(CompactRow) - 1
            : Children.IndexOf(Picker) == Children.IndexOf(Selector) + 1;
        if (placed)
        {
            return;
        }

        Children.Remove(Picker);
        Children.Insert(
            below ? Children.IndexOf(CompactRow) : Children.IndexOf(Selector) + 1,
            Picker
        );
    }

    /// <summary>
    /// Hosts the layers of the panel in the body: the tile menu above the grid area, the test mode indicator above the
    /// notice bar, and the marks, the × and «+ [add]» on the tiles (docs/04, prototype).
    /// </summary>
    /// <param name="layers">The layers.</param>
    public void AttachLayers(PanelLayerModels layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        TileMenu = new TileContextMenuView(layers.Menu);
        Children.Insert(Children.IndexOf(GridArea), TileMenu);
        TestModeIndicator = new TestModeIndicator(layers.TestMode);
        Children.Insert(Children.IndexOf(Notices), TestModeIndicator);
        Strip.AttachLayers(layers);
        ShortcutGrid.AttachLayers(layers);
    }

    /// <summary>A horizontal swipe that started on the grid area (CUA-005).</summary>
    /// <param name="towardLeft">Whether the finger moved toward the left.</param>
    public void Swiped(bool towardLeft) => _viewModel.Pager.Swiped(towardLeft);

    /// <summary>Stops following the view model (when the surface closes).</summary>
    public void Detach()
    {
        _viewModel.PropertyChanged -= OnPanelChanged;
        AdminNotice.Detach();
        Strip.Detach();
        Sticky.Detach();
        Selector.Detach();
        Picker.Detach();
        ShortcutGrid.Detach();
        EmptyState.Detach();
        Pager.Detach();
        CompactRow.Detach();
        Notices.Detach();
        TileMenu?.Detach();
        TestModeIndicator?.Detach();
    }

    private void OnPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            e.PropertyName
            is nameof(PanelViewModel.Layout)
                or nameof(PanelViewModel.Layers)
                or nameof(PanelViewModel.ShowsNoResults)
                or nameof(PanelViewModel.NoResultsText)
        )
        {
            ApplyWidth();
        }
    }

    private void ApplyWidth()
    {
        Width = GridMetrics.PanelWidth(_viewModel.Layout);
        var noResults = _viewModel.ShowsNoResults;
        ShortcutGrid.Visibility =
            _viewModel.Layers.EmptyProfile || noResults ? Visibility.Collapsed : Visibility.Visible;
        NoResults.Text = _viewModel.NoResultsText;
        NoResults.Visibility = noResults ? Visibility.Visible : Visibility.Collapsed;
    }
}
