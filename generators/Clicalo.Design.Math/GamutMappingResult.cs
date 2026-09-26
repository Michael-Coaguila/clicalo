using System.Runtime.InteropServices;

namespace Clicalo.Design.Math;

/// <summary>Outcome of <see cref="GamutMapping.ToSrgb"/>.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct GamutMappingResult
{
    public GamutMappingResult(Srgb color, bool wasInGamut, double deltaEok)
    {
        Color = color;
        WasInGamut = wasInGamut;
        DeltaEok = deltaEok;
    }

    /// <summary>The in-gamut sRGB color (channels within [0, 1]).</summary>
    public Srgb Color { get; }

    /// <summary>True when the original color was representable in sRGB without mapping.</summary>
    public bool WasInGamut { get; }

    /// <summary>ΔEOK between the original color and <see cref="Color"/>: how much the mapping changed it.</summary>
    public double DeltaEok { get; }
}
