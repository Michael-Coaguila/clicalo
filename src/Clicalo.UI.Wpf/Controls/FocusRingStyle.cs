using System.Windows;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The visible keyboard focus of every control (TEM-009, blueprint §8.6): a ring <see cref="FocusRing.Thickness"/>
/// (3) px wide, <see cref="FocusRing.Offset"/> (2) px outside the control, in the <c>focusRing</c> token (accent;
/// yellow in Clícalo's high contrast, the system highlight in a Windows contrast theme). It is a
/// <c>FocusVisualStyle</c>, so WPF shows it only when the focus arrived from the keyboard: on a surface, during the
/// keyboard and voice mode (<c>KeyboardNavigation</c> lease).
/// </summary>
/// <remarks>The styles are sealed, so one instance serves every UI thread.</remarks>
public static class FocusRingStyle
{
    /// <summary>The ring of a shortcut tile (tile corner radius).</summary>
    public static Style Tile { get; } = Create(Radii.Tile);

    /// <summary>The ring of a button (button corner radius).</summary>
    public static Style Button { get; } = Create(Radii.Button);

    /// <summary>A sealed focus visual style that follows a control whose corners have <paramref name="cornerRadius"/>.</summary>
    /// <param name="cornerRadius">Corner radius of the control, in device-independent pixels.</param>
    public static Style Create(double cornerRadius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);
        var outset = FocusRing.Offset + FocusRing.Thickness;
        var ring = new FrameworkElementFactory(typeof(Border));
        ring.SetValue(FrameworkElement.MarginProperty, new Thickness(-outset));
        ring.SetValue(Border.BorderThicknessProperty, new Thickness(FocusRing.Thickness));
        ring.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius + outset));
        ring.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        ring.SetValue(UIElement.IsHitTestVisibleProperty, false);
        ring.SetResourceReference(
            Border.BorderBrushProperty,
            ThemeBrushKey.For(ColorToken.FocusRing)
        );

        var template = new ControlTemplate(typeof(Control)) { VisualTree = ring };
        template.Seal();
        var style = new Style(typeof(Control));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Seal();
        return style;
    }
}
