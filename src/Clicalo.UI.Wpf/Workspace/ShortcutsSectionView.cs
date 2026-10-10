using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace;

/// <summary>
/// The «Atajos» section (docs/05 §1): the top bar, the profiles column (180, or 150 when narrow), the shortcuts column
/// and the editor column (400, or 340). It only draws <see cref="ShortcutsSectionViewModel.Screen"/> and forwards taps;
/// each region is rebuilt only when its part of the screen changed, and the text fields live across rebuilds so typing
/// is never interrupted.
/// </summary>
public sealed class ShortcutsSectionView : Grid
{
    private const double WideProfiles = 180;
    private const double NarrowProfiles = 150;
    private const double WideEditor = 400;
    private const double NarrowEditor = 340;

    /// <summary>Below this width of the list column, [Añadir] goes under the name of the list.</summary>
    private const double HeaderStackWidth = 300;
    private static readonly TimeSpan LongPress = TimeSpan.FromMilliseconds(450);

    private readonly ShortcutsSectionViewModel _viewModel;
    private readonly ContentControl _topBar = new() { Focusable = false };
    private readonly ContentControl _profiles = new() { Focusable = false };
    private readonly ContentControl _header = new() { Focusable = false };
    private readonly ContentControl _profileEdit = new() { Focusable = false };
    private readonly ContentControl _link = new() { Focusable = false };
    private readonly ContentControl _grid = new() { Focusable = false };
    private readonly ContentControl _editorColumn = new() { Focusable = false };
    private readonly TouchPanScrollViewer _listScroll = new();
    private readonly ShortcutEditorView _editor;
    private readonly TextField _profileName;
    private readonly ColumnDefinition _profilesColumn = new()
    {
        Width = new GridLength(WideProfiles),
    };
    private readonly ColumnDefinition _editorColumnDefinition = new()
    {
        Width = new GridLength(WideEditor),
    };
    private readonly DispatcherTimer _pressTimer;
    private ShortcutsScreen? _shown;
    private bool _narrow;
    private ShortcutId? _pressed;
    private ShortcutId? _dragging;
    private UIElement? _dragTile;

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The section.</param>
    public ShortcutsSectionView(ShortcutsSectionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _editor = new ShortcutEditorView(viewModel.Editor);
        _profileName = new TextField(string.Empty, string.Empty);
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
        _pressTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
        {
            Interval = LongPress,
        };
        _pressTimer.Tick += (_, _) => StartDrag();

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var topBar = new Border
        {
            Child = _topBar,
            Padding = new Thickness(16, 12, 16, 12),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        Ui.Ink(topBar, Border.BorderBrushProperty, ColorToken.Border);
        Children.Add(topBar);

        var columns = new Grid();
        columns.ColumnDefinitions.Add(_profilesColumn);
        columns.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 }
        );
        columns.ColumnDefinitions.Add(_editorColumnDefinition);
        SetRow(columns, 1);
        Children.Add(columns);

        var profilesScroll = new TouchPanScrollViewer
        {
            Content = _profiles,
            Padding = new Thickness(10, 16, 10, 16),
        };
        columns.Children.Add(Bordered(profilesScroll, 0));

        var list = Ui.Column(12, _header, _profileEdit, _link, _grid);
        _listScroll.Content = list;
        _listScroll.Padding = new Thickness(16);
        columns.Children.Add(Bordered(_listScroll, 1));

        var editorScroll = new TouchPanScrollViewer
        {
            Content = _editorColumn,
            Padding = new Thickness(16),
        };
        SetColumn(editorScroll, 2);
        columns.Children.Add(editorScroll);

        viewModel.PropertyChanged += OnChanged;
        Render();
    }

    /// <summary>Below 1240 the columns narrow to 150 / flexible / 340 (CCM-005).</summary>
    /// <param name="narrow">Whether the window is narrow.</param>
    public void SetNarrow(bool narrow)
    {
        if (_narrow == narrow)
        {
            return;
        }

        _narrow = narrow;
        _profilesColumn.Width = new GridLength(narrow ? NarrowProfiles : WideProfiles);
        _editorColumnDefinition.Width = new GridLength(narrow ? NarrowEditor : WideEditor);
    }

    /// <summary>Stops following the view model.</summary>
    public void Detach()
    {
        _viewModel.PropertyChanged -= OnChanged;
        _editor.Detach();
        _pressTimer.Stop();
    }

    private static Border Bordered(UIElement child, int column)
    {
        var border = new Border { Child = child, BorderThickness = new Thickness(0, 0, 1, 0) };
        Ui.Ink(border, Border.BorderBrushProperty, ColorToken.Border);
        SetColumn(border, column);
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

    private void Render()
    {
        var screen = _viewModel.Screen;
        var before = _shown;
        _shown = screen;
        if (before?.TopBar != screen.TopBar)
        {
            _topBar.Content = TopBar(screen.TopBar);
        }

        if (before?.Profiles != screen.Profiles)
        {
            _profiles.Content = Profiles(screen.Profiles);
        }

        if (before?.Header != screen.Header)
        {
            _header.Content = Header(screen.Header);
        }

        if (before?.ProfileEdit != screen.ProfileEdit)
        {
            _profileEdit.Content = screen.ProfileEdit is { } edit ? ProfileEdit(edit) : null;
        }

        if (before?.Link != screen.Link)
        {
            _link.Content = screen.Link is { } link ? Link(link) : null;
        }

        if (before?.Grid != screen.Grid)
        {
            _grid.Content = Grid(screen.Grid);
        }

        if (
            before is null
            || before.Column != screen.Column
            || before.Library != screen.Library
            || !string.Equals(before.EmptyText, screen.EmptyText, StringComparison.Ordinal)
        )
        {
            _editorColumn.Content = screen.Column switch
            {
                EditorColumn.Library when screen.Library is { } library => Library(library),
                EditorColumn.Editor => _editor,
                _ => Empty(screen.EmptyText),
            };
        }
    }

    private static TextBlock Empty(string text)
    {
        var block = Ui.Text(text, 14, ink: ColorToken.Muted, wrap: true);
        block.TextAlignment = TextAlignment.Center;
        block.HorizontalAlignment = HorizontalAlignment.Center;
        block.Margin = new Thickness(0, 120, 0, 0);
        return block;
    }

    private DockPanel TopBar(TopBarModel model)
    {
        var layers = new List<UIElement>
        {
            Ui.Text(model.Title, 13, bold: true, ink: ColorToken.Muted),
        };
        for (var i = 0; i < model.Layers.Count; i++)
        {
            var layer = model.Layers[i];
            if (i > 0)
            {
                layers.Add(Ui.Separator("+", 14, bold: true));
            }

            var chip = Ui.Card(
                Ui.Row(
                    6,
                    Ui.Icon(layer.Icon, 16, ColorToken.Accent),
                    Ui.Text(layer.Label, 13, bold: true),
                    Ui.Text(layer.Meta, 13, ink: ColorToken.Muted)
                ),
                ColorToken.Card,
                ColorToken.Border,
                16,
                new Thickness(10, 6, 10, 6)
            );
            AutomationProperties.SetName(chip, layer.Label + " " + layer.Meta);
            layers.Add(chip);
        }

        var dock = new DockPanel { LastChildFill = true };
        if (model.Duplicates is { } duplicates)
        {
            var review = Ui.Button(
                Ui.IconLabel("content_copy", duplicates, 18, 13, iconInk: ColorToken.Warn),
                duplicates,
                _viewModel.ReviewDuplicates,
                ColorToken.WarnWash,
                radius: 22
            );
            review.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(review, Dock.Right);
            dock.Children.Add(review);
        }

        dock.Children.Add(
            Ui.Wrap(
                8,
                layers.Select(l =>
                {
                    if (l is FrameworkElement f)
                    {
                        f.VerticalAlignment = VerticalAlignment.Center;
                    }
                    return l;
                })
            )
        );
        return dock;
    }

    private StackPanel Profiles(ProfilesModel model)
    {
        var column = Ui.Column(4);
        var title = Ui.Text(
            model.Title.ToUpper(System.Globalization.CultureInfo.CurrentCulture),
            12,
            bold: true,
            ink: ColorToken.Muted
        );
        title.Margin = new Thickness(8, 0, 8, 6);
        column.Children.Add(title);
        foreach (var row in model.Rows)
        {
            var content = Ui.Row(
                8,
                Ui.Icon(row.Icon, 20, ColorToken.Accent),
                Ui.Column(
                    0,
                    Ui.Text(row.Name, 14, bold: true),
                    Ui.Text(row.Sub, 11, ink: ColorToken.Muted, mono: true)
                )
            );
            var button = Ui.Choice(
                content,
                row.Name,
                row.Selected,
                () => _viewModel.SelectList(row.List),
                52,
                10,
                offFill: null,
                role: CcToggleRole.Option
            );
            if (row.Selected)
            {
                CcChrome.Paint(button, ColorToken.CardHi, ColorToken.Text, null);
                button.BorderThickness = new Thickness(0);
            }
            else
            {
                CcChrome.Paint(button, null, ColorToken.Text, null);
            }

            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Padding = new Thickness(8, 4, 8, 4);
            button.MinHeight = 52;
            button.Height = double.NaN;
            AutomationProperties.SetHelpText(button, row.Sub);
            column.Children.Add(button);
        }

        var add = Dashed(
            Ui.Text(model.NewProfile, 13, bold: true, ink: ColorToken.Muted),
            model.NewProfile,
            _viewModel.NewProfile,
            44
        );
        add.Margin = new Thickness(0, 6, 0, 0);
        column.Children.Add(add);
        return column;
    }

    private static CcButton Dashed(UIElement content, string name, Action click, double height)
    {
        var frame = new Rectangle
        {
            RadiusX = 10,
            RadiusY = 10,
            StrokeThickness = 2,
            StrokeDashArray = [4, 3],
            IsHitTestVisible = false,
        };
        Ui.Ink(frame, Shape.StrokeProperty, ColorToken.Line);
        var layers = new Grid();
        layers.Children.Add(frame);
        layers.Children.Add(content);
        if (content is FrameworkElement element)
        {
            element.HorizontalAlignment = HorizontalAlignment.Center;
            element.VerticalAlignment = VerticalAlignment.Center;
        }

        var button = Ui.Button(layers, name, click, height: height);
        button.Padding = new Thickness(0);
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.VerticalContentAlignment = VerticalAlignment.Stretch;
        button.BorderThickness = new Thickness(0);
        return button;
    }

    private DockPanel Header(ListHeaderModel model)
    {
        var icon = Ui.Card(
            Ui.Icon(model.Icon, 22, ColorToken.Accent),
            ColorToken.Card,
            null,
            11,
            new Thickness(0)
        );
        icon.Width = 40;
        icon.Height = 40;
        // The pencil is docked first, so a long name is trimmed with «…» and never pushes it out of the row (REG-02).
        var title = new DockPanel
        {
            LastChildFill = true,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        if (model.CanEdit)
        {
            var edit = Ui.Choice(
                Ui.Icon("edit", 18),
                model.EditName,
                model.Editing,
                _viewModel.ToggleProfileEdit,
                44,
                10,
                offFill: null,
                role: CcToggleRole.Expander
            );
            edit.Width = 44;
            edit.Padding = new Thickness(0);
            if (!model.Editing)
            {
                edit.BorderThickness = new Thickness(0);
            }

            edit.Margin = new Thickness(4, 0, 0, 0);
            DockPanel.SetDock(edit, Dock.Right);
            title.Children.Add(edit);
        }

        title.Children.Add(Ui.Text(model.Title, 18, bold: true));

        var subtitle = Ui.Text(model.Subtitle, 12, ink: ColorToken.Muted, wrap: true);
        var add = Ui.Button(
            Ui.IconLabel("add", model.AddText, 20, 14, ink: ColorToken.OnAccent),
            model.AddText,
            _viewModel.OpenLibrary,
            ColorToken.Accent,
            ColorToken.OnAccent
        );
        var dock = new DockPanel { LastChildFill = true };
        add.VerticalAlignment = VerticalAlignment.Top;
        dock.Children.Add(add);
        DockPanel.SetDock(icon, Dock.Left);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 0, 10, 0);
        dock.Children.Add(icon);
        dock.Children.Add(Ui.Column(2, title, subtitle));
        PlaceAdd(add, double.PositiveInfinity);
        dock.SizeChanged += (_, e) => PlaceAdd(add, e.NewSize.Width);
        return dock;
    }

    /// <summary>
    /// [Añadir] goes beside the name of the list; where the column is too narrow for both (the smallest window,
    /// CCM-005), it goes under it at full width, so the name and its pencil keep their 44 (REG-02).
    /// </summary>
    private static void PlaceAdd(CcButton add, double width)
    {
        var below = width < HeaderStackWidth;
        DockPanel.SetDock(add, below ? Dock.Bottom : Dock.Right);
        add.Margin = below ? new Thickness(0, 10, 0, 0) : new Thickness(10, 0, 0, 0);
    }

    private Border ProfileEdit(ProfileEditModel model)
    {
        Detach(_profileName);
        AutomationProperties.SetName(_profileName.Box, model.NameLabel);
        _profileName.Show(model.Name);
        var dictate = Ui.Button(
            Ui.Icon("mic", 22, ColorToken.Accent),
            model.DictateName,
            () => FocusAndDictate(_profileName),
            ColorToken.AccentWash
        );
        dictate.Width = 44;
        dictate.Padding = new Thickness(0);
        var name = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(dictate, Dock.Right);
        dictate.Margin = new Thickness(6, 0, 0, 0);
        name.Children.Add(dictate);
        name.Children.Add(_profileName);

        var icons = new AutoFillGrid
        {
            MinItemWidth = 44,
            ItemHeight = 44,
            Gap = 4,
        };
        foreach (var option in model.Icons)
        {
            var button = Ui.Choice(
                Ui.Icon(option.Icon, 20),
                option.Icon,
                option.Selected,
                () => _viewModel.SetProfileIcon(option.Icon),
                44,
                8,
                role: CcToggleRole.Option
            );
            button.Padding = new Thickness(0);
            icons.Children.Add(button);
        }

        var iconsScroll = new TouchPanScrollViewer { Content = icons, MaxHeight = 140 };
        var compat = Ui.SwitchRow(
            "sports_esports",
            model.CompatTitle,
            model.CompatText,
            model.Compatible,
            _viewModel.ToggleCompatible
        );
        var share = Ui.Button(
            Ui.IconLabel("share", model.ShareText),
            model.ShareText,
            () => _ = _viewModel.ShareAsync(withTexts: false),
            ColorToken.Card,
            stroke: ColorToken.Border
        );
        share.IsEnabled = model.CanShare;
        var done = Ui.Button(
            Ui.Text(model.DoneText, 14, bold: true, ink: ColorToken.OnAccent),
            model.DoneText,
            _viewModel.ToggleProfileEdit,
            ColorToken.Accent,
            ColorToken.OnAccent
        );
        var actions = new List<UIElement> { share };
        if (model.DeleteText is { } deleteText)
        {
            var delete = Ui.Button(
                Ui.IconLabel(
                    model.DeleteArmed ? "warning" : "delete",
                    deleteText,
                    ink: model.DeleteArmed ? ColorToken.OnDanger : ColorToken.DangerText,
                    iconInk: model.DeleteArmed ? ColorToken.OnDanger : ColorToken.DangerText
                ),
                deleteText,
                _viewModel.DeleteProfile,
                model.DeleteArmed ? ColorToken.Danger : ColorToken.DangerWash
            );
            delete.Padding = new Thickness(6, 0, 6, 0);
            actions.Add(delete);
        }

        share.Padding = new Thickness(6, 0, 6, 0);
        var buttons = Ui.Column(6, done, Ui.Columns(actions.Count, 6, actions));
        if (model.ShareTextsText is { } shareTexts)
        {
            // DAT-007, PQ-37: the texts travel in clear only when the person chooses so.
            buttons.Children.Add(
                Ui.Button(
                    Ui.IconLabel("lock_open", shareTexts),
                    shareTexts,
                    () => _ = _viewModel.ShareAsync(withTexts: true),
                    ColorToken.Card,
                    stroke: ColorToken.Border
                )
            );
        }

        return Ui.Card(
            Ui.Column(
                10,
                Ui.Caption(model.NameLabel),
                name,
                Ui.Caption(model.IconLabel),
                iconsScroll,
                compat,
                buttons
            ),
            ColorToken.AccentWash,
            ColorToken.Accent,
            12,
            new Thickness(12)
        );
    }

    private Border Link(LinkModel model)
    {
        var tone = model.State switch
        {
            LinkState.Unlinked => (
                Fill: ColorToken.WarnWash,
                Stroke: ColorToken.Warn,
                Icon: ColorToken.Warn
            ),
            LinkState.Waiting => (
                Fill: ColorToken.AccentWash,
                Stroke: ColorToken.Accent,
                Icon: ColorToken.Accent
            ),
            _ => (Fill: ColorToken.Card, Stroke: ColorToken.Border, Icon: ColorToken.Accent),
        };
        var head = new DockPanel { LastChildFill = true };
        var icon = Ui.Icon(model.Icon, 20, tone.Icon);
        icon.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(icon, Dock.Left);
        head.Children.Add(icon);
        if (model.Action is { } action)
        {
            var label = Ui.Text(action, 12, bold: true, ink: ColorToken.Accent);
            label.Margin = new Thickness(10, 0, 0, 0);
            DockPanel.SetDock(label, Dock.Right);
            head.Children.Add(label);
        }

        head.Children.Add(
            Ui.Column(
                1,
                Ui.Text(model.Title, 13, bold: true, wrap: true),
                Ui.Text(model.Subtitle, 12, ink: ColorToken.Muted, wrap: true)
            )
        );
        UIElement top;
        if (model.Action is not null)
        {
            var button = Ui.Button(head, model.Title, _viewModel.LinkAction, height: double.NaN);
            button.MinHeight = 48;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Padding = new Thickness(12, 6, 12, 6);
            button.BorderThickness = new Thickness(0);
            AutomationProperties.SetHelpText(button, model.Subtitle);
            top = button;
        }
        else
        {
            head.Margin = new Thickness(12, 10, 12, 10);
            top = head;
        }

        var body = Ui.Column(8, top);
        if (model.Question is { } question)
        {
            var yes = Ui.Button(
                Ui.Text(model.YesText, 13, bold: true, ink: ColorToken.OnWarn),
                model.YesText,
                () => _viewModel.AnswerTakeOver(true),
                ColorToken.Warn,
                ColorToken.OnWarn
            );
            var no = Ui.Button(
                Ui.Text(model.NoText, 13, bold: true),
                model.NoText,
                () => _viewModel.AnswerTakeOver(false),
                ColorToken.Card,
                stroke: ColorToken.Border
            );
            var ask = Ui.Card(
                Ui.Column(
                    8,
                    Ui.Text(question, 13, bold: true, wrap: true),
                    Ui.Columns(2, 6, [yes, no])
                ),
                ColorToken.WarnWash,
                ColorToken.Warn,
                10,
                new Thickness(10)
            );
            ask.Margin = new Thickness(12, 0, 12, 12);
            AutomationProperties.SetLiveSetting(ask, AutomationLiveSetting.Polite);
            body.Children.Add(ask);
        }

        if (model.Expanded)
        {
            var apps = model.Apps.Select(app =>
            {
                var mark = app.Mark is { } active
                    ? Ui.Card(
                        Ui.Text(active, 11, bold: true, ink: ColorToken.Accent),
                        ColorToken.AccentWash,
                        null,
                        8,
                        new Thickness(6, 1, 6, 1)
                    )
                    : null;
                var chip = Ui.Choice(
                    Ui.Row(
                        6,
                        Ui.Text(app.Name, 13, bold: true),
                        Ui.Text(app.Process, 11, ink: ColorToken.Muted, mono: true),
                        mark
                    ),
                    app.Mark is null
                        ? app.Name + " " + app.Process
                        : app.Name + " " + app.Process + ", " + app.Mark,
                    app.Selected,
                    () => _viewModel.BindApp(app.Process),
                    44,
                    22,
                    role: CcToggleRole.Option
                );
                return (UIElement)chip;
            });
            var detect = Ui.Button(
                Ui.IconLabel("radar", model.DetectText, 18, 12),
                model.DetectText,
                _viewModel.Detect,
                ColorToken.Card,
                stroke: ColorToken.Border,
                height: double.NaN
            );
            var none = Ui.Button(
                Ui.IconLabel("link_off", model.NoneText, 18, 12),
                model.NoneText,
                _viewModel.Unlink,
                ColorToken.Card,
                stroke: ColorToken.Border,
                height: double.NaN
            );
            detect.MinHeight = 44;
            none.MinHeight = 44;
            detect.HorizontalContentAlignment = HorizontalAlignment.Left;
            none.HorizontalContentAlignment = HorizontalAlignment.Left;
            var options = Ui.Column(8, Ui.Caption(model.AppsTitle), Ui.Wrap(6, apps), detect, none);
            options.Margin = new Thickness(12, 0, 12, 12);
            body.Children.Add(options);
        }

        return Ui.Card(body, tone.Fill, tone.Stroke, 12, new Thickness(0));
    }

    private StackPanel Grid(GridModel model)
    {
        var grid = new AutoFillGrid
        {
            MinItemWidth = 96,
            ItemHeight = 88,
            Gap = 8,
        };
        foreach (var tile in model.Tiles)
        {
            grid.Children.Add(Tile(tile, model.IncompleteText));
        }

        var library = Dashed(
            Ui.Column(
                4,
                Ui.Icon("library_add", 24, ColorToken.Muted),
                Ui.Text(model.LibraryText, 13, bold: true, ink: ColorToken.Muted)
            ),
            model.LibraryText,
            _viewModel.OpenLibrary,
            88
        );
        grid.Children.Add(library);
        var hint = Ui.Text(model.Hint, 12, ink: ColorToken.Muted, wrap: true);
        return Ui.Column(8, grid, hint);
    }

    private CcToggle Tile(GridTile tile, string incompleteText)
    {
        var name = Ui.Text(tile.Name, 13, bold: true);
        name.TextAlignment = TextAlignment.Center;
        name.HorizontalAlignment = HorizontalAlignment.Center;
        var foot = Ui.Text(tile.Foot, 11, ink: ColorToken.Muted, mono: true);
        foot.HorizontalAlignment = HorizontalAlignment.Center;
        var center = Ui.Column(4, Ui.CategoryIcon(tile.Icon, 26, tile.Category), name, foot);
        center.VerticalAlignment = VerticalAlignment.Center;
        center.HorizontalAlignment = HorizontalAlignment.Center;
        var layers = new Grid();
        layers.Children.Add(center);
        if (tile.Repeated)
        {
            layers.Children.Add(
                Corner(
                    Ui.Icon("warning", 16, ColorToken.Warn),
                    HorizontalAlignment.Left,
                    VerticalAlignment.Top
                )
            );
        }

        if (tile.TypeIcon is { } type)
        {
            layers.Children.Add(
                Corner(
                    Ui.Icon(type, 14, ColorToken.Muted),
                    HorizontalAlignment.Right,
                    VerticalAlignment.Top
                )
            );
        }

        if (tile.Number is { } number)
        {
            var badge = Ui.Card(
                Ui.Text(number, 11, bold: true, ink: ColorToken.OnWarn),
                ColorToken.Warn,
                null,
                5,
                new Thickness(4, 0, 4, 0)
            );
            layers.Children.Add(Corner(badge, HorizontalAlignment.Left, VerticalAlignment.Bottom));
        }

        if (tile.Incomplete)
        {
            var badge = Ui.Card(
                Ui.Text(incompleteText, 11, bold: true, ink: ColorToken.WarnText),
                ColorToken.WarnWash,
                null,
                4,
                new Thickness(5, 0, 5, 0)
            );
            layers.Children.Add(Corner(badge, HorizontalAlignment.Right, VerticalAlignment.Bottom));
        }

        var button = Ui.Choice(
            layers,
            tile.AccessibleName,
            tile.Selected,
            () => { },
            88,
            12,
            role: CcToggleRole.Option
        );
        button.Padding = new Thickness(4);
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.VerticalContentAlignment = VerticalAlignment.Stretch;
        button.Tag = tile.Id;
        AutomationProperties.SetItemStatus(button, tile.AccessibleState);
        button.PreviewMouseLeftButtonDown += (_, _) => Press(tile.Id, button);
        button.Click += (_, _) =>
        {
            if (_dragging is null)
            {
                _viewModel.SelectTile(tile.Id);
            }
        };
        return button;
    }

    private static FrameworkElement Corner(
        FrameworkElement element,
        HorizontalAlignment horizontal,
        VerticalAlignment vertical
    )
    {
        element.HorizontalAlignment = horizontal;
        element.VerticalAlignment = vertical;
        element.IsHitTestVisible = false;
        return element;
    }

    private void Press(ShortcutId id, UIElement tile)
    {
        _pressed = id;
        _dragTile = tile;
        _pressTimer.Stop();
        _pressTimer.Start();
        tile.PreviewMouseMove -= OnTileMove;
        tile.PreviewMouseMove += OnTileMove;
        tile.PreviewMouseLeftButtonUp -= OnTileUp;
        tile.PreviewMouseLeftButtonUp += OnTileUp;
        tile.LostMouseCapture -= OnTileCaptureLost;
        tile.LostMouseCapture += OnTileCaptureLost;
    }

    /// <summary>A long press picks a tile up (ATJ-009): it follows the finger until it is dropped.</summary>
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
        _ = _dragTile.CaptureMouse();
    }

    private void OnTileMove(object sender, MouseEventArgs e)
    {
        if (
            _dragging is null
            && _pressed is not null
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
            var target = TileUnder(e.GetPosition(_grid));
            _dragTile.Opacity = 1;
            _dragTile.ReleaseMouseCapture();
            _dragging = null;
            _pressed = null;
            e.Handled = true;
            if (target != moved)
            {
                _viewModel.Reorder(moved, target);
            }

            return;
        }

        _pressed = null;
    }

    private void OnTileCaptureLost(object sender, MouseEventArgs e)
    {
        _pressTimer.Stop();
        if (_dragging is not null && _dragTile is not null)
        {
            _dragTile.Opacity = 1;
            _dragging = null;
        }
    }

    /// <summary>The shortcut of the tile under <paramref name="point"/>; null for the library tile or a gap (the end).</summary>
    private ShortcutId? TileUnder(Point point)
    {
        var hit = _grid.InputHitTest(point) as DependencyObject;
        for (var node = hit; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement { Tag: ShortcutId id })
            {
                return id;
            }
        }

        return null;
    }

    private StackPanel Library(LibraryModel model)
    {
        var close = Ui.Button(
            Ui.Icon("close", 20),
            model.CloseName,
            _viewModel.CloseLibrary,
            ColorToken.Card
        );
        close.Width = 44;
        close.Padding = new Thickness(0);
        var head = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(Ui.Text(model.Title, 18, bold: true));

        var veil = Ui.Card(null, ColorToken.OnAccent, null, 12, new Thickness(0));
        veil.Opacity = 0.2;
        var badge = new Grid { Width = 44, Height = 44 };
        badge.Children.Add(veil);
        badge.Children.Add(Ui.Icon("edit_square", 26, ColorToken.OnAccent));
        var createContent = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(badge, Dock.Left);
        badge.Margin = new Thickness(0, 0, 12, 0);
        createContent.Children.Add(badge);
        var arrow = Ui.Icon("arrow_forward", 22, ColorToken.OnAccent);
        DockPanel.SetDock(arrow, Dock.Right);
        createContent.Children.Add(arrow);
        createContent.Children.Add(
            Ui.Column(
                2,
                Ui.Text(model.CreateTitle, 16, bold: true, ink: ColorToken.OnAccent),
                Ui.Text(model.CreateText, 13, ink: ColorToken.OnAccent, wrap: true)
            )
        );
        var create = Ui.Button(
            createContent,
            model.CreateTitle,
            _viewModel.CreateOwn,
            ColorToken.Accent,
            ColorToken.OnAccent,
            height: double.NaN,
            radius: 14
        );
        create.Padding = new Thickness(14);
        create.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetHelpText(create, model.CreateText);

        var or = new Grid();
        or.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        or.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        or.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
        );
        var left = Ui.Line();
        var right = Ui.Line();
        var orText = Ui.Text(
            model.OrPick.ToUpper(System.Globalization.CultureInfo.CurrentCulture),
            12,
            bold: true,
            ink: ColorToken.Muted
        );
        orText.Margin = new Thickness(10, 0, 10, 0);
        SetColumn(orText, 1);
        SetColumn(right, 2);
        or.Children.Add(left);
        or.Children.Add(orText);
        or.Children.Add(right);

        var chips = model.Chips.Select(chip =>
        {
            var button = Ui.Choice(
                Ui.IconLabel(chip.Icon, chip.Label, 18, 13),
                chip.Label,
                chip.Selected,
                () => _viewModel.ChooseCategory(chip.Id),
                44,
                22,
                role: CcToggleRole.Option
            );
            if (chip.Selected)
            {
                CcChrome.Paint(button, ColorToken.Accent, ColorToken.OnAccent, ColorToken.Accent);
                button.Content = Ui.IconLabel(
                    chip.Icon,
                    chip.Label,
                    18,
                    13,
                    iconInk: ColorToken.OnAccent,
                    ink: ColorToken.OnAccent
                );
            }

            return (UIElement)button;
        });

        var rows = Ui.Column(6);
        foreach (var row in model.Rows)
        {
            var content = new DockPanel { LastChildFill = true };
            var icon = Ui.CategoryIcon(row.Icon, 24, row.Category);
            icon.Margin = new Thickness(0, 0, 10, 0);
            DockPanel.SetDock(icon, Dock.Left);
            content.Children.Add(icon);
            var state = Ui.Icon(
                row.Added ? "check_circle" : "add_circle",
                26,
                row.Added ? ColorToken.Accent : ColorToken.Muted
            );
            DockPanel.SetDock(state, Dock.Right);
            content.Children.Add(state);
            content.Children.Add(
                Ui.Column(
                    2,
                    Ui.Text(row.Name, 14, bold: true),
                    Ui.Text(row.Foot, 11, ink: ColorToken.Muted, mono: true)
                )
            );
            var button = Ui.Button(
                content,
                row.Name,
                () => _viewModel.AddFromLibrary(row.Index),
                ColorToken.Card,
                stroke: ColorToken.Border,
                height: double.NaN
            );
            button.MinHeight = 56;
            button.Padding = new Thickness(12, 6, 12, 6);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            AutomationProperties.SetItemStatus(button, row.AccessibleState);
            rows.Children.Add(button);
        }

        return Ui.Column(16, head, create, or, Ui.Wrap(6, chips), rows);
    }

    private void FocusAndDictate(TextField field)
    {
        _ = Keyboard.Focus(field.Box);
        _viewModel.Dictate();
    }
}
