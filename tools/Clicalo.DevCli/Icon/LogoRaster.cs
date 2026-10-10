using Clicalo.Design.Math;

namespace Clicalo.DevCli.Icon;

/// <summary>
/// Draws the square logo of Clícalo (TEM-008) at any size without a graphics library: the accent square with rounded
/// corners and the three strokes of the «í», exactly the shapes <c>BrandMark.Mark</c> draws in «Acerca de» and in the
/// welcome, on its 48 × 48 grid. Every pixel is the average of 8 × 8 samples, so the edges are smooth at 16 pixels too.
/// </summary>
/// <remarks>
/// Deterministic on every machine: only additions, multiplications and comparisons of doubles, with the sine and the
/// cosine of the two angles written as constants (a math library may round them differently).
/// </remarks>
internal static class LogoRaster
{
    /// <summary>The side of the grid the logo is designed on.</summary>
    public const double Grid = 48;

    private const double CornerRadius = 13;
    private const int Samples = 8;

    /// <summary>The strokes of <c>BrandMark.Mark</c>: left, top, width, height, cosine, sine and opacity.</summary>
    private static readonly Stroke[] Strokes =
    [
        new(20, 20, 8, 19, 1, 0, 1),
        // 32 degrees.
        new(23, 6, 7, 12, 0.84804809615642596, 0.52991926423320495, 1),
        // 72 degrees.
        new(33, 8, 4, 6, 0.30901699437494742, 0.95105651629515357, 0.75),
    ];

    /// <summary>The logo at <paramref name="size"/> pixels.</summary>
    /// <param name="size">The side of the image.</param>
    /// <param name="fill">The color of the square: the accent.</param>
    /// <param name="ink">The color of the strokes: what is written over the accent.</param>
    /// <param name="opacity">The opacity of the whole image, 0 to 1 (BUR-003: 0.55 while the panel is hidden).</param>
    public static IcoImage Render(int size, Rgba8 fill, Rgba8 ink, double opacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        var pixels = new byte[size * size * 4];
        var unit = Grid / size;
        const double Weight = 1d / (Samples * Samples);
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                double red = 0;
                double green = 0;
                double blue = 0;
                double alpha = 0;
                for (var sy = 0; sy < Samples; sy++)
                {
                    var y = (row + ((sy + 0.5) / Samples)) * unit;
                    for (var sx = 0; sx < Samples; sx++)
                    {
                        var x = (column + ((sx + 0.5) / Samples)) * unit;
                        var (r, g, b, a) = Sample(x, y, fill, ink);
                        red += r;
                        green += g;
                        blue += b;
                        alpha += a;
                    }
                }

                alpha *= Weight;
                var offset = ((row * size) + column) * 4;
                if (alpha <= 0)
                {
                    continue;
                }

                // The sums are premultiplied; an icon stores straight alpha.
                pixels[offset] = Channel(blue * Weight / alpha);
                pixels[offset + 1] = Channel(green * Weight / alpha);
                pixels[offset + 2] = Channel(red * Weight / alpha);
                pixels[offset + 3] = Channel(alpha * opacity * 255);
            }
        }

        return new IcoImage(size, pixels);
    }

    /// <summary>The premultiplied color of one point of the grid, with channels from 0 to 255 and alpha from 0 to 1.</summary>
    private static (double R, double G, double B, double A) Sample(
        double x,
        double y,
        Rgba8 fill,
        Rgba8 ink
    )
    {
        double red = 0;
        double green = 0;
        double blue = 0;
        double alpha = 0;
        if (InRounded(x - (Grid / 2), y - (Grid / 2), Grid / 2, Grid / 2, CornerRadius))
        {
            red = fill.R;
            green = fill.G;
            blue = fill.B;
            alpha = 1;
        }

        foreach (var stroke in Strokes)
        {
            var dx = x - (stroke.Left + (stroke.Width / 2));
            var dy = y - (stroke.Top + (stroke.Height / 2));

            // The stroke is turned clockwise around its center: the point is turned back.
            var localX = (dx * stroke.Cos) + (dy * stroke.Sin);
            var localY = (dy * stroke.Cos) - (dx * stroke.Sin);
            if (!InRounded(localX, localY, stroke.Width / 2, stroke.Height / 2, stroke.Width / 2))
            {
                continue;
            }

            var over = stroke.Opacity;
            red = (ink.R * over) + (red * (1 - over));
            green = (ink.G * over) + (green * (1 - over));
            blue = (ink.B * over) + (blue * (1 - over));
            alpha = over + (alpha * (1 - over));
        }

        return (red, green, blue, alpha);
    }

    /// <summary>Whether a point, measured from the center, is inside a rectangle with rounded corners.</summary>
    private static bool InRounded(
        double x,
        double y,
        double halfWidth,
        double halfHeight,
        double radius
    )
    {
        var ax = x < 0 ? -x : x;
        var ay = y < 0 ? -y : y;
        if (ax > halfWidth || ay > halfHeight)
        {
            return false;
        }

        var cx = ax - (halfWidth - radius);
        var cy = ay - (halfHeight - radius);
        return cx <= 0 || cy <= 0 || (cx * cx) + (cy * cy) <= radius * radius;
    }

    private static byte Channel(double value) =>
        value <= 0 ? (byte)0
        : value >= 255 ? (byte)255
        : (byte)(value + 0.5);

    private sealed record Stroke(
        double Left,
        double Top,
        double Width,
        double Height,
        double Cos,
        double Sin,
        double Opacity
    );
}
