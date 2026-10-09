using System.Runtime.InteropServices;

namespace Clicalo.Domain.Geometry;

/// <summary>
/// A displacement in physical screen pixels: how far a dragging contact is from where it went down
/// (<c>Clicalo.Domain.Touch.DragTracker</c>).
/// </summary>
/// <param name="Dx">Horizontal offset; positive to the right.</param>
/// <param name="Dy">Vertical offset; positive downward.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PhysicalOffset(int Dx, int Dy);
