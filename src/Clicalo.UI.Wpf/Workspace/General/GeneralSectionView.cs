using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.ControlCenter.General;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.General;

/// <summary>
/// «General y panel» (docs/05 §3, GEN-001): the title, the 2 × 2 grid of cards of equal height (Idioma, Tema, Tamaño,
/// Vista) and, below, two columns from 1240 of window width (one below): Disposición and Transparencia on the left;
/// Modo pestaña, Confirmación al tocar, Seguridad de teclas, Accesibilidad y datos and Primeros pasos on the right. It
/// only draws <see cref="GeneralSectionViewModel.Screen"/> and forwards taps; each block is rebuilt only when its part
/// changed, and the two sliders live across rebuilds so a drag is never cut.
/// </summary>
public sealed class GeneralSectionView : ContentControl
{
    private const double NarrowBelow = 1240;
    private const double Gap = 24;

    private readonly GeneralSectionViewModel _viewModel;
    private readonly TextBlock _title = Ui.Text(string.Empty, 24, bold: true);
    private readonly TextBlock _subtitle = Ui.Text(
        string.Empty,
        14,
        ink: ColorToken.Muted,
        wrap: true
    );
    private readonly UniformGrid _top = new() { Columns = 2 };
    private readonly ContentControl _language = Region();
    private readonly ContentControl _theme = Region();
    private readonly ContentControl _size = Region();
    private readonly ContentControl _view = Region();
    private readonly ContentControl _layout = Region();
    private readonly ContentControl _transparency = Region();
    private readonly ContentControl _dock = Region();
    private readonly ContentControl _feedback = Region();
    private readonly ContentControl _safety = Region();
    private readonly ContentControl _access = Region();
    private readonly ContentControl _start = Region();
    private readonly ContentControl _ai = Region();
    private readonly Grid _columns = new();
    private readonly StackPanel _left;
    private readonly StackPanel _right;
    private readonly SettingSlider _opacity;
    private readonly SettingSlider _dim;
    private readonly Border _preview = new() { CornerRadius = new CornerRadius(10) };
    private GeneralScreen? _shown;
    private bool _narrow;

    /// <summary>Creates the view of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The section.</param>
    public GeneralSectionView(GeneralSectionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Focusable = false;
        _opacity = new SettingSlider(viewModel.SetOpacityPercent, withDescription: false);
        _dim = new SettingSlider(viewModel.SetDimPercent, withDescription: false);
        Ui.Ink(_preview, Border.BackgroundProperty, ColorToken.Panel);
        Ui.Ink(_preview, Border.BorderBrushProperty, ColorToken.Border);
        _preview.BorderThickness = new Thickness(1);

        foreach (var card in new[] { _language, _theme, _size, _view })
        {
            card.Margin = new Thickness(0, 0, 12, 12);
            _top.Children.Add(card);
        }

        _top.Margin = new Thickness(0, 0, -12, -12);
        _left = Ui.Column(10, _layout, _transparency);
        _right = Ui.Column(10, _dock, _feedback, _safety, _access, _ai, _start);
        var page = Ui.Column(Gap, Ui.Column(4, _title, _subtitle), _top, _columns);
        page.Margin = new Thickness(Gap);
        var root = new Border { Child = new TouchPanScrollViewer { Content = page } };
        Ui.Ink(root, Border.BackgroundProperty, ColorToken.Win);
        Content = root;

        viewModel.PropertyChanged += OnChanged;
        SizeChanged += (_, _) => ApplyWidth();
        Arrange(narrow: false);
        Render();
    }

    /// <summary>Stops following the view model (the window is closing for good).</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private static ContentControl Region() =>
        new()
        {
            Focusable = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Render();

    private void ApplyWidth()
    {
        var width = Window.GetWindow(this)?.ActualWidth ?? ActualWidth;
        var narrow = width > 0 && width < NarrowBelow;
        if (narrow != _narrow)
        {
            Arrange(narrow);
        }
    }

    /// <summary>Two columns from 1240 of window width, one below (GEN-001, CCM-005).</summary>
    private void Arrange(bool narrow)
    {
        _narrow = narrow;
        _columns.Children.Clear();
        _columns.ColumnDefinitions.Clear();
        _columns.RowDefinitions.Clear();
        if (narrow)
        {
            _columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _left.Margin = new Thickness(0, 0, 0, Gap);
            _right.Margin = new Thickness(0);
            Grid.SetColumn(_left, 0);
            Grid.SetColumn(_right, 0);
            Grid.SetRow(_right, 1);
        }
        else
        {
            _columns.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
            _columns.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
            _left.Margin = new Thickness(0, 0, Gap / 2, 0);
            _right.Margin = new Thickness(Gap / 2, 0, 0, 0);
            Grid.SetColumn(_left, 0);
            Grid.SetColumn(_right, 1);
            Grid.SetRow(_right, 0);
        }

        _columns.Children.Add(_left);
        _columns.Children.Add(_right);
    }

    private void Render()
    {
        var screen = _viewModel.Screen;
        var shown = _shown;
        _shown = screen;
        _title.Text = screen.Title;
        _subtitle.Text = screen.Subtitle;
        if (shown?.Look != screen.Look)
        {
            var look = screen.Look;
            _language.Content = LanguageCard(look);
            _theme.Content = ThemeCard(look);
            _size.Content = SizeCard(look);
            _view.Content = ViewCard(look);
        }

        if (shown?.Layout != screen.Layout)
        {
            _layout.Content = LayoutSection(screen.Layout);
        }

        ShowTransparency(screen.Transparency, shown?.Transparency);
        if (shown?.Dock != screen.Dock)
        {
            _dock.Content = DockSection(screen.Dock);
        }

        if (shown?.Feedback != screen.Feedback)
        {
            var feedback = screen.Feedback;
            _feedback.Content = Section(
                feedback.Caption,
                SwitchRow(feedback.Sound, _viewModel.ToggleSound),
                SwitchRow(feedback.Flash, _viewModel.ToggleFlash)
            );
        }

        if (shown?.Safety != screen.Safety)
        {
            _safety.Content = SafetySection(screen.Safety);
        }

        if (shown?.Access != screen.Access)
        {
            _access.Content = AccessSection(screen.Access);
        }

        if (shown?.Start != screen.Start)
        {
            _start.Content = StartSection(screen.Start);
        }

        if (shown?.Ai != screen.Ai)
        {
            _ai.Content = screen.Ai is { } ai ? AiSection(ai) : null;
            _ai.Visibility = screen.Ai is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    // ---- The 2 × 2 grid ------------------------------------------------------------------------------------------

    private static Border TopCard(string icon, string title, UIElement body)
    {
        var heading = Ui.IconLabel(icon, title, 20, 15, iconInk: ColorToken.Accent);
        var layout = new DockPanel { LastChildFill = true };
        heading.Margin = new Thickness(0, 0, 0, 10);
        DockPanel.SetDock(heading, Dock.Top);
        layout.Children.Add(heading);
        layout.Children.Add(body);
        var card = Ui.Card(layout, null, ColorToken.Border, 14, new Thickness(16));
        card.VerticalAlignment = VerticalAlignment.Stretch;
        return card;
    }

    private Border LanguageCard(LookModel look)
    {
        var buttons = new List<UIElement>();
        foreach (var option in look.Languages)
        {
            var ink = option.Selected ? ColorToken.OnAccent : ColorToken.Text;
            var label = Ui.Text(option.Label, 12, ink: ink, mono: true);
            label.Opacity = 0.8;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            var name = Ui.Text(option.Name, 16, bold: true, ink: ink);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            var button = Ui.Choice(
                Ui.Column(2, label, name),
                option.Name,
                option.Selected,
                () => _viewModel.SetLanguage(option.Code),
                64,
                12
            );
            if (option.Selected)
            {
                CcChrome.Paint(button, ColorToken.Accent, ColorToken.OnAccent, null);
                button.BorderThickness = new Thickness(0);
            }

            button.Height = double.NaN;
            button.MinHeight = 64;
            button.VerticalAlignment = VerticalAlignment.Stretch;
            buttons.Add(button);
        }

        var grid = Ui.Columns(2, 8, buttons);
        grid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
        return TopCard("language", look.LanguageTitle, grid);
    }

    private Border ThemeCard(LookModel look)
    {
        var cards = new List<UIElement>();
        foreach (var option in look.Themes)
        {
            var palette = ThemeCatalog.GetPalette(
                option.Value switch
                {
                    ThemeChoice.Light => ThemeId.Light,
                    ThemeChoice.HighContrast => ThemeId.HighContrast,
                    _ => ThemeId.Dark,
                }
            );
            var border = new SolidColorBrush(palette.GetColor(ColorToken.Border));
            var swatch = new Border
            {
                Height = 44,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(palette.GetColor(ColorToken.Win)),
                BorderBrush = border,
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new Border
                        {
                            Width = 22,
                            Height = 22,
                            CornerRadius = new CornerRadius(6),
                            BorderThickness = new Thickness(1),
                            BorderBrush = border,
                            Background = new SolidColorBrush(palette.GetColor(ColorToken.Card)),
                            Margin = new Thickness(0, 0, 6, 0),
                        },
                        new Border
                        {
                            Width = 22,
                            Height = 22,
                            CornerRadius = new CornerRadius(6),
                            Background = new SolidColorBrush(palette.GetColor(ColorToken.Accent)),
                        },
                    },
                },
            };
            var label = Ui.Text(option.Label, 13, bold: true);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            cards.Add(
                OptionCard(
                    Ui.Column(8, swatch, label),
                    option.Name,
                    option.Selected,
                    () => _viewModel.SetTheme(option.Value),
                    new Thickness(8)
                )
            );
        }

        return TopCard("palette", look.ThemeTitle, Ui.Columns(2, 8, cards));
    }

    private Border SizeCard(LookModel look)
    {
        var cards = new List<UIElement>();
        foreach (var option in look.Sizes)
        {
            var (width, height, icon) = option.Value switch
            {
                PanelSize.Small => (26d, 22d, "view_compact"),
                PanelSize.Large => (42d, 34d, "apps"),
                _ => (34d, 28d, "grid_view"),
            };
            cards.Add(
                OptionCard(
                    Miniature(width, height, icon, 16, option.Label),
                    option.Name,
                    option.Selected,
                    () => _viewModel.SetSize(option.Value),
                    new Thickness(4, 8, 4, 8)
                )
            );
        }

        var sizes = Ui.Columns(3, 6, cards);
        foreach (UIElement card in sizes.Children)
        {
            ((FrameworkElement)card).VerticalAlignment = VerticalAlignment.Bottom;
        }

        var smaller = StepButton(
            "remove",
            look.TextSmallerName,
            look.CanTextSmaller,
            _viewModel.TextSmaller,
            ColorToken.Card
        );
        var bigger = StepButton(
            "add",
            look.TextBiggerName,
            look.CanTextBigger,
            _viewModel.TextBigger,
            ColorToken.Card
        );
        var value = Ui.Text(look.TextSize, 14, ink: ColorToken.Accent, mono: true);
        value.Width = 48;
        value.TextAlignment = TextAlignment.Center;
        AutomationProperties.SetName(value, look.TextSizeTitle);
        var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 4, 0, 0) };
        var textIcon = Ui.Icon("format_size", 18, ColorToken.Muted);
        textIcon.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(textIcon, Dock.Left);
        row.Children.Add(textIcon);
        DockPanel.SetDock(bigger, Dock.Right);
        row.Children.Add(bigger);
        DockPanel.SetDock(value, Dock.Right);
        row.Children.Add(value);
        DockPanel.SetDock(smaller, Dock.Right);
        row.Children.Add(smaller);
        row.Children.Add(Ui.Text(look.TextSizeTitle, 13, wrap: true));
        return TopCard("format_size", look.SizeTitle, Ui.Column(10, sizes, row));
    }

    private Border ViewCard(LookModel look)
    {
        var cards = new List<UIElement>();
        foreach (var option in look.Views)
        {
            var (width, height, icon) = option.Value switch
            {
                PanelDensity.Compact => (30d, 26d, "view_compact"),
                PanelDensity.Dock => (14d, 38d, "view_sidebar"),
                _ => (30d, 38d, "view_agenda"),
            };
            var card = OptionCard(
                Miniature(width, height, icon, 14, option.Label),
                option.Name,
                option.Selected,
                () => _viewModel.SetDensity(option.Value),
                new Thickness(4, 8, 4, 8)
            );
            card.VerticalAlignment = VerticalAlignment.Bottom;
            AutomationProperties.SetHelpText(
                card,
                option.Selected ? look.ViewDescription : string.Empty
            );
            cards.Add(card);
        }

        return TopCard(
            "view_quilt",
            look.ViewTitle,
            Ui.Column(
                10,
                Ui.Columns(3, 6, cards),
                Ui.Text(look.ViewDescription, 12, ink: ColorToken.Muted, wrap: true)
            )
        );
    }

    private static StackPanel Miniature(
        double width,
        double height,
        string icon,
        double iconSize,
        string label
    )
    {
        var box = Ui.Card(
            Ui.Icon(icon, iconSize, ColorToken.Accent),
            ColorToken.Side,
            ColorToken.Border,
            6,
            new Thickness(0)
        );
        box.Width = width;
        box.Height = height;
        box.HorizontalAlignment = HorizontalAlignment.Center;
        var text = Ui.Text(label, 12, bold: true);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        return Ui.Column(6, box, text);
    }

    // ---- Left column ---------------------------------------------------------------------------------------------

    private StackPanel LayoutSection(LayoutModel layout)
    {
        var rows = new List<UIElement>();
        foreach (var option in layout.Rows)
        {
            var cells = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            for (var i = 0; i < option.Cells; i++)
            {
                cells.Children.Add(Cell(option.Selected, 9, i == 0 ? 0 : 3));
            }

            var box = Ui.Card(cells, ColorToken.Side, ColorToken.Border, 6, new Thickness(5));
            box.Height = 46;
            rows.Add(
                OptionCard(
                    Ui.Column(8, box, Centered(Ui.Text(option.Label, 13, bold: true))),
                    option.Name,
                    option.Selected,
                    () => _viewModel.SetRows(option.Value),
                    new Thickness(10)
                )
            );
        }

        var columns = new List<UIElement>();
        foreach (var option in layout.Columns)
        {
            var cells = new UniformGrid { Columns = option.Cells, Rows = 2 };
            for (var i = 0; i < option.Cells * 2; i++)
            {
                var cell = Cell(option.Selected, 12, 0);
                cell.Margin = new Thickness(1.5);
                cells.Children.Add(cell);
            }

            var box = Ui.Card(cells, ColorToken.Side, ColorToken.Border, 6, new Thickness(4.5));
            columns.Add(
                OptionCard(
                    Ui.Column(8, box, Centered(Ui.Text(option.Label, 13, bold: true))),
                    option.Name,
                    option.Selected,
                    () => _viewModel.SetColumns(option.Value),
                    new Thickness(10)
                )
            );
        }

        var keys = new List<UIElement>();
        foreach (var option in layout.ShowKeys)
        {
            var tile = Ui.Card(
                Ui.Column(
                    3,
                    Centered(Ui.Icon("content_copy", 22, ColorToken.Accent)),
                    Centered(Ui.Text(layout.SampleName, 12, bold: true)),
                    option.Value
                        ? Centered(
                            Ui.Text(layout.SampleKeys, 11, ink: ColorToken.Muted, mono: true)
                        )
                        : null
                ),
                ColorToken.Side,
                ColorToken.Border,
                10,
                new Thickness(4)
            );
            tile.Width = 84;
            tile.Height = 70;
            if (tile.Child is FrameworkElement inner)
            {
                inner.VerticalAlignment = VerticalAlignment.Center;
            }

            keys.Add(
                OptionCard(
                    Ui.Column(8, Centered(tile), Centered(Ui.Text(option.Label, 13, bold: true))),
                    option.Name,
                    option.Selected,
                    () => _viewModel.SetShowKeys(option.Value),
                    new Thickness(10)
                )
            );
        }

        var selector = SwitchRow(layout.ProfileSelectorRow, _viewModel.ToggleProfileSelectorRow);
        var column = Ui.Column(
            10,
            Caption(layout.Caption),
            Ui.Text(layout.RowsTitle, 15, bold: true),
            Ui.Text(layout.RowsDescription, 13, ink: ColorToken.Muted, wrap: true),
            Ui.Columns(4, 8, rows),
            SwitchRow(layout.FollowApp, _viewModel.ToggleFollowApp),
            SwitchRow(layout.AlwaysVisibleRow, _viewModel.ToggleAlwaysVisibleRow),
            selector,
            layout.ProfileSelectorNote.Length == 0 ? null : Note(layout.ProfileSelectorNote),
            Spaced(Ui.Text(layout.ColumnsTitle, 15, bold: true)),
            Ui.Columns(3, 8, columns),
            Spaced(Ui.Text(layout.ShowKeysTitle, 15, bold: true)),
            Ui.Columns(2, 8, keys)
        );
        return column;
    }

    private void ShowTransparency(TransparencyModel model, TransparencyModel? shown)
    {
        _opacity.Show(
            model.OpacityTitle,
            model.OpacityValue,
            model.OpacityPercent,
            Percent(SettingsSchema.Opacity.Min),
            Percent(SettingsSchema.Opacity.Max),
            Percent(SettingsSchema.Opacity.Step),
            Percent(SettingsSchema.OpacityButtonStep),
            model.OpacityLessName,
            model.OpacityMoreName
        );
        _dim.Show(
            model.DimTitle,
            model.DimValue,
            model.DimPercent,
            Percent(SettingsSchema.DimTo.Min),
            Percent(SettingsSchema.DimTo.Max),
            Percent(SettingsSchema.DimTo.Step),
            Percent(SettingsSchema.DimTo.Step),
            model.DimLessName,
            model.DimMoreName
        );
        _preview.Opacity = model.OpacityPercent / 100d;
        if (
            shown is not null
            && string.Equals(shown.Caption, model.Caption, StringComparison.Ordinal)
            && string.Equals(shown.Behind, model.Behind, StringComparison.Ordinal)
            && shown.AutoDim == model.AutoDim
        )
        {
            return;
        }

        _transparency.Content = Ui.Column(
            10,
            Spaced(Caption(model.Caption)),
            Ui.Card(
                Ui.Column(10, _opacity.Detached(), Document(model.Behind)),
                ColorToken.Card,
                null,
                12,
                new Thickness(14)
            ),
            SwitchRow(model.AutoDim, _viewModel.ToggleAutoDim),
            Ui.Card(_dim.Detached(), ColorToken.Card, null, 12, new Thickness(14))
        );
    }

    /// <summary>The preview of the opacity over a «document» (GEN-009): light stripes and the panel on top.</summary>
    private Grid Document(string behind)
    {
        var paper = ThemeCatalog.GetPalette(ThemeId.Light);
        var light = new SolidColorBrush(paper.GetColor(ColorToken.Win));
        var dark = new SolidColorBrush(paper.GetColor(ColorToken.CardHi));
        var stripes = new DrawingBrush
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 20, 20),
            ViewportUnits = BrushMappingMode.Absolute,
            Transform = new RotateTransform(45),
            Drawing = new DrawingGroup
            {
                Children =
                {
                    new GeometryDrawing(light, null, new RectangleGeometry(new Rect(0, 0, 10, 20))),
                    new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(10, 0, 10, 20))),
                },
            },
        };
        var document = new Grid { Height = 84, ClipToBounds = true };
        document.Children.Add(
            new Border { Background = stripes, CornerRadius = new CornerRadius(10) }
        );
        var label = Ui.Text(behind, 12, mono: true);
        label.Foreground = new SolidColorBrush(paper.GetColor(ColorToken.Muted));
        label.Margin = new Thickness(12, 14, 0, 0);
        label.VerticalAlignment = VerticalAlignment.Top;
        label.HorizontalAlignment = HorizontalAlignment.Left;
        document.Children.Add(label);
        if (_preview.Parent is Panel old)
        {
            old.Children.Remove(_preview);
        }

        var cards = new UniformGrid { Columns = 3 };
        for (var i = 0; i < 3; i++)
        {
            var card = new Border { CornerRadius = new CornerRadius(6), Margin = new Thickness(3) };
            Ui.Ink(card, Border.BackgroundProperty, ColorToken.Card);
            cards.Children.Add(card);
        }

        _preview.Child = cards;
        _preview.Padding = new Thickness(5);
        _preview.Margin = new Thickness(0, 12, 12, 12);
        _preview.HorizontalAlignment = HorizontalAlignment.Right;
        document.SizeChanged += (_, e) => _preview.Width = e.NewSize.Width * 0.55;
        document.Children.Add(_preview);
        AutomationProperties.SetName(document, behind);
        return document;
    }

    // ---- Right column --------------------------------------------------------------------------------------------

    private StackPanel DockSection(DockModel dock)
    {
        var sides = new List<UIElement>();
        foreach (var option in dock.Sides)
        {
            sides.Add(
                OptionCard(
                    Ui.Column(
                        8,
                        SideMiniature(option.Value, dock.Gutter),
                        Centered(Ui.Text(option.Label, 13, bold: true))
                    ),
                    option.Name,
                    option.Selected,
                    () => _viewModel.SetDockSide(option.Value),
                    new Thickness(10)
                )
            );
        }

        var back = StepButton(
            dock.Vertical ? "arrow_upward" : "arrow_back",
            dock.HandleBackName,
            dock.CanBack,
            _viewModel.HandleBack,
            ColorToken.CardHi
        );
        var forward = StepButton(
            dock.Vertical ? "arrow_downward" : "arrow_forward",
            dock.HandleForwardName,
            dock.CanForward,
            _viewModel.HandleForward,
            ColorToken.CardHi
        );
        forward.Margin = new Thickness(8, 0, 0, 0);
        var handle = CardRow(
            "open_with",
            Ui.Column(
                2,
                Ui.Text(dock.HandleTitle, 15, bold: true, wrap: true),
                Ui.Text(dock.HandleDescription, 13, ink: ColorToken.Muted, wrap: true)
            ),
            Ui.Row(0, back, forward)
        );

        var counts = Ui.Row(4);
        foreach (var option in dock.PerPage)
        {
            var button = Ui.Choice(
                Ui.Text(
                    option.Label,
                    14,
                    bold: true,
                    ink: option.Selected ? ColorToken.OnAccent : ColorToken.Text
                ),
                option.Name,
                option.Selected,
                () => _viewModel.SetPerPage(option.Value),
                44,
                10,
                ColorToken.CardHi
            );
            CcChrome.Paint(
                button,
                option.Selected ? ColorToken.Accent : ColorToken.CardHi,
                option.Selected ? ColorToken.OnAccent : ColorToken.Text,
                null
            );
            button.BorderThickness = new Thickness(0);
            button.Width = 44;
            button.Padding = new Thickness(0);
            counts.Children.Add(button);
        }

        var perPage = CardRow(
            "view_agenda",
            Ui.Text(dock.PerPageTitle, 15, bold: true, wrap: true),
            counts
        );
        return Ui.Column(
            10,
            Caption(dock.Caption),
            Ui.Text(dock.Explain, 14, ink: ColorToken.Muted, wrap: true),
            Ui.Columns(4, 8, sides),
            handle,
            SwitchRow(dock.HandleLock, _viewModel.ToggleHandleLock),
            perPage,
            SwitchRow(dock.AutoHide, _viewModel.ToggleAutoHide),
            SwitchRow(dock.KeepScrollbar, _viewModel.ToggleKeepScrollbar)
        );
    }

    /// <summary>The miniature of an edge (GEN-010): the scroll bar on the right and the bar on its edge.</summary>
    private static Border SideMiniature(DockSide side, bool gutter)
    {
        var area = new Grid { Height = 64, ClipToBounds = true };
        var scroll = new Border { Width = 6, HorizontalAlignment = HorizontalAlignment.Right };
        Ui.Ink(scroll, Border.BackgroundProperty, ColorToken.Line);
        area.Children.Add(scroll);
        var bar = new Border { CornerRadius = new CornerRadius(3) };
        Ui.Ink(bar, Border.BackgroundProperty, ColorToken.Accent);
        switch (side)
        {
            case DockSide.Left:
                bar.Width = 10;
                bar.HorizontalAlignment = HorizontalAlignment.Left;
                bar.Margin = new Thickness(0, 10, 0, 10);
                break;
            case DockSide.Top:
                bar.Height = 10;
                bar.VerticalAlignment = VerticalAlignment.Top;
                bar.Margin = new Thickness(14, 0, 14, 0);
                break;
            case DockSide.Bottom:
                bar.Height = 10;
                bar.VerticalAlignment = VerticalAlignment.Bottom;
                bar.Margin = new Thickness(14, 0, 14, 0);
                break;
            default:
                bar.Width = 10;
                bar.HorizontalAlignment = HorizontalAlignment.Right;
                bar.Margin = new Thickness(0, 10, gutter ? 8 : 0, 10);
                break;
        }

        area.Children.Add(bar);
        return Ui.Card(area, ColorToken.Side, ColorToken.Border, 6, new Thickness(0));
    }

    private StackPanel SafetySection(SafetyModel safety)
    {
        var options = new List<UIElement>();
        foreach (var option in safety.MaxHold)
        {
            var button = Ui.Choice(
                Ui.Text(
                    option.Label,
                    12,
                    bold: true,
                    ink: option.Selected ? ColorToken.OnAccent : ColorToken.Text
                ),
                option.Name,
                option.Selected,
                () => _viewModel.SetMaxHold(option.Value),
                44,
                8,
                null
            );
            CcChrome.Paint(
                button,
                option.Selected ? ColorToken.Accent : null,
                option.Selected ? ColorToken.OnAccent : ColorToken.Text,
                null
            );
            button.BorderThickness = new Thickness(0);
            button.Padding = new Thickness(6, 0, 6, 0);
            options.Add(button);
        }

        var segments = Ui.Card(
            Ui.Columns(options.Count, 2, options),
            ColorToken.Side,
            null,
            10,
            new Thickness(3)
        );
        AutomationProperties.SetName(segments, safety.MaxHoldTitle);
        return Section(
            safety.Caption,
            Ui.Card(
                Ui.Column(
                    8,
                    Ui.Text(safety.MaxHoldTitle, 15, bold: true, wrap: true),
                    segments,
                    Ui.Text(safety.MaxHoldDescription, 13, ink: ColorToken.Muted, wrap: true)
                ),
                ColorToken.Card,
                null,
                12,
                new Thickness(14)
            ),
            SwitchRow(safety.ReleaseOnAppSwitch, _viewModel.ToggleReleaseOnAppSwitch)
        );
    }

    private StackPanel AccessSection(AccessModel access)
    {
        var ink = access.ResetArmed ? ColorToken.OnDanger : ColorToken.Text;
        var reset = Ui.Button(
            ButtonRow(
                Ui.Icon("star", 22, access.ResetArmed ? ColorToken.OnDanger : ColorToken.Accent),
                access.ResetTitle,
                access.ResetDescription,
                ink,
                access.ResetArmed ? ColorToken.OnDanger : ColorToken.Muted,
                null
            ),
            access.ResetName,
            _viewModel.ResetFrequents,
            access.ResetArmed ? ColorToken.Danger : ColorToken.Card,
            ink,
            height: double.NaN,
            radius: 12
        );
        StretchButton(reset, 52);
        AutomationProperties.SetHelpText(reset, access.ResetDescription);
        var times = Segments(
            access.TimesTitle,
            access.Times.Select(option =>
                (
                    option.Label,
                    option.Name,
                    option.Selected,
                    (Action)(() => _viewModel.SetTimeMultiplier(option.Value))
                )
            )
        );
        var section = Section(
            access.Caption,
            SwitchRow(access.ReduceMotion, _viewModel.ToggleReduceMotion),
            Ui.Card(
                Ui.Column(
                    8,
                    Ui.Text(access.TimesTitle, 15, bold: true, wrap: true),
                    times,
                    Ui.Text(access.TimesDescription, 13, ink: ColorToken.Muted, wrap: true)
                ),
                ColorToken.Card,
                null,
                12,
                new Thickness(14)
            ),
            SwitchRow(access.Hotkey, _viewModel.ToggleGlobalHotkey)
        );
        if (!access.Hotkeys.IsEmpty)
        {
            // BUR-005, D10: the combination comes from a closed list; chips, never a combo box.
            var chips = Ui.Wrap(
                6,
                access.Hotkeys.Select(option =>
                    (UIElement)
                        Ui.Choice(
                            Ui.Text(option.Label, 13, mono: true),
                            option.Name,
                            option.Selected,
                            () => _viewModel.SetGlobalHotkey(option.Value),
                            44,
                            8
                        )
                )
            );
            AutomationProperties.SetName(chips, access.HotkeyChoicesName);
            section.Children.Add(
                Ui.Card(
                    Ui.Column(8, Ui.Text(access.HotkeyChoicesName, 13, bold: true), chips),
                    ColorToken.Card,
                    null,
                    12,
                    new Thickness(14)
                )
            );
        }

        section.Children.Add(reset);
        return section;
    }

    // GEN-015: the state of the consent, the AI on or off and the saved key.
    private StackPanel AiSection(AiModel ai)
    {
        var consent = Ui.Button(
            Ui.Text(ai.ConsentAction, 13, bold: true),
            ai.ConsentAction,
            _viewModel.ToggleAiConsent,
            ColorToken.CardHi,
            radius: 10
        );
        var consentText = Ui.Column(
            2,
            Ui.Text(ai.ConsentState, 15, bold: true, wrap: true),
            Ui.Text(ai.ConsentDetail, 13, ink: ColorToken.Muted, wrap: true)
        );
        var keyText = Ui.Text(ai.KeyState, 15, bold: true, wrap: true);
        keyText.VerticalAlignment = VerticalAlignment.Center;
        UIElement keyEnd = new Border();
        if (ai.DeleteKeyText is { } delete)
        {
            var button = Ui.Button(
                Ui.IconLabel(
                    ai.DeleteKeyArmed ? "warning" : "delete",
                    delete,
                    18,
                    13,
                    ink: ai.DeleteKeyArmed ? ColorToken.OnDanger : ColorToken.DangerText,
                    iconInk: ai.DeleteKeyArmed ? ColorToken.OnDanger : ColorToken.DangerText
                ),
                delete,
                _viewModel.DeleteAiKey,
                ai.DeleteKeyArmed ? ColorToken.Danger : null,
                stroke: ai.DeleteKeyArmed ? ColorToken.Danger : ColorToken.Border,
                radius: 10
            );
            if (ai.DeleteKeyArmed)
            {
                AutomationProperties.SetLiveSetting(button, AutomationLiveSetting.Polite);
            }

            keyEnd = button;
        }

        return Section(
            ai.Caption,
            SwitchRow(ai.Use, _viewModel.ToggleAi),
            CardRow(ai.Consent ? "verified_user" : "gpp_maybe", consentText, consent),
            CardRow("key", keyText, keyEnd)
        );
    }

    private static Border Segments(
        string name,
        IEnumerable<(string Label, string Name, bool Selected, Action Click)> choices
    )
    {
        var options = new List<UIElement>();
        foreach (var (label, optionName, selected, click) in choices)
        {
            var button = Ui.Choice(
                Ui.Text(
                    label,
                    12,
                    bold: true,
                    ink: selected ? ColorToken.OnAccent : ColorToken.Text
                ),
                optionName,
                selected,
                click,
                44,
                8,
                null
            );
            CcChrome.Paint(
                button,
                selected ? ColorToken.Accent : null,
                selected ? ColorToken.OnAccent : ColorToken.Text,
                null
            );
            button.BorderThickness = new Thickness(0);
            button.Padding = new Thickness(6, 0, 6, 0);
            options.Add(button);
        }

        var segments = Ui.Card(
            Ui.Columns(options.Count, 2, options),
            ColorToken.Side,
            null,
            10,
            new Thickness(3)
        );
        AutomationProperties.SetName(segments, name);
        return segments;
    }

    private StackPanel StartSection(StartModel start)
    {
        var welcome = Ui.Button(
            ButtonRow(
                Ui.Icon("waving_hand", 22, ColorToken.Accent),
                start.WelcomeTitle,
                start.WelcomeDescription,
                ColorToken.Text,
                ColorToken.Muted,
                Ui.Icon("chevron_right", 22, ColorToken.Muted)
            ),
            start.WelcomeTitle,
            _viewModel.SeeWelcome,
            ColorToken.Card,
            height: double.NaN,
            radius: 12
        );
        StretchButton(welcome, 56);
        AutomationProperties.SetHelpText(welcome, start.WelcomeDescription);
        var coach = Ui.Button(
            ButtonRow(
                Ui.Icon("help", 22, ColorToken.Accent),
                start.CoachTitle,
                start.CoachDescription,
                ColorToken.Text,
                ColorToken.Muted,
                null
            ),
            start.CoachTitle,
            _viewModel.SeeTabGuide,
            ColorToken.Card,
            height: double.NaN,
            radius: 12
        );
        StretchButton(coach, 56);
        AutomationProperties.SetHelpText(coach, start.CoachDescription);
        return Section(
            start.Caption,
            welcome,
            coach,
            SwitchRow(start.NoKeyboard, _viewModel.ToggleNoKeyboard)
        );
    }

    // ---- Builders ------------------------------------------------------------------------------------------------

    private static double Percent(double fraction) => Math.Round(fraction * 100);

    private static TextBlock Caption(string text) =>
        Ui.Text(text.ToUpperInvariant(), 13, bold: true, ink: ColorToken.Muted);

    private static StackPanel Section(string caption, params UIElement?[] children) =>
        Ui.Column(10, [Spaced(Caption(caption)), .. children]);

    private static T Spaced<T>(T element)
        where T : FrameworkElement
    {
        element.Margin = new Thickness(0, 12, 0, 0);
        return element;
    }

    private static T Centered<T>(T element)
        where T : FrameworkElement
    {
        element.HorizontalAlignment = HorizontalAlignment.Center;
        return element;
    }

    private static TextBlock Note(string text)
    {
        var note = Ui.Text(text, 13, ink: ColorToken.Muted, wrap: true);
        note.Margin = new Thickness(14, -4, 0, 0);
        return note;
    }

    private static Border Cell(bool selected, double height, double top)
    {
        var cell = new Border
        {
            Height = height,
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, top, 0, 0),
        };
        Ui.Ink(cell, Border.BackgroundProperty, selected ? ColorToken.Accent : ColorToken.CardHi);
        return cell;
    }

    private static CcToggle OptionCard(
        UIElement content,
        string name,
        bool selected,
        Action click,
        Thickness padding
    )
    {
        var card = Ui.Choice(content, name, selected, click, 44, 12);
        card.Height = double.NaN;
        card.MinHeight = 44;
        card.Padding = padding;
        card.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        card.VerticalContentAlignment = VerticalAlignment.Stretch;
        return card;
    }

    private static CcToggle SwitchRow(SwitchItem item, Action click) =>
        Ui.SwitchRow(item.Icon, item.Title, item.Description, item.On, click);

    private static CcButton StepButton(
        string icon,
        string name,
        bool enabled,
        Action click,
        ColorToken fill
    )
    {
        var button = Ui.Button(Ui.Icon(icon, 20), name, click, fill);
        button.Width = 44;
        button.Padding = new Thickness(0);
        button.BorderThickness = new Thickness(0);
        button.IsEnabled = enabled;
        button.Opacity = enabled ? 1 : 0.4;
        return button;
    }

    private static Border CardRow(string icon, UIElement text, UIElement end)
    {
        var row = new DockPanel { LastChildFill = true };
        var symbol = Ui.Icon(icon, 22, ColorToken.Accent);
        symbol.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(symbol, Dock.Left);
        row.Children.Add(symbol);
        if (end is FrameworkElement element)
        {
            element.Margin = new Thickness(10, 0, 0, 0);
            element.VerticalAlignment = VerticalAlignment.Center;
        }

        DockPanel.SetDock(end, Dock.Right);
        row.Children.Add(end);
        row.Children.Add(text);
        return Ui.Card(row, ColorToken.Card, null, 12, new Thickness(14, 12, 14, 12));
    }

    private static DockPanel ButtonRow(
        UIElement icon,
        string title,
        string description,
        ColorToken ink,
        ColorToken muted,
        UIElement? end
    )
    {
        var row = new DockPanel { LastChildFill = true };
        if (icon is FrameworkElement start)
        {
            start.Margin = new Thickness(0, 0, 12, 0);
        }

        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        if (end is not null)
        {
            DockPanel.SetDock(end, Dock.Right);
            row.Children.Add(end);
        }

        row.Children.Add(
            Ui.Column(
                2,
                Ui.Text(title, 15, bold: true, ink: ink, wrap: true),
                Ui.Text(description, 13, ink: muted, wrap: true)
            )
        );
        return row;
    }

    private static void StretchButton(CcButton button, double minHeight)
    {
        button.MinHeight = minHeight;
        button.Padding = new Thickness(14, 10, 14, 10);
        button.BorderThickness = new Thickness(0);
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
    }
}
