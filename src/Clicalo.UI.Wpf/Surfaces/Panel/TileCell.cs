using System.Windows;
using System.Windows.Controls;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Surfaces.Panel.EditMode;
using Clicalo.UI.Wpf.Surfaces.Panel.TestMode;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// A tile with what the layers of the panel draw over it (docs/04 «Anatomía de un botón»): the test mode mark over the
/// whole tile (TAC-008) and, on the grid, the red × of edit mode at its top right corner (CUA-012). Without layers it is
/// the tile alone.
/// </summary>
internal sealed class TileCell
{
    private TileCell(FrameworkElement element, TestMarkBadge? badge, TileRemoveButton? remove)
    {
        Element = element;
        Badge = badge;
        Remove = remove;
    }

    /// <summary>What goes into the grid or the row.</summary>
    public FrameworkElement Element { get; }

    /// <summary>The test mode mark, with layers.</summary>
    public TestMarkBadge? Badge { get; }

    /// <summary>The × of edit mode, on grid tiles with layers.</summary>
    public TileRemoveButton? Remove { get; }

    /// <summary>The × as a tap target while it shows.</summary>
    public PanelTapTarget? RemoveTarget =>
        Remove is { IsVisible: true } remove ? remove.TapTarget : null;

    /// <summary>Wraps a sized tile.</summary>
    /// <param name="control">The tile, already sized (its margin is half the gap).</param>
    /// <param name="viewModel">Its view model.</param>
    /// <param name="layers">The layers of the panel, or <see langword="null"/>.</param>
    /// <param name="removable">Whether the tile shows the × in edit mode (grid tiles only).</param>
    public static TileCell Create(
        ShortcutTile control,
        TileViewModel viewModel,
        PanelLayerModels? layers,
        bool removable
    )
    {
        if (layers is null)
        {
            return new TileCell(control, null, null);
        }

        var cell = new Grid();
        cell.Children.Add(control);
        var badge = new TestMarkBadge(layers.TestMode, viewModel.Id) { Margin = control.Margin };
        cell.Children.Add(badge);
        TileRemoveButton? remove = null;
        if (removable)
        {
            remove = new TileRemoveButton(layers.EditMode, viewModel);
            var inset = control.Margin.Top;
            remove.Margin = new Thickness(
                0,
                inset - TileRemoveButton.Overhang,
                control.Margin.Right - TileRemoveButton.Overhang,
                0
            );
            cell.Children.Add(remove);
        }

        return new TileCell(cell, badge, remove);
    }

    /// <summary>Stops following the layers when the tile goes away.</summary>
    public void Detach()
    {
        Badge?.Detach();
        Remove?.Detach();
    }
}
