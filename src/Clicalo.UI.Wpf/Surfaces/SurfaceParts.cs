using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>The small pieces the surfaces of the Tab view and the bubble are built from: icons, texts and buttons.</summary>
internal static class SurfaceParts
{
    /// <summary>A Material Symbols icon in the color of <paramref name="token"/>.</summary>
    public static SymbolIcon Icon(string symbol, double size, ColorToken token)
    {
        var icon = new SymbolIcon
        {
            Symbol = symbol,
            Size = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        icon.SetResourceReference(SymbolIcon.ForegroundProperty, ThemeBrushKey.For(token));
        return icon;
    }

    /// <summary>A line of text of the type scale in the color of <paramref name="token"/>.</summary>
    public static TextBlock Text(double designPx, ColorToken token, bool bold = false)
    {
        var text = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        };
        text.SetResourceReference(
            TextBlock.FontSizeProperty,
            ThemeKeys.TextSize(Math.Max(TypeScale.Minimum, designPx))
        );
        text.SetResourceReference(TextBlock.ForegroundProperty, ThemeBrushKey.For(token));
        return text;
    }

    /// <summary>
    /// A button with an icon and, optionally, a label: UI Automation Invoke runs <paramref name="tap"/> through its
    /// Click; the surface's pointer layer runs it directly for touch.
    /// </summary>
    public static TouchButton Button(
        string symbol,
        double iconSize,
        ButtonAppearance appearance,
        Action tap,
        double width = double.NaN,
        double height = double.NaN
    )
    {
        var button = new TouchButton
        {
            Symbol = symbol,
            IconSize = iconSize,
            Appearance = appearance,
            Width = width,
            Height = height,
            Padding = new Thickness(2),
            MinWidth = 0,
            MinHeight = 0,
            Focusable = false,
            IsTabStop = false,
        };
        button.Click += (_, _) => tap();
        return button;
    }

    /// <summary>
    /// Puts the icon above the label inside <paramref name="button"/> (the narrow bar of the Tab view), the icon in the
    /// foreground of the button; returns the label to fill.
    /// </summary>
    public static TextBlock Stack(TouchButton button, string symbol, double iconSize)
    {
        ArgumentNullException.ThrowIfNull(button);
        var icon = new SymbolIcon
        {
            Symbol = symbol,
            Size = iconSize,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _ = icon.SetBinding(
            SymbolIcon.ForegroundProperty,
            new System.Windows.Data.Binding(nameof(Control.Foreground)) { Source = button }
        );
        var label = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0),
        };
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        _ = stack.Children.Add(icon);
        _ = stack.Children.Add(label);
        button.Symbol = null;
        button.Content = stack;
        return label;
    }

    /// <summary>
    /// A small icon button of the bar (close, expand, search, repeat, the pager): drawn at its own size, it still
    /// answers on 44 × 44 around its center (REG-02). UI Automation Invoke runs <paramref name="tap"/>.
    /// </summary>
    public static Clicalo.UI.Wpf.Automation.ShortcutTile SmallButton(
        string symbol,
        double iconSize,
        Action tap,
        bool ghost = false
    )
    {
        var button = Clicalo.UI.Wpf.Surfaces.Panel.PanelChrome.NewButton(
            Clicalo.UI.Wpf.Surfaces.Panel.PanelChrome.Button,
            Clicalo.UI.Wpf.Automation.ShortcutTilePattern.Invoke
        );
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.Tag = new SymbolIcon { Symbol = symbol, Size = iconSize };
        Clicalo.UI.Wpf.Surfaces.Panel.PanelChrome.Paint(
            button,
            ghost ? null : ColorToken.CardHi,
            ColorToken.Text,
            null
        );
        button.Invoked += (_, _) => tap();
        return button;
    }

    /// <summary>Sets the accessible name of <paramref name="element"/>.</summary>
    public static void Name(DependencyObject element, string name) =>
        AutomationProperties.SetName(element, name);

    /// <summary>A thin line between two zones of the bar.</summary>
    public static Border Divider(bool vertical)
    {
        var divider = new Border
        {
            Opacity = 0.7,
            Margin = vertical ? new Thickness(8, 0, 8, 0) : new Thickness(0, 8, 0, 8),
            Height = vertical ? 1 : double.NaN,
            Width = vertical ? double.NaN : 1,
            HorizontalAlignment = vertical
                ? HorizontalAlignment.Stretch
                : HorizontalAlignment.Center,
            VerticalAlignment = vertical ? VerticalAlignment.Center : VerticalAlignment.Stretch,
        };
        divider.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Line));
        return divider;
    }

    /// <summary><see cref="Visibility.Visible"/> or <see cref="Visibility.Collapsed"/>.</summary>
    public static Visibility Shown(bool visible) =>
        visible ? Visibility.Visible : Visibility.Collapsed;
}
