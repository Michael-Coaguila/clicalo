using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.Domain.Catalog;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Surfaces.Panel.EditMode;
using Clicalo.UI.Wpf.Theming;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The shortcut grid (CUA-001, CUA-004): <c>cols</c> columns and the visible rows of <see cref="PanelViewModel.Shape"/>,
/// the tiles of the page in view and nothing more (no scroll, only whole rows, CUA-002). Its height is always the
/// height of its rows, so a short last page does not move the pager nor the notice bar. With the layers of the panel
/// each tile carries its test mode mark and its × of edit mode, and the dashed «+ [add]» tile follows the last one
/// (CUA-012). It only projects the view models.
/// </summary>
public sealed class ShortcutGridView : Border
{
    private readonly PanelViewModel _viewModel;
    private readonly UniformGrid _grid = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly List<(PanelTileControl Tile, PropertyChangedEventHandler Handler)> _tiles = [];
    private readonly List<TileCell> _cells = [];
    private PanelLayerModels? _layers;
    private AddTileView? _add;

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

    /// <summary>The × of the tiles and the dashed «+ [add]» tile, while they show (CUA-012).</summary>
    public IEnumerable<PanelTapTarget> TapTargets
    {
        get
        {
            foreach (var cell in _cells)
            {
                if (cell.RemoveTarget is { } target)
                {
                    yield return target;
                }
            }

            if (_add is { IsVisible: true } add)
            {
                yield return add.TapTarget;
            }
        }
    }

    /// <summary>Draws the layers of the panel over the tiles (test mode, edit mode).</summary>
    /// <param name="layers">The layers.</param>
    public void AttachLayers(PanelLayerModels layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        _layers = layers;
        Rebuild();
    }

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
        if (
            e.PropertyName
            is nameof(PanelViewModel.Shape)
                or nameof(PanelViewModel.Layout)
                or nameof(PanelViewModel.ShowsAddTile)
        )
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
            var (control, handler) = TileFactory.Create(
                viewModel,
                _layers is { } attached ? attached.Modes.Tapped : null,
                _layers is { } menu
                    ? tile => _ = menu.Modes.OpenMenu(tile, menu.InFrequents())
                    : null
            );
            TileFactory.Size(
                control,
                shape.TileHeightPx,
                shape.GapPx,
                size.TileIconPx,
                size.TileLabelPx,
                TypeScale.Scale(size.TileKeysPx, _viewModel.Layout.TextScalePercent)
            );
            _tiles.Add((new PanelTileControl(viewModel, control), handler));
            var cell = TileCell.Create(control, viewModel, _layers, removable: true);
            _cells.Add(cell);

            // The × reaches past the tile's corner: earlier tiles stay above the ones after them.
            System.Windows.Controls.Panel.SetZIndex(cell.Element, -_cells.Count);
            _grid.Children.Add(cell.Element);
        }

        if (_layers is { } layers && _viewModel.ShowsAddTile)
        {
            _add = new AddTileView(layers.EditMode, size.TileIconPx, size.TileLabelPx)
            {
                Margin = new Thickness(shape.GapPx / 2),
            };
            System.Windows.Controls.Panel.SetZIndex(_add, -_cells.Count - 1);
            _grid.Children.Add(_add);
        }
    }

    private void DetachTiles()
    {
        foreach (var (tile, handler) in _tiles)
        {
            TileFactory.Detach(tile.ViewModel, handler);
        }

        _tiles.Clear();
        foreach (var cell in _cells)
        {
            cell.Detach();
        }

        _cells.Clear();
        _add?.Detach();
        _add = null;
    }
}
