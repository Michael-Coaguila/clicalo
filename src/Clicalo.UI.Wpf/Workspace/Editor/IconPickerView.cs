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
    private readonly ColumnDefinition _categoriesColumn = new()
    {
        Width = new GridLength(CategoriesWidth),
    };
    private readonly ThemedScrollViewer _categoriesScroll = new()
    {
        Padding = new Thickness(0, 0, Space.S2, Space.S6),
    };
    private readonly ThemedScrollViewer _categoriesList = new()
    {
        Padding = new Thickness(0, 0, Space.S2, Space.S6),
    };
    private readonly Border _categoriesLayer;
    private readonly TouchButton _category = new()
    {
        Variant = ButtonVariant.Secondary,
        Size = ControlSize.Medium,
        Width = CategoriesWidth,
        VerticalAlignment = VerticalAlignment.Top,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Padding = new Thickness(Space.S3, 0, Space.S2, 0),
        Margin = new Thickness(Space.S2, 0, 0, 0),
        Visibility = Visibility.Collapsed,
    };
    private readonly DockPanel _right;
    private IIconPicker? _owner;
    private IconPickerModel? _shown;
    private bool _compact;
    private bool _listOpen;
    private string _categoryLabel = string.Empty;

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
        // The grid works out its cells on its own resize first; the suggestions follow them.
        _grid.SizeChanged += (_, _) => ResizeSuggested();
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
        // In the narrow window the selector of categories goes beside the search, as wide as their column: it takes no
        // height from the icons, which the smallest window has little of.
        var searchRow = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(0, Space.S3, 0, Space.S4),
        };
        DockPanel.SetDock(_category, Dock.Right);
        searchRow.Children.Add(_category);
        searchRow.Children.Add(_search);

        _categoriesScroll.Content = _categories;
        _right = new DockPanel { LastChildFill = true, Margin = new Thickness(Space.S4, 0, 0, 0) };
        DockPanel.SetDock(_suggested, Dock.Top);
        DockPanel.SetDock(_empty, Dock.Top);
        _right.Children.Add(_suggested);
        _right.Children.Add(_empty);
        _right.Children.Add(_grid);
        // In the narrow window the categories are a selector whose list is a layer over the icons (R-35 (4)), as the
        // profiles of «Atajos» are: a column of 216 would leave the grid a few columns.
        _categoriesLayer = Parts.Layer(_categoriesList);
        _categoriesLayer.Visibility = Visibility.Collapsed;
        _category.Click += (_, _) =>
        {
            _listOpen = !_listOpen;
            ApplyMode();
        };
        var body = new System.Windows.Controls.Grid();
        body.ColumnDefinitions.Add(_categoriesColumn);
        body.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        System.Windows.Controls.Grid.SetColumn(_right, 1);
        System.Windows.Controls.Grid.SetColumn(_categoriesLayer, 1);
        body.Children.Add(_categoriesScroll);
        body.Children.Add(_right);
        body.Children.Add(_categoriesLayer);
        Parts.Cover(_categoriesLayer, _right);

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(searchRow, Dock.Top);
        layout.Children.Add(head);
        layout.Children.Add(searchRow);
        layout.Children.Add(body);
        Child = layout;
    }

    /// <summary>
    /// Whether the picker is in the narrow window (CCM-005, R-35 (4)): the categories become a selector beside the
    /// search whose list is a layer over the icons; otherwise they are a column on the left.
    /// </summary>
    public bool Compact
    {
        get => _compact;
        set
        {
            if (_compact == value)
            {
                return;
            }

            _compact = value;
            _listOpen = false;
            ApplyMode();
        }
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
            if (_listOpen)
            {
                _listOpen = false;
                ApplyMode();
            }

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
        _grid.Show(model.Icons.Items, model.IconNames.Items, model.Current);
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
            option.Click += (_, _) =>
            {
                // A category chosen in the list of the narrow window closes it: the icons are what comes next.
                _listOpen = false;
                ApplyMode();
                _owner?.ChooseIconCategory(category.Id);
            };
            _categories.Children.Add(option);
        }

        var chosen = model.Categories.Items.FirstOrDefault(static c => c.Selected);
        _categoryLabel = chosen?.Label ?? string.Empty;
        ApplyMode();
    }

    /// <summary>
    /// The categories as a column (wide) or as the selector with its list as a layer (narrow): the same options, moved
    /// from one place to the other.
    /// </summary>
    private void ApplyMode()
    {
        _categoriesColumn.Width = new GridLength(_compact ? 0 : CategoriesWidth);
        _categoriesScroll.Visibility = _compact ? Visibility.Collapsed : Visibility.Visible;
        _right.Margin = new Thickness(_compact ? 0 : Space.S4, 0, 0, 0);
        var home = _compact ? _categoriesList : _categoriesScroll;
        if (!ReferenceEquals(_categories.Parent, home))
        {
            _categoriesScroll.Content = null;
            _categoriesList.Content = null;
            home.Content = _categories;
        }

        _category.Visibility = _compact ? Visibility.Visible : Visibility.Collapsed;
        _category.IsExpanded = _compact ? _listOpen : null;
        _category.IsActive = _compact && _listOpen;
        _category.Content = Selector(_categoryLabel, _listOpen);
        AutomationProperties.SetName(_category, _categoryLabel);
        _categoriesLayer.Visibility =
            _compact && _listOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The content of the selector of the narrow window: the chosen category and ▾.</summary>
    private static DockPanel Selector(string label, bool open)
    {
        var caret = Ui.Icon(open ? "expand_less" : "expand_more", IconBox.Medium, ColorToken.Muted);
        caret.Margin = new Thickness(Space.S2, 0, 0, 0);
        var line = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(caret, Dock.Right);
        line.Children.Add(caret);
        line.Children.Add(
            new StyledText(label, TextStyleToken.Label)
            {
                VerticalAlignment = VerticalAlignment.Center,
            }
        );
        return line;
    }

    private void ResizeSuggested()
    {
        if (
            _suggested.Content is StackPanel { Children.Count: > 1 } column
            && column.Children[1] is Panel cells
        )
        {
            foreach (var cell in cells.Children.OfType<OptionButton>())
            {
                cell.Width = _grid.CellWidth;
            }
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
            // As wide as a cell of the grid under it, so the suggestions and the grid share their columns.
            var cell = new OptionButton
            {
                Kind = OptionKind.Tile,
                Content = icon,
                IsSelected = option.Selected,
                Width = _grid.CellWidth,
                Height = IconGrid.Cell,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, IconGrid.Gap, IconGrid.Gap),
            };
            AutomationProperties.SetName(cell, option.Name);
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
