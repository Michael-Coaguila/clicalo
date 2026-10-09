using Clicalo.Domain.Geometry;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>A drag of a surface moved: how far the finger is from where it went down, in physical pixels.</summary>
/// <param name="offset">The offset.</param>
public sealed class SurfaceDragEventArgs(PhysicalOffset offset) : EventArgs
{
    /// <summary>The offset from where the finger went down.</summary>
    public PhysicalOffset Offset { get; } = offset;
}
