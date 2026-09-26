using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Clicalo.Design.Math;

/// <summary>Linear-light sRGB (D65), channels nominally 0–1 but unclamped, with straight alpha.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct LinearSrgb : IEquatable<LinearSrgb>
{
    public LinearSrgb(double r, double g, double b, double alpha = 1d)
    {
        R = r;
        G = g;
        B = b;
        Alpha = alpha;
    }

    public double R { get; }

    public double G { get; }

    public double B { get; }

    public double Alpha { get; }

    public static bool operator ==(LinearSrgb left, LinearSrgb right) => left.Equals(right);

    public static bool operator !=(LinearSrgb left, LinearSrgb right) => !left.Equals(right);

    /// <summary>Applies the sRGB transfer function (CSS Color 4 <c>gam_sRGB</c>), extended symmetrically to negatives.</summary>
    public Srgb ToSrgb() =>
        new(
            TransferFunctions.Encode(R),
            TransferFunctions.Encode(G),
            TransferFunctions.Encode(B),
            Alpha
        );

    /// <summary>Linear sRGB → LMS → LMS′ (cube root) → OKLab, with Ottosson's published matrices.</summary>
    public Oklab ToOklab()
    {
        var l = (0.4122214708 * R) + (0.5363325363 * G) + (0.0514459929 * B);
        var m = (0.2119034982 * R) + (0.6806995451 * G) + (0.1073969566 * B);
        var s = (0.0883024619 * R) + (0.2817188376 * G) + (0.6299787005 * B);

        var lp = Cbrt(l);
        var mp = Cbrt(m);
        var sp = Cbrt(s);

        return new Oklab(
            (0.2104542553 * lp) + (0.7936177850 * mp) - (0.0040720468 * sp),
            (1.9779984951 * lp) - (2.4285922050 * mp) + (0.4505937099 * sp),
            (0.0259040371 * lp) + (0.7827717662 * mp) - (0.8086757660 * sp),
            Alpha
        );
    }

    public bool Equals(LinearSrgb other) =>
        R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && Alpha.Equals(other.Alpha);

    public override bool Equals(object? obj) => obj is LinearSrgb other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = R.GetHashCode();
            hash = (hash * 397) ^ G.GetHashCode();
            hash = (hash * 397) ^ B.GetHashCode();
            return (hash * 397) ^ Alpha.GetHashCode();
        }
    }

    public override string ToString() =>
        string.Format(
            CultureInfo.InvariantCulture,
            "srgb-linear({0:R} {1:R} {2:R} / {3:R})",
            R,
            G,
            B,
            Alpha
        );

    // Math.Cbrt does not exist in netstandard2.0; Pow on the magnitude keeps the sign of negative inputs.
    private static double Cbrt(double value) =>
        value < 0d ? -System.Math.Pow(-value, 1d / 3d) : System.Math.Pow(value, 1d / 3d);
}
