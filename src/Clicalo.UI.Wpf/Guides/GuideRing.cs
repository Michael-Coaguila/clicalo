using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Guides;

/// <summary>
/// The ring a guide draws around the control a step points at (R-39): a dashed line of <see cref="Stroke.FocusRing"/>
/// in <c>accentInk</c>, a little outside the control, so it never covers what it points at, and it never takes a touch.
/// Dashed, because the solid ring of direction C means «chosen» (D12) and a control pointed at is not chosen. It is
/// drawn on the adorner layer of the control's window.
/// </summary>
public sealed class GuideRing : Adorner
{
    /// <summary>Dashes of twice the line and gaps of one and a half: long enough to read as a line from afar.</summary>
    private static readonly DashStyle Dashes = new([2, 1.5], 0);

    /// <summary>Identifies <see cref="Brush"/>.</summary>
    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush),
        typeof(Brush),
        typeof(GuideRing),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>Creates the ring of <paramref name="adorned"/>.</summary>
    /// <param name="adorned">The control pointed at.</param>
    public GuideRing(UIElement adorned)
        : base(adorned)
    {
        IsHitTestVisible = false;
        SetResourceReference(BrushProperty, ThemeBrushKey.For(ColorToken.AccentInk));
    }

    /// <summary>The color of the line.</summary>
    public Brush? Brush
    {
        get => (Brush?)GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    /// <summary>How far the middle of the line is from the edge of the control: the line and a pixel of air outside it.</summary>
    public static double Offset => (Stroke.FocusRing / 2) + 1;

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (Brush is not { } brush)
        {
            return;
        }

        var size = AdornedElement.RenderSize;
        var bounds = new Rect(size);
        bounds.Inflate(Offset, Offset);
        var radius = Radii.Medium + Offset;
        var pen = new Pen(brush, Stroke.FocusRing) { DashStyle = Dashes, DashCap = PenLineCap.Round };
        drawingContext.DrawRoundedRectangle(null, pen, bounds, radius, radius);
    }
}
