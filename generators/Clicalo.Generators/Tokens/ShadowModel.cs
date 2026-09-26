namespace Clicalo.Generators.Tokens;

/// <summary>A precomputed drop shadow (never <c>DropShadowEffect</c>, blueprint §8.1): geometry and opacity.</summary>
internal sealed class ShadowModel(
    string key,
    double offsetX,
    double offsetY,
    double blur,
    double opacity
)
{
    public string Key { get; } = key;

    public double OffsetX { get; } = offsetX;

    public double OffsetY { get; } = offsetY;

    public double Blur { get; } = blur;

    /// <summary>Multiplies the alpha of the theme's <c>shadow</c> color.</summary>
    public double Opacity { get; } = opacity;
}
