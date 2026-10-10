using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.Domain.Catalog;
using Clicalo.Presentation.Panel.Header;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.Header;

/// <summary>
/// The header row of the full and compact views (CAB-001, docs/04 §1): padding 8/6/6/6 and a gap of 2, with the grip
/// ⋮⋮ (26 wide), the icon and the name of the profile (bold 15, trimmed with «…»), the Auto/Fixed button (36 × 36)
/// and the Search, Edit, Quick settings and Minimize buttons (32 wide, 40 in L, as high as the size's header buttons).
/// Every control responds on at least 44 × 44 (REG-02); the targets overlap the neighbours' gaps instead of widening the
/// row, so the drawing keeps the prototype's measures.
/// </summary>
/// <remarks>
/// It only shows <see cref="PanelHeaderViewModel"/> and forwards taps. The grip and the title are the drag zones that
/// move the panel (PAN-004): the panel's pointer layer reads <see cref="DragZones"/>. The grip is never a button; the
/// title is one only while the Full view shows no selector row (SEL-006): a tap that did not drag opens the profile
/// grid, and UI Automation sees it as an ExpandCollapse button with the name of the profile. A button whose action the
/// composition did not give stays hidden.
/// </remarks>
public sealed class PanelHeader : Border
{
    private const double GripWidth = 26;
    private const double GripIconSize = 18;
    private const double TitleIconSize = 18;
    private const double TitleTextSize = 15;
    private const double TitleGap = 5;
    private const double TitlePadding = 2;
    private const double DotSize = 8;
    private const double Gap = 2;

    private readonly PanelHeaderViewModel _viewModel;
    private readonly Border _grip;
    private readonly DockPanel _title;
    private readonly ShortcutTile _titleButton;
    private readonly SymbolIcon _titleIcon;
    private readonly TextBlock _titleText;
    private readonly Ellipse _dot;
    private readonly AutoFixedButton _autoFixed;
    private readonly IconButton _search;
    private readonly IconButton _edit;
    private readonly IconButton _quick;
    private readonly IconButton _minimize;

    /// <summary>Creates the header for the panel size <paramref name="size"/>.</summary>
    /// <param name="viewModel">What it shows.</param>
    /// <param name="size">The measures of the panel size (header buttons: <c>sizes.json</c>).</param>
    public PanelHeader(PanelHeaderViewModel viewModel, SizeMetrics size)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(size);
        _viewModel = viewModel;
        Padding = new Thickness(6, 8, 6, 6);
        double height = size.HeaderButtonHeightPx;

        var gripIcon = new SymbolIcon
        {
            Symbol = "drag_indicator",
            Size = GripIconSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        gripIcon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        _grip = new Border
        {
            Width = TouchTarget.AtLeastMinimum(GripWidth),
            Height = TouchTarget.AtLeastMinimum(height),
            Margin = Overlap(GripWidth, height),
            Background = Brushes.Transparent,
            Child = gripIcon,
        };

        _titleIcon = new SymbolIcon
        {
            Size = TitleIconSize,
            Margin = new Thickness(TitlePadding, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _titleIcon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Accent)
        );
        _titleText = new TextBlock
        {
            FontWeight = FontWeights.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(TitleGap, 0, 0, 0),
        };
        _titleText.SetResourceReference(
            TextBlock.FontSizeProperty,
            ThemeKeys.TextSize(TitleTextSize)
        );
        _titleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Text)
        );
        _dot = new Ellipse
        {
            Width = DotSize,
            Height = DotSize,
            Margin = new Thickness(TitleGap, 0, TitlePadding, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _dot.SetResourceReference(Shape.FillProperty, ThemeBrushKey.For(ColorToken.Accent));
        // A dock panel, not a stack panel: the name takes the remaining width (flex: 1 in the prototype), so a long
        // name is trimmed with «…» instead of being measured without limit and cut.
        _title = new DockPanel
        {
            LastChildFill = true,
            Height = TouchTarget.AtLeastMinimum(height),
            Margin = new Thickness(Gap, -Overflow(height), 0, -Overflow(height)),
            Background = Brushes.Transparent,
            ClipToBounds = true,
        };
        DockPanel.SetDock(_titleIcon, Dock.Left);
        DockPanel.SetDock(_dot, Dock.Right);
        _title.Children.Add(_titleIcon);
        _title.Children.Add(_dot);
        _title.Children.Add(_titleText);

        // SEL-006: over the title, an invisible ExpandCollapse button for voice, keyboard and switches; the finger
        // goes through the pointer layer of the panel, which also lets the same zone drag (PAN-004).
        _titleButton = PanelChrome.NewButton(PanelChrome.Large, ShortcutTilePattern.ExpandCollapse);
        _titleButton.Height = TouchTarget.AtLeastMinimum(height);
        _titleButton.Margin = _title.Margin;
        _titleButton.Opacity = 0;
        _titleButton.Visibility = Visibility.Collapsed;
        _titleButton.Invoked += (_, _) => _viewModel.TitleTapped();
        _titleButton.ExpandRequested += (_, _) => _viewModel.TitleTapped();
        _titleButton.CollapseRequested += (_, _) => _viewModel.TitleTapped();

        _autoFixed = new AutoFixedButton
        {
            Margin = Overlap(AutoFixedButton.VisualSize, AutoFixedButton.VisualSize),
        };
        _autoFixed.ToggleRequested += (_, _) => _viewModel.ToggleLock();

        double width = size.HeaderButtonWidthPx;
        _search = HeaderButton("search", width, height, () => _viewModel.Search());
        _edit = HeaderButton("edit", width, height, () => _viewModel.Edit());
        _quick = HeaderButton("tune", width, height, () => _viewModel.QuickSettings());
        _minimize = HeaderButton("remove", width, height, () => _viewModel.Minimize());

        var row = new DockPanel { LastChildFill = true };
        foreach (
            var right in new FrameworkElement[] { _minimize, _quick, _edit, _search, _autoFixed }
        )
        {
            DockPanel.SetDock(right, Dock.Right);
            row.Children.Add(right);
        }

        DockPanel.SetDock(_grip, Dock.Left);
        row.Children.Add(_grip);
        var titleArea = new Grid();
        titleArea.Children.Add(_title);
        titleArea.Children.Add(_titleButton);
        row.Children.Add(titleArea);
        Child = row;

        _viewModel.PropertyChanged += OnViewModelChanged;
        Refresh();
    }

    /// <summary>The zones that drag the panel (PAN-004): the grip and the title.</summary>
    public IReadOnlyList<FrameworkElement> DragZones => [_grip, _title];

    /// <summary>The button over the title while it opens the profile grid (SEL-006); collapsed otherwise.</summary>
    public ShortcutTile TitleButton => _titleButton;

    /// <summary>The Auto/Fixed button (desktop tests and the composition locate it).</summary>
    public AutoFixedButton AutoFixed => _autoFixed;

    /// <summary>The Search, Edit, Quick settings and Minimize buttons, in order.</summary>
    public IReadOnlyList<IconButton> Buttons => [_search, _edit, _quick, _minimize];

    /// <summary>
    /// Auto/Fixed and the header buttons as targets of the panel's pointer layer, which never lets WPF raise their
    /// Click (<see cref="PanelTapTarget"/>); hidden ones have no bounds and the surface skips them.
    /// </summary>
    public IReadOnlyList<PanelTapTarget> TapTargets =>
        _viewModel.TitleOpensPicker
            ?
            [
                // SEL-006: a tap on the title that did not drag the panel opens the profile grid.
                new(_title, _viewModel.TitleTapped),
                new(_autoFixed, _viewModel.ToggleLock),
                new(_search, _viewModel.Search),
                new(_edit, _viewModel.Edit),
                new(_quick, _viewModel.QuickSettings),
                new(_minimize, _viewModel.Minimize),
            ]
            :
            [
                new(_autoFixed, _viewModel.ToggleLock),
                new(_search, _viewModel.Search),
                new(_edit, _viewModel.Edit),
                new(_quick, _viewModel.QuickSettings),
                new(_minimize, _viewModel.Minimize),
            ];

    private static double Overflow(double visual) =>
        Math.Max(0, (TouchTarget.MinimumSize - visual) / 2);

    // A target larger than its drawing overlaps the gaps around it, so the row keeps the drawn measures (REG-02).
    private static Thickness Overlap(double visualWidth, double visualHeight)
    {
        var x = Overflow(visualWidth);
        var y = Overflow(visualHeight);
        return new Thickness(Gap - x, -y, -x, -y);
    }

    private static IconButton HeaderButton(string symbol, double width, double height, Action tap)
    {
        var button = new IconButton
        {
            Symbol = symbol,
            Width = width,
            Height = height,
            Margin = Overlap(width, height),
            Focusable = false,
            IsTabStop = false,
        };
        button.Click += (_, _) => tap();
        return button;
    }

    private static Visibility Shown(bool visible) =>
        visible ? Visibility.Visible : Visibility.Collapsed;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        var vm = _viewModel;
        AutomationProperties.SetName(_grip, vm.MoveName);
        _titleIcon.Symbol = vm.TitleIcon;
        _titleText.Text = vm.Title;
        _dot.Visibility = Shown(vm.ShowsActiveAppDot);
        AutomationProperties.SetItemStatus(
            _titleText,
            vm.ShowsActiveAppDot ? vm.ActiveAppDotName : string.Empty
        );

        _titleButton.Visibility = Shown(vm.TitleOpensPicker);
        _titleButton.AccessibleName = vm.Title;
        _titleButton.AccessibleHelpText = vm.TitleHelp;
        _titleButton.AccessibleState = vm.ShowsActiveAppDot ? vm.ActiveAppDotName : string.Empty;
        _titleButton.IsExpanded = vm.IsPickerOpen;

        _autoFixed.Visibility = Shown(vm.ShowsAutoFixed);
        _autoFixed.IsChecked = vm.IsFixed;
        _autoFixed.Symbol = vm.AutoFixedIcon;
        _autoFixed.ToolTip = vm.AutoFixedName;
        AutomationProperties.SetName(_autoFixed, vm.AutoFixedName);

        Configure(
            _search,
            vm.HasSearch,
            vm.SearchName,
            vm.IsSearchOpen ? ButtonAppearance.Neutral : ButtonAppearance.Ghost
        );
        Configure(
            _edit,
            vm.HasEdit,
            vm.EditName,
            vm.IsEditing ? ButtonAppearance.Accent : ButtonAppearance.Ghost
        );
        _edit.Symbol = vm.EditIcon;
        Configure(
            _quick,
            vm.HasQuickSettings,
            vm.QuickSettingsName,
            vm.IsQuickSettingsOpen ? ButtonAppearance.Neutral : ButtonAppearance.Ghost
        );
        // ACC-001: «Ajustes rápidos» opens and closes its sheet, and says which.
        _quick.IsExpanded = vm.IsQuickSettingsOpen;
        Configure(_minimize, vm.HasMinimize, vm.MinimizeName, ButtonAppearance.Ghost);
    }

    private static void Configure(
        IconButton button,
        bool available,
        string name,
        ButtonAppearance appearance
    )
    {
        button.Visibility = Shown(available);
        button.Appearance = appearance;
        AutomationProperties.SetName(button, name);
    }
}
