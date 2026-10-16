using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.Domain.Settings;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.General;

/// <summary>
/// The pieces of direction C (D12, docs/design/sistema.md) that «General y panel», «Precisión táctil», «Sistema»,
/// «Acerca de» and the welcome share, so their groups, options, rows and texts have the same borders, baselines and
/// paddings: a page title, a group (an outlined frame on the surface with its <see cref="Overline"/>), options with the
/// single look of «elegido», theme swatches, setting rows and texts of the type scale. Only tokens: no loose measure.
/// </summary>
internal static class SettingsUi
{
    /// <summary>
    /// The width of the window that shows <paramref name="view"/>: the outermost element above it (the window itself,
    /// or the root a host draws the window's content in), so a page follows the window's breakpoints (GEN-001,
    /// CCM-005) wherever it is drawn; the view's own width when nothing holds it.
    /// </summary>
    public static double WindowWidth(FrameworkElement view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var top = view;
        for (
            var node = VisualTreeHelper.GetParent(view) ?? view.Parent;
            node is not null;
            node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node)
        )
        {
            if (node is FrameworkElement element)
            {
                top = element;
            }
        }

        return top.ActualWidth;
    }

    /// <summary>A text of the type scale in <paramref name="ink"/>, in <paramref name="lines"/> lines (0: no limit).</summary>
    public static StyledText Styled(
        string text,
        TextStyleToken style,
        ColorToken ink = ColorToken.Text,
        int lines = 1
    ) => new(text, style, ink) { MaxLines = lines, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// The title of a page of the Control Center (<see cref="TextStyleToken.Title"/>, a heading for UI Automation) and
    /// its subtitle in <see cref="TextStyleToken.BodySmall"/>, <see cref="Space.S1"/> apart.
    /// </summary>
    public static StackPanel PageHeader(StyledText title, StyledText subtitle)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(subtitle);
        title.TextStyle = TextStyleToken.Title;
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level1);
        subtitle.TextStyle = TextStyleToken.BodySmall;
        subtitle.Ink = ColorToken.Muted;
        subtitle.MaxLines = 0;
        return Ui.Column(Space.S1, title, subtitle);
    }

    /// <summary>A new title and subtitle for <see cref="PageHeader"/>.</summary>
    public static (StyledText Title, StyledText Subtitle) NewHeader() =>
        (
            Styled(string.Empty, TextStyleToken.Title),
            Styled(string.Empty, TextStyleToken.BodySmall)
        );

    /// <summary>
    /// A group of a page: an outlined frame on the surface (radius <see cref="Radii.Large"/>, the decorative line inside,
    /// padding <see cref="Space.S4"/>) with its <see cref="Overline"/> and the controls <see cref="Space.S3"/> apart. The
    /// frame is not elevated: its controls are the second level, so there are never more than two nested borders.
    /// </summary>
    public static Border Group(string caption, params UIElement?[] children)
    {
        var column = Ui.Column(Space.S3, [new Overline(caption), .. children]);
        return Frame(column, caption);
    }

    /// <summary>The outlined frame of <see cref="Group"/> around <paramref name="child"/>, named for UI Automation.</summary>
    public static Border Frame(UIElement child, string name)
    {
        var frame = new Border
        {
            Child = child,
            CornerRadius = new CornerRadius(Radii.Large),
            Padding = new Thickness(Space.S4),
            SnapsToDevicePixels = true,
        };
        frame.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        Ui.Ink(frame, Border.BorderBrushProperty, ColorToken.Border);
        AutomationProperties.SetName(frame, name);
        return frame;
    }

    /// <summary>
    /// An option of <paramref name="kind"/> that the view model chooses (a tap only raises <c>Click</c>), with the
    /// single look of «elegido». A string content shows ✓ when chosen; a view's own content brings its mark
    /// (<see cref="CheckLabel"/>). UI Automation: a radio button with SelectionItem (<see cref="OptionRole.Option"/>).
    /// </summary>
    public static OptionButton Option(
        OptionKind kind,
        object content,
        string name,
        bool selected,
        Action click,
        string? symbol = null,
        OptionRole role = OptionRole.Option
    )
    {
        ArgumentNullException.ThrowIfNull(click);
        var option = new OptionButton
        {
            Kind = kind,
            Content = content,
            Symbol = string.IsNullOrEmpty(symbol) ? null : symbol,
            Role = role,
            IsSelected = selected,
        };
        AutomationProperties.SetName(option, name);
        option.Click += (_, _) => click();
        return option;
    }

    /// <summary>
    /// An option whose content is a column (a miniature, a description): it grows with its content from
    /// <paramref name="least"/>, padded <see cref="Space.S3"/>, and fills the height of its row so the options of a
    /// group have the same height and baseline.
    /// </summary>
    public static OptionButton Tall(OptionButton option, double least = ControlHeight.Large)
    {
        ArgumentNullException.ThrowIfNull(option);
        option.Height = double.NaN;
        option.MinHeight = least;
        option.Padding = new Thickness(Space.S3);
        option.VerticalAlignment = VerticalAlignment.Stretch;
        option.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        option.VerticalContentAlignment = VerticalAlignment.Center;
        return option;
    }

    /// <summary>
    /// The label of an option with its own content: ✓ before the text while chosen, as a tile without icon shows it
    /// (the same look of «elegido» everywhere), centered, in one line with «…».
    /// </summary>
    public static StackPanel CheckLabel(
        string text,
        bool selected,
        TextStyleToken style = TextStyleToken.Label
    )
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        if (selected)
        {
            var check = Ui.Icon("check", IconBox.Small, ColorToken.AccentInk);
            check.Margin = new Thickness(0, 0, Space.S1, 0);
            row.Children.Add(check);
        }

        row.Children.Add(Styled(text, style));
        return row;
    }

    /// <summary>
    /// A theme option (GEN-003, BIE-008): a tile of 44 with the swatch of the theme, its name in one line and ✓ at the
    /// end while chosen, as in the quick settings of the mockup.
    /// </summary>
    public static OptionButton ThemeOption(
        ThemeChoice theme,
        string label,
        string name,
        bool selected,
        Action click
    )
    {
        var row = new DockPanel { LastChildFill = true };
        var swatch = Swatch(theme);
        swatch.Margin = new Thickness(0, 0, Space.S2, 0);
        DockPanel.SetDock(swatch, Dock.Left);
        row.Children.Add(swatch);
        if (selected)
        {
            var check = Ui.Icon("check", IconBox.Small, ColorToken.AccentInk);
            check.Margin = new Thickness(Space.S2, 0, 0, 0);
            DockPanel.SetDock(check, Dock.Right);
            row.Children.Add(check);
        }

        row.Children.Add(Styled(label, TextStyleToken.Label));
        var option = Option(OptionKind.Tile, row, name, selected, click);
        option.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        return option;
    }

    /// <summary>
    /// The swatch of a theme: a circle of <see cref="IconBox.Small"/> split between the background and the accent of
    /// its palette («Como Windows»: the light and the dark background), with the control line of the current theme.
    /// </summary>
    public static Ellipse Swatch(ThemeChoice theme)
    {
        var (first, second) = theme switch
        {
            ThemeChoice.Light => Pair(
                ThemeId.Light,
                ColorToken.Win,
                ThemeId.Light,
                ColorToken.Accent
            ),
            ThemeChoice.Dark => Pair(ThemeId.Dark, ColorToken.Win, ThemeId.Dark, ColorToken.Accent),
            ThemeChoice.HighContrast => Pair(
                ThemeId.HighContrast,
                ColorToken.Win,
                ThemeId.HighContrast,
                ColorToken.Accent
            ),
            _ => Pair(ThemeId.Light, ColorToken.Win, ThemeId.Dark, ColorToken.Win),
        };
        var fill = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
        };
        fill.GradientStops.Add(new GradientStop(first, 0));
        fill.GradientStops.Add(new GradientStop(first, 0.5));
        fill.GradientStops.Add(new GradientStop(second, 0.5));
        fill.GradientStops.Add(new GradientStop(second, 1));
        fill.Freeze();
        var swatch = new Ellipse
        {
            Width = IconBox.Small,
            Height = IconBox.Small,
            Fill = fill,
            StrokeThickness = Stroke.Line,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Ui.Ink(swatch, Shape.StrokeProperty, ColorToken.Line);
        return swatch;

        static (Color, Color) Pair(ThemeId a, ColorToken at, ThemeId b, ColorToken bt) =>
            (ThemeCatalog.GetPalette(a).GetColor(at), ThemeCatalog.GetPalette(b).GetColor(bt));
    }

    /// <summary>
    /// A setting row: an icon in its box of 24 in <c>muted</c> (as a switch row), the title in
    /// <see cref="TextStyleToken.Body"/> and its description in <see cref="TextStyleToken.Caption"/>, and
    /// <paramref name="end"/> at the end; at least <see cref="ControlHeight.Medium"/> high, so it lines up with the
    /// switch rows of the same group.
    /// </summary>
    public static DockPanel SettingRow(
        string? icon,
        string title,
        string? description,
        UIElement? end
    )
    {
        var row = new DockPanel { LastChildFill = true, MinHeight = ControlHeight.Medium };
        if (!string.IsNullOrEmpty(icon))
        {
            var symbol = Ui.Icon(icon, IconBox.Medium, ColorToken.Muted);
            symbol.Width = IconBox.Medium;
            symbol.Margin = new Thickness(0, 0, Space.S3, 0);
            DockPanel.SetDock(symbol, Dock.Left);
            row.Children.Add(symbol);
        }

        if (end is FrameworkElement element)
        {
            element.Margin = new Thickness(Space.S3, 0, 0, 0);
            element.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(element, Dock.Right);
            row.Children.Add(element);
        }

        var texts = Ui.Column(
            0,
            Styled(title, TextStyleToken.Body, lines: 2),
            string.IsNullOrEmpty(description)
                ? null
                : Styled(description, TextStyleToken.Caption, ColorToken.Muted, lines: 0)
        );
        texts.VerticalAlignment = VerticalAlignment.Center;
        texts.Margin = new Thickness(0, Space.S2, 0, Space.S2);
        row.Children.Add(texts);
        return row;
    }

    /// <summary>
    /// The title of a setting with its value at the end, both in <see cref="TextStyleToken.Label"/> (the value in
    /// <c>text</c>, never monospace: that is only for keys).
    /// </summary>
    public static DockPanel TitleWithValue(StyledText title, StyledText value)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(value);
        var row = new DockPanel { LastChildFill = true };
        value.Margin = new Thickness(Space.S3, 0, 0, 0);
        DockPanel.SetDock(value, Dock.Right);
        row.Children.Add(value);
        row.Children.Add(title);
        return row;
    }

    /// <summary>A button of one of the four variants, named by its text.</summary>
    public static TouchButton Button(
        string text,
        ButtonVariant variant,
        Action click,
        string? symbol = null,
        ControlSize size = ControlSize.Small,
        string? name = null
    )
    {
        ArgumentNullException.ThrowIfNull(click);
        var button = new TouchButton
        {
            Content = text,
            Variant = variant,
            Symbol = string.IsNullOrEmpty(symbol) ? null : symbol,
            Size = size,
        };
        AutomationProperties.SetName(button, name ?? text);
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>A secondary icon button of 44 (−, +, ←, →, copy), named for UI Automation.</summary>
    public static IconButton IconAction(
        string symbol,
        string name,
        Action click,
        bool enabled = true,
        ButtonVariant variant = ButtonVariant.Secondary
    )
    {
        ArgumentNullException.ThrowIfNull(click);
        var button = new IconButton
        {
            Symbol = symbol,
            Variant = variant,
            IsEnabled = enabled,
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>
    /// A grid of <paramref name="columns"/> equal columns, <see cref="Space.S2"/> apart, whose rows are as tall as
    /// their tallest item: the options of a group share borders, height and baseline.
    /// </summary>
    public static Grid Options(int columns, IReadOnlyList<UIElement> options) =>
        Ui.Columns(columns, Space.S2, options);

    /// <summary>
    /// A row of segments in their track (the look of <see cref="SegmentedControl"/>), named for UI Automation; each
    /// segment is a radio button with SelectionItem.
    /// </summary>
    public static Border Track(string name, IReadOnlyList<OptionButton> segments)
    {
        var row = new UniformGrid { Rows = 1 };
        foreach (var segment in segments)
        {
            segment.Margin = new Thickness(Space.S1 / 2);
            row.Children.Add(segment);
        }

        var track = new Border
        {
            Child = row,
            CornerRadius = new CornerRadius(Radii.Medium),
            Padding = new Thickness(Space.S1 / 2),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        track.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        Ui.Ink(track, Border.BackgroundProperty, ColorToken.Side);
        Ui.Ink(track, Border.BorderBrushProperty, ColorToken.Border);
        AutomationProperties.SetName(track, name);
        return track;
    }
}
