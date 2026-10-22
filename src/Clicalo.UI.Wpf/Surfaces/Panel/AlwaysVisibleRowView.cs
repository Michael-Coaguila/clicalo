using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.Domain.PanelLayout;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The Always visible row (FIJ-001 to FIJ-004) of direction C (D12): the section label «SIEMPRE VISIBLE»
/// (<see cref="Overline"/>, hidden in Compact) <see cref="Space.S2"/> above four equal columns with the gap of the grid,
/// <see cref="Space.S2"/>. Its tiles are as high as the size says: <see cref="ControlHeight.Large"/> in M and L, with
/// the anatomy of every tile and their name (one or two lines with «…»), and one touch target in S and Compact, with
/// icons alone (FIJ-002, VCO-001, D19). The «··· i/N» chip
/// takes the last column when the row pages. The panel decides whether the row is on screen; it only projects
/// <see cref="AlwaysVisibleRowViewModel"/>.
/// </summary>
public sealed class AlwaysVisibleRowView : StackPanel
{
    private readonly AlwaysVisibleRowViewModel _viewModel;
    private readonly PanelViewModel _panel;
    private readonly Overline _label = new() { Margin = new Thickness(0, 0, 0, Space.S2) };
    private readonly UniformGrid _grid = new()
    {
        Columns = StripLayout.Columns,
        Margin = new Thickness(-Space.S1, -Space.S1, -Space.S1, -Space.S1),
    };
    private readonly ShortcutTile _more;
    private readonly StyledText _moreText = PanelChrome.Text(TextStyleToken.LabelSmall);
    private readonly List<(PanelTileControl Tile, PropertyChangedEventHandler Handler)> _tiles = [];
    private readonly List<TileCell> _cells = [];
    private PanelLayerModels? _layers;
    private bool _halves;

    /// <summary>Creates the row of <paramref name="panel"/>.</summary>
    /// <param name="panel">The panel.</param>
    public AlwaysVisibleRowView(PanelViewModel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        _panel = panel;
        _viewModel = panel.Strip;
        Orientation = Orientation.Vertical;
        Children.Add(_label);
        Children.Add(_grid);

        _more = PanelChrome.NewButton(PanelChrome.Key, ShortcutTilePattern.Invoke);
        _more.Margin = new Thickness(Space.S1);
        var moreContent = new StackPanel { Orientation = Orientation.Vertical };
        moreContent.Children.Add(PanelChrome.Icon("more_horiz", IconBox.Medium, ColorToken.Text));
        _moreText.HorizontalAlignment = HorizontalAlignment.Center;
        moreContent.Children.Add(_moreText);
        _more.Tag = moreContent;
        _more.Invoked += (_, _) => _viewModel.More();

        _viewModel.PropertyChanged += OnChanged;
        _viewModel.Tiles.CollectionChanged += OnTilesChanged;
        _panel.PropertyChanged += OnPanelChanged;

        // The names are measured with the font of the theme, which the row only finds once it is in the panel.
        Loaded += (_, _) =>
        {
            if (Halves(_panel.Layout) != _halves)
            {
                Rebuild();
            }
        };
        Rebuild();
    }

    /// <summary>The tiles of the page in view, while the row shows.</summary>
    public IReadOnlyList<PanelTileControl> TileControls =>
        _viewModel.IsVisible ? [.. _tiles.Select(static t => t.Tile)] : [];

    /// <summary>The «··· i/N» chip while it shows.</summary>
    public IEnumerable<PanelTapTarget> TapTargets
    {
        get
        {
            if (_viewModel.IsVisible && _viewModel.HasMore)
            {
                yield return new PanelTapTarget(_more, _viewModel.More);
            }
        }
    }

    /// <summary>The «SIEMPRE VISIBLE» label.</summary>
    public Overline Label => _label;

    /// <summary>Draws the test mode mark over the tiles and routes their Invoke through the modes (TAC-008).</summary>
    /// <param name="layers">The layers of the panel.</param>
    public void AttachLayers(PanelLayerModels layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        _layers = layers;
        Rebuild();
    }

    /// <summary>Stops following the view model.</summary>
    public void Detach()
    {
        _viewModel.PropertyChanged -= OnChanged;
        _viewModel.Tiles.CollectionChanged -= OnTilesChanged;
        _panel.PropertyChanged -= OnPanelChanged;
        DetachTiles();
    }

    /// <summary>The voice numbers and the text scale change the anatomy of the tiles of the row.</summary>
    private void OnPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(PanelViewModel.Layout), StringComparison.Ordinal))
        {
            Rebuild();
        }
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AlwaysVisibleRowViewModel.ShowsNames):
            case nameof(AlwaysVisibleRowViewModel.TileHeightPx):
            case nameof(AlwaysVisibleRowViewModel.HasMore):
                Rebuild();
                break;
            default:
                Refresh();
                break;
        }
    }

    private void Refresh()
    {
        _label.Visibility = _viewModel.ShowsLabel ? Visibility.Visible : Visibility.Collapsed;
        _label.Text = _viewModel.Label;
        _moreText.Text = _viewModel.MoreLabel;
        _more.AccessibleName = _viewModel.MoreName;
    }

    private void OnTilesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        Refresh();
        DetachTiles();
        _grid.Children.Clear();
        var height = _viewModel.TileHeightPx;
        _more.Height = height;

        // At a larger text, when a name does not fit its quarter of the row, the row halves its columns instead of
        // cutting the names: two rows of two (D12: a column too narrow for its text gives way, the text never does).
        var layout = _panel.Layout;
        var largeText = layout.TextScalePercent > TypeScale.MinScalePercent;
        var halves = Halves(layout);
        _halves = halves;
        _grid.Columns = halves ? StripLayout.Columns / 2 : StripLayout.Columns;
        var look = TileLook.Strip(
            cramped: layout.VoiceNumbers && !halves,
            largeText,
            _viewModel.ShowsNames
        );
        foreach (var viewModel in _viewModel.Tiles)
        {
            var (control, handler) = TileFactory.Create(
                viewModel,
                _layers is { } attached ? attached.Modes.Tapped : null,
                _layers is { } menu
                    ? tile => _ = menu.Modes.OpenMenu(tile, menu.InFrequents())
                    : null,
                showsKeys: false
            );
            TileFactory.Size(control, height, look);
            TileFactory.ShowName(control, _viewModel.ShowsNames);
            _tiles.Add((new PanelTileControl(viewModel, control), handler));

            // CUA-012: the × is only on grid tiles; a tap on the row in edit mode opens the editor.
            var cell = TileCell.Create(control, viewModel, _layers, removable: false);
            _cells.Add(cell);
            _grid.Children.Add(cell.Element);
        }

        if (_viewModel.HasMore)
        {
            _grid.Children.Add(_more);
        }
    }

    /// <summary>Whether the row goes in two rows of two: at a larger text, when a name does not fit four columns.</summary>
    private bool Halves(PanelLayoutSettings layout)
    {
        var largeText = layout.TextScalePercent > TypeScale.MinScalePercent;
        return largeText && !NamesFit(layout, TileLook.Strip(layout.VoiceNumbers, largeText));
    }

    /// <summary>
    /// Whether every name of the row is read whole in four columns with <paramref name="look"/>: in its lines, and
    /// without a word wider than the tile (a word is never cut, CUA-011).
    /// </summary>
    private bool NamesFit(PanelLayoutSettings layout, TileLook look)
    {
        if (!_viewModel.ShowsNames)
        {
            return true;
        }

        var room = TileNameFit.Room(layout, StripLayout.Columns, look);
        foreach (var viewModel in _viewModel.Tiles)
        {
            var name = TileNameFit.Layout(
                viewModel.AccessibleName,
                look,
                layout.TextScalePercent,
                room,
                this
            );
            if (
                name.MinWidth > room
                || TileNameFit.Lines(name, look, layout.TextScalePercent) > look.NameLines
            )
            {
                return false;
            }
        }

        return true;
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
    }
}
