using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Clicalo.Design.Math;

/// <summary>
/// A color in the OKLCH space of CSS Color 4: perceptual lightness (0–1), chroma (≥ 0),
/// hue in degrees and straight (non-premultiplied) alpha (0–1).
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct Oklch : IEquatable<Oklch>
{
    private const double DegreesToRadians = System.Math.PI / 180d;

    public Oklch(double l, double c, double h, double alpha = 1d)
    {
        L = l;
        C = c;
        H = h;
        Alpha = alpha;
    }

    /// <summary>Perceptual lightness, 0 (black) to 1 (white).</summary>
    public double L { get; }

    /// <summary>Chroma; 0 is achromatic. The sRGB gamut reaches about 0.37.</summary>
    public double C { get; }

    /// <summary>Hue angle in degrees; meaningless when <see cref="C"/> is 0.</summary>
    public double H { get; }

    /// <summary>Straight alpha, 0 (transparent) to 1 (opaque).</summary>
    public double Alpha { get; }

    public static bool operator ==(Oklch left, Oklch right) => left.Equals(right);

    public static bool operator !=(Oklch left, Oklch right) => !left.Equals(right);

    /// <summary>Polar to rectangular: a = C·cos(h), b = C·sin(h).</summary>
    public Oklab ToOklab()
    {
        var radians = H * DegreesToRadians;
        return new Oklab(L, C * System.Math.Cos(radians), C * System.Math.Sin(radians), Alpha);
    }

    /// <summary>Returns the same color with another chroma (used by gamut mapping).</summary>
    public Oklch WithChroma(double chroma) => new(L, chroma, H, Alpha);

    /// <summary>Returns the same color with another lightness (used to correct contrast keeping the hue).</summary>
    public Oklch WithLightness(double lightness) => new(lightness, C, H, Alpha);

    public bool Equals(Oklch other) =>
        L.Equals(other.L) && C.Equals(other.C) && H.Equals(other.H) && Alpha.Equals(other.Alpha);

    public override bool Equals(object? obj) => obj is Oklch other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = L.GetHashCode();
            hash = (hash * 397) ^ C.GetHashCode();
            hash = (hash * 397) ^ H.GetHashCode();
            return (hash * 397) ^ Alpha.GetHashCode();
        }
    }

    /// <summary>CSS serialization, for example <c>oklch(0.8 0.11 200 / 0.16)</c>.</summary>
    public override string ToString()
    {
        var core = string.Format(CultureInfo.InvariantCulture, "oklch({0:R} {1:R} {2:R}", L, C, H);
        return Alpha >= 1d
            ? core + ")"
            : core + string.Format(CultureInfo.InvariantCulture, " / {0:R})", Alpha);
    }
}
