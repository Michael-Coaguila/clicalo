using System.Runtime.InteropServices;

namespace Clicalo.Domain.Dimming;

/// <summary>Everything <see cref="DimPolicy"/> needs, as values (blueprint §6.4).</summary>
/// <param name="AutoDim">The «Dim when not in use» setting (GEN-009).</param>
/// <param name="Opacity">The opacity setting, 0.30 to 1.00.</param>
/// <param name="DimTo">The dimmed opacity setting, 0.10 to 0.80.</param>
/// <param name="Surface">The surface whose opacity is decided.</param>
/// <param name="PointerInside">Whether a finger, the pen or the mouse pointer is on the surface.</param>
/// <param name="LastLeave">
/// When the finger or the pointer last left the surface, or when it appeared; null while it has never been left, and
/// then it stays awake.
/// </param>
/// <param name="Active">What is open or on that keeps every surface from dimming.</param>
/// <param name="ReduceMotion">The «Reduce motion» setting or Windows animations off (TEM-006).</param>
/// <param name="HighContrast">A contrast theme is on: no translucency at all (PAN-003, TEM-004).</param>
/// <param name="Now">The current time.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DimInputs(
    bool AutoDim,
    double Opacity,
    double DimTo,
    DimSurface Surface,
    bool PointerInside,
    DateTimeOffset? LastLeave,
    DimExceptions Active,
    bool ReduceMotion,
    bool HighContrast,
    DateTimeOffset Now
);
