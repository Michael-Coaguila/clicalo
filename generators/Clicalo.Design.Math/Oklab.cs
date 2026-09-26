using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Clicalo.Design.Math;

/// <summary>
/// A color in Björn Ottosson's OKLab space (the rectangular form of <see cref="Oklch"/>), with straight alpha.
/// </summary>
/// <remarks>
/// The matrices are the published ones from <see href="https://bottosson.github.io/posts/oklab/"/>, which
/// CSS Color 4 adopts: OKLab → LMS′ (M2⁻¹), cube, LMS → linear sRGB (M1⁻¹ composed with XYZ → sRGB).
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly struct Oklab : IEquatable<Oklab>
{
    private const double RadiansToDegrees = 180d / System.Math.PI;

    /// <summary>
    /// At or below this chroma the hue is powerless (the epsilon of the CSS Color 4 sample code for OKLab → OKLCH);
    /// it is reported as 0 because the rounded matrices leave grays with a chroma of about 1e-9.
    /// </summary>
    private const double AchromaticChroma = 0.000004;

    public Oklab(double l, double a, double b, double alpha = 1d)
    {
        L = l;
        A = a;
        B = b;
        Alpha = alpha;
    }

    /// <summary>Perceptual lightness, 0 (black) to 1 (white).</summary>
    public double L { get; }

    /// <summary>Green (negative) to red (positive) axis.</summary>
    public double A { get; }

    /// <summary>Blue (negative) to yellow (positive) axis.</summary>
    public double B { get; }

    /// <summary>Straight alpha, 0 (transparent) to 1 (opaque).</summary>
    public double Alpha { get; }

    public static bool operator ==(Oklab left, Oklab right) => left.Equals(right);

    public static bool operator !=(Oklab left, Oklab right) => !left.Equals(right);

    /// <summary>OKLab → LMS′ → LMS (cube) → linear sRGB. The result is unclamped (may be out of gamut).</summary>
    public LinearSrgb ToLinearSrgb()
    {
        var lp = L + (0.3963377774 * A) + (0.2158037573 * B);
        var mp = L - (0.1055613458 * A) - (0.0638541728 * B);
        var sp = L - (0.0894841775 * A) - (1.2914855480 * B);

        var l = lp * lp * lp;
        var m = mp * mp * mp;
        var s = sp * sp * sp;

        return new LinearSrgb(
            (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s),
            (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s),
            (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s),
            Alpha
        );
    }

    /// <summary>Convenience: OKLab → linear sRGB → gamma-encoded sRGB (unclamped).</summary>
    public Srgb ToSrgb() => ToLinearSrgb().ToSrgb();

    /// <summary>Rectangular to polar. Achromatic colors get hue 0, as CSS does for a missing hue.</summary>
    public Oklch ToOklch()
    {
        var chroma = System.Math.Sqrt((A * A) + (B * B));
        if (chroma <= AchromaticChroma)
        {
            return new Oklch(L, 0d, 0d, Alpha);
        }

        var hue = System.Math.Atan2(B, A) * RadiansToDegrees;
        return new Oklch(L, chroma, hue < 0d ? hue + 360d : hue, Alpha);
    }

    /// <summary>ΔEOK: the Euclidean distance in OKLab, as defined by CSS Color 4 (alpha is ignored).</summary>
    public double DistanceTo(Oklab other)
    {
        var dl = L - other.L;
        var da = A - other.A;
        var db = B - other.B;
        return System.Math.Sqrt((dl * dl) + (da * da) + (db * db));
    }

    public bool Equals(Oklab other) =>
        L.Equals(other.L) && A.Equals(other.A) && B.Equals(other.B) && Alpha.Equals(other.Alpha);

    public override bool Equals(object? obj) => obj is Oklab other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = L.GetHashCode();
            hash = (hash * 397) ^ A.GetHashCode();
            hash = (hash * 397) ^ B.GetHashCode();
            return (hash * 397) ^ Alpha.GetHashCode();
        }
    }

    public override string ToString() =>
        string.Format(
            CultureInfo.InvariantCulture,
            "oklab({0:R} {1:R} {2:R} / {3:R})",
            L,
            A,
            B,
            Alpha
        );
}
