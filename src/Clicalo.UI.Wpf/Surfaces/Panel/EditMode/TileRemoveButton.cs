using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.EditMode;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.EditMode;

/// <summary>
/// The × of a tile in edit mode (CUA-012, CUA-013) in direction C (D12): a danger button drawn as a circle of
/// <see cref="IconBox.Medium"/> (the card with its line and the <c>close</c> glyph in <c>dangerText</c>) in the slot at
/// the top right of the tile, which gives up its type icon meanwhile, inside a 44 × 44 touch box (REG-02). Nine red
/// fills at once would take the alert out of red, so only the glyph is red. The first tap turns it into the
/// «[delConfirm]» pill for 3.5 s, written in the font of the name of its tile and as high as its line of text needs
/// (CUA-011); the second deletes, or removes from Frequents. Its accessible name is «Eliminar
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

    // «Confirmar» in a line of its own font: the label of a button keeps the line of 18 of its style, which a larger
    // name overflows.
    private readonly TextBlock _confirm = new()
    {
        TextWrapping = TextWrapping.NoWrap,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextAlignment = TextAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private Thickness _restMargin;
    private bool _ready;

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
        if (control is not null)
        {
            // Armed, «Confirmar» is written in the font of the name of its tile (labelSmall in S), which follows the text
            // scale: the label of 14 of a button did not fit the narrowest tile and ended in «…».
            SetBinding(
                FontSizeProperty,
                new Binding(nameof(FontSize)) { Source = control, Mode = BindingMode.OneWay }
            );
        }

        Click += (_, _) => Remove();
        _editMode.PropertyChanged += OnChanged;
        _tile.PropertyChanged += OnChanged;
        _ready = true;
        Refresh();
    }

    /// <summary>
    /// Where the × sits on its tile, its 44 × 44 box reaching past its drawing (<see cref="Overflow"/>). Armed, the pill
    /// «Confirmar» is its own box and keeps inside the tile, <see cref="Space.S1"/> from its corner (D21 review: it
    /// reached 6 past the tile).
    /// </summary>
    public Thickness RestMargin
    {
        get => _restMargin;
        set
        {
            _restMargin = value;
            Refresh();
        }
    }

    /// <summary>The × as a tap target of the panel while it shows.</summary>
    public PanelTapTarget TapTarget => new(this, Remove);

    /// <summary>Stops following the view models when the tile goes away.</summary>
    public void Detach()
    {
        _editMode.PropertyChanged -= OnChanged;
        _tile.PropertyChanged -= OnChanged;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // The font of the name follows the text scale: the armed pill grows with it. The base control sets its font
        // before this one has its view models.
        if (e.Property == FontSizeProperty && _ready)
        {
            Refresh();
        }
    }

    /// <summary>
    /// The height of the armed pill: its line of text and <see cref="Stroke.Selected"/> above and below it, the room
    /// the largest name has in it at 100 % (16 in 24, L), and never less than the circle of the ×.
    /// </summary>
    private static double PillHeight(double fontSize) =>
        Math.Max(
            IconBox.Medium,
            Math.Ceiling((AppFonts.Ui.LineSpacing * fontSize) + (2 * Stroke.Selected))
        );

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
        _confirm.Text = _editMode.ConfirmText;
        Content = armed ? _confirm : null;
        Width = armed ? double.NaN : IconBox.Medium;

        // CUA-011: armed, the pill grows with its text, so a larger text stays inside it; its top keeps its place. Its
        // corners keep the radius of a button (Radii.Medium), which makes the 24 of the × a circle.
        var height = armed ? PillHeight(FontSize) : IconBox.Medium;
        Height = height;
        Padding = armed ? new Thickness(Space.S2, 0, Space.S2, 0) : new Thickness(0);
        Margin = armed
            ? new Thickness(
                _restMargin.Left,
                _restMargin.Top + ((height - IconBox.Medium) / 2),
                _restMargin.Right + Overflow,
                _restMargin.Bottom
            )
            : _restMargin;
        MaxWidth =
            armed && _control is { ActualWidth: > 0 } tile
                ? Math.Max(IconBox.Medium, tile.ActualWidth - (2 * Space.S1))
                : double.PositiveInfinity;
        AutomationProperties.SetName(this, _editMode.RemoveNameOf(_tile.AccessibleName));
        AutomationProperties.SetItemStatus(this, armed ? _editMode.ConfirmText : string.Empty);
    }
}
