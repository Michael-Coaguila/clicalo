using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.ControlCenter.Editor;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Editor;

/// <summary>
/// The icon picker (EDI-004, D14) as a layer over what it edits (D12: a temporary state never pushes the content): its
/// title with the icon in use and ✕; the search with 🎤, which finds icons by their Spanish and English words; the
/// categories of the collection on the left, «Todos» first; and on the right the suggestions for the name and the
/// virtualized grid with every icon of the category that matches. The editor of a shortcut, the card of a profile and
/// «Perfil vacío» show the same layer, each with its own <see cref="IIconPicker"/>.
/// </summary>
internal sealed class IconPickerView : Border
{
    /// <summary>The width of the list of categories (the width of the profiles column of «Atajos»).</summary>
    public const double CategoriesWidth = Space.S12 * 4 + Space.S6;

    private readonly TextInput _search;
    private readonly SymbolIcon _currentIcon = new() { Size = IconBox.Medium, IsFilled = true };
    private readonly StyledText _title = new(string.Empty, TextStyleToken.Heading);
    private readonly IconButton _close = new() { Symbol = "close" };
    private readonly StackPanel _categories = new();
    private readonly ContentControl _suggested = new() { Focusable = false };
    private readonly IconGrid _grid = new();
    private readonly StyledText _empty = new(string.Empty, TextStyleToken.Body, ColorToken.Muted)
    {
        MaxLines = 0,
        Visibility = Visibility.Collapsed,
        Margin = new Thickness(0, Space.S4, 0, 0),
    };
    private IIconPicker? _owner;
    private IconPickerModel? _shown;

    /// <summary>Creates the layer, hidden until it gets a model.</summary>
    public IconPickerView()
    {
        Visibility = Visibility.Collapsed;
        Padding = new Thickness(Space.S6, Space.S4, Space.S4, 0);
        Ui.Ink(this, BackgroundProperty, ColorToken.Win);
        _search = new TextInput(string.Empty, string.Empty) { Symbol = "search" };
        _search.Changed += (_, _) => _owner?.SearchIcons(_search.Text);
        _search.DictateRequested += (_, _) => _owner?.DictateIconSearch();
        _close.Click += (_, _) => _owner?.CloseIconPicker();
        _grid.Pick = icon => _owner?.PickIcon(icon);
        Ui.Ink(_currentIcon, SymbolIcon.ForegroundProperty, ColorToken.AccentInk);

        var preview = Ui.Card(
            _currentIcon,
            ColorToken.Card,
            ColorToken.Border,
            Radii.Medium,
            new Thickness(0)
        );
        preview.Width = ControlHeight.Small;
        preview.Height = ControlHeight.Small;
        preview.Margin = new Thickness(0, 0, Space.S3, 0);
        _title.VerticalAlignment = VerticalAlignment.Center;
        var head = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(preview, Dock.Left);
        DockPanel.SetDock(_close, Dock.Right);
        head.Children.Add(preview);
        head.Children.Add(_close);
        head.Children.Add(_title);
        _search.Margin = new Thickness(0, Space.S3, 0, Space.S4);

        var categories = new ThemedScrollViewer
        {
            Content = _categories,
            Padding = new Thickness(0, 0, Space.S2, Space.S6),
        };
        var right = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(Space.S4, 0, 0, 0),
        };
        DockPanel.SetDock(_suggested, Dock.Top);
        DockPanel.SetDock(_empty, Dock.Top);
        right.Children.Add(_suggested);
        right.Children.Add(_empty);
        right.Children.Add(_grid);
        var body = new System.Windows.Controls.Grid();
        body.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(CategoriesWidth) }
        );
        body.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        System.Windows.Controls.Grid.SetColumn(right, 1);
        body.Children.Add(categories);
        body.Children.Add(right);

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(_search, Dock.Top);
        layout.Children.Add(head);
        layout.Children.Add(_search);
        layout.Children.Add(body);
        Child = layout;
    }

    /// <summary>The search field (it keeps what is typed across the changes of the picker).</summary>
    public TextInput Search => _search;

    /// <summary>The grid of icons.</summary>
    public IconGrid Icons => _grid;

    /// <summary>Shows <paramref name="model"/> for <paramref name="owner"/>, or hides the layer when it is null.</summary>
    /// <param name="model">The picker, or null when it is closed.</param>
    /// <param name="owner">Who gets the taps.</param>
    public void Show(IconPickerModel? model, IIconPicker owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var before = _shown;
        _owner = owner;
        _shown = model;
        if (model is null)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        var opening = before is null || Visibility != Visibility.Visible;
        Visibility = Visibility.Visible;
        _currentIcon.Symbol = model.Current;
        _title.Text = model.Title;
        AutomationProperties.SetName(_close, model.CloseName);
        AutomationProperties.SetName(_search.Box, model.SearchPlaceholder);
        AutomationProperties.SetName(_grid, model.Title);
        _search.SetPlaceholder(model.SearchPlaceholder);
        _search.DictateName = model.DictateName;
        _search.Show(model.Query, force: opening);
        if (opening || before!.Categories != model.Categories)
        {
            Categories(model);
        }

        if (
            opening
            || before!.Suggested != model.Suggested
            || !string.Equals(before.SuggestedLabel, model.SuggestedLabel, StringComparison.Ordinal)
        )
        {
            _suggested.Content = model.Suggested.IsEmpty ? null : Suggested(model);
        }

        _empty.Text = model.EmptyText ?? string.Empty;
        _empty.Visibility = model.EmptyText is null ? Visibility.Collapsed : Visibility.Visible;
        var newList =
            opening
            || before!.Icons != model.Icons
                && (
                    !string.Equals(before.Query, model.Query, StringComparison.Ordinal)
                    || before.Categories != model.Categories
                );
        _grid.Show(model.Icons.Items, model.Current);
        if (newList)
        {
            _grid.ScrollToTop();
        }
    }

    private void Categories(IconPickerModel model)
    {
        _categories.Children.Clear();
        foreach (var category in model.Categories)
        {
            var option = new OptionButton
            {
                Kind = OptionKind.ListRow,
                Content = category.Label,
                IsSelected = category.Selected,
                Margin = new Thickness(0, 0, 0, Space.S1),
            };
            AutomationProperties.SetName(option, category.Label);
            option.Click += (_, _) => _owner?.ChooseIconCategory(category.Id);
            _categories.Children.Add(option);
        }
    }

    private StackPanel Suggested(IconPickerModel model)
    {
        var cells = new WrapPanel();
        foreach (var option in model.Suggested)
        {
            var icon = new SymbolIcon
            {
                Symbol = option.Icon,
                Size = IconBox.Medium,
                IsFilled = option.Selected,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Ui.Ink(
                icon,
                SymbolIcon.ForegroundProperty,
                option.Selected ? ColorToken.AccentInk : ColorToken.Text
            );
            var cell = new OptionButton
            {
                Kind = OptionKind.Tile,
                Content = icon,
                IsSelected = option.Selected,
                Width = IconGrid.Cell,
                Height = IconGrid.Cell,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, IconGrid.Gap, IconGrid.Gap),
            };
            AutomationProperties.SetName(cell, option.Icon.Replace('_', ' '));
            cell.Click += (_, _) => _owner?.PickIcon(option.Icon);
            cells.Children.Add(cell);
        }

        var column = new StackPanel { Margin = new Thickness(0, 0, 0, Space.S2) };
        column.Children.Add(
            new Overline(model.SuggestedLabel) { Margin = new Thickness(0, 0, 0, Space.S2) }
        );
        column.Children.Add(cells);
        return column;
    }
}
