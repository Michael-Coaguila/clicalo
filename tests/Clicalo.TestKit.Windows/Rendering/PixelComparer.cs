namespace Clicalo.TestKit.Windows.Rendering;

/// <summary>Pixel-by-pixel comparison with a per-channel tolerance, and the diff image shown on failure.</summary>
public static class PixelComparer
{
    /// <summary>
    /// Compares <paramref name="actual"/> with <paramref name="expected"/>: a pixel differs when any of B, G, R or A
    /// differs by more than <paramref name="channelTolerance"/>. Two fully transparent pixels are equal whatever
    /// their color channels hold.
    /// </summary>
    public static PixelComparison Compare(
        PixelBuffer expected,
        PixelBuffer actual,
        byte channelTolerance
    )
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        var total = (long)expected.Width * expected.Height;
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return new PixelComparison(
                SameSize: false,
                total,
                DifferentPixels: total,
                MaxChannelDelta: 255,
                FirstDifference: null
            );
        }

        var a = expected.Bgra;
        var b = actual.Bgra;
        long different = 0;
        var maxDelta = 0;
        (int X, int Y)? first = null;
        for (var offset = 0; offset < a.Length; offset += PixelBuffer.BytesPerPixel)
        {
            var delta = PixelDelta(a, b, offset);
            maxDelta = Math.Max(maxDelta, delta);
            if (delta > channelTolerance)
            {
                different++;
                if (first is null)
                {
                    var index = offset / PixelBuffer.BytesPerPixel;
                    first = (index % expected.Width, index / expected.Width);
                }
            }
        }

        return new PixelComparison(SameSize: true, total, different, maxDelta, first);
    }

    /// <summary>
    /// An image the size of the larger input: different pixels (and pixels outside either image) in opaque magenta,
    /// the rest as a faded grayscale copy of <paramref name="expected"/>, so the differences stand out.
    /// </summary>
    public static PixelBuffer CreateDiffImage(
        PixelBuffer expected,
        PixelBuffer actual,
        byte channelTolerance
    )
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        var width = Math.Max(expected.Width, actual.Width);
        var height = Math.Max(expected.Height, actual.Height);
        var diff = new PixelBuffer(
            width,
            height,
            new byte[width * height * PixelBuffer.BytesPerPixel]
        );
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var inBoth =
                    x < expected.Width
                    && y < expected.Height
                    && x < actual.Width
                    && y < actual.Height;
                if (
                    !inBoth
                    || PixelDelta(
                        expected.Bgra,
                        expected.Offset(x, y),
                        actual.Bgra,
                        actual.Offset(x, y)
                    ) > channelTolerance
                )
                {
                    diff.SetPixel(x, y, 255, 0, 255, 255);
                    continue;
                }

                var offset = expected.Offset(x, y);
                var e = expected.Bgra;
                var alpha = e[offset + 3] / 255.0;
                var luminance =
                    ((0.0722 * e[offset]) + (0.7152 * e[offset + 1]) + (0.2126 * e[offset + 2]))
                        * alpha
                    + (255 * (1 - alpha));
                var faded = (byte)Math.Round(255 - ((255 - luminance) * 0.35));
                diff.SetPixel(x, y, faded, faded, faded, 255);
            }
        }

        return diff;
    }

    private static int PixelDelta(byte[] a, byte[] b, int offset) =>
        PixelDelta(a, offset, b, offset);

    private static int PixelDelta(byte[] a, int offsetA, byte[] b, int offsetB)
    {
        if (a[offsetA + 3] == 0 && b[offsetB + 3] == 0)
        {
            return 0;
        }

        var delta = 0;
        for (var channel = 0; channel < PixelBuffer.BytesPerPixel; channel++)
        {
            delta = Math.Max(delta, Math.Abs(a[offsetA + channel] - b[offsetB + channel]));
        }

        return delta;
    }
}
