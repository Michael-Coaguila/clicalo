using System.Runtime.InteropServices;

namespace Clicalo.Design.Math;

/// <summary>Result of <see cref="ContrastEvaluator.Measure"/>: the worst case over every backdrop.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct ContrastMeasurement
{
    public ContrastMeasurement(double ratio, Srgb foreground, Srgb background, int backdropIndex)
    {
        Ratio = ratio;
        Foreground = foreground;
        Background = background;
        BackdropIndex = backdropIndex;
    }

    /// <summary>WCAG contrast ratio, 1–21.</summary>
    public double Ratio { get; }

    /// <summary>The foreground as painted (composited over <see cref="Background"/>).</summary>
    public Srgb Foreground { get; }

    /// <summary>The fully composited background under the foreground.</summary>
    public Srgb Background { get; }

    /// <summary>
    /// Index of the backdrop that produced the worst case, or −1 when the stack is opaque or the worst case is a
    /// desktop between two backdrops (the foreground and the background reach the same luminance there).
    /// </summary>
    public int BackdropIndex { get; }
}
