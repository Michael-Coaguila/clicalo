using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The one dashed line of direction C (D12): the rest line of a disabled control and the outline of a place that is not
/// a control yet (the dashed «+ [add]» tile, «Biblioteca», «+ Más» of the profiles, the empty grid). Every surface draws
/// it with the same dash, the same <c>line</c> color and the same thickness (2 in high contrast), so the panel, the Tab
/// view, the Control Center and the welcome show the same thing the same way.
/// </summary>
public static class DashedLine
{
    /// <summary>The dash, in units of the stroke thickness: dashes as long as the ring of «elegido», with gaps as long.</summary>
    public static DoubleCollection Dash { get; } = Frozen();

    /// <summary>
    /// An outline in the dashed line, drawn inside the box of whatever it lies over: <c>line</c>, the rest thickness of
    /// the theme and <paramref name="radius"/>. It never takes a touch.
    /// </summary>
    /// <param name="radius">The corner radius (<see cref="Radii"/>).</param>
    /// <returns>The outline.</returns>
    public static Rectangle Outline(double radius = Radii.Medium)
    {
        var outline = new Rectangle
        {
            RadiusX = radius,
            RadiusY = radius,
            StrokeDashArray = Dash,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false,
        };
        outline.SetResourceReference(Shape.StrokeProperty, ThemeBrushKey.For(ColorToken.Line));
        outline.SetResourceReference(Shape.StrokeThicknessProperty, ThemeKeys.LineThickness);
        return outline;
    }

    private static DoubleCollection Frozen()
    {
        var dash = new DoubleCollection([Stroke.Selected, Stroke.Selected]);
        dash.Freeze();
        return dash;
    }
}
