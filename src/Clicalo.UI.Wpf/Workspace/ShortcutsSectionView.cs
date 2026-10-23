using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Editor;
using Clicalo.UI.Wpf.Workspace.Internal;
using Clicalo.UI.Wpf.Workspace.Shortcuts;

namespace Clicalo.UI.Wpf.Workspace;

/// <summary>
/// The «Atajos» section (docs/05 §1) in direction C (D12): «Tu panel muestra» in one line, then the profiles
/// (<see cref="ProfilesWidth"/>), the shortcuts as the panel shows them (flexible) and the editor
/// (<see cref="EditorWidth"/>) with its fixed foot. Where the three columns do not fit (the smallest window, CCM-005),
/// the profiles become a selector of the whole width whose list is a layer, and the editor opens as a page with its way
/// back; the maximized window gives the grid more columns. The temporary states (the card of the profile, the options
/// of its binding, the list of profiles, the icon picker) are layers over the content, never blocks that push it.
/// </summary>
/// <remarks>
/// It only draws <see cref="ShortcutsSectionViewModel.Screen"/> and forwards taps; each region is rebuilt only when its
/// part of the screen changed, and the text fields live across rebuilds so typing is never interrupted.
/// </remarks>
public sealed class ShortcutsSectionView : Grid
{
    /// <summary>The width of the profiles column (216 in the design of C).</summary>
    public const double ProfilesWidth = IconPickerView.CategoriesWidth;

    /// <summary>The width of the editor column (376 in the design of C): four kinds of 80 and their gaps.</summary>
    public const double EditorWidth = Space.S12 * 8 - Space.S2;

    /// <summary>The least width of the shortcuts column: three tiles, their gaps and the margins of the area.</summary>
    public const double ListMinWidth =
        GridTiles.MinWidth * 3 + Space.S2 * 2 + Space.S4 + Space.S1 + ComponentSize.ScrollBar;

    private static readonly TimeSpan LongPress = TimeSpan.FromMilliseconds(450);

    private readonly ShortcutsSectionViewModel _viewModel;
    private readonly ContentControl _topBar = new() { Focusable = false };
    private readonly ContentControl _profiles = new() { Focusable = false };
    private readonly ContentControl _header = new() { Focusable = false };
    private readonly ContentControl _link = new() { Focusable = false };
    private readonly ContentControl _grid = new() { Focusable = false };
    private readonly ContentControl _listLayerContent = new() { Focusable = false };
    private readonly ContentControl _profilesLayerContent = new() { Focusable = false };
    private readonly ContentControl _editorColumn = new() { Focusable = false };
    private readonly ContentControl _pageHead = new() { Focusable = false };
    private readonly ThemedScrollViewer _listScroll = new();
    private readonly Border _profilesPane;
    private readonly Border _listPane;
    private readonly Grid _editorPane = new();
    private readonly Border _listLayer;
    private readonly Border _profilesLayer;
    private readonly IconPickerView _picker = new();
    private readonly ShortcutEditorView _editor;
    private readonly TextInput _profileName;
    private readonly ColumnDefinition _profilesColumn = new()
    {
        Width = new GridLength(ProfilesWidth),
    };
    private readonly ColumnDefinition _listColumn = new()
    {
        Width = new GridLength(1, GridUnitType.Star),
    };
    private readonly ColumnDefinition _editorColumnDefinition = new()
    {
        Width = new GridLength(EditorWidth),
    };
    private readonly DispatcherTimer _pressTimer;
    private readonly Canvas _dropLayer = new() { IsHitTestVisible = false };
    private readonly Border _dropMark = new()
    {
        Width = Theming.Generated.Stroke.Selected,
        Height = GridTiles.TileHeight,
        Visibility = Visibility.Collapsed,
    };
    private ShortcutsScreen? _shown;
    private bool _narrow;
    private bool _pageOpen;
    private bool _profilesOpen;
    private ShortcutId? _pressed;
    private ShortcutId? _dragging;
    private UIElement? _dragTile;
    private AutoFillGrid? _tiles;
    private Point _pressPoint;

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The section.</param>
    public ShortcutsSectionView(ShortcutsSectionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _editor = new ShortcutEditorView(viewModel.Editor);
        _profileName = new TextInput(string.Empty, string.Empty);
        _profileName.Left += (_, _) =>
        {
            if (!_viewModel.RenameProfile(_profileName.Text))
            {
                _profileName.Show(_shown?.ProfileEdit?.Name ?? string.Empty, force: true);
            }
        };
        _profileName.Changed += (_, _) =>
        {
            if (_profileName.Text.Trim().Length > 0)
            {
                _viewModel.RenameProfile(_profileName.Text);
            }
        };
        _profileName.DictateRequested += (_, _) => _viewModel.Dictate();
        _pressTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
        {
            Interval = LongPress,
        };
        _pressTimer.Tick += (_, _) => StartDrag();
        Ui.Ink(_dropMark, Border.BackgroundProperty, ColorToken.Accent);
        _dropLayer.Children.Add(_dropMark);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var topBar = new Border
        {
            Child = _topBar,
            Padding = new Thickness(Space.S6, Space.S3, Space.S4, Space.S3),
            BorderThickness = new Thickness(0, 0, 0, Theming.Generated.Stroke.Line),
        };
        Ui.Ink(topBar, Border.BorderBrushProperty, ColorToken.Border);
        Children.Add(topBar);

        var body = new Grid();
        body.ColumnDefinitions.Add(_profilesColumn);
        body.ColumnDefinitions.Add(_listColumn);
        body.ColumnDefinitions.Add(_editorColumnDefinition);
        SetRow(body, 1);
        Children.Add(body);

        // The profiles.
        _profilesPane = Bordered(
            new ThemedScrollViewer
            {
                Content = _profiles,
                Padding = new Thickness(Space.S3, Space.S4, Space.S1, Space.S4),
            }
        );
        body.Children.Add(_profilesPane);

        // The shortcuts: the header (in the narrow window, the selector of profiles) and the binding fixed above, the
        // grid below, and the layers: the list of profiles under the selector, the card of the profile and the options
        // of the binding over the grid.
        _header.Margin = new Thickness(Space.S4, Space.S4, Space.S4, 0);
        _link.Margin = new Thickness(Space.S4, 0, Space.S4, Space.S3);
        _listScroll.Content = _grid;
        // S2 and the channel of the bar are the S4 of the header and the binding above: the grid lines up with them.
        _listScroll.Padding = new Thickness(Space.S4, Space.S1, Space.S2, Space.S6);
        _listLayer = Parts.Layer(
            new ThemedScrollViewer
            {
                Content = _listLayerContent,
                Padding = new Thickness(Space.S4, Space.S1, Space.S2, Space.S6),
            }
        );
        _listLayer.Visibility = Visibility.Collapsed;
        _profilesLayer = Parts.Layer(
            new ThemedScrollViewer
            {
                Content = _profilesLayerContent,
                Padding = new Thickness(Space.S3, Space.S1, Space.S2, Space.S6),
            }
        );
        _profilesLayer.Visibility = Visibility.Collapsed;
        var list = new Grid();
        list.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        list.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        list.RowDefinitions.Add(
            new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
        );
        list.Children.Add(_header);
        SetRow(_link, 1);
        list.Children.Add(_link);
        SetRow(_listScroll, 2);
        list.Children.Add(_listScroll);
        SetRow(_listLayer, 2);
        list.Children.Add(_listLayer);
        SetRow(_profilesLayer, 1);
        SetRowSpan(_profilesLayer, 2);
        list.Children.Add(_profilesLayer);
        _listPane = Bordered(list);
        SetColumn(_listPane, 1);
        body.Children.Add(_listPane);

        // The editor, with the way back above it when it is a page.
        _editorPane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _editorPane.RowDefinitions.Add(
            new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
        );
        _editorPane.Children.Add(_pageHead);
        SetRow(_editorColumn, 1);
        _editorPane.Children.Add(_editorColumn);
        SetColumn(_editorPane, 2);
        body.Children.Add(_editorPane);

        // The icon picker covers the shortcuts and the editor: wider than the editor, it shows the categories.
        SetColumn(_picker, 1);
        SetColumnSpan(_picker, 2);
        body.Children.Add(_picker);

        viewModel.PropertyChanged += OnChanged;
        viewModel.Editor.PropertyChanged += OnEditorChanged;
        Render();
        ApplyLayout();
    }

    /// <summary>Whether the window is too narrow for the three columns (CCM-005).</summary>
    public bool IsNarrow => _narrow;

    /// <summary>Whether the editor is shown as a page of its own (the narrow window).</summary>
    public bool IsPageOpen => _narrow && _pageOpen && _shown?.Column is not EditorColumn.Empty;

    /// <summary>The icon picker, a layer of the section.</summary>
    internal IconPickerView Picker => _picker;

    /// <summary>
    /// Lays the section out for <paramref name="width"/>, the width beside the rail (CCM-005): three columns where
    /// they fit; otherwise the profiles in a selector and the editor as a page.
    /// </summary>
    /// <param name="width">The width of the section.</param>
    public void ApplyWidth(double width)
    {
        var narrow = width < ProfilesWidth + EditorWidth + ListMinWidth;
        if (_narrow == narrow)
        {
            return;
        }

        _narrow = narrow;
        _profilesOpen = false;
        if (_shown is { } shown)
        {
            _header.Content = Header(shown.Header);
        }

        ApplyLayout();
    }

    /// <summary>Shows the editor as a page in the narrow window, as a tap on a tile does.</summary>
    public void ShowPage()
    {
        _pageOpen = true;
        _profilesOpen = false;
        ApplyLayout();
    }

    /// <summary>Opens the list of profiles of the narrow window, as a tap on its selector does.</summary>
    public void OpenProfiles()
    {
        if (_narrow && !_profilesOpen)
        {
            ToggleProfiles();
        }
    }

    /// <summary>Closes the list of profiles of the narrow window; says whether it was open (Esc).</summary>
    public bool CloseProfiles()
    {
        if (!_narrow || !_profilesOpen)
        {
            return false;
        }

        ToggleProfiles();
        return true;
    }

    /// <summary>Goes back from the page of the editor to the list (Esc, or ← of the page); says whether it was open.</summary>
    public bool ClosePage()
    {
        if (!IsPageOpen)
        {
            return false;
        }

        if (_shown?.Column == EditorColumn.Library)
        {
            _viewModel.CloseLibrary();
        }

        _pageOpen = false;
        ApplyLayout();
        return true;
    }

    /// <summary>Stops following the view model.</summary>
    public void Detach()
    {
        _viewModel.PropertyChanged -= OnChanged;
        _viewModel.Editor.PropertyChanged -= OnEditorChanged;
        _editor.Detach();
        _pressTimer.Stop();
    }

    private static Border Bordered(UIElement child)
    {
        var border = new Border
        {
            Child = child,
            BorderThickness = new Thickness(0, 0, Theming.Generated.Stroke.Line, 0),
        };
        Ui.Ink(border, Border.BorderBrushProperty, ColorToken.Border);
        return border;
    }

    private static void Detach(FrameworkElement element)
    {
        switch (element.Parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case ContentControl content:
                content.Content = null;
                break;
        }
    }

    /// <summary>
    /// The columns of the width: three side by side; or, narrow, the shortcuts alone or the editor alone as a page.
    /// </summary>
    private void ApplyLayout()
    {
        var page = IsPageOpen;
        _profilesColumn.Width = new GridLength(_narrow ? 0 : ProfilesWidth);
        _profilesPane.Visibility = _narrow ? Visibility.Collapsed : Visibility.Visible;
        _listColumn.Width = page ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        _listPane.Visibility = page ? Visibility.Collapsed : Visibility.Visible;
        _listPane.BorderThickness = new Thickness(
            0,
            0,
            _narrow ? 0 : Theming.Generated.Stroke.Line,
            0
        );
        _editorColumnDefinition.Width =
            !_narrow ? new GridLength(EditorWidth)
            : page ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        _editorPane.Visibility = !_narrow || page ? Visibility.Visible : Visibility.Collapsed;
        _pageHead.Content = page && _shown is { } shown ? PageHead(shown) : null;
        _profilesLayer.Visibility =
            _narrow && _profilesOpen && !page ? Visibility.Visible : Visibility.Collapsed;
        SetColumn(_picker, page ? 2 : 1);
        SetColumnSpan(_picker, page ? 1 : 2);
        if (_shown is { } current)
        {
            _profilesLayerContent.Content =
                _narrow && _profilesOpen
                    ? ProfilesList.Build(
                        current.Profiles,
                        current.TopBar,
                        Review,
                        SelectFromLayer,
                        _viewModel.NewProfile
                    )
                    : null;
        }
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            string.Equals(
                e.PropertyName,
                nameof(ShortcutsSectionViewModel.Screen),
                StringComparison.Ordinal
            )
        )
        {
            Render();
        }
    }

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e) => RenderPicker();

    /// <summary>
    /// The icon picker: the shortcut's when the editor has it open, or the profile's when its card has it open (D14).
    /// </summary>
    private void RenderPicker()
    {
        if (_shown?.ProfileEdit?.Picker is { } profile)
        {
            _picker.Show(profile, _viewModel);
        }
        else
        {
            _picker.Show(
                _shown?.Column == EditorColumn.Editor ? _viewModel.Editor.Model?.Picker : null,
                _viewModel.Editor
            );
        }
    }

    private void Render()
    {
        var screen = _viewModel.Screen;
        var before = _shown;
        _shown = screen;
        if (before?.TopBar != screen.TopBar)
        {
            _topBar.Content = SummaryBar.Build(screen.TopBar);
        }

        if (before?.Profiles != screen.Profiles || before?.TopBar != screen.TopBar)
        {
            _profiles.Content = ProfilesList.Build(
                screen.Profiles,
                screen.TopBar,
                Review,
                _viewModel.SelectList,
                _viewModel.NewProfile
            );
        }

        if (
            before?.Header != screen.Header
            || before?.Profiles != screen.Profiles
            || before?.TopBar != screen.TopBar
            || (before?.Link is null) != (screen.Link is null)
        )
        {
            _header.Content = Header(screen.Header);
        }

        if (before?.Link != screen.Link)
        {
            _link.Content = screen.Link is { } link ? Link(link) : null;
        }

        if (before?.Grid != screen.Grid)
        {
            _grid.Content = Grid(screen.Grid);
        }

        if (before?.ProfileEdit != screen.ProfileEdit || before?.Link != screen.Link)
        {
            UIElement? layer =
                screen.ProfileEdit is { } edit ? ProfileEdit(edit)
                : screen.Link is { } open && (open.Expanded || open.Question is not null)
                    ? LinkOptions(open)
                : null;
            _listLayerContent.Content = layer;
            _listLayer.Visibility = layer is null ? Visibility.Collapsed : Visibility.Visible;
        }

        if (
            before is null
            || before.Column != screen.Column
            || before.Library != screen.Library
            || !string.Equals(before.EmptyText, screen.EmptyText, StringComparison.Ordinal)
        )
        {
            Detach(_editor);
            _editorColumn.Content = screen.Column switch
            {
                EditorColumn.Library when screen.Library is { } library => new ThemedScrollViewer
                {
                    Content = LibraryPane.Build(
                        library,
                        _viewModel.CloseLibrary,
                        _viewModel.CreateOwn,
                        _viewModel.ChooseCategory,
                        _viewModel.AddFromLibrary
                    ),
                    Padding = new Thickness(Space.S4, Space.S4, Space.S2, Space.S6),
                },
                EditorColumn.Editor => _editor,
                _ => Empty(screen.EmptyText),
            };
        }

        // A library opened from elsewhere (a new profile of Plantillas) is a page of its own in the narrow window.
        if (screen.Column == EditorColumn.Library && before?.Column != EditorColumn.Library)
        {
            _pageOpen = true;
        }
        else if (screen.Column == EditorColumn.Empty)
        {
            _pageOpen = false;
        }

        RenderPicker();
        ApplyLayout();
    }

    private static StyledText Empty(string text) =>
        new(text, TextStyleToken.Body, ColorToken.Muted)
        {
            MaxLines = 0,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.S6),
        };

    private void Review()
    {
        _viewModel.ReviewDuplicates();
        _profilesOpen = false;
        ShowPage();
    }

    private void SelectFromLayer(ListRef list)
    {
        _viewModel.SelectList(list);
        _profilesOpen = false;
        ApplyLayout();
    }

    private void ToggleProfiles()
    {
        _profilesOpen = !_profilesOpen;
        if (_shown is { } shown)
        {
            _header.Content = Header(shown.Header);
        }

        ApplyLayout();
    }

    // ---- The way back of the page (narrow window) ---------------------------------------------------------------

    private Border PageHead(ShortcutsScreen screen)
    {
        var back = Parts.Button(
            screen.Header.BackText,
            "arrow_back",
            ButtonVariant.Tertiary,
            () => ClosePage()
        );
        back.HorizontalAlignment = HorizontalAlignment.Left;
        var head = new Border
        {
            Child = back,
            Padding = new Thickness(Space.S2, Space.S1, Space.S4, Space.S1),
            BorderThickness = new Thickness(0, 0, 0, Theming.Generated.Stroke.Line),
        };
        Ui.Ink(head, Border.BorderBrushProperty, ColorToken.Border);
        return head;
    }

    // ---- The selector of profiles (narrow window) ------------------------------------------------------------

    /// <summary>
    /// The profile in view as a selector of the whole width (CCM-005): its icon, its name and where it acts, and ▾; it
    /// opens the list of profiles as a layer over the shortcuts.
    /// </summary>
    private TouchButton Selector(ShortcutsScreen screen)
    {
        var row = screen.Profiles.Rows.FirstOrDefault(static r => r.Selected);
        var icon = Ui.Icon(screen.Header.Icon, IconBox.Medium, ColorToken.AccentInk);
        icon.Margin = new Thickness(0, 0, Space.S3, 0);
        var caret = Ui.Icon(
            _profilesOpen ? "expand_less" : "expand_more",
            IconBox.Medium,
            ColorToken.Muted
        );
        caret.Margin = new Thickness(Space.S2, 0, 0, 0);
        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(new StyledText(screen.Header.Title, TextStyleToken.Label));
        if (row is not null && row.Sub.Length > 0)
        {
            lines.Children.Add(new StyledText(row.Sub, TextStyleToken.Caption, ColorToken.Muted));
        }

        var content = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(caret, Dock.Right);
        content.Children.Add(icon);
        content.Children.Add(caret);
        content.Children.Add(lines);
        if (screen.TopBar.Duplicates is not null)
        {
            // The notice of repeated combinations is in the list: the selector says there is one.
            var warning = Ui.Icon("difference", IconBox.Small, ColorToken.WarnText);
            warning.Margin = new Thickness(Space.S2, 0, 0, 0);
            DockPanel.SetDock(warning, Dock.Right);
            content.Children.Insert(2, warning);
        }

        var selector = new TouchButton
        {
            Content = content,
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Medium,
            IsExpanded = _profilesOpen,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(Space.S3, 0, Space.S2, 0),
        };
        AutomationProperties.SetName(selector, screen.Profiles.Title + ": " + screen.Header.Title);
        AutomationProperties.SetHelpText(
            selector,
            screen.TopBar.Duplicates ?? row?.Sub ?? string.Empty
        );
        selector.Click += (_, _) => ToggleProfiles();
        return selector;
    }

    // ---- The header of the list and its binding (ATJ-003 to ATJ-008) ----------------------------------------------

    /// <summary>
    /// The name of the list with its icon, ✏ (the card of the profile) and «Añadir», the one primary button of the
    /// column. In the narrow window the selector above already names the list, so only ✏ and «Añadir» remain.
    /// </summary>
    private FrameworkElement Header(ListHeaderModel model)
    {
        var add = Parts.Button(model.AddText, "add", ButtonVariant.Primary, OpenLibraryPage);
        add.Margin = new Thickness(Space.S2, 0, 0, 0);
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(add, Dock.Right);
        row.Children.Add(add);
        if (!_narrow)
        {
            var pic = Ui.Card(
                Ui.Icon(model.Icon, IconBox.Medium, ColorToken.AccentInk),
                ColorToken.Card,
                ColorToken.Border,
                Radii.Medium,
                new Thickness(0)
            );
            pic.Width = ControlHeight.Small;
            pic.Height = ControlHeight.Small;
            pic.Margin = new Thickness(0, 0, Space.S3, 0);
            DockPanel.SetDock(pic, Dock.Left);
            row.Children.Add(pic);
        }

        // The pencil is docked first, so a long name ends in «…» and never pushes it out of the row (REG-02).
        var title = new DockPanel
        {
            LastChildFill = true,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        if (model.CanEdit)
        {
            var edit = new IconButton
            {
                Symbol = "edit",
                IconSize = IconBox.Small,
                IsExpanded = model.Editing,
                IsActive = model.Editing,
                Margin = new Thickness(Space.S2, 0, 0, 0),
            };
            AutomationProperties.SetName(edit, model.EditName);
            edit.Click += (_, _) => _viewModel.ToggleProfileEdit();
            DockPanel.SetDock(edit, Dock.Right);
            title.Children.Add(edit);
        }

        if (!_narrow)
        {
            title.Children.Add(
                new StyledText(model.Title, TextStyleToken.Title)
                {
                    VerticalAlignment = VerticalAlignment.Center,
                }
            );
            row.Children.Add(title);
        }
        else
        {
            // Narrow: the selector of profiles names the list and takes the width; ✏ and «Añadir» stay beside it.
            DockPanel.SetDock(title, Dock.Right);
            row.Children.Add(title);
            if (_shown is { } screen)
            {
                row.Children.Add(Selector(screen));
            }
        }

        row.Margin = new Thickness(0, 0, 0, Space.S3);
        if (_shown?.Link is not null || _narrow)
        {
            return row;
        }

        // Always visible has no binding row: its subtitle says where it acts.
        var subtitle = Parts.Note(model.Subtitle, style: TextStyleToken.Caption);
        subtitle.Margin = new Thickness(ControlHeight.Small + Space.S3, 0, 0, 0);
        var column = new StackPanel();
        column.Children.Add(row);
        column.Children.Add(subtitle);
        return column;
    }

    /// <summary>
    /// The one row of the binding (ATJ-005): what the profile follows, in one line with «…», the process in the type
    /// of the text (never cut) and its action as a tertiary button; it warns in warn when the profile follows nothing.
    /// </summary>
    private Border Link(LinkModel model)
    {
        var (fill, line, ink) = model.State switch
        {
            LinkState.Unlinked => (ColorToken.WarnWash, ColorToken.Warn, ColorToken.WarnText),
            LinkState.Waiting => (ColorToken.AccentWash, ColorToken.Accent, ColorToken.AccentInk),
            LinkState.Linked => (ColorToken.Card, ColorToken.Border, ColorToken.Success),
            _ => (ColorToken.Card, ColorToken.Border, ColorToken.Muted),
        };
        var icon = Ui.Icon(model.Icon, IconBox.Small, ink);
        icon.Margin = new Thickness(0, 0, Space.S3, 0);
        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(new StyledText(model.Title, TextStyleToken.Label));
        lines.Children.Add(
            new StyledText(model.Subtitle, TextStyleToken.Caption, ColorToken.Muted)
        );
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        if (model.Action is { } action)
        {
            var button = Parts.Button(action, null, ButtonVariant.Tertiary, _viewModel.LinkAction);
            button.IsExpanded = model.State == LinkState.Waiting ? null : model.Expanded;
            if (button.IsExpanded is { } open)
            {
                // It opens the options of the binding: ▾ like every fold, and «elegido» while they are open, so it
                // reads as the way to close them.
                button.IsActive = open;
                button.Content = Folding(action, open);
            }

            AutomationProperties.SetHelpText(button, model.Title);
            button.Margin = new Thickness(Space.S2, 0, 0, 0);
            DockPanel.SetDock(button, Dock.Right);
            row.Children.Add(button);
        }

        row.Children.Add(lines);
        var card = Ui.Card(
            row,
            fill,
            line,
            Radii.Medium,
            new Thickness(Space.S3, Space.S2, Space.S1, Space.S2)
        );
        card.MinHeight = ControlHeight.Large;
        card.Margin = new Thickness(0, Space.S3, 0, 0);
        return card;
    }

    /// <summary>The text of a tertiary button that folds something, with its caret after it.</summary>
    private static StackPanel Folding(string text, bool open)
    {
        var caret = Ui.Icon(
            open ? "expand_less" : "expand_more",
            IconBox.Small,
            ColorToken.AccentInk
        );
        caret.Margin = new Thickness(Space.S1, 0, 0, 0);
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(
            new StyledText(
                text,
                TextStyleToken.Label,
                open ? ColorToken.Text : ColorToken.AccentInk
            )
            {
                VerticalAlignment = VerticalAlignment.Center,
            }
        );
        line.Children.Add(caret);
        return line;
    }

    /// <summary>
    /// The options of the binding as a layer over the grid (ATJ-006 to ATJ-008): the question when another profile has
    /// the app, the open apps, «Detectar» and «Ninguna».
    /// </summary>
    private StackPanel LinkOptions(LinkModel model)
    {
        var column = new StackPanel();
        if (model.Question is { } question)
        {
            var yes = Parts.Button(
                model.YesText,
                null,
                ButtonVariant.Primary,
                () => _viewModel.AnswerTakeOver(true)
            );
            var no = Parts.Button(
                model.NoText,
                null,
                ButtonVariant.Secondary,
                () => _viewModel.AnswerTakeOver(false)
            );
            var ask = new Card
            {
                Tone = CardTone.Warn,
                Content = Parts.Section(
                    null,
                    new StyledText(question, TextStyleToken.Label) { MaxLines = 0 },
                    Ui.Columns(2, Space.S2, [yes, no])
                ),
                Margin = new Thickness(0, 0, 0, Space.S4),
            };
            AutomationProperties.SetLiveSetting(ask, AutomationLiveSetting.Polite);
            column.Children.Add(ask);
        }

        if (!model.Expanded)
        {
            return column;
        }

        var apps = new WrapPanel { Margin = new Thickness(0, 0, -Space.S2, -Space.S2) };
        foreach (var app in model.Apps)
        {
            var chip = new OptionButton
            {
                Kind = OptionKind.Chip,
                Content = app.Mark is null ? app.Name : app.Name + " · " + app.Mark,
                Symbol = "apps",
                IsSelected = app.Selected,
                Margin = new Thickness(0, 0, Space.S2, Space.S2),
            };
            AutomationProperties.SetName(
                chip,
                app.Mark is null
                    ? app.Name + " " + app.Process
                    : app.Name + " " + app.Process + ", " + app.Mark
            );
            AutomationProperties.SetHelpText(chip, app.Process);
            chip.Click += (_, _) => _viewModel.BindApp(app.Process);
            apps.Children.Add(chip);
        }

        column.Children.Add(Parts.Section(model.AppsTitle, apps));
        var detect = Parts.Button(
            model.DetectText,
            "radar",
            ButtonVariant.Secondary,
            _viewModel.Detect
        );
        var none = Parts.Button(
            model.NoneText,
            "link_off",
            ButtonVariant.Secondary,
            _viewModel.Unlink
        );
        // One per row: their texts are long ([linkDetect], [linkNone]) and must not be cut.
        detect.HorizontalContentAlignment = HorizontalAlignment.Left;
        none.HorizontalContentAlignment = HorizontalAlignment.Left;
        detect.Margin = new Thickness(0, Space.S4, 0, Space.S2);
        column.Children.Add(detect);
        column.Children.Add(none);
        return column;
    }

    // ---- The card of the profile (ATJ-004) ---------------------------------------------------------------------

    /// <summary>
    /// «Editar perfil» as a layer over the grid: its name with 🎤, its icon (which opens the icon picker, D14), «Modo
    /// compatible», «Listo» (primary), «Compartir» and «Eliminar perfil» (two taps, REG-04).
    /// </summary>
    private StackPanel ProfileEdit(ProfileEditModel model)
    {
        Detach(_profileName);
        AutomationProperties.SetName(_profileName.Box, model.NameLabel);
        _profileName.DictateName = model.DictateName;
        _profileName.Show(model.Name);
        var icon = new OptionButton
        {
            Kind = OptionKind.Tile,
            Content = Ui.Icon(model.Icon, IconBox.Medium, ColorToken.AccentInk),
            Role = OptionRole.Expander,
            IsSelected = model.Picker is not null,
            Width = ControlHeight.Medium,
            Height = ControlHeight.Medium,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, Space.S2, 0),
        };
        AutomationProperties.SetName(icon, model.ChangeIconName);
        icon.Click += (_, _) => _viewModel.ToggleProfileIcons();
        var name = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        name.Children.Add(icon);
        name.Children.Add(_profileName);
        var compat = Ui.SwitchRow(
            "sports_esports",
            model.CompatTitle,
            model.CompatText,
            model.Compatible,
            _viewModel.ToggleCompatible,
            wholeDescription: true
        );
        var done = Parts.Button(
            model.DoneText,
            "check",
            ButtonVariant.Primary,
            _viewModel.ToggleProfileEdit
        );
        var share = Parts.Button(
            model.ShareText,
            "share",
            ButtonVariant.Secondary,
            () => _ = _viewModel.ShareAsync(withTexts: false)
        );
        share.IsEnabled = model.CanShare;
        var actions = new List<UIElement> { share };
        if (model.DeleteText is { } deleteText)
        {
            actions.Add(
                Parts.Button(
                    deleteText,
                    model.DeleteArmed ? "warning" : "delete",
                    ButtonVariant.Danger,
                    _viewModel.DeleteProfile
                )
            );
        }

        var column = Parts.Section(
            model.NameLabel,
            name,
            compat,
            Ui.Columns(actions.Count, Space.S2, actions),
            model.ShareTextsText is { } shareTexts
                ? Parts.Button(
                    shareTexts,
                    "lock_open",
                    ButtonVariant.Secondary,
                    () => _ = _viewModel.ShareAsync(withTexts: true)
                )
                : null,
            done
        );
        // DAT-007, PQ-37: the texts travel in clear only when the person chooses so.
        return column;
    }

    // ---- The grid (ATJ-009) ------------------------------------------------------------------------------------

    private StackPanel Grid(GridModel model)
    {
        // The grid gives up a column before it cuts a name (ATJ-009: «Así se ven en el panel»).
        var grid = new AutoFillGrid
        {
            MinItemWidth = GridTiles.WidthFor(
                [.. model.Tiles.Select(static tile => tile.Name), model.LibraryText],
                this
            ),
            ItemHeight = GridTiles.TileHeight,
            Gap = Space.S2,
        };
        foreach (var tile in model.Tiles)
        {
            grid.Children.Add(Tile(tile, model.IncompleteText));
        }

        grid.Children.Add(GridTiles.Library(model.LibraryText, OpenLibraryPage));
        _tiles = grid;
        _dropMark.Visibility = Visibility.Collapsed;
        // The mark of where a dragged tile lands is drawn over the grid, never in its flow.
        Detach(_dropLayer);
        var layers = new Grid();
        layers.Children.Add(grid);
        layers.Children.Add(_dropLayer);
        var hint = Parts.Note(model.Hint, style: TextStyleToken.Caption);
        hint.Margin = new Thickness(0, Space.S3, 0, 0);
        var column = new StackPanel();
        column.Children.Add(layers);
        column.Children.Add(hint);
        return column;
    }

    private void OpenLibraryPage()
    {
        _viewModel.OpenLibrary();
        ShowPage();
    }

    private OptionButton Tile(GridTile tile, string incompleteText)
    {
        var button = GridTiles.Tile(tile, incompleteText);
        button.PreviewMouseLeftButtonDown += (_, e) => Press(tile.Id, button, e);
        button.Click += (_, _) =>
        {
            if (_dragging is null)
            {
                _viewModel.SelectTile(tile.Id);
                ShowPage();
            }
        };
        return button;
    }

    private void Press(ShortcutId id, UIElement tile, MouseButtonEventArgs e)
    {
        _pressed = id;
        _dragTile = tile;
        _pressPoint = _tiles is null ? default : e.GetPosition(_tiles);
        _pressTimer.Stop();
        _pressTimer.Start();
        tile.PreviewMouseMove -= OnTileMove;
        tile.PreviewMouseMove += OnTileMove;
        tile.PreviewMouseLeftButtonUp -= OnTileUp;
        tile.PreviewMouseLeftButtonUp += OnTileUp;
        tile.LostMouseCapture -= OnTileCaptureLost;
        tile.LostMouseCapture += OnTileCaptureLost;
    }

    /// <summary>
    /// A long press picks a tile up (ATJ-009): it follows the finger above the others, and a line of the accent marks
    /// the gap where it will land, until it is dropped.
    /// </summary>
    private void StartDrag()
    {
        _pressTimer.Stop();
        if (
            _pressed is not { } id
            || _dragTile is null
            || Mouse.LeftButton != MouseButtonState.Pressed
        )
        {
            return;
        }

        _listScroll.CancelPan();
        _dragging = id;
        _dragTile.Opacity = 0.55;
        _dragTile.RenderTransform = new TranslateTransform();
        Panel.SetZIndex(_dragTile, 1);
        _ = _dragTile.CaptureMouse();
    }

    private void OnTileMove(object sender, MouseEventArgs e)
    {
        if (_dragging is { } moved && _dragTile is not null && _tiles is not null)
        {
            var point = e.GetPosition(_tiles);
            if (_dragTile.RenderTransform is TranslateTransform follow)
            {
                follow.X = point.X - _pressPoint.X;
                follow.Y = point.Y - _pressPoint.Y;
            }

            ShowDrop(DropAt(moved, point));
            return;
        }

        if (
            _pressed is not null
            && _dragTile is not null
            && !_dragTile.IsMouseCaptured
            && e.LeftButton != MouseButtonState.Pressed
        )
        {
            _pressTimer.Stop();
        }
    }

    private void OnTileUp(object sender, MouseButtonEventArgs e)
    {
        _pressTimer.Stop();
        if (_dragging is { } moved && _dragTile is not null)
        {
            var drop = _tiles is null ? null : DropAt(moved, e.GetPosition(_tiles));
            EndDrag();
            _dragTile.ReleaseMouseCapture();
            _pressed = null;
            e.Handled = true;
            if (drop is { Moves: true } landing)
            {
                _viewModel.Reorder(moved, landing.Before);
            }

            return;
        }

        _pressed = null;
    }

    private void OnTileCaptureLost(object sender, MouseEventArgs e)
    {
        _pressTimer.Stop();
        if (_dragging is not null)
        {
            EndDrag();
        }
    }

    /// <summary>Puts the dragged tile back in its place and hides the mark.</summary>
    private void EndDrag()
    {
        if (_dragTile is not null)
        {
            _dragTile.Opacity = 1;
            _dragTile.RenderTransform = Transform.Identity;
            Panel.SetZIndex(_dragTile, 0);
        }

        _dragging = null;
        _dropMark.Visibility = Visibility.Collapsed;
    }

    private void ShowDrop(Landing? drop)
    {
        if (drop is not { Moves: true } landing)
        {
            _dropMark.Visibility = Visibility.Collapsed;
            return;
        }

        Canvas.SetLeft(_dropMark, landing.MarkX - (_dropMark.Width / 2));
        Canvas.SetTop(_dropMark, landing.MarkTop);
        _dropMark.Height = landing.MarkHeight;
        _dropMark.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Where <paramref name="moved"/> lands when it is dropped at <paramref name="point"/> of the grid: before the tile
    /// under the finger when the finger is on its first half, after it on its second half, and at the end on «Biblioteca»
    /// or past the last tile. Null over a gap that is not a place; <see cref="Landing.Moves"/> is false where it already
    /// is, so the drop changes nothing.
    /// </summary>
    private Landing? DropAt(ShortcutId moved, Point point)
    {
        if (_tiles is null)
        {
            return null;
        }

        var order = new List<(ShortcutId? Id, Rect Slot)>();
        foreach (FrameworkElement child in _tiles.Children)
        {
            order.Add((child.Tag as ShortcutId?, LayoutInformation.GetLayoutSlot(child)));
        }

        var from = order.FindIndex(entry => entry.Id == moved);
        var half = _tiles.Gap / 2;
        for (var i = 0; i < order.Count; i++)
        {
            var (id, slot) = order[i];
            var area = slot;
            area.Inflate(half, half);
            if (i == from || !area.Contains(point))
            {
                continue;
            }

            // «Biblioteca», the last child, is the end of the list.
            var after = id is not null && point.X > slot.Left + (slot.Width / 2);
            var at =
                id is null ? i
                : after ? i + 1
                : i;
            var before = order
                .Skip(at)
                .Select(static entry => entry.Id)
                .FirstOrDefault(other => other != moved);
            var to = at > from ? at - 1 : at;
            var markX = after ? slot.Right + half : slot.Left - half;
            return new Landing(before, to != from, markX, slot.Top, slot.Height);
        }

        return null;
    }

    /// <summary>Where a dragged tile lands: in front of <see cref="Before"/> (null for the end) and where its mark goes.</summary>
    private sealed record Landing(
        ShortcutId? Before,
        bool Moves,
        double MarkX,
        double MarkTop,
        double MarkHeight
    );
}
