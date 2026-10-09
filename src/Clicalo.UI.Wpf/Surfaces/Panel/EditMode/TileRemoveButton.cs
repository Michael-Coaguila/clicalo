using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using Clicalo.Domain.Catalog;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.EditMode;
using Clicalo.UI.Wpf.Controls;

namespace Clicalo.UI.Wpf.Surfaces.Panel.EditMode;

/// <summary>
/// The red × of a tile in edit mode (CUA-012, CUA-013, prototype line 215): a 32 px <c>danger</c> button with the
/// <c>close</c> glyph at the tile's top right corner, 8 px outside it, inside a 44 × 44 touch box (REG-02). The first
/// tap turns it into the «[delConfirm]» pill for 3.5 s; the second deletes, or removes from Frequents. Its accessible
/// name is «Eliminar {name}» or «Quitar {name} de Frecuentes» and, while armed, its item status says [delConfirm]. It
/// only projects <see cref="EditModeViewModel"/> for one <see cref="TileViewModel"/>.
/// </summary>
/// <remarks>
/// The tile control places it in its top right corner (8 px above and to the right, the prototype's offset) and the
/// panel registers <see cref="TapTarget"/> before the tile itself, so a tap on the × never reaches the tile.
/// </remarks>
public sealed class TileRemoveButton : TouchButton
{
    /// <summary>The prototype draws the × 8 px outside the tile's corner.</summary>
    public const double Overhang = 8;

    private const double GlyphPx = 18;
    private const double ConfirmPx = 11;

    private readonly EditModeViewModel _editMode;
    private readonly TileViewModel _tile;

    /// <summary>Creates the × of <paramref name="tile"/>.</summary>
    /// <param name="editMode">Edit mode.</param>
    /// <param name="tile">The tile it removes.</param>
    public TileRemoveButton(EditModeViewModel editMode, TileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(editMode);
        ArgumentNullException.ThrowIfNull(tile);
        _editMode = editMode;
        _tile = tile;
        Appearance = ButtonAppearance.Danger;
        Height = PanelSizes.Layout.PanelEditDeleteButtonPx;
        MinWidth = PanelSizes.Layout.PanelEditDeleteButtonPx;
        Padding = new Thickness(6, 0, 6, 0);
        IconSize = GlyphPx;
        FontSize = ConfirmPx;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, -Overhang, -Overhang, 0);
        Focusable = false;
        IsTabStop = false;
        Click += (_, _) => Remove();
        _editMode.PropertyChanged += OnChanged;
        _tile.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <summary>The × as a tap target of the panel while it shows.</summary>
    public PanelTapTarget TapTarget => new(this, Remove);

    /// <summary>Stops following the view models when the tile goes away.</summary>
    public void Detach()
    {
        _editMode.PropertyChanged -= OnChanged;
        _tile.PropertyChanged -= OnChanged;
    }

    private void Remove() => _editMode.Remove(_tile.Id);

    private void OnChanged(object? sender, PropertyChangedEventArgs change) => Refresh();

    private void Refresh()
    {
        Visibility = _editMode.ShowsRemove ? Visibility.Visible : Visibility.Collapsed;
        var armed = _editMode.Armed == _tile.Id;
        Symbol = armed ? null : "close";
        Content = armed ? _editMode.ConfirmText : null;
        Width = armed ? double.NaN : PanelSizes.Layout.PanelEditDeleteButtonPx;
        AutomationProperties.SetName(this, _editMode.RemoveNameOf(_tile.AccessibleName));
        AutomationProperties.SetItemStatus(this, armed ? _editMode.ConfirmText : string.Empty);
    }
}
