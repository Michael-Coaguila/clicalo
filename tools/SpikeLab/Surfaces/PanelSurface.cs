using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// The panel of the laboratory: the fourteen tiles of <see cref="LabTiles.Panel"/> in four columns and, on its left,
/// the handle that drags it between monitors (S1 row 30), a named touch target of at least 44 logical pixels.
/// </summary>
internal sealed class PanelSurface : LabSurface
{
    /// <summary>Width of the handle, in logical pixels: a touch target of at least 44 (REG-02).</summary>
    public const double GripWidth = 48;

    private readonly DragGrip _handle;

    /// <summary>Creates the panel.</summary>
    public PanelSurface(SurfaceRegistry registry, LabSurfaceContext context)
        : base(LabSurfaceIds.Panel, registry, context, SurfaceGroup.Panel)
    {
        var grid = new UniformGrid { Columns = 4, Margin = new Thickness(4) };
        foreach (var tile in LabTiles.Panel)
        {
            grid.Children.Add(AddTile(tile, width: 104, height: 72));
        }

        _handle = new DragGrip("Asa del panel", GripWidth);

        var layout = new DockPanel();
        DockPanel.SetDock(_handle, System.Windows.Controls.Dock.Left);
        layout.Children.Add(_handle);
        layout.Children.Add(grid);
        Content = layout;
    }

    /// <summary>The handle (S1 row 30).</summary>
    public DragGrip Grip => _handle;

    /// <inheritdoc />
    protected override FrameworkElement DragHandle => _handle;
}
