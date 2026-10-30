using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// Draws the shadow of a <see cref="SurfaceLook"/> like a CSS <c>box-shadow</c> (blueprint §8.1: precomputed, never
/// <c>DropShadowEffect</c>): the rounded shape moved by the offset, blurred with a gaussian of σ = blur / 2 (three box
/// blurs) and cut out where the surface is, so a translucent surface never shows its own shadow through it.
/// </summary>
/// <remarks>
/// It runs on the UI thread each time a surface changes size, so it only computes what differs: along a straight side,
/// once the corners and the reach of the blur are behind, every column (or row) of the shadow is the same. A long
/// surface gets the shadow of a short one with the same corners, widened by repeating its middle column and row, and
/// the columns are blurred a row at a time, in the order the pixels are in memory.
/// </remarks>
public static class ShadowRaster
{
    private const int BoxPasses = 3;

    /// <summary>The shadow of a surface of <paramref name="width"/> × <paramref name="height"/> physical pixels.</summary>
    /// <param name="width">Physical width of the surface.</param>
    /// <param name="height">Physical height of the surface.</param>
    /// <param name="scale">Physical pixels per device-independent pixel (DPI / 96).</param>
    /// <param name="look">Shape and shadow; its shadow must not be null.</param>
    /// <param name="color">The shadow color with its final alpha (the theme's <c>shadow</c> × the elevation opacity).</param>
    public static ShadowImage Render(
        int width,
        int height,
        double scale,
        SurfaceLook look,
        Color color
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
        ArgumentNullException.ThrowIfNull(look);
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
        var corners = Corners(look, width, height, scale);
        var boxes = blur / 2.0 < 0.5 ? [] : BoxSizes(blur / 2.0);

        // Past this many pixels from both ends of a side, nothing of either end reaches: the corner, the blur, the
        // offset and the pixel of antialiasing on each side.
        var reach =
            (int)
                Math.Ceiling(
                    Math.Max(
                        Math.Max(corners.TopLeft, corners.TopRight),
                        Math.Max(corners.BottomLeft, corners.BottomRight)
                    )
                )
            + boxes.Sum(static size => (size - 1) / 2)
            + Math.Max(Math.Abs(dx), Math.Abs(dy))
            + 2;
        var shortWidth = Math.Min(width, (2 * reach) + 1);
        var shortHeight = Math.Min(height, (2 * reach) + 1);
        var shape = new ShadowShape(shortWidth, shortHeight, left, top, dx, dy, corners);
        var pixels = Pixels(shape, right, bottom, boxes, color);
        if (shortWidth != width || shortHeight != height)
        {
            pixels = Widen(
                pixels,
                shortWidth + left + right,
                shortHeight + top + bottom,
                w,
                h,
                left + (shortWidth / 2),
                top + (shortHeight / 2)
            );
        }

        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixels, w * 4);
        bitmap.Freeze();
        return new ShadowImage(bitmap, left, top, right, bottom);
    }

    /// <summary>The corner radii of <paramref name="look"/> in physical pixels, never more than half a side.</summary>
    /// <param name="look">The shape.</param>
    /// <param name="width">Physical width.</param>
    /// <param name="height">Physical height.</param>
    /// <param name="scale">Physical pixels per device-independent pixel.</param>
    public static CornerRadius Corners(SurfaceLook look, double width, double height, double scale)
    {
        ArgumentNullException.ThrowIfNull(look);
        var max = Math.Max(0, Math.Min(width, height) / 2);
        if (look.Round)
        {
            return new CornerRadius(max);
        }

        var c = look.Corners;
        return new CornerRadius(
            Math.Min(c.TopLeft * scale, max),
            Math.Min(c.TopRight * scale, max),
            Math.Min(c.BottomRight * scale, max),
            Math.Min(c.BottomLeft * scale, max)
        );
    }

    private static byte ToByte(double value) =>
        (byte)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>The premultiplied pixels of the shadow of <paramref name="shape"/>, with its margins.</summary>
    private static byte[] Pixels(ShadowShape shape, int right, int bottom, int[] boxes, Color color)
    {
        var w = shape.Width + shape.Left + right;
        var h = shape.Height + shape.Top + bottom;
        var alpha = new float[w * h];
        Fill(
            alpha,
            w,
            h,
            new Int32Rect(shape.Left + shape.Dx, shape.Top + shape.Dy, shape.Width, shape.Height),
            shape.Corners
        );
        Blur(alpha, w, h, boxes);

        var strength = color.A / 255.0;
        var pixels = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w) + x;
                var inside = Coverage(
                    x + 0.5 - shape.Left,
                    y + 0.5 - shape.Top,
                    shape.Width,
                    shape.Height,
                    shape.Corners
                );
                var a = Math.Clamp(alpha[i] * strength * (1 - inside), 0, 1);
                pixels[(i * 4) + 0] = ToByte(color.B * a);
                pixels[(i * 4) + 1] = ToByte(color.G * a);
                pixels[(i * 4) + 2] = ToByte(color.R * a);
                pixels[(i * 4) + 3] = ToByte(255 * a);
            }
        }

        return pixels;
    }

    /// <summary>
    /// The shadow of the short shape (<paramref name="width"/> × <paramref name="height"/>) widened to
    /// <paramref name="targetWidth"/> × <paramref name="targetHeight"/>: everything up to its middle column
    /// <paramref name="midX"/> and row <paramref name="midY"/> stays, the far end moves out, and the middle repeats.
    /// </summary>
    private static byte[] Widen(
        byte[] pixels,
        int width,
        int height,
        int targetWidth,
        int targetHeight,
        int midX,
        int midY
    )
    {
        ReadOnlySpan<uint> source = MemoryMarshal.Cast<byte, uint>(pixels.AsSpan());
        var widened = new byte[targetWidth * targetHeight * 4];
        var target = MemoryMarshal.Cast<byte, uint>(widened.AsSpan());
        var columns = new int[targetWidth];
        for (var x = 0; x < targetWidth; x++)
        {
            columns[x] = Stretch(x, targetWidth, width, midX);
        }

        for (var y = 0; y < targetHeight; y++)
        {
            var from = Stretch(y, targetHeight, height, midY) * width;
            var to = y * targetWidth;
            for (var x = 0; x < targetWidth; x++)
            {
                target[to + x] = source[from + columns[x]];
            }
        }

        return widened;
    }

    /// <summary>Where pixel <paramref name="at"/> of a line <paramref name="length"/> long comes from in one <paramref name="shortLength"/> long.</summary>
    private static int Stretch(int at, int length, int shortLength, int middle) =>
        at <= middle ? at
        : at >= length - (shortLength - middle) ? at - (length - shortLength)
        : middle;

    /// <summary>
    /// How much of the pixel centered at (<paramref name="x"/>, <paramref name="y"/>) the rounded rectangle at the
    /// origin covers, 0 to 1, antialiased over one pixel.
    /// </summary>
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

    private static void Fill(float[] alpha, int w, int h, Int32Rect shape, CornerRadius corners)
    {
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
    }

    /// <summary>A gaussian blur as the three box blurs of <see cref="BoxSizes"/>, rows then columns.</summary>
    private static void Blur(float[] alpha, int w, int h, int[] boxes)
    {
        if (boxes.Length == 0)
        {
            return;
        }

        var scratch = new float[alpha.Length];
        var sums = new float[w];
        foreach (var size in boxes)
        {
            var radius = (size - 1) / 2;
            BoxBlur(alpha, scratch, new BoxLine(w, h, radius, Horizontal: true));
            BoxBlurColumns(scratch, alpha, new BoxLine(w, h, radius, Horizontal: false), sums);
        }
    }

    /// <summary>
    /// The box blur of every column at once, a row at a time: the running sum of each column, in the order the pixels
    /// are in memory, with the same additions as <see cref="BoxBlur"/> makes down one column.
    /// </summary>
    private static void BoxBlurColumns(float[] source, float[] target, BoxLine box, float[] sums)
    {
        var w = box.Width;
        var h = box.Height;
        var scale = 1f / ((2 * box.Radius) + 1);
        Array.Clear(sums);
        for (var y = 0; y <= Math.Min(box.Radius, h - 1); y++)
        {
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                sums[x] += source[row + x];
            }
        }

        for (var y = 0; y < h; y++)
        {
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                target[row + x] = sums[x] * scale;
            }

            var add = y + box.Radius + 1;
            if (add < h)
            {
                var added = add * w;
                for (var x = 0; x < w; x++)
                {
                    sums[x] += source[added + x];
                }
            }

            var remove = y - box.Radius;
            if (remove >= 0)
            {
                var removed = remove * w;
                for (var x = 0; x < w; x++)
                {
                    sums[x] -= source[removed + x];
                }
            }
        }
    }

    /// <summary>Box widths whose three passes approximate a gaussian of σ <paramref name="sigma"/>.</summary>
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

    /// <summary>The box blur of every row.</summary>
    private static void BoxBlur(float[] source, float[] target, BoxLine box)
    {
        var lines = box.Horizontal ? box.Height : box.Width;
        var length = box.Horizontal ? box.Width : box.Height;
        var step = box.Horizontal ? 1 : box.Width;
        var scale = 1f / ((2 * box.Radius) + 1);
        for (var line = 0; line < lines; line++)
        {
            var start = box.Horizontal ? line * box.Width : line;
            var sum = 0f;
            for (var i = 0; i <= Math.Min(box.Radius, length - 1); i++)
            {
                sum += source[start + (i * step)];
            }

            for (var i = 0; i < length; i++)
            {
                target[start + (i * step)] = sum * scale;
                var add = i + box.Radius + 1;
                var remove = i - box.Radius;
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

    private readonly record struct BoxLine(int Width, int Height, int Radius, bool Horizontal);

    /// <summary>The surface whose shadow is drawn, at (<see cref="Left"/>, <see cref="Top"/>) in the image.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ShadowShape(
        int Width,
        int Height,
        int Left,
        int Top,
        int Dx,
        int Dy,
        CornerRadius Corners
    );
}
