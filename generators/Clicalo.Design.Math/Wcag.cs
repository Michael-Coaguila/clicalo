namespace Clicalo.Design.Math;

/// <summary>WCAG 2.x relative luminance and contrast ratio.</summary>
/// <seealso href="https://www.w3.org/TR/WCAG22/#dfn-relative-luminance"/>
public static class Wcag
{
    /// <summary>Success criterion 1.4.3 (AA): normal-size text and images of text.</summary>
    public const double MinimumTextContrast = 4.5;

    /// <summary>Success criterion 1.4.11 (AA): user-interface components and meaningful graphics.</summary>
    public const double MinimumGraphicContrast = 3d;

    /// <summary>Relative luminance (0 black – 1 white) of an opaque color; alpha is ignored, composite first.</summary>
    public static double RelativeLuminance(Srgb color)
    {
        var linear = color.ToLinear();
        return (0.2126 * linear.R) + (0.7152 * linear.G) + (0.0722 * linear.B);
    }

    /// <inheritdoc cref="RelativeLuminance(Srgb)"/>
    public static double RelativeLuminance(Rgba8 color) => RelativeLuminance(color.ToSrgb());

    /// <summary>(L1 + 0.05) / (L2 + 0.05) with L1 the lighter of the two; ranges from 1 to 21.</summary>
    public static double ContrastRatio(double luminance, double otherLuminance)
    {
        var lighter = System.Math.Max(luminance, otherLuminance);
        var darker = System.Math.Min(luminance, otherLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>Contrast ratio between two opaque colors.</summary>
    public static double ContrastRatio(Srgb color, Srgb other) =>
        ContrastRatio(RelativeLuminance(color), RelativeLuminance(other));

    /// <inheritdoc cref="ContrastRatio(Srgb, Srgb)"/>
    public static double ContrastRatio(Rgba8 color, Rgba8 other) =>
        ContrastRatio(color.ToSrgb(), other.ToSrgb());
}
