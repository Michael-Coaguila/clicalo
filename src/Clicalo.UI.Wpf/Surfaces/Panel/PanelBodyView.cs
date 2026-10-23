using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.PanelLayout;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.Header;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel.ContextMenu;
using Clicalo.UI.Wpf.Surfaces.Panel.TestMode;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The body of the panel in direction C (D12), the rows of PAN-007 <see cref="Space.S2"/> apart, the gap of the
/// tiles, so the rows read as one grid and not as boxes apart (D19): the Always visible
/// row, the sticky keys, the row «Qué ver», the grid of shortcuts (or the empty card, or [noResults]) and the pager; the
/// Compact view puts «Qué ver» right under the grid, above the pager (VCO-002, D21). The body has the size of its
/// design: a row that a temporary state hides (the search with text, PAN-008) keeps its place under the layer that
/// covers it; with one page the pager row takes no room, though the rows of the grid are counted as if it did (D21);
/// and the grid is as high as its rows whatever it shows. The status row, the administrator
/// notice, the profile grid, the tile menu and the test mode band are built here and placed by the surface: the status
/// row at the foot of the panel and the others as layers over the body (<see cref="PanelLayerHost"/>). It only projects
/// <see cref="PanelViewModel"/>; the surface feeds back the space it measured for the grid
/// (<see cref="MeasureGridSpace"/>) and the swipes (<see cref="Swiped"/>).
/// </summary>
public sealed class PanelBodyView : StackPanel
{
    private readonly PanelViewModel _viewModel;
    private bool _coversChooser;
    private bool _coversGrid;
    private bool _coversPager;
    private bool _coversFoot;

    /// <summary>Creates the body of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The panel.</param>
    /// <param name="header">The header, whose Auto/Fixed button goes in the row «Qué ver»; null leaves it out.</param>
    public PanelBodyView(PanelViewModel viewModel, PanelHeaderViewModel? header = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Orientation = Orientation.Vertical;
        AdminNotice = new AdminNoticeView(viewModel.Admin);
        Strip = new AlwaysVisibleRowView(viewModel);
        Sticky = new StickyKeysRowView(viewModel.Sticky);
        Selector = new SelectorRowView(viewModel.Selector, header);
        Picker = new PickerGridView(viewModel.Picker);
        ShortcutGrid = new ShortcutGridView(viewModel);
        EmptyState = new EmptyStateView(viewModel.Empty);
        Pager = new PagerView(viewModel.Pager);
        Notices = new NoticeBarView(viewModel.Notices);
        NoResults = new StyledText(string.Empty, TextStyleToken.Body, ColorToken.Muted)
        {
            MaxLines = 0,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.S3),
            Visibility = Visibility.Collapsed,
        };
        GridArea = new Grid();
        GridArea.Children.Add(ShortcutGrid);
        GridArea.Children.Add(EmptyState);
        GridArea.Children.Add(NoResults);

        Children.Add(Strip);
        Children.Add(Sticky);
        Children.Add(Selector);
        Children.Add(GridArea);
        Children.Add(Pager);

        viewModel.PropertyChanged += OnPanelChanged;
        viewModel.Empty.PropertyChanged += OnPartChanged;
        viewModel.Strip.PropertyChanged += OnPartChanged;
        viewModel.Selector.PropertyChanged += OnPartChanged;
        viewModel.Sticky.PropertyChanged += OnPartChanged;
        viewModel.Pager.PropertyChanged += OnPartChanged;
        Apply();
    }

    /// <summary>The administrator notice, for the surface to show as a layer.</summary>
    public AdminNoticeView AdminNotice { get; }

    /// <summary>The Always visible row.</summary>
    public AlwaysVisibleRowView Strip { get; }

    /// <summary>The sticky modifiers row.</summary>
    public StickyKeysRowView Sticky { get; }

    /// <summary>The row «Qué ver»: ★ Frequents, the profile and Auto/Fixed.</summary>
    public SelectorRowView Selector { get; }

    /// <summary>The profile grid, for the surface to show as a layer over the grid and the pager.</summary>
    public PickerGridView Picker { get; }

    /// <summary>The shortcut grid.</summary>
    public ShortcutGridView ShortcutGrid { get; }

    /// <summary>The empty profile card.</summary>
    public EmptyStateView EmptyState { get; }

    /// <summary>The pager.</summary>
    public PagerView Pager { get; }

    /// <summary>The status row, for the surface to put at the foot of the panel.</summary>
    public NoticeBarView Notices { get; }

    /// <summary>[noResults], in place of the grid while a search with text finds nothing (BUS-005).</summary>
    public TextBlock NoResults { get; }

    /// <summary>The area of the grid: the only part that shrinks when vertical space runs out (CUA-002).</summary>
    public Grid GridArea { get; }

    /// <summary>The tile menu (CUA-014), once the layers are attached; the surface shows it as a layer.</summary>
    public TileContextMenuView? TileMenu { get; private set; }

    /// <summary>The test mode band (TAC-008), once the layers are attached; the surface shows it over the pager row.</summary>
    public TestModeIndicator? TestModeIndicator { get; private set; }

    /// <summary>Every shortcut tile on screen: the page of the grid, then the Always visible row (FIJ-004).</summary>
    public IReadOnlyList<PanelTileControl> TileControls =>
        [.. ShortcutGrid.TileControls, .. Strip.TileControls];

    /// <summary>
    /// Every other tappable element of the rows of the body; the surface adds those of the parts it places elsewhere
    /// (the status row, the administrator notice and the profile grid, <see cref="PlacedTapTargets"/>).
    /// </summary>
    public IEnumerable<PanelTapTarget> TapTargets =>
        Strip
            .TapTargets.Concat(Sticky.TapTargets)
            .Concat(Selector.TapTargets)
            .Concat(EmptyState.TapTargets)
            .Concat(ShortcutGrid.TapTargets)
            .Concat(Pager.TapTargets);

    /// <summary>The tappable elements of the parts the surface places out of the body: layers and the status row.</summary>
    public IEnumerable<PanelTapTarget> PlacedTapTargets =>
        AdminNotice.TapTargets.Concat(Picker.TapTargets).Concat(Notices.TapTargets);

    /// <summary>
    /// How much higher the grid is <b>on screen</b> than one row of tiles, in device-independent pixels. Zero while it
    /// shows one row or something else. It is read from the rendered grid.
    /// </summary>
    public double GridSlack
    {
        get
        {
            var shape = _viewModel.Shape;
            return ShortcutGrid.IsVisible
                ? Math.Max(0, ShortcutGrid.ActualHeight - (shape.TileHeightPx + shape.GapPx))
                : 0;
        }
    }

    /// <summary>
    /// Measures the space left for the grid and hands it to the view model (CUA-001: the rows that fit are
    /// <b>measured</b>, never estimated; CUA-003 decides from the same measure): from the top of the grid area down to
    /// 16 px above the bottom of the work area, less everything the window shows below the grid area. Call it after
    /// every layout pass, move, resize or change of work area.
    /// </summary>
    /// <param name="window">The window that holds the body.</param>
    /// <param name="workAreaBottomDip">The bottom edge of the work area of its monitor, in the window's DIPs.</param>
    /// <param name="shiftAbove">
    /// How much what is above the grid grows (or shrinks, when negative) in the next layout pass, already decided in this
    /// one. The layers of direction C never move the grid, so the panel passes zero.
    /// </param>
    /// <returns>The space measured, in device-independent pixels.</returns>
    public double MeasureGridSpace(Window window, double workAreaBottomDip, double shiftAbove = 0)
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

        // CUA-001 (D21 review): the rows never depend on whether the pager row shows, so opening the search or the
        // test mode, which keep its place, never takes a row away.
        if (Pager.Visibility == Visibility.Collapsed)
        {
            below += ControlHeight.Small + Space.S2;
        }
        // The grid area is as high as its rows and the gaps between them, which is what GridMetrics.RowsThatFit counts.
        var space =
            workAreaBottomDip - PanelSizes.Layout.PanelBottomMarginPx - top - below - shiftAbove;
        _viewModel.ApplyGridSpace(space);
        return space;
    }

    /// <summary>
    /// The view changed between Full and Compact (or the search began or ended): «Qué ver» goes above the grid in Full
    /// and to the foot in Compact (VCO-002), where it hides while the search has text.
    /// </summary>
    /// <param name="shown">Whether the bottom row of the Compact view shows (Compact and no search text).</param>
    public void ApplyCompactRow(bool shown)
    {
        _ = shown;
        Apply();
    }

    /// <summary>Hosts the layers of the panel: the tile menu, the test mode band and the marks, × and «+ [add]» on the tiles.</summary>
    /// <param name="layers">The layers.</param>
    public void AttachLayers(PanelLayerModels layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        TileMenu = new TileContextMenuView(layers.Menu);
        TestModeIndicator = new TestModeIndicator(layers.TestMode);
        Strip.AttachLayers(layers);
        ShortcutGrid.AttachLayers(layers);
    }

    /// <summary>
    /// Says which rows a layer of the panel covers: the row «Qué ver» (the search field takes its place), the grid and
    /// its pager (the profile grid) or the pager alone (the test mode band). Covered rows keep their place but are not
    /// on screen, so neither the finger nor UI Automation reaches a control under a layer.
    /// </summary>
    /// <param name="chooser">The row «Qué ver».</param>
    /// <param name="grid">The grid and the pager.</param>
    /// <param name="pager">The pager.</param>
    /// <param name="foot">
    /// The rows under a notice card that takes the whole Compact body: «Qué ver» and the pager, hidden where they are
    /// (D21 review), without reserving a pager row that one page does not have.
    /// </param>
    public void Cover(bool chooser, bool grid, bool pager, bool foot = false)
    {
        if (
            _coversChooser == chooser
            && _coversGrid == grid
            && _coversPager == pager
            && _coversFoot == foot
        )
        {
            return;
        }

        _coversChooser = chooser;
        _coversGrid = grid;
        _coversPager = pager;
        _coversFoot = foot;
        Apply();
    }

    /// <summary>A horizontal swipe that started on the grid area (CUA-005).</summary>
    /// <param name="towardLeft">Whether the finger moved toward the left.</param>
    public void Swiped(bool towardLeft) => _viewModel.Pager.Swiped(towardLeft);

    /// <summary>Stops following the view model (when the surface closes).</summary>
    public void Detach()
    {
        _viewModel.PropertyChanged -= OnPanelChanged;
        _viewModel.Empty.PropertyChanged -= OnPartChanged;
        _viewModel.Strip.PropertyChanged -= OnPartChanged;
        _viewModel.Selector.PropertyChanged -= OnPartChanged;
        _viewModel.Sticky.PropertyChanged -= OnPartChanged;
        _viewModel.Pager.PropertyChanged -= OnPartChanged;
        AdminNotice.Detach();
        Strip.Detach();
        Sticky.Detach();
        Selector.Detach();
        Picker.Detach();
        ShortcutGrid.Detach();
        EmptyState.Detach();
        Pager.Detach();
        Notices.Detach();
        TileMenu?.Detach();
        TestModeIndicator?.Detach();
    }

    private static Visibility Shown(bool visible, bool keepsPlace) =>
        visible ? Visibility.Visible
        : keepsPlace ? Visibility.Hidden
        : Visibility.Collapsed;

    private static Visibility Covered(Visibility visibility, bool covered) =>
        covered && visibility == Visibility.Visible ? Visibility.Hidden : visibility;

    private void OnPartChanged(object? sender, PropertyChangedEventArgs e) => Apply();

    private void OnPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            e.PropertyName
            is nameof(PanelViewModel.Layout)
                or nameof(PanelViewModel.Layers)
                or nameof(PanelViewModel.Shape)
                or nameof(PanelViewModel.ShowsNoResults)
                or nameof(PanelViewModel.NoResultsText)
        )
        {
            Apply();
        }
    }

    /// <summary>Places, shows and spaces the rows of the design of the current view.</summary>
    private void Apply()
    {
        var layout = _viewModel.Layout;
        var compact = layout.Compact;
        var searching = _viewModel.IsSearchingWithText;
        Width = GridMetrics.PanelWidth(layout) - (2 * Space.S3);

        // A row that the search hides keeps its place under the search layer: the grid does not move (PAN-008, D12).
        Strip.Visibility = Shown(
            _viewModel.Strip.IsVisible,
            searching && layout.ShowAlwaysVisibleRow && _viewModel.Strip.Tiles.Count > 0
        );
        Sticky.KeepsPlace = searching && layout.StickyRow;
        Selector.IsCompact = compact;
        Selector.IsNarrow = layout.Metrics.Size == PanelSize.S;
        Selector.Visibility = Covered(
            compact
                ? Shown(CompactRowRules.Visible(layout, searching), keepsPlace: true)
                : Shown(_viewModel.Selector.IsVisible, searching && layout.ShowSelectorRow),
            _coversChooser || _coversFoot
        );
        GridArea.Visibility = Covered(Visibility.Visible, _coversGrid);

        // D21: with one page the pager row takes no room, so a profile of nine shortcuts leaves no empty band at the
        // foot. It is the last row of the body in both views, so only the bottom edge moves when it comes and goes and
        // no control moves under the finger. From the moment the search opens it keeps its place (the results change
        // with every letter, the panel must not jump), and under the test mode band, which is drawn over it. The
        // results page like the grid in both views (BUS-005).
        var empty = _viewModel.Empty.IsVisible;
        var paged = compact
            ? CompactRowRules.ShowsPager(_viewModel.Pager.PageCount, empty)
            : _viewModel.Pager.IsVisible;
        Pager.ShowPages(paged);
        Pager.Visibility = Covered(
            paged || _coversChooser || _coversPager ? Visibility.Visible : Visibility.Collapsed,
            _coversGrid || _coversPager || _coversFoot
        );

        var shape = _viewModel.Shape;
        GridArea.Height = GridMetrics.HeightOf(shape.Rows, shape.TileHeightPx, shape.GapPx);
        var noResults = _viewModel.ShowsNoResults;
        ShortcutGrid.Visibility = empty || noResults ? Visibility.Collapsed : Visibility.Visible;
        NoResults.Text = _viewModel.NoResultsText;
        NoResults.Visibility = noResults ? Visibility.Visible : Visibility.Collapsed;

        // Full: «Qué ver» above the grid; Compact: right under the grid, above the pager (VCO-002, D21).
        var below = Children.IndexOf(Selector) > Children.IndexOf(GridArea);
        if (below != compact)
        {
            Children.Remove(Selector);
            Children.Insert(Children.IndexOf(compact ? Pager : GridArea), Selector);
        }

        // Every row Space.S2 under the one before it, as the tiles of the grid; the first one starts at the top.
        var first = true;
        foreach (FrameworkElement row in Children)
        {
            if (row.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var top = first ? 0 : Space.S2;
            var side = ReferenceEquals(row, Sticky) ? -Space.S1 : 0;
            row.Margin = ReferenceEquals(row, Sticky)
                ? new Thickness(side, top + side, side, side)
                : new Thickness(0, top, 0, 0);
            first = false;
        }
    }
}
