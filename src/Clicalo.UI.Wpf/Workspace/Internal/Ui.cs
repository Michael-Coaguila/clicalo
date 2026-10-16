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
            Ink(icon, SymbolIcon.ForegroundProperty, ColorToken.AccentInk);
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
    /// A button of direction C with <paramref name="content"/>, named <paramref name="name"/> for UI Automation: the
    /// variant follows from the old fill, text and outline (<see cref="VariantOf"/>) until each view names its
    /// variant. With <paramref name="expanded"/> it is the header of a collapsible: ExpandCollapse with that state
    /// instead of Invoke (ACC-001).
    /// </summary>
    public static TouchButton Button(
        object content,
        string name,
        Action click,
        ColorToken? fill = null,
        ColorToken ink = ColorToken.Text,
        ColorToken? stroke = null,
        double height = ControlHeight.Small,
        double radius = Radii.Medium,
        bool? expanded = null
    )
    {
        ArgumentNullException.ThrowIfNull(click);
        _ = radius;
        var button = new TouchButton { Content = content, IsExpanded = expanded };
        Paint(button, fill, ink, stroke);
        button.Height = height;
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>
    /// The variant of direction C that takes the place of an old fill, text and outline: accent fill → primary;
    /// danger → danger; no fill and no outline → tertiary; anything else → secondary. An armed fill (warn) or a
    /// highlighted one (accent wash with an accent outline) is drawn pressed.
    /// </summary>
    public static (ButtonVariant Variant, bool Pressed) VariantOf(
        ColorToken? fill,
        ColorToken ink,
        ColorToken? stroke
    ) =>
        (fill, ink, stroke) switch
        {
            (ColorToken.Accent, _, _) or (_, ColorToken.OnAccent, _) => (
                ButtonVariant.Primary,
                false
            ),
            (ColorToken.Danger or ColorToken.DangerWash, _, _)
            or (_, ColorToken.DangerText or ColorToken.OnDanger, _) => (
                ButtonVariant.Danger,
                false
            ),
            (ColorToken.Warn, _, _) => (ButtonVariant.Secondary, true),
            (ColorToken.AccentWash, _, ColorToken.Accent) => (ButtonVariant.Secondary, true),
            (null, _, null) => (ButtonVariant.Tertiary, false),
            _ => (ButtonVariant.Secondary, false),
        };

    /// <summary>
    /// Gives <paramref name="control"/> the look of direction C that takes the place of an old fill, text and outline:
    /// a button gets its variant (<see cref="VariantOf"/>); an option keeps the look of its kind and state; any other
    /// control is painted with the tokens.
    /// </summary>
    public static void Paint(Control control, ColorToken? fill, ColorToken ink, ColorToken? stroke)
    {
        ArgumentNullException.ThrowIfNull(control);
        switch (control)
        {
            case TouchButton button:
                var (variant, pressed) = VariantOf(fill, ink, stroke);
                button.Variant = variant;
                button.IsActive = pressed;
                break;
            case OptionButton or SystemSection.SystemTabItem:
                // «Elegido» has one look, drawn by the option itself from its state.
                break;
            default:
                if (fill is { } background)
                {
                    Ink(control, Control.BackgroundProperty, background);
                }

                Ink(control, Control.ForegroundProperty, ink);
                if (stroke is { } line)
                {
                    Ink(control, Control.BorderBrushProperty, line);
                }

                break;
        }
    }

    /// <summary>
    /// An option of direction C: chosen or not (the single look of «elegido»: wash, the ring of 2 inside and a filled
    /// icon or ✓). With an <paramref name="offFill"/> it is a tile with its card and line; without, it has no fill of
    /// its own. For UI Automation it is what <paramref name="role"/> says (ACC-001): a choice of a group with
    /// SelectionItem, a switch with Toggle, or the header of a collapsible with ExpandCollapse.
    /// </summary>
    public static OptionButton Choice(
        object content,
        string name,
        bool on,
        Action click,
        double height = ControlHeight.Small,
        double radius = Radii.Medium,
        ColorToken? offFill = ColorToken.Card,
        OptionRole role = OptionRole.Toggle
    )
    {
        ArgumentNullException.ThrowIfNull(click);
        _ = radius;
        var option = new OptionButton
        {
            Content = content,
            Kind = offFill is null ? OptionKind.Segment : OptionKind.Tile,
            Role = role,
            IsSelected = on,
        };
        option.Height = height;
        AutomationProperties.SetName(option, name);
        option.Click += (_, _) => click();
        return option;
    }

    /// <summary>
    /// Marks a <see cref="Choice"/> as chosen or not, in place: a long list changes its mark without building its
    /// buttons again (EDI-014).
    /// </summary>
    /// <param name="option">The choice.</param>
    /// <param name="on">Whether it is the chosen one.</param>
    public static void PaintChoice(OptionButton option, bool on)
    {
        ArgumentNullException.ThrowIfNull(option);
        option.IsSelected = on;
    }

    /// <summary>
    /// A row that is a switch as a whole (docs/07: «toda la fila es tocable»): icon, title and description, and the
    /// 48 × 28 switch at the end; the Toggle pattern for UI Automation. Its state is the view model's.
    /// </summary>
    public static ToggleSwitch SwitchRow(
        string? icon,
        string title,
        string description,
        bool on,
        Action click
    )
    {
        ArgumentNullException.ThrowIfNull(click);
        var row = new ToggleSwitch
        {
            Content = title,
            Symbol = icon,
            Description = description.Length == 0 ? null : description,
            IsChecked = on,
            FollowsViewModel = true,
        };
        AutomationProperties.SetName(row, title);
        AutomationProperties.SetHelpText(row, description);
        row.Click += (_, _) => click();
        return row;
    }

    /// <summary>The section label: the single <see cref="Overline"/> of direction C.</summary>
    public static Overline Caption(string text) => new(text);

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
