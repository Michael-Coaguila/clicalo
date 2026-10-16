using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Domain.Frequents;
using Clicalo.Presentation.Panel.ContextMenu;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.ContextMenu;

/// <summary>
/// The menu of a tile (CUA-014) in direction C (D12): the tile's icon in <c>accentInk</c> and its name in
/// <c>heading</c>, then one secondary button per action ([ctxPin]/[ctxUnpin], [ctxHide], [edit]) of
/// <see cref="ControlHeight.Small"/>, <see cref="Space.S2"/> apart, with its icon and its name, and [cancel] as a
/// tertiary button, lighter than the actions. The panel shows it as a layer at the foot of its body, over the tiles,
/// without moving them, and rings the tile it belongs to. It only projects <see cref="TileContextMenuViewModel"/>.
/// </summary>
/// <remarks>
/// While it is open the panel registers only <see cref="TapTargets"/> and treats any other tap as «tap outside»
/// (<see cref="TileContextMenuViewModel.Close"/>, CUA-014); UI Automation Invoke reaches the same rows through the
/// buttons. It is not a <c>ContextMenu</c> nor a <c>Popup</c> (banned on the surfaces): it is part of the panel.
/// </remarks>
public sealed class TileContextMenuView : Border
{
    private readonly TileContextMenuViewModel _viewModel;
    private readonly SymbolIcon _headerIcon = PanelChrome.Icon(null, IconBox.Medium, ColorToken.AccentInk);
    private readonly StyledText _headerText = PanelChrome.Text(TextStyleToken.Heading);
    private readonly StackPanel _rows = new();
    private readonly List<(TouchButton Button, TileMenuRowViewModel Row)> _buttons = [];

    /// <summary>Creates the menu of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The menu.</param>
    public TileContextMenuView(TileContextMenuViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));

        _headerIcon.Margin = new Thickness(0, 0, Space.S2, 0);
        _headerText.VerticalAlignment = VerticalAlignment.Center;
        var header = new DockPanel
        {
            LastChildFill = true,
            Height = ControlHeight.Small,
        };
        DockPanel.SetDock(_headerIcon, Dock.Left);
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
            var cancel = row.Item == TileMenuItem.Cancel;
            var button = new TouchButton
            {
                Variant = cancel ? ButtonVariant.Tertiary : ButtonVariant.Secondary,
                Symbol = cancel ? null : row.Icon,
                Content = row.Label,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = cancel
                    ? HorizontalAlignment.Center
                    : HorizontalAlignment.Left,
                Margin = new Thickness(0, Space.S2, 0, 0),
                Focusable = false,
                IsTabStop = false,
            };
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
        _headerIcon.Symbol = _viewModel.Icon.Length == 0 ? null : _viewModel.Icon;
        _headerText.Text = _viewModel.Title;
        AutomationProperties.SetName(this, _viewModel.AccessibleName);
        AutomationProperties.SetHelpText(this, _viewModel.Title);
    }
}
