using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls.Internal;

/// <summary>
/// The layered chrome of every base control of direction C (D12): a fill, a rest line drawn inside (1, or 2 in high
/// contrast; dashed while disabled) and the «elegido» ring of <see cref="Stroke.Selected"/>, also inside. The control
/// never changes size between its states: the rest line and the ring are shapes inside the same box, never a border
/// that pushes the content. The corner radius comes from <see cref="RadiusProperty"/>.
/// </summary>
internal static class ControlChrome
{
    /// <summary>The fill (a <see cref="Border"/> with the control's background).</summary>
    public const string FillPart = "PART_Fill";

    /// <summary>The rest line (a <see cref="Rectangle"/> stroked with the control's border brush).</summary>
    public const string LinePart = "PART_Line";

    /// <summary>The ring of «elegido» and «pulsado» (a <see cref="Rectangle"/> in <c>accent</c>).</summary>
    public const string RingPart = "PART_Ring";

    /// <summary>The content presenter.</summary>
    public const string ContentPart = "PART_Content";

    /// <summary>Identifies the corner radius of the chrome, in device-independent pixels.</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached(
        "Radius",
        typeof(double),
        typeof(ControlChrome),
        new FrameworkPropertyMetadata(Radii.Medium)
    );

    /// <summary>The dash of the rest line of a disabled control, in units of its thickness.</summary>
    public static DoubleCollection DisabledDash { get; } =
        Frozen([Stroke.Selected, Stroke.Selected]);

    /// <summary>Reads the corner radius of <paramref name="element"/>.</summary>
    public static double GetRadius(DependencyObject element) =>
        (double)element.GetValue(RadiusProperty);

    /// <summary>Sets the corner radius of <paramref name="element"/>.</summary>
    public static void SetRadius(DependencyObject element, double value) =>
        element.SetValue(RadiusProperty, value);

    /// <summary>
    /// The layers around <paramref name="content"/>: fill, rest line, ring and the content, which keeps the control's
    /// padding. The ring is collapsed until a trigger shows it.
    /// </summary>
    public static FrameworkElementFactory Layers(FrameworkElementFactory content)
    {
        var fill = Templates
            .Element<Border>(FillPart)
            .With(Border.BackgroundProperty, Templates.Bind(Control.BackgroundProperty))
            .With(
                Border.CornerRadiusProperty,
                new TemplateBindingExtension(RadiusProperty)
                {
                    Converter = CornerConverter.Instance,
                }
            )
            .With(UIElement.SnapsToDevicePixelsProperty, true);
        var line = Templates
            .Element<Rectangle>(LinePart)
            .With(Shape.StrokeProperty, Templates.Bind(Control.BorderBrushProperty))
            .With(Rectangle.RadiusXProperty, Templates.Bind(RadiusProperty))
            .With(Rectangle.RadiusYProperty, Templates.Bind(RadiusProperty))
            .With(UIElement.SnapsToDevicePixelsProperty, true)
            .With(UIElement.IsHitTestVisibleProperty, false);
        line.SetResourceReference(Shape.StrokeThicknessProperty, ThemeKeys.LineThickness);
        var ring = Templates
            .Element<Rectangle>(RingPart)
            .With(Shape.StrokeThicknessProperty, Stroke.Selected)
            .With(Rectangle.RadiusXProperty, Templates.Bind(RadiusProperty))
            .With(Rectangle.RadiusYProperty, Templates.Bind(RadiusProperty))
            .With(UIElement.VisibilityProperty, Visibility.Collapsed)
            .With(UIElement.SnapsToDevicePixelsProperty, true)
            .With(UIElement.IsHitTestVisibleProperty, false)
            .Paint(Shape.StrokeProperty, ColorToken.Accent);
        content
            .With(FrameworkElement.MarginProperty, Templates.Bind(Control.PaddingProperty))
            .With(
                FrameworkElement.HorizontalAlignmentProperty,
                Templates.Bind(Control.HorizontalContentAlignmentProperty)
            )
            .With(
                FrameworkElement.VerticalAlignmentProperty,
                Templates.Bind(Control.VerticalContentAlignmentProperty)
            );
        return Templates.Element<Grid>().Add(fill, line, ring, content);
    }

    /// <summary>The setters of «elegido» and «pulsado»: wash, ring of 2 inside and accent-ink icons (filled by the control).</summary>
    public static SetterBase[] Selected(params SetterBase[] extra) =>
        [
            Templates.Brush(Border.BackgroundProperty, ColorToken.AccentWash, FillPart),
            new Setter(UIElement.VisibilityProperty, Visibility.Visible, RingPart),
            new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, LinePart),
            .. extra,
        ];

    /// <summary>The setters of a disabled control: no fill, a dashed rest line and muted text.</summary>
    public static SetterBase[] Disabled() =>
        [
            new Setter(Border.BackgroundProperty, Brushes.Transparent, FillPart),
            new Setter(UIElement.VisibilityProperty, Visibility.Visible, LinePart),
            Templates.Brush(Shape.StrokeProperty, ColorToken.Line, LinePart),
            new Setter(Shape.StrokeDashArrayProperty, DisabledDash, LinePart),
            new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, RingPart),
            Templates.Brush(TextElement.ForegroundProperty, ColorToken.Muted, ContentPart),
        ];

    private static DoubleCollection Frozen(double[] values)
    {
        var collection = new DoubleCollection(values);
        collection.Freeze();
        return collection;
    }

    /// <summary>Converts a radius in device-independent pixels into a uniform <see cref="CornerRadius"/>.</summary>
    private sealed class CornerConverter : IValueConverter
    {
        public static CornerConverter Instance { get; } = new();

        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) => new CornerRadius(value is double radius ? radius : 0);

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        ) => throw new NotSupportedException();
    }
}
