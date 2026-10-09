using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Domain.Catalog;
using Clicalo.Presentation.Panel.ContextMenu;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.ContextMenu;

/// <summary>
/// The context menu of a tile inside the panel, over the grid (CUA-014, prototype lines 200–205): a <c>win</c> card of
/// radius 12 with a 1 px <c>accent</c> outline and padding 6, a header with the tile's icon in accent and its name in
/// 13 bold, then 44 px rows ([ctxPin]/[ctxUnpin], [ctxHide], [edit], [cancel]) with a 20 px icon and 14 px text. It only
/// projects <see cref="TileContextMenuViewModel"/>.
/// </summary>
/// <remarks>
/// While it is open the panel registers only <see cref="TapTargets"/> and treats any other tap as «tap outside»
/// (<see cref="TileContextMenuViewModel.Close"/>, CUA-014); UI Automation Invoke reaches the same rows through the
/// buttons. It is not a <c>ContextMenu</c> nor a <c>Popup</c> (banned on the surfaces): it is part of the panel.
/// </remarks>
public sealed class TileContextMenuView : Border
{
    private const double HeaderIconPx = 18;
    private const double HeaderTextPx = 13;
    private const double RowIconPx = 20;
    private const double RowTextPx = 14;

    private readonly TileContextMenuViewModel _viewModel;
    private readonly SymbolIcon _headerIcon = new() { Size = HeaderIconPx };
    private readonly TextBlock _headerText = new()
    {
        FontWeight = FontWeights.Bold,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Margin = new Thickness(8, 0, 0, 0),
    };
    private readonly StackPanel _rows = new();
    private readonly List<(TouchButton Button, TileMenuRowViewModel Row)> _buttons = [];

    /// <summary>Creates the menu of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The menu.</param>
    public TileContextMenuView(TileContextMenuViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Margin = new Thickness(12, 0, 12, 8);
        Padding = new Thickness(6);
        CornerRadius = new CornerRadius(Radii.Tile);
        BorderThickness = new Thickness(1);
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Accent));

        _headerIcon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Accent)
        );
        _headerText.SetResourceReference(
            TextBlock.FontSizeProperty,
            ThemeKeys.TextSize(HeaderTextPx)
        );
        _headerText.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Text)
        );
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 6, 8, 6),
        };
        header.Children.Add(_headerIcon);
        header.Children.Add(_headerText);

        var layout = new StackPanel();
        layout.Children.Add(header);
        layout.Children.Add(_rows);
        Child = layout;

        _viewModel.PropertyChanged += OnChanged;
        _viewModel.Rows.CollectionChanged += OnRowsChanged;
        BuildRows();
        Refresh();
    }

    /// <summary>The rows while the menu is open.</summary>
    public IEnumerable<PanelTapTarget> TapTargets =>
        _viewModel.IsOpen
            ? _buttons.Select(static pair => new PanelTapTarget(pair.Button, pair.Row.Activate))
            : [];

    /// <summary>Stops following the view model when the panel closes.</summary>
    public void Detach()
    {
        _viewModel.PropertyChanged -= OnChanged;
        _viewModel.Rows.CollectionChanged -= OnRowsChanged;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs change) => Refresh();

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs change) =>
        BuildRows();

    private void BuildRows()
    {
        _rows.Children.Clear();
        _buttons.Clear();
        foreach (var row in _viewModel.Rows)
        {
            var button = new TouchButton
            {
                Appearance = ButtonAppearance.Ghost,
                Symbol = row.Icon,
                IconSize = RowIconPx,
                Content = row.Label,
                MinHeight = PanelSizes.Layout.PanelContextMenuRowHeightPx,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 0, 10, 0),
                FontWeight = FontWeights.Normal,
                Focusable = false,
                IsTabStop = false,
            };
            button.SetResourceReference(Control.FontSizeProperty, ThemeKeys.TextSize(RowTextPx));
            AutomationProperties.SetName(button, row.Label);
            var activate = row.Activate;
            button.Click += (_, _) => activate();
            _rows.Children.Add(button);
            _buttons.Add((button, row));
        }
    }

    private void Refresh()
    {
        Visibility = _viewModel.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        _headerIcon.Symbol = _viewModel.Icon;
        _headerText.Text = _viewModel.Title;
        AutomationProperties.SetName(this, _viewModel.AccessibleName);
        AutomationProperties.SetHelpText(this, _viewModel.Title);
    }
}
