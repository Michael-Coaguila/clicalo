using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.PanelLayout;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Surfaces.Panel.EditMode;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The shortcut grid (CUA-001, CUA-004): <c>cols</c> columns and the visible rows of <see cref="PanelViewModel.Shape"/>,
/// the tiles of the page in view and nothing more (no scroll, only whole rows, CUA-002), with the tiles of direction C
/// (D12: 84 × 72, 100 × 80 or 124 × 96, <see cref="Space.S2"/> apart, the key line in the Full view). Its height is
/// always the height of its rows, so a short last page does not move the pager nor the status row. With the layers of
/// the panel each tile carries its test mode mark and its × of edit mode, and the dashed «+ [add]» tile follows the last
/// one (CUA-012). It measures the names for the view model: with a larger text, a word that does not fit a tile of the
/// chosen columns gives the grid two wider columns, so the name is read whole (D17). It only projects the view models.
/// </summary>
public sealed class ShortcutGridView : Border
{
    private readonly PanelViewModel _viewModel;
    private readonly ReadingOrderGrid _grid = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly List<(PanelTileControl Tile, PropertyChangedEventHandler Handler)> _tiles = [];
    private readonly List<TileCell> _cells = [];
    private readonly Dictionary<(PanelSize, int, int, int, string), bool> _wordsFit = [];
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
        viewModel.ApplyWordsFit(WordsFit);
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
        _viewModel.ApplyWordsFit(null);
        DetachTiles();
    }

    /// <summary>
    /// Whether every word of <paramref name="name"/> fits a tile of the grid of <paramref name="layout"/> in
    /// <paramref name="columns"/> (D17); measured once per name and layout.
    /// </summary>
    private bool WordsFit(PanelLayoutSettings layout, int columns, string name)
    {
        var size = layout.Metrics.Size;
        var key = (size, layout.TextScalePercent, columns, GridMetrics.PanelWidth(layout), name);
        if (!_wordsFit.TryGetValue(key, out var fits))
        {
            var look = TileLook.Grid(size);
            fits = TileNameFit.WordsFit(
                name,
                look,
                layout.TextScalePercent,
                TileNameFit.Room(layout, columns, look),
                this
            );
            _wordsFit[key] = fits;
        }

        return fits;
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
        var layout = _viewModel.Layout;
        var look = TileLook.Grid(layout.Metrics.Size);

        // Each tile keeps half the gap around it (Space.S1): the grid reaches as far past the body, so its outer tiles
        // line up with the rows above and below.
        Margin = new Thickness(-Space.S1);
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
                    : null,
                // VCO-001: the Compact view has no key line; a result keeps its profile of origin under its name
                // (BUS-005), in the place of its icon, which the short tile has no room for.
                showsKeys: !layout.Compact || viewModel.IsSearchResult
            );
            TileFactory.Size(
                control,
                shape.TileHeightPx,
                layout.Compact && viewModel.IsSearchResult ? look with { ShowsIcon = false } : look
            );
            _tiles.Add((new PanelTileControl(viewModel, control), handler));
            var cell = TileCell.Create(control, viewModel, _layers, removable: true);
            _cells.Add(cell);

            // The 44 × 44 box of the × reaches past the tile's corner: earlier tiles stay above the ones after them.
            System.Windows.Controls.Panel.SetZIndex(cell.Element, -_cells.Count);
            _grid.Children.Add(cell.Element);
        }

        if (_layers is { } layers && _viewModel.ShowsAddTile)
        {
            _add = new AddTileView(layers.EditMode, look.IconSize, look.Name)
            {
                Margin = new Thickness(Space.S1),
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
