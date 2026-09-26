using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The root of a control template whose drawing may be smaller than its touch target (REG-02, ACC-002): it takes
/// the whole box of the control, which <see cref="TouchTarget.Enforce"/> keeps at 44 × 44 or more, hit-tests on all
/// of it, and draws its child at <see cref="VisualWidth"/> × <see cref="VisualHeight"/>, centered.
/// </summary>
/// <remarks>
/// A template binds the visual size to the control's <c>Width</c> and <c>Height</c>: a control declared
/// 32 × 32 is laid out, hit-tested and exposed to UI Automation at 44 × 44 (its <c>MinWidth</c> and
/// <c>MinHeight</c> win) but drawn at 32 × 32. Unset (NaN), the child fills the box.
/// </remarks>
public sealed class TouchTargetBox : Decorator
{
    /// <summary>Identifies <see cref="VisualWidth"/>.</summary>
    public static readonly DependencyProperty VisualWidthProperty = DependencyProperty.Register(
        nameof(VisualWidth),
        typeof(double),
        typeof(TouchTargetBox),
        new FrameworkPropertyMetadata(
            double.NaN,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange
        )
    );

    /// <summary>Identifies <see cref="VisualHeight"/>.</summary>
    public static readonly DependencyProperty VisualHeightProperty = DependencyProperty.Register(
        nameof(VisualHeight),
        typeof(double),
        typeof(TouchTargetBox),
        new FrameworkPropertyMetadata(
            double.NaN,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange
        )
    );

    /// <summary>Width of the drawing, in device-independent pixels; NaN fills the box.</summary>
    public double VisualWidth
    {
        get => (double)GetValue(VisualWidthProperty);
        set => SetValue(VisualWidthProperty, value);
    }

    /// <summary>Height of the drawing, in device-independent pixels; NaN fills the box.</summary>
    public double VisualHeight
    {
        get => (double)GetValue(VisualHeightProperty);
        set => SetValue(VisualHeightProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        var child = Child;
        if (child is null)
        {
            return new Size(TouchTarget.MinimumSize, TouchTarget.MinimumSize);
        }

        child.Measure(
            new Size(Limit(VisualWidth, constraint.Width), Limit(VisualHeight, constraint.Height))
        );
        var desired = child.DesiredSize;
        return new Size(
            Math.Max(desired.Width, TouchTarget.MinimumSize),
            Math.Max(desired.Height, TouchTarget.MinimumSize)
        );
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (Child is { } child)
        {
            var width = Limit(VisualWidth, arrangeSize.Width);
            var height = Limit(VisualHeight, arrangeSize.Height);
            child.Arrange(
                new Rect(
                    (arrangeSize.Width - width) / 2,
                    (arrangeSize.Height - height) / 2,
                    width,
                    height
                )
            );
        }

        return arrangeSize;
    }

    /// <summary>Paints the whole box transparent so the full touch target hit-tests, not only the drawing.</summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
    }

    private static double Limit(double requested, double available) =>
        double.IsNaN(requested) ? available : Math.Min(requested, available);
}
