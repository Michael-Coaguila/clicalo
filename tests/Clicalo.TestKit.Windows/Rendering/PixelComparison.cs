using System.Globalization;

namespace Clicalo.TestKit.Windows.Rendering;

/// <summary>Result of comparing two images pixel by pixel with a per-channel tolerance.</summary>
/// <param name="SameSize">False when the dimensions differ (nothing else is compared then).</param>
/// <param name="TotalPixels">Pixels compared (those of the expected image when sizes differ).</param>
/// <param name="DifferentPixels">Pixels where some channel differs by more than the tolerance.</param>
/// <param name="MaxChannelDelta">Largest difference found in any channel (0–255).</param>
/// <param name="FirstDifference">Coordinates of the first different pixel, row by row, if any.</param>
public sealed record PixelComparison(
    bool SameSize,
    long TotalPixels,
    long DifferentPixels,
    int MaxChannelDelta,
    (int X, int Y)? FirstDifference
)
{
    /// <summary>Share of different pixels, in percent.</summary>
    public double DifferentPercent => TotalPixels == 0 ? 0 : 100.0 * DifferentPixels / TotalPixels;

    /// <summary>True when the sizes match and at most <paramref name="maxDifferentPercent"/> % of the pixels differ.</summary>
    public bool IsWithin(double maxDifferentPercent) =>
        SameSize && DifferentPercent <= maxDifferentPercent;

    /// <summary>A one-line summary for failure messages.</summary>
    public string Describe(byte channelTolerance) =>
        !SameSize
            ? "The image sizes differ."
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{DifferentPixels} of {TotalPixels} pixels ({DifferentPercent:0.###} %) differ by more than {channelTolerance} in some channel; largest channel difference {MaxChannelDelta}"
            )
                + (
                    FirstDifference is { } first
                        ? string.Create(
                            CultureInfo.InvariantCulture,
                            $"; first at ({first.X}, {first.Y})"
                        )
                        : string.Empty
                )
                + ".";
}
