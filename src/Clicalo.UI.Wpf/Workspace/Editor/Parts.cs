using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Editor;

/// <summary>
/// The pieces the editor, «Atajos» and «Plantillas» compose with direction C (D12): a section with its overline, the
/// four buttons, a line of help with its icon and the track of a segmented choice. Only tokens: no measure of their own.
/// </summary>
internal static class Parts
{
    /// <summary>U+2060 WORD JOINER: no line breaks on either side of it.</summary>
    private const char Joiner = '⁠';

    /// <summary>A section: its <see cref="Overline"/> (when it has a title) and its content, <see cref="Space.S2"/> apart.</summary>
    /// <param name="title">The title, or null for none.</param>
    /// <param name="children">The content; nulls are skipped.</param>
    public static StackPanel Section(string? title, params UIElement?[] children)
    {
        var column = new StackPanel();
        if (!string.IsNullOrEmpty(title))
        {
            column.Children.Add(new Overline(title));
        }

        foreach (var child in children)
        {
            if (child is null)
            {
                continue;
            }

            if (column.Children.Count > 0 && child is FrameworkElement element)
            {
                var margin = element.Margin;
                element.Margin = new Thickness(
                    margin.Left,
                    margin.Top + Space.S2,
                    margin.Right,
                    margin.Bottom
                );
            }

            column.Children.Add(child);
        }

        return column;
    }

    /// <summary>A text of <paramref name="style"/> that wraps (descriptions, help, warnings).</summary>
    /// <param name="text">The text.</param>
    /// <param name="ink">Its color.</param>
    /// <param name="style">Its style (<see cref="TextStyleToken.BodySmall"/> by default).</param>
    public static StyledText Note(
        string text,
        ColorToken ink = ColorToken.Muted,
        TextStyleToken style = TextStyleToken.BodySmall
    ) => new(Unbroken(text), style, ink) { MaxLines = 0 };

    /// <summary>
    /// <paramref name="text"/> with its combinations of keys glued (§5: an identifier never breaks): «Ctrl+N» keeps
    /// together at the end of a line, with a word joiner on each side of every «+» between two characters.
    /// </summary>
    /// <param name="text">A text that may name keys.</param>
    public static string Unbroken(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.Contains('+', StringComparison.Ordinal))
        {
            return text;
        }

        var result = new System.Text.StringBuilder(text.Length + 8);
        for (var i = 0; i < text.Length; i++)
        {
            var glue =
                text[i] == '+'
                && i > 0
                && i < text.Length - 1
                && !char.IsWhiteSpace(text[i - 1])
                && !char.IsWhiteSpace(text[i + 1]);
            if (glue)
            {
                result.Append(Joiner).Append('+').Append(Joiner);
            }
            else
            {
                result.Append(text[i]);
            }
        }

        return result.ToString();
    }

    /// <summary>An icon of <see cref="IconBox.Small"/> followed by a text that wraps beside it.</summary>
    /// <param name="icon">The icon.</param>
    /// <param name="text">The text.</param>
    /// <param name="iconInk">The color of the icon.</param>
    /// <param name="ink">The color of the text.</param>
    /// <param name="style">The style of the text.</param>
    public static DockPanel IconLine(
        string? icon,
        string text,
        ColorToken iconInk = ColorToken.Muted,
        ColorToken ink = ColorToken.Muted,
        TextStyleToken style = TextStyleToken.BodySmall
    ) => IconLine(icon, Note(text, ink, style), iconInk);

    /// <summary>An icon of <see cref="IconBox.Small"/> followed by <paramref name="text"/>, a text element of its own.</summary>
    /// <param name="icon">The icon.</param>
    /// <param name="text">The text; it must have no parent.</param>
    /// <param name="iconInk">The color of the icon.</param>
    public static DockPanel IconLine(
        string? icon,
        UIElement text,
        ColorToken iconInk = ColorToken.Muted
    )
    {
        var line = new DockPanel { LastChildFill = true };
        if (!string.IsNullOrEmpty(icon))
        {
            var symbol = Ui.Icon(icon, IconBox.Small, iconInk);
            symbol.VerticalAlignment = VerticalAlignment.Top;
            symbol.Margin = new Thickness(0, 0, Space.S2, 0);
            DockPanel.SetDock(symbol, Dock.Left);
            line.Children.Add(symbol);
        }

        line.Children.Add(text);
        return line;
    }

    /// <summary>One of the four buttons of direction C, named with its text (or <paramref name="name"/>).</summary>
    /// <param name="text">Its text; empty for an icon button.</param>
    /// <param name="symbol">Its icon, or null.</param>
    /// <param name="variant">Primary, secondary, tertiary or danger.</param>
    /// <param name="click">What it does.</param>
    /// <param name="name">Its accessible name when it is not its text.</param>
    public static TouchButton Button(
        string text,
        string? symbol,
        ButtonVariant variant,
        Action click,
        string? name = null
    )
    {
        ArgumentNullException.ThrowIfNull(click);
        TouchButton button =
            text.Length == 0
                ? new IconButton { Symbol = symbol, Variant = variant }
                : new TouchButton
                {
                    Content = text,
                    Symbol = symbol,
                    Variant = variant,
                };
        AutomationProperties.SetName(button, name ?? text);
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>
    /// The track of a segmented choice: <see cref="ControlHeight.Medium"/> on <c>side</c> with its line, and the
    /// segments of 44 inside it, side by side and equal.
    /// </summary>
    /// <param name="segments">The segments (<see cref="Segment"/>).</param>
    public static Border Track(IReadOnlyList<UIElement> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var track = Ui.Card(
            Ui.Columns(Math.Max(1, segments.Count), Space.S1, segments),
            ColorToken.Side,
            ColorToken.Border,
            Radii.Medium,
            new Thickness(Space.S1)
        );
        return track;
    }

    /// <summary>A segment of a <see cref="Track"/>: an option without fill; chosen, it has the single look of «elegido».</summary>
    /// <param name="label">Its text.</param>
    /// <param name="selected">Whether it is the chosen one.</param>
    /// <param name="click">What a tap does.</param>
    public static OptionButton Segment(string label, bool selected, Action click)
    {
        ArgumentNullException.ThrowIfNull(click);
        var option = new OptionButton
        {
            Kind = OptionKind.Segment,
            Content = label,
            IsSelected = selected,
        };
        AutomationProperties.SetName(option, label);
        option.Click += (_, _) => click();
        return option;
    }

    /// <summary>A layer over the content (D12): the surface of the window, so nothing under it shows through.</summary>
    /// <param name="child">What it shows.</param>
    public static Border Layer(UIElement child)
    {
        var layer = new Border { Child = child };
        Ui.Ink(layer, Border.BackgroundProperty, ColorToken.Win);
        return layer;
    }
}
