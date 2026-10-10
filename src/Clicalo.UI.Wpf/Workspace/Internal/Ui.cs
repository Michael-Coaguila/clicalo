using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// Small builders of the Control Center's views in code: texts on the type scale (so the text size of the settings
/// applies), icons, rows, cards and buttons, all painted with theme tokens.
/// </summary>
internal static class Ui
{
    /// <summary>Below this width a switch row has no room for its text beside the icon and the switch.</summary>
    private const double SwitchRowStackWidth = 220;

    /// <summary>A text on the type scale.</summary>
    public static TextBlock Text(
        string text,
        double px,
        bool bold = false,
        ColorToken ink = ColorToken.Text,
        bool mono = false,
        bool wrap = false
    ) => Fill(new TextBlock(), text, px, bold, ink, mono, wrap);

    /// <summary>
    /// A separator drawn as text (the «+» between two keys), on the type scale: UI Automation does not see it
    /// (<see cref="DecorativeText"/>, UIA008).
    /// </summary>
    public static TextBlock Separator(
        string text,
        double px,
        bool bold = false,
        ColorToken ink = ColorToken.Muted
    ) => Fill(new DecorativeText(), text, px, bold, ink, mono: false, wrap: false);

    private static TextBlock Fill(
        TextBlock block,
        string text,
        double px,
        bool bold,
        ColorToken ink,
        bool mono,
        bool wrap
    )
    {
        block.Text = text;
        block.FontWeight = bold ? FontWeights.Bold : FontWeights.Normal;
        block.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        block.TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis;
        block.VerticalAlignment = VerticalAlignment.Center;
        block.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(px));
        Ink(block, TextBlock.ForegroundProperty, ink);
        if (mono)
        {
            block.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.MonoFont);
            block.FontWeight = AppFonts.MonoWeight;
        }

        return block;
    }

    /// <summary>A Material Symbols icon; never a name for UI Automation (UIA008).</summary>
    public static SymbolIcon Icon(string? symbol, double size, ColorToken? ink = null)
    {
        var icon = new SymbolIcon
        {
            Symbol = string.IsNullOrEmpty(symbol) ? null : symbol,
            Size = size,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        if (ink is { } token)
        {
            Ink(icon, SymbolIcon.ForegroundProperty, token);
        }

        return icon;
    }

    /// <summary>References the brush of <paramref name="token"/> for <paramref name="property"/>.</summary>
    public static void Ink(
        FrameworkElement element,
        DependencyProperty property,
        ColorToken token
    ) => element.SetResourceReference(property, ThemeBrushKey.For(token));

    /// <summary>A rounded card filled and outlined with theme tokens.</summary>
    public static Border Card(
        UIElement? child,
        ColorToken? fill,
        ColorToken? stroke,
        double radius,
        Thickness padding,
        double strokeWidth = 1
    )
    {
        var card = new Border
        {
            Child = child,
            CornerRadius = new CornerRadius(radius),
            Padding = padding,
            BorderThickness = stroke is null ? new Thickness(0) : new Thickness(strokeWidth),
            SnapsToDevicePixels = true,
        };
        if (fill is { } background)
        {
            Ink(card, Border.BackgroundProperty, background);
        }

        if (stroke is { } border)
        {
            Ink(card, Border.BorderBrushProperty, border);
        }

        return card;
    }

    /// <summary>
    /// A horizontal row of <paramref name="children"/>, <paramref name="gap"/> apart. When the last one is a wrapping
    /// text, it takes the rest of the width and wraps.
    /// </summary>
    public static Panel Row(double gap, params UIElement?[] children)
    {
        var items = children.OfType<UIElement>().ToList();
        if (items.Count == 0 || items[^1] is not TextBlock { TextWrapping: TextWrapping.Wrap })
        {
            return Stack(Orientation.Horizontal, gap, children);
        }

        var dock = new DockPanel { LastChildFill = true };
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0 && items[i] is FrameworkElement element)
            {
                var margin = element.Margin;
                element.Margin = new Thickness(
                    margin.Left + gap,
                    margin.Top,
                    margin.Right,
                    margin.Bottom
                );
            }

            if (i < items.Count - 1)
            {
                DockPanel.SetDock(items[i], Dock.Left);
            }

            dock.Children.Add(items[i]);
        }

        return dock;
    }

    /// <summary>A Material Symbols icon in the tint of a shortcut category (TEM-003).</summary>
    public static SymbolIcon CategoryIcon(string? symbol, double size, string category)
    {
        var icon = Icon(symbol, size);
        if (Enum.TryParse<CategoryToken>(category, ignoreCase: true, out var token))
        {
            icon.SetResourceReference(SymbolIcon.ForegroundProperty, CategoryBrushKey.Tint(token));
        }
        else
        {
            Ink(icon, SymbolIcon.ForegroundProperty, ColorToken.Accent);
        }

        return icon;
    }

    /// <summary>A vertical column of <paramref name="children"/>, <paramref name="gap"/> apart.</summary>
    public static StackPanel Column(double gap, params UIElement?[] children) =>
        Stack(Orientation.Vertical, gap, children);

    /// <summary>Children that wrap to the next line, <paramref name="gap"/> apart in both directions.</summary>
    public static WrapPanel Wrap(double gap, IEnumerable<UIElement> children)
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 0, -gap, -gap) };
        foreach (var child in children)
        {
            if (child is FrameworkElement element)
            {
                element.Margin = new Thickness(0, 0, gap, gap);
            }

            panel.Children.Add(child);
        }

        return panel;
    }

    /// <summary>A grid of <paramref name="columns"/> equal columns, <paramref name="gap"/> apart.</summary>
    public static Grid Columns(int columns, double gap, IReadOnlyList<UIElement> children)
    {
        var grid = new Grid();
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            );
        }

        var rows = (children.Count + columns - 1) / Math.Max(1, columns);
        for (var r = 0; r < rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var column = i % columns;
            var row = i / columns;
            if (child is FrameworkElement element)
            {
                element.Margin = new Thickness(
                    column == 0 ? 0 : gap / 2,
                    row == 0 ? 0 : gap / 2,
                    column == columns - 1 ? 0 : gap / 2,
                    row == rows - 1 ? 0 : gap / 2
                );
            }

            Grid.SetColumn(child, column);
            Grid.SetRow(child, row);
            grid.Children.Add(child);
        }

        return grid;
    }

    /// <summary>An icon followed by a label.</summary>
    public static StackPanel IconLabel(
        string? icon,
        string label,
        double iconSize = 18,
        double px = 13,
        bool bold = true,
        ColorToken? iconInk = null,
        ColorToken ink = ColorToken.Text
    )
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (!string.IsNullOrEmpty(icon))
        {
            var symbol = Icon(icon, iconSize, iconInk);
            symbol.Margin = new Thickness(0, 0, label.Length == 0 ? 0 : 6, 0);
            row.Children.Add(symbol);
        }

        if (label.Length > 0)
        {
            row.Children.Add(Text(label, px, bold, ink));
        }

        return row;
    }

    /// <summary>
    /// A button with <paramref name="content"/>, named <paramref name="name"/> for UI Automation. With
    /// <paramref name="expanded"/> it is the header of a collapsible: ExpandCollapse with that state instead of Invoke
    /// (ACC-001).
    /// </summary>
    public static CcButton Button(
        object content,
        string name,
        Action click,
        ColorToken? fill = null,
        ColorToken ink = ColorToken.Text,
        ColorToken? stroke = null,
        double height = 44,
        double radius = 10,
        bool? expanded = null
    )
    {
        ArgumentNullException.ThrowIfNull(click);
        var button = new CcButton
        {
            Content = content,
            Height = height,
            IsExpanded = expanded,
        };
        CcChrome.Paint(button, fill, ink, stroke);
        button.SetValue(CcChrome.RadiusProperty, new CornerRadius(radius));
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>
    /// A button that shows a state: accentWash with a 2 px accent outline when <paramref name="on"/>, card with a
    /// border otherwise. For UI Automation it is what <paramref name="role"/> says (ACC-001): a switch with Toggle (the
    /// default), a choice of a group with SelectionItem, or the header of a collapsible with ExpandCollapse.
    /// </summary>
    public static CcToggle Choice(
        object content,
        string name,
        bool on,
        Action click,
        double height = 44,
        double radius = 10,
        ColorToken? offFill = ColorToken.Card,
        CcToggleRole role = CcToggleRole.Toggle
    )
    {
        ArgumentNullException.ThrowIfNull(click);
        var toggle = new CcToggle
        {
            Content = content,
            Height = height,
            Role = role,
        };
        PaintChoice(toggle, on, offFill);
        toggle.SetValue(CcChrome.RadiusProperty, new CornerRadius(radius));
        AutomationProperties.SetName(toggle, name);
        toggle.Click += (_, _) => click();
        return toggle;
    }

    /// <summary>
    /// Marks a <see cref="Choice"/> as chosen or not, in place: a long list changes its mark without building its
    /// buttons again (EDI-014).
    /// </summary>
    /// <param name="toggle">The choice.</param>
    /// <param name="on">Whether it is the chosen one.</param>
    /// <param name="offFill">Its fill while it is not chosen.</param>
    public static void PaintChoice(CcToggle toggle, bool on, ColorToken? offFill = ColorToken.Card)
    {
        ArgumentNullException.ThrowIfNull(toggle);
        toggle.IsChecked = on;
        if (on)
        {
            CcChrome.Paint(toggle, ColorToken.AccentWash, ColorToken.Text, ColorToken.Accent);
            toggle.BorderThickness = new Thickness(2);
        }
        else
        {
            CcChrome.Paint(toggle, offFill, ColorToken.Text, ColorToken.Border);
            toggle.BorderThickness = new Thickness(1);
        }
    }

    /// <summary>
    /// A row that is a switch as a whole (docs/07: «toda la fila es tocable»): icon, title and description, and the
    /// 48 × 28 switch drawn at the end; the Toggle pattern for UI Automation.
    /// </summary>
    public static CcToggle SwitchRow(
        string? icon,
        string title,
        string description,
        bool on,
        Action click
    )
    {
        var text = Column(
            2,
            Text(title, 14, bold: true, wrap: true),
            description.Length == 0
                ? null
                : Text(description, 12, ink: ColorToken.Muted, wrap: true)
        );
        var knob = new ToggleSwitch
        {
            IsChecked = on,
            IsHitTestVisible = false,
            Focusable = false,
            IsDrawingOnly = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var layout = new DockPanel { LastChildFill = true };
        SymbolIcon? symbol = null;
        if (icon is not null)
        {
            symbol = Icon(icon, 22, ColorToken.Accent);
            symbol.Margin = new Thickness(0, 0, 12, 0);
            DockPanel.SetDock(symbol, Dock.Left);
            layout.Children.Add(symbol);
        }

        layout.Children.Add(knob);
        layout.Children.Add(text);
        PlaceSwitch(knob, symbol, double.PositiveInfinity);
        layout.SizeChanged += (_, e) => PlaceSwitch(knob, symbol, e.NewSize.Width);
        var row = new CcToggle
        {
            Content = layout,
            IsChecked = on,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(14, 10, 14, 10),
            MinHeight = 56,
        };
        CcChrome.Paint(row, ColorToken.Card, ColorToken.Text, null);
        row.SetValue(CcChrome.RadiusProperty, new CornerRadius(12));
        AutomationProperties.SetName(row, title);
        AutomationProperties.SetHelpText(row, description);
        row.Click += (_, _) => click();
        return row;
    }

    /// <summary>
    /// The switch goes at the end of its row; where the row is too narrow to leave room for the text beside it (a
    /// narrow column of the smallest window, CCM-005), it goes under the text and the icon is left out, so the text
    /// keeps the width of the row and nothing is cut.
    /// </summary>
    private static void PlaceSwitch(ToggleSwitch knob, SymbolIcon? symbol, double width)
    {
        var below = width < SwitchRowStackWidth;
        DockPanel.SetDock(knob, below ? Dock.Bottom : Dock.Right);
        knob.HorizontalAlignment = below ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        knob.Margin = below ? new Thickness(0, 6, 0, 0) : new Thickness(12, 0, 0, 0);
        symbol?.Visibility = below ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>A small caption above a group of controls (12 px, bold, muted).</summary>
    public static TextBlock Caption(string text) =>
        Text(text, 12, bold: true, ink: ColorToken.Muted);

    /// <summary>A thin line of the theme.</summary>
    public static Border Line(double margin = 0)
    {
        var line = new Border
        {
            Height = 1,
            Margin = new Thickness(0, margin, 0, margin),
            Opacity = 0.6,
        };
        Ink(line, Border.BackgroundProperty, ColorToken.Line);
        return line;
    }

    private static StackPanel Stack(Orientation orientation, double gap, UIElement?[] children)
    {
        var panel = new StackPanel { Orientation = orientation };
        var first = true;
        foreach (var child in children)
        {
            if (child is null)
            {
                continue;
            }

            if (!first && child is FrameworkElement element)
            {
                var margin = element.Margin;
                element.Margin =
                    orientation == Orientation.Horizontal
                        ? new Thickness(margin.Left + gap, margin.Top, margin.Right, margin.Bottom)
                        : new Thickness(margin.Left, margin.Top + gap, margin.Right, margin.Bottom);
            }

            panel.Children.Add(child);
            first = false;
        }

        return panel;
    }
}
