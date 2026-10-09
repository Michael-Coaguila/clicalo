using System.Windows;
using System.Windows.Media;
using Clicalo.Domain.Geometry;
using TouchTargetSize = Clicalo.UI.Wpf.Controls.TouchTarget;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// Where an element answers to touch on screen (REG-02): its bounds in physical pixels and, for a target, an invisible
/// touch margin that grows them around their center to at least 44 × 44 logical pixels without changing the drawing.
/// Where grown targets overlap, the gesture recognizer gives the touch to the nearest center (<c>HitResolver</c>).
/// </summary>
internal static class TouchBounds
{
    /// <summary>
    /// The bounds of <paramref name="element"/> on screen, in physical pixels; with <paramref name="inflate"/>, grown
    /// around its center to at least 44 × 44 logical pixels. Empty while it is hidden or has no window.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="inflate">Whether to add the touch margin of a target.</param>
    public static PhysicalRect Of(FrameworkElement element, bool inflate)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!element.IsVisible || PresentationSource.FromVisual(element) is null)
        {
            return PhysicalRect.Empty;
        }

        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(
            new Point(element.ActualWidth, element.ActualHeight)
        );
        var rect = PhysicalRect.FromEdges(
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            (int)Math.Round(bottomRight.X),
            (int)Math.Round(bottomRight.Y)
        );
        return inflate ? Grow(rect, VisualTreeHelper.GetDpi(element).DpiScaleX) : rect;
    }

    /// <summary>Grows <paramref name="rect"/> around its center to at least 44 × 44 logical pixels.</summary>
    /// <param name="rect">The bounds, in physical pixels.</param>
    /// <param name="scale">Physical pixels per logical pixel.</param>
    public static PhysicalRect Grow(PhysicalRect rect, double scale)
    {
        if (rect.IsEmpty)
        {
            return rect;
        }

        var minimum = (int)Math.Ceiling(TouchTargetSize.MinimumSize * scale);
        var width = Math.Max(rect.Width, minimum);
        var height = Math.Max(rect.Height, minimum);
        return new PhysicalRect(
            rect.Left - ((width - rect.Width) / 2),
            rect.Top - ((height - rect.Height) / 2),
            width,
            height
        );
    }
}
