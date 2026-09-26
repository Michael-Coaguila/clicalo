using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Application.Ports;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// A surface that is only a row or a column of tiles: the edge bar («Pestaña»), its side window, the profile side
/// window and the bubble.
/// </summary>
internal sealed class TileStripSurface : LabSurface
{
    /// <summary>Creates the strip.</summary>
    /// <param name="id">Its identity.</param>
    /// <param name="registry">The surface registry.</param>
    /// <param name="context">What the surfaces share.</param>
    /// <param name="group">The group of the surface under test.</param>
    /// <param name="tiles">Its tiles.</param>
    /// <param name="orientation">Vertical for the edge bar, horizontal for the side windows.</param>
    /// <param name="tileWidth">Width of each tile, in logical pixels.</param>
    /// <param name="tileHeight">Height of each tile, in logical pixels.</param>
    public TileStripSurface(
        SurfaceId id,
        SurfaceRegistry registry,
        LabSurfaceContext context,
        SurfaceGroup group,
        ImmutableArray<LabTile> tiles,
        Orientation orientation,
        double tileWidth,
        double tileHeight
    )
        : base(id, registry, context, group)
    {
        var stack = new StackPanel { Orientation = orientation, Margin = new Thickness(2) };
        foreach (var tile in tiles)
        {
            stack.Children.Add(AddTile(tile, tileWidth, tileHeight));
        }

        Content = stack;
    }
}
