using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// A counter of direction C (D12): a number in a pill of <see cref="IconBox.Small"/> high, <c>warn</c> fill with
/// <c>onWarn</c> text (warn means attention: repeated combinations, pending updates). It takes nothing when
/// <see cref="Count"/> is 0 or less. It goes in a reserved slot, beside what it counts and never over an icon.
/// </summary>
/// <remarks>
/// Decorative for UI Automation: the control it belongs to says the count in its item status or its name.
/// </remarks>
public sealed class Counter : FrameworkElement
{
    /// <summary>Identifies <see cref="Count"/>.</summary>
    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(
        nameof(Count),
        typeof(int),
        typeof(Counter),
        new FrameworkPropertyMetadata(
            0,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender
        )
    );

    /// <summary>Identifies <see cref="Fill"/>.</summary>
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill),
        typeof(Brush),
        typeof(Counter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>Identifies <see cref="Ink"/>.</summary>
    public static readonly DependencyProperty InkProperty = DependencyProperty.Register(
        nameof(Ink),
        typeof(Brush),
        typeof(Counter),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender
        )
    );

    private FormattedText? _text;

    /// <summary>Creates an empty counter in <c>warn</c>.</summary>
    public Counter()
    {
        SetResourceReference(FillProperty, ThemeBrushKey.For(ColorToken.Warn));
        SetResourceReference(InkProperty, ThemeBrushKey.For(ColorToken.OnWarn));
        IsHitTestVisible = false;
        Focusable = false;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Left;
    }

    /// <summary>The number shown; 0 or less hides the counter.</summary>
    public int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    /// <summary>The fill (<c>warn</c>).</summary>
    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>The color of the number (<c>onWarn</c>).</summary>
    public Brush? Ink
    {
        get => (Brush?)GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _text =
            Count > 0 && Ink is { } ink
                ? PillText.Format(
                    Count.ToString(CultureInfo.CurrentCulture),
                    ink,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip
                )
                : null;
        return PillText.Measure(_text);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        PillText.Draw(drawingContext, _text, Fill, RenderSize, Radii.Pill);
    }
}
