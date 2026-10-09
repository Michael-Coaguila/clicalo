using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The looks of the custom buttons of the body of the panel, drawn on a <see cref="ShortcutTile"/> so UI Automation
/// sees the right pattern (ExpandCollapse for the profile button, a three-state Toggle for the sticky modifiers, Invoke
/// for the rest): a <see cref="TouchTargetBox"/> (REG-02) with a rounded border that draws the control's background
/// and border brush, and the element in <see cref="FrameworkElement.Tag"/> as its content. Colors are theme resources
/// set with <see cref="Paint"/>, so a theme change repaints in place. Sealed: one instance serves every UI thread.
/// </summary>
internal static class PanelChrome
{
    /// <summary>Radius of a page dot (half of its 10 px height, CUA-004).</summary>
    private const double DotRadius = 5;

    /// <summary>The look of a 10 px rounded button (sticky keys, ◀ ▶, the «··· i/N» chip).</summary>
    public static ControlTemplate Button { get; } = Create(Radii.Button);

    /// <summary>The look of a 12 px rounded button (the selector, the profile grid tiles).</summary>
    public static ControlTemplate Large { get; } = Create(Radii.Tile);

    /// <summary>The look of a page dot.</summary>
    public static ControlTemplate Dot { get; } = Create(DotRadius);

    /// <summary>A new tile drawn with <paramref name="template"/>, unfocusable (REG-01).</summary>
    /// <param name="template">One of the looks.</param>
    /// <param name="pattern">The UI Automation pattern.</param>
    public static ShortcutTile NewButton(ControlTemplate template, ShortcutTilePattern pattern) =>
        new()
        {
            Template = template,
            Pattern = pattern,
            Focusable = false,
            IsTabStop = false,
        };

    /// <summary>Paints <paramref name="control"/>; a <see langword="null"/> token is transparent.</summary>
    /// <param name="control">The control.</param>
    /// <param name="background">Its fill.</param>
    /// <param name="foreground">Its text and icons.</param>
    /// <param name="border">Its outline.</param>
    public static void Paint(
        Control control,
        ColorToken? background,
        ColorToken foreground,
        ColorToken? border
    )
    {
        SetBrush(control, Control.BackgroundProperty, background);
        SetBrush(control, Control.BorderBrushProperty, border);
        control.SetResourceReference(Control.ForegroundProperty, ThemeBrushKey.For(foreground));
        control.SetResourceReference(
            Control.BorderThicknessProperty,
            ThemeScope.BorderThicknessKey
        );
    }

    /// <summary>A brush property bound to a theme token, or transparent.</summary>
    /// <param name="element">The element.</param>
    /// <param name="property">The brush property.</param>
    /// <param name="token">The token, or <see langword="null"/> for transparent.</param>
    public static void SetBrush(
        FrameworkElement element,
        DependencyProperty property,
        ColorToken? token
    )
    {
        if (token is { } color)
        {
            element.SetResourceReference(property, ThemeBrushKey.For(color));
        }
        else
        {
            element.SetValue(property, Brushes.Transparent);
        }
    }

    private static ControlTemplate Create(double radius)
    {
        var box = new FrameworkElementFactory(typeof(TouchTargetBox));
        box.SetValue(
            TouchTargetBox.VisualWidthProperty,
            new TemplateBindingExtension(FrameworkElement.WidthProperty)
        );
        box.SetValue(
            TouchTargetBox.VisualHeightProperty,
            new TemplateBindingExtension(FrameworkElement.HeightProperty)
        );

        var chrome = new FrameworkElementFactory(typeof(Border));
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        chrome.SetValue(
            Border.BackgroundProperty,
            new TemplateBindingExtension(Control.BackgroundProperty)
        );
        chrome.SetValue(
            Border.BorderBrushProperty,
            new TemplateBindingExtension(Control.BorderBrushProperty)
        );
        chrome.SetValue(
            Border.BorderThicknessProperty,
            new TemplateBindingExtension(Control.BorderThicknessProperty)
        );
        chrome.SetValue(
            Border.PaddingProperty,
            new TemplateBindingExtension(Control.PaddingProperty)
        );
        chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        box.AppendChild(chrome);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(
            ContentPresenter.ContentProperty,
            new TemplateBindingExtension(FrameworkElement.TagProperty)
        );
        content.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty)
        );
        content.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            new TemplateBindingExtension(Control.VerticalContentAlignmentProperty)
        );
        chrome.AppendChild(content);

        var template = new ControlTemplate(typeof(ShortcutTile)) { VisualTree = box };
        template.Seal();
        return template;
    }
}
