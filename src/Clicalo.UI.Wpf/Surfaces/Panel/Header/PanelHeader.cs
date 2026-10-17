using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Presentation.Panel.Header;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.Header;

/// <summary>
/// The header of the Full and Compact views in direction C (D12, CAB-001): one row of <see cref="ControlHeight.Small"/>
/// with only tools. At the start, the zone that moves the panel (PAN-004): the grip ⋮⋮ in <c>muted</c>, the icon of the
/// profile in <c>accentInk</c> and its name in <c>heading</c> with «…». Then Search, Edit, Quick settings and Minimize,
/// tertiary icon buttons of 44 × 44 <see cref="Space.S1"/> apart; the three that open a mode show it with the single look
/// of «elegido» while it is open, so the header says in which mode the panel is. Auto/Fixed lives in the row «Qué ver»,
/// next to what it controls (<see cref="SelectorRowView"/>).
/// </summary>
/// <remarks>
/// In S the zone keeps the grip and the icon but not the name: the name is right below, in the row «Qué ver» (or the
/// bottom row of Compact), and a name cut to one letter («W…») was the worst problem of the critique. It only shows
/// <see cref="PanelHeaderViewModel"/> and forwards taps. The title is a button only while the Full view shows no
/// selector row (SEL-006): a tap that did not drag opens the profile grid, and UI Automation sees an ExpandCollapse
/// button with the name of the profile. A button whose action the composition did not give stays hidden.
/// </remarks>
public sealed class PanelHeader : Grid
{
    private readonly PanelHeaderViewModel _viewModel;
    private readonly Border _grip;
    private readonly DockPanel _title;
    private readonly ShortcutTile _titleButton;
    private readonly SymbolIcon _titleIcon;
    private readonly bool _showsName;
    private readonly StyledText _titleText;
    private readonly IconButton _search;
    private readonly IconButton _edit;
    private readonly IconButton _quick;
    private readonly IconButton _minimize;

    /// <summary>Creates the header.</summary>
    /// <param name="viewModel">What it shows.</param>
    /// <param name="showsName">Whether the zone shows the name of the profile (not in S).</param>
    public PanelHeader(PanelHeaderViewModel viewModel, bool showsName = true)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Height = ControlHeight.Small;

        _grip = new Border
        {
            Child = PanelChrome.Icon("drag_indicator", IconBox.Small, ColorToken.Muted),
            Margin = new Thickness(-Space.S1, 0, Space.S2, 0),
            Background = Brushes.Transparent,
        };
        _titleIcon = PanelChrome.Icon(null, IconBox.Medium, ColorToken.AccentInk);
        _titleIcon.Margin = new Thickness(0, 0, Space.S2, 0);
        _titleText = PanelChrome.Text(TextStyleToken.Heading);
        _titleText.VerticalAlignment = VerticalAlignment.Center;
        _showsName = showsName;
        _titleText.Visibility = showsName ? Visibility.Visible : Visibility.Collapsed;

        // A dock panel: the name takes the width that is left and ends in «…» instead of being cut.
        _title = new DockPanel
        {
            LastChildFill = true,
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 0, Space.S1, 0),
        };
        DockPanel.SetDock(_grip, Dock.Left);
        DockPanel.SetDock(_titleIcon, Dock.Left);
        _title.Children.Add(_grip);
        _title.Children.Add(_titleIcon);
        _title.Children.Add(_titleText);

        // SEL-006: over the title, an invisible ExpandCollapse button for voice, keyboard and switches; the finger
        // goes through the pointer layer of the panel, which also lets the same zone drag (PAN-004).
        _titleButton = PanelChrome.NewButton(PanelChrome.Plain, ShortcutTilePattern.ExpandCollapse);
        _titleButton.Margin = _title.Margin;
        _titleButton.Visibility = Visibility.Collapsed;
        _titleButton.Invoked += (_, _) => _viewModel.TitleTapped();
        _titleButton.ExpandRequested += (_, _) => _viewModel.TitleTapped();
        _titleButton.CollapseRequested += (_, _) => _viewModel.TitleTapped();

        _search = Tool("search", () => _viewModel.Search());
        _edit = Tool("edit", () => _viewModel.Edit());
        _quick = Tool("tune", () => _viewModel.QuickSettings());
        _minimize = Tool("remove", () => _viewModel.Minimize());

        // REG-06: one column each, from left to right, so UI Automation reads the header as it is seen.
        ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        Children.Add(_title);
        Children.Add(_titleButton);
        IconButton[] tools = [_search, _edit, _quick, _minimize];
        for (var column = 0; column < tools.Length; column++)
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            SetColumn(tools[column], column + 1);
            Children.Add(tools[column]);
        }

        _viewModel.PropertyChanged += OnViewModelChanged;
        _title.SizeChanged += (_, _) => FitTitle();
        Refresh();
    }

    /// <summary>The zone that drags the panel (PAN-004): the grip, the icon and the name.</summary>
    public IReadOnlyList<FrameworkElement> DragZones => [_title];

    /// <summary>The button over the title while it opens the profile grid (SEL-006); collapsed otherwise.</summary>
    public ShortcutTile TitleButton => _titleButton;

    /// <summary>The name of the profile in the header (hidden in S).</summary>
    public StyledText TitleText => _titleText;

    /// <summary>The Search, Edit, Quick settings and Minimize buttons, in order.</summary>
    public IReadOnlyList<IconButton> Buttons => [_search, _edit, _quick, _minimize];

    /// <summary>
    /// The header buttons as targets of the panel's pointer layer, which never lets WPF raise their Click
    /// (<see cref="PanelTapTarget"/>); hidden ones have no bounds and the surface skips them.
    /// </summary>
    public IReadOnlyList<PanelTapTarget> TapTargets =>
        _viewModel.TitleOpensPicker
            ?
            [
                // SEL-006: a tap on the title that did not drag the panel opens the profile grid.
                new(_title, _viewModel.TitleTapped),
                new(_search, _viewModel.Search),
                new(_edit, _viewModel.Edit),
                new(_quick, _viewModel.QuickSettings),
                new(_minimize, _viewModel.Minimize),
            ]
            :
            [
                new(_search, _viewModel.Search),
                new(_edit, _viewModel.Edit),
                new(_quick, _viewModel.QuickSettings),
                new(_minimize, _viewModel.Minimize),
            ];

    /// <summary>Stops following the view model (when the header is replaced or the panel closes).</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnViewModelChanged;

    private static IconButton Tool(string symbol, Action tap)
    {
        var button = new IconButton
        {
            Symbol = symbol,
            Margin = new Thickness(Space.S1, 0, 0, 0),
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
        _titleIcon.Symbol = vm.TitleIcon.Length == 0 ? null : vm.TitleIcon;
        _titleText.Text = vm.Title;
        FitTitle();
        AutomationProperties.SetItemStatus(
            _titleText,
            vm.ShowsActiveAppDot ? vm.ActiveAppDotName : string.Empty
        );

        _titleButton.Visibility = Shown(vm.TitleOpensPicker);
        _titleButton.AccessibleName = vm.Title;
        _titleButton.AccessibleHelpText = vm.TitleHelp;
        _titleButton.AccessibleState = vm.ShowsActiveAppDot ? vm.ActiveAppDotName : string.Empty;
        _titleButton.IsExpanded = vm.IsPickerOpen;

        Configure(_search, vm.HasSearch, vm.SearchName, vm.IsSearchOpen);
        Configure(_edit, vm.HasEdit, vm.EditName, vm.IsEditing);
        _edit.Symbol = vm.EditIcon;
        Configure(_quick, vm.HasQuickSettings, vm.QuickSettingsName, vm.IsQuickSettingsOpen);
        // ACC-001: «Ajustes rápidos» opens and closes its sheet, and says which.
        _quick.IsExpanded = vm.IsQuickSettingsOpen;
        Configure(_minimize, vm.HasMinimize, vm.MinimizeName, active: false);
    }

    /// <summary>
    /// Shows the name only when it fits whole beside the grip and the icon: a name cut to «W…» does not orient anyone
    /// (D12). Otherwise the icon alone stays, as in S, and the row «Qué ver» names the view in full.
    /// </summary>
    private void FitTitle()
    {
        if (!_showsName || _title.ActualWidth <= 0)
        {
            return;
        }

        var room =
            _title.ActualWidth
            - _grip.ActualWidth
            - _grip.Margin.Left
            - _grip.Margin.Right
            - (
                _titleIcon.Visibility == Visibility.Visible
                    ? _titleIcon.ActualWidth + _titleIcon.Margin.Right
                    : 0
            );
        var name = new FormattedText(
            _titleText.Text ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                _titleText.FontFamily,
                _titleText.FontStyle,
                _titleText.FontWeight,
                _titleText.FontStretch
            ),
            _titleText.FontSize,
            _titleText.Foreground,
            VisualTreeHelper.GetDpi(this).PixelsPerDip
        );
        _titleText.Visibility = Shown(name.WidthIncludingTrailingWhitespace <= room);
    }

    private static void Configure(IconButton button, bool available, string name, bool active)
    {
        button.Visibility = Shown(available);
        button.IsActive = active;
        AutomationProperties.SetName(button, name);
    }
}
