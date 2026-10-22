using System.Windows;
using System.Windows.Media;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// The shadow of a <see cref="SurfaceLook"/> drawn the plain way, every pixel of the whole image, as
/// <see cref="ShadowRaster"/> drew it before it learned to draw only what differs: the oracle its faster drawing is
/// compared with. Premultiplied BGRA, the margins of the <see cref="ShadowImage"/> around the surface.
/// </summary>
internal static class ReferenceShadow
{
    private const int BoxPasses = 3;

    public static byte[] Pixels(int width, int height, double scale, SurfaceLook look, Color color)
    {
        var shadow =
            look.Shadow ?? throw new ArgumentException("The look has no shadow.", nameof(look));
        var blur = (int)Math.Ceiling(shadow.BlurRadius * scale);
        var dx = (int)Math.Round(shadow.OffsetX * scale, MidpointRounding.AwayFromZero);
        var dy = (int)Math.Round(shadow.OffsetY * scale, MidpointRounding.AwayFromZero);
        var left = Math.Max(0, blur - dx);
        var right = Math.Max(0, blur + dx);
        var top = Math.Max(0, blur - dy);
        var bottom = Math.Max(0, blur + dy);
        var w = width + left + right;
        var h = height + top + bottom;
        var corners = ShadowRaster.Corners(look, width, height, scale);

        var alpha = new float[w * h];
        var shape = new Int32Rect(left + dx, top + dy, width, height);
        for (var y = Math.Max(0, shape.Y - 1); y < Math.Min(h, shape.Y + shape.Height + 1); y++)
        {
            for (var x = Math.Max(0, shape.X - 1); x < Math.Min(w, shape.X + shape.Width + 1); x++)
            {
                alpha[(y * w) + x] = (float)Coverage(
                    x + 0.5 - shape.X,
                    y + 0.5 - shape.Y,
                    shape.Width,
                    shape.Height,
                    corners
                );
            }
        }

        var sigma = blur / 2.0;
        if (sigma >= 0.5)
        {
            var scratch = new float[alpha.Length];
            foreach (var size in BoxSizes(sigma))
            {
                var radius = (size - 1) / 2;
                BoxBlur(alpha, scratch, w, h, radius, horizontal: true);
                BoxBlur(scratch, alpha, w, h, radius, horizontal: false);
            }
        }

        var strength = color.A / 255.0;
        var pixels = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w) + x;
                var inside = Coverage(x + 0.5 - left, y + 0.5 - top, width, height, corners);
                var a = Math.Clamp(alpha[i] * strength * (1 - inside), 0, 1);
                pixels[(i * 4) + 0] = ToByte(color.B * a);
                pixels[(i * 4) + 1] = ToByte(color.G * a);
                pixels[(i * 4) + 2] = ToByte(color.R * a);
                pixels[(i * 4) + 3] = ToByte(255 * a);
            }
        }

        return pixels;
    }

    private static byte ToByte(double value) =>
        (byte)Math.Round(value, MidpointRounding.AwayFromZero);

    private static double Coverage(double x, double y, double width, double height, CornerRadius r)
    {
        if (x < -1 || y < -1 || x > width + 1 || y > height + 1)
        {
            return 0;
        }

        var leftHalf = x < width / 2;
        var topHalf = y < height / 2;
        var radius = (leftHalf, topHalf) switch
        {
            (true, true) => r.TopLeft,
            (false, true) => r.TopRight,
            (false, false) => r.BottomRight,
            _ => r.BottomLeft,
        };
        var inX = leftHalf ? x : width - x;
        var inY = topHalf ? y : height - y;
        var qx = radius - inX;
        var qy = radius - inY;
        var distance =
            qx > 0 && qy > 0 ? Math.Sqrt((qx * qx) + (qy * qy)) - radius : -Math.Min(inX, inY);
        return Math.Clamp(0.5 - distance, 0, 1);
    }

    private static int[] BoxSizes(double sigma)
    {
        var ideal = Math.Sqrt((12 * sigma * sigma / BoxPasses) + 1);
        var lower = (int)Math.Floor(ideal);
        if (lower % 2 == 0)
        {
            lower--;
        }

        var upper = lower + 2;
        var count = (int)
            Math.Round(
                (
                    (12 * sigma * sigma)
                    - (BoxPasses * lower * lower)
                    - (4 * BoxPasses * lower)
                    - (3 * BoxPasses)
                ) / ((-4 * lower) - 4),
                MidpointRounding.AwayFromZero
            );
        var sizes = new int[BoxPasses];
        for (var i = 0; i < BoxPasses; i++)
        {
            sizes[i] = i < count ? lower : upper;
        }

        return sizes;
    }

    private static void BoxBlur(
        float[] source,
        float[] target,
        int width,
        int height,
        int radius,
        bool horizontal
    )
    {
        var lines = horizontal ? height : width;
        var length = horizontal ? width : height;
        var step = horizontal ? 1 : width;
        var scale = 1f / ((2 * radius) + 1);
        for (var line = 0; line < lines; line++)
        {
            var start = horizontal ? line * width : line;
            var sum = 0f;
            for (var i = 0; i <= Math.Min(radius, length - 1); i++)
            {
                sum += source[start + (i * step)];
            }

            for (var i = 0; i < length; i++)
            {
                target[start + (i * step)] = sum * scale;
                var add = i + radius + 1;
                var remove = i - radius;
                if (add < length)
                {
                    sum += source[start + (add * step)];
                }

                if (remove >= 0)
                {
                    sum -= source[start + (remove * step)];
                }
            }
        }
    }
}
