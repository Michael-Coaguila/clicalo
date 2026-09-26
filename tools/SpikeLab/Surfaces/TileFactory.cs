using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.UI.Wpf.Automation;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// Creates the real <see cref="ShortcutTile"/> of each laboratory tile with a simple template: its voice number in
/// the top-left corner, its glyph and its name. Colors are system colors, so high contrast applies live (S3 row 13).
/// Tiles never take the keyboard focus.
/// </summary>
internal static class TileFactory
{
    private static readonly ControlTemplate Template = BuildTemplate();

    /// <summary>
    /// A tile <paramref name="width"/> logical pixels wide and at least <paramref name="height"/> high (never below 44,
    /// REG-02); it grows when its name needs two lines.
    /// </summary>
    public static ShortcutTile Create(LabTile tile, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(tile);
        var control = new ShortcutTile
        {
            AccessibleName = tile.Name,
            Pattern = Map(tile.Pattern),
            Tag = tile.Glyph,
            Width = Math.Max(44, width),
            MinHeight = Math.Max(44, height),
            Margin = new Thickness(4),
            Focusable = false,
            IsTabStop = false,
            FontSize = 15,
            BorderThickness = new Thickness(2),
            Template = Template,
        };
        ShowHighlighted(control, highlighted: false);
        return control;
    }

    /// <summary>The UI Automation pattern of a lab pattern.</summary>
    public static ShortcutTilePattern Map(CommandPattern pattern) =>
        pattern switch
        {
            CommandPattern.Toggle => ShortcutTilePattern.Toggle,
            CommandPattern.ExpandCollapse => ShortcutTilePattern.ExpandCollapse,
            _ => ShortcutTilePattern.Invoke,
        };

    /// <summary>Highlighted (latched, expanded or flashing) or normal, with system colors.</summary>
    public static void ShowHighlighted(ShortcutTile control, bool highlighted)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetResourceReference(
            System.Windows.Controls.Control.BackgroundProperty,
            highlighted ? SystemColors.HighlightBrushKey : SystemColors.ControlBrushKey
        );
        control.SetResourceReference(
            System.Windows.Controls.Control.ForegroundProperty,
            highlighted ? SystemColors.HighlightTextBrushKey : SystemColors.ControlTextBrushKey
        );
        control.SetResourceReference(
            System.Windows.Controls.Control.BorderBrushProperty,
            highlighted ? SystemColors.HighlightTextBrushKey : SystemColors.ActiveBorderBrushKey
        );
    }

    /// <summary>
    /// Routes the UI Automation events of <paramref name="control"/> to <paramref name="command"/>: the pattern and,
    /// for ExpandCollapse, whether to expand.
    /// </summary>
    public static void WireAutomation(ShortcutTile control, Action<CommandPattern, bool> command)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(command);
        control.Invoked += (_, _) => command(CommandPattern.Invoke, false);
        control.Toggled += (_, _) => command(CommandPattern.Toggle, false);
        control.ExpandRequested += (_, _) => command(CommandPattern.ExpandCollapse, true);
        control.CollapseRequested += (_, _) => command(CommandPattern.ExpandCollapse, false);
    }

    private static ControlTemplate BuildTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(
            Border.BackgroundProperty,
            new TemplateBindingExtension(System.Windows.Controls.Control.BackgroundProperty)
        );
        border.SetValue(
            Border.BorderBrushProperty,
            new TemplateBindingExtension(System.Windows.Controls.Control.BorderBrushProperty)
        );
        border.SetValue(
            Border.BorderThicknessProperty,
            new TemplateBindingExtension(System.Windows.Controls.Control.BorderThicknessProperty)
        );

        var grid = new FrameworkElementFactory(typeof(Grid));
        var number = new FrameworkElementFactory(typeof(TextBlock));
        number.SetBinding(TextBlock.TextProperty, Parent(nameof(ShortcutTile.VoiceNumber)));
        number.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        number.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        number.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 2, 0, 0));
        number.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        number.SetValue(TextBlock.FontSizeProperty, 14.0);
        grid.AppendChild(number);

        var stack = new FrameworkElementFactory(typeof(StackPanel));
        stack.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        stack.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 6, 4, 4));
        var glyph = new FrameworkElementFactory(typeof(TextBlock));
        glyph.SetBinding(TextBlock.TextProperty, Parent(nameof(FrameworkElement.Tag)));
        glyph.SetValue(TextBlock.FontSizeProperty, 20.0);
        glyph.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        stack.AppendChild(glyph);
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, Parent(nameof(ShortcutTile.AccessibleName)));
        name.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        name.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        stack.AppendChild(name);
        grid.AppendChild(stack);
        border.AppendChild(grid);

        var template = new ControlTemplate(typeof(ShortcutTile)) { VisualTree = border };
        template.Seal();
        return template;
    }

    private static Binding Parent(string path) =>
        new(path) { RelativeSource = RelativeSource.TemplatedParent };
}
