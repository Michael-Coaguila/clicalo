using System;
using System.Collections.Generic;

namespace Clicalo.Design.Math;

/// <summary>
/// Measures WCAG contrast on the real composite: the foreground is painted over a stack of background layers,
/// and a stack that is not opaque is itself painted over every candidate backdrop (for the floating panel,
/// whatever desktop lies behind it). The worst case is reported.
/// </summary>
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
            if (i == 0 || measurement.Ratio < worst.Ratio)
            {
                worst = measurement;
            }
        }

        return worst;
    }

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
