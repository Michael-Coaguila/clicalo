using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.EditMode;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.EditMode;

/// <summary>
/// The × of a tile in edit mode (CUA-012, CUA-013) in direction C (D12): a danger button drawn as a circle of
/// <see cref="IconBox.Medium"/> (the card with its line and the <c>close</c> glyph in <c>dangerText</c>) in the slot at
/// the top right of the tile, which gives up its type icon meanwhile, inside a 44 × 44 touch box (REG-02). Nine red
/// fills at once would take the alert out of red, so only the glyph is red. The first tap turns it into the
/// «[delConfirm]» pill for 3.5 s; the second deletes, or removes from Frequents. Its accessible name is «Eliminar
/// {name}» or «Quitar {name} de Frecuentes» and, while armed, its item status says [delConfirm]. It only projects
/// <see cref="EditModeViewModel"/> for one <see cref="TileViewModel"/>.
/// </summary>
/// <remarks>
/// The tile cell places it over the tile's top right slot and the panel registers <see cref="TapTarget"/> before the
/// tile itself, so a tap on the × never reaches the tile.
/// </remarks>
public sealed class TileRemoveButton : TouchButton
{
    /// <summary>How far the 44 × 44 box of the × reaches past its drawing on each side.</summary>
    public static readonly double Overflow = (TouchTarget.MinimumSize - IconBox.Medium) / 2;

    private readonly EditModeViewModel _editMode;
    private readonly TileViewModel _tile;
    private readonly ShortcutTile? _control;

    /// <summary>Creates the × of <paramref name="tile"/>.</summary>
    /// <param name="editMode">Edit mode.</param>
    /// <param name="tile">The tile it removes.</param>
    /// <param name="control">The tile's control, whose type slot the × takes while it shows.</param>
    public TileRemoveButton(
        EditModeViewModel editMode,
        TileViewModel tile,
        ShortcutTile? control = null
    )
    {
        ArgumentNullException.ThrowIfNull(editMode);
        ArgumentNullException.ThrowIfNull(tile);
        _editMode = editMode;
        _tile = tile;
        _control = control;
        Variant = ButtonVariant.Danger;
        Height = IconBox.Medium;
        MinWidth = IconBox.Medium;
        Padding = new Thickness(0);
        IconSize = IconBox.Badge;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;
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
        var shown = _editMode.ShowsRemove;
        Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        if (_control is not null)
        {
            TileFactory.HideType(_control, _tile, shown);
        }

        var armed = _editMode.Armed == _tile.Id;
        Symbol = armed ? null : "close";
        Content = armed ? _editMode.ConfirmText : null;
        Width = armed ? double.NaN : IconBox.Medium;
        Padding = armed ? new Thickness(Space.S2, 0, Space.S2, 0) : new Thickness(0);
        AutomationProperties.SetName(this, _editMode.RemoveNameOf(_tile.AccessibleName));
        AutomationProperties.SetItemStatus(this, armed ? _editMode.ConfirmText : string.Empty);
    }
}
