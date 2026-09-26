namespace Clicalo.Design.Math;

/// <summary>
/// Porter–Duff source-over on gamma-encoded sRGB with straight alpha: the blending WPF (and browsers)
/// apply by default, so a composite computed here matches what is painted on screen.
/// </summary>
public static class Compositing
{
    /// <summary>Paints <paramref name="source"/> over <paramref name="backdrop"/>.</summary>
    public static Srgb Over(Srgb source, Srgb backdrop)
    {
        var alpha = source.Alpha + (backdrop.Alpha * (1d - source.Alpha));
        if (alpha <= 0d)
        {
            return new Srgb(0d, 0d, 0d, 0d);
        }

        var backdropWeight = backdrop.Alpha * (1d - source.Alpha);
        return new Srgb(
            ((source.R * source.Alpha) + (backdrop.R * backdropWeight)) / alpha,
            ((source.G * source.Alpha) + (backdrop.G * backdropWeight)) / alpha,
            ((source.B * source.Alpha) + (backdrop.B * backdropWeight)) / alpha,
            alpha
        );
    }

    /// <inheritdoc cref="Over(Srgb, Srgb)"/>
    public static Srgb Over(Rgba8 source, Srgb backdrop) => Over(source.ToSrgb(), backdrop);
}
