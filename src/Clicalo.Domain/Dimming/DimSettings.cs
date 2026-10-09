using System.Runtime.InteropServices;

namespace Clicalo.Domain.Dimming;

/// <summary>The settings <see cref="DimPolicy"/> obeys (GEN-009), as one value a surface keeps.</summary>
/// <param name="AutoDim">The «Dim when not in use» setting.</param>
/// <param name="Opacity">The opacity setting, 0.30 to 1.00.</param>
/// <param name="DimTo">The dimmed opacity setting, 0.10 to 0.80.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DimSettings(bool AutoDim, double Opacity, double DimTo);
