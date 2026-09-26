using System;
using System.Collections.Generic;

namespace Clicalo.Design.Math;

/// <summary>
/// Measures WCAG contrast on the real composite: the foreground is painted over a stack of background layers,
/// and a stack that is not opaque is itself painted over every candidate backdrop (for the floating panel,
/// whatever desktop lies behind it). The worst case is reported.
/// </summary>
/// <remarks>
/// When the foreground is lighter than the background over one backdrop and darker over another, some desktop
/// between the two (every color on the segment joining them is a possible desktop, and compositing is
/// continuous) paints both at the same luminance: the worst case is then 1:1, found by bisection on that segment.
/// </remarks>
public static class ContrastEvaluator
{
    /// <param name="foreground">The text or graphic color, possibly translucent.</param>
    /// <param name="layersTopToBottom">Background layers, the one directly under the foreground first.</param>
    /// <param name="backdrops">Opaque colors that may show through a translucent stack.</param>
    /// <exception cref="ArgumentException">
    /// The stack is empty or translucent and no backdrop is given, so the background is undefined.
    /// </exception>
    public static ContrastMeasurement Measure(
        Rgba8 foreground,
        IReadOnlyList<Rgba8> layersTopToBottom,
        IReadOnlyList<Rgba8> backdrops
    )
    {
        if (layersTopToBottom is null)
        {
            throw new ArgumentNullException(nameof(layersTopToBottom));
        }

        if (backdrops is null)
        {
            throw new ArgumentNullException(nameof(backdrops));
        }

        var opaqueIndex = IndexOfFirstOpaque(layersTopToBottom);
        if (opaqueIndex >= 0)
        {
            return MeasureOver(
                foreground,
                layersTopToBottom,
                opaqueIndex,
                layersTopToBottom[opaqueIndex].ToSrgb(),
                -1
            );
        }

        if (backdrops.Count == 0)
        {
            throw new ArgumentException(
                "A translucent background needs at least one opaque backdrop to be measured.",
                nameof(backdrops)
            );
        }

        var worst = default(ContrastMeasurement);
        var firstIsLighter = false;
        for (var i = 0; i < backdrops.Count; i++)
        {
            var backdrop = backdrops[i].ToSrgb();
            if (backdrop.Alpha < 1d)
            {
                throw new ArgumentException("Backdrops must be opaque.", nameof(backdrops));
            }

            var measurement = MeasureOver(
                foreground,
                layersTopToBottom,
                layersTopToBottom.Count,
                backdrop,
                i
            );
            if (i == 0)
            {
                firstIsLighter = IsForegroundLighter(measurement);
            }
            else if (IsForegroundLighter(measurement) != firstIsLighter)
            {
                return Crossing(foreground, layersTopToBottom, backdrops[0].ToSrgb(), backdrop);
            }

            if (i == 0 || measurement.Ratio < worst.Ratio)
            {
                worst = measurement;
            }
        }

        return worst;
    }

    private static bool IsForegroundLighter(ContrastMeasurement measurement) =>
        Wcag.RelativeLuminance(measurement.Foreground)
        >= Wcag.RelativeLuminance(measurement.Background);

    /// <summary>
    /// The foreground is lighter than the background over <paramref name="from"/> and darker over
    /// <paramref name="to"/> (or the reverse): bisects the segment between both desktops to the one where the
    /// two luminances meet.
    /// </summary>
    private static ContrastMeasurement Crossing(
        Rgba8 foreground,
        IReadOnlyList<Rgba8> layers,
        Srgb from,
        Srgb to
    )
    {
        const int Iterations = 60;
        var lighterAtLow = IsForegroundLighter(
            MeasureOver(foreground, layers, layers.Count, from, -1)
        );
        var low = 0d;
        var high = 1d;
        for (var i = 0; i < Iterations; i++)
        {
            var middle = (low + high) / 2d;
            var measurement = MeasureOver(
                foreground,
                layers,
                layers.Count,
                Mix(from, to, middle),
                -1
            );
            if (IsForegroundLighter(measurement) == lighterAtLow)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return MeasureOver(foreground, layers, layers.Count, Mix(from, to, low), -1);
    }

    private static Srgb Mix(Srgb from, Srgb to, double t) =>
        new(
            from.R + ((to.R - from.R) * t),
            from.G + ((to.G - from.G) * t),
            from.B + ((to.B - from.B) * t)
        );

    private static int IndexOfFirstOpaque(IReadOnlyList<Rgba8> layers)
    {
        for (var i = 0; i < layers.Count; i++)
        {
            if (layers[i].IsOpaque)
            {
                return i;
            }
        }

        return -1;
    }

    private static ContrastMeasurement MeasureOver(
        Rgba8 foreground,
        IReadOnlyList<Rgba8> layers,
        int bottomExclusive,
        Srgb bottom,
        int backdropIndex
    )
    {
        var background = bottom;
        for (var i = bottomExclusive - 1; i >= 0; i--)
        {
            background = Compositing.Over(layers[i], background);
        }

        var painted = Compositing.Over(foreground, background);
        return new ContrastMeasurement(
            Wcag.ContrastRatio(painted, background),
            painted,
            background,
            backdropIndex
        );
    }
}
