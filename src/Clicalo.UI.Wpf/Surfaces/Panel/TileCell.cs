using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Surfaces.Panel.EditMode;
using Clicalo.UI.Wpf.Surfaces.Panel.TestMode;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// A tile with what the layers of the panel draw over it (docs/04 «Anatomía de un botón»): the test mode mark over the
/// whole tile (TAC-008), the slight outline of an ignored touch (TAC-003), the ring while its menu is open (CUA-014)
/// and, on the grid, the × of edit mode in its top right slot (CUA-012). Without layers it is the tile alone.
/// </summary>
internal sealed class TileCell
{
    private readonly Action? _detachMenu;

    private TileCell(
        FrameworkElement element,
        TestMarkBadge? badge,
        TileRemoveButton? remove,
        IgnoredTouchOutline? ignored = null,
        Action? detachMenu = null
    )
    {
        Element = element;
        Badge = badge;
        Remove = remove;
        Ignored = ignored;
        _detachMenu = detachMenu;
    }

    /// <summary>The outline of an ignored touch, with layers (TAC-003).</summary>
    public IgnoredTouchOutline? Ignored { get; }

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
        var ignored = new IgnoredTouchOutline(viewModel) { Margin = control.Margin };
        cell.Children.Add(ignored);
        var badge = new TestMarkBadge(layers.TestMode, viewModel.Id) { Margin = control.Margin };
        cell.Children.Add(badge);
        TileRemoveButton? remove = null;
        if (removable)
        {
            // The × takes the slot at the top right of the tile, Space.S1 inside its corner (direction C: never
            // outside the tile nor over its icon); its 44 × 44 box reaches past the drawing around it.
            remove = new TileRemoveButton(layers.EditMode, viewModel, control);
            remove.RestMargin = new Thickness(
                0,
                control.Margin.Top + Space.S1 - TileRemoveButton.Overflow,
                control.Margin.Right + Space.S1 - TileRemoveButton.Overflow,
                0
            );
            cell.Children.Add(remove);
        }

        // CUA-014: the tile whose menu is open keeps the ring, so the menu says which tile it belongs to.
        var menu = layers.Menu;
        void Ring() => control.IsExpanded = menu.IsOpen && menu.Shortcut == viewModel.Id;
        PropertyChangedEventHandler onMenu = (_, _) => Ring();
        menu.PropertyChanged += onMenu;
        Ring();
        return new TileCell(cell, badge, remove, ignored, () => menu.PropertyChanged -= onMenu);
    }

    /// <summary>Stops following the layers when the tile goes away.</summary>
    public void Detach()
    {
        Badge?.Detach();
        Remove?.Detach();
        Ignored?.Detach();
        _detachMenu?.Invoke();
    }
}
