using System.Runtime.InteropServices;

namespace Clicalo.Domain.Geometry;

/// <summary>
/// A rectangle in physical screen pixels of the virtual desktop (blueprint §3.7): surface bounds, touch targets,
/// contact areas (<c>rcContact</c>) and the area the touch keyboard covers. The right and bottom edges are
/// exclusive, as in Win32 <c>RECT</c>.
/// </summary>
/// <param name="Left">Left edge, in physical pixels.</param>
/// <param name="Top">Top edge, in physical pixels.</param>
/// <param name="Width">Width, in physical pixels; never negative.</param>
/// <param name="Height">Height, in physical pixels; never negative.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PhysicalRect(int Left, int Top, int Width, int Height)
{
    /// <summary>The empty rectangle at the origin.</summary>
    public static PhysicalRect Empty => default;

    /// <summary>Exclusive right edge.</summary>
    public int Right => Left + Width;

    /// <summary>Exclusive bottom edge.</summary>
    public int Bottom => Top + Height;

    /// <summary>True when the rectangle covers no pixel.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>The center, rounded towards the top-left pixel.</summary>
    public PhysicalPoint Center => new(Left + (Width / 2), Top + (Height / 2));

    /// <summary>Creates a rectangle from its edges (right and bottom exclusive).</summary>
    public static PhysicalRect FromEdges(int left, int top, int right, int bottom) =>
        new(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));

    /// <summary>True when <paramref name="point"/> lies inside (right and bottom edges excluded).</summary>
    public bool Contains(PhysicalPoint point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    /// <summary>
    /// The rectangle grown by <paramref name="amount"/> pixels on every side (the extra hit area of TAC-002 and
    /// REG-02); a negative amount shrinks it, never below empty.
    /// </summary>
    public PhysicalRect Inflate(int amount) =>
        FromEdges(Left - amount, Top - amount, Right + amount, Bottom + amount);
}
