using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Clicalo.UI.Wpf.Windowing.Internal;

/// <summary>
/// The root of a surface with a <see cref="SurfaceLook"/>: a rounded <see cref="Border"/> with the window's background
/// and border that also clips its content to the inner rounded shape, so nothing paints (or takes a touch) in the
/// transparent corners (spike S6).
/// </summary>
internal sealed class SurfaceFrame : Border
{
    /// <summary>A circle or a pill: every radius is half the shorter side.</summary>
    public static readonly DependencyProperty IsRoundProperty = DependencyProperty.Register(
        nameof(IsRound),
        typeof(bool),
        typeof(SurfaceFrame),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsArrange)
    );

    /// <summary>Whether every radius is half the shorter side.</summary>
    public bool IsRound
    {
        get => (bool)GetValue(IsRoundProperty);
        set => SetValue(IsRoundProperty, value);
    }

    /// <summary>The template of a surface window with <paramref name="look"/>: this frame, an adorner layer and the content.</summary>
    public static ControlTemplate Template(SurfaceLook look)
    {
        var frame = new FrameworkElementFactory(typeof(SurfaceFrame));
        frame.SetValue(
            BackgroundProperty,
            new TemplateBindingExtension(Control.BackgroundProperty)
        );
        frame.SetValue(
            BorderBrushProperty,
            new TemplateBindingExtension(Control.BorderBrushProperty)
        );
        frame.SetValue(
            BorderThicknessProperty,
            new TemplateBindingExtension(Control.BorderThicknessProperty)
        );
        frame.SetValue(CornerRadiusProperty, look.Corners);
        frame.SetValue(IsRoundProperty, look.Round);
        var adorners = new FrameworkElementFactory(typeof(AdornerDecorator));
        adorners.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        frame.AppendChild(adorners);
        var template = new ControlTemplate(typeof(Window)) { VisualTree = frame };
        template.Seal();
        return template;
    }

    /// <summary>A rounded rectangle with a radius per corner.</summary>
    public static Geometry Rounded(Rect bounds, CornerRadius corners)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(
                new Point(bounds.Left + corners.TopLeft, bounds.Top),
                isFilled: true,
                isClosed: true
            );
            context.LineTo(
                new Point(bounds.Right - corners.TopRight, bounds.Top),
                isStroked: true,
                isSmoothJoin: false
            );
            Corner(
                context,
                new Point(bounds.Right, bounds.Top + corners.TopRight),
                corners.TopRight
            );
            context.LineTo(
                new Point(bounds.Right, bounds.Bottom - corners.BottomRight),
                isStroked: true,
                isSmoothJoin: false
            );
            Corner(
                context,
                new Point(bounds.Right - corners.BottomRight, bounds.Bottom),
                corners.BottomRight
            );
            context.LineTo(
                new Point(bounds.Left + corners.BottomLeft, bounds.Bottom),
                isStroked: true,
                isSmoothJoin: false
            );
            Corner(
                context,
                new Point(bounds.Left, bounds.Bottom - corners.BottomLeft),
                corners.BottomLeft
            );
            context.LineTo(
                new Point(bounds.Left, bounds.Top + corners.TopLeft),
                isStroked: true,
                isSmoothJoin: false
            );
            Corner(context, new Point(bounds.Left + corners.TopLeft, bounds.Top), corners.TopLeft);
        }

        geometry.Freeze();
        return geometry;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (IsRound)
        {
            var round = new CornerRadius(Math.Min(arrangeSize.Width, arrangeSize.Height) / 2);
            if (CornerRadius != round)
            {
                CornerRadius = round;
            }
        }

        var size = base.ArrangeOverride(arrangeSize);
        if (Child is { } child)
        {
            var t = BorderThickness;
            var inner = new Rect(
                0,
                0,
                Math.Max(0, size.Width - t.Left - t.Right),
                Math.Max(0, size.Height - t.Top - t.Bottom)
            );
            var c = CornerRadius;
            var max = Math.Min(inner.Width, inner.Height) / 2;
            child.Clip = Rounded(
                inner,
                new CornerRadius(
                    Inset(c.TopLeft, Math.Max(t.Left, t.Top), max),
                    Inset(c.TopRight, Math.Max(t.Right, t.Top), max),
                    Inset(c.BottomRight, Math.Max(t.Right, t.Bottom), max),
                    Inset(c.BottomLeft, Math.Max(t.Left, t.Bottom), max)
                )
            );
        }

        return size;
    }

    private static double Inset(double radius, double thickness, double max) =>
        Math.Clamp(radius - thickness, 0, max);

    private static void Corner(StreamGeometryContext context, Point to, double radius)
    {
        if (radius <= 0)
        {
            context.LineTo(to, isStroked: true, isSmoothJoin: false);
            return;
        }

        context.ArcTo(
            to,
            new Size(radius, radius),
            0,
            isLargeArc: false,
            SweepDirection.Clockwise,
            isStroked: true,
            isSmoothJoin: false
        );
    }
}
