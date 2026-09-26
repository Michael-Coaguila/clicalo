using System.Windows.Media;

namespace Clicalo.TestKit.Windows.Rendering;

/// <summary>How a visual is rendered and how strictly it is compared with its golden PNG.</summary>
public sealed record RenderSnapshotOptions
{
    /// <summary>96 DPI (100 % scale), transparent background, tolerance 2 per channel, at most 0.5 % different pixels.</summary>
    public static RenderSnapshotOptions Default { get; } = new();

    /// <summary>
    /// Layout width in device-independent pixels. Null sizes a <see cref="System.Windows.UIElement"/> to its content;
    /// other visuals need an explicit size.
    /// </summary>
    public double? Width { get; init; }

    /// <summary>Layout height in device-independent pixels (see <see cref="Width"/>).</summary>
    public double? Height { get; init; }

    /// <summary>Fixed rendering DPI: 96 is 100 %, 120 is 125 %, 144 is 150 %. Never the machine's own scale.</summary>
    public double Dpi { get; init; } = 96;

    /// <summary>Color painted under the visual; null keeps the background transparent.</summary>
    public Color? Background { get; init; }

    /// <summary>Largest difference allowed in any channel (0–255) for a pixel to count as equal.</summary>
    public byte ChannelTolerance { get; init; } = 2;

    /// <summary>Largest share of different pixels allowed, in percent.</summary>
    public double MaxDifferentPixelsPercent { get; init; } = 0.5;
}
