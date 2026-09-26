using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// The panel of the laboratory: the fourteen tiles of <see cref="LabTiles.Panel"/> in four columns and, on its left,
/// the handle that drags it between monitors (S1 row 30).
/// </summary>
internal sealed class PanelSurface : LabSurface
{
    private readonly Border _handle;

    /// <summary>Creates the panel.</summary>
    public PanelSurface(SurfaceRegistry registry, LabSurfaceContext context)
        : base(LabSurfaceIds.Panel, registry, context, SurfaceGroup.Panel)
    {
        var grid = new UniformGrid { Columns = 4, Margin = new Thickness(4) };
        foreach (var tile in LabTiles.Panel)
        {
            grid.Children.Add(AddTile(tile, width: 104, height: 72));
        }

        _handle = new Border
        {
            Width = 36,
            Child = new TextBlock
            {
                Text = "⠿",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        _handle.SetResourceReference(Border.BackgroundProperty, SystemColors.ControlDarkBrushKey);
        AutomationProperties.SetName(_handle, "Asa del panel");

        var layout = new DockPanel();
        DockPanel.SetDock(_handle, System.Windows.Controls.Dock.Left);
        layout.Children.Add(_handle);
        layout.Children.Add(grid);
        Content = layout;
    }

    /// <inheritdoc />
    protected override FrameworkElement DragHandle => _handle;
}
