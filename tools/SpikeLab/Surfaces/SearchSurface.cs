using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// The search of the laboratory (S4): a text field with its «Dictar» button (ACC-011, UIA010), the result «Resultado
/// Negrita» and «Cerrar búsqueda». It is the target of the <c>TextInput</c> lease: only while the lease lasts is it the
/// foreground window and does its field have the keyboard focus (BUS-002). The text is never read, only its length.
/// </summary>
internal sealed class SearchSurface : LabSurface
{
    private static readonly LabTile FieldTarget = new(
        "search-field",
        "Campo de búsqueda",
        string.Empty,
        CommandPattern.Invoke,
        LabAction.SearchField
    )
    {
        IsTestTarget = false,
    };

    private readonly TextBox _field;

    /// <summary>Creates the search surface.</summary>
    public SearchSurface(SurfaceRegistry registry, LabSurfaceContext context)
        : base(LabSurfaceIds.Search, registry, context, SurfaceGroup.Any)
    {
        var label = new TextBlock
        {
            Text = FieldTarget.Name,
            FontSize = 16,
            Margin = new Thickness(6, 6, 6, 2),
        };
        _field = new TextBox
        {
            MinWidth = 320,
            MinHeight = 48,
            FontSize = 22,
            Margin = new Thickness(6),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        // Its own name, which does not contain «Buscar»: «clic Buscar» (S4) always means the tile of the panel.
        AutomationProperties.SetName(_field, FieldTarget.Name);
        AutomationProperties.SetLabeledBy(_field, label);
        AddTarget(_field, FieldTarget);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(2),
        };
        foreach (var tile in LabTiles.SearchControls)
        {
            buttons.Children.Add(AddTile(tile, width: 150, height: 64));
        }

        var layout = new StackPanel();
        layout.Children.Add(label);
        layout.Children.Add(_field);
        layout.Children.Add(buttons);
        Content = layout;
    }

    /// <summary>Length of the text in the field (the text itself is never read: LOG-001).</summary>
    public int FieldLength => _field.Text.Length;

    /// <summary>Gives the field the keyboard focus (only effective while the lease makes this window the foreground).</summary>
    public void FocusField()
    {
        Keyboard.Focus(_field);
        _field.CaretIndex = _field.Text.Length;
    }

    /// <summary>Empties the field for the next cycle.</summary>
    public void Clear() => _field.Clear();
}
