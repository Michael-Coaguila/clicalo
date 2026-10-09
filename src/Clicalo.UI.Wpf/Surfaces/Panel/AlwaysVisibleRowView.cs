using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using Clicalo.Domain.PanelLayout;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The Always visible row (FIJ-001 to FIJ-004): the «📌 SIEMPRE VISIBLE» label in 12 px capitals (hidden in Compact),
/// 4 equal columns with a gap of 6, the tiles of the page in view (icons only in S and Compact) and the «··· i/N» chip.
/// It only projects <see cref="AlwaysVisibleRowViewModel"/>.
/// </summary>
public sealed class AlwaysVisibleRowView : StackPanel
{
    private const double Gap = 6;
    private const double LabelPx = 12;
    private const double PinIcon = 16;
    private const double MoreIcon = 18;
    private const double MorePx = 13;

    private readonly AlwaysVisibleRowViewModel _viewModel;
    private readonly PanelViewModel _panel;
    private readonly StackPanel _label = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _labelText = new() { FontWeight = FontWeights.Bold };
    private readonly UniformGrid _grid = new() { Columns = StripLayout.Columns };
    private readonly ShortcutTile _more;
    private readonly TextBlock _moreText = new() { FontWeight = FontWeights.Bold };
    private readonly List<(PanelTileControl Tile, PropertyChangedEventHandler Handler)> _tiles = [];
    private readonly List<TileCell> _cells = [];
    private PanelLayerModels? _layers;

    /// <summary>Creates the row of <paramref name="panel"/>.</summary>
    /// <param name="panel">The panel.</param>
    public AlwaysVisibleRowView(PanelViewModel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        _panel = panel;
        _viewModel = panel.Strip;
        Orientation = Orientation.Vertical;
        Margin = new Thickness(12, 0, 12, 10);

        var pin = new SymbolIcon
        {
            Symbol = "push_pin",
            Size = PinIcon,
            Margin = new Thickness(0, 0, Gap, 0),
        };
        _label.Children.Add(pin);
        _label.Children.Add(_labelText);
        _label.Margin = new Thickness(0, 0, 0, Gap);
        _label.SetResourceReference(
            TextElement.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        _labelText.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(LabelPx));
        _labelText.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        pin.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        Children.Add(_label);

        _grid.Margin = new Thickness(-Gap / 2, 0, -Gap / 2, 0);
        Children.Add(_grid);

        _more = PanelChrome.NewButton(PanelChrome.Button, ShortcutTilePattern.Invoke);
        _more.Margin = new Thickness(Gap / 2);
        _more.HorizontalContentAlignment = HorizontalAlignment.Center;
        _more.VerticalContentAlignment = VerticalAlignment.Center;
        var moreContent = new StackPanel { Orientation = Orientation.Vertical };
        moreContent.Children.Add(
            new SymbolIcon
            {
                Symbol = "more_horiz",
                Size = MoreIcon,
                HorizontalAlignment = HorizontalAlignment.Center,
            }
        );
        _moreText.HorizontalAlignment = HorizontalAlignment.Center;
        _moreText.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(MorePx));
        moreContent.Children.Add(_moreText);
        _more.Tag = moreContent;
        PanelChrome.Paint(_more, ColorToken.Card, ColorToken.Text, ColorToken.Border);
        _more.Invoked += (_, _) => _viewModel.More();

        _viewModel.PropertyChanged += OnChanged;
        _viewModel.Tiles.CollectionChanged += OnTilesChanged;
        Rebuild();
    }

    /// <summary>The tiles of the page in view.</summary>
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
        DetachTiles();
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
        Visibility = _viewModel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        _label.Visibility = _viewModel.ShowsLabel ? Visibility.Visible : Visibility.Collapsed;
        _labelText.Text = _viewModel.Label.ToUpper(CultureInfo.CurrentCulture);
        _moreText.Text = _viewModel.MoreLabel;
        _more.AccessibleName = _viewModel.MoreName;
    }

    private void OnTilesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        Refresh();
        DetachTiles();
        _grid.Children.Clear();
        var size = _panel.Layout.Metrics;
        var height = _viewModel.TileHeightPx;
        _more.Height = height;
        foreach (var viewModel in _viewModel.Tiles)
        {
            var (control, handler) = TileFactory.Create(
                viewModel,
                _layers is { } attached ? attached.Modes.Tapped : null,
                _layers is { } menu
                    ? tile => _ = menu.Modes.OpenMenu(tile, menu.InFrequents())
                    : null
            );
            TileFactory.Size(control, height, Gap, size.StripIconPx, size.StripLabelPx);
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
