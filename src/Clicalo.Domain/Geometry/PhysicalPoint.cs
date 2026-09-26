using System.Runtime.InteropServices;

namespace Clicalo.Domain.Geometry;

/// <summary>
/// A point in physical screen pixels of the virtual desktop, as Win32 reports it for a per-monitor DPI aware
/// process (<c>POINTER_INFO.ptPixelLocation</c>, <c>GetCursorPos</c>). Never scaled to logical units (blueprint §3.7).
/// </summary>
/// <param name="X">Horizontal coordinate, in physical pixels; negative on monitors left of the primary one.</param>
/// <param name="Y">Vertical coordinate, in physical pixels; negative on monitors above the primary one.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PhysicalPoint(int X, int Y)
{
    /// <summary>Euclidean distance to <paramref name="other"/>, in physical pixels.</summary>
    public double DistanceTo(PhysicalPoint other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
