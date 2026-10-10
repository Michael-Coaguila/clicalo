using System.Windows;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The visible keyboard focus of a text field (TEM-009, blueprint §8.6): the same ring as every other control,
/// <see cref="FocusRing.Thickness"/> (3) px wide and <see cref="FocusRing.Offset"/> (2) px outside the field, in the
/// <c>focusRing</c> token (accent; yellow in high contrast). Unlike <see cref="FocusRingStyle"/>, which WPF shows only
/// when the focus arrived from the keyboard, a field shows it whenever it has the keyboard: what is typed or dictated
/// goes there, however the focus arrived (a tap, 🎤, a lease).
/// </summary>
public static class FieldFocusRing
{
    /// <summary>
    /// Adds the ring to <paramref name="layers"/>, the panel that fills the inside of the field's border, and shows it
    /// while <paramref name="box"/> has the keyboard.
    /// </summary>
    /// <param name="layers">The panel inside the border of the field, where the text box is.</param>
    /// <param name="box">The text box of the field.</param>
    /// <param name="fieldRadius">The corner radius of the field.</param>
    /// <param name="fieldBorder">The thickness of the field's own border, which the ring leaves inside.</param>
    /// <returns>The ring, collapsed until the box takes the keyboard.</returns>
    public static Border Attach(
        Panel layers,
        UIElement box,
        double fieldRadius,
        double fieldBorder
    )
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(box);
        ArgumentOutOfRangeException.ThrowIfNegative(fieldRadius);
        ArgumentOutOfRangeException.ThrowIfNegative(fieldBorder);
        var outset = FocusRing.Offset + FocusRing.Thickness;
        var ring = new Border
        {
            Margin = new Thickness(-(outset + fieldBorder)),
            BorderThickness = new Thickness(FocusRing.Thickness),
            CornerRadius = new CornerRadius(fieldRadius + outset),
            SnapsToDevicePixels = true,
            IsHitTestVisible = false,
            Focusable = false,
            Visibility = box.IsKeyboardFocusWithin ? Visibility.Visible : Visibility.Collapsed,
        };
        ring.SetResourceReference(
            Border.BorderBrushProperty,
            ThemeBrushKey.For(ColorToken.FocusRing)
        );
        layers.Children.Add(ring);
        box.IsKeyboardFocusWithinChanged += (_, e) =>
            ring.Visibility = e.NewValue is true ? Visibility.Visible : Visibility.Collapsed;
        return ring;
    }
}
