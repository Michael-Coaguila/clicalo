using System.Windows;
using System.Windows.Media;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// A badge of direction C (D12): a short word on a fill of <see cref="IconBox.Small"/> high with radius
/// <see cref="Radii.Small"/>, in the <c>labelSmall</c> style; its <see cref="Tone"/> keeps the one meaning of its
/// color. It takes nothing while <see cref="Text"/> is empty. It goes in a reserved slot and never over an icon.
/// </summary>
/// <remarks>
/// Decorative for UI Automation: the control it belongs to says the same in its item status or its name.
/// </remarks>
public sealed class Badge : FrameworkElement
{
    /// <summary>Identifies <see cref="Text"/>.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(Badge),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender
        )
    );

    /// <summary>Identifies <see cref="Tone"/>.</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone),
        typeof(BadgeTone),
        typeof(Badge),
        new FrameworkPropertyMetadata(BadgeTone.Neutral, OnToneChanged)
    );

    private static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        "Fill",
        typeof(Brush),
        typeof(Badge),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    private static readonly DependencyProperty InkProperty = DependencyProperty.Register(
        "Ink",
        typeof(Brush),
        typeof(Badge),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender
        )
    );

    private FormattedText? _text;

    /// <summary>Creates an empty neutral badge.</summary>
    public Badge()
    {
        Paint(this, BadgeTone.Neutral);
        IsHitTestVisible = false;
        Focusable = false;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Left;
    }

    /// <summary>The word shown (already localized); empty hides the badge.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The fill and its meaning (<see cref="BadgeTone.Neutral"/> by default).</summary>
    public BadgeTone Tone
    {
        get => (BadgeTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _text = GetValue(InkProperty) is Brush ink
            ? PillText.Format(Text, ink, VisualTreeHelper.GetDpi(this).PixelsPerDip)
            : null;
        return PillText.Measure(_text);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        PillText.Draw(
            drawingContext,
            _text,
            GetValue(FillProperty) as Brush,
            RenderSize,
            Radii.Small
        );
    }

    private static void OnToneChanged(DependencyObject badge, DependencyPropertyChangedEventArgs e) =>
        Paint((Badge)badge, (BadgeTone)e.NewValue);

    private static void Paint(Badge badge, BadgeTone tone)
    {
        var (fill, ink) = tone switch
        {
            BadgeTone.Accent => (ColorToken.Accent, ColorToken.OnAccent),
            BadgeTone.Warn => (ColorToken.Warn, ColorToken.OnWarn),
            _ => (ColorToken.Card, ColorToken.Muted),
        };
        badge.SetResourceReference(FillProperty, ThemeBrushKey.For(fill));
        badge.SetResourceReference(InkProperty, ThemeBrushKey.For(ink));
    }
}
