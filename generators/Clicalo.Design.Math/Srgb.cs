using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Clicalo.Design.Math;

/// <summary>Gamma-encoded sRGB with channels nominally 0–1 (unclamped until <see cref="Clip"/>) and straight alpha.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct Srgb : IEquatable<Srgb>
{
    public Srgb(double r, double g, double b, double alpha = 1d)
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

    public static bool operator ==(Srgb left, Srgb right) => left.Equals(right);

    public static bool operator !=(Srgb left, Srgb right) => !left.Equals(right);

    /// <summary>True when every color channel lies in [0, 1] (exact, as the CSS Color 4 gamut check).</summary>
    public bool IsInGamut() => IsInRange(R) && IsInRange(G) && IsInRange(B);

    /// <summary>Clamps every channel (and alpha) to [0, 1]: the CSS Color 4 <c>clip</c> operation.</summary>
    public Srgb Clip() => new(Clamp01(R), Clamp01(G), Clamp01(B), Clamp01(Alpha));

    public LinearSrgb ToLinear() =>
        new(
            TransferFunctions.Decode(R),
            TransferFunctions.Decode(G),
            TransferFunctions.Decode(B),
            Alpha
        );

    public Oklab ToOklab() => ToLinear().ToOklab();

    /// <summary>Quantizes to 8 bits per channel (clamping first), rounding half away from zero.</summary>
    public Rgba8 ToRgba8() => new(ToByte(R), ToByte(G), ToByte(B), ToByte(Alpha));

    public bool Equals(Srgb other) =>
        R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && Alpha.Equals(other.Alpha);

    public override bool Equals(object? obj) => obj is Srgb other && Equals(other);

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
            "srgb({0:R} {1:R} {2:R} / {3:R})",
            R,
            G,
            B,
            Alpha
        );

    internal static byte ToByte(double channel) =>
        (byte)System.Math.Floor((Clamp01(channel) * 255d) + 0.5d);

    private static bool IsInRange(double channel) => channel is >= 0d and <= 1d;

    private static double Clamp01(double value) =>
        value < 0d ? 0d
        : value > 1d ? 1d
        : value;
}
