using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.PanelLayout;
using Clicalo.Presentation.Panel;

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
        Notices = new NoticeBarView(viewModel.Notices);
        GridArea = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        GridArea.Children.Add(ShortcutGrid);
        GridArea.Children.Add(EmptyState);

        Children.Add(Strip);
        Children.Add(Sticky);
        Children.Add(Selector);
        Children.Add(Picker);
        Children.Add(GridArea);
        Children.Add(Pager);
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

    /// <summary>The notice bar.</summary>
    public NoticeBarView Notices { get; }

    /// <summary>The area of the grid: the only part that shrinks when vertical space runs out (CUA-002).</summary>
    public Grid GridArea { get; }

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
            .Concat(Pager.TapTargets)
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
        Notices.Detach();
    }

    private void OnPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PanelViewModel.Layout) or nameof(PanelViewModel.Layers))
        {
            ApplyWidth();
        }
    }

    private void ApplyWidth()
    {
        Width = GridMetrics.PanelWidth(_viewModel.Layout);
        ShortcutGrid.Visibility = _viewModel.Layers.EmptyProfile
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
