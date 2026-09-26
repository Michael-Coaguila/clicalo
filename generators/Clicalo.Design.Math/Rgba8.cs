using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Clicalo.Design.Math;

/// <summary>
/// An 8-bit-per-channel sRGB color with straight alpha: exactly what a WPF <c>Color</c> stores and renders.
/// Contrast is always measured on this quantized form, so the check covers what actually ships.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct Rgba8 : IEquatable<Rgba8>
{
    public Rgba8(byte r, byte g, byte b, byte a = byte.MaxValue)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public byte R { get; }

    public byte G { get; }

    public byte B { get; }

    public byte A { get; }

    /// <summary>True when the color fully hides what lies below it.</summary>
    public bool IsOpaque => A == byte.MaxValue;

    public static bool operator ==(Rgba8 left, Rgba8 right) => left.Equals(right);

    public static bool operator !=(Rgba8 left, Rgba8 right) => !left.Equals(right);

    public Srgb ToSrgb() => new(R / 255d, G / 255d, B / 255d, A / 255d);

    public Oklab ToOklab() => ToSrgb().ToOklab();

    /// <summary><c>#RRGGBB</c>, ignoring alpha.</summary>
    public string ToRgbHex() =>
        string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", R, G, B);

    /// <summary><c>#AARRGGBB</c>, the order used by WPF's <c>Color.ToString()</c> and <c>Color.FromArgb</c>.</summary>
    public string ToArgbHex() =>
        string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", A, R, G, B);

    public bool Equals(Rgba8 other) => R == other.R && G == other.G && B == other.B && A == other.A;

    public override bool Equals(object? obj) => obj is Rgba8 other && Equals(other);

    public override int GetHashCode() => (A << 24) | (R << 16) | (G << 8) | B;

    public override string ToString() => ToArgbHex();
}
