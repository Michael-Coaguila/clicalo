using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.Domain.Catalog;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Theming;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The shortcut grid (CUA-001, CUA-004): <c>cols</c> columns and the visible rows of <see cref="PanelViewModel.Shape"/>,
/// the tiles of the page in view and nothing more (no scroll, only whole rows, CUA-002). Its height is always the
/// height of its rows, so a short last page does not move the pager nor the notice bar. It only projects the view
/// model.
/// </summary>
public sealed class ShortcutGridView : Border
{
    private readonly PanelViewModel _viewModel;
    private readonly UniformGrid _grid = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly List<(PanelTileControl Tile, PropertyChangedEventHandler Handler)> _tiles = [];

    /// <summary>Creates the grid of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The panel.</param>
    public ShortcutGridView(PanelViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Child = _grid;
        viewModel.Tiles.CollectionChanged += OnTilesChanged;
        viewModel.PropertyChanged += OnPanelChanged;
        Rebuild();
    }

    /// <summary>The tiles of the page in view, in display order.</summary>
    public IReadOnlyList<PanelTileControl> TileControls => [.. _tiles.Select(static t => t.Tile)];

    /// <summary>Stops following the view model (when the surface closes).</summary>
    public void Detach()
    {
        _viewModel.Tiles.CollectionChanged -= OnTilesChanged;
        _viewModel.PropertyChanged -= OnPanelChanged;
        DetachTiles();
    }

    private void OnTilesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PanelViewModel.Shape) or nameof(PanelViewModel.Layout))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        DetachTiles();
        _grid.Children.Clear();
        var shape = _viewModel.Shape;
        var size = _viewModel.Layout.Metrics;
        var side = Math.Max(0, (PanelSizes.Layout.PanelChromeWidthPx - shape.GapPx) / 2.0);
        Padding = new Thickness(side, 0, side, 0);
        _grid.Columns = shape.Columns;
        _grid.Rows = shape.Rows;
        _grid.Height = shape.Rows * (shape.TileHeightPx + shape.GapPx);
        foreach (var viewModel in _viewModel.Tiles)
        {
            var (control, handler) = TileFactory.Create(viewModel);
            TileFactory.Size(
                control,
                shape.TileHeightPx,
                shape.GapPx,
                size.TileIconPx,
                size.TileLabelPx,
                TypeScale.Scale(size.TileKeysPx, _viewModel.Layout.TextScalePercent)
            );
            _tiles.Add((new PanelTileControl(viewModel, control), handler));
            _grid.Children.Add(control);
        }
    }

    private void DetachTiles()
    {
        foreach (var (tile, handler) in _tiles)
        {
            TileFactory.Detach(tile.ViewModel, handler);
        }

        _tiles.Clear();
    }
}
