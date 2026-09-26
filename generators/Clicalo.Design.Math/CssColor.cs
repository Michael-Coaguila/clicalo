using System.Runtime.InteropServices;

namespace Clicalo.Design.Math;

/// <summary>A color parsed from the token data, in the notation it was written.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct CssColor
{
    private CssColor(CssColorNotation notation, Oklch oklch, Rgba8 hex)
    {
        Notation = notation;
        Oklch = oklch;
        Hex = hex;
    }

    public CssColorNotation Notation { get; }

    /// <summary>The OKLCH value; meaningful only when <see cref="Notation"/> is <see cref="CssColorNotation.Oklch"/>.</summary>
    public Oklch Oklch { get; }

    /// <summary>The 8-bit value; meaningful only when <see cref="Notation"/> is <see cref="CssColorNotation.Hex"/>.</summary>
    public Rgba8 Hex { get; }

    public static CssColor FromOklch(Oklch value) => new(CssColorNotation.Oklch, value, default);

    public static CssColor FromHex(Rgba8 value) => new(CssColorNotation.Hex, default, value);

    /// <summary>Converts to sRGB, gamut-mapping OKLCH colors that sRGB cannot display (CSS Color 4).</summary>
    public GamutMappingResult MapToSrgb() =>
        Notation == CssColorNotation.Oklch
            ? GamutMapping.ToSrgb(Oklch)
            : new GamutMappingResult(Hex.ToSrgb(), wasInGamut: true, deltaEok: 0d);

    /// <summary>The 8-bit color that is actually rendered.</summary>
    public Rgba8 ToRgba8() =>
        Notation == CssColorNotation.Oklch ? MapToSrgb().Color.ToRgba8() : Hex;

    public override string ToString() =>
        Notation == CssColorNotation.Oklch ? Oklch.ToString() : Hex.ToRgbHex();
}
